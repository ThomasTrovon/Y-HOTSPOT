using HotspotApp.Helpers;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace HotspotApp.Services.UI;

public sealed class WindowCustomizer
{
    private readonly Window _window;
    private readonly AppWindow _appWindow;
    private readonly IntPtr _hWnd;

    public WindowCustomizer(Window window, AppWindow appWindow)
    {
        _window = window;
        _appWindow = appWindow;
        _hWnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
    }

    public void InitializeWindowStyle()
    {
        var style = WindowInterop.GetWindowLong(_hWnd, WindowInterop.GwlStyle);
        style &= ~(WindowInterop.WsCaption | WindowInterop.WsThickFrame | WindowInterop.WsBorder);
        _ = WindowInterop.SetWindowLong(_hWnd, WindowInterop.GwlStyle, style);

        var exStyle = WindowInterop.GetWindowLong(_hWnd, WindowInterop.WlExStyle);
        exStyle |= WindowInterop.WsExToolWindow | WindowInterop.WsExTopmost;
        _ = WindowInterop.SetWindowLong(_hWnd, WindowInterop.WlExStyle, exStyle);

        WindowInterop.SetWindowPos(
            _hWnd,
            WindowInterop.HwndTopmost,
            0,
            0,
            0,
            0,
            WindowInterop.SwpNoMove | WindowInterop.SwpNoSize | WindowInterop.SwpNoActivate
        );

        ApplyRoundedCorners();

        var presenter = _appWindow.Presenter as OverlappedPresenter ?? OverlappedPresenter.Create();
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.SetBorderAndTitleBar(false, false);
        _appWindow.SetPresenter(presenter);

        UpdateAppSize();
    }

    public void UpdateAppSize()
    {
        var dpi = WindowInterop.GetDpiForWindow(_hWnd);
        var scale = dpi / 96.0;
        var width = (int)(650 * scale);
        var height = (int)(550 * scale);
        var newSize = new Windows.Graphics.SizeInt32(width, height);
        _appWindow.Resize(newSize);
    }

    public void UpdateAppPosition()
    {
        UpdateAppSize();
        var targetPos = GetBottomRightPosition();
        _appWindow.Move(targetPos);
    }

    public void EnsureWindowVisible()
    {
        var currentStyle = WindowInterop.GetWindowLong(_hWnd, WindowInterop.WlExStyle);
        if ((currentStyle & WindowInterop.WsExLayered) != 0)
        {
            _ = WindowInterop.SetWindowLong(
                _hWnd,
                WindowInterop.WlExStyle,
                currentStyle & ~WindowInterop.WsExLayered
            );
        }
        WindowInterop.SetLayeredWindowAttributes(_hWnd, 0, 255, WindowInterop.LwaAlpha);

        UpdateAppPosition();
        _appWindow.Show();

        WindowInterop.SetWindowPos(
            _hWnd,
            WindowInterop.HwndTopmost,
            0,
            0,
            0,
            0,
            WindowInterop.SwpNoMove | WindowInterop.SwpNoSize | WindowInterop.SwpNoActivate
        );

        _window.Activate();
        WindowInterop.SetForegroundWindow(_hWnd);
    }

    public async Task AnimateWindowEntranceAsync()
    {
        var targetPos = GetBottomRightPosition();
        var startY = targetPos.Y + 400;
        await AnimateWindowAsync(
            targetPos.X,
            startY,
            targetPos.Y,
            EaseOutCubic,
            static progress => progress,
            300,
            false
        );
    }

    public async Task AnimateWindowExitAsync()
    {
        var currentPos = _appWindow.Position;
        var targetY = currentPos.Y + 400;
        await AnimateWindowAsync(
            currentPos.X,
            currentPos.Y,
            targetY,
            EaseInCubic,
            static progress => 1 - progress,
            200,
            true
        );
    }

