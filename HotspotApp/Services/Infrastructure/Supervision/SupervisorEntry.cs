using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using HotspotApp.Config;
using HotspotApp.Logging;
using HotspotApp.Localization;

namespace HotspotApp.Services.Infrastructure.Supervision;

public static class SupervisorEntry
{
    public static void Run(string[] args)
    {
        // Mutex adquirido/liberado na mesma thread; independente do mutex da interface.
        using var mutex = new Mutex(false, @"Global\Y-HOTSPOT_Supervisor");
        bool held;
        try { held = mutex.WaitOne(0); }
        catch (AbandonedMutexException) { held = true; }
        if (!held) { Environment.ExitCode = 3; return; }
        try { RunAsync(args).GetAwaiter().GetResult(); }
        catch (Exception ex) { Environment.ExitCode = 2; ManualTestDiagnostics.Write("Supervisor: falha fatal; pendência preservada", ex); }
        finally { mutex.ReleaseMutex(); }
    }

    private static async Task RunAsync(string[] args)
    {
        if (args.Length != 4 || !Guid.TryParseExact(args[1], "N", out var nonce)
            || !int.TryParse(args[2], out var parentId) || !long.TryParse(args[3], out var ticks))
            throw new ArgumentException("Identidade do supervisor inválida.");
        using var parent = Process.GetProcessById(parentId);
        if (parent.StartTime.ToUniversalTime().Ticks != ticks || parent.HasExited)
            throw new InvalidOperationException("A interface de origem já encerrou.");
        var journal = Path.Combine(LogPaths.DirectoryPath, "supervisor-recovery.json");
        // The UI owns the same-user pipe ACL. The client-side CurrentUserOnly
        // check also requires matching elevation, which is intentionally different.
        using var pipe = new NamedPipeClientStream(".", "Y-HOTSPOT-" + nonce.ToString("N"),
            PipeDirection.InOut, PipeOptions.Asynchronous,
            System.Security.Principal.TokenImpersonationLevel.Identification);
        await pipe.ConnectAsync(10000);
        if (!PipePeer.GetNamedPipeServerProcessId(pipe.SafePipeHandle, out var peer) || peer != parentId)
            throw new IOException("Processo de origem do pipe não corresponde à interface.");
        global::WinRT.ComWrappersSupport.InitializeComWrappers();
        LanguagePreferences.Initialize();
        if (File.Exists(journal)) await RecoverInterruptedSessionAsync(journal);
        File.WriteAllText(journal, JsonSerializer.Serialize(new
        {
            SupervisorPid = Environment.ProcessId, ParentPid = parentId,
            Started = DateTimeOffset.Now, State = "Sessão sob supervisão; confirmar limpeza antes de remover este arquivo"
        }), new System.Text.UTF8Encoding(false));
        ManualTestDiagnostics.Write($"Supervisor iniciado: PID={Environment.ProcessId}; interface={parentId}");
        var backend = new Backend();
        var cleaned = await new SupervisorLoop(backend, TimeSpan.FromSeconds(60), ManualTestDiagnostics.Write)
            .RunAsync(pipe, parent.WaitForExitAsync());
        if (cleaned) File.Delete(journal);
        // Retornar do Main encerra este processo, sem matar a interface ou serviços Windows.
        Environment.ExitCode = cleaned ? 0 : 2;
    }

    private static async Task RecoverInterruptedSessionAsync(string journal)
    {
        // Never change adapter settings while a hotspot is still running.
        var profile = Windows.Networking.Connectivity.NetworkInformation.GetInternetConnectionProfile()
            ?? throw new InvalidOperationException("Cannot verify hotspot state without an upstream profile.");
        var manager = Windows.Networking.NetworkOperators.NetworkOperatorTetheringManager.CreateFromConnectionProfile(profile);
        if (manager.TetheringOperationalState != Windows.Networking.NetworkOperators.TetheringOperationalState.Off)
            throw new InvalidOperationException("Stop the hotspot before recovering the interrupted session.");

        await IntelChannelSession.RecoverPendingAsync();
        await DnsRecovery.RecoverPendingAsync();
        var dnsJournal = Path.Combine(LogPaths.DirectoryPath, "dns-backend-recovery.json");
        if (File.Exists(dnsJournal))
        {
            using var record = JsonDocument.Parse(await File.ReadAllTextAsync(dnsJournal));
            var name = record.RootElement.GetProperty("rule").GetString();
            const string prefix = "YHotspotDns-";
            if (name is null || !name.StartsWith(prefix, StringComparison.Ordinal)
                || !Guid.TryParseExact(name[prefix.Length..], "N", out _))
                throw new InvalidDataException("Invalid DNS firewall recovery rule.");
            dynamic policy = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2", throwOnError: true)!)!;
            foreach (var protocol in new[] { 6, 17 }) policy.Rules.Remove(name + "-" + protocol);
            File.Delete(dnsJournal);
        }
        File.Delete(journal);
        ManualTestDiagnostics.Write("Supervisor: sessão anterior restaurada antes da inicialização");
    }

    private sealed class Backend : ISupervisedHotspot
    {
        private readonly LocalHotspotSession session = new();
        public Task Failure => session.Failure;
        public bool OwnsSession => session.OwnsSession;
        public bool HasPending => session.HasPending;
        public void CancelPendingStart() => session.CancelPendingStart();
        public Task StartAsync(JsonElement config) => session.StartAsync(
            config.Deserialize<HotspotConfig>() ?? throw new InvalidOperationException("Configuração ausente."));
        public Task<string> ReadStatusAsync() => session.ReadStatusAsync();
        public Task AdoptRecoveredAsync(JsonElement config) => session.AdoptRecoveredAsync(
            config.Deserialize<HotspotConfig>()?.UpstreamAdapterId ?? throw new InvalidOperationException("Recovery upstream missing"));
        public Task StopAsync() => session.StopAsync();
    }
}
