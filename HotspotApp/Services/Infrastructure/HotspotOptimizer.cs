using System.Net.NetworkInformation;
using System.ServiceProcess;
using System.Text.Json;
using System.Text.RegularExpressions;
using HotspotApp.Config;
using HotspotApp.Constants;
using HotspotApp.Services.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Windows.Networking.NetworkOperators;

namespace HotspotApp.Services.Infrastructure;

public partial class HotspotOptimizer(
    ILogger<HotspotOptimizer> logger,
    IProcessRunner processRunner,
    ServiceManager serviceManager
)
{
    private const int CurrentSchemaVersion = 1;
    private readonly string _backupPath = Path.Combine(
        ConfigManager.ConfigFolderPath,
        AppConstants.SystemConfigBackupFileName
    );

    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public async Task OptimizeSystemForHotspotAsync(HotspotConfig config)
    {
        try
        {
            var existingBackup = await LoadBackupAsync();
            var wirelessAdapters = GetWirelessAdapters();

            if (existingBackup == null)
            {
                var backup = await CaptureBackupAsync(wirelessAdapters);
                await SaveBackupAsync(backup);
                existingBackup = backup;
            }

            if (existingBackup == null)
                return;

            // 1. Registry Settings
            SetIcsDword(RegistryConstants.RegPeerlessTimeoutEnabled, 0);

            foreach (var adapter in wirelessAdapters)
            {
                var regPath = GetAdapterRegistryKeyPath(adapter.Id);
                SetAdapterPnp(regPath, 24);
            }

            SetIcsDword(RegistryConstants.RegPeerlessTimeout, 120);
            SetIcsDword(RegistryConstants.RegPublicConnectionTimeout, 60);
            SetIcsDword(RegistryConstants.RegWifiMaxPeers, config.MaxClientCount);

            // 2. AutoConfig
            if (config.DisableAutoConfig)
            {
                foreach (var adapter in wirelessAdapters)
                {
                    if (adapter.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase))
                        continue;
                    await SetAutoConfigAsync(adapter.Name, false);
                }
            }

            // 3. Restart Services
            foreach (var service in ServiceConstants.RequiredServices)
            {
                try
                {
                    await serviceManager.RestartServiceAsync(service);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to restart service {Service}", service);
                }
            }

            // 4. TCP Settings
            await SetTcpResetAsync();
        }
        catch (Exception ex)
        {
            LogHotspotOptimizationFailed(ex);
        }
    }

    public async Task RestoreSystemSettingsAsync()
    {
        if (!File.Exists(_backupPath))
            return;

        try
        {
            var backup = await LoadBackupAsync();
            if (backup == null)
                return;

            // 1. Registry
            SetIcsDword(RegistryConstants.RegPeerlessTimeoutEnabled, backup.PeerlessTimeoutEnabled);
            SetIcsDword(RegistryConstants.RegPeerlessTimeout, backup.PeerlessTimeout);
            SetIcsDword(
                RegistryConstants.RegPublicConnectionTimeout,
                backup.PublicConnectionTimeout
            );
            SetIcsDword(RegistryConstants.RegWifiMaxPeers, backup.WifiMaxPeers);

            var wirelessAdapters = GetWirelessAdapters();
            foreach (var adapter in wirelessAdapters)
            {
                if (backup.Adapters.TryGetValue(adapter.Id, out var pnpValue))
                {
                    var regPath = GetAdapterRegistryKeyPath(adapter.Id);
                    SetAdapterPnp(regPath, pnpValue);
                }
            }

            // 2. AutoConfig
            foreach (var adapter in wirelessAdapters)
            {
                var enabled =
                    backup.AutoConfigEnabled == null
                    || !backup.AutoConfigEnabled.TryGetValue(adapter.Id, out var e)
                    || e;
                await SetAutoConfigAsync(adapter.Name, enabled);
            }

            // 3. TCP
            if (
                backup.TcpGlobalSettings != null
                && !string.IsNullOrWhiteSpace(backup.TcpGlobalSettings.AutoTuningLevel)
            )
            {
                await SetTcpTuningAsync(backup.TcpGlobalSettings.AutoTuningLevel);
            }
            else
            {
                await SetTcpResetAsync();
            }

            DeleteBackupFile();
        }
        catch (Exception ex)
        {
            LogHotspotRestoreFailed(ex);
        }
    }

    public async Task DisconnectWirelessOnBandAsync(TetheringWiFiBand band)
    {
        if (band == TetheringWiFiBand.Auto)
            return;
        try
        {
            var interfaces = await GetWirelessInterfaceStatusAsync();
            foreach (
                var i in interfaces.Where(i =>
                    i.Connected && !IsHotspotAdapter(i) && IsBandMatch(i.Band, band)
                )
            )
            {
                await DisconnectInterfaceAsync(i.Name);
            }
        }
        catch (Exception ex)
        {
            LogHotspotDisconnectBandFailed(ex);
        }
    }

    public async Task DisconnectAllWirelessAsync()
    {
        try
        {
            var interfaces = await GetWirelessInterfaceStatusAsync();
            foreach (var i in interfaces.Where(i => i.Connected && !IsHotspotAdapter(i)))
            {
                await DisconnectInterfaceAsync(i.Name);
            }
        }
        catch (Exception ex)
        {
            LogHotspotDisconnectAllFailed(ex);
        }
    }

    public async Task EnableWirelessAutoConfigAsync()
    {
        try
        {
            var adapters = GetWirelessAdapters();
            foreach (var adapter in adapters)
            {
                await SetAutoConfigAsync(adapter.Name, true);
            }
        }
        catch (Exception ex)
        {
            LogEnableAutoConfigFailed(ex);
            throw;
        }
    }

    public async Task ResetHotspotRegistrySettingsAsync()
    {
        try
        {
            SetIcsDword(RegistryConstants.RegPeerlessTimeoutEnabled, null);
            SetIcsDword(RegistryConstants.RegPeerlessTimeout, null);
            SetIcsDword(RegistryConstants.RegPublicConnectionTimeout, null);
            SetIcsDword(RegistryConstants.RegWifiMaxPeers, null);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            LogResetRegistryFailed(ex);
            throw;
        }
    }

    public async Task ResetAdapterPowerSettingsAsync()
    {
        try
        {
            var adapters = GetWirelessAdapters();
            foreach (var adapter in adapters)
            {
                var regPath = GetAdapterRegistryKeyPath(adapter.Id);
                SetAdapterPnp(regPath, null);
            }
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            LogResetPowerSettingsFailed(ex);
            throw;
        }
    }

    public async Task ResetTcpGlobalSettingsAsync()
    {
        try
        {
            await SetTcpResetAsync();
        }
        catch (Exception ex)
        {
            LogResetTcpFailed(ex);
            throw;
        }
    }

    private void DeleteBackupFile()
    {
        try
        {
            if (File.Exists(_backupPath))
                File.Delete(_backupPath);
        }
        catch (Exception ex)
        {
            LogBackupFileDeleteFailed(ex);
        }
    }

    // --- Private Helpers ---

    private void SetIcsDword(string valueName, int? value)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                RegistryConstants.IcsRegistryPath,
                true
            );
            if (key == null)
                return;

            if (value.HasValue)
            {
                key.SetValue(valueName, value.Value, RegistryValueKind.DWord);
            }
            else
            {
                try
                {
                    key.DeleteValue(valueName);
                }
                catch (ArgumentException) { } // Ignore if missing
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to set ICS registry value {Name}", valueName);
        }
    }

    private void SetAdapterPnp(string? registryPath, int? value)
    {
        if (string.IsNullOrWhiteSpace(registryPath))
            return;

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(registryPath, true);
            if (key == null)
                return;

            if (value.HasValue)
            {
                key.SetValue(
                    RegistryConstants.RegPnPCapabilities,
                    value.Value,
                    RegistryValueKind.DWord
                );
            }
            else
            {
                try
                {
                    key.DeleteValue(RegistryConstants.RegPnPCapabilities);
                }
                catch (ArgumentException) { }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to set Adapter PnP value for {Path}", registryPath);
        }
    }

    private async Task SetAutoConfigAsync(string adapterName, bool enabled)
    {
        var enableStr = enabled ? "yes" : "no";
        await processRunner.RunAsync(
            "netsh",
            $"wlan set autoconfig enabled={enableStr} interface=\"{adapterName}\""
        );
    }

    private async Task SetTcpResetAsync()
    {
        await processRunner.RunAsync("netsh", "int tcp set global autotuninglevel=normal");
    }

    private async Task SetTcpTuningAsync(string level)
    {
        await processRunner.RunAsync("netsh", $"int tcp set global autotuninglevel={level}");
    }

    private async Task DisconnectInterfaceAsync(string interfaceName)
    {
        await processRunner.RunAsync("netsh", $"wlan disconnect interface=\"{interfaceName}\"");
    }

    private static List<NetworkInterface> GetWirelessAdapters()
    {
        return
        [
            .. NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211),
        ];
    }

    private static string? GetAdapterRegistryKeyPath(string adapterId)
    {
        using var classKey = Registry.LocalMachine.OpenSubKey(
            RegistryConstants.NetworkClassRegistryPath
        );
        if (classKey == null)
            return null;

        foreach (var subKeyName in classKey.GetSubKeyNames())
        {
            using var subKey = classKey.OpenSubKey(subKeyName);
            if (subKey == null)
                continue;

            if (
                string.Equals(
                    subKey.GetValue(RegistryConstants.RegNetCfgInstanceId) as string,
                    adapterId,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return $"{RegistryConstants.NetworkClassRegistryPath}\\{subKeyName}";
            }
        }
        return null;
    }

    private async Task<OptimizerBackup> CaptureBackupAsync(List<NetworkInterface> wirelessAdapters)
    {
        var backup = new OptimizerBackup();
        using var icsKey = Registry.LocalMachine.OpenSubKey(RegistryConstants.IcsRegistryPath);
        backup.PeerlessTimeoutEnabled = ReadDwordValue(
            icsKey,
            RegistryConstants.RegPeerlessTimeoutEnabled
        );
        backup.PeerlessTimeout = ReadDwordValue(icsKey, RegistryConstants.RegPeerlessTimeout);
        backup.PublicConnectionTimeout = ReadDwordValue(
            icsKey,
            RegistryConstants.RegPublicConnectionTimeout
        );
        backup.WifiMaxPeers = ReadDwordValue(icsKey, RegistryConstants.RegWifiMaxPeers);

        foreach (var adapter in wirelessAdapters)
        {
            var regPath = GetAdapterRegistryKeyPath(adapter.Id);
            if (regPath != null)
            {
                using var key = Registry.LocalMachine.OpenSubKey(regPath);
                if (key != null)
                    backup.Adapters[adapter.Id] = ReadDwordValue(
                        key,
                        RegistryConstants.RegPnPCapabilities
                    );
            }
        }

        var autoConfigStatus = await GetAutoConfigStatusAsync();
        foreach (var adapter in wirelessAdapters)
        {
            if (adapter.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase))
                continue;
            if (autoConfigStatus.TryGetValue(adapter.Name, out var enabled))
                backup.AutoConfigEnabled[adapter.Id] = enabled;
        }

        backup.TcpGlobalSettings = await ReadTcpGlobalSettingsAsync();
        return backup;
    }

    private static int? ReadDwordValue(RegistryKey? key, string name)
    {
        if (key?.GetValue(name) is int val)
            return val;
        return null;
    }

    private async Task<Dictionary<string, bool>> GetAutoConfigStatusAsync()
    {
        var output = await processRunner.RunAsync("netsh", "wlan show autoconfig");
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(output))
            return result;

        var regex = MyRegex();
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var match = regex.Match(line.Trim());
            if (match.Success)
                result[match.Groups["name"].Value] = match
                    .Groups["status"]
                    .Value.Equals("enabled", StringComparison.OrdinalIgnoreCase);
        }
        return result;
    }

    private async Task<List<WirelessInterfaceStatus>> GetWirelessInterfaceStatusAsync()
    {
        var output = await processRunner.RunAsync("netsh", "wlan show interfaces");
        var list = new List<WirelessInterfaceStatus>();
        if (string.IsNullOrWhiteSpace(output))
            return list;

        WirelessInterfaceStatus? current = null;
        foreach (var rawLine in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("Name", StringComparison.OrdinalIgnoreCase))
            {
                if (current != null && !string.IsNullOrWhiteSpace(current.Name))
                    list.Add(current);
                current = new WirelessInterfaceStatus { Name = ExtractValue(line) ?? string.Empty };
            }
            else if (current != null)
            {
                if (line.StartsWith("Description", StringComparison.OrdinalIgnoreCase))
                    current.Description = ExtractValue(line) ?? string.Empty;
                else if (line.StartsWith("State", StringComparison.OrdinalIgnoreCase))
                    current.Connected = (ExtractValue(line) ?? string.Empty).Equals(
                        "connected",
                        StringComparison.OrdinalIgnoreCase
                    );
                else if (line.StartsWith("Band", StringComparison.OrdinalIgnoreCase))
                    current.Band = ExtractValue(line);
            }
        }
        if (current != null && !string.IsNullOrWhiteSpace(current.Name))
            list.Add(current);
        return list;
    }

    private static bool IsBandMatch(string? bandText, TetheringWiFiBand band)
    {
        if (string.IsNullOrWhiteSpace(bandText))
            return false;
        return band switch
        {
            TetheringWiFiBand.TwoPointFourGigahertz => bandText.Contains(
                "2.4",
                StringComparison.OrdinalIgnoreCase
            ),
            TetheringWiFiBand.FiveGigahertz => bandText.Contains(
                '5',
                StringComparison.OrdinalIgnoreCase
            ),
            _ => false,
        };
    }

    private static bool IsHotspotAdapter(WirelessInterfaceStatus status)
    {
        return status.Description.Contains(
                "Microsoft Wi-Fi Direct Virtual Adapter",
                StringComparison.OrdinalIgnoreCase
            ) || status.Description.Contains("Hosted Network", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<OptimizerBackup?> LoadBackupAsync()
    {
        if (!File.Exists(_backupPath))
            return null;
        try
        {
            var content = await ReadFileWithRetryAsync(_backupPath);
            if (string.IsNullOrWhiteSpace(content))
                return null;

            var backup = JsonSerializer.Deserialize<OptimizerBackup>(content);
            if (backup == null || backup.SchemaVersion != CurrentSchemaVersion)
            {
                DeleteBackupFile();
                return null;
            }
            return backup;
        }
        catch (Exception ex)
        {
            LogBackupReadFailed(ex);
            return null;
        }
    }

    private async Task SaveBackupAsync(OptimizerBackup backup)
    {
        try
        {
            var json = JsonSerializer.Serialize(backup, _jsonOptions);
            Directory.CreateDirectory(Path.GetDirectoryName(_backupPath)!);
            await WriteFileWithRetryAsync(_backupPath, json);
        }
        catch (Exception ex)
        {
            LogBackupWriteFailed(ex);
        }
    }

    private static async Task<string> ReadFileWithRetryAsync(
        string path,
        int maxRetries = 5,
        int delayMs = 200
    )
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                using var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite
                );
                using var reader = new StreamReader(stream);
                return await reader.ReadToEndAsync();
            }
            catch (IOException)
            {
                await Task.Delay(delayMs);
            }
        }
        using var finalStream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite
        );
        using var finalReader = new StreamReader(finalStream);
        return await finalReader.ReadToEndAsync();
    }

    private static async Task WriteFileWithRetryAsync(
        string path,
        string content,
        int maxRetries = 5,
        int delayMs = 200
    )
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                using var stream = new FileStream(
                    path,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None
                );
                using var writer = new StreamWriter(stream);
                await writer.WriteAsync(content);
                return;
            }
            catch (IOException)
            {
                await Task.Delay(delayMs);
            }
        }
        using var finalStream = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None
        );
        using var finalWriter = new StreamWriter(finalStream);
        await finalWriter.WriteAsync(content);
    }

    private async Task<TcpGlobalSettingsBackup> ReadTcpGlobalSettingsAsync()
    {
        var output = await processRunner.RunAsync("netsh", "int tcp show global");
        var backup = new TcpGlobalSettingsBackup();
        if (string.IsNullOrWhiteSpace(output))
            return backup;

        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (
                trimmed.StartsWith(
                    "Receive Window Auto-Tuning Level",
                    StringComparison.OrdinalIgnoreCase
                )
            )
                backup.AutoTuningLevel = ExtractValue(trimmed)?.ToLowerInvariant();
        }
        return backup;
    }

    private static string? ExtractValue(string line)
    {
        var index = line.IndexOf(':');
        return (index < 0 || index + 1 >= line.Length) ? null : line[(index + 1)..].Trim();
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "HotspotOptimizationFailed")]
    private partial void LogHotspotOptimizationFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "HotspotRestoreFailed")]
    private partial void LogHotspotRestoreFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "HotspotDisconnectBandFailed")]
    private partial void LogHotspotDisconnectBandFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "HotspotDisconnectAllFailed")]
    private partial void LogHotspotDisconnectAllFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "EnableAutoConfigFailed")]
    private partial void LogEnableAutoConfigFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "ResetRegistryFailed")]
    private partial void LogResetRegistryFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "ResetPowerSettingsFailed")]
    private partial void LogResetPowerSettingsFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "ResetTcpFailed")]
    private partial void LogResetTcpFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "BackupFileDeleteFailed")]
    private partial void LogBackupFileDeleteFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "HotspotOptimizationBackupReadFailed")]
    private partial void LogBackupReadFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "HotspotOptimizationBackupWriteFailed")]
    private partial void LogBackupWriteFailed(Exception ex);

    private class OptimizerBackup
    {
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public int? PeerlessTimeoutEnabled { get; set; }
        public int? PeerlessTimeout { get; set; }
        public int? PublicConnectionTimeout { get; set; }
        public int? WifiMaxPeers { get; set; }
        public Dictionary<string, int?> Adapters { get; set; } = [];
        public Dictionary<string, bool> AutoConfigEnabled { get; set; } = [];
        public TcpGlobalSettingsBackup? TcpGlobalSettings { get; set; }
    }

    private class TcpGlobalSettingsBackup
    {
        public string? AutoTuningLevel { get; set; }
    }

    private sealed class WirelessInterfaceStatus
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Connected { get; set; }
        public string? Band { get; set; }
    }

    [GeneratedRegex(
        "Auto configuration logic is (?<status>enabled|disabled) on interface \"(?<name>[^\"]+)\"",
        RegexOptions.IgnoreCase,
        "en-001"
    )]
    private static partial Regex MyRegex();
}
