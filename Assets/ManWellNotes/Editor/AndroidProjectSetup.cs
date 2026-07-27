using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace ManWellNotesEditor
{
    // Keeps the Android settings in one documented, repeatable place. If the
    // project settings are ever reset, run the menu command below to restore them.
    public static class AndroidProjectSetup
    {
        private const string ApplicationId = "com.manwell.notes";

        [MenuItem("Tools/Man-Well Notes/Configure Project for Android")]
        public static void Configure()
        {
            NamedBuildTarget android = NamedBuildTarget.Android;

            PlayerSettings.companyName = "Man-Well";
            PlayerSettings.productName = "Man-Well Notes";
            PlayerSettings.SetApplicationIdentifier(android, ApplicationId);

            // Version name is user-facing. Version code must increase for every
            // release submitted to an Android app store.
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.Android.bundleVersionCode = 1;

            // Android 8 is a practical baseline for this small prototype. "Auto"
            // targets the newest API installed with this Unity Editor.
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            // Google Play expects 64-bit support. IL2CPP produces native ARM64 code.
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // The Android interface uses one full-screen page at a time.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.useAnimatedAutorotation = false;

            EditorUserBuildSettings.buildAppBundle = false; // Build an APK while prototyping.
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;

            // Switching the active target makes the Build Profiles window and asset
            // import pipeline use Android immediately, not merely on the next build.
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Android,
                    BuildTarget.Android);

                if (!switched)
                    throw new System.InvalidOperationException("Unity could not switch the active build target to Android.");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Configured Android: {ApplicationId}, ARM64, IL2CPP, portrait, minimum API 26.");
        }
    }
}
