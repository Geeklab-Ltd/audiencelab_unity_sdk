using System;

namespace Geeklab.AudiencelabSDK
{
    [Serializable]
    public class DeviceMetricsData
    {
        public string device_name;
        public int dpi;
        public string gpu_rendered;
        public string gpu_vendor;
        public string gpu_version;
        public string gpu_content;
        public int window_height;
        public int legacy_height;
        public int window_width;
        public int legacy_width;
        public string[] installed_fonts;
        public bool low_battery_level;
        public string os_system;
        public string device_model;
        public string timezone;
    }

    [Serializable]
    public class DeviceMetricsRequest
    {
        public string type;
        public DeviceMetricsData data;
        public string created_at;
    }

    [Serializable]
    public class TokenVerificationRequest
    {
        public string token;
    }

    [Serializable]
    public class WebhookRequestData
    {
        public string type;
        public string created_at;
        public string creativeToken;
        public string device_name;
        public string device_model;
        public string os_system;
        public string utc_offset;
        public string retention_day;
        public object payload;
    }
} 