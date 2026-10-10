using System.Runtime.InteropServices;
using System.Text;
using AntiPrism.Core.Models;
using AntiPrism.Core.Native;
using Serilog;

namespace AntiPrism.Core.Hardware;

public static class CameraEnumerator
{
    public static List<CameraDevice> GetAllCameras()
    {
        var cameras = new Dictionary<string, CameraDevice>(StringComparer.OrdinalIgnoreCase);

        // 1. Try Media Foundation enumeration (captures friendly names & symbolic links)
        try
        {
            var mfCameras = EnumerateViaMediaFoundation();
            foreach (var cam in mfCameras)
            {
                cameras[cam.SymbolicLink] = cam;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "MediaFoundation camera enumeration encountered an error");
        }

        // 2. Fallback / Merge with SetupDi enumeration (ensures PnP instance IDs are bound)
        try
        {
            var pnpCameras = DeviceDisabler.EnumeratePnpCameras();
            foreach (var pnp in pnpCameras)
            {
                // Match existing by substring in symbolic link or add
                var existing = cameras.Values.FirstOrDefault(c =>
                    !string.IsNullOrEmpty(c.SymbolicLink) &&
                    (c.SymbolicLink.Contains(pnp.DeviceInstanceId, StringComparison.OrdinalIgnoreCase) ||
                     pnp.DeviceInstanceId.Contains(c.FriendlyName, StringComparison.OrdinalIgnoreCase)));

                if (existing != null)
                {
                    existing.DeviceInstanceId = pnp.DeviceInstanceId;
                }
                else if (!cameras.ContainsKey(pnp.Id))
                {
                    cameras[pnp.Id] = pnp;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "SetupDi camera enumeration encountered an error");
        }

        // 3. Complementary / UWP Fallback: Windows.Devices.Enumeration
        try
        {
            var task = Task.Run(async () =>
            {
                var selector = Windows.Media.Devices.MediaDevice.GetVideoCaptureSelector();
                return await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(selector);
            });

            if (task.Wait(2000) && task.Result != null)
            {
                foreach (var dev in task.Result)
                {
                    if (!cameras.ContainsKey(dev.Id))
                    {
                        cameras[dev.Id] = new CameraDevice
                        {
                            Id = dev.Id,
                            FriendlyName = dev.Name,
                            SymbolicLink = dev.Id,
                            DeviceInstanceId = ExtractInstanceIdFromSymbolicLink(dev.Id),
                            IsDisabled = !dev.IsEnabled,
                            IsOccupied = false
                        };
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Windows.Devices.Enumeration fallback encountered an error");
        }

        return cameras.Values.ToList();
    }

    private static List<CameraDevice> EnumerateViaMediaFoundation()
    {
        var result = new List<CameraDevice>();

        int hr = MediaFoundationNative.MFStartup(MediaFoundationNative.MF_VERSION);
        if (hr != 0)
        {
            Log.Warning("MFStartup returned HR {Hr}", hr);
            return result;
        }

        try
        {
            hr = MediaFoundationNative.MFCreateAttributes(out var attributes, 1);
            if (hr != 0 || attributes == null) return result;

            attributes.SetGUID(
                MediaFoundationNative.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE,
                MediaFoundationNative.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID);

            hr = MediaFoundationNative.MFEnumDeviceSources(attributes, out var pppActivate, out uint count);
            if (hr != 0 || pppActivate == nint.Zero || count == 0) return result;

            try
            {
                var activatePtrs = new nint[count];
                Marshal.Copy(pppActivate, activatePtrs, 0, (int)count);

                for (int i = 0; i < count; i++)
                {
                    if (activatePtrs[i] == nint.Zero) continue;
                    var activate = (MediaFoundationNative.IMFActivate)Marshal.GetObjectForIUnknown(activatePtrs[i]);

                    string friendlyName = "Camera Device";
                    string symLink = string.Empty;

                    try
                    {
                        var sbName = new StringBuilder(256);
                        activate.GetString(MediaFoundationNative.MF_DEVSOURCE_ATTRIBUTE_FRIENDLY_NAME, sbName, (uint)sbName.Capacity, out _);
                        friendlyName = sbName.ToString();
                    }
                    catch { }

                    try
                    {
                        var sbLink = new StringBuilder(1024);
                        activate.GetString(MediaFoundationNative.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_SYMBOLIC_LINK, sbLink, (uint)sbLink.Capacity, out _);
                        symLink = sbLink.ToString();
                    }
                    catch { }

                    if (!string.IsNullOrEmpty(symLink) || !string.IsNullOrEmpty(friendlyName))
                    {
                        var id = !string.IsNullOrEmpty(symLink) ? symLink : friendlyName;
                        result.Add(new CameraDevice
                        {
                            Id = id,
                            FriendlyName = friendlyName,
                            SymbolicLink = symLink,
                            DeviceInstanceId = ExtractInstanceIdFromSymbolicLink(symLink),
                            IsDisabled = false,
                            IsOccupied = false
                        });
                    }

                    Marshal.ReleaseComObject(activate);
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(pppActivate);
            }
        }
        finally
        {
            MediaFoundationNative.MFShutdown();
        }

        return result;
    }

    private static string ExtractInstanceIdFromSymbolicLink(string symLink)
    {
        if (string.IsNullOrEmpty(symLink)) return string.Empty;

        // SymbolicLink typically: \\?\usb#vid_046d&pid_0825&mi_00#6&...#{65e8773d-8f56-11d0-a3b9-00a0c9223196}\global
        var parts = symLink.Split('#');
        if (parts.Length >= 4)
        {
            // Reconstruct PnP device ID: USB\VID_xxxx&PID_xxxx\xxxx
            return $"{parts[1]}\\{parts[2]}".Replace("/", "\\");
        }

        return symLink;
    }
}
