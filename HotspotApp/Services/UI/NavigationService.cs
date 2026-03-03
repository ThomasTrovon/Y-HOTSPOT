using HotspotApp.Localization;
using HotspotApp.Models;
using HotspotApp.Views;
using Microsoft.UI.Xaml.Controls;

namespace HotspotApp.Services.UI;

public sealed class NavigationService
{
    public IReadOnlyList<NavigationItem> Items { get; }

    public NavigationService()
    {
        Items =
        [
            new()
            {
                Label = AppStrings.Get("Text_NavStatusLabel"),
                IconGlyph = "\uE88A",
                PageType = typeof(StatusPage),
            },
            new()
            {
                Label = AppStrings.Get("Text_NavDiagnosticsLabel"),
                IconGlyph = "\uE95E",
                PageType = typeof(DiagnosticsPage),
            },
            new()
            {
                Label = AppStrings.Get("Text_NavTroubleshootingLabel"),
                IconGlyph = "\uE90F",
                PageType = typeof(TroubleshootingPage),
            },
            new()
            {
                Label = AppStrings.Get("Text_NavSettingsLabel"),
                IconGlyph = "\uE713",
                PageType = typeof(SettingsPage),
            },
        ];
    }

    public static void Navigate(Frame frame, NavigationItem item)
    {
        frame.Navigate(item.PageType);
    }
}
