using System;
using System.Collections.Generic;
using UnityEngine;

namespace Geeklab.AudiencelabSDK
{
    public class DebugOverlayManager : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static DebugOverlayManager instance;
        private readonly List<RequestResult> entries = new List<RequestResult>();
        private RequestResult lastResult;
        private bool isVisible = false;
        private bool wasFiveFingerTouchActive = false;
        private int selectedEntryIndex = -1;
        private Vector2 eventsScrollPosition;
        private Vector2 detailsScrollPosition;

        public static void EnsureCreated()
        {
            if (instance != null)
                return;

            var go = new GameObject("AudienceLabDebugOverlay");
            instance = go.AddComponent<DebugOverlayManager>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            // Subscribe as early as possible to catch all requests including fetch-token
            WebRequestManager.OnRequestResult += HandleRequestResult;
        }

        private void OnDestroy()
        {
            WebRequestManager.OnRequestResult -= HandleRequestResult;
        }

        private void Update()
        {
            var settings = AudienceLabSettings.Instance;
            if (settings == null)
                return;

            if (Input.GetKeyDown(settings.debugOverlayToggleKey))
            {
                isVisible = !isVisible;
            }

            if (IsFiveFingerToggleTriggered())
            {
                isVisible = !isVisible;
            }
        }

        private bool IsFiveFingerToggleTriggered()
        {
            if (Input.touchCount == 0)
            {
                wasFiveFingerTouchActive = false;
                return false;
            }

            if (Input.touchCount >= 5)
            {
                if (!wasFiveFingerTouchActive)
                {
                    wasFiveFingerTouchActive = true;
                    return true;
                }
            }

            return false;
        }

        private void HandleRequestResult(RequestResult result)
        {
            lastResult = result;
            var settings = AudienceLabSettings.Instance;
            if (settings == null)
                return;

            entries.Insert(0, result);
            if (selectedEntryIndex >= 0)
            {
                selectedEntryIndex++;
            }
            else
            {
                selectedEntryIndex = 0;
            }
            if (entries.Count > settings.debugOverlayMaxEvents)
            {
                entries.RemoveAt(entries.Count - 1);
                if (selectedEntryIndex >= entries.Count)
                {
                    selectedEntryIndex = entries.Count - 1;
                }
            }
        }

