namespace HotspotApp.Constants;

public static class ServiceConstants
{
    public const string PipeName = "YHotspotPipe";
    public const string PipeCommandGetStatus = "GET_STATUS";
    public const string PipeCommandUpdateConfig = "UPDATE_CONFIG";
    public const string PipeCommandRestartHotspot = "RESTART_HOTSPOT";

    public const string VirtualAdapterType = "LAN Fallback";

    public static readonly string[] RequiredServices =
    [
        "icssvc",
        "FDResPub",
        "SSDPSRV",
        "upnphost",
    ];
}
