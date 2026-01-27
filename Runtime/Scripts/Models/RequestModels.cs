using System;
using System.Collections.Generic;
using Newtonsoft.Json;

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
        public string sdk_version;
        public string sdk_type;
        public string app_version;
        public string unity_version;
        public bool dev;
        [JsonProperty("ifv")]
        public string idfv;
        [JsonProperty("ga")]
        public string gaid;
        [JsonProperty("asid")]
        public string app_set_id;
        [JsonProperty("aid")]
        public string android_id;
        [JsonProperty("lat")]
        public bool? limit_ad_tracking;
        [JsonProperty("wp")]
        public Dictionary<string, object> whitelisted_properties;
        [JsonProperty("bp")]
        public Dictionary<string, object> blacklisted_properties;
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
        [JsonProperty("eid")]
        public string event_id;
        [JsonProperty("dk")]
        public string dedupe_key;
        public string created_at;
        public string creativeToken;
        public string device_name;
        public string device_model;
        public string os_system;
        public string utc_offset;
        public int? retention_day;
        public string sdk_version;
        public string sdk_type;
        public string app_version;
        public string unity_version;
        public bool dev;
        [JsonProperty("ifv")]
        public string idfv;
        [JsonProperty("ga")]
        public string gaid;
        [JsonProperty("asid")]
        public string app_set_id;
        [JsonProperty("aid")]
        public string android_id;
        [JsonProperty("lat")]
        public bool? limit_ad_tracking;
        [JsonProperty("wp")]
        public Dictionary<string, object> whitelisted_properties;
        [JsonProperty("bp")]
        public Dictionary<string, object> blacklisted_properties;
        public object payload;
    }
} 