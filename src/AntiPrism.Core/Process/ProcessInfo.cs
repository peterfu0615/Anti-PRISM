namespace AntiPrism.Core.Process;

public class ProcessInfo
{
    public int Id { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string? ExecutablePath { get; set; }
    public string? Description { get; set; }
    public string? Publisher { get; set; }
    public bool IsSystemOrProtected { get; set; }
    public string? PackageFamilyName { get; set; }

    public override string ToString() => $"{ProcessName} (PID: {Id}, Path: {ExecutablePath ?? "Access Denied"})";
}
