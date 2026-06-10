using System;
using UnityEngine;

namespace Geeklab.AudiencelabSDK
{
    public class SessionManager : MonoBehaviour
    {
        private const int SessionTimeoutSeconds = 1800;
        private const string SessionIdKey = "GeeklabSDK_SessionId";
        private const string SessionIndexKey = "GeeklabSDK_SessionIndex";
        private const string LastActiveUtcKey = "GeeklabSDK_LastActiveUtc";
        private const string SessionStartUtcKey = "GeeklabSDK_SessionStartUtc";
        private const string SessionDurationKey = "GeeklabSDK_SessionDuration";
        private const string SessionRetentionDayKey = "GeeklabSDK_SessionRetentionDay";

        private static SessionManager instance;

        internal static event Action OnSessionContextAvailable;

        private bool sessionActive;
        private string sessionId;
        private int sessionIndex;
        private DateTimeOffset sessionStartUtc;
        private DateTimeOffset? lastPauseUtc;
        private DateTimeOffset? currentSegmentStartUtc; // When the current active segment started
        private double accumulatedDurationSeconds;       // Total playtime accumulated so far
        private bool startupSessionEvaluated;

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

            TrackSessionOnStartup();
        }

        internal static void TrackSessionOnStartup()
        {
            if (instance == null)
                return;

            instance.InitializeSessionLifecycle();
        }

        internal static bool TryGetCurrentSession(out string currentSessionId, out int currentSessionIndex)
        {
            currentSessionId = null;
            currentSessionIndex = 0;

            if (instance == null)
            {
                return false;
            }

            if (!instance.startupSessionEvaluated)
            {
                instance.InitializeSessionLifecycle();
            }

            instance.EnsureSessionIsCurrent(DateTimeOffset.UtcNow);

            if (string.IsNullOrEmpty(instance.sessionId) || instance.sessionIndex <= 0)
            {
                return false;
            }

            currentSessionId = instance.sessionId;
            currentSessionIndex = instance.sessionIndex;
            return true;
        }

        private void InitializeSessionLifecycle()
        {
            if (startupSessionEvaluated)
                return;

            startupSessionEvaluated = true;

            if (SDKSettingsModel.Instance != null && SDKSettingsModel.Instance.ShowDebugLog)
            {
                Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} SessionManager - initializing session management");
            }

            LoadSessionState();
            StartSessionIfNeeded(DateTimeOffset.UtcNow);
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (instance != this)
                return;

            if (!startupSessionEvaluated)
            {
                return;
            }

            var now = DateTimeOffset.UtcNow;
            
            if (isPaused)
            {
                // App going to background - save current segment duration
                SaveCurrentSegmentDuration(now);
                lastPauseUtc = now;
                UpdateLastActive(now);
                return;
            }

            // App resuming from background.
            // Refresh the retention day BEFORE any new session start so resumed-session
            // events carry the current day instead of the value cached at the last cold start.
            UserMetrics.PrepareRetentionOnStartup();

            if (lastPauseUtc.HasValue)
            {
                var gapSeconds = (now - lastPauseUtc.Value).TotalSeconds;
                if (gapSeconds > SessionTimeoutSeconds)
                {
                    // Session timed out while in background
                    EndSessionWithAccumulatedDuration("background_timeout");
                    StartNewSession(now);
                }
                else
                {
                    // Resuming within timeout - start new segment
                    currentSegmentStartUtc = now;
                }
            }

            UpdateLastActive(now);

