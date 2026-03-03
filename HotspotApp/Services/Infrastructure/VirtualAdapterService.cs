using System.Runtime.InteropServices;
using HotspotApp.Constants;
using HotspotApp.Helpers;
using Microsoft.Extensions.Logging;

namespace HotspotApp.Services.Infrastructure;

public sealed partial class VirtualAdapterService : IDisposable, IAsyncDisposable
{
    private readonly ILogger<VirtualAdapterService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IntPtr _adapterHandle = IntPtr.Zero;
    private bool _disposed;

    public bool IsInitialized => _adapterHandle != IntPtr.Zero;
    public Guid? AdapterGuid { get; private set; }

    public VirtualAdapterService(ILogger<VirtualAdapterService> logger)
    {
        _logger = logger;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        await CleanupAsync();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Cleanup();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (IsInitialized)
            {
                LogAdapterAlreadyInitialized();
                return true;
            }

            LogInitializingAdapter();

            AdapterGuid = Guid.NewGuid();
            _adapterHandle = await CreateAdapterAsync(AdapterGuid.Value, cancellationToken);

            if (_adapterHandle == IntPtr.Zero)
            {
                return false;
            }

            LogAdapterCreated(AdapterGuid.Value);

            if (!ConfigureAdapter(_adapterHandle))
            {
                Cleanup();
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            LogInitializationException(ex);
            Cleanup();
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<IntPtr> CreateAdapterAsync(Guid guid, CancellationToken cancellationToken)
    {
        IntPtr handle = Wintun.CreateAdapter(
            AppConstants.VirtualAdapterName,
            ServiceConstants.VirtualAdapterType,
            in guid
        );

        if (handle != IntPtr.Zero)
            return handle;

        int error = Marshal.GetLastWin32Error();
        if (
            error != Win32Constants.ERROR_ALREADY_EXISTS
            && error != Win32Constants.ERROR_OBJECT_ALREADY_EXISTS
        )
        {
            LogCreateAdapterFailed(error);
            return IntPtr.Zero;
        }

        LogAdapterExistsCleanup();

        CleanupExistingAdapter();

        // Wait a bit for cleanup to propagate
        await Task.Delay(1000, cancellationToken);

        handle = Wintun.CreateAdapter(
            AppConstants.VirtualAdapterName,
            ServiceConstants.VirtualAdapterType,
            in guid
        );

        if (handle == IntPtr.Zero)
        {
            LogCreateAdapterFailed(Marshal.GetLastWin32Error());
        }

        return handle;
    }

    private static void CleanupExistingAdapter()
    {
        var existing = Wintun.OpenAdapter(AppConstants.VirtualAdapterName);
        if (existing != IntPtr.Zero)
        {
            try
            {
                Wintun.CloseAdapter(existing);
            }
            catch
            {
                // Ignore cleanup errors for existing adapter
            }
        }
    }

    private bool ConfigureAdapter(IntPtr adapterHandle)
    {
        try
        {
            Wintun.GetAdapterLuid(adapterHandle, out var luidStruct);
            ulong luid = luidStruct.Value;

            // 10.137.137.1 -> 0x0189890A (Little Endian)
            const uint ipv4Address = 0x0189890A;

            if (!SetIpAddress(luid, ipv4Address))
                return false;
            if (!SetGateway(luid, ipv4Address))
                return false;

            return true;
        }
        catch (Exception ex)
        {
            LogConfigurationError(ex);
            return false;
        }
    }

    private bool SetIpAddress(ulong luid, uint ipv4Address)
    {
        var row = new IpHelper.MIB_UNICASTIPADDRESS_ROW();
        IpHelper.InitializeUnicastIpAddressEntry(ref row);

        row.InterfaceLuid = luid;
        row.Address.si_family = IpHelper.AF_INET;
        row.Address.Ipv4Address = ipv4Address;
        row.OnLinkPrefixLength = 24;
        row.DadState = 4; // IpDadStatePreferred

        int result = IpHelper.CreateUnicastIpAddressEntry(ref row);
        if (
            result != Win32Constants.ERROR_SUCCESS
            && result != Win32Constants.ERROR_OBJECT_ALREADY_EXISTS
        )
        {
            LogSetIpFailed(result);
            return false;
        }

        LogIpAddressSet();
        return true;
    }

    private bool SetGateway(ulong luid, uint ipv4Address)
    {
        var row = new IpHelper.MIB_IPFORWARD_ROW2();
        IpHelper.InitializeIpForwardEntry(ref row);

        row.InterfaceLuid = luid;
        row.DestinationPrefix.si_family = IpHelper.AF_INET;
        row.NextHop.si_family = IpHelper.AF_INET;
        row.NextHop.Ipv4Address = ipv4Address;
        row.Metric = 700;

        int result = IpHelper.CreateIpForwardEntry2(ref row);
        if (
            result != Win32Constants.ERROR_SUCCESS
            && result != Win32Constants.ERROR_OBJECT_ALREADY_EXISTS
        )
        {
            LogSetGatewayFailed(result);
            return false;
        }

        LogGatewaySet();
        return true;
    }

    public void Cleanup()
    {
        if (_adapterHandle == IntPtr.Zero)
            return;

        LogCleaningUp();
        try
        {
            Wintun.CloseAdapter(_adapterHandle);
        }
        catch (Exception ex)
        {
            LogCleanupError(ex);
        }
        finally
        {
            _adapterHandle = IntPtr.Zero;
            AdapterGuid = null;
        }
    }

    private Task CleanupAsync()
    {
        Cleanup();
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Wintun adapter already initialized.")]
    private partial void LogAdapterAlreadyInitialized();

    [LoggerMessage(Level = LogLevel.Information, Message = "Initializing Wintun adapter...")]
    private partial void LogInitializingAdapter();

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Failed to create Wintun adapter. Error: {Error}"
    )]
    private partial void LogCreateAdapterFailed(int error);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Wintun adapter created successfully. GUID: {Guid}"
    )]
    private partial void LogAdapterCreated(Guid guid);

    [LoggerMessage(Level = LogLevel.Error, Message = "Exception initializing Wintun adapter")]
    private partial void LogInitializationException(Exception ex);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Wintun adapter already exists. Attempting cleanup and recreation..."
    )]
    private partial void LogAdapterExistsCleanup();

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to set IP address. Error: {Error}")]
    private partial void LogSetIpFailed(int error);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "IP address 10.137.137.1 set on Wintun adapter."
    )]
    private partial void LogIpAddressSet();

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to set Gateway. Error: {Error}")]
    private partial void LogSetGatewayFailed(int error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Gateway set on Wintun adapter.")]
    private partial void LogGatewaySet();

    [LoggerMessage(Level = LogLevel.Error, Message = "Error configuring Wintun adapter IP/Gateway")]
    private partial void LogConfigurationError(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Cleaning up Wintun adapter...")]
    private partial void LogCleaningUp();

    [LoggerMessage(Level = LogLevel.Error, Message = "Error closing Wintun adapter")]
    private partial void LogCleanupError(Exception ex);
}
