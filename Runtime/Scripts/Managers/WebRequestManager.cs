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
        private const int MaxQueuedEvents = 200;
        private const double MaxQueuedAgeHours = 24d;
        private const float IdentityWaitTimeoutSeconds = 2f;

        private static WebRequestManager instance;
        private static List<QueuedWebhookRequest> queuedWebhookRequests;
        private static bool isFlushRunning;
        private static bool hasLoggedQueueDrop;
        private static long queueSequence;

        internal static RequestEnvelopeSnapshot LastWebhookEnvelope;

        internal static event Action<RequestResult> OnRequestResult;

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


        private bool wasOffline;
        private float lastConnectivityCheckTime;
        private const float ConnectivityCheckIntervalSeconds = 5f;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                TokenHandler.OnTokenAvailable += HandleTokenAvailable;
                wasOffline = !IsInternetAvailable();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            // Periodically check for connectivity changes and flush queue when back online
            if (Time.realtimeSinceStartup - lastConnectivityCheckTime < ConnectivityCheckIntervalSeconds)
                return;

            lastConnectivityCheckTime = Time.realtimeSinceStartup;

            var isOnline = IsInternetAvailable();
            if (wasOffline && isOnline)
            {
                // Connection restored - reset retry counts and try to flush queued events
                if (SDKSettingsModel.Instance != null && SDKSettingsModel.Instance.ShowDebugLog)
                {
                    Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Internet connection restored - resetting retry counts and flushing queued events");
                }
                TokenHandler.ResetRetryCount();
                TokenHandler.StartRetryLoop(); // Restart token fetch if needed
                FlushQueuedWebhookRequests();
            }
            wasOffline = !isOnline;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                TokenHandler.OnTokenAvailable -= HandleTokenAvailable;
            }
        }

        
        public void CheckDataCollectionStatusRequest(Action<string> onSuccess = null, Action<string> onError = null)
        {
            SendRequest(ApiEndpointsModel.CHECK_DATA_COLLECTION_STATUS, "", onSuccess, onError,
                UnityWebRequest.kHttpVerbGET);
        }
        

        public void SendUserMetricsRequest(object data, Action<string> onSuccess = null, Action<string> onError = null)
        {
            SendWebhookRequest("retention", data, null, false, onSuccess, onError);
        }

        public void SendAdEventRequest(object data, bool isCustom, string dedupeKey = null, Action<string> onSuccess = null, Action<string> onError = null)
        {
            var type = "ad";
            if (isCustom) type = "custom.ad";

            SendWebhookRequest(type, data, dedupeKey, false, onSuccess, onError);
        }


        public void SendPurchaseMetricsRequest(object data, bool isCustom, string dedupeKey = null, Action<string> onSuccess = null, Action<string> onError = null)
        {
            var type = "purchase";
            if (isCustom) type = "custom.purchase";
                
            SendWebhookRequest(type, data, dedupeKey, false, onSuccess, onError);
        }
        
        
        public void VerifyCreativeTokenRequest(string token, Action<string> onSuccess = null, Action<string> onError = null)
        {
            var postData = new TokenVerificationRequest
            {
                token = token
            };
            var json = JsonConvert.SerializeObject(postData);
            SendRequest(ApiEndpointsModel.VERIFY_TOKEN, json, onSuccess, onError);
        }
        

        public void FetchTokenRequest(Action<string> onSuccess, Action<string> onError = null)
        {
            if (SDKSettingsModel.Instance != null && SDKSettingsModel.Instance.ShowDebugLog)
            {
                Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} FetchTokenRequest called, waiting for identity: {!IdentityHandler.IsSettled}");
            }
            FetchTokenRequestInternal(onSuccess, onError, true);
        }

        private void FetchTokenRequestInternal(Action<string> onSuccess, Action<string> onError, bool waitForIdentity)
        {
            if (waitForIdentity && !IdentityHandler.IsSettled)
            {
                if (SDKSettingsModel.Instance != null && SDKSettingsModel.Instance.ShowDebugLog)
                {
                    Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Waiting for identity to settle before fetch-token...");
                }
                StartCoroutine(IdentityHandler.WaitForIdentitySettle(() =>
                    FetchTokenRequestInternal(onSuccess, onError, false)));
                return;
            }

            var currentDate = DateTime.UtcNow;
            var currentDateText = currentDate.ToString("yyyy-MM-dd HH:mm:ss");

            var deviceInfo = DeviceInfoHandler.GetDeviceInfo();
            var identityInfo = IdentityHandler.Current;
            var whitelisted = UserPropertiesManager.GetWhitelistedProperties();
            var blacklisted = UserPropertiesManager.GetBlacklistedProperties();

            if (SDKSettingsModel.Instance != null && SDKSettingsModel.Instance.ShowDebugLog)
            {
                Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Sending fetch-token with identity: gaid={identityInfo.gaid ?? "null"}, app_set_id={identityInfo.app_set_id ?? "null"}, android_id={identityInfo.android_id ?? "null"}, idfv={identityInfo.idfv ?? "null"}");
            }
            
            var postData = new DeviceMetricsData
            {
                device_name = deviceInfo.DeviceName,
                dpi = (int)deviceInfo.Dpi,
                gpu_rendered = SystemInfo.graphicsDeviceID.ToString(),
                gpu_vendor = SystemInfo.graphicsDeviceVendor,
                gpu_version = SystemInfo.graphicsDeviceVersion,
                gpu_content = deviceInfo.GpuContent.ToString() ,
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

            var postDataFull = new DeviceMetricsRequest
            {
                type = "device-metrics",
                data = postData,
                created_at = currentDateText,
                sdk_version = SDKVersion.VERSION,
                sdk_type = SDKVersion.SDK_TYPE,
                app_version = SDKVersion.AppVersion,
                unity_version = SDKVersion.UnityVersion,
                dev = Application.isEditor || Debug.isDebugBuild,
                idfv = identityInfo.idfv,
                gaid = identityInfo.gaid,
                app_set_id = identityInfo.app_set_id,
                android_id = identityInfo.android_id,
                limit_ad_tracking = identityInfo.limit_ad_tracking,
                whitelisted_properties = whitelisted,
                blacklisted_properties = blacklisted
            };
            
            var json = JsonConvert.SerializeObject(postDataFull);
            Debug.Log(json);
            var meta = new RequestMeta("fetch-token", null, null, null);
            SendRequest(ApiEndpointsModel.FETCH_TOKEN, json, onSuccess, onError, UnityWebRequest.kHttpVerbPOST, null, meta);
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

        
        public void SendSessionEventRequest(object data, bool waitForIdentity, Action<string> onSuccess = null, Action<string> onError = null)
        {
            SendWebhookRequest("session", data, null, waitForIdentity, onSuccess, onError);
        }

        public void SendCustomEventRequest(object data, string dedupeKey = null, string eventName = null, Action<string> onSuccess = null, Action<string> onError = null)
        {
            SendWebhookRequest("custom", data, dedupeKey, false, onSuccess, onError, eventName);
        }

        private void SendWebhookRequest(string type, object data, string dedupeKey, bool waitForIdentity, Action<string> onSuccess = null, Action<string> onError = null, string eventName = null)
        {
            if (!TokenHandler.HasValidToken())
            {
                EnqueueWebhookRequest(type, data, dedupeKey, eventName, waitForIdentity);
                TokenHandler.StartRetryLoop();
                return;
            }

            SendWebhookRequestInternal(type, data, dedupeKey, waitForIdentity, eventName, DateTime.UtcNow, null, onSuccess, onError);
        }

        private void SendWebhookRequestInternal(string type, object data, string dedupeKey, bool waitForIdentity, string eventName,
            DateTime createdAt, string eventIdOverride, Action<string> onSuccess, Action<string> onError, int? retentionDayOverride = null)
        {
            if ((waitForIdentity || ShouldWaitForIdentity()) && !IdentityHandler.IsSettled)
            {
                StartCoroutine(WaitForIdentityOrTimeout(() =>
                    SendWebhookRequestInternal(type, data, dedupeKey, false, eventName, createdAt, eventIdOverride, onSuccess, onError, retentionDayOverride)));
                return;
            }

            // Check for internet - if offline, queue the request instead of losing it
            if (!IsInternetAvailable())
            {
                EnqueueWebhookRequest(type, data, dedupeKey, eventName, waitForIdentity, eventIdOverride);
                if (SDKSettingsModel.Instance != null && SDKSettingsModel.Instance.ShowDebugLog)
                {
                    Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} No internet - queued {type} event for later");
                }
                onError?.Invoke("Queued for retry - no internet");
                return;
            }

            var currentDateText = createdAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss");
            var deviceInfo = DeviceInfoHandler.GetDeviceInfo();
            var utcOffset = GetUtcOffset();
            var identityInfo = IdentityHandler.Current;
            var whitelisted = UserPropertiesManager.GetWhitelistedProperties();
            var blacklisted = UserPropertiesManager.GetBlacklistedProperties();

            // Use override if provided (from queued request), otherwise read from PlayerPrefs
            int? retentionDay = retentionDayOverride;
            if (!retentionDay.HasValue && PlayerPrefs.HasKey("retentionDay"))
            {
                retentionDay = PlayerPrefs.GetInt("retentionDay");
            }

            var eventId = string.IsNullOrEmpty(eventIdOverride) ? EventIdProvider.GenerateEventId() : eventIdOverride;
            var postData = new WebhookRequestData
            {
                type = type,
                event_id = eventId,
                dedupe_key = dedupeKey,
                created_at = currentDateText,
                creativeToken = TokenHandler.GetValidToken(),
                device_name = deviceInfo.DeviceName,
                device_model = SystemInfo.deviceModel,
                os_system = deviceInfo.OsVersion,
                utc_offset = utcOffset,
                retention_day = retentionDay,
                sdk_version = SDKVersion.VERSION,
                sdk_type = SDKVersion.SDK_TYPE,
                app_version = SDKVersion.AppVersion,
                unity_version = SDKVersion.UnityVersion,
                dev = Application.isEditor || Debug.isDebugBuild,
                idfv = identityInfo.idfv,
                gaid = identityInfo.gaid,
                app_set_id = identityInfo.app_set_id,
                android_id = identityInfo.android_id,
                limit_ad_tracking = identityInfo.limit_ad_tracking,
                whitelisted_properties = whitelisted,
                blacklisted_properties = blacklisted,
                payload = data
            };

            LastWebhookEnvelope = new RequestEnvelopeSnapshot
            {
                creativeToken = postData.creativeToken,
                idfv = postData.idfv,
                gaid = postData.gaid,
                app_set_id = postData.app_set_id,
                android_id = postData.android_id,
                limit_ad_tracking = postData.limit_ad_tracking,
                retention_day = postData.retention_day,
                event_type = type,
                event_name = eventName,
                event_id = eventId
            };
            
            var json = JsonConvert.SerializeObject(postData);
            Debug.Log(json);
            var meta = new RequestMeta("webhook", type, eventId, eventName);
            SendRequest(ApiEndpointsModel.WEBHOOK, json, onSuccess, onError, UnityWebRequest.kHttpVerbPOST, null, meta);
        }

        private static bool ShouldWaitForIdentity()
        {
            if (IdentityHandler.IsSettled)
            {
                return false;
            }

            var settings = AudienceLabSettings.Instance;
            var autoMode = settings != null
                ? settings.enableGaidAutoCollection
                : (SDKSettingsModel.Instance != null && SDKSettingsModel.Instance.EnableGaidCollection);
            var appSetAuto = settings == null || settings.enableAppSetIdAutoCollection;

            // Only wait for identity if auto-collecting GAID or App Set ID
            var wantsAndroidIdentity = autoMode || appSetAuto;
            var wantsIosIdentity = Application.platform == RuntimePlatform.IPhonePlayer;
            var wantsIdentity = wantsAndroidIdentity || wantsIosIdentity;

            if (!wantsIdentity)
            {
                return false;
            }

            var identityInfo = IdentityHandler.Current;
            var hasAnyIdentity = !string.IsNullOrEmpty(identityInfo.idfv) ||
                                 !string.IsNullOrEmpty(identityInfo.gaid) ||
                                 !string.IsNullOrEmpty(identityInfo.app_set_id) ||
                                 !string.IsNullOrEmpty(identityInfo.android_id) ||
                                 identityInfo.limit_ad_tracking.HasValue;
            return !hasAnyIdentity;
        }

        private void HandleTokenAvailable(string token)
        {
            FlushQueuedWebhookRequests();
        }

        private static void EnqueueWebhookRequest(string type, object data, string dedupeKey, string eventName, bool waitForIdentity, string eventIdOverride = null)
        {
            EnsureQueueLoaded();

            int? retentionDay = null;
            if (PlayerPrefs.HasKey("retentionDay"))
            {
                retentionDay = PlayerPrefs.GetInt("retentionDay");
            }

            var payloadJson = JsonConvert.SerializeObject(data);
            var entry = new QueuedWebhookRequest
            {
                eventId = string.IsNullOrEmpty(eventIdOverride) ? EventIdProvider.GenerateEventId() : eventIdOverride,
                type = type,
                dedupeKey = dedupeKey,
                eventName = eventName,
                payloadJson = payloadJson,
                createdAtIso = DateTime.UtcNow.ToString("o"),
                waitForIdentity = waitForIdentity,
                sequence = ++queueSequence,
                retentionDay = retentionDay
            };

            queuedWebhookRequests.Add(entry);
            TrimQueue();
            PersistQueue();

            if (SDKSettingsModel.Instance != null && SDKSettingsModel.Instance.ShowDebugLog)
            {
                Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Queued {type} event (eventId={entry.eventId}, retentionDay={retentionDay?.ToString() ?? "null"})");
            }
        }

        private static void FlushQueuedWebhookRequests()
        {
            if (!TokenHandler.HasValidToken())
            {
                return;
            }

            if (isFlushRunning)
            {
                return;
            }

            isFlushRunning = true;
            try
            {
                EnsureQueueLoaded();
                TrimQueue();

                queuedWebhookRequests.Sort(CompareQueuedEntries);
                var snapshot = new List<QueuedWebhookRequest>(queuedWebhookRequests);

                foreach (var entry in snapshot)
                {
                    var payload = string.IsNullOrEmpty(entry.payloadJson)
                        ? null
                        : JsonConvert.DeserializeObject<object>(entry.payloadJson);

                    var createdAt = DateTime.UtcNow;
                    if (!string.IsNullOrEmpty(entry.createdAtIso) &&
                        DateTime.TryParse(entry.createdAtIso, out var parsed))
                    {
                        createdAt = parsed.ToUniversalTime();
                    }

                    // Use stored retention_day, but fallback to current PlayerPrefs value if stored is null
                    var retentionDayForEntry = entry.retentionDay;
                    if (!retentionDayForEntry.HasValue && PlayerPrefs.HasKey("retentionDay"))
                    {
                        retentionDayForEntry = PlayerPrefs.GetInt("retentionDay");
                    }

                    Instance.SendWebhookRequestInternal(entry.type, payload, entry.dedupeKey, entry.waitForIdentity,
                        entry.eventName, createdAt, entry.eventId,
                        _ => RemoveQueuedEntry(entry.eventId),
                        _ => { },
                        retentionDayForEntry);
                }
            }
            finally
            {
                isFlushRunning = false;
            }
        }

        private static void EnsureQueueLoaded()
        {
            if (queuedWebhookRequests != null)
            {
                return;
            }

            var path = GetQueueFilePath();
            if (!File.Exists(path))
            {
                queuedWebhookRequests = new List<QueuedWebhookRequest>();
                return;
            }

            try
            {
                var json = File.ReadAllText(path);
                queuedWebhookRequests = JsonConvert.DeserializeObject<List<QueuedWebhookRequest>>(json) ??
                                        new List<QueuedWebhookRequest>();
            }
            catch (Exception)
            {
                queuedWebhookRequests = new List<QueuedWebhookRequest>();
            }
        }

        private static void PersistQueue()
        {
            try
            {
                var path = GetQueueFilePath();
                var json = JsonConvert.SerializeObject(queuedWebhookRequests);
                File.WriteAllText(path, json);
            }
            catch (Exception)
            {
                // Ignore persistence failures.
            }
        }

        private static void TrimQueue()
        {
            if (queuedWebhookRequests == null)
            {
                return;
            }

            var cutoff = DateTime.UtcNow.AddHours(-MaxQueuedAgeHours);
            var removedByAge = queuedWebhookRequests.RemoveAll(entry =>
            {
                if (string.IsNullOrEmpty(entry.createdAtIso))
                {
                    return false;
                }

                return DateTime.TryParse(entry.createdAtIso, out var createdAt) && createdAt < cutoff;
            });

            if (removedByAge > 0 && !hasLoggedQueueDrop)
            {
                Debug.LogWarning($"{SDKSettingsModel.GetColorPrefixLog()} Dropped queued events older than {MaxQueuedAgeHours}h.");
                hasLoggedQueueDrop = true;
            }

            var droppedBySize = 0;
            while (queuedWebhookRequests.Count > MaxQueuedEvents)
            {
                queuedWebhookRequests.RemoveAt(0);
                droppedBySize++;
            }

            if (droppedBySize > 0 && !hasLoggedQueueDrop)
            {
                Debug.LogWarning($"{SDKSettingsModel.GetColorPrefixLog()} Dropped queued events (queue cap {MaxQueuedEvents}).");
                hasLoggedQueueDrop = true;
            }
        }

        private static string GetQueueFilePath()
        {
            return Path.Combine(Application.persistentDataPath, "audiencelab_webhook_queue.json");
        }

        private static void RemoveQueuedEntry(string eventId)
        {
            if (string.IsNullOrEmpty(eventId))
            {
                return;
            }

            EnsureQueueLoaded();
            var removed = queuedWebhookRequests.RemoveAll(entry => entry.eventId == eventId);
            if (removed > 0)
            {
                PersistQueue();
            }
        }

        private static int CompareQueuedEntries(QueuedWebhookRequest a, QueuedWebhookRequest b)
        {
            if (a == null && b == null)
            {
                return 0;
            }
            if (a == null)
            {
                return -1;
            }
            if (b == null)
            {
                return 1;
            }

            if (DateTime.TryParse(a.createdAtIso, out var aTime) && DateTime.TryParse(b.createdAtIso, out var bTime))
            {
                var compare = aTime.CompareTo(bTime);
                if (compare != 0)
                {
                    return compare;
                }
            }

            return a.sequence.CompareTo(b.sequence);
        }

        private IEnumerator WaitForIdentityOrTimeout(Action onSettled)
        {
            var startTime = Time.realtimeSinceStartup;
            while (!IdentityHandler.IsSettled)
            {
                if (Time.realtimeSinceStartup - startTime >= IdentityWaitTimeoutSeconds)
                {
                    break;
                }

                yield return null;
            }

            onSettled?.Invoke();
        }

        private sealed class QueuedWebhookRequest
        {
            public string eventId;
            public string type;
            public string dedupeKey;
            public string eventName;
            public string payloadJson;
            public string createdAtIso;
            public bool waitForIdentity;
            public long sequence;
            public int? retentionDay;
        }

        internal sealed class RequestEnvelopeSnapshot
        {
            public string creativeToken;
            public string idfv;
            public string gaid;
            public string app_set_id;
            public string android_id;
            public bool? limit_ad_tracking;
            public int? retention_day;
            public string event_type;
            public string event_name;
            public string event_id;
        }
        
        
        private void SendRequest(string endpoint, string json, Action<string> onSuccess, Action<string> onError = null,
            string method = UnityWebRequest.kHttpVerbPOST, Dictionary<string, string> headerData = null, RequestMeta meta = null)
        {
            if (meta != null)
            {
                meta.requestBody = json;
                meta.endpoint = endpoint;
            }

            if (IsInternetAvailable())
            {
                StartCoroutine(SendRequestCoroutine(endpoint, json, onSuccess, onError, method, headerData, meta));
            }
            else
            {
                var message =
                    $"{SDKSettingsModel.GetColorPrefixLog()} There is no Internet connection. Please check your connection and try again.";
                Debug.LogWarning(message);
                EmitRequestResult(meta, false, 0, "offline", null);
                onError?.Invoke(message);
            }
        }


        private static IEnumerator SendRequestCoroutine(string endpoint, string json, Action<string> onSuccess,
            Action<string> onError, string method, Dictionary<string, string> headerData = null, RequestMeta meta = null)
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
                    EmitRequestResult(meta, false, www.responseCode, www.error, www.downloadHandler.text);
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
                        EmitRequestResult(meta, true, www.responseCode, null, www.downloadHandler.text);
                    }
                    catch (WebException webEx)
                    {
                        EmitRequestResult(meta, false, www.responseCode, webEx.Message, www.downloadHandler.text);
                        DebugLogError($"Exception encountered: {webEx.Message}", onError);
                    }
                    catch (IOException ioEx)
                    {
                        EmitRequestResult(meta, false, www.responseCode, ioEx.Message, www.downloadHandler.text);
                        DebugLogError($"IOException encountered: {ioEx.Message}", onError);
                    }
                    catch (Exception ex)
                    {
                        EmitRequestResult(meta, false, www.responseCode, ex.Message, www.downloadHandler.text);
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

        private static void EmitRequestResult(RequestMeta meta, bool success, long responseCode, string errorMessage, string responseBody)
        {
            if (meta == null || OnRequestResult == null)
                return;

            int? httpStatus = responseCode > 0 ? (int)responseCode : (int?)null;
            var result = new RequestResult
            {
                requestKind = meta.requestKind,
                eventType = meta.eventType,
                eventId = meta.eventId,
                eventName = meta.eventName,
                endpoint = meta.endpoint,
                requestBody = meta.requestBody,
                responseBody = responseBody,
                httpStatus = httpStatus,
                success = success,
                errorMessage = string.IsNullOrEmpty(errorMessage) ? null : errorMessage,
                timestampUtcIso = DateTime.UtcNow.ToString("o")
            };

            OnRequestResult.Invoke(result);
        }

        private sealed class RequestMeta
        {
            public string requestKind;
            public string eventType;
            public string eventId;
            public string eventName;
            public string requestBody;
            public string endpoint;

            public RequestMeta(string requestKind, string eventType, string eventId, string eventName)
            {
                this.requestKind = requestKind;
                this.eventType = eventType;
                this.eventId = eventId;
                this.eventName = eventName;
            }
        }
    }
}