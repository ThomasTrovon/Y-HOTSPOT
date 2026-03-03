using HotspotApp.Services.UI;
using HotspotApp.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace HotspotApp.Views;

public sealed partial class SettingsPage : ViewModelPage
{
    public SettingsViewModel ViewModel { get; }
    private readonly DialogService _dialogService;

    public SettingsPage()
    {
        InitializeComponent();
        _dialogService = App.Services.GetRequiredService<DialogService>();
        ViewModel = App.Services.GetRequiredService<SettingsViewModel>();
        InitializeViewModel(ViewModel);
    }

    protected override Task OnLoadedAsync()
    {
        _dialogService.SetXamlRoot(XamlRoot);
        return Task.CompletedTask;
    }
}
