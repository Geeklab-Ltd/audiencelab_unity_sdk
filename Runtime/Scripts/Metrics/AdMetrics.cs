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
        
        // PlayerPrefs key for storing cumulative ad views
        private const string TOTAL_AD_VIEWS_KEY = "GeeklabSDK_TotalAdViews";

        /// <summary>
        /// Gets the cumulative total ad views count from PlayerPrefs
        /// </summary>
        /// <returns>Total ad views count</returns>
        public static int GetTotalAdViews()
        {
            return PlayerPrefs.GetInt(TOTAL_AD_VIEWS_KEY, 0);
        }

        /// <summary>
        /// Increments and saves the total ad views count
        /// </summary>
        /// <returns>New total ad views count</returns>
        private static int IncrementTotalAdViews()
        {
            int currentTotal = GetTotalAdViews();
            int newTotal = currentTotal + 1;
            PlayerPrefs.SetInt(TOTAL_AD_VIEWS_KEY, newTotal);
            PlayerPrefs.Save();
            
            if (SDKSettingsModel.Instance.ShowDebugLog)
                Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Total ad views incremented to: {newTotal}");
            
            return newTotal;
        }

        public static void SendCustomAdEvent(string ad_id, string name, string source, int watch_time, bool reward, string media_source, string channel, double value, string currency)
        {
            if (!SDKSettingsModel.Instance.IsSDKEnabled) 
                return;
            
            if (SDKSettingsModel.Instance.ShowDebugLog)
                Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Sending custom.Ad event"); 

            // Increment the cumulative ad views counter
            int totalAdViews = IncrementTotalAdViews();
            
            var data = new {
                ad_id = ad_id,
                name = name,
                source = source,
                watch_time = watch_time,
                reward = reward,
                media_source = media_source,
                channel = channel,
                value = value,
                currency = currency,
                total_ad_views = totalAdViews
                };      

            SendMetrics(data, true);
        }

        /// <summary>
        /// Send a generic ad view event with automatic total_ad_views tracking
        /// </summary>
        /// <param name="ad_id">Unique identifier for the ad</param>
        /// <param name="ad_source">Source of the ad (e.g., "unity_ads", "admob")</param>
        /// <param name="watch_time">Time watched in seconds (optional)</param>
        /// <param name="reward">Whether this was a rewarded ad</param>
        public static void SendAdViewEvent(string ad_id, string ad_source, int watch_time = 0, bool reward = false)
        {
            if (!SDKSettingsModel.Instance.IsSDKEnabled) 
                return;
            
            if (SDKSettingsModel.Instance.ShowDebugLog)
                Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Sending ad view event"); 

            // Increment the cumulative ad views counter
            int totalAdViews = IncrementTotalAdViews();
            
            var data = new {
                ad_id = ad_id,
                name = "ad_view",
                source = ad_source,
                watch_time = watch_time,
                reward = reward,
                total_ad_views = totalAdViews
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
