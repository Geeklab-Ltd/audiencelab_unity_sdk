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


        private void Start()
        {
            sessionStartTime = DateTimeOffset.UtcNow;
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
            {
                sessionDuration = DateTimeOffset.UtcNow - sessionStartTime;

            }
            else
            {
                sessionStartTime = DateTimeOffset.UtcNow;
            }
        }

        private void OnApplicationQuit()
        {
            sessionDuration = DateTime.UtcNow - sessionStartTime;
            

        }


        public static DeviceInfoModel GetDeviceInfo()
        {
            var deviceGeneration = deviceModel.GetDeviceModel();
            var installedFonts = deviceModel.GetInstalledFonts();

            int nativeWidth;
            int nativeHeight;

#if UNITY_IOS && !UNITY_TVOS && !UNITY_EDITOR
            IOSDeviceModel.GetNativeResolution(out nativeWidth, out nativeHeight);
#else
            // Use Screen dimensions as fallback for Editor, Android, and other platforms
            nativeWidth = Screen.width;
            nativeHeight = Screen.height;
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

#if UNITY_IOS && !UNITY_TVOS && !UNITY_EDITOR
    class IOSDeviceModel : IDeviceModel {
        [DllImport("__Internal")]
        static extern string _GetDeviceModel();

        [DllImport("__Internal")]
        static extern string _GetInstalledFonts();

        [DllImport("__Internal")]
        static extern IntPtr _GetNativeScreenWidth();

        [DllImport("__Internal")]
        static extern IntPtr _GetNativeScreenHeight();

        public string GetDeviceModel() {
            return _GetDeviceModel();
        }

        public string GetInstalledFonts() {
            return _GetInstalledFonts();
        }

        public static void GetNativeResolution(out int width, out int height) {
            try {
                string nativeWidthString = Marshal.PtrToStringAnsi(_GetNativeScreenWidth());
                string nativeHeightString = Marshal.PtrToStringAnsi(_GetNativeScreenHeight());

                if (!string.IsNullOrEmpty(nativeWidthString) && !string.IsNullOrEmpty(nativeHeightString) &&
                    float.TryParse(nativeWidthString, out float w) && 
                    float.TryParse(nativeHeightString, out float h))
                {
                    width = Mathf.RoundToInt(w);
                    height = Mathf.RoundToInt(h);
                    return;
                }
            }
            catch (Exception e) {
                Debug.LogError($"Error getting native resolution: {e.Message}. Using Screen.currentResolution as fallback");
            }
            width = Screen.currentResolution.width;
            height = Screen.currentResolution.height;
        }
    }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
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