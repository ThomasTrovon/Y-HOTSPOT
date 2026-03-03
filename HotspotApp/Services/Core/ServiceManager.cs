using System.ServiceProcess;
using HotspotApp.Constants;
using HotspotApp.Localization;
using HotspotApp.Models;
using HotspotApp.Services.Infrastructure;
using Microsoft.Win32;

namespace HotspotApp.Services.Core;

public sealed class ServiceManager(IProcessRunner processRunner)
{
    public async Task<bool> IsServiceInstalledAsync(string serviceName)
    {
        var status = await GetServiceStatusAsync(serviceName);
        return status != ServiceStatus.NotInstalled;
    }

    public Task<string?> GetServiceImagePathAsync(string serviceName)
    {
        return Task.Run(() =>
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(
                    RegistryHive.LocalMachine,
                    RegistryView.Registry64
                );
                using var key = baseKey.OpenSubKey(
                    $@"SYSTEM\CurrentControlSet\Services\{serviceName}"
                );
                return key?.GetValue("ImagePath") as string;
            }
            catch
            {
                return null;
            }
        });
    }

    public Task<ServiceStatus> GetServiceStatusAsync(string serviceName)
    {
        return Task.Run(() =>
        {
            try
            {
                using var sc = new ServiceController(serviceName);
                return sc.Status switch
                {
                    ServiceControllerStatus.Running => ServiceStatus.Running,
                    ServiceControllerStatus.Stopped => ServiceStatus.Stopped,
                    ServiceControllerStatus.StartPending => ServiceStatus.Running,
                    ServiceControllerStatus.StopPending => ServiceStatus.Stopped,
                    ServiceControllerStatus.Paused => ServiceStatus.Stopped,
                    ServiceControllerStatus.PausePending => ServiceStatus.Running,
                    ServiceControllerStatus.ContinuePending => ServiceStatus.Stopped,
                    _ => ServiceStatus.Unknown,
                };
            }
            catch
            {
                return ServiceStatus.NotInstalled;
            }
        });
    }

    public Task<string> GetServiceDisplayNameAsync(string serviceName)
    {
        return Task.Run(() =>
        {
            try
            {
                using var sc = new ServiceController(serviceName);
                return sc.DisplayName;
            }
            catch
            {
                return string.Empty;
            }
        });
    }

    public async Task ApplyServiceStateAsync(
        string serviceName,
        bool wantsEnabled,
        Func<string> exePathProvider,
        bool forceReinstall = false,
        string arguments = "--service"
    )
    {
        var isInstalled = await IsServiceInstalledAsync(serviceName);
        if (wantsEnabled)
        {
            if (isInstalled)
            {
                var currentDisplayName = await GetServiceDisplayNameAsync(serviceName);
                if (forceReinstall || currentDisplayName != AppConstants.AppDisplayName)
                {
                    await UninstallServiceAsync(serviceName);
                    isInstalled = false;
                }
            }

            if (!isInstalled)
            {
                var exePath = exePathProvider();
                if (!File.Exists(exePath))
                {
                    throw new FileNotFoundException(
                        AppStrings.Get("Text_ExecutableNotFoundMessage"),
                        exePath
                    );
                }

                await InstallServiceAsync(serviceName, exePath, arguments);
            }
            else
            {
                // Ensure it's running if installed and enabled requested
                await StartServiceAsync(serviceName);
            }
        }
        else if (isInstalled)
        {
            await UninstallServiceAsync(serviceName);
        }
    }

    public async Task RestartServiceAsync(string serviceName)
    {
        await Task.Run(() =>
        {
            using var sc = new ServiceController(serviceName);
            try
            {
                if (sc.Status != ServiceControllerStatus.Stopped)
                {
                    if (sc.CanStop)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                    }
                }
            }
            catch (Exception)
            {
                // If stopping fails, we'll try to ensure it's running anyway.
            }

            sc.Refresh();
            if (sc.Status == ServiceControllerStatus.Stopped)
            {
                sc.Start();
            }
            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
        });
    }

    private async Task StartServiceAsync(string serviceName)
    {
        try
        {
            await Task.Run(() =>
            {
                using var sc = new ServiceController(serviceName);
                if (sc.Status == ServiceControllerStatus.Stopped)
                {
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                }
            });
        }
        catch { }
    }

    private async Task InstallServiceAsync(string serviceName, string exePath, string arguments)
    {
        var binPath = string.IsNullOrEmpty(arguments)
            ? $"\\\"{exePath}\\\""
            : $"\\\"{exePath}\\\" {arguments}";
        var displayName = AppConstants.AppDisplayName;

        await processRunner.RunAdminAsync(
            "sc",
            $"create \"{serviceName}\" binPath= \"{binPath}\" start= auto DisplayName= \"{displayName}\""
        );
        await processRunner.RunAdminAsync(
            "sc",
            $"failure \"{serviceName}\" reset= 86400 actions= restart/60000/restart/60000/restart/60000"
        );
        await processRunner.RunAdminAsync("sc", $"start \"{serviceName}\"");
    }

    private async Task UninstallServiceAsync(string serviceName)
    {
        try
        {
            await processRunner.RunAdminAsync("sc", $"stop \"{serviceName}\"");
        }
        catch { }

        await processRunner.RunAdminAsync("sc", $"delete \"{serviceName}\"");

        // Wait for service to disappear
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (!await IsServiceInstalledAsync(serviceName))
            {
                return;
            }
            await Task.Delay(250);
        }
    }
}
