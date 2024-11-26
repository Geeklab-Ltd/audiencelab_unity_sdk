using UnityEngine.Networking;
using System.Collections;
using System.Text;
using UnityEngine;
using System.Net;
using System.IO;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;


namespace Geeklab.AudiencelabSDK
{
    public class WebRequestManager : MonoBehaviour
    {
        private static bool isDebugOn = true;

        private static WebRequestManager instance;

        public static WebRequestManager Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject(nameof(WebRequestManager));
                    instance = go.AddComponent<WebRequestManager>();
                    DontDestroyOnLoad(go);
                }

                return instance;
            }
        }


        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        
        public void CheckDataCollectionStatusRequest(Action<string> onSuccess = null, Action<string> onError = null)
        {
            SendRequest(ApiEndpointsModel.CHECK_DATA_COLLECTION_STATUS, "", onSuccess, onError,
                UnityWebRequest.kHttpVerbGET);
        }
        

        public void SendUserMetricsRequest(object data, Action<string> onSuccess = null, Action<string> onError = null)
        {
            SendWebhookRequest("retention", data, onSuccess, onError);
        }

        public void SendAdEventRequest(object data, bool isCustom, Action<string> onSuccess = null, Action<string> onError = null)
        {
            var type = "ad";
            if (isCustom) type = "custom.ad";

            SendWebhookRequest(type, data, onSuccess, onError);
        }


        public void SendPurchaseMetricsRequest(object data, bool isCustom, Action<string> onSuccess = null, Action<string> onError = null)
        {
            var type = "purchase";
            if (isCustom) type = "custom.purchase";
                
            SendWebhookRequest(type, data, onSuccess, onError);
        }
        
        
        public void VerifyCreativeTokenRequest(string token, Action<string> onSuccess = null, Action<string> onError = null)
        {
            var postData = new
            {
                token = token,
            };
            var json = JsonConvert.SerializeObject(postData);
            SendRequest(ApiEndpointsModel.VERIFY_TOKEN, json, onSuccess, onError);
        }
        

        public void FetchTokenRequest(Action<string> onSuccess, Action<string> onError = null)
        {

            var currentDate = DateTime.Now;
            var currentDateText = currentDate.ToString("yyyy-MM-dd HH:mm:ss");

            var deviceInfo = DeviceInfoHandler.GetDeviceInfo();
            var postData = new
            {
                device_name = deviceInfo.DeviceName,
                dpi = (int)deviceInfo.Dpi,
                gpu_rendered = SystemInfo.graphicsDeviceID.ToString(),
                gpu_vendor = SystemInfo.graphicsDeviceVendor,
                gpu_version = SystemInfo.graphicsDeviceVersion,
                gpu_content =  deviceInfo.GpuContent,
                window_height = deviceInfo.NativeHeight,
                legacy_height = deviceInfo.Height,
                window_width = deviceInfo.NativeWidth,
                legacy_width = deviceInfo.Width,
                installed_fonts = deviceInfo.InstalledFonts,
                low_battery_level = deviceInfo.LowPower,
                os_system = deviceInfo.OsVersion,
                device_model = SystemInfo.deviceModel,
                timezone = deviceInfo.Timezone, 
            };

            var postDataFull = new
            {
                type = "device-metrics",
                data = postData,
                created_at = currentDateText,
            };
            
            var json = JsonConvert.SerializeObject(postDataFull);
            Debug.Log(json);
            SendRequest(ApiEndpointsModel.FETCH_TOKEN, json, onSuccess, onError);

        }

        private string GetUtcOffset()
        {
            // Get current UTC offset
            TimeSpan offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);

            // Format as "+HH:mm" or "-HH:mm"
            string formattedOffset = offset >= TimeSpan.Zero
                ? $"+{offset.Hours:D2}:{offset.Minutes:D2}"
                : $"-{Math.Abs(offset.Hours):D2}:{Math.Abs(offset.Minutes):D2}";

            return formattedOffset;
        }

        
        private void SendWebhookRequest(string type, object data, Action<string> onSuccess = null, Action<string> onError = null)
        {

            var currentDate = DateTime.Now;
            var currentDateText = currentDate.ToString("yyyy-MM-dd HH:mm:ss");

            var deviceInfo = DeviceInfoHandler.GetDeviceInfo();

            // Get UTC offset

            var utcOffset = GetUtcOffset();

            string retentionDay;
            if (PlayerPrefs.HasKey("retentionDay") && PlayerPrefs.GetString("retentionDay") != "") {
                retentionDay = PlayerPrefs.GetString("retentionDay");
            } else {
                retentionDay = "0";
            }


            var postData = new
            {
                type = type,
                created_at = currentDateText,
                creativeToken = TokenHandler.GetCreativeToken(),
                device_name = deviceInfo.DeviceName,
                device_model = SystemInfo.deviceModel,
                os_system = deviceInfo.OsVersion,
                utc_offset = utcOffset,
                retention_day = retentionDay,
                payload = data
            };
            var json = JsonConvert.SerializeObject(postData);
            Debug.Log(json);
            SendRequest(ApiEndpointsModel.WEBHOOK, json, onSuccess, onError);
        }
        
        
        private void SendRequest(string endpoint, string json, Action<string> onSuccess, Action<string> onError = null,
            string method = UnityWebRequest.kHttpVerbPOST, Dictionary<string, string> headerData = null)
        {
            if (IsInternetAvailable())
            {
                StartCoroutine(SendRequestCoroutine(endpoint, json, onSuccess, onError, method, headerData));
            }
            else
            {
                Debug.LogWarning(
                    $"{SDKSettingsModel.GetColorPrefixLog()} There is no Internet connection. Please check your connection and try again.");
            }
        }


        private static IEnumerator SendRequestCoroutine(string endpoint, string json, Action<string> onSuccess,
            Action<string> onError, string method, Dictionary<string, string> headerData = null)
        {
            using (UnityWebRequest www = new UnityWebRequest(endpoint, method))
            {
                if (method == UnityWebRequest.kHttpVerbPOST)
                {
                    var bodyRaw = Encoding.UTF8.GetBytes(json);
                    www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                }

                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");

                if (headerData != null)
                {
                    foreach (var headerItem in headerData)
                    {
                        www.SetRequestHeader(headerItem.Key, headerItem.Value);
                    }
                }

                if (!string.IsNullOrEmpty(SDKSettingsModel.Instance.Token))
                {
                    www.SetRequestHeader("geeklab-api-key", SDKSettingsModel.Instance.Token);
                }

                yield return www.SendWebRequest();
                
#pragma warning disable CS0618
                if (www.isNetworkError || www.isHttpError)
#pragma warning restore CS0618
                {
                    switch (www.responseCode)
                    {
                        case 400:
                            DebugLogError("Bad request, data not formatted properly.", onError);
                            break;
                        case 401:
                            DebugLogError("API key is not valid.", onError);
                            break;
                        case 404:
                            DebugLogError($"{www.error}\n{www.downloadHandler.text}", onError);
                            break;
                        case 500:
                            DebugLogError("Server error.\n" + www.downloadHandler.text + "\n", onError);
                            break;
                        default:
                            DebugLogError($"Error: {www.error}\n" + www.downloadHandler.text + "\n", onError);
                            break;
                    }
                }
                else
                {
                    try
                    {
                        onSuccess?.Invoke(www.downloadHandler.text);
                    }
                    catch (WebException webEx)
                    {
                        DebugLogError($"Exception encountered: {webEx.Message}", onError);
                    }
                    catch (IOException ioEx)
                    {
                        DebugLogError($"IOException encountered: {ioEx.Message}", onError);
                    }
                    catch (Exception ex)
                    {
                        DebugLogError($"Unexpected exception encountered: {ex.Message}", onError);
                    }
                }
            }
        }



        private static bool IsInternetAvailable()
        {
            return Application.internetReachability != NetworkReachability.NotReachable;
        }



        public static void DebugLogError(string message, Action<string> onError)
        {
            if (onError == null && isDebugOn)
            {
                Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} {message}");
            }
            else
            {
                onError?.Invoke($"{SDKSettingsModel.GetColorPrefixLog()} {message}");
            }
        }
    }
}