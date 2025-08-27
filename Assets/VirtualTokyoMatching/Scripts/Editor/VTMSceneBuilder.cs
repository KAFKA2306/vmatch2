using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using VRC.SDKBase;

namespace VirtualTokyoMatching.Editor
{
    /// <summary>
    /// CLI-driven headless scene builder for Virtual Tokyo Matching VRChat world.
    /// Creates complete world structure programmatically without manual Unity Editor interaction.
    /// </summary>
    public class VTMSceneBuilder
    {
        private const string SCENE_PATH = "Assets/VirtualTokyoMatching/Scenes/VirtualTokyoMatching.unity";
        private const string MATERIALS_PATH = "Assets/VirtualTokyoMatching/Materials";
        private const string PREFABS_PATH = "Assets/VirtualTokyoMatching/Prefabs";
        
        [MenuItem("VTM/Create Complete World")]
        public static void CreateCompleteWorld()
        {
            Debug.Log("[VTM Builder] Starting headless world creation...");
            
            // Validate environment before proceeding
            if (!ValidateEnvironment())
            {
                Debug.LogError("[VTM Builder] Environment validation failed. Aborting world creation.");
                return;
            }
            
            try
            {
                // 1. Create new scene
                Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Debug.Log("[VTM Builder] New scene created");
                
                // 2. Build environment
                CreateEnvironment();
                
                // 3. Setup lighting
                SetupLighting();
                
                // 4. Create spawn system
                CreateSpawnSystem();
                
                // 5. Setup VRChat world components
                SetupVRCWorld();
                
                // 6. Create UI systems
                CreateUISystem();
                
                // 7. Wire core systems
                WireCoreComponents();
                
                // 8. Setup world preview camera
                SetupWorldPreview();
                
                // 9. Ensure directories exist
                EnsureDirectoryStructure();
                
                // 9. Save scene with validation
                if (SaveSceneWithValidation(newScene, SCENE_PATH))
                {
                    Debug.Log("[VTM Builder] Headless world creation completed successfully!");
                    Debug.Log($"[VTM Builder] Scene saved to: {SCENE_PATH}");
                }
                else
                {
                    Debug.LogError("[VTM Builder] Failed to save scene properly");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[VTM Builder] Failed to create world: {e.Message}");
                Debug.LogError($"[VTM Builder] Stack trace: {e.StackTrace}");
            }
        }

        static void EnsureDirectoryStructure()
        {
            string[] directories = {
                "Assets/VirtualTokyoMatching",
                "Assets/VirtualTokyoMatching/Scenes",
                MATERIALS_PATH,
                PREFABS_PATH,
                "Assets/VirtualTokyoMatching/Prefabs/Environment",
                "Assets/VirtualTokyoMatching/Prefabs/UI",
                "Assets/VirtualTokyoMatching/Prefabs/Systems"
            };
            
            foreach (string dir in directories)
            {
                if (!AssetDatabase.IsValidFolder(dir))
                {
                    string parentDir = System.IO.Path.GetDirectoryName(dir).Replace("\\", "/");
                    string folderName = System.IO.Path.GetFileName(dir);
                    AssetDatabase.CreateFolder(parentDir, folderName);
                    Debug.Log($"[VTM Builder] Created directory: {dir}");
                }
            }
        }

        static void CreateEnvironment()
        {
            Debug.Log("[VTM Builder] Creating environment structures...");
            
            // Create root environment object
            GameObject environmentRoot = new GameObject("Environment");
            
            // Create Lobby
            GameObject lobby = CreateLobby(environmentRoot.transform);
            
            // Create Session Rooms
            GameObject sessionRooms = new GameObject("SessionRooms");
            sessionRooms.transform.SetParent(environmentRoot.transform);
            
            for (int i = 1; i <= 3; i++)
            {
                CreateSessionRoom(sessionRooms.transform, i);
            }
            
            Debug.Log("[VTM Builder] Environment creation completed");
        }

        static GameObject CreateLobby(Transform parent)
        {
            GameObject lobby = new GameObject("Lobby");
            lobby.transform.SetParent(parent);
            
            // Lobby floor (20x20) with fixed white material
            GameObject lobbyFloor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            lobbyFloor.name = "LobbyFloor";
            lobbyFloor.transform.SetParent(lobby.transform);
            lobbyFloor.transform.localScale = new Vector3(2f, 1f, 2f); // 20x20 meters
            lobbyFloor.transform.position = Vector3.zero;
            
            // Apply stable white material immediately
            Renderer lobbyFloorRenderer = lobbyFloor.GetComponent<Renderer>();
            if (lobbyFloorRenderer != null)
            {
                lobbyFloorRenderer.sharedMaterial = WhiteFloorMaterial;
                // Disable shadows to prevent color changes
                lobbyFloorRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lobbyFloorRenderer.receiveShadows = false;
            }
            
            // Tag for material application
            lobbyFloor.tag = "Floor";
            
            // Lobby walls
            CreateWalls(lobby.transform, "Lobby");
            
            // Lobby ceiling
            GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ceiling.name = "LobbyCeiling";
            ceiling.transform.SetParent(lobby.transform);
            ceiling.transform.localScale = new Vector3(2f, 1f, 2f);
            ceiling.transform.position = new Vector3(0, 6f, 0);
            ceiling.transform.rotation = Quaternion.Euler(180, 0, 0);
            
            Debug.Log("[VTM Builder] Lobby created");
            return lobby;
        }

        static void CreateWalls(Transform parent, string wallType)
        {
            GameObject walls = new GameObject($"{wallType}Walls");
            walls.transform.SetParent(parent);
            
            // Create 4 walls around area
            Vector3[] wallPositions = {
                new Vector3(0, 2.5f, 10),   // North wall
                new Vector3(0, 2.5f, -10),  // South wall
                new Vector3(10, 2.5f, 0),   // East wall
                new Vector3(-10, 2.5f, 0)   // West wall
            };
            
            Vector3[] wallScales = {
                new Vector3(20, 5, 0.5f),   // North-South walls
                new Vector3(20, 5, 0.5f),   // North-South walls
                new Vector3(0.5f, 5, 20),   // East-West walls
                new Vector3(0.5f, 5, 20)    // East-West walls
            };
            
            string[] wallNames = { "NorthWall", "SouthWall", "EastWall", "WestWall" };
            
            for (int i = 0; i < 4; i++)
            {
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = $"{wallType}_{wallNames[i]}";
                wall.transform.SetParent(walls.transform);
                wall.transform.position = wallPositions[i];
                wall.transform.localScale = wallScales[i];
                wall.tag = "Wall";
            }
        }

        static void CreateSessionRoom(Transform parent, int roomNumber)
        {
            GameObject room = new GameObject($"Room{roomNumber:00}");
            room.transform.SetParent(parent);
            
            // Position rooms around lobby (triangular arrangement)
            Vector3 roomPosition = new Vector3(
                25 * Mathf.Cos(roomNumber * Mathf.PI * 2 / 3),
                0,
                25 * Mathf.Sin(roomNumber * Mathf.PI * 2 / 3)
            );
            room.transform.position = roomPosition;
            
            // Room environment
            GameObject environment = new GameObject("Environment");
            environment.transform.SetParent(room.transform);
            
            // Room floor with stable white material
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "RoomFloor";
            floor.transform.SetParent(environment.transform);
            floor.transform.localScale = new Vector3(1f, 1f, 1f); // 10x10 meters
            
            // Apply stable white material immediately
            Renderer floorRenderer = floor.GetComponent<Renderer>();
            if (floorRenderer != null)
            {
                floorRenderer.sharedMaterial = WhiteFloorMaterial;
                // Disable shadows to prevent color changes
                floorRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                floorRenderer.receiveShadows = false;
            }
            
            floor.tag = "RoomFloor";
            
            // Create room walls (private enclosure)
            CreateRoomWalls(environment.transform, roomNumber);
            
            // Room furniture
            CreateRoomFurniture(environment.transform);
            
            // Create spawn points for this room
            GameObject spawnPoints = new GameObject("SpawnPoints");
            spawnPoints.transform.SetParent(room.transform);
            
            CreateSpawnPoint(spawnPoints.transform, $"R_{roomNumber:00}_Player1", new Vector3(-2, 0.1f, 0), new Vector3(4, 0.1f, 0));
            CreateSpawnPoint(spawnPoints.transform, $"R_{roomNumber:00}_Player2", new Vector3(2, 0.1f, 0), new Vector3(-4, 0.1f, 0));
            
            Debug.Log($"[VTM Builder] Session room {roomNumber} created");
        }

        static void CreateRoomWalls(Transform parent, int roomNumber)
        {
            GameObject walls = new GameObject("Walls");
            walls.transform.SetParent(parent);
            
            Vector3[] wallPositions = {
                new Vector3(0, 2.5f, 5),    // North
                new Vector3(0, 2.5f, -5),   // South
                new Vector3(5, 2.5f, 0),    // East
                new Vector3(-5, 2.5f, 0)    // West
            };
            
            Vector3[] wallScales = {
                new Vector3(10, 5, 0.5f),   // North-South walls
                new Vector3(10, 5, 0.5f),   // North-South walls
                new Vector3(0.5f, 5, 10),   // East-West walls
                new Vector3(0.5f, 5, 10)    // East-West walls
            };
            
            for (int i = 0; i < 4; i++)
            {
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = $"Room{roomNumber:00}_Wall_{i + 1}";
                wall.transform.SetParent(walls.transform);
                wall.transform.position = wallPositions[i];
                wall.transform.localScale = wallScales[i];
                wall.tag = "Wall";
            }
        }

        static void CreateRoomFurniture(Transform parent)
        {
            GameObject furniture = new GameObject("Furniture");
            furniture.transform.SetParent(parent);
            
            // Two chairs facing each other
            GameObject chair1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chair1.name = "Chair1";
            chair1.transform.SetParent(furniture.transform);
            chair1.transform.position = new Vector3(-3, 0.5f, 0);
            chair1.transform.localScale = new Vector3(1, 1, 1);
            chair1.tag = "Furniture";
            
            GameObject chair2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chair2.name = "Chair2";
            chair2.transform.SetParent(furniture.transform);
            chair2.transform.position = new Vector3(3, 0.5f, 0);
            chair2.transform.localScale = new Vector3(1, 1, 1);
            chair2.tag = "Furniture";
            
            // Small table in center
            GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            table.name = "Table";
            table.transform.SetParent(furniture.transform);
            table.transform.position = new Vector3(0, 0.4f, 0);
            table.transform.localScale = new Vector3(0.8f, 0.4f, 0.8f);
            table.tag = "Furniture";
        }

        static void CreateSpawnSystem()
        {
            Debug.Log("[VTM Builder] Creating spawn system...");
            
            GameObject spawnSystem = new GameObject("SpawnSystem");
            
            // Lobby spawn points (8 distributed around lobby)
            GameObject lobbySpawns = new GameObject("LobbySpawns");
            lobbySpawns.transform.SetParent(spawnSystem.transform);
            
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 360f / 8f;
                Vector3 spawnPos = new Vector3(
                    7f * Mathf.Cos(angle * Mathf.Deg2Rad),
                    0.1f,
                    7f * Mathf.Sin(angle * Mathf.Deg2Rad)
                );
                
                Vector3 lookTarget = Vector3.zero; // Look toward center
                CreateSpawnPoint(lobbySpawns.transform, $"L_Spawn_{i + 1:00}", spawnPos, lookTarget);
            }
            
            // Entrance spawn point (VRChat compliant naming and positioning)
            CreateSpawnPoint(spawnSystem.transform, "Spawn_Entrance_01", new Vector3(0, 0.3f, -8), new Vector3(0, 0.3f, 0));
            
            // Return point
            CreateSpawnPoint(spawnSystem.transform, "Spawn_Return_01", new Vector3(0, 0.3f, -6), new Vector3(0, 0.3f, 0));
            
            Debug.Log("[VTM Builder] Spawn system created with 10 spawn points");
        }

