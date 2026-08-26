using UnityEngine.Networking;
using System.Collections;
using System.Text;
using UnityEngine;
using System.Net;
using System.IO;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace Geeklab.AudiencelabSDK
{
    public class WebRequestManager : MonoBehaviour
    {
        private static bool isDebugOn = true;
        private const int MaxQueuedEvents = 200;
        private const double MaxQueuedAgeHours = 24d;
        private const float IdentityWaitTimeoutSeconds = 2f;
        private const float QueueRetryDelaySeconds = 15f;

        private static WebRequestManager instance;
        private static List<QueuedWebhookRequest> queuedWebhookRequests;
        private static readonly HashSet<string> inFlightWebhookEventIds = new HashSet<string>();
        private static bool isFlushRunning;
        private static bool isQueueRetryScheduled;
        private static bool hasLoggedQueueDrop;
        private static long queueSequence;

        internal static RequestEnvelopeSnapshot LastWebhookEnvelope;

        internal static event Action<RequestResult> OnRequestResult;

        public static WebRequestManager Instance
        {
            get { return EnsureCreated(); }
        }

        internal static WebRequestManager EnsureOn(GameObject owner)
        {
            return EnsureCreated(owner);
        }

        private static WebRequestManager EnsureCreated(GameObject owner = null)
        {
            if (instance != null)
            {
                return instance;
            }

            if (owner != null)
            {
                var ownerManager = owner.GetComponent<WebRequestManager>();
                return ownerManager != null ? ownerManager : owner.AddComponent<WebRequestManager>();
            }

            var go = new GameObject(nameof(WebRequestManager));
            return go.AddComponent<WebRequestManager>();
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
                SessionManager.OnSessionContextAvailable += HandleSessionContextAvailable;
                wasOffline = !IsInternetAvailable();
            }
            else
            {
                Destroy(this);
            }
        }

        private void Update()
        {
            if (instance != this)
                return;

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
                SessionManager.OnSessionContextAvailable -= HandleSessionContextAvailable;
                instance = null;
            }
        }

        
        public void CheckDataCollectionStatusRequest(Action<string> onSuccess = null, Action<string> onError = null)
        {
            SendRequest(ApiEndpointsModel.CHECK_DATA_COLLECTION_STATUS, "", onSuccess, onError,
                UnityWebRequest.kHttpVerbGET);
        }
        

        public void SendUserMetricsRequest(object data, Action<string> onSuccess = null, Action<string> onError = null)
        {
            SendUserMetricsRequestWithContext(data, null, null, onSuccess, onError);
        }

        internal void SendUserMetricsRequestWithContext(object data, string eventId = null, string dedupeKey = null,
            Action<string> onSuccess = null, Action<string> onError = null)
        {
            SendWebhookRequest("retention", data, dedupeKey, false, onSuccess, onError, null, eventId);
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
                Debug.Log(
                    $"{SDKSettingsModel.GetColorPrefixLog()} Sending fetch-token with identity: " +
                    $"gaid={!string.IsNullOrEmpty(identityInfo.gaid)}, " +
                    $"app_set_id={!string.IsNullOrEmpty(identityInfo.app_set_id)}, " +
                    $"android_id={!string.IsNullOrEmpty(identityInfo.android_id)}, " +
                    $"idfv={!string.IsNullOrEmpty(identityInfo.idfv)}");
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

        private static string GetUtcOffset()
        {
            // Get current UTC offset
            TimeSpan offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);

            // Format as "+HH:mm" or "-HH:mm"
            string formattedOffset = offset >= TimeSpan.Zero
                ? $"+{offset.Hours:D2}:{offset.Minutes:D2}"
                : $"-{Math.Abs(offset.Hours):D2}:{Math.Abs(offset.Minutes):D2}";

            return formattedOffset;
        }

        
        public void SendSessionEventRequest(object data, bool waitForIdentity, Action<string> onSuccess = null,
            Action<string> onError = null, int? retentionDayOverride = null)
        {
            SendWebhookRequest("session", data, null, waitForIdentity, onSuccess, onError,
                retentionDayOverride: retentionDayOverride);
        }

        public void SendCustomEventRequest(object data, string dedupeKey = null, string eventName = null, Action<string> onSuccess = null, Action<string> onError = null)
        {
            SendWebhookRequest("custom", data, dedupeKey, false, onSuccess, onError, eventName);
        }

        private void SendWebhookRequest(string type, object data, string dedupeKey, bool waitForIdentity,
            Action<string> onSuccess = null, Action<string> onError = null, string eventName = null,
            string eventIdOverride = null, int? retentionDayOverride = null)
        {
            var request = CreateWebhookRequest(type, data, dedupeKey, waitForIdentity, eventName, eventIdOverride,
                retentionDayOverride: retentionDayOverride);

            if (ShouldPersistBeforeSend(request))
            {
                EnqueueWebhookRequest(request);
                if (!TokenHandler.HasValidToken())
                {
                    TokenHandler.StartRetryLoop();
                    return;
                }

                SendWebhookRequestInternal(request,
                    response =>
                    {
                        RemoveQueuedEntry(request.eventId);
                        onSuccess?.Invoke(response);
                    },
                    onError);
                return;
            }

            if (!TokenHandler.HasValidToken())
            {
                EnqueueWebhookRequest(request);
                TokenHandler.StartRetryLoop();
                return;
            }

            SendWebhookRequestInternal(request, onSuccess, onError);
        }

        private static WebhookRequestContext CreateWebhookRequest(string type, object data, string dedupeKey,
            bool waitForIdentity, string eventName, string eventIdOverride = null, DateTime? createdAtOverride = null,
            int? retentionDayOverride = null, string sessionIdOverride = null, int? sessionIndexOverride = null,
            string utcOffsetOverride = null)
        {
            var sessionId = sessionIdOverride;
            var sessionIndex = sessionIndexOverride;
            if (string.IsNullOrEmpty(sessionId))
            {
                ResolveSessionContext(type, data, out sessionId, out sessionIndex);
            }

            return new WebhookRequestContext
            {
                type = type,
                data = data,
                dedupeKey = dedupeKey,
                eventName = eventName,
                waitForIdentity = waitForIdentity,
                eventId = string.IsNullOrEmpty(eventIdOverride) ? EventIdProvider.GenerateEventId() : eventIdOverride,
                createdAt = createdAtOverride ?? DateTime.UtcNow,
                retentionDay = retentionDayOverride.HasValue ? retentionDayOverride : GetCurrentRetentionDay(),
                sessionId = sessionId,
                sessionIndex = sessionIndex,
                // Snapshot the UTC offset at creation time so events queued offline keep their
                // origin-time offset instead of adopting the device offset at flush time.
                utcOffset = string.IsNullOrEmpty(utcOffsetOverride) ? GetUtcOffset() : utcOffsetOverride
            };
        }

        private void SendWebhookRequestInternal(WebhookRequestContext request, Action<string> onSuccess, Action<string> onError)
        {
            if (ShouldWaitForSessionContext(request))
            {
                EnqueueWebhookRequest(request);
                if (SDKSettingsModel.Instance != null && SDKSettingsModel.Instance.ShowDebugLog)
                {
                    Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Queued {request.type} event until session context is available");
                }
                return;
            }

            if (!TokenHandler.HasValidToken())
            {
                EnqueueWebhookRequest(request);
                TokenHandler.StartRetryLoop();
                return;
            }

            if ((request.waitForIdentity || ShouldWaitForIdentity()) && !IdentityHandler.IsSettled)
            {
                StartCoroutine(WaitForIdentityOrTimeout(() =>
                {
                    request.waitForIdentity = false;
                    SendWebhookRequestInternal(request, onSuccess, onError);
                }));
                return;
            }

            // Check for internet - if offline, queue the request instead of losing it
            if (!IsInternetAvailable())
            {
                EnqueueWebhookRequest(request);
                if (SDKSettingsModel.Instance != null && SDKSettingsModel.Instance.ShowDebugLog)
                {
                    Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} No internet - queued {request.type} event for later");
                }
                onError?.Invoke("Queued for retry - no internet");
                return;
            }

            var currentDateText = request.createdAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss");
            var deviceInfo = DeviceInfoHandler.GetDeviceInfo();
            var utcOffset = string.IsNullOrEmpty(request.utcOffset) ? GetUtcOffset() : request.utcOffset;
            var identityInfo = IdentityHandler.Current;
            var whitelisted = UserPropertiesManager.GetWhitelistedProperties();
            var blacklisted = UserPropertiesManager.GetBlacklistedProperties();

            var postData = new WebhookRequestData
            {
                type = request.type,
                event_id = request.eventId,
                dedupe_key = request.dedupeKey,
                created_at = currentDateText,
                creativeToken = TokenHandler.GetValidToken(),
                device_name = deviceInfo.DeviceName,
                device_model = SystemInfo.deviceModel,
                os_system = deviceInfo.OsVersion,
                utc_offset = utcOffset,
                retention_day = request.retentionDay,
                sid = request.sessionId,
                si = request.sessionIndex,
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
                payload = request.data
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
                sid = postData.sid,
                si = postData.si,
                event_type = request.type,
                event_name = request.eventName,
                event_id = request.eventId
            };
            
            var json = JsonConvert.SerializeObject(postData);
            Debug.Log(json);
            var meta = new RequestMeta("webhook", request.type, request.eventId, request.eventName);

            if (!TryMarkWebhookInFlight(request.eventId))
            {
                var duplicateMessage = $"{SDKSettingsModel.GetColorPrefixLog()} Event already in flight (eventId={request.eventId})";
                if (SDKSettingsModel.Instance != null && SDKSettingsModel.Instance.ShowDebugLog)
                {
                    Debug.Log(duplicateMessage);
                }
                onError?.Invoke(duplicateMessage);
                return;
            }

            SendRequest(
                ApiEndpointsModel.WEBHOOK,
                json,
                response =>
                {
                    ClearWebhookInFlight(request.eventId);
                    if (IsRetentionEvent(request))
                    {
                        UserMetrics.MarkRetentionDeliveryAcknowledged(request.eventId);
                    }
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    ClearWebhookInFlight(request.eventId);
                    onError?.Invoke(error);
                    if (ShouldPersistBeforeSend(request))
                    {
                        // Webhook delivery is intentionally at least once: a transport failure
                        // can occur after the server accepted the request. Keep the same event ID
                        // on every retry so the receiver can handle it idempotently.
                        EnqueueWebhookRequest(request);
                        ScheduleQueuedWebhookRetry();
                    }
                },
                UnityWebRequest.kHttpVerbPOST,
                null,
                meta);
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

        private void HandleSessionContextAvailable()
        {
            StampQueuedEventsWithCurrentSessionContext();
            FlushQueuedWebhookRequests();
        }

        private static void EnqueueWebhookRequest(WebhookRequestContext request)
        {
            EnsureQueueLoaded();

            var existingEntry = queuedWebhookRequests.Find(entry => entry.eventId == request.eventId);
            if (existingEntry != null)
            {
                if (MergeQueuedEntry(existingEntry, request))
                {
                    PersistQueue();
                }
                return;
            }

            var payloadJson = JsonConvert.SerializeObject(request.data);
            var entry = new QueuedWebhookRequest
            {
                eventId = request.eventId,
                type = request.type,
                dedupeKey = request.dedupeKey,
                eventName = request.eventName,
                payloadJson = payloadJson,
                createdAtIso = request.createdAt.ToUniversalTime().ToString("o"),
                waitForIdentity = request.waitForIdentity,
                sequence = ++queueSequence,
                retentionDay = request.retentionDay,
                sessionId = request.sessionId,
                sessionIndex = request.sessionIndex,
                utcOffset = request.utcOffset
            };

            queuedWebhookRequests.Add(entry);
            TrimQueue();
            PersistQueue();

            if (SDKSettingsModel.Instance != null && SDKSettingsModel.Instance.ShowDebugLog)
            {
                Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Queued {request.type} event (eventId={entry.eventId}, retentionDay={request.retentionDay?.ToString() ?? "null"})");
            }
        }

        internal static void FlushQueuedWebhookRequestsIfAllowed()
        {
            if (!Application.isPlaying || !CanSendMetrics())
            {
                return;
            }

            FlushQueuedWebhookRequests();
        }

        private static bool CanSendMetrics()
        {
            var settings = SDKSettingsModel.Instance;
            return settings != null &&
                   settings.IsSDKEnabled &&
                   settings.SendStatistics;
        }

        private static void FlushQueuedWebhookRequests()
        {
            if (!CanSendMetrics())
            {
                return;
            }

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

                    var request = CreateWebhookRequest(
                        entry.type,
                        payload,
                        entry.dedupeKey,
                        entry.waitForIdentity,
                        entry.eventName,
                        entry.eventId,
                        createdAt,
                        entry.retentionDay,
                        entry.sessionId,
                        entry.sessionIndex,
                        entry.utcOffset);

                    if (MergeQueuedEntry(entry, request))
                    {
                        PersistQueue();
                    }

                    Instance.SendWebhookRequestInternal(request,
                        _ => RemoveQueuedEntry(entry.eventId),
                        _ => { });
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
                if (IsCriticalQueuedEvent(entry))
                {
                    return false;
                }

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
                var dropIndex = queuedWebhookRequests.FindIndex(entry => !IsCriticalQueuedEvent(entry));
                queuedWebhookRequests.RemoveAt(dropIndex >= 0 ? dropIndex : 0);
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

        internal static int? GetCurrentRetentionDay()
        {
            // Derive the retention day from the persisted first-login date at request-creation
            // time so every event carries the correct day, even on warm resumes where the
            // cold-start retention pipeline has not re-run yet.
            var today = DateTime.Now.Date;
            int? storedRetentionDay = null;
            if (PlayerPrefs.HasKey("retentionDay"))
            {
                var storedValue = PlayerPrefs.GetInt("retentionDay");
                if (RetentionDateStorage.IsPlausibleElapsedDays(storedValue, today))
                {
                    storedRetentionDay = storedValue;
                }
            }

            var firstLogin = PlayerPrefs.GetString("firstLogin", "");
            if (!string.IsNullOrEmpty(firstLogin) &&
                RetentionDateStorage.TryParse(firstLogin, out var firstLoginDate))
            {
                if (RetentionDateStorage.TryCalculateElapsedDays(
                        firstLoginDate,
                        today,
                        out var retentionDay))
                {
                    return RetentionDateStorage.PreserveMonotonicElapsedDays(
                        retentionDay,
                        storedRetentionDay,
                        today);
                }
            }

            return storedRetentionDay;
        }

        private static bool IsRetentionEvent(QueuedWebhookRequest entry)
        {
            return entry != null &&
                   string.Equals(entry.type, "retention", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRetentionEvent(WebhookRequestContext request)
        {
            return request != null &&
                   string.Equals(request.type, "retention", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSessionEvent(WebhookRequestContext request)
        {
            return request != null &&
                   string.Equals(request.type, "session", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSessionEvent(QueuedWebhookRequest entry)
        {
            return entry != null &&
                   string.Equals(entry.type, "session", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCriticalQueuedEvent(QueuedWebhookRequest entry)
        {
            return IsRetentionEvent(entry) || IsSessionEvent(entry);
        }

        private static bool ShouldPersistBeforeSend(WebhookRequestContext request)
        {
            return IsSessionEvent(request) || IsRetentionEvent(request);
        }

        private static bool ShouldWaitForSessionContext(WebhookRequestContext request)
        {
            return request != null &&
                   !IsSessionEvent(request) &&
                   string.IsNullOrEmpty(request.sessionId);
        }

        private static void ResolveSessionContext(string type, object data, out string sessionId, out int? sessionIndex)
        {
            sessionId = null;
            sessionIndex = null;

            if (TryExtractSessionContextFromPayload(data, out sessionId, out sessionIndex))
            {
                return;
            }

            if (SessionManager.TryGetCurrentSession(out var currentSessionId, out var currentSessionIndex))
            {
                sessionId = currentSessionId;
                sessionIndex = currentSessionIndex;
            }
        }

        private static bool TryExtractSessionContextFromPayload(object data, out string sessionId, out int? sessionIndex)
        {
            sessionId = null;
            sessionIndex = null;

            if (data is System.Collections.IDictionary dictionary)
            {
                if (!dictionary.Contains("sid"))
                {
                    return false;
                }

                sessionId = dictionary["sid"]?.ToString();
                sessionIndex = TryConvertToInt(dictionary.Contains("si") ? dictionary["si"] : null);
                return !string.IsNullOrEmpty(sessionId);
            }

            if (data is JObject jObject)
            {
                sessionId = jObject.Value<string>("sid");
                sessionIndex = TryConvertToInt(jObject["si"]);
                return !string.IsNullOrEmpty(sessionId);
            }

            return false;
        }

        private static int? TryConvertToInt(object value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is int intValue)
            {
                return intValue;
            }

            if (value is long longValue)
            {
                return longValue > int.MaxValue || longValue < int.MinValue ? (int?)null : (int)longValue;
            }

            if (value is JValue jValue)
            {
                return TryConvertToInt(jValue.Value);
            }

            return int.TryParse(value.ToString(), out var parsed) ? parsed : (int?)null;
        }

        private static bool MergeQueuedEntry(QueuedWebhookRequest entry, WebhookRequestContext request)
        {
            if (entry == null || request == null)
            {
                return false;
            }

            var changed = false;
            if (string.IsNullOrEmpty(entry.sessionId) && !string.IsNullOrEmpty(request.sessionId))
            {
                entry.sessionId = request.sessionId;
                changed = true;
            }

            if (!entry.sessionIndex.HasValue && request.sessionIndex.HasValue)
            {
                entry.sessionIndex = request.sessionIndex;
                changed = true;
            }

            return changed;
        }

        private static void StampQueuedEventsWithCurrentSessionContext()
        {
            if (!SessionManager.TryGetCurrentSession(out var sessionId, out var sessionIndex))
            {
                return;
            }

            EnsureQueueLoaded();
            var changed = false;
            foreach (var entry in queuedWebhookRequests)
            {
                if (entry == null || IsSessionEvent(entry) || !string.IsNullOrEmpty(entry.sessionId))
                {
                    continue;
                }

                entry.sessionId = sessionId;
                entry.sessionIndex = sessionIndex;
                changed = true;
            }

            if (changed)
            {
                PersistQueue();
            }
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

        internal static bool HasQueuedWebhookEvent(string eventId)
        {
            if (string.IsNullOrEmpty(eventId))
            {
                return false;
            }

            EnsureQueueLoaded();
            return queuedWebhookRequests.Exists(entry => entry.eventId == eventId);
        }

        internal static bool TryGetQueuedRetentionEventId(int retentionDay, out string eventId)
        {
            eventId = null;
            EnsureQueueLoaded();

            var entry = queuedWebhookRequests.Find(queuedEntry =>
                IsRetentionEvent(queuedEntry) &&
                queuedEntry.retentionDay.HasValue &&
                queuedEntry.retentionDay.Value == retentionDay &&
                !string.IsNullOrEmpty(queuedEntry.eventId));

            if (entry == null)
            {
                return false;
            }

            eventId = entry.eventId;
            return true;
        }

        internal static bool IsWebhookInFlight(string eventId)
        {
            return !string.IsNullOrEmpty(eventId) && inFlightWebhookEventIds.Contains(eventId);
        }

        private static bool TryMarkWebhookInFlight(string eventId)
        {
            if (string.IsNullOrEmpty(eventId))
            {
                return true;
            }

            return inFlightWebhookEventIds.Add(eventId);
        }

        private static void ClearWebhookInFlight(string eventId)
        {
            if (string.IsNullOrEmpty(eventId))
            {
                return;
            }

            inFlightWebhookEventIds.Remove(eventId);
        }

        private static void ScheduleQueuedWebhookRetry()
        {
            if (isQueueRetryScheduled || !Application.isPlaying || instance == null)
            {
                return;
            }

            instance.StartCoroutine(FlushQueuedWebhookRequestsAfterDelay());
        }

        private static IEnumerator FlushQueuedWebhookRequestsAfterDelay()
        {
            isQueueRetryScheduled = true;
            yield return new WaitForSeconds(QueueRetryDelaySeconds);
            isQueueRetryScheduled = false;
            FlushQueuedWebhookRequests();
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
            public string sessionId;
            public int? sessionIndex;
            public string utcOffset;
        }

        private sealed class WebhookRequestContext
        {
            public string eventId;
            public string type;
            public string dedupeKey;
            public string eventName;
            public object data;
            public DateTime createdAt;
            public bool waitForIdentity;
            public int? retentionDay;
            public string sessionId;
            public int? sessionIndex;
            public string utcOffset;
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
            public string sid;
            public int? si;
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
