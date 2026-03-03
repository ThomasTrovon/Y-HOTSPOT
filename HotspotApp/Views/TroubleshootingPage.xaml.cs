using HotspotApp.Services.UI;
using HotspotApp.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace HotspotApp.Views;

public sealed partial class TroubleshootingPage : ViewModelPage
{
    public TroubleshootingViewModel ViewModel { get; }
    private readonly DialogService _dialogService;

    public TroubleshootingPage()
    {
        InitializeComponent();
        _dialogService = App.Services.GetRequiredService<DialogService>();
        ViewModel = App.Services.GetRequiredService<TroubleshootingViewModel>();
        InitializeViewModel(ViewModel);
    }

    protected override Task OnLoadedAsync()
    {
        _dialogService.SetXamlRoot(XamlRoot);
        return Task.CompletedTask;
    }
}
