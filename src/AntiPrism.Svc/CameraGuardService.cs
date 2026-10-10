using AntiPrism.Core.Hardware;
using AntiPrism.Core.Models;
using Serilog;

namespace AntiPrism.Svc;

public class CameraGuardService : IDisposable
{
    private readonly CameraOccupier _cameraOccupier = new();
    private readonly System.Timers.Timer _watchdogTimer;
    private AntiPrismConfig _config;
    private readonly object _stateLock = new();
    private List<CameraDevice> _discoveredCameras = new();

    public CameraProtectionMode CurrentMode => _config.ProtectionMode;
    public IReadOnlyCollection<string> OccupiedCameraIds => _cameraOccupier.OccupiedCameraIds;
    public IReadOnlyList<CameraDevice> DiscoveredCameras => _discoveredCameras;

    public CameraGuardService(AntiPrismConfig initialConfig)
    {
        _config = initialConfig;
        _watchdogTimer = new System.Timers.Timer(5000); // 5-second watchdog
        _watchdogTimer.Elapsed += async (_, _) => await RunWatchdogCheckAsync();

        _cameraOccupier.CameraHandleLost += async (id) =>
        {
            Log.Warning("Camera handle lost event received for {Id}, initiating immediate re-acquisition", id);
            if (_config.ProtectionMode == CameraProtectionMode.Occupy)
            {
                await RunWatchdogCheckAsync();
            }
        };
    }

    public void Start()
    {
        RefreshCameras();
        _watchdogTimer.Start();
        Log.Information("CameraGuardService started. Watchdog interval: 5s, Mode: {Mode}", _config.ProtectionMode);
        _ = ApplyProtectionModeAsync(_config.ProtectionMode);
    }

    public void UpdateConfig(AntiPrismConfig newConfig)
    {
        lock (_stateLock)
        {
            var oldMode = _config.ProtectionMode;
            _config = newConfig;

            if (oldMode != newConfig.ProtectionMode)
            {
                _ = ApplyProtectionModeAsync(newConfig.ProtectionMode);
            }
        }
    }

    public void RefreshCameras()
    {
        lock (_stateLock)
        {
            try
            {
                _discoveredCameras = CameraEnumerator.GetAllCameras();
                Log.Information("Camera discovery refreshed. Found {Count} devices.", _discoveredCameras.Count);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to enumerate cameras during refresh");
            }
        }
    }

    public async Task ApplyProtectionModeAsync(CameraProtectionMode mode)
    {
        lock (_stateLock)
        {
            _config.ProtectionMode = mode;
        }

        Log.Information("Applying camera protection mode: {Mode}", mode);

        try
        {
            switch (mode)
            {
                case CameraProtectionMode.None:
                    await _cameraOccupier.ReleaseAllAsync();
                    DeviceDisabler.EnableAllCameras();
                    break;

                case CameraProtectionMode.Occupy:
                    // Ensure cameras are enabled in driver first so MediaCapture can connect
                    DeviceDisabler.EnableAllCameras();
                    await OccupyTargetCamerasAsync();
                    break;

                case CameraProtectionMode.Disable:
                    // Release any open capture handles before disabling in driver stack
                    await _cameraOccupier.ReleaseAllAsync();
                    DisableTargetCameras();
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error applying camera protection mode {Mode}", mode);
        }
    }

    private async Task OccupyTargetCamerasAsync()
    {
        var targets = GetTargetCameras();
        foreach (var cam in targets)
        {
            var targetId = !string.IsNullOrEmpty(cam.SymbolicLink) ? cam.SymbolicLink : cam.Id;
            if (!_cameraOccupier.IsOccupied(targetId))
            {
                bool success = await _cameraOccupier.OccupyCameraAsync(targetId);
                if (!success)
                {
                    Log.Warning("Watchdog: Camera {Id} could not be occupied, consider switching to Disable mode", targetId);
                }
            }
        }
    }

    private void DisableTargetCameras()
    {
        var targets = GetTargetCameras();
        foreach (var cam in targets)
        {
            var pnpId = !string.IsNullOrEmpty(cam.DeviceInstanceId) ? cam.DeviceInstanceId : cam.Id;
            DeviceDisabler.DisableDevice(pnpId);
        }
    }

    private List<CameraDevice> GetTargetCameras()
    {
        lock (_stateLock)
        {
            // If user explicitly selected cameras, protect those; otherwise protect all discovered
            if (_config.SelectedCameraIds != null && _config.SelectedCameraIds.Count > 0)
            {
                return _discoveredCameras
                    .Where(c => _config.SelectedCameraIds.Contains(c.Id) || _config.SelectedCameraIds.Contains(c.SymbolicLink))
                    .ToList();
            }

            return _discoveredCameras.ToList();
        }
    }

    private async Task RunWatchdogCheckAsync()
    {
        try
        {
            RefreshCameras();

            if (_config.ProtectionMode == CameraProtectionMode.Occupy)
            {
                await OccupyTargetCamerasAsync();
            }
            else if (_config.ProtectionMode == CameraProtectionMode.Disable)
            {
                DisableTargetCameras();
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Exception in camera watchdog cycle");
        }
    }

    public void Dispose()
    {
        _watchdogTimer.Stop();
        _watchdogTimer.Dispose();
        _cameraOccupier.Dispose();
    }
}
