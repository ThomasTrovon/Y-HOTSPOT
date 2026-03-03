using HotspotApp.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace HotspotApp.Views;

public sealed partial class StatusPage : ViewModelPage
{
    public StatusViewModel ViewModel { get; }

    public StatusPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<StatusViewModel>();
        InitializeViewModel(ViewModel);
    }
}
