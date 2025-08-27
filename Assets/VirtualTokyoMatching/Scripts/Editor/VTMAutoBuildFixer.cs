using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace VirtualTokyoMatching.Editor
{
    /// <summary>
    /// Automatically applies scene fixes right before a build.
    /// </summary>
    public class VTMAutoBuildFixer : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log("[VTM AutoFixer] Applying VRChat fixes before build…");
            try
            {
                VTMSceneBuilder.ApplyAllVRChatFixes();
                Debug.Log("[VTM AutoFixer] VRChat fixes applied successfully.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[VTM AutoFixer] Failed to apply fixes: {e.Message}");
                // Do **not** throw; let the build continue.
            }
        }
    }

    /// <summary>
    /// Menu items for manual world-build helpers.
    /// </summary>
    public static class VTMBuildCommands
    {
        [MenuItem("VTM/Build World for VRChat")]
        public static void BuildWorldForVRChat()
        {
            Debug.Log("[VTM Build] Starting world build prep…");
            try
            {
                VTMSceneBuilder.ApplyAllVRChatFixes();
                VTMVRChatValidator.ValidateVRChatFixes();
                VTMVRChatValidator.GeneratePerformanceReport();
                UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

                Debug.Log("[VTM Build] Prep complete. Next steps:");
                Debug.Log("1. Import VRChat SDK3 Worlds");
                Debug.Log("2. Add VRC_SceneDescriptor to your VRCWorld object");
                Debug.Log("3. Configure world settings");
                Debug.Log("4. Use VRChat SDK Control Panel to build & upload");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[VTM Build] Prep failed: {e.Message}");
            }
        }

        [MenuItem("VTM/Quick Fix Scene")]
        public static void QuickFixScene()
        {
            Debug.Log("[VTM Quick Fix] Running quick fixes…");

            VTMSceneBuilder.FixCanvasToWorldSpace();
            VTMSceneBuilder.FixFloorMaterials();

            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            Debug.Log("[VTM Quick Fix] Done and scene saved!");
        }
    }
}
