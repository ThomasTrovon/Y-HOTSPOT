using System.Diagnostics;
using System.Text.Json;
using HotspotApp.Localization;

namespace HotspotApp.Services.Infrastructure;

/// <summary>Journaled recovery invoked only after explicit UI confirmation.</summary>
public sealed class WslDnsRepairService(ManualHotspotSession session)
{
    private Process? worker;
    private readonly SemaphoreSlim gate = new(1, 1);
    // Single-file extraction changes AppContext.BaseDirectory; use the actual exe.
    public string DirectoryPath => HotspotApp.Logging.LogPaths.DirectoryPath;

    public async Task RepairAsync()
    {
        if (!await gate.WaitAsync(0)) throw new InvalidOperationException(AppStrings.Get("Text_WslDnsBusy"));
        IDisposable? maintenance = null;
        try
        {
            if (worker is { HasExited: false })
                throw new InvalidOperationException(AppStrings.Get("Text_WslDnsBusy"));
            maintenance = await session.EnterMaintenanceAsync();
            Directory.CreateDirectory(DirectoryPath);
            // Qualify the environment without interrupting a working session first.
            foreach (var inspectOnly in new[] { true, false })
            {
                if (!inspectOnly && session.OwnsSession) await session.StopAsync();
                var resultPath = Path.Combine(DirectoryPath, "dns-recovery-result.json");
                if (File.Exists(resultPath)) File.Delete(resultPath);
                worker?.Dispose();
                worker = await ElevatedMaintenance.StartAsync(inspectOnly ? "wsl-inspect" : "wsl-repair");
                try { await worker.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(4)); }
                catch (TimeoutException)
                {
                    // Killing the worker could interrupt restoration. Let it finish;
                    // keep its handle so another click cannot start a concurrent repair.
                    throw new TimeoutException(AppStrings.Format("Text_WslDnsTimeout", DirectoryPath));
                }
                if (!File.Exists(resultPath))
                    throw new IOException(AppStrings.Format("Text_WslDnsFailed", DirectoryPath));
                using var result = JsonDocument.Parse(await File.ReadAllTextAsync(resultPath));
                if (worker.ExitCode != 0 || !result.RootElement.GetProperty("Success").GetBoolean())
                    throw new IOException(AppStrings.Format("Text_WslDnsFailed", DirectoryPath));
            }
            using var state = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(DirectoryPath, "dns-recovery-state.json")));
            var upstream = Guid.Parse(state.RootElement.GetProperty("Upstream").GetString()!);
            await session.AdoptRecoveredAsync(upstream);
        }
        finally
        {
            if (maintenance != null && worker is { HasExited: false })
            {
                // UI timeout must not allow another hotspot start during restoration.
                var lease = maintenance;
                _ = ReleaseAfterExitAsync(worker, lease);
            }
            else maintenance?.Dispose();
            gate.Release();
        }
    }

    private static async Task ReleaseAfterExitAsync(Process process, IDisposable lease)
    {
        try { await process.WaitForExitAsync(); }
        finally { lease.Dispose(); }
    }
}
