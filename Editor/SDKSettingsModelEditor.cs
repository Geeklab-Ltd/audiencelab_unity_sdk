#if UNITY_EDITOR
using UnityEditor;
#endif

using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Geeklab.AudiencelabSDK
{
    [CustomEditor(typeof(SDKSettingsModel))]
    public class SDKSettingsModelEditor : Editor
    {
        private static SDKSettingsModelEditor _instance;

        private SDKSettingsModelEditor()
        {
        }
        
        
        public static SDKSettingsModelEditor GetInstance()
        {
            if (_instance == null)
            {
                _instance = new SDKSettingsModelEditor();
            }

            return _instance;
        }

        private void OnEnable()
        {
            var sdkSettings = (SDKSettingsModel)target;
            if (!string.IsNullOrEmpty((SDKSettingsModel.Instance.Token)))
            {
                sdkSettings.Token = SDKSettingsModel.Instance.Token;
                SDKTokenModel.Instance.Token = sdkSettings.Token;
                SDKTokenModel.Instance.IsTokenVerified = true;
            }
        }

        public override void OnInspectorGUI()
        {
            var sdkSettings = (SDKSettingsModel)target;

            if (!SDKTokenModel.Instance.IsTokenVerified)
            {
                EditorGUILayout.HelpBox("Token not verified. Please verify your token in the SDK Settings window.", MessageType.Warning);

                sdkSettings.Token = EditorGUILayout.TextField("Token", sdkSettings.Token);

                if (GUILayout.Button("Verify Token", GUILayout.Height(30), GUILayout.ExpandWidth(true)))
                {
                    VerifySDKToken(sdkSettings);
                    Repaint();
                    GUI.FocusControl(null);
                }
            }
            else
            {
                if (GUILayout.Button("Clear Token", GUILayout.Height(30), GUILayout.ExpandWidth(true)))
                {
                    sdkSettings.Token = "";
                    SDKTokenModel.Instance.IsTokenVerified = false;
                    SDKTokenModel.Instance.Token = "";
                    SDKSettingsModel.Instance.Token = "";
                    EditorUtility.SetDirty(sdkSettings);
                    AssetDatabase.SaveAssets();
                    GUI.FocusControl(null);
                    Repaint();
                }
                
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField("Token", sdkSettings.Token);
                EditorGUI.EndDisabledGroup();
                
                DrawDefaultInspector();

                EditorGUILayout.Separator();

                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.EndVertical();
            }
        
            if (GUI.changed)
            {
                EditorUtility.SetDirty(sdkSettings);
                AssetDatabase.SaveAssets();
            }
        }

        private static async void VerifySDKToken(SDKSettingsModel sdkSettings)
        {
            if (string.IsNullOrEmpty(sdkSettings.Token))
            {
                return;
            }
    
            var tcs = new TaskCompletionSource<bool>();
            var www = UnityWebRequest.Get(ApiEndpointsModel.VERIFY_API_KEY);
            www.SetRequestHeader("Authorization", "Bearer " + sdkSettings.Token);

            www.SendWebRequest().completed += _ => tcs.SetResult(true);

            await tcs.Task;

#if UNITY_2020_2_OR_NEWER
            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
#else
            if (www.isNetworkError || www.isHttpError)
#endif
            {
                EditorUtility.DisplayDialog("Error", "Invalid SDK token. Please try again.", "OK");
                SDKTokenModel.Instance.IsTokenVerified = false;
                SDKTokenModel.Instance.Token = "";
            }
            else
            {
                SDKTokenModel.Instance.IsTokenVerified = true;
                SDKTokenModel.Instance.Token = sdkSettings.Token;
                SDKSettingsModel.Instance.Token = sdkSettings.Token;
                EditorUtility.SetDirty(sdkSettings);
                AssetDatabase.SaveAssets();
            }
        }


    }
}