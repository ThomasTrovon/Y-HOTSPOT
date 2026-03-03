using HotspotApp.Localization;
using HotspotApp.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HotspotApp.Services.UI;

public static class UiExceptionService
{
    public static void HandleUnhandledException(
        Exception exception,
        string context,
        string message,
        string title
    )
    {
        WriteUiLog(exception, context);
        var window = App.CurrentWindow;
        if (window?.Content is not FrameworkElement root || root.XamlRoot == null)
        {
            return;
        }

        _ = window.DispatcherQueue.TryEnqueue(async () =>
        {
            ContentDialog dialog = new()
            {
                XamlRoot = root.XamlRoot,
                Title = title,
                Content = message,
                CloseButtonText = AppStrings.Get("Text_ErrorDialogCloseButtonText"),
                DefaultButton = ContentDialogButton.Close,
            };

            await dialog.ShowAsync();
        });
    }

    public static void LogUnhandledException(Exception exception, string context)
    {
        WriteUiLog(exception, context);
    }

    private static void WriteUiLog(Exception ex, string context)
    {
        var logPath = LogPaths.GetUiLogPath();
        LogPaths.Append(
            logPath,
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {context}: {ex}{Environment.NewLine}"
        );
    }
}
