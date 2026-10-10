using System.Text.Json;
using AntiPrism.Core.Hardware;
using AntiPrism.Core.Ipc;
using AntiPrism.Core.Models;
using AntiPrism.Core.Storage;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace AntiPrism.Svc;

public class Worker : BackgroundService
{
    private readonly AntiPrismConfig _config;
    private readonly BlacklistStore _blacklistStore;
    private readonly CameraGuardService _cameraGuard;
    private readonly ProcessHunter _processHunter;
    private NamedPipeServer? _pipeServer;

    public Worker()
    {
        _config = ConfigManager.LoadConfig();
        _blacklistStore = new BlacklistStore();
        _blacklistStore.LoadEntries(_config.Blacklist);

        _cameraGuard = new CameraGuardService(_config);
        _processHunter = new ProcessHunter(_blacklistStore);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("AntiPrism Worker starting up...");

        _cameraGuard.Start();
        _processHunter.Start();

        _pipeServer = new NamedPipeServer(HandleIpcRequestAsync);
        _pipeServer.Start();

        Log.Information("AntiPrism Service initialized successfully");

        return Task.CompletedTask;
    }

    private async Task<IpcResponse> HandleIpcRequestAsync(IpcRequest request)
    {
        try
        {
            switch (request.Command)
            {
                case IpcCommandType.Ping:
                    return IpcResponse.Ok(request.RequestId, "Pong");

                case IpcCommandType.GetStatus:
                    var status = new SystemStatusDto
                    {
                        ProtectionMode = _cameraGuard.CurrentMode,
                        Cameras = _cameraGuard.DiscoveredCameras.ToList(),
                        OccupiedCameraIds = _cameraGuard.OccupiedCameraIds.ToList(),
                        BlacklistCount = _blacklistStore.GetAll().Count,
                        ProcessesKilledCount = _processHunter.ProcessesKilledCount,
                        ServiceRunning = true,
                        ServerTime = DateTime.UtcNow
                    };
                    return IpcResponse.Ok(request.RequestId, JsonSerializer.Serialize(status));

                case IpcCommandType.SetProtectionMode:
                    if (!string.IsNullOrEmpty(request.PayloadJson) &&
                        Enum.TryParse<CameraProtectionMode>(request.PayloadJson.Trim('"'), out var mode))
                    {
                        await _cameraGuard.ApplyProtectionModeAsync(mode);
                        _config.ProtectionMode = mode;
                        ConfigManager.SaveConfig(_config);
                        return IpcResponse.Ok(request.RequestId, $"Mode updated to {mode}");
                    }
                    return IpcResponse.Fail(request.RequestId, "Invalid protection mode payload");

                case IpcCommandType.EmergencyRestore:
                    int restored = DeviceDisabler.EnableAllCameras();
                    await _cameraGuard.ApplyProtectionModeAsync(CameraProtectionMode.None);
                    _config.ProtectionMode = CameraProtectionMode.None;
                    ConfigManager.SaveConfig(_config);
                    return IpcResponse.Ok(request.RequestId, $"Restored {restored} cameras to enabled state");

                case IpcCommandType.RefreshCameras:
                    _cameraGuard.RefreshCameras();
                    return IpcResponse.Ok(request.RequestId, $"Found {_cameraGuard.DiscoveredCameras.Count} cameras");

                case IpcCommandType.UpdateBlacklist:
                    if (!string.IsNullOrEmpty(request.PayloadJson))
                    {
                        var newBlacklist = JsonSerializer.Deserialize<List<BlacklistEntry>>(request.PayloadJson);
                        if (newBlacklist != null)
                        {
                            _blacklistStore.LoadEntries(newBlacklist);
                            _config.Blacklist = newBlacklist;
                            ConfigManager.SaveConfig(_config);
                            return IpcResponse.Ok(request.RequestId, "Blacklist updated");
                        }
                    }
                    return IpcResponse.Fail(request.RequestId, "Invalid blacklist payload");

                case IpcCommandType.UpdateConfig:
                    if (!string.IsNullOrEmpty(request.PayloadJson))
                    {
                        var updatedConfig = JsonSerializer.Deserialize<AntiPrismConfig>(request.PayloadJson);
                        if (updatedConfig != null)
                        {
                            _cameraGuard.UpdateConfig(updatedConfig);
                            _blacklistStore.LoadEntries(updatedConfig.Blacklist);
                            ConfigManager.SaveConfig(updatedConfig);
                            return IpcResponse.Ok(request.RequestId, "Configuration updated successfully");
                        }
                    }
                    return IpcResponse.Fail(request.RequestId, "Invalid configuration payload");

                default:
                    return IpcResponse.Fail(request.RequestId, $"Unknown command {request.Command}");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling IPC request {Command}", request.Command);
            return IpcResponse.Fail(request.RequestId, ex.Message);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        Log.Information("AntiPrism Worker stopping...");
        if (_pipeServer != null)
        {
            await _pipeServer.DisposeAsync();
        }
        _processHunter.Dispose();
        _cameraGuard.Dispose();
        await base.StopAsync(cancellationToken);
    }
}
