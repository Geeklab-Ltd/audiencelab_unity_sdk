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

            if (ToggleKeyPressedThisFrame(settings.debugOverlayToggleKey))
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
            var touchCount = GetActiveTouchCount();
            if (touchCount == 0)
            {
                wasFiveFingerTouchActive = false;
                return false;
            }

            if (touchCount >= 5)
            {
                if (!wasFiveFingerTouchActive)
                {
                    wasFiveFingerTouchActive = true;
                    return true;
                }
            }

            return false;
        }

        private static bool ToggleKeyPressedThisFrame(KeyCode keyCode)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(keyCode);
#elif ENABLE_INPUT_SYSTEM
            return InputSystemKeyPressedThisFrame(keyCode);
#else
            return false;
#endif
        }

        private static int GetActiveTouchCount()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.touchCount;
#elif ENABLE_INPUT_SYSTEM
            return GetInputSystemTouchCount();
#else
            return 0;
#endif
        }

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        // Use reflection so this package's core assembly remains optional-dependency safe. A
        // direct Unity.InputSystem reference in this asmdef would force every SDK consumer to
        // install the Input System package, including legacy-input-only projects.
        private static bool InputSystemKeyPressedThisFrame(KeyCode keyCode)
        {
            try
            {
                var keyboardType = Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
                var keyType = Type.GetType("UnityEngine.InputSystem.Key, Unity.InputSystem");
                if (keyboardType == null || keyType == null)
                    return false;

                var keyboard = keyboardType.GetProperty("current")?.GetValue(null);
                if (keyboard == null || !TryMapKeyCodeToInputSystemKeyName(keyCode, out var keyName))
                    return false;

                var key = Enum.Parse(keyType, keyName);
                var control = keyboardType.GetProperty("Item", new[] { keyType })
                    ?.GetValue(keyboard, new[] { key });
                var pressed = control?.GetType().GetProperty("wasPressedThisFrame")?.GetValue(control);
                return pressed is bool wasPressed && wasPressed;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static int GetInputSystemTouchCount()
        {
            try
            {
                var touchscreenType = Type.GetType("UnityEngine.InputSystem.Touchscreen, Unity.InputSystem");
                var touchscreen = touchscreenType?.GetProperty("current")?.GetValue(null);
                var touches = touchscreenType?.GetProperty("touches")?.GetValue(touchscreen)
                    as System.Collections.IEnumerable;
                if (touches == null)
                    return 0;

                var count = 0;
                foreach (var touch in touches)
                {
                    var press = touch?.GetType().GetProperty("press")?.GetValue(touch);
                    var isPressed = press?.GetType().GetProperty("isPressed")?.GetValue(press);
                    if (isPressed is bool pressed && pressed)
                        count++;
                }

                return count;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static bool TryMapKeyCodeToInputSystemKeyName(KeyCode keyCode, out string keyName)
        {
            switch (keyCode)
            {
                case KeyCode.Alpha0: keyName = "Digit0"; return true;
                case KeyCode.Alpha1: keyName = "Digit1"; return true;
                case KeyCode.Alpha2: keyName = "Digit2"; return true;
                case KeyCode.Alpha3: keyName = "Digit3"; return true;
                case KeyCode.Alpha4: keyName = "Digit4"; return true;
                case KeyCode.Alpha5: keyName = "Digit5"; return true;
                case KeyCode.Alpha6: keyName = "Digit6"; return true;
                case KeyCode.Alpha7: keyName = "Digit7"; return true;
                case KeyCode.Alpha8: keyName = "Digit8"; return true;
                case KeyCode.Alpha9: keyName = "Digit9"; return true;
                case KeyCode.Keypad0: keyName = "Numpad0"; return true;
                case KeyCode.Keypad1: keyName = "Numpad1"; return true;
                case KeyCode.Keypad2: keyName = "Numpad2"; return true;
                case KeyCode.Keypad3: keyName = "Numpad3"; return true;
                case KeyCode.Keypad4: keyName = "Numpad4"; return true;
                case KeyCode.Keypad5: keyName = "Numpad5"; return true;
                case KeyCode.Keypad6: keyName = "Numpad6"; return true;
                case KeyCode.Keypad7: keyName = "Numpad7"; return true;
                case KeyCode.Keypad8: keyName = "Numpad8"; return true;
                case KeyCode.Keypad9: keyName = "Numpad9"; return true;
                case KeyCode.Return: keyName = "Enter"; return true;
                case KeyCode.KeypadEnter: keyName = "NumpadEnter"; return true;
                case KeyCode.LeftControl: keyName = "LeftCtrl"; return true;
                case KeyCode.RightControl: keyName = "RightCtrl"; return true;
                case KeyCode.LeftCommand: keyName = "LeftMeta"; return true;
                case KeyCode.RightCommand: keyName = "RightMeta"; return true;
                case KeyCode.BackQuote: keyName = "Backquote"; return true;
                default:
                    keyName = keyCode.ToString();
                    return true;
            }
        }
#endif

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
                GUILayout.Label($"envelope retention_day: {(envelope.retention_day.HasValue ? envelope.retention_day.Value.ToString() : "n/a")} | sid: {Truncate(envelope.sid, 8)} | si: {(envelope.si.HasValue ? envelope.si.Value.ToString() : "n/a")}");
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
            GUILayout.Label("Recent Events (newest first)", EditorHeaderStyle());
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
