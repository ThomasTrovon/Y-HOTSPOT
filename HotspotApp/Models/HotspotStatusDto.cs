namespace HotspotApp.Models;

/// <summary>
/// Data Transfer Object for Hotspot Status.
/// </summary>
public class HotspotStatusDto
{
    public bool IsActive { get; set; }
    public List<ConnectedDevice> Clients { get; set; } = [];

    // Diagnostic / Extended Info
    public string? Ssid { get; set; }
    public string? Band { get; set; }
    public string? AuthenticationKind { get; set; }
    public string? UpstreamConnectionProfile { get; set; }
    public bool HasInternetAccess { get; set; }
    public int MaxClientCount { get; set; }
    public string? LastError { get; set; }
}
