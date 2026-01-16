using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace Geeklab.AudiencelabSDK
{
    public static class UserPropertiesManager
    {
        private const string WhitelistKey = "GeeklabSDK_UserProps_Whitelist";
        private const string BlacklistKey = "GeeklabSDK_UserProps_Blacklist";

        public const int MaxPropertiesPerSet = 50;
        public const int MaxKeyLength = 64;
        public const int MaxStringValueLength = 256;
        public const int MaxSerializedBytesPerSet = 2048;

        public static bool SetUserProperty(string key, object value, bool blacklisted = false)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning($"{SDKSettingsModel.GetColorPrefixLog()} User property key is null/empty.");
                return false;
            }

            if (key.StartsWith("_"))
            {
                Debug.LogWarning($"{SDKSettingsModel.GetColorPrefixLog()} User property keys starting with '_' are reserved.");
                return false;
            }

            if (key.Length > MaxKeyLength)
            {
                Debug.LogWarning($"{SDKSettingsModel.GetColorPrefixLog()} User property key exceeds {MaxKeyLength} characters.");
                return false;
            }

            if (value == null)
            {
                return UnsetUserProperty(key, blacklisted);
            }

            var properties = GetProperties(blacklisted);
            if (!properties.ContainsKey(key) && properties.Count >= MaxPropertiesPerSet)
            {
                Debug.LogWarning($"{SDKSettingsModel.GetColorPrefixLog()} User properties limit reached ({MaxPropertiesPerSet}).");
                return false;
            }

            if (value is string stringValue && stringValue.Length > MaxStringValueLength)
            {
                Debug.LogWarning($"{SDKSettingsModel.GetColorPrefixLog()} User property value exceeds {MaxStringValueLength} characters.");
                return false;
            }

            properties[key] = value;
            if (!TryPersistProperties(properties, blacklisted))
            {
                properties.Remove(key);
                return false;
            }

            return true;
        }

        public static bool UnsetUserProperty(string key, bool blacklisted = false)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning($"{SDKSettingsModel.GetColorPrefixLog()} User property key is null/empty.");
                return false;
            }

            if (key.StartsWith("_"))
            {
                Debug.LogWarning($"{SDKSettingsModel.GetColorPrefixLog()} User property keys starting with '_' are reserved.");
                return false;
            }

            var properties = GetProperties(blacklisted);
            if (!properties.Remove(key))
            {
                return false;
            }

            return TryPersistProperties(properties, blacklisted);
        }

        public static void ClearUserProperties(bool includeBlacklisted = false)
        {
            ClearPropertiesPreservingReserved(false);
            if (includeBlacklisted)
            {
                ClearPropertiesPreservingReserved(true);
            }
        }

        private static void ClearPropertiesPreservingReserved(bool blacklisted)
        {
            var properties = GetProperties(blacklisted);
            var reserved = new Dictionary<string, object>();

            foreach (var entry in properties)
            {
                if (entry.Key.StartsWith("_"))
                {
                    reserved[entry.Key] = entry.Value;
                }
            }

            var key = blacklisted ? BlacklistKey : WhitelistKey;
            PlayerPrefs.SetString(key, JsonConvert.SerializeObject(reserved));
            PlayerPrefs.Save();
        }

        public static Dictionary<string, object> GetWhitelistedProperties()
        {
            return GetProperties(false);
        }

        public static Dictionary<string, object> GetBlacklistedProperties()
        {
            return GetProperties(true);
        }

        public static void MergeWhitelistedProperties(Dictionary<string, object> incoming)
        {
            if (incoming == null || incoming.Count == 0)
            {
                return;
            }

            var properties = GetProperties(false);
            foreach (var entry in incoming)
            {
                if (string.IsNullOrEmpty(entry.Key) || entry.Key.Length > MaxKeyLength)
                {
                    continue;
                }

                if (entry.Value is string stringValue && stringValue.Length > MaxStringValueLength)
                {
                    continue;
                }

                if (!properties.ContainsKey(entry.Key) && properties.Count >= MaxPropertiesPerSet)
                {
                    continue;
                }

                properties[entry.Key] = entry.Value;
            }

            TryPersistProperties(properties, false);
        }

        private static Dictionary<string, object> GetProperties(bool blacklisted)
        {
            var key = blacklisted ? BlacklistKey : WhitelistKey;
            var json = PlayerPrefs.GetString(key, "{}");

            try
            {
                var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                return dict ?? new Dictionary<string, object>();
            }
            catch (Exception)
            {
                return new Dictionary<string, object>();
            }
        }

        private static bool TryPersistProperties(Dictionary<string, object> properties, bool blacklisted)
        {
            var json = JsonConvert.SerializeObject(properties);
            var byteCount = Encoding.UTF8.GetByteCount(json);
            if (byteCount > MaxSerializedBytesPerSet)
            {
                Debug.LogWarning($"{SDKSettingsModel.GetColorPrefixLog()} User properties exceed {MaxSerializedBytesPerSet} bytes.");
                return false;
            }

            var key = blacklisted ? BlacklistKey : WhitelistKey;
            PlayerPrefs.SetString(key, json);
            PlayerPrefs.Save();
            return true;
        }
    }
}
