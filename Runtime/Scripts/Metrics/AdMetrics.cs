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
        


        public static void SendCustomAdEvent(string ad_id, string name, string source, int watch_time, bool reward, string media_source, string channel)
        {
            if (!SDKSettingsModel.Instance.IsSDKEnabled) 
                return;
            
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
