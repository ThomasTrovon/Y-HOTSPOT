using HotspotApp.Config;
using HotspotApp.Constants;
using HotspotApp.Localization;
using HotspotApp.Logging;
using HotspotApp.Models;
using HotspotApp.Services.Core;
using HotspotApp.Services.Infrastructure;
using HotspotApp.Services.UI;
using HotspotApp.Views;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;
using Windows.Networking.NetworkOperators;

namespace HotspotApp.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase, ViewModelBase.IInitializable
{
    private readonly DialogService _dialogService;
    private readonly ServiceClient _serviceClient;
    private readonly ServiceStateManager _serviceStateManager;
    private readonly FileExplorerService _fileExplorerService;
    private readonly ILogger<SettingsViewModel> _logger;
    private string _ssid = string.Empty;
    private string _passphrase = string.Empty;
    private SelectionOption<TetheringWiFiAuthenticationKind>? _selectedAuthentication;
    private SelectionOption<TetheringWiFiBand>? _selectedBand;
    private SelectionOption<LogLevelSetting>? _selectedLogLevel;
    private SelectionOption<DisconnectWirelessMode>? _selectedDisconnectMode;
    private double _maxClientCount = AppConstants.DefaultMaxClientCount;
    private bool _enableService;
    private bool _disableAutoConfig;
    private bool _forceLanFallback;

    private readonly ServiceManager _serviceManager;

