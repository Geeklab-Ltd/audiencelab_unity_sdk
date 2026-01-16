using System.Collections.Generic;
using UnityEngine;


namespace Geeklab.AudiencelabSDK
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "SDKSettings", menuName = "Geeklab/SDK Settings", order = 1)]
    public class SDKSettingsModel : ScriptableObject
    {
        private static SDKSettingsModel _instance;

        public static SDKSettingsModel Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<SDKSettingsModel>("SDKSettings");

                    if (_instance == null)
                    {
                        _instance = CreateInstance<SDKSettingsModel>();

#if UNITY_EDITOR
                        UnityEditor.AssetDatabase.CreateAsset(_instance, "Assets/Resources/SDKSettings.asset");
                        UnityEditor.AssetDatabase.SaveAssets();
#endif
                        if (Instance.ShowDebugLog)
                            Debug.Log("New SDKSettings instance created.");
                    }
                }

                return _instance;
            }
        }
        
        public static bool IsTokenVerified = false;


        [HideInInspector]
        [HideInFieldGroup("Main Settings")]
        [FieldGroup("Main Settings")]
        public string Token;


        [FieldGroup("Main Settings")] [Header("Main Settings")]
        public bool IsSDKEnabled = false;
    

        [DisableIfSDKDisabled] [FieldGroup("Main Settings")]
        public bool SendStatistics = true;

        [DisableIfSDKDisabled] [FieldGroup("Main Settings")]
        public bool ShowDebugLog = true;

        [FieldGroup("Privacy Settings")] [Header("Privacy Settings")]
        public bool EnableGaidCollection = true;
        
        [DisableIfSDKDisabled]
        private static string PrefixDebugLog = $"<color=#668cff>AudiencelabSDK</color> <color=#666666>=></color>";

        public static string GetColorPrefixLog()
        {
            return PrefixDebugLog;
        }
    }
}