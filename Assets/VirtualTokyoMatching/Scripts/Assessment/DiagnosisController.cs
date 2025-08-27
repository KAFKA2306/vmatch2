using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using TMPro;

namespace VirtualTokyoMatching
{
    /// <summary>
    /// Controls the 112-question assessment interface with resume functionality.
    /// Handles UI navigation, response collection, and incremental saving.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class DiagnosisController : UdonSharpBehaviour
    {
        [Header("Dependencies")]
        public PlayerDataManager playerDataManager;
        public VectorBuilder vectorBuilder;
        public QuestionDatabase questionDatabase;

        [Header("UI References")]
        public GameObject assessmentPanel;
        public TextMeshProUGUI questionText;
        public Button[] choiceButtons = new Button[5];
        public TextMeshProUGUI[] choiceTexts = new TextMeshProUGUI[5];
        
        [Header("Navigation")]
        public Button previousButton;
        public Button nextButton;
        public Button skipButton;
        public Button finishButton;
        
        [Header("Progress")]
        public Slider progressSlider;
        public TextMeshProUGUI progressText;
        public TextMeshProUGUI questionNumberText;

        [Header("Status")]
        public TextMeshProUGUI statusText;
        public GameObject loadingIndicator;

        [Header("Events")]
        public UdonBehaviour[] onQuestionAnsweredTargets;
        public string onQuestionAnsweredEvent = "OnQuestionAnswered";
        
        public UdonBehaviour[] onAssessmentCompleteTargets;
        public string onAssessmentCompleteEvent = "OnAssessmentComplete";

        // State
        private int currentQuestionIndex = 0;
        private int[] currentResponses = new int[112];
        private bool isInitialized = false;
        private bool isAnswering = false;

        void Start()
        {
            InitializeUI();
            
            if (playerDataManager != null && playerDataManager.IsDataLoaded)
            {
                OnPlayerDataLoaded();
            }
        }

        public void OnPlayerDataLoaded()
        {
            if (playerDataManager == null) return;

            // Load existing responses
            currentResponses = playerDataManager.GetAllQuestionResponses();
            
            // Find resume position (first unanswered question)
            int resumeIndex = playerDataManager.GetNextUnansweredQuestion();
            if (resumeIndex >= 0)
            {
                currentQuestionIndex = resumeIndex;
            }
            else
            {
                currentQuestionIndex = 111; // All answered, go to last question
            }

            isInitialized = true;
            UpdateUI();
            
            Debug.Log($"[DiagnosisController] Resumed at question {currentQuestionIndex + 1}, progress: {playerDataManager.CurrentProgress}/112");
        }

        private void InitializeUI()
        {
            // Set up choice button events
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                int choiceIndex = i;
                if (choiceButtons[i] != null)
                {
                    choiceButtons[i].onClick.AddListener(() => OnChoiceSelected(choiceIndex + 1));
                }
            }

            // Set up navigation button events
            if (previousButton != null) previousButton.onClick.AddListener(OnPreviousClicked);
            if (nextButton != null) nextButton.onClick.AddListener(OnNextClicked);
            if (skipButton != null) skipButton.onClick.AddListener(OnSkipClicked);
            if (finishButton != null) finishButton.onClick.AddListener(OnFinishClicked);

            // Initialize UI state
            if (assessmentPanel != null) assessmentPanel.SetActive(false);
            if (loadingIndicator != null) loadingIndicator.SetActive(false);
        }

        public void StartAssessment()
        {
            if (!isInitialized)
            {
                ShowStatus("データを読み込み中...");
                if (loadingIndicator != null) loadingIndicator.SetActive(true);
                return;
            }

            if (assessmentPanel != null) assessmentPanel.SetActive(true);
            UpdateUI();
            
            Debug.Log($"[DiagnosisController] Started assessment at question {currentQuestionIndex + 1}");
        }

        public void StopAssessment()
        {
            if (assessmentPanel != null) assessmentPanel.SetActive(false);
            
            Debug.Log("[DiagnosisController] Assessment stopped");
        }

        private void UpdateUI()
        {
            if (!isInitialized || questionDatabase == null) return;

            // Validate question database
            if (!questionDatabase.ValidateDatabase())
            {
                ShowStatus("質問データベースにエラーがあります");
                return;
            }

            // Get current question
            var question = questionDatabase.questions[currentQuestionIndex];
            if (question == null) return;

            // Update question display
            if (questionText != null)
                questionText.text = question.text;

            if (questionNumberText != null)
                questionNumberText.text = $"質問 {currentQuestionIndex + 1} / 112";

            // Update choice buttons
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (choiceButtons[i] != null && choiceTexts[i] != null)
                {
                    choiceTexts[i].text = question.choices[i];
                    
                    // Highlight if this choice is selected
                    bool isSelected = currentResponses[currentQuestionIndex] == (i + 1);
                    var colors = choiceButtons[i].colors;
                    colors.normalColor = isSelected ? Color.green : Color.white;
                    choiceButtons[i].colors = colors;
                }
            }

            // Update progress
            int answeredCount = 0;
            for (int i = 0; i < 112; i++)
            {
                if (currentResponses[i] > 0) answeredCount++;
            }

            if (progressSlider != null)
            {
                progressSlider.value = (float)answeredCount / 112f;
            }

            if (progressText != null)
            {
                progressText.text = $"{answeredCount}/112 完了 ({(int)(100f * answeredCount / 112f)}%)";
            }

