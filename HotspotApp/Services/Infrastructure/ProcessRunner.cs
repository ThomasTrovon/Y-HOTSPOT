using System.Diagnostics;
using HotspotApp.Logging;
using Microsoft.Extensions.Logging;

namespace HotspotApp.Services.Infrastructure;

public interface IProcessRunner
{
    Task<string> RunAsync(string fileName, string arguments, bool asAdmin = false);
    Task RunAdminAsync(string fileName, string arguments, int timeoutMs = 10000);
    void StartDetached(string fileName, string arguments);
}

public sealed partial class ProcessRunner(ILogger<ProcessRunner> logger) : IProcessRunner
{
    public void StartDetached(string fileName, string arguments)
    {
        try
        {
            Process.Start(new ProcessStartInfo(fileName, arguments) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            LogStartDetachedFailed(ex, fileName, arguments);
        }
    }

    public async Task<string> RunAsync(string fileName, string arguments, bool asAdmin = false)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = asAdmin,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        if (asAdmin)
        {
            startInfo.Verb = "runas";
        }
        else
        {
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
        }

        try
        {
            if (asAdmin)
            {
                await Task.Run(() =>
                {
                    using var process = Process.Start(startInfo);
                    process?.WaitForExit();
                });
                return string.Empty;
            }
            else
            {
                using var process = Process.Start(startInfo);
                if (process == null)
                    return string.Empty;

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                return string.IsNullOrWhiteSpace(output) ? error : output;
            }
        }
        catch (Exception ex)
        {
            LogCommandExecutionFailed(ex, fileName, arguments);
            return string.Empty;
        }
    }

    public async Task RunAdminAsync(string fileName, string arguments, int timeoutMs = 10000)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = true,
            Verb = "runas",
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        try
        {
            // We use Task.Run because Process.WaitForExit with timeout is blocking
            await Task.Run(() =>
            {
                var process =
                    Process.Start(psi)
                    ?? throw new InvalidOperationException(
                        $"{fileName} command failed to start. Args: {arguments}"
                    );
                if (!process.WaitForExit(timeoutMs))
                {
                    try
                    {
                        process.Kill(true);
                    }
                    catch { }

                    throw new TimeoutException($"{fileName} command timed out. Args: {arguments}");
                }

                if (process.ExitCode != 0)
                {
                    var normalizedArgs = arguments.TrimStart();
                    // Ignore "service instance is already running" (1056) for 'sc start'
                    if (
                        fileName == "sc"
                        && process.ExitCode == 1056
                        && normalizedArgs.StartsWith("start", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        return;
                    }

                    // Ignore "The system was unable to find the specified registry key or value" (1) for 'reg delete'
                    if (
                        fileName == "reg"
                        && process.ExitCode == 1
                        && normalizedArgs.StartsWith("delete", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        return;
                    }

                    throw new InvalidOperationException(
                        $"{fileName} command failed with exit code {process.ExitCode}. Args: {arguments}"
                    );
                }
            });
        }
        catch (Exception ex)
        {
            LogAdminCommandFailed(ex, fileName, arguments);
            // Fallback logging to file in case ILogger fails or isn't enough
            try
            {
                var logPath = LogPaths.GetServiceLogPath();
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                File.AppendAllText(
                    logPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Admin command failed: {ex.Message}{Environment.NewLine}"
                );
            }
            catch { }

            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "StartDetachedFailed: {FileName} {Args}")]
    private partial void LogStartDetachedFailed(Exception ex, string fileName, string args);

    [LoggerMessage(Level = LogLevel.Error, Message = "CommandExecutionFailed: {FileName} {Args}")]
    private partial void LogCommandExecutionFailed(Exception ex, string fileName, string args);

    [LoggerMessage(Level = LogLevel.Error, Message = "AdminCommandFailed {FileName} {Args}")]
    private partial void LogAdminCommandFailed(Exception ex, string fileName, string args);
}
