using System.Windows;
using AntiPrism.Tray.Gesture;
using AntiPrism.Tray.Services;

namespace AntiPrism.Tray.UI;

public partial class FakeBrightnessWindow : Window
{
    private readonly GestureTracker _gestureTracker;
    private readonly TrayServiceCoordinator _serviceCoordinator;
    private double _previousValue = 50;

    public FakeBrightnessWindow(GestureTracker gestureTracker, TrayServiceCoordinator serviceCoordinator)
    {
        InitializeComponent();
        _gestureTracker = gestureTracker;
        _serviceCoordinator = serviceCoordinator;

        Deactivated += (_, _) => Hide();
    }

    public void ShowNearTray()
    {
        var workingArea = SystemParameters.WorkArea;
        Left = workingArea.Right - Width - 16;
        Top = workingArea.Bottom - Height - 16;

        Show();
        Activate();
    }

    private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (BrightnessLabel != null)
        {
            BrightnessLabel.Text = $"{(int)e.NewValue}%";
        }

        _gestureTracker.OnSliderValueChanged(_previousValue, e.NewValue, _serviceCoordinator.Config.Gesture);
        _previousValue = e.NewValue;
    }
}
