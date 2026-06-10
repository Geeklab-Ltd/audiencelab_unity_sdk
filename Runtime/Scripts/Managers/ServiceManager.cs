using UnityEngine;
using UnityEngine.EventSystems;


namespace Geeklab.AudiencelabSDK
{
    public class ServiceManager : MonoBehaviour
    {
        private static ServiceManager instance;

        public static DeepLinkHandler DeepLinkHandler { get; private set; }
        public static DeviceInfoHandler DeviceInfoHandler { get; private set; }
        public static TokenHandler TokenHandler { get; private set; }
        public static IdentityHandler IdentityHandler { get; private set; }

        public static UserMetrics UserMetrics { get; private set; }
        public static SessionManager SessionManager { get; private set; }

        public static MetricToggle MetricToggle { get; private set; }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            WebRequestManager.EnsureOn(gameObject);

            TokenHandler = EnsureComponent<TokenHandler>();
            DeepLinkHandler = EnsureComponent<DeepLinkHandler>();
            DeviceInfoHandler = EnsureComponent<DeviceInfoHandler>();
            IdentityHandler = EnsureComponent<IdentityHandler>();
            global::Geeklab.AudiencelabSDK.TokenHandler.EnsureTokenAvailabilityStarted();

            SessionManager = EnsureComponent<SessionManager>();
            UserMetrics = EnsureComponent<UserMetrics>();
            global::Geeklab.AudiencelabSDK.UserMetrics.PrepareRetentionOnStartup();
            global::Geeklab.AudiencelabSDK.SessionManager.TrackSessionOnStartup();
            global::Geeklab.AudiencelabSDK.UserMetrics.TrackRetentionOnStartup();

            MetricToggle = EnsureComponent<MetricToggle>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DebugOverlayManager.EnsureCreated();
#endif
        }

        private void OnDestroy()
        {
            if (instance != this)
                return;

            instance = null;
            DeepLinkHandler = null;
            DeviceInfoHandler = null;
            TokenHandler = null;
            IdentityHandler = null;
            UserMetrics = null;
            SessionManager = null;
            MetricToggle = null;
        }

        private T EnsureComponent<T>() where T : Component
        {
            var component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }
    }
}
