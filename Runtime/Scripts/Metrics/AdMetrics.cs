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
        
        // PlayerPrefs key for storing cumulative ad value
        private const string TOTAL_AD_VALUE_KEY = "GeeklabSDK_TotalAdValue";

        /// <summary>
        /// Gets the cumulative total ad value from PlayerPrefs
        /// </summary>
        /// <returns>Total ad value</returns>
        public static double GetTotalAdValue()
        {
            string storedValue = PlayerPrefs.GetString(TOTAL_AD_VALUE_KEY, "0");
            if (double.TryParse(storedValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double result))
            {
                return result;
            }
            return 0.0;
        }

        /// <summary>
        /// Adds to the total ad value and saves it
        /// </summary>
        /// <param name="adValue">The value to add to the total</param>
        /// <returns>New total ad value</returns>
        private static double AddToTotalAdValue(double adValue)
        {
            double currentTotal = GetTotalAdValue();
            double newTotal = currentTotal + adValue;
            PlayerPrefs.SetString(TOTAL_AD_VALUE_KEY, newTotal.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
            
            if (SDKSettingsModel.Instance.ShowDebugLog)
                Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Total ad value updated by {adValue} to: {newTotal}");
            
            return newTotal;
        }

        public static void SendCustomAdEvent(string ad_id, string name, string source, int watch_time, bool reward, string media_source, string channel, double value, string currency)
        {
            if (!SDKSettingsModel.Instance.IsSDKEnabled) 
                return;
            
            if (SDKSettingsModel.Instance.ShowDebugLog)
                Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Sending custom.Ad event"); 

            // Add to the cumulative ad value
            double totalAdValue = AddToTotalAdValue(value);
            
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
                total_ad_value = totalAdValue
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
