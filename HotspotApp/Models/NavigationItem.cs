using Microsoft.UI.Xaml.Controls;

namespace HotspotApp.Models;

public sealed class NavigationItem
{
    public string Label { get; init; } = string.Empty;
    public string IconGlyph { get; init; } = string.Empty;
    public Type PageType { get; init; } = typeof(Page);
}
