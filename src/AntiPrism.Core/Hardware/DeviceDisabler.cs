using System.Runtime.InteropServices;
using System.Text;
using AntiPrism.Core.Models;
using AntiPrism.Core.Native;
using Serilog;

namespace AntiPrism.Core.Hardware;

public static class DeviceDisabler
{
    private static readonly Guid[] CameraClasses = new[]
    {
        SetupDiNative.GUID_DEVCLASS_CAMERA,
        SetupDiNative.GUID_DEVCLASS_IMAGE
    };

    public static List<CameraDevice> EnumeratePnpCameras()
    {
        var result = new List<CameraDevice>();

        foreach (var classGuid in CameraClasses)
        {
            var guidCopy = classGuid;
            var hDevInfo = SetupDiNative.SetupDiGetClassDevsW(
                ref guidCopy,
                null,
                nint.Zero,
                SetupDiNative.DIGCF_PRESENT);

            if (hDevInfo == (nint)(-1) || hDevInfo == nint.Zero)
            {
                continue;
            }

            try
            {
                var devInfoData = new SetupDiNative.SP_DEVINFO_DATA
                {
                    cbSize = (uint)Marshal.SizeOf<SetupDiNative.SP_DEVINFO_DATA>()
                };

                uint memberIndex = 0;
                while (SetupDiNative.SetupDiEnumDeviceInfo(hDevInfo, memberIndex++, ref devInfoData))
                {
                    var instanceIdBuffer = new char[512];
                    if (SetupDiNative.SetupDiGetDeviceInstanceIdW(hDevInfo, ref devInfoData, instanceIdBuffer, (uint)instanceIdBuffer.Length, out var requiredSize))
                    {
                        var instanceId = new string(instanceIdBuffer, 0, (int)requiredSize - 1);
                        var friendlyName = GetDevicePropertyString(hDevInfo, ref devInfoData, SetupDiNative.SPDRP_FRIENDLYNAME)
                                           ?? GetDevicePropertyString(hDevInfo, ref devInfoData, SetupDiNative.SPDRP_DEVICEDESC)
                                           ?? instanceId;

                        result.Add(new CameraDevice
                        {
                            Id = instanceId,
                            DeviceInstanceId = instanceId,
                            FriendlyName = friendlyName,
                            SymbolicLink = instanceId,
                            IsDisabled = false
                        });
                    }
                }
            }
            finally
            {
                SetupDiNative.SetupDiDestroyDeviceInfoList(hDevInfo);
            }
        }

        return result;
    }

    public static bool DisableDevice(string deviceInstanceId)
    {
        return ChangeDeviceState(deviceInstanceId, SetupDiNative.DICS_DISABLE);
    }

    public static bool EnableDevice(string deviceInstanceId)
    {
        return ChangeDeviceState(deviceInstanceId, SetupDiNative.DICS_ENABLE);
    }

    public static int EnableAllCameras()
    {
        int restoredCount = 0;
        Log.Information("Starting emergency camera restore (Unbrick)...");

        foreach (var classGuid in CameraClasses)
        {
            var guidCopy = classGuid;
            var hDevInfo = SetupDiNative.SetupDiGetClassDevsW(
                ref guidCopy,
                null,
                nint.Zero,
                SetupDiNative.DIGCF_ALLCLASSES);

            if (hDevInfo == (nint)(-1) || hDevInfo == nint.Zero)
            {
                continue;
            }

            try
            {
                var devInfoData = new SetupDiNative.SP_DEVINFO_DATA
                {
                    cbSize = (uint)Marshal.SizeOf<SetupDiNative.SP_DEVINFO_DATA>()
                };

                uint memberIndex = 0;
                while (SetupDiNative.SetupDiEnumDeviceInfo(hDevInfo, memberIndex++, ref devInfoData))
                {
                    var paramsStruct = new SetupDiNative.SP_PROPCHANGE_PARAMS
                    {
                        ClassInstallHeader = new SetupDiNative.SP_CLASSINSTALL_HEADER
                        {
                            cbSize = (uint)Marshal.SizeOf<SetupDiNative.SP_CLASSINSTALL_HEADER>(),
                            InstallFunction = SetupDiNative.DIF_PROPERTYCHANGE
                        },
                        StateChange = SetupDiNative.DICS_ENABLE,
                        Scope = SetupDiNative.DICS_FLAG_GLOBAL,
                        HwProfile = 0
                    };

                    if (SetupDiNative.SetupDiSetClassInstallParamsW(hDevInfo, ref devInfoData, ref paramsStruct, (uint)Marshal.SizeOf(paramsStruct)))
                    {
                        if (SetupDiNative.SetupDiCallClassInstaller(SetupDiNative.DIF_PROPERTYCHANGE, hDevInfo, ref devInfoData))
                        {
                            restoredCount++;
                        }
                    }
                }
            }
            finally
            {
                SetupDiNative.SetupDiDestroyDeviceInfoList(hDevInfo);
            }
        }

        Log.Information("Emergency restore completed. Restored devices count: {Count}", restoredCount);
        return restoredCount;
    }

