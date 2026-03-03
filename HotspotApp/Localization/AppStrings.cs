using Microsoft.Windows.ApplicationModel.Resources;

namespace HotspotApp.Localization;

public static class AppStrings
{
    private static readonly ResourceLoader _resourceLoader = new();

    public static string Get(string key)
    {
        try
        {
            var value = _resourceLoader.GetString(key);
            return string.IsNullOrEmpty(value) ? key : value;
        }
        catch
        {
            return key;
        }
    }

    public static string Format(string key, params object[] args)
    {
        var format = Get(key);
        return string.Format(format, args);
    }
}
