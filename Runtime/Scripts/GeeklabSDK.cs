using System.Collections.Generic;
using System.Threading.Tasks;
using Geeklab.AudiencelabSDK;
using UnityEngine;


public class AudiencelabSDK : MonoBehaviour
    {
        private const string ManualGaidKey = "GeeklabSDK_ManualGAID";
        private const string ManualAppSetIdKey = "GeeklabSDK_ManualAppSetId";
        private bool showServiceInHierarchy;

        private static AudiencelabSDK instance;
        public static AudiencelabSDK Instance => Initialize();

        private static AudiencelabSDK Initialize()
        {
            if (instance != null) {
                return instance;
            }

            if (!SDKSettingsModel.Instance.IsSDKEnabled || string.IsNullOrEmpty(SDKSettingsModel.Instance.Token))
                return null;
            
            var gameObject = new GameObject("AudiencelabSDK");
            instance = gameObject.AddComponent<AudiencelabSDK>();
            DontDestroyOnLoad(gameObject);
            return instance;
        }
        
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnAfterSceneLoadRuntimeMethod() 
        {
            if (SDKSettingsModel.Instance.IsSDKEnabled && !string.IsNullOrEmpty(SDKSettingsModel.Instance.Token))
                Initialize();
        }
        
        
        private void Awake()
        {
            if (SDKSettingsModel.Instance != null && SDKSettingsModel.Instance.ShowDebugLog)
                Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} SDK Initialized!");
            
            //------Init All Managers and hide them------//
            var serviceLocator = new GameObject("ServiceLocator");
            serviceLocator.AddComponent<ServiceManager>();
            serviceLocator.hideFlags = showServiceInHierarchy ? serviceLocator.hideFlags : HideFlags.HideInHierarchy;
            gameObject.hideFlags = showServiceInHierarchy ? gameObject.hideFlags : HideFlags.HideInHierarchy;
        }
        
        /// <summary>
        /// Get Creative Token if any.
        /// </summary>
        /// <returns>Creative Token</returns>
        public static string GetCreativeToken() 
        {
            return TokenHandler.GetCreativeToken();
        }
        
        /// <summary>
        /// Get deep link URL if any.
        /// </summary>
        /// <returns>Deep link URL</returns>
        public static string GetDeepLink() 
        {
            return DeepLinkHandler.GetDeepLink();
        }
        
        /// <summary>
        /// Enable or disable metrics collection.
        /// </summary>
        /// <param name="isEnabled">A flag indicating whether to enable metrics collection</param>
        public static void ToggleMetricsCollection(bool isEnabled)
        {
            if (SDKSettingsModel.Instance != null)
                SDKSettingsModel.Instance.SendStatistics = isEnabled;
        }
        
        /// <summary>
        /// Check if metrics collection is enabled.
        /// </summary>
        /// <returns>True if metrics collection is enabled, false otherwise</returns>
        public static bool GetIsMetricsCollection()
        {
            if (SDKSettingsModel.Instance != null)
                return SDKSettingsModel.Instance.SendStatistics;
            else
                return false;
        }

        /// <summary>
        /// Send User Metrics to the server.
        /// </summary>
        public static async Task<bool?> SendUserMetrics(object data)
        {
            if (SDKSettingsModel.Instance == null || !IsConfigFullyEnabled(SDKSettingsModel.Instance.SendStatistics))
                return false;
            
            var postData = JsonConverter.ConvertToJson(data);
            return await UserMetrics.SendMetrics(postData);
        }
        
        /// <summary>
        /// Send purchase metrics to the server
        /// </summary>
        /// <param name="data">Purchase data. Can be a string, list, or dictionary.</param>
        public static async Task<bool?> SendCustomPurchaseMetrics(object data)
        {
            if (SDKSettingsModel.Instance == null || !IsConfigFullyEnabled(SDKSettingsModel.Instance.SendStatistics))
                return false;

            var postData = JsonConverter.ConvertToJson(data);
            return await PurchaseMetrics.SendPurchaseMetrics(postData);
        }
        
        /// <summary>
        /// Send advertisement metrics to the server.
        /// </summary>
        /// <param name="postData">Advertisement data to be sent. Can be a string, list, or dictionary.</param>
        public static async Task<bool?> SendCustomAdMetrics(object data)
        {
            if (SDKSettingsModel.Instance == null || !IsConfigFullyEnabled(SDKSettingsModel.Instance.SendStatistics))
                return false;

            var postData = JsonConverter.ConvertToJson(data);
            return await AdMetrics.SendMetrics(postData, true);
        }

        /// <summary>
        /// Send a custom event with arbitrary properties.
        /// </summary>
        public static async Task<bool?> SendCustomEvent(string eventName, object properties = null, string dedupeKey = null)
        {
            if (SDKSettingsModel.Instance == null || !IsConfigFullyEnabled(SDKSettingsModel.Instance.SendStatistics))
                return false;

            return await CustomMetrics.SendCustomEvent(eventName, properties, dedupeKey);
        }



        /// <summary>
        /// Get the current cumulative total ad value stored locally on the device.
        /// </summary>
        /// <returns>Total ad value accumulated</returns>
        public static double GetTotalAdValue()
        {
            return AdMetrics.GetTotalAdValue();
        }

        /// <summary>
        /// Get the current app version from Unity's Application.version
        /// </summary>
        /// <returns>App version string</returns>
        public static string GetAppVersion()
        {
            return SDKVersion.AppVersion;
        }

        /// <summary>
        /// Get the SDK version
        /// </summary>
        /// <returns>SDK version string</returns>
        public static string GetSDKVersion()
        {
            return SDKVersion.VERSION;
        }

        /// <summary>
        /// Get complete version information including app and SDK versions
        /// </summary>
        /// <returns>Complete version information string</returns>
        public static string GetUnityVersion()
        {
            return SDKVersion.UnityVersion;
        }

        /// <summary>
        /// Set a user property that will be attached to all requests.
        /// </summary>
        public static void SetUserProperty(string key, object value, bool blacklisted = false)
        {
            UserPropertiesManager.SetUserProperty(key, value, blacklisted);
        }

        /// <summary>
        /// Remove a user property.
        /// </summary>
        public static void UnsetUserProperty(string key, bool blacklisted = false)
        {
            UserPropertiesManager.UnsetUserProperty(key, blacklisted);
        }

        /// <summary>
        /// Clear all user properties.
        /// </summary>
        public static void ClearUserProperties(bool includeBlacklisted = false)
        {
            UserPropertiesManager.ClearUserProperties(includeBlacklisted);
        }

        /// <summary>
        /// Send an ad event triggered directly by the developer.
        /// </summary>
        public static void SendAdEvent(string ad_id, string name, string source, int watch_time, bool reward,
            string media_source, string channel, double value, string currency, string dedupeKey = null)
        {
            AdMetrics.SendCustomAdEvent(ad_id, name, source, watch_time, reward, media_source, channel, value, currency, dedupeKey);
        }

        /// <summary>
        /// Send a purchase event (preferred API).
        /// </summary>
        /// 
        public static void SendPurchaseEvent(string id, string name, double value, string currency, string status, string tr_id = null)
        {
            PurchaseMetrics.SendCustomPurchaseEvent(id, name, value, currency, status, tr_id);
        }


        /// <summary>
        /// Manually set GAID (Advertising ID) for Android when using manual mode.
        /// </summary>
        public static void SetAdvertisingId(string gaid)
        {
            var normalized = NormalizeAdvertisingId(gaid);
            if (string.IsNullOrEmpty(normalized) || IsAllZeroAdvertisingId(normalized))
            {
                return;
            }

            PlayerPrefs.SetString(ManualGaidKey, normalized);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Clear the manual GAID override.
        /// </summary>
        public static void ClearAdvertisingId()
        {
            PlayerPrefs.DeleteKey(ManualGaidKey);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Manually set App Set ID for Android when using manual mode.
        /// </summary>
        public static void SetAppSetId(string appSetId)
        {
            var normalized = string.IsNullOrEmpty(appSetId) ? null : appSetId.Trim();
            if (string.IsNullOrEmpty(normalized))
            {
                return;
            }

            PlayerPrefs.SetString(ManualAppSetIdKey, normalized);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Clear the manual App Set ID override.
        /// </summary>
        public static void ClearAppSetId()
        {
            PlayerPrefs.DeleteKey(ManualAppSetIdKey);
            PlayerPrefs.Save();
        }

        internal static string GetManualAdvertisingId()
        {
            var value = PlayerPrefs.GetString(ManualGaidKey, "");
            value = NormalizeAdvertisingId(value);
            if (string.IsNullOrEmpty(value) || IsAllZeroAdvertisingId(value))
            {
                return null;
            }

            return value;
        }

        internal static string GetManualAppSetId()
        {
            var value = PlayerPrefs.GetString(ManualAppSetIdKey, "");
            return string.IsNullOrEmpty(value) ? null : value.Trim();
        }

        private static string NormalizeAdvertisingId(string gaid)
        {
            return string.IsNullOrEmpty(gaid) ? null : gaid.Trim();
        }

        private static bool IsAllZeroAdvertisingId(string gaid)
        {
            if (string.IsNullOrEmpty(gaid))
            {
                return true;
            }

            var raw = gaid.Replace("-", "");
            if (raw.Length == 0)
            {
                return true;
            }

            foreach (var ch in raw)
            {
                if (ch != '0')
                {
                    return false;
                }
            }

            return true;
        }


        private static bool IsConfigFullyEnabled(bool value)
        { 
            if (SDKSettingsModel.Instance != null && SDKSettingsModel.Instance.IsSDKEnabled 
                                                  && !string.IsNullOrEmpty(SDKSettingsModel.Instance.Token))
            {
                if (value)
                {
                    return true;
                }
                else
                {
                    Debug.LogWarning($"This option is disabled in the settings!\n" + 
                                     "Please enable it in the GeeklabSDK -> SDK Setting menu");
                }
            }
            else
            {
                Debug.LogWarning($"GeeklabSDK is disabled!\n" + 
                                 "To work with the SDK, please enable it in the GeeklabSDK -> SDK Setting menu");
            }
            return false;
        }
    }