            // Update navigation buttons
            if (previousButton != null)
                previousButton.interactable = currentQuestionIndex > 0;

            if (nextButton != null)
                nextButton.interactable = currentQuestionIndex < 111;

            if (finishButton != null)
                finishButton.interactable = answeredCount >= 112;

            // Clear status if everything is OK
            ShowStatus("");
            if (loadingIndicator != null) loadingIndicator.SetActive(false);
        }

        public void OnChoiceSelected(int choice)
        {
            if (!isInitialized || isAnswering || choice < 1 || choice > 5) return;

            isAnswering = true;

            // Record the response
            currentResponses[currentQuestionIndex] = choice;

            // Save immediately to PlayerData
            if (playerDataManager != null)
            {
                playerDataManager.SaveQuestionResponse(currentQuestionIndex, choice);
            }

            // Trigger vector update
            if (vectorBuilder != null)
            {
                vectorBuilder.UpdateVectorIncremental(currentQuestionIndex, choice);
            }

            // Fire event
            SendEventToTargets(onQuestionAnsweredTargets, onQuestionAnsweredEvent);

            // Update UI
            UpdateUI();

            // Auto-advance to next unanswered question
            SendCustomEventDelayedSeconds(nameof(AutoAdvance), 0.5f);

            Debug.Log($"[DiagnosisController] Q{currentQuestionIndex + 1} answered: {choice}");
        }

        public void AutoAdvance()
        {
            isAnswering = false;

            // Find next unanswered question
            int nextUnanswered = -1;
            for (int i = currentQuestionIndex + 1; i < 112; i++)
            {
                if (currentResponses[i] == 0)
                {
                    nextUnanswered = i;
                    break;
                }
            }

            if (nextUnanswered >= 0)
            {
                currentQuestionIndex = nextUnanswered;
            }
            else
            {
                // All questions after current are answered, check if assessment is complete
                bool allAnswered = true;
                for (int i = 0; i < 112; i++)
                {
                    if (currentResponses[i] == 0)
                    {
                        allAnswered = false;
                        currentQuestionIndex = i; // Go to first unanswered
                        break;
                    }
                }

                if (allAnswered)
                {
                    OnAssessmentCompleted();
                    return;
                }
            }

            UpdateUI();
        }

        public void OnPreviousClicked()
        {
            if (currentQuestionIndex > 0)
            {
                currentQuestionIndex--;
                UpdateUI();
            }
        }

        public void OnNextClicked()
        {
            if (currentQuestionIndex < 111)
            {
                currentQuestionIndex++;
                UpdateUI();
            }
        }

        public void OnSkipClicked()
        {
            // Find next unanswered question
            int nextUnanswered = -1;
            for (int i = currentQuestionIndex + 1; i < 112; i++)
            {
                if (currentResponses[i] == 0)
                {
                    nextUnanswered = i;
                    break;
                }
            }

            if (nextUnanswered >= 0)
            {
                currentQuestionIndex = nextUnanswered;
                UpdateUI();
            }
        }

        public void OnFinishClicked()
        {
            // Check if all questions are answered
            bool allAnswered = true;
            for (int i = 0; i < 112; i++)
            {
                if (currentResponses[i] == 0)
                {
                    allAnswered = false;
                    break;
                }
            }

            if (allAnswered)
            {
                OnAssessmentCompleted();
            }
            else
            {
                ShowStatus("全ての質問に回答してから完了してください");
            }
        }

        private void OnAssessmentCompleted()
        {
            ShowStatus("診断が完了しました！");
            
            // Trigger final vector calculation
            if (vectorBuilder != null)
            {
                vectorBuilder.FinalizeVector();
            }

            // Fire completion event
            SendEventToTargets(onAssessmentCompleteTargets, onAssessmentCompleteEvent);

            Debug.Log("[DiagnosisController] Assessment completed!");

            // Hide panel after a delay
            SendCustomEventDelayedSeconds(nameof(HideAssessmentPanel), 2f);
        }

        public void HideAssessmentPanel()
        {
            if (assessmentPanel != null) 
                assessmentPanel.SetActive(false);
        }

        /// <summary>
        /// Jump to specific question (for debugging or special navigation)
        /// </summary>
        public void GoToQuestion(int questionIndex)
        {
            if (questionIndex >= 0 && questionIndex < 112)
            {
                currentQuestionIndex = questionIndex;
                UpdateUI();
            }
        }

        /// <summary>
        /// Get current question index
        /// </summary>
        public int GetCurrentQuestionIndex()
        {
            return currentQuestionIndex;
        }

        /// <summary>
        /// Get current response for a question
        /// </summary>
        public int GetCurrentResponse(int questionIndex)
        {
            if (questionIndex >= 0 && questionIndex < 112)
                return currentResponses[questionIndex];
            return 0;
        }

        /// <summary>
        /// Check if assessment is complete
        /// </summary>
        public bool IsComplete()
        {
            for (int i = 0; i < 112; i++)
            {
                if (currentResponses[i] == 0) return false;
            }
            return true;
        }

        private void ShowStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
                statusText.gameObject.SetActive(!string.IsNullOrEmpty(message));
            }
        }

        private void SendEventToTargets(UdonBehaviour[] targets, string eventName)
        {
            if (targets == null || string.IsNullOrEmpty(eventName)) return;

            foreach (var target in targets)
            {
                if (target != null)
                {
                    target.SendCustomEvent(eventName);
                }
            }
        }
    }
}