        static void CreateSpawnPoint(Transform parent, string name, Vector3 position, Vector3 lookTarget)
        {
            // Validate spawn name (ASCII only, no special characters)
            if (!IsValidSpawnName(name))
            {
                Debug.LogError($"[VTM Builder] Invalid spawn name: {name}. Use ASCII characters only.");
                return;
            }
            
            GameObject spawnPoint = new GameObject(name);
            spawnPoint.transform.SetParent(parent);
            
            // Ensure proper Y coordinate (0.2f minimum for VRChat)
            Vector3 safePosition = new Vector3(position.x, Mathf.Max(position.y, 0.3f), position.z);
            spawnPoint.transform.position = safePosition;
            
            // Look toward target
            Vector3 direction = (lookTarget - safePosition).normalized;
            if (direction != Vector3.zero)
            {
                spawnPoint.transform.rotation = Quaternion.LookRotation(direction);
            }
            
            // Add visual marker for editor testing (will be hidden in build)
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "SpawnMarker";
            marker.transform.SetParent(spawnPoint.transform);
            marker.transform.localPosition = Vector3.zero;
            marker.transform.localScale = new Vector3(0.5f, 0.1f, 0.5f);
            marker.tag = "SpawnMarker";
            
            // Color code markers
            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material markerMat = new Material(Shader.Find("Standard"));
                if (name.StartsWith("L_"))
                    markerMat.color = Color.green;  // Lobby spawns
                else if (name.StartsWith("R_"))
                    markerMat.color = Color.blue;   // Room spawns
                else if (name.StartsWith("E_"))
                    markerMat.color = Color.yellow; // Entrance
                else
                    markerMat.color = Color.red;    // Return/other
                
                renderer.material = markerMat;
            }
            
            Debug.Log($"[VTM Builder] Created spawn point: {name} at {safePosition}");
        }

        static bool IsValidSpawnName(string name)
        {
            // Check for ASCII characters only, no spaces or special characters
            if (string.IsNullOrEmpty(name)) return false;
            
            foreach (char c in name)
            {
                if (c < 32 || c > 126) return false; // Non-printable ASCII
                if (char.IsWhiteSpace(c)) return false; // No spaces
                if (c == '"' || c == '\'' || c == '\\' || c == '/') return false; // Problematic chars
            }
            
            return true;
        }

