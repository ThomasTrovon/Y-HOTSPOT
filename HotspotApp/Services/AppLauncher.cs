using HotspotApp.Constants;
using HotspotApp.Helpers;
using HotspotApp.Localization;
using HotspotApp.Logging;
using HotspotApp.Services.UI;

namespace HotspotApp.Services;

public static class AppLauncher
{
    public static void Run(string[] args)
    {
        var serviceMode = args.Contains("--service") || !Environment.UserInteractive;
        var mutexName = serviceMode
            ? $"Global\\{AppConstants.AppName}_Service_Mutex"
            : $"Global\\{AppConstants.AppName}_UI_Mutex";

        using var guard = SingleInstanceGuard.Acquire(mutexName);
        if (!guard.HasHandle)
        {
            if (!serviceMode)
            {
                var msgId = WindowInterop.RegisterWindowMessage("WM_SHOW_HOTSPOT_APP");
                if (msgId != 0)
                {
                    WindowInterop.PostMessage(
                        WindowInterop.HwndBroadcast,
                        msgId,
                        IntPtr.Zero,
                        IntPtr.Zero
                    );
                }
            }
            return;
        }

        try
        {
            if (serviceMode)
            {
                ServiceLauncher.Run(args);
            }
            else
            {
                UiLauncher.Run(args);
            }
        }
        catch (Exception ex)
        {
            if (serviceMode)
            {
                var logPath = LogPaths.GetServiceLogPath();
                LogPaths.Append(
                    logPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {ex}{Environment.NewLine}"
                );
            }
            else
            {
                UiExceptionService.HandleUnhandledException(
                    ex,
                    "AppStartFailed",
                    AppStrings.Format(
                        "Text_AppStartFailedMessage",
                        AppConstants.AppDisplayName,
                        ex
                    ),
                    AppStrings.Get("Text_FatalErrorTitle")
                );
            }
        }
    }
}
