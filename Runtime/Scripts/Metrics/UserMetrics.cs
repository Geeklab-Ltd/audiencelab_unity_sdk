using System;
using UnityEngine;
using System.Threading.Tasks;


namespace Geeklab.AudiencelabSDK
{
    public class UserMetrics : MonoBehaviour
    {
        private const string RetentionGuardDayKey = "GeeklabSDK_RetentionGuardDay";
        private const string RetentionGuardEventIdKey = "GeeklabSDK_RetentionGuardEventId";
        private const string RetentionGuardStateKey = "GeeklabSDK_RetentionGuardState";
        private const string RetentionStateQueued = "queued";
        private const string RetentionStateAcknowledged = "acknowledged";
        private const string RetentionStateFailedRetryable = "failed_retryable";

        private static UserMetrics instance;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }

            instance = this;
        }

        private void Start()
        {
            if (instance != this)
                return;

            TrackRetentionOnStartup();
        }

        internal static void TrackRetentionOnStartup()
        {
            PrepareRetentionOnStartup();
            UpdateRetention();
        }

        internal static void PrepareRetentionOnStartup()
        {
            if (string.IsNullOrEmpty(PlayerPrefs.GetString("firstLogin")))
            {
                InitializeFirstLogin();
            }
            EnsureRetentionDayCalculated();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
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
            PlayerPrefs.Save();
        }



        public static void UpdateRetention()
        {
            var today = DateTime.Now.ToString("dd/MM/yyyy");
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

            var existingEventId = PlayerPrefs.GetString(RetentionGuardEventIdKey, "");
            var existingState = PlayerPrefs.GetString(RetentionGuardStateKey, "");
            var hasExistingRetentionDay = PlayerPrefs.HasKey(RetentionGuardDayKey);
            var existingRetentionDay = PlayerPrefs.GetInt(RetentionGuardDayKey, -1);
            var hasSameRetentionGuard = hasExistingRetentionDay && existingRetentionDay == daysBetween;

            if (!hasSameRetentionGuard &&
                WebRequestManager.TryGetQueuedRetentionEventId(daysBetween, out var queuedRetentionEventId))
            {
                existingEventId = queuedRetentionEventId;
                existingState = RetentionStateQueued;
                hasExistingRetentionDay = true;
                existingRetentionDay = daysBetween;
                hasSameRetentionGuard = true;
                PlayerPrefs.SetInt(RetentionGuardDayKey, daysBetween);
                PlayerPrefs.SetString(RetentionGuardEventIdKey, existingEventId);
                PlayerPrefs.SetString(RetentionGuardStateKey, RetentionStateQueued);
                PlayerPrefs.SetString("lastSentMetricDate", today);
            }

            var hasExistingLogicalEvent = hasSameRetentionGuard && !string.IsNullOrEmpty(existingEventId);
            var isAcknowledgedExistingEvent = hasSameRetentionGuard &&
                                              existingState == RetentionStateAcknowledged;
            var isQueuedExistingEvent = hasExistingLogicalEvent &&
                                        existingState == RetentionStateQueued;
            var isRetryableExistingEvent = hasExistingLogicalEvent &&
                                           (existingState == RetentionStateFailedRetryable ||
                                            (isQueuedExistingEvent &&
                                             !WebRequestManager.HasQueuedWebhookEvent(existingEventId) &&
                                             !WebRequestManager.IsWebhookInFlight(existingEventId)));

            if (isAcknowledgedExistingEvent ||
                (isQueuedExistingEvent && !isRetryableExistingEvent))
            {
                PlayerPrefs.Save();
                return;
            }

            if (!isRetryableExistingEvent && PlayerPrefs.GetString("lastSentMetricDate") == today)
            {
                PlayerPrefs.Save();
                return;
            }

            var backfillDay = PlayerPrefs.GetInt("backfillDay").ToString();
            var retentionDay = PlayerPrefs.GetInt("retentionDay").ToString();
            var eventId = isRetryableExistingEvent ? existingEventId : EventIdProvider.GenerateEventId();

            var data = WebhookPayloadFactory.CreateRetention(retentionDay, backfillDay);

            PlayerPrefs.SetInt(RetentionGuardDayKey, daysBetween);
            PlayerPrefs.SetString(RetentionGuardEventIdKey, eventId);
            PlayerPrefs.SetString(RetentionGuardStateKey, RetentionStateQueued);
            PlayerPrefs.SetString("lastSentMetricDate", today);
            PlayerPrefs.Save();

            _ = SendMetrics(data, eventId);
        }
        
        
        public static async Task<bool> SendMetrics(object postData = null, string eventId = null, string dedupeKey = null)
        {
            if (!SDKSettingsModel.Instance.SendStatistics) 
                return false;

            var taskCompletionSource = new TaskCompletionSource<bool>();
            Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Send metrics");
            Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} {postData}");
            WebRequestManager.Instance.SendUserMetricsRequestWithContext(postData, eventId, dedupeKey,
                (response) =>
                {
                    if (SDKSettingsModel.Instance.ShowDebugLog)
                        Debug.Log(
                            $"{SDKSettingsModel.GetColorPrefixLog()} {response}");
                    MarkRetentionState(eventId, RetentionStateAcknowledged);
                    taskCompletionSource.SetResult(true);
                },
                (error) =>
                {
                    Debug.LogError(error);
                    MarkRetentionState(eventId, RetentionStateFailedRetryable);
                    taskCompletionSource.SetResult(false);
                }
            );
            
            return await taskCompletionSource.Task;
        }

        private static void MarkRetentionState(string eventId, string state)
        {
            if (string.IsNullOrEmpty(eventId))
                return;

            if (PlayerPrefs.GetString(RetentionGuardEventIdKey, "") != eventId)
                return;

            PlayerPrefs.SetString(RetentionGuardStateKey, state);
            PlayerPrefs.Save();
        }

        internal static void MarkRetentionDeliveryAcknowledged(string eventId)
        {
            MarkRetentionState(eventId, RetentionStateAcknowledged);
        }
    }
}
