using UnityEngine;
using UnityEngine.EventSystems;


namespace Geeklab.AudiencelabSDK
{
    public class ServiceManager : MonoBehaviour
    {
        public static DeepLinkHandler DeepLinkHandler { get; private set; }
        public static DeviceInfoHandler DeviceInfoHandler { get; private set; }
        public static TokenHandler TokenHandler { get; private set; }

        public static UserMetrics UserMetrics { get; private set; }

        public static MetricToggle MetricToggle { get; private set; }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            gameObject.AddComponent<WebRequestManager>();

            TokenHandler = gameObject.AddComponent<TokenHandler>();
            DeepLinkHandler = gameObject.AddComponent<DeepLinkHandler>();
            DeviceInfoHandler = gameObject.AddComponent<DeviceInfoHandler>();

            UserMetrics = gameObject.AddComponent<UserMetrics>();

            MetricToggle = gameObject.AddComponent<MetricToggle>();
        }
    }
}