    public SettingsViewModel(
        DialogService dialogService,
        ServiceClient serviceClient,
        ServiceStateManager serviceStateManager,
        ServiceManager serviceManager,
        FileExplorerService fileExplorerService,
        ILogger<SettingsViewModel> logger
    )
    {
        _dialogService = dialogService;
        _serviceClient = serviceClient;
        _serviceStateManager = serviceStateManager;
        _serviceManager = serviceManager;
        _fileExplorerService = fileExplorerService;
        _logger = logger;

        AuthenticationOptions = ConfigOptions.AuthenticationOptions;
        BandOptions = ConfigOptions.BandOptions;
        DisconnectOptions = ConfigOptions.DisconnectOptions;
        LogLevelOptions = ConfigOptions.LogLevelOptions;

        // Default selection
        _selectedAuthentication = AuthenticationOptions[0];
        _selectedBand = BandOptions[0];
        _selectedDisconnectMode = DisconnectOptions[0];
        _selectedLogLevel = LogLevelOptions[0];

        GeneratePasswordCommand = new AsyncRelayCommand(GeneratePasswordAsync);
        CopySsidCommand = new AsyncRelayCommand(CopySsidAsync);
        CopyPasswordCommand = new AsyncRelayCommand(CopyPasswordAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        ShowQrCodeCommand = new AsyncRelayCommand(ShowQrCodeAsync);
        ResetCommand = new AsyncRelayCommand(ResetAsync);
        OpenConfigCommand = new AsyncRelayCommand(OpenConfigAsync);
    }

    public IReadOnlyList<
        SelectionOption<TetheringWiFiAuthenticationKind>
    > AuthenticationOptions { get; }
    public IReadOnlyList<SelectionOption<TetheringWiFiBand>> BandOptions { get; }
    public IReadOnlyList<SelectionOption<DisconnectWirelessMode>> DisconnectOptions { get; }
    public IReadOnlyList<SelectionOption<LogLevelSetting>> LogLevelOptions { get; }

    public AsyncRelayCommand GeneratePasswordCommand { get; }
    public AsyncRelayCommand CopySsidCommand { get; }
    public AsyncRelayCommand CopyPasswordCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand ShowQrCodeCommand { get; }
    public AsyncRelayCommand ResetCommand { get; }
    public AsyncRelayCommand OpenConfigCommand { get; }

    public string Ssid
    {
        get => _ssid;
        set => SetProperty(ref _ssid, value);
    }

    public string Passphrase
    {
        get => _passphrase;
        set => SetProperty(ref _passphrase, value);
    }

    public SelectionOption<TetheringWiFiAuthenticationKind>? SelectedAuthentication
    {
        get => _selectedAuthentication;
        set => SetProperty(ref _selectedAuthentication, value);
    }

    public SelectionOption<TetheringWiFiBand>? SelectedBand
    {
        get => _selectedBand;
        set => SetProperty(ref _selectedBand, value);
    }

    public SelectionOption<DisconnectWirelessMode>? SelectedDisconnectMode
    {
        get => _selectedDisconnectMode;
        set => SetProperty(ref _selectedDisconnectMode, value);
    }

    public SelectionOption<LogLevelSetting>? SelectedLogLevel
    {
        get => _selectedLogLevel;
        set => SetProperty(ref _selectedLogLevel, value);
    }

    public double MaxClientCount
    {
        get => _maxClientCount;
        set => SetProperty(ref _maxClientCount, value);
    }

    public bool EnableService
    {
        get => _enableService;
        set => SetProperty(ref _enableService, value);
    }

    public bool DisableAutoConfig
    {
        get => _disableAutoConfig;
        set => SetProperty(ref _disableAutoConfig, value);
    }

    public bool ForceLanFallback
    {
        get => _forceLanFallback;
        set => SetProperty(ref _forceLanFallback, value);
    }

    public async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            await Task.WhenAll(LoadConfigAsync(), UpdateServiceStatusAsync());
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadConfigAsync()
    {
        HotspotConfig config;
        try
        {
            config = await ConfigManager.LoadConfigAsync();
        }
        catch
        {
            try
            {
                await _serviceStateManager.ApplyAsync(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup on config load error");
            }

            var shouldContinue = await _dialogService.ShowConfigLoadErrorAsync();
            if (!shouldContinue)
            {
                Application.Current.Exit();
                return;
            }

            ConfigManager.DeleteConfigFolder();

            config = await ConfigManager.LoadConfigAsync();
        }

        Ssid = config.Ssid;
        Passphrase = config.Passphrase;
        SelectedAuthentication = AuthenticationOptions.FirstOrDefault(option =>
            option.Value == config.AuthenticationKind
        );
        SelectedBand = BandOptions.FirstOrDefault(option => option.Value == config.Band);
        SelectedLogLevel = LogLevelOptions.FirstOrDefault(option =>
            option.Value == config.LogLevel
        );
        SelectedDisconnectMode = DisconnectOptions.FirstOrDefault(option =>
            option.Value == config.DisconnectMode
        );
        MaxClientCount = config.MaxClientCount;
        DisableAutoConfig = config.DisableAutoConfig;
        ForceLanFallback = config.ForceLanFallback;
    }

    public async Task RefreshServiceStatusAsync()
    {
        await UpdateServiceStatusAsync();
    }

    private async Task UpdateServiceStatusAsync()
    {
        try
        {
            EnableService = await _serviceManager.IsServiceInstalledAsync(AppConstants.AppName);
        }
        catch { }
    }

    private Task GeneratePasswordAsync()
    {
        Passphrase = HotspotConfig.GenerateRandomPassphrase();
        return Task.CompletedTask;
    }

    private Task CopySsidAsync() => CopyToClipboardAsync(Ssid);

    private Task CopyPasswordAsync() => CopyToClipboardAsync(Passphrase);

    private static Task CopyToClipboardAsync(string text)
    {
        var dataPackage = new DataPackage();
        dataPackage.SetText(text);
        Clipboard.SetContent(dataPackage);
        return Task.CompletedTask;
    }

    private async Task SaveAsync()
    {
        if (IsBusy)
            return;

        if (Passphrase.Length < 8)
        {
            await _dialogService.ShowErrorAsync(AppStrings.Get("Text_PassphraseInvalidError"));
            return;
        }

        if (MaxClientCount < 1 || MaxClientCount > 32)
        {
            await _dialogService.ShowErrorAsync(AppStrings.Get("Text_MaxClientCountInvalidError"));
            return;
        }

        IsBusy = true;
        SetGlobalProcessing(true);
        try
        {
            var config =
                await ConfigManager.SaveConfigAsync(BuildConfig())
                ?? throw new InvalidOperationException("Config save failed");

            AsyncFileLogger.CurrentLogLevel = config.LogLevel;

            await ApplyServiceStateChangeAsync(config);

            _serviceStateManager.SetAutostart(
                EnableService,
                ProcessExecutablePathProvider.GetExecutablePath()
            );

            if (EnableService)
            {
                await _serviceClient.WaitForHotspotActiveAsync();
            }

            var serviceStatus = await _serviceManager.GetServiceStatusAsync(AppConstants.AppName);
            if (serviceStatus != ServiceStatus.Running && EnableService)
            {
                _logger.LogInformation("ServiceStarting");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SettingsSaveFailed");
            UiExceptionService.HandleUnhandledException(
                ex,
                "SettingsSave",
                AppStrings.Get("Text_SettingsSaveFailed"),
                AppStrings.Get("Text_ErrorDialogTitle")
            );
        }
        finally
        {
            IsBusy = false;
            SetGlobalProcessing(false);
        }
    }

    private static void SetGlobalProcessing(bool processing)
    {
        if (App.CurrentWindow is MainWindow mainWindow)
        {
            mainWindow.SetProcessingState(processing);
        }
    }

    private async Task ShowQrCodeAsync()
    {
        if (IsBusy)
            return;

        var config = await ConfigManager.LoadConfigAsync();

        await _dialogService.ShowHotspotQrAsync(config.Ssid, config.Passphrase);
    }

    private async Task ApplyServiceStateChangeAsync(HotspotConfig config)
    {
        var isInstalled = await _serviceManager.IsServiceInstalledAsync(AppConstants.AppName);
        if (EnableService != isInstalled)
        {
            await _serviceStateManager.ApplyAsync(EnableService);
            isInstalled = EnableService;
        }

        if (isInstalled)
        {
            await _serviceClient.UpdateConfigAsync(config);
        }
    }

    private async Task ResetAsync()
    {
        var result = await _dialogService.ShowResetConfirmationAsync();

        if (result)
        {
            IsBusy = true;
            SetGlobalProcessing(true);
            try
            {
                // 1. Stop/Remove Service & Clean System
                await _serviceStateManager.ApplyAsync(false);

                // 2. Delete Config
                ConfigManager.DeleteConfigFolder();

                // 3. Close App
                Application.Current.Exit();
            }
            catch (Exception ex)
            {
                IsBusy = false;
                SetGlobalProcessing(false);
                UiExceptionService.HandleUnhandledException(
                    ex,
                    "AppReset",
                    AppStrings.Format("Text_ResetFailedFormat", ex.Message),
                    AppStrings.Get("Text_ErrorDialogTitle")
                );
            }
        }
    }

    private Task OpenConfigAsync()
    {
        try
        {
            var folder = ConfigManager.ConfigFolderPath;
            if (Directory.Exists(folder))
            {
                _fileExplorerService.OpenFolder(folder);
            }
        }
        catch { }
        return Task.CompletedTask;
    }

    private HotspotConfig BuildConfig()
    {
        var config = new HotspotConfig
        {
            Ssid = Ssid,
            Passphrase = Passphrase,
            DisableAutoConfig = DisableAutoConfig,
            ForceLanFallback = ForceLanFallback,
        };

        if (SelectedAuthentication?.Value is { } auth)
        {
            config.AuthenticationKind = auth;
        }

        if (SelectedBand?.Value is { } band)
        {
            config.Band = band;
        }

        if (SelectedDisconnectMode?.Value is { } disconnect)
        {
            config.DisconnectMode = disconnect;
        }

        if (MaxClientCount is >= 1 and <= 32)
        {
            config.MaxClientCount = (int)Math.Round(MaxClientCount);
        }

        if (SelectedLogLevel?.Value is { } logLevel)
        {
            config.LogLevel = logLevel;
        }

        return config;
    }
}
