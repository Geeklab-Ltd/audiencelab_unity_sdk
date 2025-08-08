using System.Collections.Generic;
using System.Threading.Tasks;
using Geeklab.AudiencelabSDK;
using UnityEngine;


public class AudiencelabSDK : MonoBehaviour
    {
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