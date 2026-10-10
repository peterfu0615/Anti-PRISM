namespace AntiPrism.Core.Models;

public class GestureConfig
{
    public double SecretValue { get; set; } = 61.0;
    public int SecretCount { get; set; } = 6;
    public int SecretWindowSec { get; set; } = 10;
    public string EmergencyHotkey { get; set; } = "Ctrl+Alt+Shift+F12";
    public bool EnableEmergencyHotkey { get; set; } = true;
    public bool RequirePassword { get; set; } = false;
    public string? PasswordHash { get; set; }
}

public class AntiPrismConfig
{
    public List<string> SelectedCameraIds { get; set; } = new();
    public CameraProtectionMode ProtectionMode { get; set; } = CameraProtectionMode.Occupy;
    public List<BlacklistEntry> Blacklist { get; set; } = new();
    public GestureConfig Gesture { get; set; } = new();
    public bool AutoStartService { get; set; } = true;
    public bool AutoStartTray { get; set; } = true;
    public string DisguiseTrayTooltip { get; set; } = "Display Brightness";
    public bool DisguiseNotifications { get; set; } = true;
    public bool PreLogonProtectionEnabled { get; set; } = true;
}
