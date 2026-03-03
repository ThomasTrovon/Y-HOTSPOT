using HotspotApp.Constants;
using HotspotApp.Helpers;
using HotspotApp.Views;

namespace HotspotApp.Services.UI;

public sealed partial class TrayIconNative : IDisposable
{
    private const uint TrayIconId = 1;
    private const uint TrayCallbackMessage = WindowInterop.WmApp + 1;
    private static readonly Guid TrayIconGuid = new("3978F101-23D8-4739-9CD2-A64478BB959C");
    private const int TrayClickDebounceMs = 200;

    private readonly MainWindow _mainWindow;
    private readonly WindowMessageMonitor _monitor;
    private readonly IntPtr _hwnd;
    private IntPtr _trayIconHandle;
    private uint _taskbarCreatedMessage;
    private long _lastTrayClickTimestamp;
    private bool _isDisposed;

    public event Action? LeftClick;
    public event Action? RightClick;

    public TrayIconNative(MainWindow mainWindow, WindowMessageMonitor monitor)
    {
        _mainWindow = mainWindow;
        _monitor = monitor;
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_mainWindow);
    }

    public void Initialize()
    {
        // Allow the callback message through User Interface Privilege Isolation
        WindowInterop.ChangeWindowMessageFilterEx(
            _hwnd,
            TrayCallbackMessage,
            WindowInterop.MsgfltAllow,
            IntPtr.Zero
        );

        _monitor.MessageReceived += OnWindowMessage;

        _taskbarCreatedMessage = WindowInterop.RegisterWindowMessage("TaskbarCreated");
        _trayIconHandle = LoadTrayIcon();

        AddNotifyIcon();
    }

    private void OnWindowMessage(object? sender, WindowMessageEventArgs e)
    {
        if (e.Msg == _taskbarCreatedMessage && _taskbarCreatedMessage != 0)
        {
            AddNotifyIcon();
            return;
        }

        if (e.Msg == TrayCallbackMessage)
        {
            var eventId = unchecked((int)e.LParam.ToInt64()) & 0xFFFF;

            if (eventId == WindowInterop.WmRButtonUp || eventId == WindowInterop.WmContextMenu)
            {
                RightClick?.Invoke();
                e.Handled = true;
                e.Result = IntPtr.Zero;
                return;
            }

            if (
                eventId == WindowInterop.WmLButtonUp
                || eventId == WindowInterop.NinSelect
                || eventId == WindowInterop.NinKeySelect
            )
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (now - _lastTrayClickTimestamp > TrayClickDebounceMs)
                {
                    _lastTrayClickTimestamp = now;
                    LeftClick?.Invoke();
                }
                e.Handled = true;
                e.Result = IntPtr.Zero;
                return;
            }
        }
    }

    public unsafe void UpdateTooltip(string text)
    {
        if (_hwnd == IntPtr.Zero)
            return;

        var data = new WindowInterop.NotifyIconData
        {
            cbSize = (uint)sizeof(WindowInterop.NotifyIconData),
            hWnd = _hwnd,
            uID = TrayIconId,
            uFlags = WindowInterop.NifTip | WindowInterop.NifShowTip,
        };

        CopyStringToFixedBuffer(text, data.szTip, 128);
        WindowInterop.Shell_NotifyIcon(WindowInterop.NimModify, ref data);
    }

    private unsafe void AddNotifyIcon()
    {
        if (_trayIconHandle == IntPtr.Zero)
            return;

        var data = new WindowInterop.NotifyIconData
        {
            cbSize = (uint)sizeof(WindowInterop.NotifyIconData),
            hWnd = _hwnd,
            uID = TrayIconId,
            uFlags =
                WindowInterop.NifMessage
                | WindowInterop.NifIcon
                | WindowInterop.NifTip
                | WindowInterop.NifGuid
                | WindowInterop.NifShowTip,
            uCallbackMessage = TrayCallbackMessage,
            hIcon = _trayIconHandle,
            guidItem = TrayIconGuid,
        };

        CopyStringToFixedBuffer(AppConstants.AppDisplayName, data.szTip, 128);

        WindowInterop.Shell_NotifyIcon(WindowInterop.NimAdd, ref data);

        data.uTimeoutOrVersion = WindowInterop.NotifyIconVersion4;
        WindowInterop.Shell_NotifyIcon(WindowInterop.NimSetVersion, ref data);
    }

    private unsafe void RemoveNotifyIcon()
    {
        if (_hwnd == IntPtr.Zero)
            return;

        var data = new WindowInterop.NotifyIconData
        {
            cbSize = (uint)sizeof(WindowInterop.NotifyIconData),
            hWnd = _hwnd,
            uID = TrayIconId,
        };
        WindowInterop.Shell_NotifyIcon(WindowInterop.NimDelete, ref data);
    }

    private static IntPtr LoadTrayIcon()
    {
        var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
        if (File.Exists(iconPath))
        {
            var icon = WindowInterop.LoadImage(
                IntPtr.Zero,
                iconPath,
                WindowInterop.ImageIcon,
                0,
                0,
                WindowInterop.LrLoadFromFile
            );
            if (icon != IntPtr.Zero)
                return icon;
        }

        return WindowInterop.LoadIcon(IntPtr.Zero, new IntPtr(WindowInterop.IdiApplication));
    }

    private static unsafe void CopyStringToFixedBuffer(
        string source,
        char* destination,
        int destinationSize
    )
    {
        if (string.IsNullOrEmpty(source))
        {
            destination[0] = '\0';
            return;
        }

        var sourceSpan = source.AsSpan();
        if (sourceSpan.Length >= destinationSize)
        {
            sourceSpan = sourceSpan[..(destinationSize - 1)];
        }

        sourceSpan.CopyTo(new Span<char>(destination, destinationSize));
        destination[sourceSpan.Length] = '\0';
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;

        RemoveNotifyIcon();
        _monitor.MessageReceived -= OnWindowMessage;

        if (_trayIconHandle != IntPtr.Zero)
        {
            WindowInterop.DestroyIcon(_trayIconHandle);
        }

        GC.SuppressFinalize(this);
    }
}
