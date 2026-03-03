using System.Runtime.InteropServices;
using HotspotApp.Helpers;
using Microsoft.UI.Xaml;

namespace HotspotApp.Services.UI;

public sealed partial class WindowMessageMonitor : IDisposable
{
    private readonly Window _window;
    private readonly IntPtr _hwnd;
    private WindowInterop.WindowProc? _newWndProc;
    private IntPtr _oldWndProc;
    private bool _isHooked;
    private bool _isDisposed;

    // Event for listeners to handle messages.
    // If handled, set Handled to true and Result to the desired return value.
    public event EventHandler<WindowMessageEventArgs>? MessageReceived;

    public WindowMessageMonitor(Window window)
    {
        _window = window;
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
    }

    public void Initialize()
    {
        if (_isHooked)
            return;

        _newWndProc = new WindowInterop.WindowProc(WndProc);
        var newWndProcPtr = Marshal.GetFunctionPointerForDelegate(_newWndProc);

        _oldWndProc = WindowInterop.SetWindowLongPtr(
            _hwnd,
            WindowInterop.GwlWndProc,
            newWndProcPtr
        );

        _isHooked = true;
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (MessageReceived != null)
        {
            var args = new WindowMessageEventArgs(hWnd, msg, wParam, lParam);
            MessageReceived.Invoke(this, args);

            if (args.Handled)
            {
                return args.Result;
            }
        }

        return WindowInterop.CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        if (_isHooked && _oldWndProc != IntPtr.Zero)
        {
            // Restore original WndProc
            WindowInterop.SetWindowLongPtr(_hwnd, WindowInterop.GwlWndProc, _oldWndProc);
            _isHooked = false;
        }

        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}

public class WindowMessageEventArgs(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam) : EventArgs
{
    public IntPtr HWnd { get; } = hWnd;
    public uint Msg { get; } = msg;
    public IntPtr WParam { get; } = wParam;
    public IntPtr LParam { get; } = lParam;

    public bool Handled { get; set; }
    public IntPtr Result { get; set; }
}