    public void InitializeEntrance()
    {
        var exStyle = WindowInterop.GetWindowLong(_hWnd, WindowInterop.WlExStyle);
        _ = WindowInterop.SetWindowLong(
            _hWnd,
            WindowInterop.WlExStyle,
            exStyle | WindowInterop.WsExLayered
        );
        WindowInterop.SetLayeredWindowAttributes(_hWnd, 0, 0, WindowInterop.LwaAlpha);

        var targetPos = GetBottomRightPosition();
        var startY = targetPos.Y + 400;
        _appWindow.Move(new Windows.Graphics.PointInt32(targetPos.X, startY));
    }

    private void ApplyRoundedCorners()
    {
        var preference = WindowInterop.DwmcpRound;
        WindowInterop.DwmSetWindowAttribute(
            _hWnd,
            WindowInterop.DwmwaWindowCornerPreference,
            ref preference,
            sizeof(int)
        );
    }

    private Windows.Graphics.PointInt32 GetBottomRightPosition()
    {
        var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;
        var size = _appWindow.Size;

        var x = workArea.Width - size.Width - 10;
        var y = workArea.Height - size.Height - 10;

        return new Windows.Graphics.PointInt32(x + workArea.X, y + workArea.Y);
    }

    private Task<bool> AnimateWindowAsync(
        int x,
        int startY,
        int endY,
        Func<double, double> easing,
        Func<double, double> opacityMap,
        int duration,
        bool hideAfter
    )
    {
        var tcs = new TaskCompletionSource<bool>();
        var exStyle = WindowInterop.GetWindowLong(_hWnd, WindowInterop.WlExStyle);
        _ = WindowInterop.SetWindowLong(
            _hWnd,
            WindowInterop.WlExStyle,
            exStyle | WindowInterop.WsExLayered
        );

        var initialOpacity = (byte)(255 * opacityMap(easing(0)));
        WindowInterop.SetLayeredWindowAttributes(_hWnd, 0, initialOpacity, WindowInterop.LwaAlpha);

        _appWindow.Move(new Windows.Graphics.PointInt32(x, startY));

        if (!hideAfter && !_appWindow.IsVisible)
        {
            _appWindow.Show();
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        void Cleanup()
        {
            Microsoft.UI.Xaml.Media.CompositionTarget.Rendering -= OnRendering;
            stopwatch.Stop();

            if (hideAfter)
            {
                _appWindow.Hide();
            }

            // Restore style by removing the layered bit
            var currentStyle = WindowInterop.GetWindowLong(_hWnd, WindowInterop.WlExStyle);
            _ = WindowInterop.SetWindowLong(
                _hWnd,
                WindowInterop.WlExStyle,
                currentStyle & ~WindowInterop.WsExLayered
            );
            WindowInterop.SetLayeredWindowAttributes(_hWnd, 0, 255, WindowInterop.LwaAlpha);

            // Ensure final position is correct if not hiding
            if (!hideAfter)
            {
                _appWindow.Move(new Windows.Graphics.PointInt32(x, endY));
            }
        }

        void OnRendering(object? sender, object e)
        {
            try
            {
                if (!_appWindow.IsVisible && !hideAfter)
                {
                    // If window became invisible unexpectedly, abort
                    Cleanup();
                    tcs.TrySetResult(false);
                    return;
                }

                var elapsed = stopwatch.ElapsedMilliseconds;
                var progress = Math.Min(1.0, (double)elapsed / duration);

                var ease = easing(progress);
                var currentY = startY + (endY - startY) * ease;
                var opacity = (byte)(255 * opacityMap(ease));

                WindowInterop.SetLayeredWindowAttributes(_hWnd, 0, opacity, WindowInterop.LwaAlpha);
                _appWindow.Move(new Windows.Graphics.PointInt32(x, (int)currentY));

                if (progress >= 1.0)
                {
                    Cleanup();
                    tcs.TrySetResult(true);
                }
            }
            catch (Exception ex)
            {
                Cleanup();
                tcs.TrySetException(ex);
            }
        }

        Microsoft.UI.Xaml.Media.CompositionTarget.Rendering += OnRendering;

        return tcs.Task;
    }

    private static double EaseOutCubic(double t)
    {
        return 1 - Math.Pow(1 - t, 3);
    }

    private static double EaseInCubic(double t)
    {
        return t * t * t;
    }
}
