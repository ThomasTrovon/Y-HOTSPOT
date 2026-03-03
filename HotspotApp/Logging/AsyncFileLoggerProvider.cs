using Microsoft.Extensions.Logging;

namespace HotspotApp.Logging;

[ProviderAlias("AsyncFile")]
public sealed partial class AsyncFileLoggerProvider(string path) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        return new AsyncFileLogger(categoryName, path);
    }

    public void Dispose() { }
}
