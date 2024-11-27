using UnityEngine;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Newtonsoft.Json;


namespace Geeklab.AudiencelabSDK
{
    public class DeviceInfoHandler : MonoBehaviour
    {
        private static DateTimeOffset sessionStartTime;
        private static TimeSpan sessionDuration;


        static IDeviceModel _deviceModel;
        static IDeviceModel deviceModel {
            get {
                if (_deviceModel == null) {
                    #if UNITY_ANDROID && !UNITY_EDITOR
                    _deviceModel = new AndroidDeviceModel();
                    #elif UNITY_IOS && !UNITY_TVOS && !UNITY_EDITOR
                    _deviceModel = new IOSDeviceModel();
                    #else
                    _deviceModel = new StandardDeviceModel();
                    #endif
                }
                return _deviceModel;
            }
        }


#if UNITY_IOS
        // [DllImport("__Internal")]
        // static extern string GetDeviceModel();

        // [DllImport("__Internal")]
        // static extern string GetInstalledFonts();
        
#endif


        private void Start()
        {
            sessionStartTime = DateTimeOffset.UtcNow;
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
            {
                sessionDuration = DateTimeOffset.UtcNow - sessionStartTime;
// #pragma warning disable CS4014
//                 SendDeviceInfo();
// #pragma warning restore CS4014
            }
            else
            {
                sessionStartTime = DateTimeOffset.UtcNow;
            }
        }

        private void OnApplicationQuit()
        {
            sessionDuration = DateTime.UtcNow - sessionStartTime;
            
// #pragma warning disable CS4014
//             SendDeviceInfo();
// #pragma warning restore CS4014
        }


