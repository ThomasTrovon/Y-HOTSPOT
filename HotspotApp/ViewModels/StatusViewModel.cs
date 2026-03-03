using System.Collections.ObjectModel;
using HotspotApp.Constants;
using HotspotApp.Localization;
using HotspotApp.Models;
using HotspotApp.Services;
using HotspotApp.Services.Core;
using HotspotApp.Services.UI;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;

namespace HotspotApp.ViewModels;

public sealed partial class StatusViewModel(
    ServiceManager serviceManager,
    ServiceClient serviceClient,
    ErrorHistoryService errorHistoryService,
    ILogger<StatusViewModel> logger
) : ViewModelBase, ViewModelBase.IRunnable
{
    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    private CancellationTokenSource? _refreshCts;
    private static readonly System.Text.Json.JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
    };

    // State Properties
    private bool _isHotspotActive;
    public bool IsHotspotActive
    {
        get => _isHotspotActive;
        set
        {
            if (SetProperty(ref _isHotspotActive, value))
            {
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    private ServiceStatus _serviceStatus = ServiceStatus.Unknown;
    public ServiceStatus ServiceStatus
    {
        get => _serviceStatus;
        set
        {
            if (SetProperty(ref _serviceStatus, value))
            {
                OnPropertyChanged(nameof(ServiceStatusText));
            }
        }
    }

    public string StatusText =>
        IsHotspotActive
            ? AppStrings.Get("Text_StatusActive")
            : AppStrings.Get("Text_StatusStopped");

    public string ServiceStatusText =>
        ServiceStatus switch
        {
            ServiceStatus.Running => AppStrings.Get("Text_ServiceStatusRunning"),
            ServiceStatus.Stopped => AppStrings.Get("Text_ServiceStatusStopped"),
            ServiceStatus.NotInstalled => AppStrings.Get("Text_ServiceStatusNotInstalled"),
            ServiceStatus.Error => AppStrings.Get("Text_ServiceErrorMessage"),
            _ => AppStrings.Get("Text_StatusUnknown"),
        };

    private string _upstreamConnectionText = AppStrings.Get("Text_StatusUnknown");
    public string UpstreamConnectionText
    {
        get => _upstreamConnectionText;
        set => SetProperty(ref _upstreamConnectionText, value);
    }

    private int _clientCount;
    public int ClientCount
    {
        get => _clientCount;
        set
        {
            if (SetProperty(ref _clientCount, value))
            {
                OnPropertyChanged(nameof(ClientCountText));
            }
        }
    }

    private int _maxClientCount = AppConstants.DefaultMaxClientCount;
    public int MaxClientCount
    {
        get => _maxClientCount;
        set
        {
            if (SetProperty(ref _maxClientCount, value))
            {
                OnPropertyChanged(nameof(ClientCountText));
            }
        }
    }

    public string ClientCountText =>
        AppStrings.Format("Text_ClientCountFormat", ClientCount, MaxClientCount);

    public ObservableCollection<ConnectedDevice> Clients { get; } = [];

    // Diagnostics
    private string _lastUpdatedText = AppStrings.Get("Text_StatusUnknown");
    public string LastUpdatedText
    {
        get => _lastUpdatedText;
        set => SetProperty(ref _lastUpdatedText, value);
    }

    private string _statusSnapshotText = AppStrings.Get("Text_DiagnosticsNotAvailable");
    public string StatusSnapshotText
    {
        get => _statusSnapshotText;
        set => SetProperty(ref _statusSnapshotText, value);
    }

    public string ServiceHealthText => ServiceStatusText;

    public ReadOnlyObservableCollection<RecentErrorEntry> RecentErrors =>
        errorHistoryService.Errors;

    private int _runCount = 0;

    public async Task StartAsync()
    {
        _runCount++;
        if (_refreshCts != null)
            return;

        _refreshCts = new CancellationTokenSource();
        IsBusy = true;

        await RefreshAsync(_refreshCts.Token);

        IsBusy = false;
        _ = RunRefreshLoopAsync(_refreshCts.Token);
    }

    public void Stop()
    {
        _runCount--;
        if (_runCount > 0)
            return;

        // Ensure count doesn't go negative
        if (_runCount < 0)
            _runCount = 0;

        _refreshCts?.Cancel();
        _refreshCts = null;
    }

    private async Task RunRefreshLoopAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(token))
        {
            await RefreshAsync(token);
        }
    }

    private async Task RefreshAsync(CancellationToken token)
    {
        try
        {
            var statusTask = serviceClient.GetStatusAsync(token);
            var serviceStatusTask = serviceManager.GetServiceStatusAsync(AppConstants.AppName);

            await Task.WhenAll(statusTask, serviceStatusTask);

            var statusResponse = await statusTask;
            var serviceStatus = await serviceStatusTask;

            _dispatcherQueue.TryEnqueue(() => UpdateState(statusResponse, serviceStatus));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "Refresh failed");
            errorHistoryService.AddError(
                AppStrings.Format("Text_DiagnosticsStatusRefreshExceptionFormat", ex.Message)
            );
        }
    }

    private void UpdateState(
        ServiceResponse<HotspotStatusDto> statusResponse,
        ServiceStatus serviceStatus
    )
    {
        LastUpdatedText = DateTime.Now.ToString("G");

        // Service Status
        ServiceStatus = serviceStatus;

        // Hotspot Status
        if (statusResponse.Success && statusResponse.Data != null)
        {
            var data = statusResponse.Data;
            IsHotspotActive = data.IsActive;

            // Upstream
            var upstream = data.UpstreamConnectionProfile;
            if (!string.IsNullOrEmpty(upstream) && !data.HasInternetAccess)
            {
                upstream = AppStrings.Format("StatusPage_ConnectionNoInternetFormat", upstream);
            }
            UpstreamConnectionText = !string.IsNullOrEmpty(upstream)
                ? upstream
                : AppStrings.Get("Text_StatusUnknown");

            // Clients
            if (data.MaxClientCount > 0)
                MaxClientCount = data.MaxClientCount;

            ClientCount = data.Clients?.Count ?? 0;

            UpdateClientsList(data.Clients);

            StatusSnapshotText = System.Text.Json.JsonSerializer.Serialize(
                new
                {
                    data.IsActive,
                    data.Ssid,
                    data.Band,
                    data.AuthenticationKind,
                    data.UpstreamConnectionProfile,
                    data.HasInternetAccess,
                    data.MaxClientCount,
                    ClientCount = data.Clients?.Count ?? 0,
                },
                _jsonOptions
            );

            // Add any reported error to history if new
            if (!string.IsNullOrEmpty(data.LastError))
            {
                errorHistoryService.AddError(data.LastError);
            }
        }
        else
        {
            IsHotspotActive = false;
            if (statusResponse.Error != null)
            {
                StatusSnapshotText = statusResponse.Error;
                errorHistoryService.AddError(statusResponse.Error);
            }
        }
    }

    private void UpdateClientsList(List<ConnectedDevice>? newClients)
    {
        if (newClients == null)
        {
            Clients.Clear();
            return;
        }

        // Remove old
        for (int i = Clients.Count - 1; i >= 0; i--)
        {
            if (!newClients.Contains(Clients[i]))
            {
                Clients.RemoveAt(i);
            }
        }

        // Add new
        foreach (var client in newClients)
        {
            if (!Clients.Contains(client))
            {
                Clients.Add(client);
            }
        }
    }
}
