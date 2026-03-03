namespace HotspotApp.Models;

// Compact device record with equality for list comparisons and updates
public class ConnectedDevice : IEquatable<ConnectedDevice>
{
    public string MacAddress { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;

    public bool Equals(ConnectedDevice? other)
    {
        if (other is null)
            return false;
        return string.Equals(MacAddress, other.MacAddress, StringComparison.Ordinal)
            && string.Equals(HostName, other.HostName, StringComparison.Ordinal)
            && string.Equals(IpAddress, other.IpAddress, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => Equals(obj as ConnectedDevice);

    public override int GetHashCode() => HashCode.Combine(MacAddress, HostName, IpAddress);
}
