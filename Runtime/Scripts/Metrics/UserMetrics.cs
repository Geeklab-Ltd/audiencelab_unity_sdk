using System;
using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace Geeklab.AudiencelabSDK
{
    public class UserMetrics : MonoBehaviour
    {
        private void Start()
        {
            // Initialize firstLogin and calculate retentionDay early (doesn't require token)
            // This ensures retentionDay is available for any events that get queued before token arrives
            if (string.IsNullOrEmpty(PlayerPrefs.GetString("firstLogin")))
            {
                InitializeFirstLogin();
            }
            EnsureRetentionDayCalculated();

            // Check if token already exists
            if (TokenHandler.HasValidToken())
            {
                Debug.Log("Creative token found");
                UpdateRetention();
            }
            else
            {
                // Subscribe to token available event instead of polling
                TokenHandler.OnTokenAvailable += HandleTokenAvailable;
            }
        }

        private void OnDestroy()
        {
            TokenHandler.OnTokenAvailable -= HandleTokenAvailable;
        }

        private void HandleTokenAvailable(string token)
        {
            TokenHandler.OnTokenAvailable -= HandleTokenAvailable;
            Debug.Log("Creative token now available");
            UpdateRetention();
        }

        /// <summary>
        /// Calculate and store retentionDay in PlayerPrefs without sending metrics.
        /// This can run before the token is available.
        /// </summary>
        private static void EnsureRetentionDayCalculated()
        {
            var firstLogin = PlayerPrefs.GetString("firstLogin");
            if (string.IsNullOrEmpty(firstLogin))
                return;

            try
            {
                var today = DateTime.Now.ToString("dd/MM/yyyy");
                var firstLoginDate = DateTime.ParseExact(firstLogin, "dd/MM/yyyy", null);
                var todayDate = DateTime.ParseExact(today, "dd/MM/yyyy", null);
                var daysBetween = (todayDate - firstLoginDate).Days;
                PlayerPrefs.SetInt("retentionDay", daysBetween);
                PlayerPrefs.Save();
            }
            catch (Exception)
            {
                // Ignore parsing errors - retentionDay will remain unset
            }
        }


        public static void InitializeFirstLogin()
        {
            PlayerPrefs.SetString("firstLogin", DateTime.Now.ToString("dd/MM/yyyy"));
            PlayerPrefs.SetString("lastLogin", DateTime.Now.ToString("dd/MM/yyyy"));
        }



        public static void UpdateRetention()
        {
            var today = DateTime.Now.ToString("dd/MM/yyyy");
            if (PlayerPrefs.GetString("lastSentMetricDate") == today)
            {
                return;
            }
            var lastLogin = PlayerPrefs.GetString("lastLogin");
            var firstLogin = PlayerPrefs.GetString("firstLogin");
            var firstLoginDate = DateTime.ParseExact(firstLogin, "dd/MM/yyyy", null);
            var daysBetween = 0;

            if (lastLogin != today)
            {
                var lastLoginDate = DateTime.ParseExact(lastLogin, "dd/MM/yyyy", null);
                daysBetween = (lastLoginDate - firstLoginDate).Days;
                PlayerPrefs.SetInt("backfillDay", daysBetween);
                PlayerPrefs.SetString("lastLogin", DateTime.Now.ToString("dd/MM/yyyy"));
            } else {
                PlayerPrefs.SetInt("backfillDay", 0);
            }
            

            var todayDate = DateTime.ParseExact(today, "dd/MM/yyyy", null);
            daysBetween = (todayDate - firstLoginDate).Days;
            PlayerPrefs.SetInt("retentionDay", daysBetween);

            var backfillDay = PlayerPrefs.GetInt("backfillDay").ToString();
            var retentionDay = PlayerPrefs.GetInt("retentionDay").ToString();

            var data = new { 
                retentionDay = retentionDay, 
                backfillDay = backfillDay 
            };
        
            PlayerPrefs.SetString("lastSentMetricDate", today);
            _ = SendMetrics(data);
        }
        
        
        public static async Task<bool> SendMetrics(object postData = null)
        {
            if (!SDKSettingsModel.Instance.SendStatistics) 
                return false;

            var taskCompletionSource = new TaskCompletionSource<bool>();
            Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Send metrics");
            Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} {postData}");
            WebRequestManager.Instance.SendUserMetricsRequest(postData,
                (response) =>
                {
                    if (SDKSettingsModel.Instance.ShowDebugLog)
                        Debug.Log(
                            $"{SDKSettingsModel.GetColorPrefixLog()} {response}");
                    taskCompletionSource.SetResult(true);
                },
                (error) =>
                {
                    Debug.LogError(error);
                    taskCompletionSource.SetResult(false);
                }
            );
            
            return await taskCompletionSource.Task;
        }
    }
}