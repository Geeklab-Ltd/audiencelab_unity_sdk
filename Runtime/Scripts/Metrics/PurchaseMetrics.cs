using System;
using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace Geeklab.AudiencelabSDK
{

    public class PurchaseMetrics : MonoBehaviour
    {
        // PlayerPrefs key for storing cumulative purchase value
        private const string TOTAL_PURCHASE_VALUE_KEY = "GeeklabSDK_TotalPurchaseValue";

        public static PurchaseMetrics Instance;

        /// <summary>
        /// Gets the cumulative total purchase value from PlayerPrefs
        /// </summary>
        /// <returns>Total purchase value</returns>
        public static double GetTotalPurchaseValue()
        {
            return (double)PlayerPrefs.GetFloat(TOTAL_PURCHASE_VALUE_KEY, 0f);
        }

        /// <summary>
        /// Adds to the total purchase value and saves it
        /// </summary>
        /// <param name="purchaseValue">The value to add to the total</param>
        /// <returns>New total purchase value</returns>
        private static double AddToTotalPurchaseValue(double purchaseValue)
        {
            double currentTotal = GetTotalPurchaseValue();
            double newTotal = currentTotal + purchaseValue;
            PlayerPrefs.SetFloat(TOTAL_PURCHASE_VALUE_KEY, (float)newTotal);
            PlayerPrefs.Save();
            
            if (SDKSettingsModel.Instance.ShowDebugLog)
                Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Total purchase value updated by {purchaseValue:F4} to: {newTotal:F4}");
            
            return newTotal;
        }

        public static void SendCustomPurchaseEvent(string id, string name, double value, string currency, string status)
        {
            if (!IsConfigFullyEnabled())
                return;
            
            if (SDKSettingsModel.Instance.ShowDebugLog)
                Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Sending custom.purchase event"); 
            
            idOfPurchasedItem = id;
            valueOfPurchase = value;
            token = SDKSettingsModel.Instance.Token;

            // Add to the cumulative purchase value
            double totalPurchaseValue = AddToTotalPurchaseValue(value);

            var data = new {
                item_id = idOfPurchasedItem,
                item_name = name,
                value = valueOfPurchase,
                currency = currency,
                status = status,
                total_purchase_value = totalPurchaseValue
                };

            SendPurchaseMetrics(data, true);
        }

        /// <summary>
        /// Send a simplified purchase event with automatic total_purchase_value tracking
        /// </summary>
        /// <param name="item_id">Unique identifier for the purchased item</param>
        /// <param name="item_name">Name of the purchased item</param>
        /// <param name="value">The value of this purchase</param>
        /// <param name="currency">Currency of the purchase value</param>
        /// <param name="status">Status of the purchase (e.g., "Completed", "Failed")</param>
        public static void SendPurchaseEvent(string item_id, string item_name, double value, string currency = "USD", string status = "Completed")
        {
            if (!IsConfigFullyEnabled())
                return;
            
            if (SDKSettingsModel.Instance.ShowDebugLog)
                Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Sending purchase event"); 

            // Add to the cumulative purchase value
            double totalPurchaseValue = AddToTotalPurchaseValue(value);

            var data = new {
                item_id = item_id,
                item_name = item_name,
                value = value,
                currency = currency,
                status = status,
                total_purchase_value = totalPurchaseValue
                };

            SendPurchaseMetrics(data, false);
        }

        private static string token;
        private static double valueOfPurchase;
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