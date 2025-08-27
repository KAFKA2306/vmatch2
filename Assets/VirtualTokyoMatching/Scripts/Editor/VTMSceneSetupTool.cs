using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using VRC.SDKBase;

namespace VirtualTokyoMatching.Editor
{
    /// <summary>
    /// Unity Editor tool to automatically set up the Virtual Tokyo Matching scene structure.
    /// Creates all required GameObjects, UI components, and wires up dependencies.
    /// </summary>
    public class VTMSceneSetupTool : EditorWindow
    {
        private bool createEnvironment = true;
        private bool createSystems = true;
        private bool createUI = true;
        private bool createNetworking = true;
        private bool wireComponents = true;
        
        private int maxExpectedUsers = 30;
        private int sessionRoomCount = 3;
        
        [MenuItem("VTM/Scene Setup Tool")]
        public static void ShowWindow()
        {
            GetWindow<VTMSceneSetupTool>("VTM Scene Setup");
        }
        
        void OnGUI()
        {
            GUILayout.Label("Virtual Tokyo Matching Scene Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            GUILayout.Label("Components to Create:", EditorStyles.label);
            createEnvironment = EditorGUILayout.Toggle("Environment & Spawn Points", createEnvironment);
            createSystems = EditorGUILayout.Toggle("Core Systems", createSystems);
            createUI = EditorGUILayout.Toggle("UI Components", createUI);
            createNetworking = EditorGUILayout.Toggle("Networking Objects", createNetworking);
            wireComponents = EditorGUILayout.Toggle("Wire Component Dependencies", wireComponents);
            
            EditorGUILayout.Space();
            GUILayout.Label("Configuration:", EditorStyles.label);
            maxExpectedUsers = EditorGUILayout.IntSlider("Max Expected Users", maxExpectedUsers, 10, 50);
            sessionRoomCount = EditorGUILayout.IntSlider("Session Rooms", sessionRoomCount, 1, 5);
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Create Scene Setup", GUILayout.Height(30)))
            {
                CreateSceneSetup();
            }
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Validate Existing Setup", GUILayout.Height(25)))
            {
                ValidateSetup();
            }
        }
        
        private void CreateSceneSetup()
        {
            try
            {
                EditorUtility.DisplayProgressBar("VTM Setup", "Creating scene structure...", 0f);
                
                GameObject root = CreateRootObject();
                
                if (createEnvironment)
                {
                    CreateEnvironment(root);
                    EditorUtility.DisplayProgressBar("VTM Setup", "Creating environment...", 0.2f);
                }
                
                if (createSystems)
                {
                    CreateCoreSystems(root);
                    EditorUtility.DisplayProgressBar("VTM Setup", "Creating core systems...", 0.4f);
                }
                
                if (createUI)
                {
                    CreateUIComponents(root);
                    EditorUtility.DisplayProgressBar("VTM Setup", "Creating UI components...", 0.6f);
                }
                
                if (createNetworking)
                {
                    CreateNetworkingObjects(root);
                    EditorUtility.DisplayProgressBar("VTM Setup", "Creating networking objects...", 0.8f);
                }
                
                if (wireComponents)
                {
                    WireComponentDependencies();
                    EditorUtility.DisplayProgressBar("VTM Setup", "Wiring dependencies...", 0.9f);
                }
                
                CreateWorldDescriptor();
                
                EditorUtility.DisplayProgressBar("VTM Setup", "Finalizing...", 1f);
                
                Debug.Log("[VTM Setup] Scene setup completed successfully!");
                EditorUtility.DisplayDialog("Success", "Virtual Tokyo Matching scene setup completed!", "OK");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[VTM Setup] Failed to create scene setup: {e.Message}");
                EditorUtility.DisplayDialog("Error", $"Setup failed: {e.Message}", "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
        
        private GameObject CreateRootObject()
        {
            GameObject root = GameObject.Find("VirtualTokyoMatchingWorld");
            if (root == null)
            {
                root = new GameObject("VirtualTokyoMatchingWorld");
            }
            return root;
        }
        
        private void CreateEnvironment(GameObject root)
        {
            // Environment parent
            GameObject environment = CreateChildObject(root, "Environment");
            
            // Lobby area
            GameObject lobby = CreateChildObject(environment, "Lobby");
            CreateLobbyGeometry(lobby);
            CreateSpawnPoints(lobby);
            
            // Session rooms
            GameObject sessionRooms = CreateChildObject(environment, "SessionRooms");
            CreateSessionRooms(sessionRooms);
            
            // Lighting
            GameObject lighting = CreateChildObject(environment, "Lighting");
            CreateLighting(lighting);
        }
        
        private void CreateLobbyGeometry(GameObject lobby)
        {
            // Floor
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.parent = lobby.transform;
            floor.transform.localScale = new Vector3(4, 1, 4);
            floor.transform.position = new Vector3(0, 0, 0);
            
            // Walls (simple boundary)
            CreateWall(lobby, "WallNorth", new Vector3(0, 1, 20), new Vector3(40, 2, 1));
            CreateWall(lobby, "WallSouth", new Vector3(0, 1, -20), new Vector3(40, 2, 1));
            CreateWall(lobby, "WallEast", new Vector3(20, 1, 0), new Vector3(1, 2, 40));
            CreateWall(lobby, "WallWest", new Vector3(-20, 1, 0), new Vector3(1, 2, 40));
        }
        
        private void CreateWall(GameObject parent, string name, Vector3 position, Vector3 scale)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.parent = parent.transform;
            wall.transform.position = position;
            wall.transform.localScale = scale;
        }
        
        private void CreateSpawnPoints(GameObject lobby)
        {
            GameObject spawnParent = CreateChildObject(lobby, "SpawnPoints");
            
            // Create spawn points in a circle around the lobby
            int spawnCount = Mathf.Min(maxExpectedUsers, 20);
            float radius = 8f;
            
            for (int i = 0; i < spawnCount; i++)
            {
                float angle = (i * 360f / spawnCount) * Mathf.Deg2Rad;
                Vector3 position = new Vector3(
                    Mathf.Cos(angle) * radius,
                    0.1f,
                    Mathf.Sin(angle) * radius
                );
                
                GameObject spawnPoint = new GameObject($"SpawnPoint_{i:00}");
                spawnPoint.transform.parent = spawnParent.transform;
                spawnPoint.transform.position = position;
                spawnPoint.transform.LookAt(Vector3.zero);
                
                // Add visual indicator (remove in production)
                CreateVisualMarker(spawnPoint, Color.green);
            }
        }
        
        private void CreateSessionRooms(GameObject sessionRoomsParent)
        {
            for (int i = 0; i < sessionRoomCount; i++)
            {
                GameObject room = CreateChildObject(sessionRoomsParent, $"Room{i:00}");
                
                // Position rooms away from lobby
                Vector3 roomPosition = new Vector3((i - 1) * 30, 0, 50);
                room.transform.position = roomPosition;
                
                CreateRoomGeometry(room, i);
                CreateRoomSpawnPoints(room);
                CreateRoomUI(room);
            }
        }
        
        private void CreateRoomGeometry(GameObject room, int roomIndex)
        {
            GameObject environment = CreateChildObject(room, "Environment");
            environment.transform.position = room.transform.position;
            
            // Room floor
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.parent = environment.transform;
            floor.transform.localPosition = Vector3.zero;
            floor.transform.localScale = new Vector3(2, 1, 2);
            
            // Room walls
            CreateWall(environment, "WallNorth", new Vector3(0, 1, 10), new Vector3(20, 2, 1));
            CreateWall(environment, "WallSouth", new Vector3(0, 1, -10), new Vector3(20, 2, 1));
            CreateWall(environment, "WallEast", new Vector3(10, 1, 0), new Vector3(1, 2, 20));
            CreateWall(environment, "WallWest", new Vector3(-10, 1, 0), new Vector3(1, 2, 20));
            
            // Furniture (2 chairs)
            CreateChair(environment, "Chair1", new Vector3(-3, 0.1f, 0));
            CreateChair(environment, "Chair2", new Vector3(3, 0.1f, 0));
        }
        
        private void CreateChair(GameObject parent, string name, Vector3 position)
        {
            GameObject chair = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chair.name = name;
            chair.transform.parent = parent.transform;
            chair.transform.position = position;
            chair.transform.localScale = new Vector3(1, 1, 1);
        }
        
        private void CreateRoomSpawnPoints(GameObject room)
        {
            GameObject spawnParent = CreateChildObject(room, "SpawnPoints");
            
            // Two spawn points facing each other
            GameObject spawn1 = new GameObject("SpawnPoint1");
            spawn1.transform.parent = spawnParent.transform;
            spawn1.transform.position = new Vector3(-4, 0.1f, 0);
            spawn1.transform.LookAt(new Vector3(4, 0.1f, 0));
            CreateVisualMarker(spawn1, Color.blue);
            
            GameObject spawn2 = new GameObject("SpawnPoint2");
            spawn2.transform.parent = spawnParent.transform;
            spawn2.transform.position = new Vector3(4, 0.1f, 0);
            spawn2.transform.LookAt(new Vector3(-4, 0.1f, 0));
            CreateVisualMarker(spawn2, Color.blue);
        }
        
        private void CreateRoomUI(GameObject room)
        {
            // This will be implemented when UI components are created
            GameObject uiParent = CreateChildObject(room, "UI");
        }
        
        private void CreateLighting(GameObject lightingParent)
        {
            // Directional light (sun)
            GameObject sunLight = new GameObject("DirectionalLight");
            sunLight.transform.parent = lightingParent.transform;
            sunLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            
            Light lightComponent = sunLight.AddComponent<Light>();
            lightComponent.type = LightType.Directional;
            lightComponent.intensity = 1.5f;
            lightComponent.shadows = LightShadows.Soft;
        }
        
        private void CreateCoreSystems(GameObject root)
        {
            GameObject systems = CreateChildObject(root, "Systems");
            
            // VTM Controller - the heart of the system
            GameObject vtmController = CreateChildObject(systems, "VTMController");
            AddCoreComponents(vtmController);
            
            // World settings
            GameObject worldSettings = CreateChildObject(systems, "WorldSettings");
            CreateWorldSettingsComponents(worldSettings);
        }
        
        private void AddCoreComponents(GameObject vtmController)
        {
            // Add all core UdonBehaviour components
            var playerDataManager = vtmController.AddComponent<PlayerDataManager>();
            var diagnosisController = vtmController.AddComponent<DiagnosisController>();
            var vectorBuilder = vtmController.AddComponent<VectorBuilder>();
            var compatibilityCalculator = vtmController.AddComponent<CompatibilityCalculator>();
            var perfGuard = vtmController.AddComponent<PerfGuard>();
            var valuesSummaryGenerator = vtmController.AddComponent<ValuesSummaryGenerator>();
            var mainUIController = vtmController.AddComponent<MainUIController>();
            var safetyController = vtmController.AddComponent<SafetyController>();
            var sessionRoomManager = vtmController.AddComponent<SessionRoomManager>();
            
            Debug.Log("[VTM Setup] Added core system components to VTMController");
        }
        
        private void CreateWorldSettingsComponents(GameObject worldSettings)
        {
            // VRC Scene Descriptor will be added in CreateWorldDescriptor
        }
        
        private void CreateUIComponents(GameObject root)
        {
            GameObject uiParent = CreateChildObject(root, "UI");
            
            CreateMainLobbyUI(uiParent);
            CreateAssessmentUI(uiParent);
            CreateRecommenderUI(uiParent);
            CreateSafetyUI(uiParent);
        }
        
        private void CreateMainLobbyUI(GameObject uiParent)
        {
            // Screen Space Overlay Canvas
            GameObject canvasGO = new GameObject("MainLobbyCanvas");
            canvasGO.transform.parent = uiParent.transform;
            
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            
            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            GraphicRaycaster raycaster = canvasGO.AddComponent<GraphicRaycaster>();
            
            // Create main UI panels
            CreateWelcomePanel(canvasGO);
            CreateActionButtonPanel(canvasGO);
            CreateLoadingScreen(canvasGO);
        }
        
        private void CreateWelcomePanel(GameObject canvas)
        {
            GameObject welcomePanel = CreateUIPanel(canvas, "WelcomePanel", new Vector2(400, 200));
            RectTransform rt = welcomePanel.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(0, 200);
            
            // Player name text
            CreateUIText(welcomePanel, "PlayerNameText", "ようこそ、{PlayerName}さん", new Vector2(0, 50));
            
            // Progress slider
            CreateUISlider(welcomePanel, "ProgressSlider", new Vector2(0, 0));
            
            // Status text
            CreateUIText(welcomePanel, "StatusText", "診断を開始して、価値観マッチングを体験しましょう", new Vector2(0, -50));
        }
        
        private void CreateActionButtonPanel(GameObject canvas)
        {
            GameObject buttonPanel = CreateUIPanel(canvas, "ActionButtonPanel", new Vector2(300, 400));
            RectTransform rt = buttonPanel.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(0, -100);
            
            // Create 5 main buttons
            CreateUIButton(buttonPanel, "StartAssessmentButton", "診断を開始", new Vector2(0, 150));
            CreateUIButton(buttonPanel, "ContinueAssessmentButton", "診断を続ける", new Vector2(0, 75));
            CreateUIButton(buttonPanel, "PublicSharingButton", "公開設定を変更", new Vector2(0, 0));
            CreateUIButton(buttonPanel, "ViewRecommendationsButton", "おすすめを見る", new Vector2(0, -75));
            CreateUIButton(buttonPanel, "GoToRoomButton", "個室へ直行", new Vector2(0, -150));
        }
        
        private void CreateLoadingScreen(GameObject canvas)
        {
            GameObject loadingScreen = CreateUIPanel(canvas, "LoadingScreen", new Vector2(1920, 1080));
            loadingScreen.SetActive(false); // Initially disabled
            
            CreateUIText(loadingScreen, "LoadingText", "データを読み込み中...", Vector2.zero);
        }
        
        private void CreateAssessmentUI(GameObject uiParent)
        {
            // World Space Canvas for assessment
            GameObject canvasGO = new GameObject("AssessmentCanvas");
            canvasGO.transform.parent = uiParent.transform;
            canvasGO.transform.position = new Vector3(0, 2, 5);
            canvasGO.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 1;
            
            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            
            GraphicRaycaster raycaster = canvasGO.AddComponent<GraphicRaycaster>();
            
            CreateQuestionPanel(canvasGO);
        }
        
        private void CreateQuestionPanel(GameObject canvas)
        {
            GameObject questionPanel = CreateUIPanel(canvas, "QuestionPanel", new Vector2(800, 600));
            
            CreateUIText(questionPanel, "QuestionText", "質問がここに表示されます", new Vector2(0, 200));
            CreateUIText(questionPanel, "QuestionNumber", "質問 1 / 112", new Vector2(0, 150));
            
            // Choice buttons
            GameObject choiceParent = CreateChildObject(questionPanel, "ChoiceButtons");
            for (int i = 0; i < 5; i++)
            {
                CreateUIButton(choiceParent, $"Choice{i + 1}Button", $"選択肢 {i + 1}", new Vector2(0, 80 - i * 40));
            }
            
            // Navigation buttons
            GameObject navParent = CreateChildObject(questionPanel, "Navigation");
            CreateUIButton(navParent, "PreviousButton", "前へ", new Vector2(-200, -200));
            CreateUIButton(navParent, "NextButton", "次へ", new Vector2(-100, -200));
            CreateUIButton(navParent, "SkipButton", "スキップ", new Vector2(100, -200));
            CreateUIButton(navParent, "FinishButton", "完了", new Vector2(200, -200));
        }
        
        private void CreateRecommenderUI(GameObject uiParent)
        {
            // World Space Canvas for recommendations
            GameObject canvasGO = new GameObject("RecommenderCanvas");
            canvasGO.transform.parent = uiParent.transform;
            canvasGO.transform.position = new Vector3(-5, 2, 0);
            canvasGO.transform.rotation = Quaternion.Euler(0, 45, 0);
            canvasGO.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 2;
            
            CreateRecommendationCards(canvasGO);
        }
        
        private void CreateRecommendationCards(GameObject canvas)
        {
            GameObject cardsParent = CreateChildObject(canvas, "RecommendationCards");
            
            // Create 3 recommendation cards
            for (int i = 0; i < 3; i++)
            {
                GameObject card = CreateUIPanel(cardsParent, $"Card{i + 1:00}", new Vector2(300, 400));
                RectTransform rt = card.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(0, 150 - i * 200);
                
                CreateRecommendationCardContent(card, i + 1);
            }
        }
        
        private void CreateRecommendationCardContent(GameObject card, int cardNumber)
        {
            CreateUIText(card, "PlayerNameText", $"プレイヤー {cardNumber}", new Vector2(0, 150));
            CreateUIText(card, "CompatibilityText", "85%", new Vector2(-100, 100));
            CreateUIText(card, "HeadlineText", "創造的で社交的な方です", new Vector2(0, 50));
            CreateUIText(card, "ProgressText", "75%完了", new Vector2(100, 100));
            
            CreateUIButton(card, "ViewDetailsButton", "詳細を見る", new Vector2(0, -50));
            CreateUIButton(card, "InviteButton", "招待する", new Vector2(0, -100));
        }
        
        private void CreateSafetyUI(GameObject uiParent)
        {
            // World Space Canvas for safety controls
            GameObject canvasGO = new GameObject("SafetyCanvas");
            canvasGO.transform.parent = uiParent.transform;
            canvasGO.transform.position = new Vector3(5, 2, 0);
            canvasGO.transform.rotation = Quaternion.Euler(0, -45, 0);
            canvasGO.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 10;
            
            CreateSafetyPanel(canvasGO);
        }
        
        private void CreateSafetyPanel(GameObject canvas)
        {
            GameObject safetyPanel = CreateUIPanel(canvas, "SafetyPanel", new Vector2(400, 500));
            safetyPanel.SetActive(false); // Initially disabled
            
            CreateUIText(safetyPanel, "Title", "プライバシー設定", new Vector2(0, 200));
            
            CreateUIToggle(safetyPanel, "PublicSharingToggle", "プロフィールを公開", new Vector2(0, 150));
            CreateUIToggle(safetyPanel, "ProvisionalSharingToggle", "暫定データも公開", new Vector2(0, 100));
            
            CreateUIButton(safetyPanel, "HideProfileButton", "即座に非公開", new Vector2(0, 0));
            CreateUIButton(safetyPanel, "ResetDataButton", "データリセット", new Vector2(0, -50));
            CreateUIButton(safetyPanel, "EmergencyHideButton", "緊急非表示", new Vector2(0, -150));
        }
        
        private void CreateNetworkingObjects(GameObject root)
        {
            GameObject networking = CreateChildObject(root, "Networking");
            
            // Create profile publishers for expected users
            GameObject profilesParent = CreateChildObject(networking, "NetworkedProfiles");
            
            for (int i = 0; i < maxExpectedUsers; i++)
            {
                GameObject profile = CreateChildObject(profilesParent, $"PlayerProfile_{i:00}");
                var publisher = profile.AddComponent<PublicProfilePublisher>();
                // Network ID will need to be set manually in inspector
                
                Debug.Log($"[VTM Setup] Created profile publisher {i + 1}/{maxExpectedUsers}");
            }
        }
        
        private void CreateWorldDescriptor()
        {
            // Find or create VRCWorld object
            GameObject vrcWorld = GameObject.Find("VRCWorld");
            if (vrcWorld == null)
            {
                vrcWorld = new GameObject("VRCWorld");
            }
            
            // This would add VRC_SceneDescriptor component if VRChat SDK is imported
            // For now, just log the requirement
            Debug.Log("[VTM Setup] VRCWorld created. Add VRC_SceneDescriptor component manually after SDK import.");
        }
        
        private void WireComponentDependencies()
        {
            // This would wire up all the component dependencies
            // For now, log the requirement
            Debug.Log("[VTM Setup] Component wiring placeholder. Wire dependencies manually using inspector.");
        }
        
        private void ValidateSetup()
        {
            Debug.Log("[VTM Setup] Running validation...");
            
            int issues = 0;
            
            // Check for required objects
            if (GameObject.Find("VirtualTokyoMatchingWorld") == null)
            {
                Debug.LogWarning("[VTM Validation] Missing root VirtualTokyoMatchingWorld object");
                issues++;
            }
            
            if (GameObject.Find("VTMController") == null)
            {
                Debug.LogWarning("[VTM Validation] Missing VTMController system");
                issues++;
            }
            
            Debug.Log($"[VTM Validation] Validation complete. Found {issues} issues.");
        }
        
        // Helper methods for UI creation
        private GameObject CreateChildObject(GameObject parent, string name)
        {
            GameObject child = new GameObject(name);
            child.transform.parent = parent.transform;
            return child;
        }
        
        private GameObject CreateUIPanel(GameObject parent, string name, Vector2 size)
        {
            GameObject panel = new GameObject(name);
            panel.transform.parent = parent.transform;
            
            RectTransform rt = panel.AddComponent<RectTransform>();
            rt.sizeDelta = size;
            
            Image image = panel.AddComponent<Image>();
            image.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            
            return panel;
        }
        
        private GameObject CreateUIText(GameObject parent, string name, string text, Vector2 position)
        {
            GameObject textGO = new GameObject(name);
            textGO.transform.parent = parent.transform;
            
            RectTransform rt = textGO.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(300, 50);
            rt.anchoredPosition = position;
            
            TextMeshProUGUI textComponent = textGO.AddComponent<TextMeshProUGUI>();
            textComponent.text = text;
            textComponent.fontSize = 18;
            textComponent.alignment = TextAlignmentOptions.Center;
            textComponent.color = Color.white;
            
            return textGO;
        }
        
        private GameObject CreateUIButton(GameObject parent, string name, string text, Vector2 position)
        {
            GameObject buttonGO = new GameObject(name);
            buttonGO.transform.parent = parent.transform;
            
            RectTransform rt = buttonGO.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200, 40);
            rt.anchoredPosition = position;
            
            Image image = buttonGO.AddComponent<Image>();
            image.color = new Color(0.2f, 0.6f, 1f, 1f);
            
            Button button = buttonGO.AddComponent<Button>();
            
            // Button text
            GameObject textGO = new GameObject("Text");
            textGO.transform.parent = buttonGO.transform;
            
            RectTransform textRT = textGO.AddComponent<RectTransform>();
            textRT.sizeDelta = rt.sizeDelta;
            textRT.anchoredPosition = Vector2.zero;
            
            TextMeshProUGUI textComponent = textGO.AddComponent<TextMeshProUGUI>();
            textComponent.text = text;
            textComponent.fontSize = 14;
            textComponent.alignment = TextAlignmentOptions.Center;
            textComponent.color = Color.white;
            
            return buttonGO;
        }
        
        private GameObject CreateUISlider(GameObject parent, string name, Vector2 position)
        {
            GameObject sliderGO = new GameObject(name);
            sliderGO.transform.parent = parent.transform;
            
            RectTransform rt = sliderGO.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(300, 20);
            rt.anchoredPosition = position;
            
            Slider slider = sliderGO.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            
            return sliderGO;
        }
        
        private GameObject CreateUIToggle(GameObject parent, string name, string labelText, Vector2 position)
        {
            GameObject toggleGO = new GameObject(name);
            toggleGO.transform.parent = parent.transform;
            
            RectTransform rt = toggleGO.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(300, 30);
            rt.anchoredPosition = position;
            
            Toggle toggle = toggleGO.AddComponent<Toggle>();
            
            // Add label
            CreateUIText(toggleGO, "Label", labelText, new Vector2(50, 0));
            
            return toggleGO;
        }
        
        private void CreateVisualMarker(GameObject target, Color color)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "VisualMarker";
            marker.transform.parent = target.transform;
            marker.transform.localPosition = Vector3.up * 0.5f;
            marker.transform.localScale = Vector3.one * 0.3f;
            
            Renderer renderer = marker.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            renderer.material = mat;
            
            // Remove collider to avoid interference
            DestroyImmediate(marker.GetComponent<Collider>());
        }
    }
}