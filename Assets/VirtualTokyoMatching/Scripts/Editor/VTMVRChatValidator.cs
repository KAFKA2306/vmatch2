using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

namespace VirtualTokyoMatching.Editor
{
    /// <summary>
    /// Validation script to check if VRChat-specific fixes have been applied correctly
    /// </summary>
    public class VTMVRChatValidator
    {
        [MenuItem("VTM/Validate VRChat Fixes")]
        public static void ValidateVRChatFixes()
        {
            Debug.Log("[VTM Validator] Starting VRChat fixes validation...");
            
            int issues = 0;
            int warnings = 0;
            
            // Validate UI Canvas Setup
            issues += ValidateUICanvas();
            
            // Validate Floor Materials
            issues += ValidateFloorMaterials();
            
            // Validate Button Enhancements
            warnings += ValidateButtonEnhancements();
            
            // Summary
            if (issues == 0 && warnings == 0)
            {
                Debug.Log("[VTM Validator] ✅ All VRChat fixes validated successfully! World is ready for VRChat.");
            }
            else if (issues == 0)
            {
                Debug.LogWarning($"[VTM Validator] ⚠️ Validation passed with {warnings} warnings. Check details above.");
            }
            else
            {
                Debug.LogError($"[VTM Validator] ❌ Validation failed: {issues} issues, {warnings} warnings. Apply VRChat fixes before uploading.");
            }
        }
        
        static int ValidateUICanvas()
        {
            Debug.Log("[VTM Validator] Checking UI Canvas setup...");
            
            int issues = 0;
            
            GameObject mainCanvas = GameObject.Find("MainLobbyCanvas");
            if (mainCanvas == null)
            {
                Debug.LogError("[VTM Validator] ❌ MainLobbyCanvas not found");
                return 1;
            }
            
            Canvas canvas = mainCanvas.GetComponent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[VTM Validator] ❌ Canvas component missing on MainLobbyCanvas");
                return 1;
            }
            
            // Check render mode
            if (canvas.renderMode != RenderMode.WorldSpace)
            {
                Debug.LogError($"[VTM Validator] ❌ Canvas render mode is {canvas.renderMode}, should be WorldSpace");
                issues++;
            }
            else
            {
                Debug.Log("[VTM Validator] ✅ Canvas render mode is WorldSpace");
            }
            
            // Check positioning (should be on wall, not following player)
            Vector3 pos = mainCanvas.transform.position;
            if (Mathf.Approximately(pos.x, 0) && pos.y > 1f && pos.z > 8f)
            {
                Debug.Log($"[VTM Validator] ✅ Canvas positioned on wall at {pos}");
            }
            else
            {
                Debug.LogWarning($"[VTM Validator] ⚠️ Canvas position {pos} may not be optimal for wall mounting");
            }
            
            // Check scale (should be small for WorldSpace)
            Vector3 scale = mainCanvas.transform.localScale;
            if (scale.x < 0.01f && scale.y < 0.01f && scale.z < 0.01f)
            {
                Debug.Log($"[VTM Validator] ✅ Canvas scale appropriate for WorldSpace: {scale}");
            }
            else
            {
                Debug.LogError($"[VTM Validator] ❌ Canvas scale too large for WorldSpace: {scale}");
                issues++;
            }
            
            // Check for interaction components
            BoxCollider collider = mainCanvas.GetComponent<BoxCollider>();
            if (collider != null && collider.isTrigger)
            {
                Debug.Log("[VTM Validator] ✅ Canvas has interaction collider");
            }
            else
            {
                Debug.LogWarning("[VTM Validator] ⚠️ Canvas missing interaction collider");
            }
            
            // Check CanvasScaler
            CanvasScaler scaler = mainCanvas.GetComponent<CanvasScaler>();
            if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize)
            {
                Debug.Log("[VTM Validator] ✅ CanvasScaler set to ConstantPixelSize");
            }
            else
            {
                Debug.LogError("[VTM Validator] ❌ CanvasScaler not set to ConstantPixelSize for WorldSpace");
                issues++;
            }
            