    private static bool ChangeDeviceState(string targetInstanceId, uint stateChange)
    {
        foreach (var classGuid in CameraClasses)
        {
            var guidCopy = classGuid;
            var hDevInfo = SetupDiNative.SetupDiGetClassDevsW(
                ref guidCopy,
                null,
                nint.Zero,
                SetupDiNative.DIGCF_ALLCLASSES);

            if (hDevInfo == (nint)(-1) || hDevInfo == nint.Zero)
            {
                continue;
            }

            try
            {
                var devInfoData = new SetupDiNative.SP_DEVINFO_DATA
                {
                    cbSize = (uint)Marshal.SizeOf<SetupDiNative.SP_DEVINFO_DATA>()
                };

                uint memberIndex = 0;
                while (SetupDiNative.SetupDiEnumDeviceInfo(hDevInfo, memberIndex++, ref devInfoData))
                {
                    var instanceIdBuffer = new char[512];
                    if (SetupDiNative.SetupDiGetDeviceInstanceIdW(hDevInfo, ref devInfoData, instanceIdBuffer, (uint)instanceIdBuffer.Length, out var requiredSize))
                    {
                        var instanceId = new string(instanceIdBuffer, 0, (int)requiredSize - 1);
                        if (string.Equals(instanceId, targetInstanceId, StringComparison.OrdinalIgnoreCase))
                        {
                            var paramsStruct = new SetupDiNative.SP_PROPCHANGE_PARAMS
                            {
                                ClassInstallHeader = new SetupDiNative.SP_CLASSINSTALL_HEADER
                                {
                                    cbSize = (uint)Marshal.SizeOf<SetupDiNative.SP_CLASSINSTALL_HEADER>(),
                                    InstallFunction = SetupDiNative.DIF_PROPERTYCHANGE
                                },
                                StateChange = stateChange,
                                Scope = SetupDiNative.DICS_FLAG_GLOBAL,
                                HwProfile = 0
                            };

                            if (SetupDiNative.SetupDiSetClassInstallParamsW(hDevInfo, ref devInfoData, ref paramsStruct, (uint)Marshal.SizeOf(paramsStruct)))
                            {
                                bool success = SetupDiNative.SetupDiCallClassInstaller(SetupDiNative.DIF_PROPERTYCHANGE, hDevInfo, ref devInfoData);
                                if (!success)
                                {
                                    int err = Marshal.GetLastWin32Error();
                                    Log.Warning("SetupDiCallClassInstaller failed for {InstanceId} with code {Error}", targetInstanceId, err);
                                }
                                return success;
                            }
                        }
                    }
                }
            }
            finally
            {
                SetupDiNative.SetupDiDestroyDeviceInfoList(hDevInfo);
            }
        }

        return false;
    }

    private static string? GetDevicePropertyString(nint hDevInfo, ref SetupDiNative.SP_DEVINFO_DATA devInfoData, uint property)
    {
        var buffer = new byte[1024];
        if (SetupDiNative.SetupDiGetDeviceRegistryPropertyW(
            hDevInfo,
            ref devInfoData,
            property,
            out _,
            buffer,
            (uint)buffer.Length,
            out var requiredSize) && requiredSize > 0)
        {
            return Encoding.Unicode.GetString(buffer, 0, (int)requiredSize).TrimEnd('\0');
        }

        return null;
    }
}