        public static DeviceInfoModel GetDeviceInfo()
        {
            var deviceGeneration = deviceModel.GetDeviceModel();
            var installedFonts = deviceModel.GetInstalledFonts();

        
            
#if UNITY_IOS && !UNITY_TVOS
            // deviceGeneration = GetDeviceModel();
            // installedFonts = GetInstalledFonts();
#elif UNITY_ANDROID
            using (var javaClass = new AndroidJavaClass("com.Geeklab.plugin.DeviceGeneration"))
            {
                deviceGeneration = javaClass.CallStatic<string>("GetDeviceGeneration");
            }
            using (var javaClass = new AndroidJavaClass("com.Geeklab.plugin.InstalledFonts"))
            {
                installedFonts = javaClass.CallStatic<string>("GetInstalledFonts");
            }
#endif

            int nativeWidth;
            int nativeHeight;

#if UNITY_IOS && !UNITY_EDITOR

    [DllImport("__Internal")]
    static extern IntPtr _GetNativeScreenWidth();

    [DllImport("__Internal")]
    static extern IntPtr _GetNativeScreenHeight();

     try
    {
        string nativeWidthString = Marshal.PtrToStringAnsi(_GetNativeScreenWidth());
        string nativeHeightString = Marshal.PtrToStringAnsi(_GetNativeScreenHeight());

        if (!string.IsNullOrEmpty(nativeWidthString) && !string.IsNullOrEmpty(nativeHeightString) &&
            float.TryParse(nativeWidthString, out float width) && 
            float.TryParse(nativeHeightString, out float height))
        {
            nativeWidth = Mathf.RoundToInt(width);
            nativeHeight = Mathf.RoundToInt(height);
        }
        else
        {
            // Fallback to Screen resolution if parsing fails
            nativeWidth = Screen.currentResolution.width;
            nativeHeight = Screen.currentResolution.height;
            Debug.LogWarning("Failed to get native resolution, using Screen.currentResolution as fallback");
        }
    }
    catch (Exception e)
    {
        // Fallback to Screen resolution if any error occurs
        nativeWidth = Screen.currentResolution.width;
        nativeHeight = Screen.currentResolution.height;
        Debug.LogError($"Error getting native resolution: {e.Message}. Using Screen.currentResolution as fallback");
    }

#elif UNITY_ANDROID && !UNITY_EDITOR
    // Use Android-specific methods for native resolution
    using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
    {
        using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        using (var metrics = new AndroidJavaObject("android.util.DisplayMetrics"))
        {
            activity.Call("getWindowManager").Call<AndroidJavaObject>("getDefaultDisplay").Call("getMetrics", metrics);
            nativeWidth = metrics.Get<int>("widthPixels");
            nativeHeight = metrics.Get<int>("heightPixels");
        }
    }
#else
    // Use Screen.currentResolution as fallback
    nativeWidth = Screen.currentResolution.width;
    nativeHeight = Screen.currentResolution.height;
#endif
            
            var installedFontsArray = installedFonts.Split(',');
            var installedFontsJson = JsonConvert.SerializeObject(installedFontsArray);
            
            
            var resolutionsStr = Screen.resolutions.Select(r => r.width + "x" + r.height).ToArray();
            
            var timeZone = DateTime.UtcNow.ToString("o");


             var systemInfoObject = new {
                IosSystem = SystemInfo.operatingSystem,
                DeviceName = SystemInfo.deviceName,
                DeviceType = SystemInfo.deviceType.ToString(),
                DeviceModel = SystemInfo.deviceModel,
                Resolutions = resolutionsStr,
                Generation = deviceGeneration,
            };

            var gpuContent = new {
                GraphicsDeviceID = SystemInfo.graphicsDeviceID.ToString(),
                GraphicsDeviceVersion = SystemInfo.graphicsDeviceVersion,
            };


            var deviceInfo = new DeviceInfoModel
            {
                Dpi = Screen.dpi,
                Width = Screen.width,
                Height = Screen.height,
                NativeHeight = nativeHeight,
                NativeWidth = nativeWidth,
                LowPower = SystemInfo.batteryLevel < 0.2f,
                Timezone = timeZone,
                OsVersion = SystemInfo.operatingSystem,
                InstalledFonts = installedFontsArray,
                GraphicsDeviceVendor = SystemInfo.graphicsDeviceVendor,
                DeviceName = SystemInfo.deviceName,
                SystemInfoPackage = systemInfoObject,
                GpuContent = gpuContent,
            };

           

            return deviceInfo;
        }
        
        
        public static async Task<bool> SendDeviceInfo()
        {
            if (!SDKSettingsModel.Instance.SendStatistics) 
                return false;

            var taskCompletionSource = new TaskCompletionSource<bool>();

            sessionDuration = DateTimeOffset.UtcNow - sessionStartTime;

            var deviceInfo = GetDeviceInfo();
            var json = JsonUtility.ToJson(deviceInfo);

            WebRequestManager.Instance.SendUserMetricsRequest(json,
                (response) =>
                {
                    if (SDKSettingsModel.Instance.ShowDebugLog)
                        Debug.Log(
                            $"{SDKSettingsModel.GetColorPrefixLog()} {response}");
                    taskCompletionSource.SetResult(true);
                },
                (error) =>
                {
                    Debug.LogError(error);
                    taskCompletionSource.SetResult(false);
                }
            );
            
            return await taskCompletionSource.Task;
        }
    }



    interface IDeviceModel {
        string GetDeviceModel();
        string GetInstalledFonts();
    }
    
    class StandardDeviceModel : IDeviceModel {
        public string GetDeviceModel() {
            return "";
        }

        public string GetInstalledFonts() {
            return "";
        }
    }

#if UNITY_IOS && !UNITY_TVOS
    class IOSDeviceModel : IDeviceModel {
        [DllImport("__Internal")]
        static extern string _GetDeviceModel();

        [DllImport("__Internal")]
        static extern string _GetInstalledFonts();

        public string GetDeviceModel() {
            return _GetDeviceModel();
        }

        public string GetInstalledFonts() {
            return _GetInstalledFonts();
        }
    }
#endif

#if UNITY_ANDROID
    class AndroidDeviceModel : IDeviceModel {
        public string GetDeviceModel() {
            using (var javaClass = new AndroidJavaClass("com.Geeklab.plugin.DeviceGeneration")) {
                return javaClass.CallStatic<string>("GetDeviceGeneration");
            }
        }

        public string GetInstalledFonts() {
            using (var javaClass = new AndroidJavaClass("com.Geeklab.plugin.InstalledFonts")) {
                return javaClass.CallStatic<string>("GetInstalledFonts");
            }
        }
    }
#endif
}