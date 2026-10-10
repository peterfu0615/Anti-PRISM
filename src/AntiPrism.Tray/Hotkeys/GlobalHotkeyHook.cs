using System.Windows.Interop;
using AntiPrism.Core.Native;
using Serilog;

namespace AntiPrism.Tray.Hotkeys;

public class GlobalHotkeyHook : IDisposable
{
    private const int HotkeyId = 0x911A;
    private nint _hWnd = nint.Zero;
    private HwndSource? _hwndSource;
    private readonly Action _onHotkeyTriggered;
    private bool _isRegistered;

    public GlobalHotkeyHook(Action onHotkeyTriggered)
    {
        _onHotkeyTriggered = onHotkeyTriggered;
    }

    public void Register(nint hWnd)
    {
        _hWnd = hWnd;
        _hwndSource = HwndSource.FromHwnd(_hWnd);
        _hwndSource?.AddHook(HwndHook);

        // Register Ctrl + Alt + Shift + F12
        uint modifiers = Win32Native.MOD_CONTROL | Win32Native.MOD_ALT | Win32Native.MOD_SHIFT | Win32Native.MOD_NOREPEAT;
        uint vkF12 = 0x7B; // VK_F12

        _isRegistered = Win32Native.RegisterHotKey(_hWnd, HotkeyId, modifiers, vkF12);
        if (_isRegistered)
        {
            Log.Information("Emergency failsafe hotkey (Ctrl+Alt+Shift+F12) successfully registered");
        }
        else
        {
            Log.Warning("Failed to register emergency failsafe hotkey");
        }
    }

    private nint HwndHook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == Win32Native.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            Log.Information("Emergency failsafe hotkey triggered!");
            _onHotkeyTriggered.Invoke();
            handled = true;
        }

        return nint.Zero;
    }

    public void Dispose()
    {
        if (_isRegistered && _hWnd != nint.Zero)
        {
            Win32Native.UnregisterHotKey(_hWnd, HotkeyId);
            _isRegistered = false;
        }

        _hwndSource?.RemoveHook(HwndHook);
        _hwndSource?.Dispose();
    }
}
