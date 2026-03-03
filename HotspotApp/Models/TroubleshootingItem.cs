using HotspotApp.ViewModels;

namespace HotspotApp.Models;

public sealed class TroubleshootingItem
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string FixLabel { get; init; } = string.Empty;
    public AsyncRelayCommand FixCommand { get; init; } =
        new AsyncRelayCommand(() => Task.CompletedTask);
}