        private void OnGUI()
        {
            if (!AudienceLabSettings.IsDebugOverlayEnabled())
                return;

            if (!isVisible)
                return;

            var settings = AudienceLabSettings.Instance;
            if (settings == null)
                return;

            var screenRect = new Rect(0, 0, Screen.width, Screen.height);
            var padding = 16f;

            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.Box(screenRect, GUIContent.none);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(padding, padding, Screen.width - (padding * 2f), Screen.height - (padding * 2f)));
            GUILayout.BeginHorizontal();
            GUILayout.Label("AudienceLab SDK Debug", EditorHeaderStyle());
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("X", CloseButtonStyle(), GUILayout.Width(28), GUILayout.Height(24)))
            {
                isVisible = false;
                GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            var tokenValue = TokenHandler.GetValidToken();
            var tokenStatus = TokenHandler.GetLastFetchStatus() ?? "unknown";
            var lastAttempt = TokenHandler.GetLastFetchAttemptUtc();
            var lastAttemptText = lastAttempt.HasValue ? lastAttempt.Value.ToString("HH:mm:ss") : "n/a";
            GUILayout.Label($"creativeToken: {Truncate(tokenValue, 8)} | status: {tokenStatus} | last attempt: {lastAttemptText} UTC");

            var identityInfo = IdentityHandler.Current;
            GUILayout.Label($"idfv: {Presence(identityInfo.idfv)} | gaid: {Presence(identityInfo.gaid)} | app_set_id: {Presence(identityInfo.app_set_id)} | android_id: {Presence(identityInfo.android_id)}");
            var envelope = WebRequestManager.LastWebhookEnvelope;
            if (envelope != null)
            {
                GUILayout.Label($"envelope gaid: {Presence(envelope.gaid)} | app_set_id: {Presence(envelope.app_set_id)} | android_id: {Presence(envelope.android_id)} | lat: {Presence(envelope.limit_ad_tracking?.ToString())}");
                GUILayout.Label($"envelope retention_day: {(envelope.retention_day.HasValue ? envelope.retention_day.Value.ToString() : "n/a")}");
            }

            if (settings.showRawIdentifiers)
            {
                GUILayout.Label($"idfv(raw): {Truncate(identityInfo.idfv, 8)} | gaid(raw): {Truncate(identityInfo.gaid, 8)}");
                GUILayout.Label($"app_set_id(raw): {Truncate(identityInfo.app_set_id, 8)} | android_id(raw): {Truncate(identityInfo.android_id, 8)}");
            }

            GUILayout.Space(4);
            GUILayout.Label($"last: {FormatLastResult(lastResult)}");

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(380));
            GUILayout.Label("Recent Events", EditorHeaderStyle());
            eventsScrollPosition = GUILayout.BeginScrollView(eventsScrollPosition, GUILayout.ExpandHeight(true));
            for (var i = 0; i < entries.Count; i++)
            {
                var label = BuildEntryLabel(entries[i]);
                var isSelected = i == selectedEntryIndex;
                var style = isSelected ? SelectedEntryStyle() : EntryButtonStyle();
                if (GUILayout.Button(label, style))
                {
                    selectedEntryIndex = i;
                    detailsScrollPosition = Vector2.zero;
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.Space(12);

            GUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            GUILayout.Label("Details", EditorHeaderStyle());
            detailsScrollPosition = GUILayout.BeginScrollView(detailsScrollPosition, GUILayout.ExpandHeight(true));
            if (selectedEntryIndex >= 0 && selectedEntryIndex < entries.Count)
            {
                var selected = entries[selectedEntryIndex];
                GUILayout.Label($"status: {(selected.success ? "OK" : "ERROR")}");
                GUILayout.Label($"endpoint: {selected.endpoint ?? "n/a"}");
                GUILayout.Label($"event_id: {Truncate(selected.eventId, 8)}");
                GUILayout.Label($"event_name: {selected.eventName ?? "n/a"}");
                if (!string.IsNullOrEmpty(selected.errorMessage))
                {
                    GUILayout.Label($"error: {selected.errorMessage}");
                }

                GUILayout.Space(6);
                GUILayout.Label("request:");
                GUILayout.TextArea(selected.requestBody ?? "n/a", WrappedTextAreaStyle(), GUILayout.ExpandHeight(true));

            GUILayout.Space(6);
                GUILayout.Label("response:");
                GUILayout.TextArea(selected.responseBody ?? "n/a", WrappedTextAreaStyle(), GUILayout.ExpandHeight(true));
            }
            else
            {
                GUILayout.Label("Select an event to view payload and response.");
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private static string BuildEntryLabel(RequestResult result)
        {
            var typeLabel = result.requestKind == "webhook" ? "webhook" : "fetch-token";
            if (!string.IsNullOrEmpty(result.eventType))
            {
                typeLabel += $" {result.eventType}";
            }
            if (!string.IsNullOrEmpty(result.eventName))
            {
                typeLabel += $"({result.eventName})";
            }

            return $"{result.timestampUtcIso} {typeLabel} event_id={Truncate(result.eventId, 8)}";
        }

        private static string FormatLastResult(RequestResult result)
        {
            if (result == null)
                return "none";

            var status = result.success ? "OK" : "ERROR";
            var code = result.httpStatus.HasValue ? result.httpStatus.Value.ToString() : "n/a";
            var typeLabel = result.requestKind == "webhook" ? "webhook" : "fetch-token";
            if (!string.IsNullOrEmpty(result.eventType))
            {
                typeLabel += $" {result.eventType}";
            }
            if (!string.IsNullOrEmpty(result.eventName))
            {
                typeLabel += $"({result.eventName})";
            }

            return $"{code} {status} {typeLabel} (event_id {Truncate(result.eventId, 8)})";
        }

        private static string Presence(string value)
        {
            return string.IsNullOrEmpty(value) ? "no" : "yes";
        }

        private static string Truncate(string value, int length)
        {
            if (string.IsNullOrEmpty(value))
                return "n/a";

            return value.Length <= length ? value : $"{value.Substring(0, length)}…";
        }

        private static GUIStyle EditorHeaderStyle()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold
            };
            return style;
        }

        private static GUIStyle EntryButtonStyle()
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                padding = new RectOffset(8, 8, 6, 6)
            };
            return style;
        }

        private static GUIStyle SelectedEntryStyle()
        {
            var style = new GUIStyle(EntryButtonStyle())
            {
                normal = { textColor = Color.white },
                hover = { textColor = Color.white }
            };
            style.normal.background = Texture2D.grayTexture;
            style.hover.background = Texture2D.grayTexture;
            return style;
        }

        private static GUIStyle WrappedTextAreaStyle()
        {
            var style = new GUIStyle(GUI.skin.textArea)
            {
                wordWrap = true
            };
            return style;
        }

        private static GUIStyle CloseButtonStyle()
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(4, 4, 2, 2)
            };
            return style;
        }
#endif
    }
}
