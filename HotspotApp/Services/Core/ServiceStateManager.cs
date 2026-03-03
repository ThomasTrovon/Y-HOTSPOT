using HotspotApp.Constants;
using HotspotApp.Helpers;
using HotspotApp.Services.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace HotspotApp.Services.Core;

public sealed class ServiceStateManager(
    ServiceManager serviceManager,
    HotspotOptimizer optimizer,
    ILogger<ServiceStateManager> logger
)
{
    public async Task ApplyAsync(bool wantsEnabled)
    {
        var exePath = ProcessExecutablePathProvider.GetExecutablePath();

        if (wantsEnabled)
        {
            var forceReinstall = false;

            var currentImagePath = await serviceManager.GetServiceImagePathAsync(
                AppConstants.AppName
            );
            var expectedImagePath = $"\"{exePath}\" --service";

            if (
                currentImagePath == null
                || !string.Equals(
                    currentImagePath,
                    expectedImagePath,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                forceReinstall = true;
            }

            await serviceManager.ApplyServiceStateAsync(
                AppConstants.AppName,
                true,
                () => exePath,
                forceReinstall
            );

            SetAutostart(true, exePath);
        }
        else
        {
            await serviceManager.ApplyServiceStateAsync(AppConstants.AppName, false, () => exePath);

            RemoveAutostart();
            RemoveAdapter();
            await optimizer.RestoreSystemSettingsAsync();
        }
    }

    public void SetAutostart(bool enable, string exePath)
    {
        try
        {
            const string runKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
            using var key = Registry.CurrentUser.OpenSubKey(runKey, true);
            if (enable)
            {
                if (!string.IsNullOrEmpty(exePath))
                {
                    key?.SetValue(AppConstants.AppName, $"\"{exePath}\" --tray");
                }
            }
            else
            {
                key?.DeleteValue(AppConstants.AppName, false);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AutostartUpdateFailed");
        }
    }

    public void RemoveAutostart()
    {
        SetAutostart(false, string.Empty);
    }

    public void RemoveAdapter()
    {
        try
        {
            var handle = Wintun.OpenAdapter(AppConstants.VirtualAdapterName);
            if (handle != IntPtr.Zero)
            {
                Wintun.CloseAdapter(handle);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RemoveAdapterFailed");
        }
    }
}
