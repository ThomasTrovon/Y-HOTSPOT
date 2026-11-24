using System.Diagnostics;
using System.Reflection;
using System.Text;
using HotspotApp.Extensions;
using HotspotApp.Logging;
using HotspotApp.Services.Core;
using Microsoft.Extensions.DependencyInjection;

namespace HotspotApp.Services.Infrastructure;

internal static class ElevatedMaintenance
{
    public static async Task<Process> StartAsync(string action)
    {
        var start = new ProcessStartInfo(ProcessExecutablePathProvider.GetExecutablePath())
        {
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };
        start.ArgumentList.Add("--maintenance");
        start.ArgumentList.Add(action);
        return await Task.Run(() => Process.Start(start))
            ?? throw new IOException("Maintenance worker could not start.");
    }

    public static async Task ExecuteAsync(string action)
    {
        using var worker = await StartAsync(action);
        await worker.WaitForExitAsync();
        if (worker.ExitCode != 0)
            throw new IOException($"Maintenance failed ({worker.ExitCode}).");
    }

    public static void Run(string action)
    {
        try
        {
            if (action is "wsl-inspect" or "wsl-repair")
            {
                Environment.ExitCode = RunWslRecoveryAsync(action == "wsl-inspect").GetAwaiter().GetResult();
                return;
            }

            using var services = new ServiceCollection().AddLogging().AddSharedServices().BuildServiceProvider();
            var optimizer = services.GetRequiredService<HotspotOptimizer>();
            switch (action)
            {
                case "restore-service":
                    services.GetRequiredService<ServiceStateManager>().RemoveAdapter();
                    goto case "restore";
                case "restore":
                    optimizer.RestoreSystemSettingsAsync().GetAwaiter().GetResult();
                    break;
                case "remove-adapter":
                    services.GetRequiredService<ServiceStateManager>().RemoveAdapter();
                    break;
                case "enable-wlan":
                    optimizer.EnableWirelessAutoConfigAsync().GetAwaiter().GetResult();
                    break;
                case "reset-registry":
                    optimizer.ResetHotspotRegistrySettingsAsync().GetAwaiter().GetResult();
                    break;
                case "reset-power":
                    optimizer.ResetAdapterPowerSettingsAsync().GetAwaiter().GetResult();
                    break;
                case "reset-tcp":
                    optimizer.ResetTcpGlobalSettingsAsync().GetAwaiter().GetResult();
                    break;
                default:
                    throw new ArgumentException("Unknown maintenance action.", nameof(action));
            }
        }
        catch (Exception ex)
        {
            Environment.ExitCode = 1;
            ManualTestDiagnostics.Write("Maintenance worker failed", ex);
        }
    }

    private static async Task<int> RunWslRecoveryAsync(bool inspectOnly)
    {
        // Execute the embedded script directly so elevation never trusts a loose .ps1 in dist.
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("HotspotApp.RepairWslHotspotDns.ps1")
            ?? throw new IOException("Recovery script resource missing.");
        using var reader = new StreamReader(resource, Encoding.UTF8);
        var directory = LogPaths.DirectoryPath.Replace("'", "''");
        var script = "& {\n" + await reader.ReadToEndAsync() + "\n} -WorkDirectory '" + directory + "'"
            + (inspectOnly ? " -InspectOnly" : "");
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,
            "WindowsPowerShell", "v1.0", "powershell.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-EncodedCommand",
            Convert.ToBase64String(Encoding.Unicode.GetBytes(script)) })
            start.ArgumentList.Add(argument);
        using var worker = Process.Start(start) ?? throw new IOException("Recovery worker could not start.");
        await worker.WaitForExitAsync();
        return worker.ExitCode;
    }
}
