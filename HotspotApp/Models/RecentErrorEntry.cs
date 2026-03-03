namespace HotspotApp.Models;

public sealed class RecentErrorEntry(string time, string message)
{
    public string Time { get; } = time;
    public string Message { get; } = message;
}
