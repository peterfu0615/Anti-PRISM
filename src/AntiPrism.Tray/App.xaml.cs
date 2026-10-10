using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using AntiPrism.Tray.Gesture;
using AntiPrism.Tray.Hotkeys;
using AntiPrism.Tray.Services;
using AntiPrism.Tray.UI;
using H.NotifyIcon;
using Serilog;

namespace AntiPrism.Tray;

public partial class App : Application
{
    private const string MutexName = @"Global\AntiPrism_Tray_Mutex";
    private Mutex? _instanceMutex;

    private TaskbarIcon? _trayIcon;
    private TrayServiceCoordinator? _coordinator;
    private GestureTracker? _gestureTracker;
    private GlobalHotkeyHook? _hotkeyHook;

    private FakeBrightnessWindow? _fakeBrightnessWindow;
    private RealControlPanelWindow? _realPanelWindow;
    private Window? _hiddenHwndHostWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. Single Instance Check
        _instanceMutex = new Mutex(true, MutexName, out bool isNewInstance);
        if (!isNewInstance)
        {
            Shutdown();
            return;
        }

        try
        {
            // 2. Initialize Services and Gesture Engine
            _coordinator = new TrayServiceCoordinator();
            _gestureTracker = new GestureTracker();
            _gestureTracker.GestureTriggered += OnGestureMatched;

            _fakeBrightnessWindow = new FakeBrightnessWindow(_gestureTracker, _coordinator);

            // 3. Initialize Tray Icon with Neutral Disguise
            _trayIcon = new TaskbarIcon
            {
                ToolTipText = _coordinator.Config.DisguiseTrayTooltip,
                Icon = CreateDisguisedSunIcon()
            };

            _trayIcon.TrayLeftMouseDown += (_, _) => ToggleFakeBrightness();

            // 4. Register Emergency Hotkey via Hidden Window
            _hiddenHwndHostWindow = new Window
            {
                Width = 0,
                Height = 0,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                Visibility = Visibility.Hidden
            };
            _hiddenHwndHostWindow.Show();
            var handle = new WindowInteropHelper(_hiddenHwndHostWindow).Handle;

            _hotkeyHook = new GlobalHotkeyHook(OpenRealControlPanel);
            _hotkeyHook.Register(handle);

            // 5. Check if launched without --hidden (e.g. debugging)
            bool isHidden = e.Args.Any(a => a.Equals("--hidden", StringComparison.OrdinalIgnoreCase));
            if (!isHidden)
            {
                // In non-hidden debug launch, optionally keep idle in tray
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Failed to initialize tray application");
        }
    }

    private void ToggleFakeBrightness()
    {
        if (_fakeBrightnessWindow == null) return;

        if (_fakeBrightnessWindow.IsVisible)
        {
            _fakeBrightnessWindow.Hide();
        }
        else
        {
            _fakeBrightnessWindow.ShowNearTray();
        }
    }

    private void OnGestureMatched()
    {
        Dispatcher.Invoke(() =>
        {
            _fakeBrightnessWindow?.Hide();
            OpenRealControlPanel();
        });
    }

    private void OpenRealControlPanel()
    {
        Dispatcher.Invoke(() =>
        {
            if (_coordinator == null) return;

            if (_realPanelWindow == null || !_realPanelWindow.IsLoaded)
            {
                _realPanelWindow = new RealControlPanelWindow(_coordinator);
                _realPanelWindow.Closed += (_, _) => _realPanelWindow = null;
                _realPanelWindow.Show();
            }
            else
            {
                _realPanelWindow.Show();
                _realPanelWindow.WindowState = WindowState.Normal;
                _realPanelWindow.Activate();
            }
        });
    }

    private static Icon CreateDisguisedSunIcon()
    {
        // Programmatically generate a clean 32x32 sun glyph
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var brush = new SolidBrush(Color.FromArgb(240, 240, 240));
            using var pen = new Pen(Color.FromArgb(240, 240, 240), 2f);

            // Center sun circle
            g.FillEllipse(brush, 10, 10, 12, 12);

            // 8 Rays
            g.DrawLine(pen, 16, 2, 16, 6);   // Top
            g.DrawLine(pen, 16, 26, 16, 30); // Bottom
            g.DrawLine(pen, 2, 16, 6, 16);   // Left
            g.DrawLine(pen, 26, 16, 30, 16); // Right
            g.DrawLine(pen, 6, 6, 9, 9);     // Top-Left
            g.DrawLine(pen, 23, 6, 26, 9);   // Top-Right
            g.DrawLine(pen, 6, 26, 9, 23);   // Bottom-Left
            g.DrawLine(pen, 23, 23, 26, 26); // Bottom-Right
        }

        return Icon.FromHandle(bmp.GetHicon());
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyHook?.Dispose();
        _hiddenHwndHostWindow?.Close();
        _trayIcon?.Dispose();
        _fakeBrightnessWindow?.Close();
        _realPanelWindow?.Close();
        _coordinator?.Dispose();

        _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();

        base.OnExit(e);
    }
}
