using System.Diagnostics;

namespace HotspotApp.Services.Infrastructure;

public sealed class ProcessExecutablePathProvider
{
    public static string GetExecutablePath()
    {
        return Process.GetCurrentProcess().MainModule?.FileName
            ?? Environment.ProcessPath
            ?? string.Empty;
    }
}
