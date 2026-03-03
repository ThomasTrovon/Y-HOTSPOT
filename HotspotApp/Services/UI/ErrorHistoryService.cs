using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using HotspotApp.Models;
using Microsoft.UI.Dispatching;

namespace HotspotApp.Services.UI;

public class ErrorHistoryService
{
    private readonly ObservableCollection<RecentErrorEntry> _errors = [];
    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

    public ReadOnlyObservableCollection<RecentErrorEntry> Errors { get; }

    public ErrorHistoryService()
    {
        Errors = new ReadOnlyObservableCollection<RecentErrorEntry>(_errors);
    }

    public void AddError(string message)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            if (_errors.Count > 0 && _errors[0].Message == message)
            {
                // Prevent duplicate consecutive errors
                return;
            }

            _errors.Insert(0, new RecentErrorEntry(DateTime.Now.ToString("HH:mm:ss"), message));
            if (_errors.Count > 50)
            {
                _errors.RemoveAt(_errors.Count - 1);
            }
        });
    }

    public void AddError(Exception ex, string context = "")
    {
        var message = string.IsNullOrWhiteSpace(context) ? ex.Message : $"{context}: {ex.Message}";
        AddError(message);
    }
}
