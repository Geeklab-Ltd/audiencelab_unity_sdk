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
        
        private const string AudienceLabSettingsAssetPath = "Assets/Resources/AudienceLabSettings.asset";
        
        private bool missingValues;
        private string tokenInputField = "";
        private bool isRequestInProgress = false;

        private SDKSettingsModel sdkSettings;
        private AudienceLabSettings audienceLabSettings;
        private SerializedObject serializedAudienceLabSettings;
        private bool showGaidSetupInstructions;
        private Vector2 scrollPosition;
        private int currentTab;

        private enum ValidationSeverity
        {
            Ok,
            Warning,
            Info
        }

        [MenuItem("Audiencelab SDK/SDK Settings", false, 0)]
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
                var resourcesPath = "Assets/Resources";
                if (!AssetDatabase.IsValidFolder(resourcesPath))
                {
                    AssetDatabase.CreateFolder("Assets", "Resources");
                }
                
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
            LoadOrCreateAudienceLabSettings();
        }
        
        
        private void OnDisable()
        {
            SaveSDKSettingsModel();
            SaveAudienceLabSettings();
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
                EditorGUI.BeginChangeCheck();
                var tabLabels = new[] { "Main", "Privacy", "Debug" };
                currentTab = GUILayout.Toolbar(currentTab, tabLabels);

                if (audienceLabSettings == null || serializedAudienceLabSettings == null)
                {
                    EditorGUILayout.HelpBox("AudienceLab settings asset not found.", MessageType.Warning);
                    if (GUILayout.Button("Create AudienceLab Settings"))
                    {
                        LoadOrCreateAudienceLabSettings();
                    }
                    return;
                }

                serializedAudienceLabSettings.Update();

                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                if (currentTab == 0)
                {
                    EditorGUILayout.BeginVertical(GUI.skin.box);
                    DrawSDKSettingsModel();
                    EditorGUILayout.EndVertical();

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
                }
                else if (currentTab == 1)
                {
                    EditorGUILayout.BeginVertical(GUI.skin.box);
                    DrawAndroidIdentitySettings();
                    EditorGUILayout.EndVertical();
                }
                else
                {
                    EditorGUILayout.BeginVertical(GUI.skin.box);
                    DrawDebugSettings();
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndScrollView();

                serializedAudienceLabSettings.ApplyModifiedProperties();

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

                if (field.Name == "EnableGaidCollection")
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

        private void LoadOrCreateAudienceLabSettings()
        {
            audienceLabSettings = AssetDatabase.LoadAssetAtPath<AudienceLabSettings>(AudienceLabSettingsAssetPath);
            if (audienceLabSettings == null)
            {
                var resourcesDir = "Assets/Resources";
                if (!AssetDatabase.IsValidFolder(resourcesDir))
                {
                    AssetDatabase.CreateFolder("Assets", "Resources");
                }

                audienceLabSettings = CreateInstance<AudienceLabSettings>();
                AssetDatabase.CreateAsset(audienceLabSettings, AudienceLabSettingsAssetPath);
                AssetDatabase.SaveAssets();
            }

            if (audienceLabSettings != null)
            {
                serializedAudienceLabSettings = new SerializedObject(audienceLabSettings);
            }
        }

        private void DrawPreferredApiNotice()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Preferred SDK API", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Use AudiencelabSDK for event APIs:\n" +
                "- AudiencelabSDK.SendCustomEvent(...)\n" +
                "- AudiencelabSDK.SendAdEvent(...)\n" +
                "- AudiencelabSDK.SendPurchaseEvent(...)\n" +
                "Older SendCustom* methods and AdMetrics/PurchaseMetrics calls still work but are deprecated.",
                MessageType.Info);
        }

        private void DrawDebugSettings()
        {
            EditorGUILayout.LabelField("Debug Overlay", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Overlay is Editor/Development builds only.", MessageType.Info);

            EditorGUILayout.PropertyField(serializedAudienceLabSettings.FindProperty("enableDebugOverlay"),
                new GUIContent("Enable Debug Overlay"));
            EditorGUILayout.PropertyField(serializedAudienceLabSettings.FindProperty("debugOverlayMaxEvents"),
                new GUIContent("Max Events"));
            EditorGUILayout.PropertyField(serializedAudienceLabSettings.FindProperty("showRawIdentifiers"),
                new GUIContent("Show Raw Identifiers"));
            EditorGUILayout.PropertyField(serializedAudienceLabSettings.FindProperty("debugOverlayToggleKey"),
                new GUIContent("Toggle Key"));
        }

        private void DrawAndroidIdentitySettings()
        {
            var autoProp = serializedAudienceLabSettings.FindProperty("enableGaidAutoCollection");
            var appSetAutoProp = serializedAudienceLabSettings.FindProperty("enableAppSetIdAutoCollection");

            EditorGUILayout.LabelField("Android Identity", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(GUI.skin.box);

            // 0 = Auto, 1 = Disabled
            var selectedMode = autoProp.boolValue ? 0 : 1;
            var previousMode = selectedMode;
            var previousAppSet = appSetAutoProp.boolValue;

            selectedMode = GUILayout.Toolbar(selectedMode, new[]
            {
                "Auto GAID",
                "Disabled"
            });

            autoProp.boolValue = selectedMode == 0;

            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Mode", GUILayout.Width(80f));
            DrawGaidModeBadge(selectedMode);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6f);
            EditorGUILayout.PropertyField(appSetAutoProp, new GUIContent("Auto-collect App Set ID",
                "Collects App Set ID via Play Services. Disable to avoid adding Play Services dependencies."));

            EditorGUILayout.Space(6f);
            DrawGaidModeBanner(selectedMode, appSetAutoProp.boolValue);

            // Show validation if Auto GAID is enabled OR if App Set ID auto is enabled (needs Play Services)
            if (selectedMode == 0 || appSetAutoProp.boolValue)
            {
                EditorGUILayout.Space(6f);
                DrawGaidValidation(selectedMode == 0);
            }

            EditorGUILayout.Space(6f);
            DrawGaidSetupInstructions(selectedMode == 0);

            EditorGUILayout.EndVertical();

            // Regenerate dependency files when Android identity settings change
            if (selectedMode != previousMode || appSetAutoProp.boolValue != previousAppSet)
            {
                serializedAudienceLabSettings.ApplyModifiedProperties();
                SaveAudienceLabSettings();
                Editor.AndroidDependencyManager.RegenerateFromCurrentSettings();
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

        private void SaveAudienceLabSettings()
        {
            if (audienceLabSettings == null)
            {
                return;
            }

            EditorUtility.SetDirty(audienceLabSettings);
            AssetDatabase.SaveAssets();
        }

        private void DrawGaidModeBadge(int selectedMode)
        {
            string label;
            Color color;
            if (selectedMode == 0)
            {
                label = "AUTO";
                color = new Color(0.2f, 0.65f, 0.2f);
            }
            else
            {
                label = "DISABLED";
                color = new Color(0.5f, 0.5f, 0.5f);
            }

            var style = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            var rect = GUILayoutUtility.GetRect(new GUIContent(label), style, GUILayout.Width(80f), GUILayout.Height(18f));
            EditorGUI.DrawRect(rect, color);
            GUI.Label(rect, label, style);
        }

        private void DrawGaidModeBanner(int selectedMode, bool appSetAutoEnabled)
        {
            if (selectedMode == 0)
            {
                EditorGUILayout.HelpBox(
                    "GAID auto-collection is enabled. Requires AD_ID permission and Google Play Services dependencies.\n" +
                    "GAID may be unavailable if user deletes Advertising ID or Play Services is missing; events will still send.",
                    MessageType.Info);
            }
            else
            {
                var depsNote = appSetAutoEnabled
                    ? "\n\nNote: App Set ID auto-collection is enabled and requires Play Services dependencies."
                    : "\n\nNo Play Services dependencies required.";
                EditorGUILayout.HelpBox(
                    "GAID auto-collection is disabled. You can optionally provide identifiers manually:\n" +
                    "• AudiencelabSDK.SetAdvertisingId(gaid)\n" +
                    "• AudiencelabSDK.SetAppSetId(appSetId)" +
                    depsNote,
                    MessageType.None);
            }
        }

        private void DrawGaidValidation(bool autoMode)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);

            var depsResult = CheckAndroidIdentityDependencies(autoMode);
            DrawValidationLine(depsResult.severity, depsResult.statusMessage);
            if (!string.IsNullOrEmpty(depsResult.detailMessage))
            {
                EditorGUILayout.HelpBox(depsResult.detailMessage, depsResult.messageType);
            }

            if (IsCustomMainGradleTemplateEnabledByFile())
            {
                EditorGUILayout.HelpBox(
                    "Custom Main Gradle Template is enabled; ensure required dependencies are in mainTemplate.gradle.",
                    MessageType.Info);
            }
        }

        private void DrawValidationLine(ValidationSeverity severity, string label)
        {
            string icon = severity == ValidationSeverity.Ok ? "✅" :
                severity == ValidationSeverity.Warning ? "⚠️" : "i";

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(icon, GUILayout.Width(22f));
            GUILayout.Label(label);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawGaidSetupInstructions(bool autoMode)
        {
            EditorGUILayout.Space(6f);
            showGaidSetupInstructions = EditorGUILayout.Foldout(showGaidSetupInstructions, "Setup instructions (Android)", true);
            if (!showGaidSetupInstructions)
            {
                return;
            }

            var instructions = BuildGaidInstructionsText(autoMode);
            EditorGUILayout.TextArea(instructions, GUILayout.MinHeight(110f));
            if (GUILayout.Button("Copy instructions"))
            {
                EditorGUIUtility.systemCopyBuffer = instructions;
            }
        }

        private static string BuildGaidInstructionsText(bool autoMode)
        {
            if (autoMode)
            {
                return
                    "Auto GAID Mode - Setup is automatic:\n\n" +
                    "1) AD_ID permission is included in the SDK's AndroidManifest.xml\n" +
                    "2) Play Services dependencies are auto-generated at build time\n" +
                    "   • Gradle file: Assets/Plugins/Android/AudienceLabIdentity.gradle\n" +
                    "   • EDM XML file: Assets/Plugins/Android/AudienceLabIdentityDependencies.xml\n" +
                    "3) EDM (External Dependency Manager) users: dependencies are resolved automatically via the XML file\n" +
                    "4) Validate in Development Build using Debug Overlay (check GAID field)\n\n" +
                    "If using a Custom Main Gradle Template (mainTemplate.gradle), add these to the dependencies block:\n" +
                    "   implementation '" + Editor.AndroidDependencyManager.PlayServicesAdsId + "'\n" +
                    "   implementation '" + Editor.AndroidDependencyManager.PlayServicesAppSet + "'\n\n" +
                    "Manual dependency generation: Audiencelab SDK > Android > Regenerate Android Dependencies";
            }

            return
                "Disabled Mode:\n\n" +
                "1) SDK will not auto-collect GAID\n" +
                "2) You can optionally provide identifiers manually:\n" +
                "   • AudiencelabSDK.SetAdvertisingId(gaid)\n" +
                "   • AudiencelabSDK.SetAppSetId(appSetId)\n" +
                "3) No Play Services dependencies added (unless App Set ID auto-collection is enabled)\n" +
                "4) Validate in Development Build using Debug Overlay";
        }

        private static (ValidationSeverity severity, string statusMessage, string detailMessage, MessageType messageType)
            CheckAdIdPermission(bool autoMode)
        {
            var manifestPath = Path.Combine(Application.dataPath, "Plugins", "Android", "AndroidManifest.xml");
            const string permission = "com.google.android.gms.permission.AD_ID";

            if (!File.Exists(manifestPath))
            {
                return (ValidationSeverity.Info,
                    "AD_ID permission check: no custom AndroidManifest.xml",
                    "No custom AndroidManifest.xml found at Assets/Plugins/Android/AndroidManifest.xml. Unity will generate/merge a manifest. Create this file to explicitly control permissions and include AD_ID.",
                    MessageType.Info);
            }

            try
            {
                var manifestText = File.ReadAllText(manifestPath);
                if (manifestText.Contains(permission))
                {
                    return (ValidationSeverity.Ok, "AD_ID permission present", null, MessageType.None);
                }
            }
            catch (Exception)
            {
                return (ValidationSeverity.Info,
                    "AD_ID permission check failed",
                    "Unable to read AndroidManifest.xml. Ensure the file is readable.",
                    MessageType.Info);
            }

            var severity = autoMode ? ValidationSeverity.Warning : ValidationSeverity.Info;
            var messageType = autoMode ? MessageType.Warning : MessageType.Info;
            return (severity,
                "AD_ID permission missing",
                "Add this line to AndroidManifest.xml:\n<uses-permission android:name=\"com.google.android.gms.permission.AD_ID\" />",
                messageType);
        }

        private static (ValidationSeverity severity, string statusMessage, string detailMessage, MessageType messageType)
            CheckAndroidIdentityDependencies(bool autoMode)
        {
            var status = Editor.AndroidDependencyManager.GetResolutionStatus();

            switch (status)
            {
                case Editor.AndroidDependencyManager.DependencyResolutionStatus.NotRequired:
                    return (ValidationSeverity.Ok,
                        "Play Services dependencies not required",
                        "GAID and App Set ID auto-collection are disabled. No Google Play Services dependencies needed.",
                        MessageType.Info);

                case Editor.AndroidDependencyManager.DependencyResolutionStatus.EdmResolved:
                    return (ValidationSeverity.Ok,
                        "Dependencies resolved via EDM",
                        null, MessageType.None);

                case Editor.AndroidDependencyManager.DependencyResolutionStatus.MainTemplateGradle:
                    return (ValidationSeverity.Ok,
                        "Dependencies in mainTemplate.gradle",
                        null, MessageType.None);

                case Editor.AndroidDependencyManager.DependencyResolutionStatus.LooseGradleOnly:
                    return (ValidationSeverity.Warning,
                        "Dependencies may not resolve",
                        "Play Services dependencies are provided via a loose .gradle file, which is not " +
                        "picked up by all Unity/Gradle configurations.\n\n" +
                        "Recommended: Install the External Dependency Manager (EDM) package for " +
                        "reliable dependency resolution, or add the dependencies to your mainTemplate.gradle.",
                        MessageType.Warning);

                case Editor.AndroidDependencyManager.DependencyResolutionStatus.EdmXmlWithoutEdm:
                    return (ValidationSeverity.Warning,
                        "EDM not installed",
                        "An EDM XML dependency file is present but the External Dependency Manager (EDM) " +
                        "package is not installed. Dependencies will not be resolved.\n\n" +
                        "Install EDM from Google, or add the dependencies to your mainTemplate.gradle:\n" +
                        "  implementation '" + Editor.AndroidDependencyManager.PlayServicesAdsId + "'\n" +
                        "  implementation '" + Editor.AndroidDependencyManager.PlayServicesAppSet + "'",
                        MessageType.Warning);

                case Editor.AndroidDependencyManager.DependencyResolutionStatus.NoneDetected:
                    return (ValidationSeverity.Warning,
                        "No dependency source detected",
                        "Auto GAID / App Set ID requires Google Play Services, but no reliable dependency " +
                        "source was found. GAID and App Set ID will be null at runtime.\n\n" +
                        "To fix, do ONE of the following:\n" +
                        "• Install External Dependency Manager (EDM) — dependencies resolve automatically\n" +
                        "• Enable Custom Main Gradle Template and add dependencies manually\n" +
                        "• Use 'Audiencelab SDK > Android > Regenerate Android Dependencies'\n\n" +
                        "Dependencies will also be auto-generated at build time, but the loose .gradle file " +
                        "may not be picked up by all project configurations.",
                        MessageType.Warning);

                default:
                    return (ValidationSeverity.Info,
                        "Dependencies auto-generated at build",
                        "Use 'Audiencelab SDK > Android > Regenerate Android Dependencies' to generate now.",
                        MessageType.Info);
            }
        }

        private static bool IsCustomMainGradleTemplateEnabledByFile()
        {
            var path = Path.Combine(Application.dataPath, "Plugins", "Android", "mainTemplate.gradle");
            return File.Exists(path);
        }
    }
}