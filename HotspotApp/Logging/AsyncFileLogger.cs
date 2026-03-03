using System.Text;
using System.Threading.Channels;
using HotspotApp.Config;
using Microsoft.Extensions.Logging;

namespace HotspotApp.Logging;

public sealed partial class AsyncFileLogger : ILogger, IDisposable
{
    private readonly string _name;
    private readonly string _filePath;
    private static readonly Channel<string> _logChannel = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleReader = true }
    );
    private static readonly CancellationTokenSource _cts = new();
    private static Task? _processTask;
    private static int _currentLevel = (int)LogLevelSetting.All;
    private static bool _isInitialized;
    private static readonly Lock _lock = new();

    public AsyncFileLogger(string name, string filePath)
    {
        _name = name;
        _filePath = filePath;
        EnsureProcessingStarted();
    }

    private static void EnsureProcessingStarted()
    {
        lock (_lock)
        {
            if (!_isInitialized)
            {
                _processTask = Task.Factory.StartNew(
                    ProcessQueueAsync,
                    TaskCreationOptions.LongRunning
                );
                _isInitialized = true;
            }
        }
    }

    public static LogLevelSetting CurrentLogLevel
    {
        get => (LogLevelSetting)Volatile.Read(ref _currentLevel);
        set => Volatile.Write(ref _currentLevel, (int)value);
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => default!;

    public bool IsEnabled(LogLevel logLevel)
    {
        return IsEnabled(CurrentLogLevel, logLevel);
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        var sb = new StringBuilder();
        sb.Append('[');
        sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        sb.Append("] [");
        sb.Append(logLevel);
        sb.Append("] [");
        sb.Append(_name);
        sb.Append("] ");
        sb.Append(message);

        if (exception != null)
        {
            sb.AppendLine();
            sb.Append(exception);
        }

        // We include the file path in the message payload so the processor knows where to write.
        // This is a bit of a hack to support multiple file paths with a single static channel,
        // but since we usually have one log file per process type (Service or UI), it's acceptable.
        // However, to be cleaner, we'll assume the file path is passed during provider creation
        // and we might need a better way if we strictly need to separate files in the same process.
        // For this app, it seems we log to one file.
        // Let's pass a tuple.

        _logChannel.Writer.TryWrite($"{_filePath}|{sb}");
    }

    private static async Task ProcessQueueAsync()
    {
        var batch = new Dictionary<string, StringBuilder>();

        try
        {
            while (await _logChannel.Reader.WaitToReadAsync(_cts.Token))
            {
                batch.Clear();

                // Read all available items
                while (_logChannel.Reader.TryRead(out var item))
                {
                    var parts = item.Split('|', 2);
                    if (parts.Length != 2)
                        continue;

                    var path = parts[0];
                    var content = parts[1];

                    if (!batch.TryGetValue(path, out var sb))
                    {
                        sb = new StringBuilder();
                        batch[path] = sb;
                    }
                    sb.AppendLine(content);
                }

                foreach (var kvp in batch)
                {
                    LogPaths.Append(kvp.Key, kvp.Value.ToString());
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
        }
        catch (Exception)
        {
            // Last resort safety
        }
    }

    private static bool IsEnabled(LogLevelSetting setting, LogLevel logLevel)
    {
        return setting switch
        {
            LogLevelSetting.None => false,
            LogLevelSetting.Important => logLevel >= LogLevel.Warning,
            _ => logLevel >= LogLevel.Trace,
        };
    }

    public void Dispose()
    {
        _cts.Cancel();
    }
}
