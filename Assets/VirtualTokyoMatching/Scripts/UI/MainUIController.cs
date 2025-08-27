using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using TMPro;

namespace VirtualTokyoMatching
{
    /// <summary>
    /// Main UI controller that orchestrates all UI panels and user flow.
    /// Manages the 5-button lobby layout and panel transitions.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class MainUIController : UdonSharpBehaviour
    {
        [Header("Dependencies")]
        public PlayerDataManager playerDataManager;
        public DiagnosisController diagnosisController;
        public RecommenderUI recommenderUI;
        public SafetyController safetyController;
        public SessionRoomManager sessionRoomManager;

        [Header("Main Lobby UI")]
        public GameObject lobbyPanel;
        public Button startAssessmentButton;
        public Button continueAssessmentButton;
        public Button publicSharingButton;
        public Button viewRecommendationsButton;
        public Button goToRoomButton;

        [Header("Status Display")]
        public TextMeshProUGUI welcomeText;
        public TextMeshProUGUI progressText;
        public TextMeshProUGUI statusText;
        public Slider progressSlider;

        [Header("Navigation")]
        public GameObject[] allPanels;
        public GameObject loadingScreen;
        public Button backToLobbyButton;

        [Header("Visual Feedback")]
        public Color enabledButtonColor = Color.green;
        public Color disabledButtonColor = Color.gray;
        public Color inProgressButtonColor = Color.yellow;

        // State
        private bool isInitialized = false;
        private string currentPanel = "lobby";

        void Start()
        {
            InitializeUI();
            
            // Wait for PlayerDataManager to load
            if (playerDataManager != null && playerDataManager.IsDataLoaded)
            {
                OnDataLoaded();
            }
            else
            {
                ShowLoadingScreen(true);
            }
        }

        private void InitializeUI()
        {
            // Set up main lobby buttons
            if (startAssessmentButton != null)
                startAssessmentButton.onClick.AddListener(StartNewAssessment);

            if (continueAssessmentButton != null)
                continueAssessmentButton.onClick.AddListener(ContinueAssessment);

            if (publicSharingButton != null)
                publicSharingButton.onClick.AddListener(TogglePublicSharing);

            if (viewRecommendationsButton != null)
                viewRecommendationsButton.onClick.AddListener(ViewRecommendations);

            if (goToRoomButton != null)
                goToRoomButton.onClick.AddListener(GoToRoom);

            if (backToLobbyButton != null)
                backToLobbyButton.onClick.AddListener(ReturnToLobby);

            // Initialize welcome message
            if (welcomeText != null && Networking.LocalPlayer != null)
            {
                welcomeText.text = $"ようこそ、{Networking.LocalPlayer.displayName}さん";
            }

            ShowLobbyPanel();
            isInitialized = true;
        }

        public void OnDataLoaded()
        {
            ShowLoadingScreen(false);
            UpdateLobbyUI();
            
            Debug.Log("[MainUIController] Data loaded, updating UI");
        }

        public void OnPlayerDataLoaded()
        {
            OnDataLoaded();
        }

        private void UpdateLobbyUI()
        {
            if (!isInitialized || playerDataManager == null) return;

            int progress = playerDataManager.CurrentProgress;
            bool isComplete = playerDataManager.IsAssessmentComplete();
            bool isPublic = playerDataManager.IsPublicSharingEnabled;

            // Update progress display
            if (progressSlider != null)
                progressSlider.value = (float)progress / 112f;

            if (progressText != null)
            {
                if (isComplete)
                    progressText.text = $"診断完了 ({progress}/112)";
                else if (progress > 0)
                    progressText.text = $"診断中 ({progress}/112)";
                else
                    progressText.text = "診断未開始";
            }

            // Update button states
            UpdateButtonStates(progress, isComplete, isPublic);

            // Update status text
            UpdateStatusText(progress, isComplete, isPublic);
        }

        private void UpdateButtonStates(int progress, bool isComplete, bool isPublic)
        {
            // Start/Continue Assessment buttons
            if (progress == 0)
            {
                // No progress - show start button
                SetButtonState(startAssessmentButton, true, enabledButtonColor, "診断を開始");
                SetButtonState(continueAssessmentButton, false, disabledButtonColor, "");
            }
            else if (!isComplete)
            {
                // In progress - show continue button
                SetButtonState(startAssessmentButton, false, disabledButtonColor, "");
                SetButtonState(continueAssessmentButton, true, inProgressButtonColor, "診断を続ける");
            }
            else
            {
                // Complete - show both as options
                SetButtonState(startAssessmentButton, true, disabledButtonColor, "診断をやり直す");
                SetButtonState(continueAssessmentButton, true, enabledButtonColor, "診断を見直す");
            }

            // Public sharing button
            string sharingText = isPublic ? "公開設定を変更" : "公開して推薦を見る";
            Color sharingColor = isPublic ? enabledButtonColor : (progress > 10 ? inProgressButtonColor : disabledButtonColor);
            SetButtonState(publicSharingButton, progress > 10, sharingColor, sharingText);

            // View recommendations button
            bool canViewRecs = isPublic && progress > 20; // Need some progress and public sharing
            SetButtonState(viewRecommendationsButton, canViewRecs, 
                canViewRecs ? enabledButtonColor : disabledButtonColor, "おすすめを見る");

            // Go to room button (only if in session or can join)
            bool inSession = sessionRoomManager != null && sessionRoomManager.IsInSession();
            bool canGoToRoom = inSession || (isPublic && progress > 50);
            string roomText = inSession ? "個室に戻る" : "個室へ直行";
            SetButtonState(goToRoomButton, canGoToRoom, 
                canGoToRoom ? enabledButtonColor : disabledButtonColor, roomText);
        }

        private void SetButtonState(Button button, bool interactable, Color color, string text)
        {
            if (button == null) return;

            button.interactable = interactable;
            
            var colors = button.colors;
            colors.normalColor = color;
            colors.disabledColor = disabledButtonColor;
            button.colors = colors;

            var buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null && !string.IsNullOrEmpty(text))
            {
                buttonText.text = text;
            }
        }

