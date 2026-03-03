using HotspotApp.Constants;
using Windows.Networking.NetworkOperators;

namespace HotspotApp.Config;

public enum DisconnectWirelessMode
{
    None = 0,
    SameBand = 1,
    All = 2,
}

public enum LogLevelSetting
{
    None,
    Important,
    All,
}

/// <summary>
/// Configuration model for the Hotspot.
/// </summary>
public class HotspotConfig
{
    public int SchemaVersion { get; set; } = ConfigManager.CurrentSchemaVersion;

    public string Ssid { get; set; } = AppConstants.AppName;
    public string Passphrase { get; set; } = GenerateRandomPassphrase();
    public TetheringWiFiBand Band { get; set; } = TetheringWiFiBand.Auto;
    public TetheringWiFiAuthenticationKind AuthenticationKind { get; set; } =
        TetheringWiFiAuthenticationKind.Wpa3;
    public int MaxClientCount { get; set; } = AppConstants.DefaultMaxClientCount;
    public bool DisableAutoConfig { get; set; } = false;
    public bool ForceLanFallback { get; set; } = false;
    public DisconnectWirelessMode DisconnectMode { get; set; } = DisconnectWirelessMode.None;
    public LogLevelSetting LogLevel { get; set; } = LogLevelSetting.All;

    public static string GenerateRandomPassphrase()
    {
        const string chars =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
        return string.Create(
            8,
            chars,
            (span, charsStr) =>
            {
                for (var i = 0; i < span.Length; i++)
                {
                    span[i] = charsStr[Random.Shared.Next(charsStr.Length)];
                }
            }
        );
    }
}
