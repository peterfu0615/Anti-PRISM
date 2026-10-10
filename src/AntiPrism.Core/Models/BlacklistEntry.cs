namespace AntiPrism.Core.Models;

public enum BlacklistMode
{
    KillWin32 = 0,
    DenyUwp = 1
}

public class BlacklistEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ExePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? PackageFamilyName { get; set; }
    public BlacklistMode Mode { get; set; } = BlacklistMode.KillWin32;
    public bool IsEnabled { get; set; } = true;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public override string ToString() => $"{FileName} ({Mode}, Enabled={IsEnabled})";
}
