using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;


namespace Geeklab.AudiencelabSDK
{
    public class SDKSettingsEditor : EditorWindow
    {
        private static Vector2 minWindowSize = new Vector2(250, 400);
        
        private bool missingValues;
        private string tokenInputField = "";
        private bool isRequestInProgress = false;

        private SDKSettingsModel sdkSettings;

        [MenuItem("AudiencelabSDK/SDK Settings")]
        public static void ShowWindow()
        {
            GetWindow<SDKSettingsEditor>("SDK Settings").minSize = minWindowSize;
            ManifestModifier();
        }

        private void OnEnable()
        {
            var guids = AssetDatabase.FindAssets("t:SDKSettingsModel");
            if (guids.Length == 0)
            {
#if UNITY_EDITOR
                var instance = CreateInstance<SDKSettingsModel>();
                AssetDatabase.CreateAsset(instance, "Assets/Resources/SDKSettings.asset");
                AssetDatabase.SaveAssets();
                guids = AssetDatabase.FindAssets("t:SDKSettingsModel");
#else
            Debug.LogError("Could not find SDKSettings asset!");
            return;
#endif
                
                if (!string.IsNullOrEmpty(SDKSettingsModel.Instance.Token))
                {
                    sdkSettings.Token = SDKSettingsModel.Instance.Token;
                    SDKTokenModel.Instance.Token = sdkSettings.Token;
                    SDKTokenModel.Instance.IsTokenVerified = !string.IsNullOrEmpty(sdkSettings.Token);
                }
            }
            
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                sdkSettings = AssetDatabase.LoadAssetAtPath<SDKSettingsModel>(path);
                if (sdkSettings != null && sdkSettings.Token != null)
                {
                    tokenInputField = sdkSettings.Token;
                    SDKTokenModel.Instance.IsTokenVerified = !string.IsNullOrEmpty(sdkSettings.Token);
                }
            }
            else
            {
                Debug.LogError("GUIDs array is empty.");
            }

            missingValues = false;
        }
        
        
        private void OnDisable()
        {
            SaveSDKSettingsModel();
        }
        
        
        private static void ManifestModifier()
        {
            var manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");

            if (!File.Exists(manifestPath))
            {
                Debug.LogError("Could not find manifest.json");
                return;
            }

            var manifestContent = File.ReadAllText(manifestPath);
            var manifestJson = JObject.Parse(manifestContent);

            if (manifestJson["testables"] == null)
            {
                manifestJson["testables"] = new JArray();
            }

            var testables = (JArray)manifestJson["testables"];
            if (!testables.Any(t => t.Value<string>() == "com.geeklab.audiencelab-sdk"))
            {
                testables.Add("com.geeklab.audiencelab-sdk");

                File.WriteAllText(manifestPath, manifestJson.ToString());
                Debug.Log("manifest.json was modified");
            }
        }

        

