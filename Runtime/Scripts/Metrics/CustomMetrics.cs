using System.Threading.Tasks;
using UnityEngine;

namespace Geeklab.AudiencelabSDK
{
    public static class CustomMetrics
    {
        public static async Task<bool> SendCustomEvent(string eventName, object properties = null, string dedupeKey = null)
        {
            if (!SDKSettingsModel.Instance.IsSDKEnabled || !SDKSettingsModel.Instance.SendStatistics)
                return false;

            if (string.IsNullOrEmpty(eventName))
            {
                Debug.LogWarning($"{SDKSettingsModel.GetColorPrefixLog()} Custom event name is required.");
                return false;
            }

            var taskCompletionSource = new TaskCompletionSource<bool>();

            var payload = new
            {
                en = eventName,
                pr = properties
            };

            WebRequestManager.Instance.SendCustomEventRequest(payload, dedupeKey, eventName, s =>
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
