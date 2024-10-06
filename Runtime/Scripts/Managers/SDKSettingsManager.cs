using UnityEngine;
using System.Reflection;


namespace Geeklab.AudiencelabSDK
{
#if UNITY_EDITOR
    using UnityEditor;

    [InitializeOnLoad]
#endif
    public class SDKSettingsManager
    {
        public static bool MissingValues { get; private set; }

#if UNITY_EDITOR
        static SDKSettingsManager()
        {
            EditorApplication.playModeStateChanged += LogPlayModeState;

            var sdkInfoModelProperties =
                typeof(SDKSettingsModel).GetProperties(BindingFlags.Public | BindingFlags.Static);
            MissingValues = false;
            foreach (var property in sdkInfoModelProperties)
            {
                if (!PlayerPrefs.HasKey(property.Name) || string.IsNullOrEmpty(PlayerPrefs.GetString(property.Name)) ||
                    PlayerPrefs.GetString(property.Name) == "0")
                {
                    MissingValues = true;
                    break;
                }
            }
        }


        private static void LogPlayModeState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
            {
                CheckSDKSettingsModel();
            }
        }
#endif


        public static void CheckSDKSettingsModel()
        {
            var sdkInfoModelProperties =
                typeof(SDKSettingsModel).GetProperties(BindingFlags.Public | BindingFlags.Static);
            foreach (var property in sdkInfoModelProperties)
            {
                if (!PlayerPrefs.HasKey(property.Name))
                {
                    if (property.PropertyType == typeof(string))
                    {
                        property.SetValue(null, property.GetValue(""));
                    }
                    else if (property.PropertyType == typeof(bool))
                    {
                        property.SetValue(null, (bool)property.GetValue(true));
                    }

                    PlayerPrefs.SetString(property.Name, property.GetValue(null).ToString());
                }
                else
                {
                    if (property.PropertyType == typeof(string))
                    {
                        property.SetValue(null, PlayerPrefs.GetString(property.Name));
                    }
                    else if (property.PropertyType == typeof(bool))
                    {
                        property.SetValue(null, PlayerPrefs.GetString(property.Name) == "True");
                    }
                }
            }
        }
    }
}