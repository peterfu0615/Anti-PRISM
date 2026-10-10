using AntiPrism.Core.Models;

namespace AntiPrism.Core.Ipc;

public enum IpcCommandType
{
    Ping = 0,
    GetStatus = 1,
    SetProtectionMode = 2,
    OccupyCamera = 3,
    ReleaseCamera = 4,
    DisableCamera = 5,
    EnableCamera = 6,
    EmergencyRestore = 7,
    UpdateBlacklist = 8,
    UpdateConfig = 9,
    RefreshCameras = 10
}

public class IpcRequest
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString("N");
    public IpcCommandType Command { get; set; }
    public string? PayloadJson { get; set; }
}

public class IpcResponse
{
    public string RequestId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? DataJson { get; set; }

    public static IpcResponse Ok(string requestId, string? dataJson = null) =>
        new() { RequestId = requestId, Success = true, DataJson = dataJson };

    public static IpcResponse Fail(string requestId, string message) =>
        new() { RequestId = requestId, Success = false, Message = message };
}

public class SystemStatusDto
{
    public CameraProtectionMode ProtectionMode { get; set; }
    public List<CameraDevice> Cameras { get; set; } = new();
    public List<string> OccupiedCameraIds { get; set; } = new();
    public int BlacklistCount { get; set; }
    public int ProcessesKilledCount { get; set; }
    public bool ServiceRunning { get; set; } = true;
    public DateTime ServerTime { get; set; } = DateTime.UtcNow;
}

public class ProcessKilledNotification
{
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string ExePath { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