            // Re-evaluate retention on resume so a day boundary crossed while backgrounded
            // sends the retention event without requiring a full cold start. The existing
            // retention guard (lastSentMetricDate / RetentionGuardDay) prevents duplicates.
            UserMetrics.UpdateRetention();
        }

        private void OnApplicationQuit()
        {
            if (instance != this)
                return;

            if (!startupSessionEvaluated)
            {
                return;
            }

            var now = DateTimeOffset.UtcNow;
            
            if (SDKSettingsModel.Instance != null && SDKSettingsModel.Instance.ShowDebugLog)
            {
                Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} OnApplicationQuit called - sessionActive={sessionActive}, sessionId={sessionId}");
            }

            // Save current segment duration before quit
            SaveCurrentSegmentDuration(now);
            UpdateLastActive(now);
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
        
        private void SaveCurrentSegmentDuration(DateTimeOffset endTime)
        {
            if (!sessionActive || !currentSegmentStartUtc.HasValue)
                return;
                
            var segmentDuration = (endTime - currentSegmentStartUtc.Value).TotalSeconds;
            if (segmentDuration > 0)
            {
                accumulatedDurationSeconds += segmentDuration;
                PlayerPrefs.SetFloat(SessionDurationKey, (float)accumulatedDurationSeconds);
                PlayerPrefs.Save();
                
                if (SDKSettingsModel.Instance != null && SDKSettingsModel.Instance.ShowDebugLog)
                {
                    Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Saved segment duration: {segmentDuration:F1}s, total accumulated: {accumulatedDurationSeconds:F1}s");
                }
            }
            
            currentSegmentStartUtc = null; // Segment ended
        }

        private void LoadSessionState()
        {
            sessionId = PlayerPrefs.GetString(SessionIdKey, "");
            sessionIndex = PlayerPrefs.GetInt(SessionIndexKey, 0);
            
            // Restore session start time if available
            var startUtcRaw = PlayerPrefs.GetString(SessionStartUtcKey, "");
            if (TryReadPersistedUnixTime(startUtcRaw, out var parsedSessionStartUtc))
            {
                sessionStartUtc = parsedSessionStartUtc;
            }
            
            // Restore accumulated duration
            accumulatedDurationSeconds = PlayerPrefs.GetFloat(SessionDurationKey, 0f);
        }

        private void StartSessionIfNeeded(DateTimeOffset now)
        {
            var lastActive = GetLastActiveUtc();
            var gapSeconds = lastActive.HasValue ? (now - lastActive.Value).TotalSeconds : double.MaxValue;
            var hasExistingSession = !string.IsNullOrEmpty(sessionId) && sessionStartUtc != default;

            if (SDKSettingsModel.Instance != null && SDKSettingsModel.Instance.ShowDebugLog)
            {
                Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} StartSessionIfNeeded: hasExistingSession={hasExistingSession}, gapSeconds={gapSeconds:F1}, accumulatedDuration={accumulatedDurationSeconds:F1}s, timeout={SessionTimeoutSeconds}");
            }

            if (!hasExistingSession)
            {
                // No previous session - start fresh
                StartNewSession(now);
            }
            else if (gapSeconds > SessionTimeoutSeconds)
            {
                // Session timed out - send end event for previous session using accumulated duration
                EndPreviousSessionWithAccumulatedDuration("timeout");
                StartNewSession(now);
            }
            else
            {
                // Resume existing session (within 30-minute window)
                sessionActive = true;
                currentSegmentStartUtc = now; // Start tracking this segment
                UpdateLastActive(now);
                NotifySessionContextAvailable();
                if (SDKSettingsModel.Instance != null && SDKSettingsModel.Instance.ShowDebugLog)
                {
                    Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Resuming existing session: sid={sessionId}, si={sessionIndex}, accumulated={accumulatedDurationSeconds:F1}s");
                }
            }
        }

        private void EnsureSessionIsCurrent(DateTimeOffset now)
        {
            if (!startupSessionEvaluated)
            {
                return;
            }

            var hasExistingSession = !string.IsNullOrEmpty(sessionId) && sessionStartUtc != default;
            if (!hasExistingSession)
            {
                StartNewSession(now);
                lastPauseUtc = null;
                return;
            }

            if (sessionActive && currentSegmentStartUtc.HasValue)
            {
                return;
            }

            var lastInactiveUtc = lastPauseUtc ?? GetLastActiveUtc();
            if (!lastInactiveUtc.HasValue)
            {
                return;
            }

            var gapSeconds = (now - lastInactiveUtc.Value).TotalSeconds;
            if (gapSeconds <= SessionTimeoutSeconds)
            {
                return;
            }

            EndPreviousSessionWithAccumulatedDuration("timeout");
            StartNewSession(now);
            lastPauseUtc = null;
        }

        private void EndPreviousSessionWithAccumulatedDuration(string reason)
        {
            // Use the accumulated playtime (not timestamp-based calculation)
            var durationSeconds = accumulatedDurationSeconds;
            if (durationSeconds < 0)
                durationSeconds = 0;

            var payload = WebhookPayloadFactory.CreateSessionEnd(reason, sessionId, sessionIndex, durationSeconds);

            var shouldSend = SDKSettingsModel.Instance != null &&
                             SDKSettingsModel.Instance.IsSDKEnabled &&
                             SDKSettingsModel.Instance.SendStatistics;
            if (shouldSend)
            {
                if (SDKSettingsModel.Instance.ShowDebugLog)
                {
                    Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Sending session end event: sid={sessionId}, reason={reason}, duration={durationSeconds:F1}s (accumulated playtime)");
                }
                WebRequestManager.Instance.SendSessionEventRequest(payload, false,
                    retentionDayOverride: GetSessionRetentionDayOverride());
            }
        }

        private void StartNewSession(DateTimeOffset startTime)
        {
            sessionIndex = PlayerPrefs.GetInt(SessionIndexKey, 0) + 1;
            sessionId = Guid.NewGuid().ToString();
            sessionStartUtc = startTime;
            sessionActive = true;
            
            // Reset accumulated duration and start tracking this segment
            accumulatedDurationSeconds = 0;
            currentSegmentStartUtc = startTime;

            PlayerPrefs.SetInt(SessionIndexKey, sessionIndex);
            PlayerPrefs.SetString(SessionIdKey, sessionId);
            PlayerPrefs.SetString(SessionStartUtcKey, startTime.ToUnixTimeSeconds().ToString());
            PlayerPrefs.SetFloat(SessionDurationKey, 0f);

            // Capture the retention day this session belongs to so its eventual end event is
            // attributed to the day the session started, not the day the end is delivered.
            var sessionRetentionDay = WebRequestManager.GetCurrentRetentionDay();
            if (sessionRetentionDay.HasValue)
            {
                PlayerPrefs.SetInt(SessionRetentionDayKey, sessionRetentionDay.Value);
            }
            else
            {
                PlayerPrefs.DeleteKey(SessionRetentionDayKey);
            }

            PlayerPrefs.Save();
            UpdateLastActive(startTime);

            var shouldSend = SDKSettingsModel.Instance != null &&
                             SDKSettingsModel.Instance.IsSDKEnabled &&
                             SDKSettingsModel.Instance.SendStatistics;

            var payload = WebhookPayloadFactory.CreateSessionStart(sessionId, sessionIndex);

            if (shouldSend)
            {
                // Start events use the current retention day (resolved at send time).
                WebRequestManager.Instance.SendSessionEventRequest(payload, true);
            }

            NotifySessionContextAvailable();
        }

        private void EndSessionWithAccumulatedDuration(string reason)
        {
            if (!sessionActive)
            {
                return;
            }

            // Use accumulated playtime
            var durationSeconds = accumulatedDurationSeconds;
            if (durationSeconds < 0)
                durationSeconds = 0;

            var payload = WebhookPayloadFactory.CreateSessionEnd(reason, sessionId, sessionIndex, durationSeconds);

            sessionActive = false;
            var shouldSend = SDKSettingsModel.Instance != null &&
                             SDKSettingsModel.Instance.IsSDKEnabled &&
                             SDKSettingsModel.Instance.SendStatistics;
            if (shouldSend)
            {
                if (SDKSettingsModel.Instance.ShowDebugLog)
                {
                    Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Sending session end event: sid={sessionId}, reason={reason}, duration={durationSeconds:F1}s (accumulated playtime)");
                }
                WebRequestManager.Instance.SendSessionEventRequest(payload, false,
                    retentionDayOverride: GetSessionRetentionDayOverride());
            }
        }

        private static DateTimeOffset? GetLastActiveUtc()
        {
            var raw = PlayerPrefs.GetString(LastActiveUtcKey, "");
            if (TryReadPersistedUnixTime(raw, out var lastActiveUtc))
            {
                return lastActiveUtc;
            }

            return null;
        }

        private static bool TryReadPersistedUnixTime(string raw, out DateTimeOffset value)
        {
            value = default;

            if (!long.TryParse(raw, out var unixTime))
            {
                return false;
            }

            try
            {
                value = unixTime > 99_999_999_999
                    ? DateTimeOffset.FromUnixTimeMilliseconds(unixTime)
                    : DateTimeOffset.FromUnixTimeSeconds(unixTime);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        private static void UpdateLastActive(DateTimeOffset time)
        {
            PlayerPrefs.SetString(LastActiveUtcKey, time.ToUnixTimeSeconds().ToString());
            PlayerPrefs.Save();
        }

        private static void NotifySessionContextAvailable()
        {
            OnSessionContextAvailable?.Invoke();
        }

        // Retention day captured when the current session started. Used to attribute a session
        // end event to the day the session began, even if it is delivered on a later day.
        private static int? GetSessionRetentionDayOverride()
        {
            if (PlayerPrefs.HasKey(SessionRetentionDayKey))
            {
                return PlayerPrefs.GetInt(SessionRetentionDayKey);
            }

            // Migration safety: sessions that started under an older SDK build never persisted
            // the key above. Reconstruct the session's day from its persisted start time and the
            // first-login date (both written by older builds) so the final end event of a
            // pre-update session is not misattributed to the current day.
            return ReconstructSessionRetentionDayFromStartTime();
        }

        private static int? ReconstructSessionRetentionDayFromStartTime()
        {
            var firstLogin = PlayerPrefs.GetString("firstLogin", "");
            if (string.IsNullOrEmpty(firstLogin))
            {
                return null;
            }

            if (!TryReadPersistedUnixTime(PlayerPrefs.GetString(SessionStartUtcKey, ""), out var sessionStartUtc))
            {
                return null;
            }

            try
            {
                var firstLoginDate = DateTime.ParseExact(firstLogin, "dd/MM/yyyy", null);
                var sessionStartLocalDate = sessionStartUtc.ToLocalTime().Date;
                var days = (sessionStartLocalDate - firstLoginDate).Days;
                return days >= 0 ? days : (int?)null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
