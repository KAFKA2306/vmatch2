using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using TMPro;

namespace VirtualTokyoMatching
{
    /// <summary>
    /// Displays compatibility recommendations with provisional indicators.
    /// Handles match cards, detail views, and invitation functionality.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class RecommenderUI : UdonSharpBehaviour
    {
        [System.Serializable]
        public class RecommendationCard
        {
            [Header("Card UI")]
            public GameObject cardObject;
            public TextMeshProUGUI playerNameText;
            public TextMeshProUGUI compatibilityText;
            public TextMeshProUGUI headlineText;
            public Transform tagContainer;
            public GameObject tagPrefab;
            
            [Header("Status Indicators")]
            public GameObject provisionalBadge;
            public Slider progressSlider;
            public TextMeshProUGUI progressText;
            public Image compatibilityRing;
            
            [Header("Actions")]
            public Button viewDetailsButton;
            public Button inviteButton;
            
            [Header("Avatar")]
            public RawImage avatarImage;
            public GameObject silhouetteImage;

            // Runtime data
            [System.NonSerialized] public CompatibilityCalculator.CompatibilityResult data;
            [System.NonSerialized] public GameObject[] tagObjects;
        }

        [Header("Dependencies")]
        public CompatibilityCalculator compatibilityCalculator;
        public SessionRoomManager sessionRoomManager;
        public ValuesSummaryGenerator summaryGenerator;

        [Header("UI References")]
        public GameObject recommendationsPanel;
        public RecommendationCard[] recommendationCards = new RecommendationCard[3];
        
        [Header("Detail View")]
        public GameObject detailPanel;
        public TextMeshProUGUI detailPlayerName;
        public TextMeshProUGUI detailHeadline;
        public TextMeshProUGUI detailSummary;
        public Transform detailTagContainer;
        public GameObject detailTagPrefab;
        public RadarChart compatibilityRadar;
        public Button detailInviteButton;
        public Button closeDetailButton;

        [Header("Status")]
        public GameObject loadingIndicator;
        public TextMeshProUGUI statusText;
        public Button refreshButton;

        [Header("Settings")]
        [Range(0.1f, 5f)]
        public float autoRefreshInterval = 3f;
        
        public Color provisionalColor = Color.yellow;
        public Color completeColor = Color.green;

        // State
        private CompatibilityCalculator.CompatibilityResult[] currentRecommendations;
        private CompatibilityCalculator.CompatibilityResult detailViewTarget;
        private bool isInitialized = false;
        private float lastRefreshTime = 0f;

        void Start()
        {
            InitializeUI();
            
            // Auto refresh
            SendCustomEventDelayedSeconds(nameof(AutoRefresh), autoRefreshInterval);
        }

        private void InitializeUI()
        {
            // Initialize recommendation cards
            for (int i = 0; i < recommendationCards.Length; i++)
            {
                var card = recommendationCards[i];
                if (card != null)
                {
                    InitializeCard(card, i);
                }
            }

            // Initialize detail panel
            if (closeDetailButton != null)
                closeDetailButton.onClick.AddListener(CloseDetailView);

            if (refreshButton != null)
                refreshButton.onClick.AddListener(RefreshRecommendations);

            // Hide panels initially
            if (detailPanel != null) detailPanel.SetActive(false);
            if (loadingIndicator != null) loadingIndicator.SetActive(false);

            isInitialized = true;
            
            // Try to load initial data
            RefreshRecommendations();
        }

        private void InitializeCard(RecommendationCard card, int index)
        {
            if (card.cardObject != null) card.cardObject.SetActive(false);

            if (card.viewDetailsButton != null)
            {
                int cardIndex = index;
                card.viewDetailsButton.onClick.AddListener(() => ShowDetailView(cardIndex));
            }

            if (card.inviteButton != null)
            {
                int cardIndex = index;
                card.inviteButton.onClick.AddListener(() => InvitePlayer(cardIndex));
            }

            // Initialize tag container
            card.tagObjects = new GameObject[0];
        }

        public void AutoRefresh()
        {
            float currentTime = Time.time;
            if (currentTime - lastRefreshTime >= autoRefreshInterval)
            {
                RefreshRecommendations();
            }

            SendCustomEventDelayedSeconds(nameof(AutoRefresh), autoRefreshInterval);
        }

        public void OnRecommendationsUpdated()
        {
            RefreshRecommendations();
        }

        public void RefreshRecommendations()
        {
            if (!isInitialized || compatibilityCalculator == null) return;

            lastRefreshTime = Time.time;

            if (compatibilityCalculator.IsCalculating())
            {
                ShowLoadingState();
                return;
            }

            // Get current recommendations
            currentRecommendations = compatibilityCalculator.GetTopRecommendations();
            
            UpdateRecommendationCards();
            HideLoadingState();
        }

        private void UpdateRecommendationCards()
        {
            for (int i = 0; i < recommendationCards.Length; i++)
            {
                var card = recommendationCards[i];
                var recommendation = i < currentRecommendations.Length ? currentRecommendations[i] : null;

                if (card != null)
                {
                    UpdateCard(card, recommendation);
                }
            }

            // Update status text
            int validCount = 0;
            for (int i = 0; i < currentRecommendations.Length; i++)
            {
                if (currentRecommendations[i] != null) validCount++;
            }

            ShowStatus(validCount > 0 ? $"{validCount}件の推薦があります" : "現在推薦可能な方はいません");
        }

        private void UpdateCard(RecommendationCard card, CompatibilityCalculator.CompatibilityResult recommendation)
        {
            card.data = recommendation;

            if (recommendation == null)
            {
                // Hide empty card
                if (card.cardObject != null) card.cardObject.SetActive(false);
                return;
            }

            // Show and update card
            if (card.cardObject != null) card.cardObject.SetActive(true);

            // Update basic info
            if (card.playerNameText != null)
                card.playerNameText.text = recommendation.profile.GetDisplayName();

            if (card.headlineText != null)
                card.headlineText.text = recommendation.profile.GetHeadline();

            // Update compatibility
            float compatibilityPercentage = recommendation.similarity * 100f;
            if (card.compatibilityText != null)
                card.compatibilityText.text = $"{compatibilityPercentage:F0}%";

            if (card.compatibilityRing != null)
            {
                card.compatibilityRing.fillAmount = recommendation.similarity;
                card.compatibilityRing.color = recommendation.isProvisional ? provisionalColor : completeColor;
            }

            // Update provisional status
            if (card.provisionalBadge != null)
                card.provisionalBadge.SetActive(recommendation.isProvisional);

            if (card.progressSlider != null)
            {
                card.progressSlider.value = recommendation.completionPercentage / 100f;
            }

            if (card.progressText != null)
            {
                card.progressText.text = $"{recommendation.completionPercentage:F0}%完了";
            }

            // Update tags
            UpdateCardTags(card);

            // Update avatar (use silhouette for privacy)
            if (card.avatarImage != null) card.avatarImage.gameObject.SetActive(false);
            if (card.silhouetteImage != null) card.silhouetteImage.SetActive(true);
        }

        private void UpdateCardTags(RecommendationCard card)
        {
            if (card.tagContainer == null || card.tagPrefab == null) return;

            // Clear existing tags
            for (int i = 0; i < card.tagObjects.Length; i++)
            {
                if (card.tagObjects[i] != null)
                    DestroyImmediate(card.tagObjects[i]);
            }

            // Get tags from profile
            string[] tags = card.data.profile.GetCurrentTags();
            card.tagObjects = new GameObject[tags.Length];

            for (int i = 0; i < tags.Length; i++)
            {
                var tagObject = Instantiate(card.tagPrefab, card.tagContainer);
                var tagText = tagObject.GetComponentInChildren<TextMeshProUGUI>();
                if (tagText != null) tagText.text = tags[i];
                
                tagObject.SetActive(true);
                card.tagObjects[i] = tagObject;
            }
        }

        public void ShowDetailView(int cardIndex)
        {
            if (cardIndex < 0 || cardIndex >= recommendationCards.Length) return;
            
            var card = recommendationCards[cardIndex];
            if (card == null || card.data == null) return;

            detailViewTarget = card.data;
            UpdateDetailView();
            
            if (detailPanel != null) detailPanel.SetActive(true);
        }

        private void UpdateDetailView()
        {
            if (detailViewTarget == null) return;

            // Update basic info
            if (detailPlayerName != null)
                detailPlayerName.text = detailViewTarget.profile.GetDisplayName();

            if (detailHeadline != null)
                detailHeadline.text = detailViewTarget.profile.GetHeadline();

            // Generate and show summary
            if (detailSummary != null && summaryGenerator != null)
            {
                var vector6D = detailViewTarget.profile.GetCurrent6DVector();
                // Note: This is simplified - ideally we'd have access to the full 30D vector
                detailSummary.text = "価値観の詳細分析は実装中です。"; // Placeholder
            }

            // Update tags in detail view
            UpdateDetailTags();

            // Update radar chart
            if (compatibilityRadar != null)
            {
                var theirVector = detailViewTarget.profile.GetCurrent6DVector();
                var myVector = GetMyVector6D();
                if (theirVector != null && myVector != null)
                {
                    compatibilityRadar.SetData(myVector, theirVector);
                }
            }

            // Update invite button
            if (detailInviteButton != null)
            {
                detailInviteButton.onClick.RemoveAllListeners();
                detailInviteButton.onClick.AddListener(() => InvitePlayerFromDetail());
            }
        }

        private void UpdateDetailTags()
        {
            if (detailTagContainer == null || detailTagPrefab == null) return;

            // Clear existing tags
            for (int i = detailTagContainer.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(detailTagContainer.GetChild(i).gameObject);
            }

            // Add new tags
            string[] tags = detailViewTarget.profile.GetCurrentTags();
            for (int i = 0; i < tags.Length; i++)
            {
                var tagObject = Instantiate(detailTagPrefab, detailTagContainer);
                var tagText = tagObject.GetComponentInChildren<TextMeshProUGUI>();
                if (tagText != null) tagText.text = tags[i];
                
                tagObject.SetActive(true);
            }
        }

        public void CloseDetailView()
        {
            if (detailPanel != null) detailPanel.SetActive(false);
            detailViewTarget = null;
        }

        public void InvitePlayer(int cardIndex)
        {
            if (cardIndex < 0 || cardIndex >= recommendationCards.Length) return;
            
            var card = recommendationCards[cardIndex];
            if (card == null || card.data == null) return;

            SendInvitation(card.data.player);
        }

        public void InvitePlayerFromDetail()
        {
            if (detailViewTarget == null) return;
            
            SendInvitation(detailViewTarget.player);
        }

        private void SendInvitation(VRCPlayerApi targetPlayer)
        {
            if (sessionRoomManager == null || targetPlayer == null) return;

            sessionRoomManager.SendInvitation(targetPlayer);
            ShowStatus($"{targetPlayer.displayName}さんに招待を送信しました");

            Debug.Log($"[RecommenderUI] Sent invitation to {targetPlayer.displayName}");
        }

        private float[] GetMyVector6D()
        {
            // Try to find our own PublicProfilePublisher
            var myPublisher = GetComponentInChildren<PublicProfilePublisher>();
            if (myPublisher != null && myPublisher.IsPublic())
            {
                return myPublisher.GetCurrent6DVector();
            }
            return null;
        }

        private void ShowLoadingState()
        {
            if (loadingIndicator != null) loadingIndicator.SetActive(true);
            ShowStatus("推薦を計算中...");
        }

        private void HideLoadingState()
        {
            if (loadingIndicator != null) loadingIndicator.SetActive(false);
        }

        private void ShowStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
                statusText.gameObject.SetActive(!string.IsNullOrEmpty(message));
            }
        }

        /// <summary>
        /// Show/hide recommendations panel
        /// </summary>
        public void SetRecommendationsPanelActive(bool active)
        {
            if (recommendationsPanel != null)
                recommendationsPanel.SetActive(active);
        }

        /// <summary>
        /// Get current recommendation count
        /// </summary>
        public int GetCurrentRecommendationCount()
        {
            if (currentRecommendations == null) return 0;
            
            int count = 0;
            for (int i = 0; i < currentRecommendations.Length; i++)
            {
                if (currentRecommendations[i] != null) count++;
            }
            return count;
        }

        /// <summary>
        /// Force UI refresh (for debugging)
        /// </summary>
        public void ForceRefresh()
        {
            lastRefreshTime = 0f;
            RefreshRecommendations();
        }
    }

    /// <summary>
    /// Simple radar chart component for compatibility visualization
    /// </summary>
    [System.Serializable]
    public class RadarChart
    {
        [Header("Chart Settings")]
        public LineRenderer chartLine;
        public Color myColor = Color.blue;
        public Color theirColor = Color.red;
        public float chartRadius = 100f;
        
        public void SetData(float[] myData, float[] theirData)
        {
            // This is a simplified implementation
            // In a full implementation, you would draw the radar chart using LineRenderer or UI elements
            if (chartLine != null && myData != null && theirData != null && myData.Length == theirData.Length)
            {
                int pointCount = myData.Length;
                chartLine.positionCount = pointCount + 1; // +1 to close the shape
                
                for (int i = 0; i < pointCount; i++)
                {
                    float angle = (float)i / pointCount * 2f * Mathf.PI;
                    float radius = Mathf.Abs(myData[i]) * chartRadius;
                    
                    Vector3 position = new Vector3(
                        Mathf.Cos(angle) * radius,
                        Mathf.Sin(angle) * radius,
                        0f
                    );
                    
                    chartLine.SetPosition(i, position);
                }
                
                // Close the shape
                chartLine.SetPosition(pointCount, chartLine.GetPosition(0));
            }
        }
    }
}