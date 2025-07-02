using System;
using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;


namespace Geeklab.AudiencelabSDK
{
   public class AdMetrics : MonoBehaviour
    {
        private readonly WaitForSeconds waitForSeconds = new WaitForSeconds(0.5f);
        public static AdMetrics Instance;
        private static bool IsInitialized { get; set; }
        public bool ShowAdWasCalled { get; set; }
        public bool? IsUnityAdsReady { get; set; }
        
        // Track total value of ads viewed
        private static double _totalAdsValue = 0.0;
        private static bool _isInitialized = false;
        private const string TOTAL_ADS_VALUE_KEY = "GeeklabSDK_TotalAdsValue";
        
        public static double TotalAdsValue 
        { 
            get 
            {
                if (!_isInitialized)
                {
                    LoadTotalAdsValue();
                    _isInitialized = true;
                }
                return _totalAdsValue;
            }
            private set 
            {
                _totalAdsValue = value;
                SaveTotalAdsValue();
            }
        }

        private static void SaveTotalAdsValue()
        {
            PlayerPrefs.SetString(TOTAL_ADS_VALUE_KEY, _totalAdsValue.ToString());
            PlayerPrefs.Save();
        }

        private static void LoadTotalAdsValue()
        {
            if (PlayerPrefs.HasKey(TOTAL_ADS_VALUE_KEY))
            {
                if (double.TryParse(PlayerPrefs.GetString(TOTAL_ADS_VALUE_KEY), out double savedValue))
                {
                    _totalAdsValue = savedValue;
                }
            }
        }

        /// <summary>
        /// Resets the total ads value to zero. Useful for testing or when starting a new session.
        /// </summary>
        public static void ResetTotalAdsValue()
        {
            TotalAdsValue = 0.0;
            if (SDKSettingsModel.Instance.ShowDebugLog)
                Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Total ads value has been reset to 0");
        }

        /// <summary>
        /// Gets the current total value of ads viewed.
        /// </summary>
        /// <returns>The cumulative value of all rewarded ads that have been viewed</returns>
        public static double GetTotalAdsValue()
        {
            return TotalAdsValue;
        }

        /// <summary>
        /// Sends a custom ad event and automatically tracks the total value of rewarded ads.
        /// The total value is included in all webhook payloads sent to the server.
        /// </summary>
        /// <param name="ad_id">Unique identifier for the ad</param>
        /// <param name="name">Name of the ad event</param>
        /// <param name="source">Source of the ad</param>
        /// <param name="watch_time">Time spent watching the ad in seconds</param>
        /// <param name="reward">Whether the user received a reward for viewing the ad</param>
        /// <param name="media_source">Media source of the ad</param>
        /// <param name="channel">Channel where the ad was shown</param>
        /// <param name="value">Value of the ad reward (will be added to total if reward is true)</param>
        /// <param name="currency">Currency of the ad value</param>
        public static void SendCustomAdEvent(string ad_id, string name, string source, int watch_time, bool reward, string media_source, string channel, double value, string currency)
        {
            if (!SDKSettingsModel.Instance.IsSDKEnabled) 
                return;
            
            // Add the current ad value to the total
            if (reward && value > 0)
            {
                TotalAdsValue += value;
                if (SDKSettingsModel.Instance.ShowDebugLog)
                    Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Added {value} to total ads value. New total: {TotalAdsValue}");
            }
            
            if (SDKSettingsModel.Instance.ShowDebugLog)
                Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Sending custom.Ad event"); 
            
            var data = new {
                ad_id = ad_id,
                name = name,
                source = source,
                watch_time = watch_time,
                reward = reward,
                media_source = media_source,
                channel = channel,
                value = value,
                currency = currency
                };      

            SendMetrics(data, true);
        }

          public static async Task<bool> SendMetrics(object postData = null, bool isCustom = false)
        {
            if (!SDKSettingsModel.Instance.SendStatistics) 
                return false;
            
            var taskCompletionSource = new TaskCompletionSource<bool>();

            WebRequestManager.Instance.SendAdEventRequest(postData, isCustom, s =>
            {
                if (SDKSettingsModel.Instance.ShowDebugLog)
                    Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} {s}");
                taskCompletionSource.SetResult(true);
            }, error =>
            {
                Debug.LogError(error);
                taskCompletionSource.SetResult(false);
            });
          
            return await taskCompletionSource.Task;
        }
    }
}
