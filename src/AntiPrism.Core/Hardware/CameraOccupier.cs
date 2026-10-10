using System.Collections.Concurrent;
using Serilog;
using Windows.Media.Capture;

namespace AntiPrism.Core.Hardware;

public class CameraOccupier : IDisposable
{
    private readonly ConcurrentDictionary<string, MediaCapture> _activeCaptures = new(StringComparer.OrdinalIgnoreCase);
    private bool _isDisposed;

    public IReadOnlyCollection<string> OccupiedCameraIds => _activeCaptures.Keys.ToList();

    public event Action<string>? CameraHandleLost;

    public async Task<bool> OccupyCameraAsync(string deviceSymbolicLinkOrId)
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(CameraOccupier));

        if (_activeCaptures.ContainsKey(deviceSymbolicLinkOrId))
        {
            return true; // Already occupied
        }

        try
        {
            Log.Information("Attempting exclusive camera occupancy for: {DeviceId}", deviceSymbolicLinkOrId);

            var capture = new MediaCapture();
            var settings = new MediaCaptureInitializationSettings
            {
                VideoDeviceId = deviceSymbolicLinkOrId,
                SharingMode = MediaCaptureSharingMode.ExclusiveControl,
                StreamingCaptureMode = StreamingCaptureMode.Video
            };

            await capture.InitializeAsync(settings);

            capture.Failed += (sender, args) =>
            {
                Log.Warning("MediaCapture handle lost for {DeviceId}: {Msg} (0x{Code:X8})",
                    deviceSymbolicLinkOrId, args.Message, args.Code);

                _ = ReleaseCameraAsync(deviceSymbolicLinkOrId);
                CameraHandleLost?.Invoke(deviceSymbolicLinkOrId);
            };

            _activeCaptures[deviceSymbolicLinkOrId] = capture;
            Log.Information("Successfully acquired exclusive lock on camera: {DeviceId}", deviceSymbolicLinkOrId);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to exclusively occupy camera {DeviceId}. Error HR: 0x{Hr:X8}",
                deviceSymbolicLinkOrId, ex.HResult);
            return false;
        }
    }

    public async Task ReleaseCameraAsync(string deviceSymbolicLinkOrId)
    {
        if (_activeCaptures.TryRemove(deviceSymbolicLinkOrId, out var capture))
        {
            try
            {
                capture.Dispose();
                Log.Information("Released camera lock for: {DeviceId}", deviceSymbolicLinkOrId);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Exception while disposing camera capture for {DeviceId}", deviceSymbolicLinkOrId);
            }
        }
    }

    public async Task ReleaseAllAsync()
    {
        var keys = _activeCaptures.Keys.ToList();
        foreach (var key in keys)
        {
            await ReleaseCameraAsync(key);
        }
    }

    public bool IsOccupied(string deviceSymbolicLinkOrId)
    {
        return _activeCaptures.ContainsKey(deviceSymbolicLinkOrId);
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            foreach (var kvp in _activeCaptures)
            {
                try
                {
                    kvp.Value.Dispose();
                }
                catch { }
            }
            _activeCaptures.Clear();
        }
    }
}
