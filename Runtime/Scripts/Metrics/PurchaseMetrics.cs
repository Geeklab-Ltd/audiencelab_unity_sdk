using System;
using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace Geeklab.AudiencelabSDK
{

    public class PurchaseMetrics : MonoBehaviour
    {
  
        public static PurchaseMetrics Instance;
        public static void SendCustomPurchaseEvent(string id, string name, int value, string currency, string status)
        {
            if (!IsConfigFullyEnabled())
                return;
            
            if (SDKSettingsModel.Instance.ShowDebugLog)
                Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Sending custom.purchase event"); 
            
            idOfPurchasedItem = id;
            valueOfPurchase = value;
            token = SDKSettingsModel.Instance.Token;

            
            var data = new {
                item_id = idOfPurchasedItem,
                item_name = name,
                value = valueOfPurchase,
                currency = currency,
                status = status,
                };

            SendPurchaseMetrics(data, true);
        }

        private static string token;
        private static int valueOfPurchase;
        private static string idOfPurchasedItem;

         private static bool IsConfigFullyEnabled()
        { 
            if (SDKSettingsModel.Instance.IsSDKEnabled)
            {
                if (SDKSettingsModel.Instance.SendStatistics)
                {
                    return true;
                }
                else
                {
                    Debug.LogWarning($"Collection of information is disabled!\n" + 
                                     "Please enable it in the AudiencelabSDK -> SDK Setting menu");
                }
            }
            else
            {
                Debug.LogWarning($"AudiencelabSDK is disabled!\n" + 
                                 "To work with the SDK, please enable it in the AudiencelabSDK -> SDK Setting menu");
            }
            return false;
        }

        public static async Task<bool> SendPurchaseMetrics(object postData = null, bool isCustom = false)
        {
            if (!IsConfigFullyEnabled())
                return false;
            
            var taskCompletionSource = new TaskCompletionSource<bool>();

            WebRequestManager.Instance.SendPurchaseMetricsRequest(postData, isCustom, s =>
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