using System.Runtime.InteropServices;

namespace AntiPrism.Core.Native;

public static class MediaFoundationNative
{
    public const uint MF_SDK_VERSION = 0x0002;
    public const uint MF_API_VERSION = 0x0070;
    public const uint MF_VERSION = (MF_SDK_VERSION << 16) | MF_API_VERSION;

    public static readonly Guid MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE = new("c60fd687-4397-452c-acae-768516925e91");
    public static readonly Guid MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID = new("8ac3587a-4ae7-42d8-99e0-0a6013eef90f");
    public static readonly Guid MF_DEVSOURCE_ATTRIBUTE_FRIENDLY_NAME = new("96361450-5777-44a1-be5b-c697d725110f");
    public static readonly Guid MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_SYMBOLIC_LINK = new("58f0ac8e-4a6f-4f2e-9b54-77922e040a68");

    [DllImport("mfplat.dll", ExactSpelling = true)]
    public static extern int MFStartup(uint Version, uint dwFlags = 0);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    public static extern int MFShutdown();

    [DllImport("mfplat.dll", ExactSpelling = true)]
    public static extern int MFCreateAttributes(out IMFAttributes ppMFAttributes, uint cInitialSize);

    [DllImport("mf.dll", ExactSpelling = true)]
    public static extern int MFEnumDeviceSources(
        IMFAttributes pAttributes,
        out nint pppSourceActivate,
        out uint pcSourceActivate);

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("2CD2D921-6E24-4AB5-A8D6-E8014164CBD4")]
    public interface IMFAttributes
    {
        void GetItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, Out] nint pValue);
        void GetItemType([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out int pType);
        void CompareItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In] nint Value, out bool pbResult);
        void Compare([In] IMFAttributes pTheirs, int MatchType, out bool pbResult);
        void GetUINT32([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out uint punValue);
        void GetUINT64([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out ulong punValue);
        void GetDouble([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out double pfValue);
        void GetGUID([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out Guid pguidValue);
        void GetStringLength([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out uint pcchLength);
        void GetString([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pwszValue, uint cchBufSize, out uint pcchLength);
        void GetAllocatedString([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] out string ppwszValue, out uint pcchLength);
        void GetBlobSize([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out uint pcbBlobSize);
        void GetBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [Out, MarshalAs(UnmanagedType.LPArray)] byte[] pBuf, uint cbBufSize, out uint pcbBlobSize);
        void GetAllocatedBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out nint ppBuf, out uint pcbSize);
        void GetUnknown([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
        void SetItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In] nint Value);
        void DeleteItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey);
        void DeleteAllItems();
        void SetUINT32([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, uint unValue);
        void SetUINT64([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, ulong unValue);
        void SetDouble([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, double fValue);
        void SetGUID([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, MarshalAs(UnmanagedType.LPStruct)] Guid guidValue);
        void SetString([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, MarshalAs(UnmanagedType.LPWStr)] string wszValue);
        void SetBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, MarshalAs(UnmanagedType.LPArray)] byte[] pBuf, uint cbBufSize);
        void SetUnknown([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
        void LockStore();
        void UnlockStore();
        void GetCount(out uint pcItems);
        void GetItemByIndex(uint unIndex, out Guid pguidKey, [In, Out] nint pValue);
        void CopyAllItems([In] IMFAttributes pDest);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("7FEE9E9A-4A89-47A6-899C-B6A53A70FB67")]
    public interface IMFActivate : IMFAttributes
    {
        // Inherited from IMFAttributes
        new void GetItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, Out] nint pValue);
        new void GetItemType([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out int pType);
        new void CompareItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In] nint Value, out bool pbResult);
        new void Compare([In] IMFAttributes pTheirs, int MatchType, out bool pbResult);
        new void GetUINT32([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out uint punValue);
        new void GetUINT64([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out ulong punValue);
        new void GetDouble([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out double pfValue);
        new void GetGUID([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out Guid pguidValue);
        new void GetStringLength([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out uint pcchLength);
        new void GetString([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pwszValue, uint cchBufSize, out uint pcchLength);
        new void GetAllocatedString([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] out string ppwszValue, out uint pcchLength);
        new void GetBlobSize([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out uint pcbBlobSize);
        new void GetBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [Out, MarshalAs(UnmanagedType.LPArray)] byte[] pBuf, uint cbBufSize, out uint pcbBlobSize);
        new void GetAllocatedBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out nint ppBuf, out uint pcbSize);
        new void GetUnknown([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
        new void SetItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In] nint Value);
        new void DeleteItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey);
        new void DeleteAllItems();
        new void SetUINT32([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, uint unValue);
        new void SetUINT64([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, ulong unValue);
        new void SetDouble([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, double fValue);
        new void SetGUID([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, MarshalAs(UnmanagedType.LPStruct)] Guid guidValue);
        new void SetString([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, MarshalAs(UnmanagedType.LPWStr)] string wszValue);
        new void SetBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, MarshalAs(UnmanagedType.LPArray)] byte[] pBuf, uint cbBufSize);
        new void SetUnknown([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
        new void LockStore();
        new void UnlockStore();
        new void GetCount(out uint pcItems);
        new void GetItemByIndex(uint unIndex, out Guid pguidKey, [In, Out] nint pValue);
        new void CopyAllItems([In] IMFAttributes pDest);

        // IMFActivate members
        void ActivateObject([In, MarshalAs(UnmanagedType.LPStruct)] Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
        void ShutdownObject();
        void DetachObject();
    }
}