        private void OnGUI()
        {
            if (missingValues)
            {
                EditorGUILayout.HelpBox("Not all fields are filled in.", MessageType.Warning);
            }

            if (!SDKTokenModel.Instance.IsTokenVerified)
            {
                GUILayout.Label("Enter your SDK token:", EditorStyles.boldLabel);
                tokenInputField = EditorGUILayout.TextField("SDK Token", tokenInputField);
                EditorGUI.BeginDisabledGroup(isRequestInProgress);
                if (GUILayout.Button("Verify Geeklab API Token", GUILayout.Height(30)))
                { 
                    VerifySDKToken(tokenInputField);
                }
                EditorGUI.EndDisabledGroup();
                
                if (isRequestInProgress)
                {
                    GUILayout.Label("➔ Verification is in progress! This may take up to a minute. Please wait...", EditorStyles.wordWrappedMiniLabel);
                }
            }
            else
            {
                if (GUILayout.Button("Clear SDK Token", GUILayout.Height(30)))
                {
                    sdkSettings.Token = "";
                    tokenInputField = "";
                    SDKTokenModel.Instance.IsTokenVerified = false;
                    SDKTokenModel.Instance.Token = "";
                    SDKSettingsModel.Instance.Token = "";
                    SaveSDKSettingsModel();
                    Repaint();
                    GUI.FocusControl(null);
                }

                
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.BeginVertical(GUI.skin.box);
                DrawSDKSettingsModel();
                EditorGUILayout.EndVertical();

                EditorGUILayout.Separator();

                var isSDKDisabled = sdkSettings != null && !sdkSettings.IsSDKEnabled;

                EditorGUI.BeginDisabledGroup(
                    isSDKDisabled);
                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.EndVertical();
                EditorGUI.EndDisabledGroup();

                if (EditorGUI.EndChangeCheck()) 
                {
                    missingValues = false;
                }
            }
        }
        
        
        private void VerifySDKToken(string token)
        {
            isRequestInProgress = true;
            var www = UnityWebRequest.Get(ApiEndpointsModel.VERIFY_API_KEY);
            www.SetRequestHeader("geeklab-api-key", token);
            www.SendWebRequest().completed += OnRequestCompleted;
        }
        
        
        private void OnRequestCompleted(AsyncOperation operation)
        {
            var wwwOp = operation as UnityWebRequestAsyncOperation;
            var www = wwwOp.webRequest;

#if UNITY_2020_2_OR_NEWER
            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
#else
            if (www.isNetworkError || www.isHttpError)
#endif
            {   
                Debug.Log(www.downloadHandler.text);
                EditorUtility.DisplayDialog("Error", "Invalid SDK token. Please try again. Please...", "OK");
                SDKTokenModel.Instance.IsTokenVerified = false; 
                SDKTokenModel.Instance.Token = "";
            }
            else
            {
                SDKTokenModel.Instance.IsTokenVerified = true;
                SDKTokenModel.Instance.Token = tokenInputField;
                sdkSettings.Token = tokenInputField;
                SaveSDKSettingsModel();
                SDKSettingsModel.Instance.Token = tokenInputField;
            }

            isRequestInProgress = false;
            Repaint();
        }
        

        private void DrawSDKSettingsModel()
        {
            if (sdkSettings == null)
            {
                Debug.LogError("sdkSettings is null");
                return;
            }
            
            var sdkSettingsModelFields = typeof(SDKSettingsModel).GetFields(BindingFlags.Public | BindingFlags.Instance);
            string currentGroup = null;
            foreach (var field in sdkSettingsModelFields)
            {
                var groupAttr = (FieldGroup)Attribute.GetCustomAttribute(field, typeof(FieldGroup));
                var hideAttr = (HideInFieldGroupAttribute)Attribute.GetCustomAttribute(field, typeof(HideInFieldGroupAttribute));
                if (hideAttr != null && hideAttr.GroupName == groupAttr.GroupName)
                {
                    continue;
                }
                
                if (groupAttr != null && groupAttr.GroupName != currentGroup)
                {
                    currentGroup = groupAttr.GroupName;
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField(currentGroup, EditorStyles.boldLabel);
                }

                var disableIfSDKDisabled = Attribute.IsDefined(field, typeof(DisableIfSDKDisabled)) &&
                                           !sdkSettings.IsSDKEnabled;
          
                var shouldDisable = disableIfSDKDisabled;

                EditorGUI.BeginDisabledGroup(shouldDisable);
                if (field.FieldType == typeof(string))
                {
                    var value = EditorGUILayout.TextField(field.Name, (string)field.GetValue(sdkSettings));
                    field.SetValue(sdkSettings, value);
                }
                else if (field.FieldType == typeof(bool))
                {
                    var value = EditorGUILayout.Toggle(field.Name, (bool)field.GetValue(sdkSettings));
                    field.SetValue(sdkSettings, value);
                }

                EditorGUI.EndDisabledGroup();
            }
        }

        private void SaveSDKSettingsModel()
        {
            if (sdkSettings != null)
            {
                EditorUtility.SetDirty(sdkSettings);
                AssetDatabase.SaveAssets();
            }
            else
            {
                Debug.LogError("sdkSettings is null.");
            }
        }
    }
}