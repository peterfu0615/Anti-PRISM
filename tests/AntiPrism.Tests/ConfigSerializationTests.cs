using System.Text.Json;
using AntiPrism.Core.Models;
using Xunit;

namespace AntiPrism.Tests;

public class ConfigSerializationTests
{
    [Fact]
    public void Config_RoundtripsThroughJson()
    {
        var config = new AntiPrismConfig
        {
            ProtectionMode = CameraProtectionMode.Occupy,
            DisguiseTrayTooltip = "Display Brightness Calibration",
            Gesture = new GestureConfig
            {
                SecretValue = 61.0,
                SecretCount = 6,
                SecretWindowSec = 10,
                EmergencyHotkey = "Ctrl+Alt+Shift+F12"
            },
            Blacklist = new List<BlacklistEntry>
            {
                new() { FileName = "obs64.exe", Mode = BlacklistMode.KillWin32 }
            }
        };

        var json = JsonSerializer.Serialize(config);
        var restored = JsonSerializer.Deserialize<AntiPrismConfig>(json);

        Assert.NotNull(restored);
        Assert.Equal(CameraProtectionMode.Occupy, restored!.ProtectionMode);
        Assert.Equal(61.0, restored.Gesture.SecretValue);
        Assert.Single(restored.Blacklist);
        Assert.Equal("obs64.exe", restored.Blacklist[0].FileName);
    }
}