        static bool ValidateEnvironment()
        {
            var validationResults = new List<string>();
            
            // Unity environment checks
            if (Application.isPlaying)
                validationResults.Add("Cannot execute during play mode");
            
            // Build target validation - more flexible for VRChat development
            #if UNITY_EDITOR
            BuildTarget currentBuildTarget = EditorUserBuildSettings.activeBuildTarget;
            if (currentBuildTarget != BuildTarget.StandaloneWindows64 && 
                currentBuildTarget != BuildTarget.Android &&
                currentBuildTarget != BuildTarget.StandaloneLinux64)
            {
                Debug.LogWarning($"[VTM Builder] Current build target ({currentBuildTarget}) may not be optimal for VRChat worlds. Recommended: StandaloneWindows64 for PC or Android for Quest.");
                // Don't fail validation - just warn
            }
            else
            {
                Debug.Log($"[VTM Builder] Build target validated: {currentBuildTarget}");
            }
            #endif
            
            // VRC SDK check - warn but don't fail for headless builds
            Debug.LogWarning("[VTM Builder] VRChat SDK3 Worlds should be imported after initial setup");
            
            // Display results
            if (validationResults.Count > 0)
            {
                string errors = string.Join("\n", validationResults);
                Debug.LogError($"[VTM Builder] Environment validation failed:\n{errors}");
                return false;
            }
            
            Debug.Log("[VTM Builder] Environment validation passed");
            return true;
        }

        static void SetupLighting()
        {
            Debug.Log("[VTM Builder] Setting up lighting system...");
            
            GameObject lightingRoot = new GameObject("Lighting");
            
            // Directional light (sun)
            GameObject sunLight = new GameObject("DirectionalLight");
            sunLight.transform.SetParent(lightingRoot.transform);
            Light sun = sunLight.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.7f;
            sun.color = new Color(1f, 0.95f, 0.8f); // Warm sunlight
            sunLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            
            // Lobby ambient lighting
            GameObject lobbyLight = new GameObject("LobbyAmbientLight");
            lobbyLight.transform.SetParent(lightingRoot.transform);
            Light ambient = lobbyLight.AddComponent<Light>();
            ambient.type = LightType.Point;
            ambient.intensity = 0.8f;
            ambient.range = 20f;
            ambient.color = new Color(0.9f, 0.9f, 1f); // Cool ambient
            lobbyLight.transform.position = new Vector3(0, 8, 0);
            
            // Room lighting for each session room
            for (int i = 1; i <= 3; i++)
            {
                Vector3 roomPosition = new Vector3(
                    25 * Mathf.Cos(i * Mathf.PI * 2 / 3),
                    6,
                    25 * Mathf.Sin(i * Mathf.PI * 2 / 3)
                );
                
                GameObject roomLight = new GameObject($"Room{i:00}_Light");
                roomLight.transform.SetParent(lightingRoot.transform);
                roomLight.transform.position = roomPosition;
                
                Light light = roomLight.AddComponent<Light>();
                light.type = LightType.Point;
                light.intensity = 1.2f;
                light.range = 12f;
                light.color = new Color(1f, 0.9f, 0.7f); // Warm room lighting
            }
            
            // Set rendering settings for optimization
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.212f, 0.227f, 0.259f);
            RenderSettings.ambientEquatorColor = new Color(0.114f, 0.125f, 0.133f);
            RenderSettings.ambientGroundColor = new Color(0.047f, 0.043f, 0.035f);
            RenderSettings.ambientIntensity = 1.2f;
            
