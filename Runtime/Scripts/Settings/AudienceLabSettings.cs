using UnityEngine;

namespace Geeklab.AudiencelabSDK
{
    [CreateAssetMenu(fileName = "AudienceLabSettings", menuName = "AudienceLab/Settings", order = 2)]
    public class AudienceLabSettings : ScriptableObject
    {
        private static AudienceLabSettings _instance;

        public bool enableDebugOverlay = false;
        public int debugOverlayMaxEvents = 20;
        public bool showRawIdentifiers = false;
        public KeyCode debugOverlayToggleKey = KeyCode.F8;

        public bool enableGaidAutoCollection = true;
        public bool enableGaidManualMode = false;
        public bool enableAppSetIdAutoCollection = true;

        public static AudienceLabSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<AudienceLabSettings>("AudienceLabSettings");
                }
                return _instance;
            }
        }

        public static bool IsDebugOverlayEnabled()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return Instance != null && Instance.enableDebugOverlay;
#else
            return false;
#endif
        }
    }
}
