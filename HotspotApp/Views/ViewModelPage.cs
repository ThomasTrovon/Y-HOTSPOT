using HotspotApp.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HotspotApp.Views;

// Page base that wires ViewModel lifecycle into page events.
public abstract class ViewModelPage : Page
{
    private ViewModelBase? _viewModel;

    protected virtual Task OnLoadedAsync() => Task.CompletedTask;

    protected virtual void OnUnloaded() { }

    protected void InitializeViewModel(ViewModelBase viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnPageLoaded;
        Unloaded += OnPageUnloaded;
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel is ViewModelBase.IInitializable initializable)
        {
            await initializable.InitializeAsync();
        }

        if (_viewModel is ViewModelBase.IRunnable runnable)
        {
            await runnable.StartAsync();
        }

        await OnLoadedAsync();
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel is ViewModelBase.IRunnable runnable)
        {
            runnable.Stop();
        }

        OnUnloaded();
    }
}
