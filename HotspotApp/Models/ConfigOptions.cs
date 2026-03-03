using HotspotApp.Config;
using HotspotApp.Localization;
using Windows.Networking.NetworkOperators;

namespace HotspotApp.Models;

public static class ConfigOptions
{
    public static List<SelectionOption<TetheringWiFiAuthenticationKind>> AuthenticationOptions =>
        [
            new()
            {
                Value = TetheringWiFiAuthenticationKind.Wpa2,
                Label = AppStrings.Get("Text_SecurityWpa2"),
            },
            new()
            {
                Value = TetheringWiFiAuthenticationKind.Wpa3,
                Label = AppStrings.Get("Text_SecurityWpa3"),
            },
        ];

    public static List<SelectionOption<TetheringWiFiBand>> BandOptions =>
        [
            new() { Value = TetheringWiFiBand.Auto, Label = AppStrings.Get("Text_BandAuto") },
            new()
            {
                Value = TetheringWiFiBand.TwoPointFourGigahertz,
                Label = AppStrings.Get("Text_Band24"),
            },
            new() { Value = TetheringWiFiBand.FiveGigahertz, Label = AppStrings.Get("Text_Band5") },
        ];

    public static List<SelectionOption<DisconnectWirelessMode>> DisconnectOptions =>
        [
            new()
            {
                Value = DisconnectWirelessMode.None,
                Label = AppStrings.Get("Text_DisconnectModeNone"),
            },
            new()
            {
                Value = DisconnectWirelessMode.SameBand,
                Label = AppStrings.Get("Text_DisconnectModeSameBand"),
            },
            new()
            {
                Value = DisconnectWirelessMode.All,
                Label = AppStrings.Get("Text_DisconnectModeAll"),
            },
        ];

    public static List<SelectionOption<LogLevelSetting>> LogLevelOptions =>
        [
            new() { Value = LogLevelSetting.None, Label = AppStrings.Get("Text_LogDetailNone") },
            new()
            {
                Value = LogLevelSetting.Important,
                Label = AppStrings.Get("Text_LogDetailImportant"),
            },
            new() { Value = LogLevelSetting.All, Label = AppStrings.Get("Text_LogDetailAll") },
        ];
}