        private void UpdateStatusText(int progress, bool isComplete, bool isPublic)
        {
            if (statusText == null) return;

            string status = "";
            
            if (progress == 0)
            {
                status = "診断を開始して、価値観マッチングを体験しましょう";
            }
            else if (!isComplete)
            {
                status = $"診断進行中 - あと{112 - progress}問で完了です";
            }
            else if (!isPublic)
            {
                status = "診断完了！公開設定をONにして推薦を受けましょう";
            }
            else
            {
                status = "全機能が利用可能です。おすすめの方をチェックしてみましょう";
            }

            statusText.text = status;
        }

        // Button event handlers
        public void StartNewAssessment()
        {
            if (diagnosisController == null) return;

            if (playerDataManager != null && playerDataManager.CurrentProgress > 0)
            {
                // Ask for confirmation to restart
                // For now, just start from current position
                diagnosisController.StartAssessment();
            }
            else
            {
                diagnosisController.StartAssessment();
            }

            ShowPanel("assessment");
        }

        public void ContinueAssessment()
        {
            if (diagnosisController == null) return;

            diagnosisController.StartAssessment();
            ShowPanel("assessment");
        }

        public void TogglePublicSharing()
        {
            if (safetyController == null) return;

            safetyController.ShowSafetyPanel();
            ShowPanel("safety");
        }

        public void ViewRecommendations()
        {
            if (recommenderUI == null) return;

            recommenderUI.SetRecommendationsPanelActive(true);
            recommenderUI.RefreshRecommendations();
            ShowPanel("recommendations");
        }

        public void GoToRoom()
        {
            if (sessionRoomManager == null) return;

            if (sessionRoomManager.IsInSession())
            {
                // Already in session, teleport back
                // This would be handled by the session manager
                Debug.Log("[MainUIController] Returning to current session");
            }
            else
            {
                // Show room selection or wait for invitation
                Debug.Log("[MainUIController] Waiting for room invitation");
                UpdateStatusText(0, false, false); // Update with waiting message
            }
        }

        public void ReturnToLobby()
        {
            ShowLobbyPanel();
            UpdateLobbyUI();
        }

        private void ShowPanel(string panelName)
        {
            currentPanel = panelName;

            // Hide all panels first
            if (allPanels != null)
            {
                foreach (var panel in allPanels)
                {
                    if (panel != null) panel.SetActive(false);
                }
            }

            // Show specific panel
            switch (panelName)
            {
                case "lobby":
                    ShowLobbyPanel();
                    break;
                case "assessment":
                    // Assessment panel is managed by DiagnosisController
                    break;
                case "recommendations":
                    // Recommendations panel is managed by RecommenderUI
                    break;
                case "safety":
                    // Safety panel is managed by SafetyController
                    break;
            }
        }

        private void ShowLobbyPanel()
        {
            if (lobbyPanel != null) lobbyPanel.SetActive(true);
            currentPanel = "lobby";
        }

        private void ShowLoadingScreen(bool show)
        {
            if (loadingScreen != null) loadingScreen.SetActive(show);
        }

        // Event handlers from other systems
        public void OnAssessmentComplete()
        {
            UpdateLobbyUI();
            ReturnToLobby();
            
            // Show completion message
            if (statusText != null)
            {
                statusText.text = "診断が完了しました！公開設定をして推薦を受けてみましょう";
            }
        }

        public void OnProfilePublished()
        {
            UpdateLobbyUI();
        }

        public void OnProfileHidden()
        {
            UpdateLobbyUI();
        }

        public void OnSessionStarted()
        {
            UpdateLobbyUI();
        }

        public void OnSessionEnded()
        {
            UpdateLobbyUI();
        }

        /// <summary>
        /// Get current panel name
        /// </summary>
        public string GetCurrentPanel()
        {
            return currentPanel;
        }

        /// <summary>
        /// Force UI refresh (for debugging)
        /// </summary>
        public void RefreshUI()
        {
            UpdateLobbyUI();
        }

        /// <summary>
        /// Check if we're currently in the lobby
        /// </summary>
        public bool IsInLobby()
        {
            return currentPanel == "lobby";
        }
    }
}