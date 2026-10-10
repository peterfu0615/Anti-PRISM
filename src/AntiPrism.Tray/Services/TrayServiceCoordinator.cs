using System.Text.Json;
using AntiPrism.Core.Ipc;
using AntiPrism.Core.Models;
using AntiPrism.Core.Storage;
using Serilog;

namespace AntiPrism.Tray.Services;

public class TrayServiceCoordinator : IDisposable
{
    private readonly NamedPipeClient _pipeClient = new();
    private AntiPrismConfig _config;

    public AntiPrismConfig Config => _config;
    public bool IsConnected => _pipeClient.IsConnected;

    public TrayServiceCoordinator()
    {
        _config = ConfigManager.LoadConfig();
    }

    public async Task<SystemStatusDto?> GetStatusAsync()
    {
        var response = await _pipeClient.SendCommandAsync(IpcCommandType.GetStatus);
        if (response.Success && !string.IsNullOrEmpty(response.DataJson))
        {
            return JsonSerializer.Deserialize<SystemStatusDto>(response.DataJson);
        }
        return null;
    }

    public async Task<bool> SetProtectionModeAsync(CameraProtectionMode mode)
    {
        var response = await _pipeClient.SendCommandAsync(
            IpcCommandType.SetProtectionMode,
            JsonSerializer.Serialize(mode.ToString()));

        if (response.Success)
        {
            _config.ProtectionMode = mode;
            ConfigManager.SaveConfig(_config);
            return true;
        }

        return false;
    }

    public async Task<bool> UpdateBlacklistAsync(List<BlacklistEntry> blacklist)
    {
        var json = JsonSerializer.Serialize(blacklist);
        var response = await _pipeClient.SendCommandAsync(IpcCommandType.UpdateBlacklist, json);
        if (response.Success)
        {
            _config.Blacklist = blacklist;
            ConfigManager.SaveConfig(_config);
            return true;
        }

        return false;
    }

    public async Task<bool> SaveConfigAsync(AntiPrismConfig updatedConfig)
    {
        _config = updatedConfig;
        ConfigManager.SaveConfig(_config);

        var json = JsonSerializer.Serialize(_config);
        var response = await _pipeClient.SendCommandAsync(IpcCommandType.UpdateConfig, json);
        return response.Success;
    }

    public async Task<string> EmergencyRestoreAsync()
    {
        var response = await _pipeClient.SendCommandAsync(IpcCommandType.EmergencyRestore);
        _config.ProtectionMode = CameraProtectionMode.None;
        ConfigManager.SaveConfig(_config);
        return response.Success ? (response.Message ?? "Cameras restored") : "Failed to restore cameras";
    }

    public void Dispose()
    {
        _pipeClient.Dispose();
    }
}