            return issues;
        }
        
        static int ValidateFloorMaterials()
        {
            Debug.Log("[VTM Validator] Checking floor materials...");
            
            int issues = 0;
            
            // Check lobby floor
            GameObject lobbyFloor = GameObject.Find("LobbyFloor");
            if (lobbyFloor != null)
            {
                issues += ValidateFloorMaterial(lobbyFloor, "LobbyFloor");
            }
            else
            {
                Debug.LogWarning("[VTM Validator] ⚠️ LobbyFloor not found");
            }
            
            // Check room floors
            GameObject[] roomFloors = GameObject.FindGameObjectsWithTag("RoomFloor");
            if (roomFloors.Length > 0)
            {
                foreach (GameObject floor in roomFloors)
                {
                    issues += ValidateFloorMaterial(floor, floor.name);
                }
            }
            else
            {
                Debug.LogWarning("[VTM Validator] ⚠️ No RoomFloor objects found");
            }
            
            // Check general floor tag objects
            GameObject[] allFloors = GameObject.FindGameObjectsWithTag("Floor");
            if (allFloors.Length > 0)
            {
                foreach (GameObject floor in allFloors)
                {
                    issues += ValidateFloorMaterial(floor, floor.name);
                }
            }
            
            return issues;
        }
        
        static int ValidateFloorMaterial(GameObject floorObject, string name)
        {
            Renderer renderer = floorObject.GetComponent<Renderer>();
            if (renderer == null)
            {
                Debug.LogWarning($"[VTM Validator] ⚠️ {name} has no Renderer component");
                return 0;
            }
            
            Material mat = renderer.sharedMaterial;
            if (mat == null)
            {
                Debug.LogError($"[VTM Validator] ❌ {name} has no material assigned");
                return 1;
            }
            
            // Check if using Unlit shader
            if (mat.shader != null && mat.shader.name.Contains("Unlit"))
            {
                Debug.Log($"[VTM Validator] ✅ {name} using Unlit shader: {mat.shader.name}");
                
                // Check if material is white
                if (mat.color == Color.white)
                {
                    Debug.Log($"[VTM Validator] ✅ {name} material is white");
                }
                else
                {
                    Debug.LogWarning($"[VTM Validator] ⚠️ {name} material color is {mat.color}, not white");
                }
                
                // Check shadow settings
                if (renderer.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off && !renderer.receiveShadows)
                {
                    Debug.Log($"[VTM Validator] ✅ {name} has shadows disabled (stable color)");
                }
                else
                {
                    Debug.LogWarning($"[VTM Validator] ⚠️ {name} shadows not disabled - may cause color changes");
                }
                
                return 0;
            }
            else
            {
                Debug.LogError($"[VTM Validator] ❌ {name} not using Unlit shader: {mat.shader?.name ?? "null"}");
                return 1;
            }
        }
        
        static int ValidateButtonEnhancements()
        {
            Debug.Log("[VTM Validator] Checking button enhancements...");
            
            int warnings = 0;
            
            Button[] buttons = Object.FindObjectsOfType<Button>();
            if (buttons.Length == 0)
            {
                Debug.LogWarning("[VTM Validator] ⚠️ No buttons found in scene");
                return 1;
            }
            
            foreach (Button button in buttons)
            {
                // Check for outline component
                var outline = button.GetComponent<UnityEngine.UI.Outline>();
                if (outline == null)
                {
                    Debug.LogWarning($"[VTM Validator] ⚠️ Button {button.name} missing Outline component for visibility");
                    warnings++;
                }
                
                // Check text component
                var text = button.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (text != null)
                {
                    if (text.fontSize < 20)
                    {
                        Debug.LogWarning($"[VTM Validator] ⚠️ Button {button.name} text size {text.fontSize} may be too small for Quest");
                        warnings++;
                    }
                    
                    if (text.outlineWidth <= 0)
                    {
                        Debug.LogWarning($"[VTM Validator] ⚠️ Button {button.name} text missing outline for Quest visibility");
                        warnings++;
                    }
                }
            }
            
            if (warnings == 0)
            {
                Debug.Log($"[VTM Validator] ✅ All {buttons.Length} buttons have proper enhancements");
            }
            
            return warnings;
        }
        
        [MenuItem("VTM/Generate VRChat Performance Report")]
        public static void GeneratePerformanceReport()
        {
            Debug.Log("[VTM Validator] Generating VRChat performance report...");
            
            // Count draw calls
            int totalRenderers = Object.FindObjectsOfType<Renderer>().Length;
            int uiElements = Object.FindObjectsOfType<Graphic>().Length;
            
            // Count lights
            int totalLights = Object.FindObjectsOfType<Light>().Length;
            
            // Check materials
            var materials = new System.Collections.Generic.HashSet<Material>();
            foreach (Renderer r in Object.FindObjectsOfType<Renderer>())
            {
                if (r.sharedMaterial != null)
                    materials.Add(r.sharedMaterial);
            }
            
            Debug.Log("=== VRChat Performance Report ===");
            Debug.Log($"Renderers: {totalRenderers} (Quest target: <100)");
            Debug.Log($"UI Elements: {uiElements}");
            Debug.Log($"Lights: {totalLights} (Quest target: <4)");
            Debug.Log($"Unique Materials: {materials.Count} (Quest target: <10)");
            
            // Performance recommendations
            if (totalRenderers > 100)
            {
                Debug.LogWarning("[Performance] ⚠️ High renderer count - consider combining meshes");
            }
            
            if (totalLights > 4)
            {
                Debug.LogWarning("[Performance] ⚠️ Too many lights for Quest - consider baked lighting");
            }
            
            if (materials.Count > 10)
            {
                Debug.LogWarning("[Performance] ⚠️ High material count - consider texture atlasing");
            }
            
            Debug.Log("=== End Performance Report ===");
        }
    }
}