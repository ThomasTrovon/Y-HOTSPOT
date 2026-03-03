using HotspotApp.Views;
using Microsoft.Extensions.DependencyInjection;

namespace HotspotApp.Services.UI;

public sealed class WindowService(IServiceProvider serviceProvider)
{
    public void ShowMainWindow(bool startInTray)
    {
        var window = serviceProvider.GetRequiredService<MainWindow>();
        App.CurrentWindow = window;
        if (!startInTray)
        {
            window.InitializeEntrance();
            window.Activate();
            window.AnimateWindowEntrance();
        }
    }
}
