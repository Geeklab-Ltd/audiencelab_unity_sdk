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

        private bool sessionActive;
        private string sessionId;
        private int sessionIndex;
        private DateTimeOffset sessionStartUtc;
        private DateTimeOffset? lastPauseUtc;
        private DateTimeOffset? currentSegmentStartUtc; // When the current active segment started
        private double accumulatedDurationSeconds;       // Total playtime accumulated so far

        private void Start()
        {
            if (SDKSettingsModel.Instance != null && SDKSettingsModel.Instance.ShowDebugLog)
            {
                Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} SessionManager.Start() - initializing session management");
            }

            LoadSessionState();
            StartSessionIfNeeded(DateTimeOffset.UtcNow);
        }

        private void OnApplicationPause(bool isPaused)
        {
            var now = DateTimeOffset.UtcNow;
            
            if (isPaused)
            {
                // App going to background - save current segment duration
                SaveCurrentSegmentDuration(now);
                lastPauseUtc = now;
                UpdateLastActive(now);
                return;
            }

            // App resuming from background
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
        }

        private void OnApplicationQuit()
        {
            var now = DateTimeOffset.UtcNow;
            
            if (SDKSettingsModel.Instance != null && SDKSettingsModel.Instance.ShowDebugLog)
            {
                Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} OnApplicationQuit called - sessionActive={sessionActive}, sessionId={sessionId}");
            }

            // Save current segment duration before quit
            SaveCurrentSegmentDuration(now);
            UpdateLastActive(now);
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
            if (long.TryParse(startUtcRaw, out var unixSeconds))
            {
                sessionStartUtc = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
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
                if (SDKSettingsModel.Instance != null && SDKSettingsModel.Instance.ShowDebugLog)
                {
                    Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Resuming existing session: sid={sessionId}, si={sessionIndex}, accumulated={accumulatedDurationSeconds:F1}s");
                }
            }
        }

        private void EndPreviousSessionWithAccumulatedDuration(string reason)
        {
            // Use the accumulated playtime (not timestamp-based calculation)
            var durationSeconds = accumulatedDurationSeconds;
            if (durationSeconds < 0)
                durationSeconds = 0;

            var payload = new
            {
                a = "end",
                r = reason,
                sid = sessionId,
                si = sessionIndex,
                sd = durationSeconds
            };

            var shouldSend = SDKSettingsModel.Instance != null &&
                             SDKSettingsModel.Instance.IsSDKEnabled &&
                             SDKSettingsModel.Instance.SendStatistics;
            if (shouldSend)
            {
                if (SDKSettingsModel.Instance.ShowDebugLog)
                {
                    Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Sending session end event: sid={sessionId}, reason={reason}, duration={durationSeconds:F1}s (accumulated playtime)");
                }
                WebRequestManager.Instance.SendSessionEventRequest(payload, false);
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
            PlayerPrefs.Save();

            var shouldSend = SDKSettingsModel.Instance != null &&
                             SDKSettingsModel.Instance.IsSDKEnabled &&
                             SDKSettingsModel.Instance.SendStatistics;

            var payload = new
            {
                a = "start",
                sid = sessionId,
                si = sessionIndex
            };

            if (shouldSend)
            {
                WebRequestManager.Instance.SendSessionEventRequest(payload, true);
            }
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

            var payload = new
            {
                a = "end",
                r = reason,
                sid = sessionId,
                si = sessionIndex,
                sd = durationSeconds
            };

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
                WebRequestManager.Instance.SendSessionEventRequest(payload, false);
            }
        }

        private static DateTimeOffset? GetLastActiveUtc()
        {
            var raw = PlayerPrefs.GetString(LastActiveUtcKey, "");
            if (long.TryParse(raw, out var unixSeconds))
            {
                return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
            }

            return null;
        }

        private static void UpdateLastActive(DateTimeOffset time)
        {
            PlayerPrefs.SetString(LastActiveUtcKey, time.ToUnixTimeSeconds().ToString());
            PlayerPrefs.Save();
        }
    }
}
