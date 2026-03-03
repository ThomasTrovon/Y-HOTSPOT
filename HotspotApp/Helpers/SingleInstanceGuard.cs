namespace HotspotApp.Helpers;

public sealed partial class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;
    public bool HasHandle { get; }

    private SingleInstanceGuard(Mutex mutex, bool hasHandle)
    {
        _mutex = mutex;
        HasHandle = hasHandle;
    }

    public static SingleInstanceGuard Acquire(string name)
    {
        Mutex mutex;
        bool createdNew;
        try
        {
            mutex = new Mutex(true, name, out createdNew);
        }
        catch (UnauthorizedAccessException)
        {
            if (name.StartsWith("Global\\", StringComparison.OrdinalIgnoreCase))
            {
                var localName = string.Concat("Local\\", name.AsSpan(7));
                mutex = new Mutex(true, localName, out createdNew);
            }
            else
            {
                throw;
            }
        }
        return new SingleInstanceGuard(mutex, createdNew);
    }

    public void Dispose()
    {
        _mutex.Dispose();
    }
}
