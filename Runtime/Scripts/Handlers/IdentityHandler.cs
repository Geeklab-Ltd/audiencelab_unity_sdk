using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Geeklab.AudiencelabSDK
{
    public class IdentityHandler : MonoBehaviour
    {
        private const float IdentitySettleTimeoutSeconds = 2f;

        private static IdentityHandler instance;
        private readonly IdentityInfoModel identityInfo = new IdentityInfoModel();
        private bool collectionStarted;
        private bool collectionComplete;
        private float collectionStartTime;
        private bool shouldWaitForIdentity;
        private bool hasReadFinalIdentity;

        public static bool IsSettled
        {
            get
            {
                if (instance == null || !instance.collectionStarted)
                    return false;

                if (!instance.shouldWaitForIdentity)
                    return true;

                if (instance.collectionComplete)
                    return true;

                return Time.realtimeSinceStartup - instance.collectionStartTime >= IdentitySettleTimeoutSeconds;
            }
        }

        public static IdentityInfoModel Current
        {
            get
            {
                if (instance == null)
                    return new IdentityInfoModel();

#if UNITY_ANDROID && !UNITY_EDITOR
                // Ensure we read the final identity values if collection is complete
                if (instance.collectionComplete && !instance.hasReadFinalIdentity)
                {
                    instance.TryUpdateIdentityFromAndroid();
                    instance.hasReadFinalIdentity = true;
                }
#endif

                return instance.identityInfo;
            }
        }

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                StartCollection();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            if (!collectionStarted || collectionComplete)
                return;

            if (Time.realtimeSinceStartup - collectionStartTime >= IdentitySettleTimeoutSeconds)
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                // Final read before marking complete
                TryUpdateIdentityFromAndroid();
                hasReadFinalIdentity = true;
#endif
                collectionComplete = true;
                LogIdentityStatus("timeout");
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            TryUpdateIdentityFromAndroid();
            if (IsAndroidCollectionComplete())
            {
                hasReadFinalIdentity = true;
                collectionComplete = true;
                LogIdentityStatus("native_complete");
            }
#endif
        }

        private void LogIdentityStatus(string reason)
        {
            if (SDKSettingsModel.Instance != null && SDKSettingsModel.Instance.ShowDebugLog)
            {
                Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Identity settled ({reason}): gaid={identityInfo.gaid ?? "null"}, app_set_id={identityInfo.app_set_id ?? "null"}, android_id={identityInfo.android_id ?? "null"}, idfv={identityInfo.idfv ?? "null"}");
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            WarnIfIdentityMissing();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void WarnIfIdentityMissing()
        {
            var settings = AudienceLabSettings.Instance;
            if (settings == null)
                return;

            bool autoGaid = settings.enableGaidAutoCollection;
            bool autoAppSet = settings.enableAppSetIdAutoCollection;

            if (autoGaid && string.IsNullOrEmpty(identityInfo.gaid))
            {
                Debug.LogWarning(
                    "[AudienceLab] Auto GAID collection is enabled but GAID is null. " +
                    "This usually means Google Play Services dependencies are missing from the build. " +
                    "Install the External Dependency Manager (EDM) package, or add the dependencies " +
                    "to your mainTemplate.gradle. See SDK Settings > Android Identity for details.");
            }

            if (autoAppSet && string.IsNullOrEmpty(identityInfo.app_set_id))
            {
                Debug.LogWarning(
                    "[AudienceLab] Auto App Set ID collection is enabled but App Set ID is null. " +
                    "This usually means Google Play Services dependencies are missing from the build. " +
                    "Install the External Dependency Manager (EDM) package, or add the dependencies " +
                    "to your mainTemplate.gradle. See SDK Settings > Android Identity for details.");
            }
        }
#endif

        public static IEnumerator WaitForIdentitySettle(Action onSettled)
        {
            if (instance == null)
            {
                onSettled?.Invoke();
                yield break;
            }

            while (!IsSettled)
            {
                yield return null;
            }

            onSettled?.Invoke();
        }

        private void StartCollection()
        {
            collectionStarted = true;
            collectionStartTime = Time.realtimeSinceStartup;

            if (SDKSettingsModel.Instance != null && SDKSettingsModel.Instance.ShowDebugLog)
            {
                Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Identity collection starting...");
            }

#if UNITY_IOS && !UNITY_EDITOR
            identityInfo.idfv = GetIdfv();
            shouldWaitForIdentity = false;
            collectionComplete = true;
            LogIdentityStatus("ios_immediate");
#elif UNITY_ANDROID && !UNITY_EDITOR
            var manualGaid = global::AudiencelabSDK.GetManualAdvertisingId();
            var manualAppSetId = global::AudiencelabSDK.GetManualAppSetId();
            var settings = AudienceLabSettings.Instance;
            var autoMode = settings != null ? settings.enableGaidAutoCollection :
                (SDKSettingsModel.Instance != null && SDKSettingsModel.Instance.EnableGaidCollection);
            var allowAppSetAuto = settings == null || settings.enableAppSetIdAutoCollection;

            var hasManualGaid = !string.IsNullOrEmpty(manualGaid);
            var hasManualAppSet = !string.IsNullOrEmpty(manualAppSetId);
            // Auto-collect GAID if enabled and no manual value provided
            var allowGaidAuto = autoMode && !hasManualGaid;
            var allowAppSetIdAuto = allowAppSetAuto && !hasManualAppSet;

            if (SDKSettingsModel.Instance != null && SDKSettingsModel.Instance.ShowDebugLog)
            {
                Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Android identity config: autoMode={autoMode}, allowGaidAuto={allowGaidAuto}, allowAppSetIdAuto={allowAppSetIdAuto}, hasManualGaid={hasManualGaid}, hasManualAppSet={hasManualAppSet}");
            }

            // Apply manual values immediately if provided
            if (hasManualGaid)
            {
                identityInfo.gaid = manualGaid;
            }
            if (hasManualAppSet)
            {
                identityInfo.app_set_id = manualAppSetId;
            }

            shouldWaitForIdentity = allowGaidAuto || allowAppSetIdAuto;
            StartAndroidCollection(allowGaidAuto);
#else
            shouldWaitForIdentity = false;
            collectionComplete = true;
            LogIdentityStatus("editor_immediate");
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern IntPtr _GetIDFV();

        private static string GetIdfv()
        {
            try
            {
                var ptr = _GetIDFV();
                return Marshal.PtrToStringAnsi(ptr);
            }
            catch (Exception)
            {
                return null;
            }
        }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        private void StartAndroidCollection(bool allowGaid)
        {
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var identityClass = new AndroidJavaClass("com.Geeklab.plugin.AudienceLabIdentity"))
                {
                    identityClass.CallStatic("StartCollecting", currentActivity, allowGaid);
                    if (SDKSettingsModel.Instance != null && SDKSettingsModel.Instance.ShowDebugLog)
                    {
                        Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Android identity collection started (allowGaid={allowGaid})");
                    }
                }
            }
            catch (Exception ex)
            {
                if (SDKSettingsModel.Instance != null && SDKSettingsModel.Instance.ShowDebugLog)
                {
                    Debug.LogWarning($"{SDKSettingsModel.GetColorPrefixLog()} Failed to start Android identity collection: {ex.Message}");
                }
                collectionComplete = true;
            }
        }

        private void TryUpdateIdentityFromAndroid()
        {
            try
            {
                using (var identityClass = new AndroidJavaClass("com.Geeklab.plugin.AudienceLabIdentity"))
                {
                    var manualGaid = global::AudiencelabSDK.GetManualAdvertisingId();
                    var manualAppSetId = global::AudiencelabSDK.GetManualAppSetId();
                    var settings = AudienceLabSettings.Instance;
                    var autoMode = settings != null ? settings.enableGaidAutoCollection :
                        (SDKSettingsModel.Instance != null && SDKSettingsModel.Instance.EnableGaidCollection);
                    var allowAppSetAuto = settings == null || settings.enableAppSetIdAutoCollection;

                    var hasManualGaid = !string.IsNullOrEmpty(manualGaid);
                    var hasManualAppSet = !string.IsNullOrEmpty(manualAppSetId);

                    // Auto-collect GAID if enabled and no manual value provided
                    var allowGaidAuto = autoMode && !hasManualGaid;
                    var allowAppSetIdAuto = allowAppSetAuto && !hasManualAppSet;

                    // GAID: manual takes priority, then auto-collect, else null
                    if (hasManualGaid)
                    {
                        identityInfo.gaid = manualGaid;
                    }
                    else if (allowGaidAuto)
                    {
                        identityInfo.gaid = identityClass.CallStatic<string>("GetGaid");
                    }
                    else
                    {
                        identityInfo.gaid = null;
                    }

                    // App Set ID: manual takes priority, then auto-collect, else null
                    if (hasManualAppSet)
                    {
                        identityInfo.app_set_id = manualAppSetId;
                    }
                    else if (allowAppSetIdAuto)
                    {
                        identityInfo.app_set_id = identityClass.CallStatic<string>("GetAppSetId");
                    }
                    else
                    {
                        identityInfo.app_set_id = null;
                    }

                    // Android ID is always collected
                    identityInfo.android_id = identityClass.CallStatic<string>("GetAndroidId");

                    // Limit ad tracking only when auto-collecting GAID
                    if (allowGaidAuto)
                    {
                        var limitAdTracking = identityClass.CallStatic<AndroidJavaObject>("GetLimitAdTracking");
                        if (limitAdTracking != null)
                        {
                            identityInfo.limit_ad_tracking = limitAdTracking.Call<bool>("booleanValue");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the error but allow timeout to settle
                if (SDKSettingsModel.Instance != null && SDKSettingsModel.Instance.ShowDebugLog)
                {
                    Debug.LogWarning($"{SDKSettingsModel.GetColorPrefixLog()} Failed to read identity from Android: {ex.Message}");
                }
            }
        }

        private static bool IsAndroidCollectionComplete()
        {
            try
            {
                using (var identityClass = new AndroidJavaClass("com.Geeklab.plugin.AudienceLabIdentity"))
                {
                    return identityClass.CallStatic<bool>("IsCollectionComplete");
                }
            }
            catch (Exception)
            {
                return true;
            }
        }
#endif
    }
}
