using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Geeklab.AudiencelabSDK.Editor
{
    /// <summary>
    /// Manages Android dependencies based on SDK settings.
    /// Generates two dependency files so that projects using either approach are covered:
    ///   1. AudienceLabIdentity.gradle          — picked up by Unity's default Gradle build
    ///   2. AudienceLabIdentityDependencies.xml  — picked up by EDM (External Dependency Manager / Play Services Resolver)
    /// Both files are regenerated (or removed) before each Android build and whenever the
    /// user triggers "Regenerate Android Dependencies" from the menu.
    /// </summary>
    public class AndroidDependencyManager : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private const string GradleFileName = "AudienceLabIdentity.gradle";
        private const string EdmXmlFileName = "AudienceLabIdentityDependencies.xml";
        
        private const string PluginsAndroidDir = "Assets/Plugins/Android/";
        
        private const string GeneratedGradlePath = PluginsAndroidDir + GradleFileName;
        private const string GeneratedGradleMetaPath = GeneratedGradlePath + ".meta";
        
        private const string GeneratedEdmXmlPath = PluginsAndroidDir + EdmXmlFileName;
        private const string GeneratedEdmXmlMetaPath = GeneratedEdmXmlPath + ".meta";

        internal const string PlayServicesAdsId = "com.google.android.gms:play-services-ads-identifier:18.0.1";
        internal const string PlayServicesAppSet = "com.google.android.gms:play-services-appset:16.0.2";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android)
            {
                return;
            }

            RegenerateFromCurrentSettings();
            ValidateDependencyResolution();
        }

        public void OnPostprocessBuild(BuildReport report)
        {
        }

        // -----------------------------------------------------------------
        //  Public helpers (used by menu items and SDKSettingsEditor)
        // -----------------------------------------------------------------

        public static void RegenerateFromCurrentSettings()
        {
            var settings = Resources.Load<AudienceLabSettings>("AudienceLabSettings");
            bool needsPlayServices = ShouldIncludePlayServicesDependencies(settings);

            EnsurePluginsAndroidFolderExists();

            if (needsPlayServices)
            {
                GenerateGradleFile();
                GenerateEdmXmlFile();
                Debug.Log("[AudienceLab] Generated Android dependency files (Gradle + EDM XML) with Play Services dependencies.");
            }
            else
            {
                RemoveGradleFileIfExists();
                RemoveEdmXmlFileIfExists();
                Debug.Log("[AudienceLab] Play Services dependencies not required — removed dependency files.");
            }

            AssetDatabase.Refresh();
        }

        public static bool CurrentSettingsNeedPlayServices()
        {
            var settings = Resources.Load<AudienceLabSettings>("AudienceLabSettings");
            return ShouldIncludePlayServicesDependencies(settings);
        }

        // -----------------------------------------------------------------
        //  Dependency resolution detection
        // -----------------------------------------------------------------

        /// <summary>
        /// Checks whether EDM (External Dependency Manager / Play Services Resolver)
        /// is installed in the project by looking for its assemblies.
        /// </summary>
        public static bool IsEdmInstalled()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Any(a =>
                {
                    var name = a.GetName().Name;
                    return name.Contains("Google.JarResolver") ||
                           name.Contains("Google.VersionHandler") ||
                           name.Contains("Google.IOSResolver");
                });
        }

        /// <summary>
        /// Returns the best dependency resolution method available in the project.
        /// </summary>
        public static DependencyResolutionStatus GetResolutionStatus()
        {
            var settings = Resources.Load<AudienceLabSettings>("AudienceLabSettings");
            bool needsPlayServices = ShouldIncludePlayServicesDependencies(settings);

            if (!needsPlayServices)
                return DependencyResolutionStatus.NotRequired;

            var pluginsAndroidDir = Path.Combine(Application.dataPath, "Plugins", "Android");

            // Best: EDM installed + XML file present
            bool hasEdmXml = File.Exists(Path.Combine(pluginsAndroidDir, EdmXmlFileName));
            if (IsEdmInstalled() && hasEdmXml)
                return DependencyResolutionStatus.EdmResolved;

            // Good: mainTemplate.gradle with deps explicitly added
            var mainTemplatePath = Path.Combine(pluginsAndroidDir, "mainTemplate.gradle");
            if (File.Exists(mainTemplatePath))
            {
                try
                {
                    var text = File.ReadAllText(mainTemplatePath);
                    if (text.Contains("play-services-ads-identifier") && text.Contains("play-services-appset"))
                        return DependencyResolutionStatus.MainTemplateGradle;
                }
                catch (Exception) { }
            }

            // Unreliable: only the loose .gradle file (may not be picked up)
            bool hasGradle = File.Exists(Path.Combine(pluginsAndroidDir, GradleFileName));
            if (hasGradle)
                return DependencyResolutionStatus.LooseGradleOnly;

            // EDM XML exists but EDM itself isn't installed
            if (hasEdmXml)
                return DependencyResolutionStatus.EdmXmlWithoutEdm;

            return DependencyResolutionStatus.NoneDetected;
        }

        public enum DependencyResolutionStatus
        {
            NotRequired,
            EdmResolved,
            MainTemplateGradle,
            LooseGradleOnly,
            EdmXmlWithoutEdm,
            NoneDetected
        }

        // -----------------------------------------------------------------
        //  Build-time validation
        // -----------------------------------------------------------------

        private void ValidateDependencyResolution()
        {
            var status = GetResolutionStatus();

            switch (status)
            {
                case DependencyResolutionStatus.NotRequired:
                case DependencyResolutionStatus.EdmResolved:
                case DependencyResolutionStatus.MainTemplateGradle:
                    return;

                case DependencyResolutionStatus.LooseGradleOnly:
                    Debug.LogWarning(
                        "[AudienceLab] Play Services dependencies are provided via a loose .gradle file only. " +
                        "This may not be picked up by all Unity/Gradle configurations. " +
                        "For reliable dependency resolution, install the External Dependency Manager (EDM) package " +
                        "or add the dependencies to your mainTemplate.gradle.");
                    return;

                case DependencyResolutionStatus.EdmXmlWithoutEdm:
                    Debug.LogWarning(
                        "[AudienceLab] Auto GAID / App Set ID collection is enabled and an EDM XML file is present, " +
                        "but the External Dependency Manager (EDM) package is not installed in this project. " +
                        "Without EDM, the Play Services dependencies will not be resolved and " +
                        "GAID / App Set ID will be null at runtime. " +
                        "To fix: install EDM, add dependencies to mainTemplate.gradle, " +
                        "or disable Auto GAID in Audiencelab SDK > SDK Settings.");
                    return;

                case DependencyResolutionStatus.NoneDetected:
                    Debug.LogWarning(
                        "[AudienceLab] Auto GAID / App Set ID collection is enabled, but no reliable dependency source was detected. " +
                        "Without the Google Play Services libraries, GAID and App Set ID will be null at runtime. " +
                        "To fix: install EDM, add dependencies to mainTemplate.gradle " +
                        "(implementation '" + PlayServicesAdsId + "' and implementation '" + PlayServicesAppSet + "'), " +
                        "or disable Auto GAID in Audiencelab SDK > SDK Settings.");
                    return;
            }
        }

        // -----------------------------------------------------------------
        //  Settings evaluation
        // -----------------------------------------------------------------

        private static bool ShouldIncludePlayServicesDependencies(AudienceLabSettings settings)
        {
            if (settings == null)
                return true;

            return settings.enableGaidAutoCollection || settings.enableAppSetIdAutoCollection;
        }

        // -----------------------------------------------------------------
        //  Directory helpers
        // -----------------------------------------------------------------

        private static void EnsurePluginsAndroidFolderExists()
        {
            var path = Path.Combine(Application.dataPath, "Plugins", "Android");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        // -----------------------------------------------------------------
        //  Gradle file
        // -----------------------------------------------------------------

        private static void GenerateGradleFile()
        {
            var content = @"// Generated by AudienceLab SDK - do not edit manually
// This file is regenerated based on SDK settings before each Android build
dependencies {
    implementation '" + PlayServicesAdsId + @"'
    implementation '" + PlayServicesAppSet + @"'
}
";
            var fullPath = Path.Combine(Application.dataPath, "Plugins", "Android", GradleFileName);
            File.WriteAllText(fullPath, content);
        }

        private static void RemoveGradleFileIfExists()
        {
            RemoveFileAndMeta(
                Path.Combine(Application.dataPath, "Plugins", "Android", GradleFileName));
        }

        // -----------------------------------------------------------------
        //  EDM / Play Services Resolver XML file
        // -----------------------------------------------------------------

        private static void GenerateEdmXmlFile()
        {
            var content = @"<?xml version=""1.0"" encoding=""utf-8""?>
<!--
  Generated by AudienceLab SDK - do not edit manually.
  This file is regenerated based on SDK settings.
  It is consumed by the External Dependency Manager (EDM / Play Services Resolver)
  to resolve Google Play Services dependencies for Android builds.
-->
<dependencies>
  <androidPackages>
    <androidPackage spec=""" + PlayServicesAdsId + @""">
      <repositories>
        <repository>https://maven.google.com</repository>
      </repositories>
    </androidPackage>
    <androidPackage spec=""" + PlayServicesAppSet + @""">
      <repositories>
        <repository>https://maven.google.com</repository>
      </repositories>
    </androidPackage>
  </androidPackages>
</dependencies>
";
            var fullPath = Path.Combine(Application.dataPath, "Plugins", "Android", EdmXmlFileName);
            File.WriteAllText(fullPath, content);
        }

        private static void RemoveEdmXmlFileIfExists()
        {
            RemoveFileAndMeta(
                Path.Combine(Application.dataPath, "Plugins", "Android", EdmXmlFileName));
        }

        // -----------------------------------------------------------------
        //  Shared helpers
        // -----------------------------------------------------------------

        private static void RemoveFileAndMeta(string fullPath)
        {
            var metaPath = fullPath + ".meta";

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }
        }

        // -----------------------------------------------------------------
        //  Menu items
        // -----------------------------------------------------------------

        [MenuItem("Audiencelab SDK/Android/Regenerate Android Dependencies", false, 100)]
        public static void RegenerateAndroidDependencies()
        {
            RegenerateFromCurrentSettings();
        }

        [MenuItem("Audiencelab SDK/Android/Check Android Dependencies", false, 101)]
        public static void CheckAndroidDependencies()
        {
            var settings = Resources.Load<AudienceLabSettings>("AudienceLabSettings");
            var status = GetResolutionStatus();
            var gradleExists = File.Exists(Path.Combine(Application.dataPath, "Plugins", "Android", GradleFileName));
            var edmXmlExists = File.Exists(Path.Combine(Application.dataPath, "Plugins", "Android", EdmXmlFileName));

            string gaidMode = "Unknown";
            bool needsDeps = true;

            if (settings != null)
            {
                if (settings.enableGaidAutoCollection)
                    gaidMode = "Auto GAID";
                else if (settings.enableGaidManualMode)
                    gaidMode = "Manual GAID";
                else
                    gaidMode = "No GAID";

                needsDeps = settings.enableGaidAutoCollection || settings.enableAppSetIdAutoCollection;
            }

            Debug.Log($"[AudienceLab] Android Dependency Status:\n" +
                      $"  - GAID Mode: {gaidMode}\n" +
                      $"  - App Set ID Auto: {(settings?.enableAppSetIdAutoCollection ?? true)}\n" +
                      $"  - Play Services Required: {needsDeps}\n" +
                      $"  - Gradle File Present: {gradleExists}\n" +
                      $"  - EDM XML File Present: {edmXmlExists}\n" +
                      $"  - EDM Installed: {IsEdmInstalled()}\n" +
                      $"  - Resolution Status: {status}");
        }
    }
}
