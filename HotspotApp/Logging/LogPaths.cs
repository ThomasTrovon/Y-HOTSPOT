using HotspotApp.Config;
using HotspotApp.Constants;

namespace HotspotApp.Logging;

public static class LogPaths
{
    public static string GetServiceLogPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            AppConstants.AppName,
            AppConstants.ServiceLogFileName
        );
    }

    public static string GetUiLogPath()
    {
        return Path.Combine(ConfigManager.ConfigFolderPath, AppConstants.UiLogFileName);
    }

    // Append content to a log file path, ensuring directory exists
    public static void Append(string path, string content)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.AppendAllText(path, content);
        }
        catch (Exception)
        {
            // Fail silently or maybe write to Debug
            System.Diagnostics.Debug.WriteLine($"Failed to write to log: {path}");
        }
    }
}
