using System;

namespace Geeklab.AudiencelabSDK
{
    [Serializable]
    public class DeviceInfoModel
    {
        public float Dpi;
        public int Width;
        public int Height;
        public int NativeWidth;
        public int NativeHeight;
        public bool LowPower;
        public string Timezone;
        public string OsVersion;
        public string DeviceName;
        public string GraphicsDeviceVendor;
        public string[] InstalledFonts;
        public object SystemInfoPackage;
        public object GpuContent;
    }
}