namespace HotspotApp.Constants;

public static class RegistryConstants
{
    public const string NetworkClassGuid = "{4D36E972-E325-11CE-BFC1-08002BE10318}";
    public static string NetworkClassRegistryPath =>
        $"SYSTEM\\CurrentControlSet\\Control\\Class\\{NetworkClassGuid}";

    public const string IcsRegistryPath = @"SYSTEM\CurrentControlSet\Services\icssvc\Settings";

    // ICS Registry Values
    public const string RegPeerlessTimeoutEnabled = "PeerlessTimeoutEnabled";
    public const string RegPeerlessTimeout = "PeerlessTimeout";
    public const string RegPublicConnectionTimeout = "PublicConnectionTimeout";
    public const string RegWifiMaxPeers = "WifiMaxPeers";

    // Network Registry Values
    public const string RegPnPCapabilities = "PnPCapabilities";
    public const string RegNetCfgInstanceId = "NetCfgInstanceId";
}
