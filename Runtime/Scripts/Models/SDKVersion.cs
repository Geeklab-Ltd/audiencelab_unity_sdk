using UnityEngine;

namespace Geeklab.AudiencelabSDK
{
    /// <summary>
    /// Centralized SDK version information
    /// </summary>
    public static class SDKVersion
    {
        /// <summary>
        /// Current SDK version - should match package.json version
        /// </summary>
        public const string VERSION = "1.1.0";
        
        /// <summary>
        /// SDK type identifier
        /// </summary>
        public const string SDK_TYPE = "Unity";
        
        /// <summary>
        /// Gets the app version from Unity's Application.version, with error handling
        /// </summary>
        public static string AppVersion
        {
            get
            {
                try
                {
                    return Application.version;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[SDKVersion] Failed to get Application.version: {ex.Message}");
                    return "unknown";
                }
            }
        }

        /// <summary>
        /// Gets the app bundle version (iOS) or version code (Android), with error handling
        /// </summary>
        public static string UnityVersion
        {
            get
            {
                try
                {
                    return Application.unityVersion;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[SDKVersion] Failed to get Application.unityVersion: {ex.Message}");
                    return "unknown";
                }
            }
        }
        
        /// <summary>
        /// Full SDK identifier combining type and version
        /// </summary>
        public static string FullIdentifier => $"{SDK_TYPE}-{VERSION}";
        
        /// <summary>
        /// Complete app and SDK version information
        /// </summary>
        public static string CompleteVersionInfo => $"App:{AppVersion} SDK:{VERSION}";
    }
} 