using System;
using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace Geeklab.AudiencelabSDK
{
    public class UserMetrics : MonoBehaviour
    {
        private static float sessionTime;
        private static int timesTriedToFetchToken = 0;

        
        
        private void Start()
        {

            timesTriedToFetchToken++;


            if (PlayerPrefs.GetString("GeeklabCreativeToken") == "" || PlayerPrefs.GetString("GeeklabCreativeToken") == null)
            {   
                Debug.Log("Waiting for creative token");
                if (timesTriedToFetchToken < 31){
                    Invoke("Start", 2);
                } else {   
                    TokenHandler.SetToken("BIN");
                    Debug.Log("Creative token not found");
                }
            }
            else
            {
                Debug.Log("Creative token found");
                sessionTime = 0.0f;
                if (PlayerPrefs.GetString("firstLogin") == "")
                {
                    InitializeFirstLogin(); 

                }

                // set last login
                UpdateRetention();

            }

        }

        private void Update()
        {
            // Update session time
            sessionTime += Time.deltaTime;
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
            SendMetrics(data);
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