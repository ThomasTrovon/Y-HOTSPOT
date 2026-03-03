using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HotspotApp.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    protected bool SetProperty<T>(
        ref T storage,
        T value,
        [CallerMemberName] string? propertyName = null
    )
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // Common page lifecycle capabilities for ViewModels
    // Implement on ViewModels when the Page should call Initialize or Start/Stop automatically.
    public interface IInitializable
    {
        Task InitializeAsync();
    }

    public interface IRunnable
    {
        Task StartAsync();
        void Stop();
    }
}
