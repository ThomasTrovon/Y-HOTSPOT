using HotspotApp.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace HotspotApp.Views;

public sealed partial class DiagnosticsPage : ViewModelPage
{
    public StatusViewModel ViewModel { get; }

    public DiagnosticsPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<StatusViewModel>();
        InitializeViewModel(ViewModel);
    }
}
