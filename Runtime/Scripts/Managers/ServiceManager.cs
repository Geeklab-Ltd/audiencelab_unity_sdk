using UnityEngine;
using UnityEngine.EventSystems;


namespace Geeklab.AudiencelabSDK
{
    public class ServiceManager : MonoBehaviour
    {
        public static DeepLinkHandler DeepLinkHandler { get; private set; }
        public static DeviceInfoHandler DeviceInfoHandler { get; private set; }
        public static TokenHandler TokenHandler { get; private set; }
        public static IdentityHandler IdentityHandler { get; private set; }

        public static UserMetrics UserMetrics { get; private set; }
        public static SessionManager SessionManager { get; private set; }

        public static MetricToggle MetricToggle { get; private set; }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            gameObject.AddComponent<WebRequestManager>();

            TokenHandler = gameObject.AddComponent<TokenHandler>();
            DeepLinkHandler = gameObject.AddComponent<DeepLinkHandler>();
            DeviceInfoHandler = gameObject.AddComponent<DeviceInfoHandler>();
            IdentityHandler = gameObject.AddComponent<IdentityHandler>();

            UserMetrics = gameObject.AddComponent<UserMetrics>();
            SessionManager = gameObject.AddComponent<SessionManager>();

            MetricToggle = gameObject.AddComponent<MetricToggle>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DebugOverlayManager.EnsureCreated();
#endif
        }
    }
}