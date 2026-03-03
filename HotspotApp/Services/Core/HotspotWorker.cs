using HotspotApp.Config;
using HotspotApp.Constants;
using HotspotApp.Logging;
using HotspotApp.Services.Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Windows.Networking.NetworkOperators;

namespace HotspotApp.Services.Core;

public partial class HotspotWorker : BackgroundService
{
    private readonly ILogger<HotspotWorker> _logger;
    private readonly HotspotManager _hotspotManager;
    private readonly IpcServer _ipcServer;
    private readonly RetryPolicy _retryPolicy;
    private readonly SemaphoreSlim _controlLock = new(1, 1);

    private CancellationTokenSource? _retryCts;
    private bool _isIntentionalStop;

    public HotspotWorker(
        ILogger<HotspotWorker> logger,
        HotspotManager hotspotManager,
        ILoggerFactory loggerFactory,
        RetryPolicy retryPolicy
    )
    {
        _logger = logger;
        _hotspotManager = hotspotManager;
        _retryPolicy = retryPolicy;

        // Pass the manager and the restart delegate to IPC server
        _ipcServer = new IpcServer(
            hotspotManager,
            loggerFactory.CreateLogger<IpcServer>(),
            RestartHotspotAsync
        );
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogServiceStarting(AppConstants.AppName);

        _hotspotManager.StatusChanged += OnStatusChanged;

        // Start IPC Server
        var ipcTask = _ipcServer.RunServerAsync(stoppingToken);

        // Start Hotspot Loop
        var hotspotTask = RunHotspotLoopAsync(stoppingToken);

        await Task.WhenAny(ipcTask, hotspotTask);

        LogServiceStopping(AppConstants.AppName);

        _retryCts?.Cancel();
        _hotspotManager.StatusChanged -= OnStatusChanged;

        await _hotspotManager.StopAsync(restoreSettings: true);
    }

    private async Task RunHotspotLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            _retryCts = CancellationTokenSource.CreateLinkedTokenSource(token);

            try
            {
                await _retryPolicy.ExecuteAsync(
                    async (ct) =>
                    {
                        if (_isIntentionalStop)
                        {
                            await Task.Delay(Timeout.Infinite, ct);
                            return;
                        }

                        if (!_hotspotManager.IsActive)
                        {
                            bool success = await StartHotspotInternalAsync();
                            if (!success)
                            {
                                throw new Exception("Hotspot start failed");
                            }
                        }

                        // Wait until cancelled
                        await Task.Delay(Timeout.Infinite, ct);
                    },
                    _retryCts.Token,
                    maxRetries: -1,
                    delaySeconds: 10
                );
            }
            catch (OperationCanceledException)
            {
                if (token.IsCancellationRequested)
                    break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hotspot loop failed");
                // Brief delay to prevent tight loop on error
                await Task.Delay(1000, token);
            }

            // Wait for lock to ensure we don't interfere with RestartHotspotAsync
            try
            {
                await _controlLock.WaitAsync(token);
                _controlLock.Release();
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task<bool> StartHotspotInternalAsync()
    {
        try
        {
            _hotspotManager.LastError = null;
            var config = await ConfigManager.LoadConfigAsync();
            AsyncFileLogger.CurrentLogLevel = config.LogLevel;

            LogHotspotStartAttempt(config.Ssid, config.AuthenticationKind, config.Band);

            if (await _hotspotManager.StartAsync(config))
            {
                return true;
            }

            LogHotspotStartFailed();
            if (_hotspotManager.LastError == null)
            {
                _hotspotManager.LastError = "Hotspot start failed (Unknown error)";
            }
            return false;
        }
        catch (Exception ex)
        {
            LogHotspotStartException(ex);
            _hotspotManager.LastError = $"Hotspot start failed: {ex.Message}";
            return false;
        }
    }

    private async Task<bool> RestartHotspotAsync(CancellationToken token)
    {
        await _controlLock.WaitAsync(token);
        try
        {
            _isIntentionalStop = true;
            _retryCts?.Cancel();

            await _hotspotManager.StopAsync(restoreSettings: false);

            _isIntentionalStop = false;
            return await StartHotspotInternalAsync();
        }
        finally
        {
            _controlLock.Release();
        }
    }

    private void OnStatusChanged(object? sender, TetheringOperationalState state)
    {
        if (_isIntentionalStop)
            return;

        LogHotspotStateChanged(state);

        if (
            state != TetheringOperationalState.On
            && state != TetheringOperationalState.InTransition
        )
        {
            LogHotspotStoppedExternally(state);

            // Trigger restart after a short delay to avoid rapid cycling
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(5000);
                    if (!_hotspotManager.IsActive && !_isIntentionalStop)
                    {
                        LogHotspotRestarting();
                        await RestartHotspotAsync(CancellationToken.None);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during auto-restart");
                }
            });
        }
    }

    public override void Dispose()
    {
        _controlLock.Dispose();
        _retryCts?.Dispose();
        _hotspotManager.Dispose();
        GC.SuppressFinalize(this);
        base.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "ServiceStarting {ServiceName}")]
    private partial void LogServiceStarting(string serviceName);

    [LoggerMessage(Level = LogLevel.Information, Message = "ServiceStopping {ServiceName}")]
    private partial void LogServiceStopping(string serviceName);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "HotspotStartAttempt {Ssid} {Auth} {Band}"
    )]
    private partial void LogHotspotStartAttempt(
        string ssid,
        TetheringWiFiAuthenticationKind auth,
        TetheringWiFiBand band
    );

    [LoggerMessage(Level = LogLevel.Warning, Message = "HotspotStartFailed")]
    private partial void LogHotspotStartFailed();

    [LoggerMessage(Level = LogLevel.Error, Message = "HotspotStartException")]
    private partial void LogHotspotStartException(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "HotspotStateChanged {State}")]
    private partial void LogHotspotStateChanged(TetheringOperationalState state);

    [LoggerMessage(Level = LogLevel.Warning, Message = "HotspotStoppedExternally {State}")]
    private partial void LogHotspotStoppedExternally(TetheringOperationalState state);

    [LoggerMessage(Level = LogLevel.Information, Message = "HotspotRestarting")]
    private partial void LogHotspotRestarting();
}
