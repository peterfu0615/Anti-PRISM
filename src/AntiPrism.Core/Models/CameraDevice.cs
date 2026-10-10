namespace AntiPrism.Core.Models;

public enum CameraProtectionMode
{
    None = 0,
    Occupy = 1,
    Disable = 2
}

public class CameraDevice
{
    public string Id { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public string SymbolicLink { get; set; } = string.Empty;
    public string DeviceInstanceId { get; set; } = string.Empty;
    public bool IsDisabled { get; set; }
    public bool IsOccupied { get; set; }
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;

    public override string ToString() => $"{FriendlyName} [{Id}] (Disabled: {IsDisabled}, Occupied: {IsOccupied})";
}
