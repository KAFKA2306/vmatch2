using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace VirtualTokyoMatching.Editor
{
    /// <summary>
    /// Automatically applies VRChat fixes during build process
    /// </summary>
    public class VTMAutoBuildFixer : IPreprocessBuildWithReport
    {
        public int callbackOrder { get { return 0; } }
        
        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log("[VTM AutoFixer] Applying VRChat fixes before build...");
            
            try
            {
                // Apply all VRChat fixes
                VTMSceneBuilder.ApplyAllVRChatFixes();
                
                Debug.Log("[VTM AutoFixer] VRChat fixes applied successfully before build");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[VTM AutoFixer] Failed to apply VRChat fixes: {e.Message}");
                // Don't fail the build, just warn
            }
        }
    }
    
    /// <summary>
    /// Manual build commands for VRChat world
    /// </summary>
    public class VTMBuildCommands
    {
        [MenuItem("VTM/Build World for VRChat")]
        public static void BuildWorldForVRChat()
        {
            Debug.Log("[VTM Build] Starting VRChat world build process...");
            
            try
            {
                // 1. Apply all VRChat fixes
                VTMSceneBuilder.ApplyAllVRChatFixes();
                
                // 2. Validate the fixes
                VTMVRChatValidator.ValidateVRChatFixes();
                
                // 3. Generate performance report
                VTMVRChatValidator.GeneratePerformanceReport();
                
                // 4. Save scene
                UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
                
                Debug.Log("[VTM Build] VRChat world build preparation completed!");
                Debug.Log("[VTM Build] Next steps:");\n                Debug.Log("1. Import VRChat SDK3 Worlds");\n                Debug.Log("2. Add VRC_SceneDescriptor to VRCWorld GameObject");\n                Debug.Log("3. Configure world settings in VRC_SceneDescriptor");\n                Debug.Log("4. Use VRChat SDK Control Panel to build and upload");\n            }\n            catch (System.Exception e)\n            {\n                Debug.LogError($"[VTM Build] Build preparation failed: {e.Message}");\n            }\n        }\n        \n        [MenuItem("VTM/Quick Fix Scene")]\n        public static void QuickFixScene()\n        {\n            Debug.Log("[VTM Quick Fix] Applying quick VRChat fixes...");\n            \n            // Apply fixes in priority order\n            VTMSceneBuilder.FixCanvasToWorldSpace();\n            VTMSceneBuilder.FixFloorMaterials();\n            \n            // Save scene\n            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();\n            \n            Debug.Log("[VTM Quick Fix] Quick fixes applied and scene saved!");\n        }\n    }\n}