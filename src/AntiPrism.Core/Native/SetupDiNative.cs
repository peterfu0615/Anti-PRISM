using System.Runtime.InteropServices;

namespace AntiPrism.Core.Native;

public static class SetupDiNative
{
    public static readonly Guid GUID_DEVCLASS_CAMERA = new("ca3e7ab9-b4c3-4ae6-8251-579ef9338f4b");
    public static readonly Guid GUID_DEVCLASS_IMAGE = new("6bdd1fc6-810f-11d0-bec7-08002be2092f");

    public const uint DIGCF_DEFAULT = 0x00000001;
    public const uint DIGCF_PRESENT = 0x00000002;
    public const uint DIGCF_ALLCLASSES = 0x00000004;
    public const uint DIGCF_PROFILE = 0x00000008;
    public const uint DIGCF_DEVICEINTERFACE = 0x00000010;

    public const uint DIF_PROPERTYCHANGE = 0x00000012;

    public const uint DICS_ENABLE = 0x00000001;
    public const uint DICS_DISABLE = 0x00000002;
    public const uint DICS_PROPCHANGE = 0x00000003;
    public const uint DICS_START = 0x00000004;
    public const uint DICS_STOP = 0x00000005;

    public const uint DICS_FLAG_GLOBAL = 0x00000001;
    public const uint DICS_FLAG_CONFIGSPECIFIC = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    public struct SP_DEVINFO_DATA
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public nint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SP_CLASSINSTALL_HEADER
    {
        public uint cbSize;
        public uint InstallFunction;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SP_PROPCHANGE_PARAMS
    {
        public SP_CLASSINSTALL_HEADER ClassInstallHeader;
        public uint StateChange;
        public uint Scope;
        public uint HwProfile;
    }

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern nint SetupDiGetClassDevsW(
        ref Guid ClassGuid,
        string? Enumerator,
        nint hwndParent,
        uint Flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiEnumDeviceInfo(
        nint DeviceInfoSet,
        uint MemberIndex,
        ref SP_DEVINFO_DATA DeviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool SetupDiGetDeviceInstanceIdW(
        nint DeviceInfoSet,
        ref SP_DEVINFO_DATA DeviceInfoData,
        char[] DeviceInstanceId,
        uint DeviceInstanceIdSize,
        out uint RequiredSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiSetClassInstallParamsW(
        nint DeviceInfoSet,
        ref SP_DEVINFO_DATA DeviceInfoData,
        ref SP_PROPCHANGE_PARAMS ClassInstallParams,
        uint ClassInstallParamsSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiCallClassInstaller(
        uint InstallFunction,
        nint DeviceInfoSet,
        ref SP_DEVINFO_DATA DeviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiDestroyDeviceInfoList(nint DeviceInfoSet);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool SetupDiGetDeviceRegistryPropertyW(
        nint DeviceInfoSet,
        ref SP_DEVINFO_DATA DeviceInfoData,
        uint Property,
        out uint PropertyRegDataType,
        byte[] PropertyBuffer,
        uint PropertyBufferSize,
        out uint RequiredSize);

    public const uint SPDRP_DEVICEDESC = 0x00000000;
    public const uint SPDRP_FRIENDLYNAME = 0x0000000C;
    public const uint SPDRP_HARDWAREID = 0x00000001;
    public const uint SPDRP_CLASS = 0x00000007;
    public const uint SPDRP_CLASSGUID = 0x00000009;
}