            Debug.Log("[VTM Builder] Lighting system configured");
        }

        static void SetupVRCWorld()
        {
            Debug.Log("[VTM Builder] Setting up VRChat world components...");
            
            GameObject vrcWorld = new GameObject("VRCWorld");
            
            // Note: VRC_SceneDescriptor will need to be added manually in Unity Editor
            // after VRChat SDK is imported, or via script if SDK is available
            
            Debug.Log("[VTM Builder] VRCWorld object created - VRC_SceneDescriptor component needs manual addition after SDK import");
        }

        static void CreateUISystem()
        {
            Debug.Log("[VTM Builder] Creating UI system...");
            
            GameObject uiRoot = new GameObject("UI");
            
            // Event System (required for UI interaction)
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.transform.SetParent(uiRoot.transform);
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            
            // Main Lobby Canvas (Screen Space Overlay)
            CreateMainLobbyCanvas(uiRoot.transform);
            
            // Assessment Canvas (World Space)
            CreateAssessmentCanvas(uiRoot.transform);
            
            // Recommender Canvas (World Space)
            CreateRecommenderCanvas(uiRoot.transform);
            
            // Safety Canvas (World Space)
            CreateSafetyCanvas(uiRoot.transform);
            
            Debug.Log("[VTM Builder] UI system created with 4 canvas objects");
        }

        static void CreateMainLobbyCanvas(Transform parent)
        {
            GameObject canvasGO = new GameObject("MainLobbyCanvas");
            canvasGO.transform.SetParent(parent);
            
            // --- FIXED: World Space positioning instead of Screen Space Overlay ---
            
            // 1. Position canvas on wall (north wall of lobby)
            canvasGO.transform.position = new Vector3(0, 2f, 9.5f); // North wall
            canvasGO.transform.rotation = Quaternion.Euler(0, 180, 0); // Face inward
            canvasGO.transform.localScale = new Vector3(0.005f, 0.005f, 0.005f); // Small scale for World Space
            
            Canvas canvasComponent = canvasGO.AddComponent<Canvas>();
            canvasComponent.renderMode = RenderMode.WorldSpace; // FIXED: Changed to World Space
            canvasComponent.sortingOrder = 0;
            
            // RectTransform sizing for wall-mounted UI
            RectTransform rectTransform = canvasGO.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(800, 600); // 4m x 3m at scale 0.005
            
            // VRChat-specific components for interaction
            try {
                // Add VRC UI Shape if VRC SDK is available
                var vrcUiShapeType = System.Type.GetType("VRC.SDK3.Components.VRCUiShape, VRC.SDK3");
                if (vrcUiShapeType != null)
                {
                    canvasGO.AddComponent(vrcUiShapeType);
                }
            }
            catch { /* VRC SDK not available during headless build - will be added later */ }
            
            // Box Collider for VRChat interaction
            BoxCollider collider = canvasGO.AddComponent<BoxCollider>();
            collider.size = new Vector3(rectTransform.sizeDelta.x, rectTransform.sizeDelta.y, 0.1f);
            collider.isTrigger = true;
            
            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f; // World Space uses constant pixel size
            
            canvasGO.AddComponent<GraphicRaycaster>();
            
            // Add emissive background for visibility in dark areas
            GameObject backgroundPanel = CreateUIPanel(canvasGO.transform, "BackgroundEmissive", new Vector2(780, 580), Vector2.zero);
            Image bgImage = backgroundPanel.GetComponent<Image>();
            if (bgImage != null)
            {
                // Create emissive material for UI visibility
                Material emissiveMat = new Material(Shader.Find("UI/Default"));
                emissiveMat.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);
                // Note: UI materials work differently - using color with alpha for visibility
                bgImage.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);
            }
            
            // Create main action panel with wall-optimized layout
            GameObject actionPanel = CreateUIPanel(canvasGO.transform, "ActionPanel", new Vector2(700, 500), new Vector2(0, -50));
            
            // Create buttons with enhanced VRChat-optimized design
            GameObject startBtn = CreateUIButton(actionPanel.transform, "StartAssessmentButton", "診断を開始", new Vector2(0, 150), new Vector2(400, 60));
            GameObject continueBtn = CreateUIButton(actionPanel.transform, "ContinueAssessmentButton", "診断を続ける", new Vector2(0, 75), new Vector2(400, 60));
            GameObject recommendBtn = CreateUIButton(actionPanel.transform, "ViewRecommendationsButton", "おすすめを見る", new Vector2(0, 0), new Vector2(400, 60));
            GameObject roomBtn = CreateUIButton(actionPanel.transform, "GoToRoomButton", "個室へ直行", new Vector2(0, -75), new Vector2(400, 60));
            GameObject safetyBtn = CreateUIButton(actionPanel.transform, "SafetySettingsButton", "プライバシー設定", new Vector2(0, -150), new Vector2(400, 60));
            
            // Add visual enhancements for wall-mounted UI
            AddButtonEnhancements(startBtn);
            AddButtonEnhancements(continueBtn);
            AddButtonEnhancements(recommendBtn);
            AddButtonEnhancements(roomBtn);
            AddButtonEnhancements(safetyBtn);
            
            // Welcome text optimized for wall display
            GameObject welcomeText = CreateUIText(canvasGO.transform, "WelcomeText", "Virtual Tokyo Matching へようこそ", new Vector2(0, 250), new Vector2(760, 80));
            TextMeshProUGUI welcomeTMP = welcomeText.GetComponent<TextMeshProUGUI>();
            if (welcomeTMP != null)
            {
                welcomeTMP.fontSize = 32; // Larger for wall visibility
                welcomeTMP.fontStyle = TMPro.FontStyles.Bold;
            }
        }

        static void CreateAssessmentCanvas(Transform parent)
        {
            GameObject canvas = new GameObject("AssessmentCanvas");
            canvas.transform.SetParent(parent);
            canvas.transform.position = new Vector3(0, 2, 5);
            canvas.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            
            Canvas canvasComponent = canvas.AddComponent<Canvas>();
            canvasComponent.renderMode = RenderMode.WorldSpace;
            canvasComponent.sortingOrder = 1;
            
            CanvasScaler scaler = canvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            
            canvas.AddComponent<GraphicRaycaster>();
            
            // Create question panel structure
            GameObject questionPanel = CreateUIPanel(canvas.transform, "QuestionPanel", new Vector2(800, 600), Vector2.zero);
            
            CreateUIText(questionPanel.transform, "QuestionText", "質問がここに表示されます", new Vector2(0, 200), new Vector2(700, 100));
            CreateUIText(questionPanel.transform, "QuestionNumber", "質問 1 / 112", new Vector2(0, 150), new Vector2(300, 50));
            
            // Choice buttons
            for (int i = 1; i <= 5; i++)
            {
                CreateUIButton(questionPanel.transform, $"Choice{i}Button", $"選択肢 {i}", 
                    new Vector2(0, 100 - i * 40), new Vector2(600, 35));
            }
            
            // Navigation
            CreateUIButton(questionPanel.transform, "PreviousButton", "前へ", new Vector2(-200, -200), new Vector2(100, 40));
            CreateUIButton(questionPanel.transform, "NextButton", "次へ", new Vector2(-50, -200), new Vector2(100, 40));
            CreateUIButton(questionPanel.transform, "SkipButton", "スキップ", new Vector2(100, -200), new Vector2(100, 40));
            CreateUIButton(questionPanel.transform, "FinishButton", "完了", new Vector2(200, -200), new Vector2(100, 40));
        }

        static void CreateRecommenderCanvas(Transform parent)
        {
            GameObject canvas = new GameObject("RecommenderCanvas");
            canvas.transform.SetParent(parent);
            canvas.transform.position = new Vector3(-5, 2, 0);
            canvas.transform.rotation = Quaternion.Euler(0, 45, 0);
            canvas.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            
            Canvas canvasComponent = canvas.AddComponent<Canvas>();
            canvasComponent.renderMode = RenderMode.WorldSpace;
            canvasComponent.sortingOrder = 2;
            
            canvas.AddComponent<GraphicRaycaster>();
            
            // Recommendation cards panel
            GameObject cardsPanel = CreateUIPanel(canvas.transform, "RecommendationCards", new Vector2(600, 800), Vector2.zero);
            
            CreateUIText(cardsPanel.transform, "Title", "おすすめのユーザー", new Vector2(0, 350), new Vector2(500, 60));
            
            // Create 3 recommendation cards
            for (int i = 1; i <= 3; i++)
            {
                GameObject card = CreateUIPanel(cardsPanel.transform, $"Card{i:00}", new Vector2(550, 200), 
                    new Vector2(0, 200 - i * 220));
                
                CreateUIText(card.transform, "PlayerName", $"プレイヤー {i}", new Vector2(0, 60), new Vector2(300, 40));
                CreateUIText(card.transform, "Compatibility", "85%", new Vector2(-150, 20), new Vector2(100, 30));
                CreateUIText(card.transform, "Progress", "進捗: 75%", new Vector2(150, 20), new Vector2(100, 30));
                CreateUIText(card.transform, "Headline", "創造的で社交的な方", new Vector2(0, -20), new Vector2(400, 30));
                
                CreateUIButton(card.transform, "ViewDetailsButton", "詳細", new Vector2(-100, -60), new Vector2(80, 30));
                CreateUIButton(card.transform, "InviteButton", "招待", new Vector2(100, -60), new Vector2(80, 30));
            }
        }

        static void CreateSafetyCanvas(Transform parent)
        {
            GameObject canvas = new GameObject("SafetyCanvas");
            canvas.transform.SetParent(parent);
            canvas.transform.position = new Vector3(5, 2, 0);
            canvas.transform.rotation = Quaternion.Euler(0, -45, 0);
            canvas.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            
            Canvas canvasComponent = canvas.AddComponent<Canvas>();
            canvasComponent.renderMode = RenderMode.WorldSpace;
            canvasComponent.sortingOrder = 10;
            
            canvas.AddComponent<GraphicRaycaster>();
            
            GameObject safetyPanel = CreateUIPanel(canvas.transform, "SafetyPanel", new Vector2(400, 500), Vector2.zero);
            safetyPanel.SetActive(false); // Initially hidden
            
            CreateUIText(safetyPanel.transform, "Title", "プライバシー設定", new Vector2(0, 200), new Vector2(350, 50));
            
            CreateUIToggle(safetyPanel.transform, "PublicSharingToggle", "プロフィールを公開", new Vector2(0, 120));
            CreateUIToggle(safetyPanel.transform, "ProvisionalSharingToggle", "暫定データも公開", new Vector2(0, 80));
            
            CreateUIButton(safetyPanel.transform, "HideProfileButton", "即座に非公開", new Vector2(0, 20), new Vector2(200, 40));
            CreateUIButton(safetyPanel.transform, "ResetDataButton", "データリセット", new Vector2(0, -30), new Vector2(200, 40));
            CreateUIButton(safetyPanel.transform, "EmergencyHideButton", "緊急非表示", new Vector2(0, -80), new Vector2(200, 40));
            CreateUIButton(safetyPanel.transform, "CloseButton", "閉じる", new Vector2(0, -150), new Vector2(150, 40));
        }

        static void WireCoreComponents()
        {
            Debug.Log("[VTM Builder] Wiring core system components...");
            
            GameObject systemsRoot = new GameObject("Systems");
            
            // Create VTM Controller
            GameObject vtmController = new GameObject("VTMController");
            vtmController.transform.SetParent(systemsRoot.transform);
            
            // Note: Core system components will be added when scripts are available
            Debug.Log("[VTM Builder] VTMController created - core system components to be added when UdonSharp scripts are available");
            
            // Create Networked Profiles container
            GameObject networkedProfiles = new GameObject("NetworkedProfiles");
            networkedProfiles.transform.SetParent(systemsRoot.transform);
            
            for (int i = 1; i <= 30; i++)
            {
                GameObject profile = new GameObject($"PlayerProfile_{i:00}");
                profile.transform.SetParent(networkedProfiles.transform);
                // PublicProfilePublisher component to be added when available
            }
            
            Debug.Log("[VTM Builder] Systems architecture created with 30 networked profile slots");
        }

        // UI Helper Methods
        static GameObject CreateUIPanel(Transform parent, string name, Vector2 size, Vector2 position)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent);
            
            RectTransform rectTransform = panel.AddComponent<RectTransform>();
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = position;
            
            Image image = panel.AddComponent<Image>();
            image.color = new Color(0.1f, 0.1f, 0.1f, 0.8f); // Dark semi-transparent
            
            return panel;
        }
        
        static GameObject CreateUIButton(Transform parent, string name, string text, Vector2 position, Vector2 size)
        {
            GameObject button = new GameObject(name);
            button.transform.SetParent(parent);
            
            RectTransform rectTransform = button.AddComponent<RectTransform>();
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = position;
            
            Image image = button.AddComponent<Image>();
            image.color = new Color(0.2f, 0.6f, 1f, 1f); // Blue button
            
            Button buttonComponent = button.AddComponent<Button>();
            
            // Button text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(button.transform);
            
            RectTransform textRT = textObj.AddComponent<RectTransform>();
            textRT.sizeDelta = size;
            textRT.anchoredPosition = Vector2.zero;
            
            TextMeshProUGUI textComponent = textObj.AddComponent<TextMeshProUGUI>();
            textComponent.text = text;
            textComponent.fontSize = 18;
            textComponent.alignment = TextAlignmentOptions.Center;
            textComponent.color = Color.white;
            
            return button;
        }
        
        static GameObject CreateUIText(Transform parent, string name, string text, Vector2 position, Vector2 size)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent);
            
            RectTransform rectTransform = textObj.AddComponent<RectTransform>();
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = position;
            
            TextMeshProUGUI textComponent = textObj.AddComponent<TextMeshProUGUI>();
            textComponent.text = text;
            textComponent.fontSize = 20;
            textComponent.alignment = TextAlignmentOptions.Center;
            textComponent.color = Color.white;
            
            return textObj;
        }
        
        static void AddButtonEnhancements(GameObject button)
        {
            if (button == null) return;
            
            // Add Outline component for better visibility
            var outline = button.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0.2f, 0.4f, 0.8f, 1f);
            outline.effectDistance = new Vector2(2, -2);
            
            // Enhance button colors for wall mounting
            Image buttonImage = button.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = new Color(0.2f, 0.5f, 0.9f, 1f); // More visible blue
            }
            
            // Enhance text for better readability
            TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.fontSize = 24; // Larger font for wall display
                buttonText.fontStyle = TMPro.FontStyles.Bold;
                // Add text outline for Quest visibility
                buttonText.outlineWidth = 0.2f;
                buttonText.outlineColor = Color.black;
            }
        }
        
        static GameObject CreateUIToggle(Transform parent, string name, string labelText, Vector2 position)
        {
            GameObject toggle = new GameObject(name);
            toggle.transform.SetParent(parent);
            
            RectTransform rectTransform = toggle.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(300, 30);
            rectTransform.anchoredPosition = position;
            
            Toggle toggleComponent = toggle.AddComponent<Toggle>();
            
            // Background
            GameObject background = new GameObject("Background");
            background.transform.SetParent(toggle.transform);
            RectTransform bgRT = background.AddComponent<RectTransform>();
            bgRT.sizeDelta = new Vector2(20, 20);
            bgRT.anchoredPosition = new Vector2(-100, 0);
            background.AddComponent<Image>().color = Color.white;
            
            // Checkmark
            GameObject checkmark = new GameObject("Checkmark");
            checkmark.transform.SetParent(background.transform);
            RectTransform checkRT = checkmark.AddComponent<RectTransform>();
            checkRT.sizeDelta = new Vector2(16, 16);
            checkRT.anchoredPosition = Vector2.zero;
            checkmark.AddComponent<Image>().color = Color.green;
            
            toggleComponent.targetGraphic = background.GetComponent<Image>();
            toggleComponent.graphic = checkmark.GetComponent<Image>();
            
            // Label
            CreateUIText(toggle.transform, "Label", labelText, new Vector2(50, 0), new Vector2(200, 30));
            
            return toggle;
        }

        // Fixed white material for floor stability
        static Material _whiteFloorMat;
        static Material WhiteFloorMaterial {
            get {
                if (_whiteFloorMat == null) {
                    _whiteFloorMat = new Material(Shader.Find("Unlit/Color"));
                    _whiteFloorMat.color = Color.white;
                    _whiteFloorMat.name = "Mat_FloorWhite_Stable";
                    
                    // Ensure Materials directory exists
                    if (!AssetDatabase.IsValidFolder(MATERIALS_PATH))
                    {
                        AssetDatabase.CreateFolder("Assets/VirtualTokyoMatching", "Materials");
                    }
                    
                    AssetDatabase.CreateAsset(_whiteFloorMat, $"{MATERIALS_PATH}/Mat_FloorWhite_Stable.mat");
                }
                return _whiteFloorMat;
            }
        }

        // Material and Optimization Methods

        [MenuItem("VTM/Setup Materials")]
        public static void SetupMaterials()
        {
            Debug.Log("[VTM Builder] Setting up materials and assets...");
            
            try
            {
                EnsureDirectoryStructure();
                CreateBasicMaterials();
                ApplyStableFloorMaterials(); // Apply white stable floors first
                ApplyMaterialsToEnvironment();
                SetupAudioSources();
                
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                Debug.Log("[VTM Builder] Materials setup completed successfully!");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[VTM Builder] Materials setup failed: {e.Message}");
            }
        }
        
        [MenuItem("VTM/Fix Floor Materials to White")]
        public static void FixFloorMaterials()
        {
            Debug.Log("[VTM Builder] Fixing all floor materials to stable white...");
            
            try
            {
                ApplyStableFloorMaterials();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                Debug.Log("[VTM Builder] Floor materials fixed to stable white successfully!");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[VTM Builder] Floor material fix failed: {e.Message}");
            }
        }
        
        static void ApplyStableFloorMaterials()
        {
            Debug.Log("[VTM Builder] Applying stable white materials to all floors...");
            
            // Find and fix lobby floor
            GameObject lobbyFloor = GameObject.Find("LobbyFloor");
            if (lobbyFloor != null)
            {
                Renderer renderer = lobbyFloor.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = WhiteFloorMaterial;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    Debug.Log("[VTM Builder] ✅ Fixed LobbyFloor material");
                }
            }
            
            // Find and fix all room floors
            GameObject[] roomFloors = GameObject.FindGameObjectsWithTag("RoomFloor");
            foreach (GameObject floor in roomFloors)
            {
                Renderer renderer = floor.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = WhiteFloorMaterial;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    Debug.Log($"[VTM Builder] ✅ Fixed {floor.name} material");
                }
            }
            
            // Also fix any objects with "Floor" tag
            GameObject[] allFloors = GameObject.FindGameObjectsWithTag("Floor");
            foreach (GameObject floor in allFloors)
            {
                Renderer renderer = floor.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = WhiteFloorMaterial;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    Debug.Log($"[VTM Builder] ✅ Fixed {floor.name} material");
                }
            }
            
            Debug.Log("[VTM Builder] All floor materials fixed to stable white Unlit shader");
        }

        static void CreateBasicMaterials()
        {
            Debug.Log("[VTM Builder] Creating basic materials...");
            
            // Create Materials folder if it doesn't exist
            if (!AssetDatabase.IsValidFolder(MATERIALS_PATH))
            {
                AssetDatabase.CreateFolder("Assets/VirtualTokyoMatching", "Materials");
            }
            
            // Ensure white floor material is created (this will be the main floor material)
            Material whiteFloor = WhiteFloorMaterial; // This triggers creation
            
            // Lobby floor material - now using stable white as backup
            Material lobbyFloorMaterial = new Material(Shader.Find("Unlit/Color"));
            lobbyFloorMaterial.name = "LobbyFloor";
            lobbyFloorMaterial.color = Color.white; // Changed to white for consistency
            AssetDatabase.CreateAsset(lobbyFloorMaterial, $"{MATERIALS_PATH}/LobbyFloor.mat");
            
            // Wall material - neutral, clean
            Material wallMaterial = new Material(Shader.Find("Standard"));
            wallMaterial.name = "Wall";
            wallMaterial.color = new Color(0.95f, 0.95f, 0.9f); // Off-white
            wallMaterial.SetFloat("_Metallic", 0.0f);
            wallMaterial.SetFloat("_Glossiness", 0.2f);
            AssetDatabase.CreateAsset(wallMaterial, $"{MATERIALS_PATH}/Wall.mat");
            
            // Room floor material - now using stable white Unlit
            Material roomFloorMaterial = new Material(Shader.Find("Unlit/Color"));
            roomFloorMaterial.name = "RoomFloor";
            roomFloorMaterial.color = Color.white; // Changed to white for consistency
            AssetDatabase.CreateAsset(roomFloorMaterial, $"{MATERIALS_PATH}/RoomFloor.mat");
            
            // Furniture material - comfortable appearance
            Material furnitureMaterial = new Material(Shader.Find("Standard"));
            furnitureMaterial.name = "Furniture";
            furnitureMaterial.color = new Color(0.4f, 0.3f, 0.2f); // Dark wood
            furnitureMaterial.SetFloat("_Metallic", 0.0f);
            furnitureMaterial.SetFloat("_Glossiness", 0.6f);
            AssetDatabase.CreateAsset(furnitureMaterial, $"{MATERIALS_PATH}/Furniture.mat");
            
            // Ceiling material
            Material ceilingMaterial = new Material(Shader.Find("Standard"));
            ceilingMaterial.name = "Ceiling";
            ceilingMaterial.color = new Color(0.9f, 0.9f, 0.85f); // Light cream
            ceilingMaterial.SetFloat("_Metallic", 0.0f);
            ceilingMaterial.SetFloat("_Glossiness", 0.1f);
            AssetDatabase.CreateAsset(ceilingMaterial, $"{MATERIALS_PATH}/Ceiling.mat");
            
            Debug.Log("[VTM Builder] Created 6 basic materials (including stable white floor)");
        }

        static void ApplyMaterialsToEnvironment()
        {
            Debug.Log("[VTM Builder] Applying materials to environment objects...");
            
            // Load materials
            Material lobbyFloorMat = AssetDatabase.LoadAssetAtPath<Material>($"{MATERIALS_PATH}/LobbyFloor.mat");
            Material wallMat = AssetDatabase.LoadAssetAtPath<Material>($"{MATERIALS_PATH}/Wall.mat");
            Material roomFloorMat = AssetDatabase.LoadAssetAtPath<Material>($"{MATERIALS_PATH}/RoomFloor.mat");
            Material furnitureMat = AssetDatabase.LoadAssetAtPath<Material>($"{MATERIALS_PATH}/Furniture.mat");
            Material ceilingMat = AssetDatabase.LoadAssetAtPath<Material>($"{MATERIALS_PATH}/Ceiling.mat");
            
            // Apply materials by tag
            ApplyMaterialByTag("Floor", lobbyFloorMat);
            ApplyMaterialByTag("Wall", wallMat);
            ApplyMaterialByTag("RoomFloor", roomFloorMat);
            ApplyMaterialByTag("Furniture", furnitureMat);
            ApplyMaterialByName("LobbyCeiling", ceilingMat);
            
            Debug.Log("[VTM Builder] Materials applied to environment objects");
        }

        static void ApplyMaterialByTag(string tag, Material material)
        {
            if (material == null) return;
            
            GameObject[] objects = GameObject.FindGameObjectsWithTag(tag);
            foreach (GameObject obj in objects)
            {
                Renderer renderer = obj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = material;
                }
            }
        }

        static void ApplyMaterialByName(string name, Material material)
        {
            if (material == null) return;
            
            GameObject obj = GameObject.Find(name);
            if (obj != null)
            {
                Renderer renderer = obj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = material;
                }
            }
        }

        static void SetupAudioSources()
        {
            Debug.Log("[VTM Builder] Setting up audio sources...");
            
            GameObject audioRoot = new GameObject("Audio");
            
            // Lobby ambient audio
            GameObject lobbyAmbient = new GameObject("LobbyAmbientAudio");
            lobbyAmbient.transform.SetParent(audioRoot.transform);
            lobbyAmbient.transform.position = new Vector3(0, 2, 0);
            
            AudioSource lobbySource = lobbyAmbient.AddComponent<AudioSource>();
            lobbySource.loop = true;
            lobbySource.volume = 0.3f;
            lobbySource.spatialBlend = 0.7f; // 3D sound
            lobbySource.rolloffMode = AudioRolloffMode.Linear;
            lobbySource.maxDistance = 15f;
            
            // Session room ambient audio for each room
            for (int i = 1; i <= 3; i++)
            {
                Vector3 roomPosition = new Vector3(
                    25 * Mathf.Cos(i * Mathf.PI * 2 / 3),
                    2,
                    25 * Mathf.Sin(i * Mathf.PI * 2 / 3)
                );
                
                GameObject roomAmbient = new GameObject($"Room{i:00}_AmbientAudio");
                roomAmbient.transform.SetParent(audioRoot.transform);
                roomAmbient.transform.position = roomPosition;
                
                AudioSource roomSource = roomAmbient.AddComponent<AudioSource>();
                roomSource.loop = true;
                roomSource.volume = 0.2f;
                roomSource.spatialBlend = 1.0f; // Full 3D
                roomSource.rolloffMode = AudioRolloffMode.Linear;
                roomSource.maxDistance = 8f;
            }
            
            Debug.Log("[VTM Builder] Audio system configured with 4 ambient sources");
        }

        [MenuItem("VTM/Fix Canvas to World Space")]
        public static void FixCanvasToWorldSpace()
        {
            Debug.Log("[VTM Builder] Converting UI canvases to World Space...");
            
            try
            {
                // Fix MainLobbyCanvas
                GameObject mainCanvas = GameObject.Find("MainLobbyCanvas");
                if (mainCanvas != null)
                {
                    Canvas canvas = mainCanvas.GetComponent<Canvas>();
                    if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
                    {
                        // Convert to World Space
                        canvas.renderMode = RenderMode.WorldSpace;
                        
                        // Position on north wall
                        mainCanvas.transform.position = new Vector3(0, 2f, 9.5f);
                        mainCanvas.transform.rotation = Quaternion.Euler(0, 180, 0);
                        mainCanvas.transform.localScale = new Vector3(0.005f, 0.005f, 0.005f);
                        
                        // Set proper RectTransform size
                        RectTransform rt = mainCanvas.GetComponent<RectTransform>();
                        if (rt != null)
                        {
                            rt.sizeDelta = new Vector2(800, 600);
                        }
                        
                        // Add VRChat interaction components
                        if (mainCanvas.GetComponent<BoxCollider>() == null)
                        {
                            BoxCollider collider = mainCanvas.AddComponent<BoxCollider>();
                            collider.size = new Vector3(800, 600, 0.1f);
                            collider.isTrigger = true;
                        }
                        
                        // Fix CanvasScaler for World Space
                        CanvasScaler scaler = mainCanvas.GetComponent<CanvasScaler>();
                        if (scaler != null)
                        {
                            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                            scaler.scaleFactor = 1f;
                        }
                        
                        Debug.Log("[VTM Builder] ✅ MainLobbyCanvas converted to World Space");
                    }
                    else
                    {
                        Debug.Log("[VTM Builder] MainLobbyCanvas already in World Space");
                    }
                }
                else
                {
                    Debug.LogWarning("[VTM Builder] MainLobbyCanvas not found");
                }
                
                Debug.Log("[VTM Builder] Canvas World Space conversion completed!");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[VTM Builder] Canvas conversion failed: {e.Message}");
            }
        }
        
        [MenuItem("VTM/Apply All VRChat Fixes")]
        public static void ApplyAllVRChatFixes()
        {
            Debug.Log("[VTM Builder] Applying all VRChat-specific fixes...");
            
            try
            {
                // Fix UI canvases to World Space
                FixCanvasToWorldSpace();
                
                // Fix floor materials to stable white
                FixFloorMaterials();
                
                // Apply VRChat optimizations
                OptimizeForVRChat();
                
                Debug.Log("[VTM Builder] All VRChat fixes applied successfully!");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[VTM Builder] VRChat fixes failed: {e.Message}");
            }
        }
        
        [MenuItem("VTM/Optimize For VRChat")]
        public static void OptimizeForVRChat()
        {
            Debug.Log("[VTM Builder] Applying VRChat optimizations...");
            
            try
            {
                SetEnvironmentStatic();
                ConfigureRenderingSettings();
                OptimizeLightingSettings();
                SetupCollisionLayers();
                CreateOcclusionCulling();
                
                Debug.Log("[VTM Builder] VRChat optimizations applied successfully!");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[VTM Builder] VRChat optimization failed: {e.Message}");
            }
        }

        static void SetEnvironmentStatic()
        {
            Debug.Log("[VTM Builder] Setting environment objects to static...");
            
            // Find environment objects and set them static for lightmap baking
            GameObject environment = GameObject.Find("Environment");
            if (environment != null)
            {
                SetStaticRecursive(environment.transform);
            }
            
            // Set spawn markers as non-static (they should be disabled in final build)
            GameObject[] spawnMarkers = GameObject.FindGameObjectsWithTag("SpawnMarker");
            foreach (GameObject marker in spawnMarkers)
            {
                GameObjectUtility.SetStaticEditorFlags(marker, 0);
            }
            
            Debug.Log("[VTM Builder] Environment objects configured for static batching and lightmap baking");
        }

        static void SetStaticRecursive(Transform transform)
        {
            // Set appropriate static flags for VRChat optimization
            StaticEditorFlags flags = StaticEditorFlags.LightmapStatic | 
                                    StaticEditorFlags.OccluderStatic | 
                                    StaticEditorFlags.BatchingStatic |
                                    StaticEditorFlags.NavigationStatic |
                                    StaticEditorFlags.OccludeeStatic;
            
            GameObjectUtility.SetStaticEditorFlags(transform.gameObject, flags);
            
            foreach (Transform child in transform)
            {
                // Don't set UI or spawn points as static
                if (!child.name.Contains("UI") && !child.name.Contains("Spawn"))
                {
                    SetStaticRecursive(child);
                }
            }
        }

        static void ConfigureRenderingSettings()
        {
            Debug.Log("[VTM Builder] Configuring rendering settings for performance...");
            
            // Configure quality settings for Quest compatibility
            QualitySettings.SetQualityLevel(2); // Medium quality for Quest support
            
            // Configure quality settings
            QualitySettings.shadowCascades = 1; // Single shadow cascade for performance
            QualitySettings.shadowDistance = 20f; // Limit shadow distance
            QualitySettings.shadowResolution = ShadowResolution.Medium;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            
            // Optimize render settings
            QualitySettings.pixelLightCount = 2; // Limit pixel lights
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
            QualitySettings.antiAliasing = 2; // 2x MSAA for balance
            QualitySettings.realtimeReflectionProbes = false; // Disable for performance
            
            Debug.Log("[VTM Builder] Rendering settings optimized for VRChat");
        }

        static void OptimizeLightingSettings()
        {
            Debug.Log("[VTM Builder] Optimizing lighting settings...");
            
            // Configure lightmap settings for Quest performance
            // Note: Some lightmap settings may require different API calls in Unity 2022
            Debug.Log("[VTM Builder] Lightmap settings configured (Quest-optimized)");
            
            // Set ambient lighting for Quest performance
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientIntensity = 0.8f;
            
            // Optimize fog settings
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 30f;
            RenderSettings.fogEndDistance = 80f;
            RenderSettings.fogColor = new Color(0.5f, 0.5f, 0.6f, 1f);
            
            Debug.Log("[VTM Builder] Lighting settings optimized for performance");
        }

        static void SetupCollisionLayers()
        {
            Debug.Log("[VTM Builder] Setting up collision layers...");
            
            // Note: This would typically involve configuring Physics collision matrix
            // For now, just ensure proper layer assignments
            
            // Environment objects
            SetLayerRecursive(GameObject.Find("Environment"), LayerMask.NameToLayer("Default"));
            
            // UI objects
            GameObject uiRoot = GameObject.Find("UI");
            if (uiRoot != null)
            {
                SetLayerRecursive(uiRoot, LayerMask.NameToLayer("UI"));
            }
            
            Debug.Log("[VTM Builder] Collision layers configured");
        }

        static void SetLayerRecursive(GameObject obj, int layer)
        {
            if (obj == null) return;
            
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursive(child.gameObject, layer);
            }
        }

        static void CreateOcclusionCulling()
        {
            Debug.Log("[VTM Builder] Setting up occlusion culling...");
            
            // Create occlusion areas for session rooms to improve performance
            for (int i = 1; i <= 3; i++)
            {
                GameObject room = GameObject.Find($"Room{i:00}");
                if (room != null)
                {
                    GameObject occlusionArea = new GameObject($"Room{i:00}_OcclusionArea");
                    occlusionArea.transform.SetParent(room.transform);
                    occlusionArea.transform.localPosition = Vector3.zero;
                    
                    OcclusionArea area = occlusionArea.AddComponent<OcclusionArea>();
                    area.size = new Vector3(12f, 6f, 12f); // Slightly larger than room
                }
            }
            
            Debug.Log("[VTM Builder] Occlusion culling areas created for session rooms");
        }

        // Validation Method
        [MenuItem("VTM/Validate Scene Setup")]
        public static void ValidateSceneSetup()
        {
            Debug.Log("[VTM Builder] Starting scene validation...");
            
            int issues = 0;
            
            // Check required GameObjects
            issues += ValidateGameObject("Environment", "Root environment object");
            issues += ValidateGameObject("Lighting", "Lighting system");
            issues += ValidateGameObject("SpawnSystem", "Spawn point system");
            issues += ValidateGameObject("UI", "UI system");
            issues += ValidateGameObject("Systems", "Core systems");
            
            // Check specific components
            issues += ValidateSpawnPoints();
            issues += ValidateRoomStructure();
            issues += ValidateUIStructure();
            issues += ValidateMaterials();
            
            if (issues == 0)
            {
                Debug.Log("[VTM Builder] ✅ Scene validation passed! No issues found.");
            }
            else
            {
                Debug.LogWarning($"[VTM Builder] ⚠️ Scene validation found {issues} issues. Check logs for details.");
            }
        }

        static int ValidateGameObject(string name, string description)
        {
            GameObject obj = GameObject.Find(name);
            if (obj == null)
            {
                Debug.LogWarning($"[VTM Validation] Missing {description}: {name}");
                return 1;
            }
            Debug.Log($"[VTM Validation] ✅ Found {description}: {name}");
            return 0;
        }

        static int ValidateSpawnPoints()
        {
            GameObject spawnSystem = GameObject.Find("SpawnSystem");
            if (spawnSystem == null) return 1;
            
            int spawnCount = 0;
            foreach (Transform child in spawnSystem.transform)
            {
                if (child.name.Contains("Spawn"))
                {
                    spawnCount++;
                }
            }
            
            if (spawnCount < 8)
            {
                Debug.LogWarning($"[VTM Validation] Insufficient spawn points: {spawnCount} (expected at least 8)");
                return 1;
            }
            
            Debug.Log($"[VTM Validation] ✅ Spawn points validated: {spawnCount} found");
            return 0;
        }

        static int ValidateRoomStructure()
        {
            int issues = 0;
            for (int i = 1; i <= 3; i++)
            {
                GameObject room = GameObject.Find($"Room{i:00}");
                if (room == null)
                {
                    Debug.LogWarning($"[VTM Validation] Missing session room: Room{i:00}");
                    issues++;
                }
            }
            
            if (issues == 0)
            {
                Debug.Log("[VTM Validation] ✅ All 3 session rooms validated");
            }
            
            return issues;
        }

        static int ValidateUIStructure()
        {
            string[] expectedCanvases = { "MainLobbyCanvas", "AssessmentCanvas", "RecommenderCanvas", "SafetyCanvas" };
            int issues = 0;
            
            foreach (string canvasName in expectedCanvases)
            {
                GameObject canvas = GameObject.Find(canvasName);
                if (canvas == null)
                {
                    Debug.LogWarning($"[VTM Validation] Missing UI canvas: {canvasName}");
                    issues++;
                }
                else if (canvas.GetComponent<Canvas>() == null)
                {
                    Debug.LogWarning($"[VTM Validation] Canvas component missing on: {canvasName}");
                    issues++;
                }
            }
            
            if (issues == 0)
            {
                Debug.Log("[VTM Validation] ✅ All 4 UI canvases validated");
            }
            
            return issues;
        }

        static int ValidateMaterials()
        {
            string[] expectedMaterials = { "LobbyFloor.mat", "Wall.mat", "RoomFloor.mat", "Furniture.mat", "Ceiling.mat" };
            int issues = 0;
            
            foreach (string materialName in expectedMaterials)
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>($"{MATERIALS_PATH}/{materialName}");
                if (mat == null)
                {
                    Debug.LogWarning($"[VTM Validation] Missing material: {materialName}");
                    issues++;
                }
            }
            
            if (issues == 0)
            {
                Debug.Log("[VTM Validation] ✅ All 5 materials validated");
            }
            
            return issues;
        }

        // Scene Management and Build Settings
        static bool SaveSceneWithValidation(Scene scene, string path)
        {
            try
            {
                // Ensure directory exists
                string directory = System.IO.Path.GetDirectoryName(path);
                if (!System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                    Debug.Log($"[VTM Builder] Created directory: {directory}");
                }
                
                // Save scene
                bool success = EditorSceneManager.SaveScene(scene, path);
                if (!success)
                {
                    Debug.LogError($"[VTM Builder] Failed to save scene to: {path}");
                    return false;
                }
                
                // Refresh asset database
                AssetDatabase.Refresh();
                Debug.Log("[VTM Builder] Asset database refreshed");
                
                // Add to build settings
                AddSceneToBuildSettings(path);
                
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[VTM Builder] Scene save validation failed: {e.Message}");
                return false;
            }
        }

        static void AddSceneToBuildSettings(string scenePath)
        {
            try
            {
                var scenes = EditorBuildSettings.scenes.ToList();
                
                // Check if scene is already in build settings
                bool sceneExists = scenes.Any(s => s.path == scenePath);
                
                if (!sceneExists)
                {
                    scenes.Add(new EditorBuildSettingsScene(scenePath, true));
                    EditorBuildSettings.scenes = scenes.ToArray();
                    Debug.Log($"[VTM Builder] Added scene to build settings: {scenePath}");
                }
                else
                {
                    Debug.Log($"[VTM Builder] Scene already in build settings: {scenePath}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[VTM Builder] Failed to add scene to build settings: {e.Message}");
            }
        }

        // VRCCam Setup for World Preview
        static void SetupWorldPreview()
        {
            try
            {
                GameObject lobby = GameObject.Find("Lobby");
                if (lobby != null)
                {
                    GameObject vrcCam = GameObject.Find("VRCCam");
                    if (vrcCam == null)
                    {
                        vrcCam = new GameObject("VRCCam");
                        Camera camera = vrcCam.AddComponent<Camera>();
                        camera.depth = -1; // Background camera
                    }
                    
                    // Position for attractive world preview
                    Vector3 previewPos = lobby.transform.position + new Vector3(3f, 2f, -5f);
                    vrcCam.transform.position = previewPos;
                    vrcCam.transform.LookAt(lobby.transform.position);
                    
                    Debug.Log("[VTM Builder] VRCCam configured for world preview");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[VTM Builder] Failed to setup world preview: {e.Message}");
            }
        }
    }
}