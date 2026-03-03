using HotspotApp.Config;
using Microsoft.Extensions.Logging;

namespace HotspotApp.Logging;

public static class LoggingBootstrapper
{
    public static void ConfigureFileLogging(ILoggingBuilder builder, string logPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        builder.ClearProviders();
        builder.AddProvider(new AsyncFileLoggerProvider(logPath));
        builder.SetMinimumLevel(LogLevel.Trace);
    }

    public static async Task InitializeLogLevelAsync()
    {
        try
        {
            var config = await ConfigManager.LoadConfigAsync();
            AsyncFileLogger.CurrentLogLevel = config.LogLevel;
        }
        catch { }
    }
}
