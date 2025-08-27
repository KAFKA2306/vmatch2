using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using System;

namespace VirtualTokyoMatching
{
    /// <summary>
    /// Builds and updates 30-dimensional personality vectors from question responses.
    /// Handles incremental updates for provisional matching and final normalization.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class VectorBuilder : UdonSharpBehaviour
    {
        [Header("Dependencies")]
        public PlayerDataManager playerDataManager;
        public VectorConfiguration vectorConfig;
        public QuestionDatabase questionDatabase;

        [Header("Events")]
        public UdonBehaviour[] onVectorUpdatedTargets;
        public string onVectorUpdatedEvent = "OnVectorUpdated";
        
        public UdonBehaviour[] onVectorFinalizedTargets;
        public string onVectorFinalizedEvent = "OnVectorFinalized";

        [Header("Status")]
        [SerializeField] private bool isInitialized = false;
        [SerializeField] private bool isProvisional = true;
        [SerializeField] private float lastUpdateTime = 0f;

        // Working vector data
        private float[] workingVector = new float[30];
        private float[] normalizedVector = new float[30];
        private int[] questionResponses = new int[112];
        private bool[] responseChanged = new bool[112];

        // Performance tracking
        private const float MIN_UPDATE_INTERVAL = 0.1f; // Minimum seconds between updates

        void Start()
        {
            InitializeVector();
        }

        private void InitializeVector()
        {
            // Initialize arrays
            for (int i = 0; i < workingVector.Length; i++)
            {
                workingVector[i] = 0f;
                normalizedVector[i] = 0f;
            }

            for (int i = 0; i < questionResponses.Length; i++)
            {
                questionResponses[i] = 0;
                responseChanged[i] = false;
            }

            isInitialized = true;
            Debug.Log("[VectorBuilder] Initialized");
        }

        public void OnPlayerDataLoaded()
        {
            if (playerDataManager == null) return;

            // Load existing responses and rebuild vector
            questionResponses = playerDataManager.GetAllQuestionResponses();
            var savedVector = playerDataManager.GetVector30D();

            // If we have a saved vector, use it
            bool hasSavedVector = false;
            for (int i = 0; i < 30; i++)
            {
                if (Mathf.Abs(savedVector[i]) > 0.001f)
                {
                    hasSavedVector = true;
                    break;
                }
            }

            if (hasSavedVector)
            {
                Array.Copy(savedVector, normalizedVector, 30);
                Array.Copy(savedVector, workingVector, 30);
                isProvisional = !playerDataManager.IsAssessmentComplete();
            }
            else
            {
                // Rebuild from responses
                RebuildVectorFromResponses();
            }

            Debug.Log($"[VectorBuilder] Loaded vector, provisional: {isProvisional}");
        }

        /// <summary>
        /// Update vector incrementally when a single question is answered
        /// </summary>
        public void UpdateVectorIncremental(int questionIndex, int response)
        {
            if (!isInitialized || questionIndex < 0 || questionIndex >= 112 || response < 1 || response > 5)
                return;

            // Throttle updates
            float currentTime = Time.time;
            if (currentTime - lastUpdateTime < MIN_UPDATE_INTERVAL)
                return;

            int oldResponse = questionResponses[questionIndex];
            questionResponses[questionIndex] = response;
            responseChanged[questionIndex] = true;

            UpdateSingleQuestionContribution(questionIndex, oldResponse, response);

            // Update provisional normalized vector
            UpdateProvisionalNormalization();

            lastUpdateTime = currentTime;
            SendEventToTargets(onVectorUpdatedTargets, onVectorUpdatedEvent);

            Debug.Log($"[VectorBuilder] Incremental update Q{questionIndex + 1}: {oldResponse} -> {response}");
        }

        private void UpdateSingleQuestionContribution(int questionIndex, int oldResponse, int newResponse)
        {
            if (vectorConfig == null || questionDatabase == null) return;

            var question = questionDatabase.questions[questionIndex];
            if (question == null) return;

            // Remove old contribution
            if (oldResponse > 0 && oldResponse <= 5)
            {
                float oldWeight = question.weights[oldResponse - 1];
                for (int axis = 0; axis < 30; axis++)
                {
                    float configWeight = vectorConfig.GetWeight(questionIndex, axis);
                    workingVector[axis] -= oldWeight * configWeight;
                }
            }

            // Add new contribution
            if (newResponse > 0 && newResponse <= 5)
            {
                float newWeight = question.weights[newResponse - 1];
                for (int axis = 0; axis < 30; axis++)
                {
                    float configWeight = vectorConfig.GetWeight(questionIndex, axis);
                    workingVector[axis] += newWeight * configWeight;
                }
            }
        }

        /// <summary>
        /// Rebuild entire vector from all responses (used for loading or recovery)
        /// </summary>
        public void RebuildVectorFromResponses()
        {
            if (!isInitialized || vectorConfig == null || questionDatabase == null) return;

            // Clear working vector
            for (int i = 0; i < workingVector.Length; i++)
            {
                workingVector[i] = 0f;
            }

            // Process all responses
            for (int questionIndex = 0; questionIndex < 112; questionIndex++)
            {
                int response = questionResponses[questionIndex];
                if (response > 0 && response <= 5)
                {
                    var question = questionDatabase.questions[questionIndex];
                    if (question != null)
                    {
                        float responseWeight = question.weights[response - 1];
                        for (int axis = 0; axis < 30; axis++)
                        {
                            float configWeight = vectorConfig.GetWeight(questionIndex, axis);
                            workingVector[axis] += responseWeight * configWeight;
                        }
                    }
                }
            }

            // Update provisional normalization
            UpdateProvisionalNormalization();

            Debug.Log("[VectorBuilder] Rebuilt vector from responses");
        }

        /// <summary>
        /// Update provisional normalization (for partial vectors)
        /// </summary>
        private void UpdateProvisionalNormalization()
        {
            if (vectorConfig == null) return;

            // Count answered questions
            int answeredCount = 0;
            for (int i = 0; i < 112; i++)
            {
                if (questionResponses[i] > 0) answeredCount++;
            }

            if (answeredCount == 0)
            {
                // No answers yet, keep zero vector
                for (int i = 0; i < normalizedVector.Length; i++)
                {
                    normalizedVector[i] = 0f;
                }
                return;
            }

            // Scale by completion ratio to estimate full vector
            float completionRatio = (float)answeredCount / 112f;
            float scalingFactor = 1f / Mathf.Max(completionRatio, 0.1f); // Avoid division by very small numbers

            // Calculate magnitude for normalization
            float magnitude = 0f;
            for (int i = 0; i < 30; i++)
            {
                float scaledValue = workingVector[i] * scalingFactor;
                magnitude += scaledValue * scaledValue;
            }
            magnitude = Mathf.Sqrt(magnitude);

            // Normalize to [-1, 1] range
            if (magnitude > vectorConfig.epsilon)
            {
                float normalizationFactor = 1f / magnitude;
                for (int i = 0; i < 30; i++)
                {
                    normalizedVector[i] = Mathf.Clamp(workingVector[i] * scalingFactor * normalizationFactor, -1f, 1f);
                }
            }
            else
            {
                // Very small vector, keep as-is but ensure [-1, 1] bounds
                for (int i = 0; i < 30; i++)
                {
                    normalizedVector[i] = Mathf.Clamp(workingVector[i], -1f, 1f);
                }
            }
        }

        /// <summary>
        /// Finalize vector when assessment is complete
        /// </summary>
        public void FinalizeVector()
        {
            if (!isInitialized) return;

            // Check if all questions are answered
            int answeredCount = 0;
            for (int i = 0; i < 112; i++)
            {
                if (questionResponses[i] > 0) answeredCount++;
            }

            if (answeredCount < 112)
            {
                Debug.LogWarning($"[VectorBuilder] Attempting to finalize with only {answeredCount}/112 questions answered");
                return;
            }

            // Perform final normalization without scaling
            float magnitude = 0f;
            for (int i = 0; i < 30; i++)
            {
                magnitude += workingVector[i] * workingVector[i];
            }
            magnitude = Mathf.Sqrt(magnitude);

            if (magnitude > vectorConfig.epsilon)
            {
                float normalizationFactor = 1f / magnitude;
                for (int i = 0; i < 30; i++)
                {
                    normalizedVector[i] = Mathf.Clamp(workingVector[i] * normalizationFactor, -1f, 1f);
                }
            }

            isProvisional = false;

            // Save to PlayerData
            if (playerDataManager != null)
            {
                playerDataManager.UpdateVector30D(normalizedVector);
                playerDataManager.SavePlayerData();
            }

            SendEventToTargets(onVectorFinalizedTargets, onVectorFinalizedEvent);
            Debug.Log("[VectorBuilder] Vector finalized and saved");
        }

        /// <summary>
        /// Get current normalized vector
        /// </summary>
        public float[] GetNormalizedVector()
        {
            var copy = new float[30];
            Array.Copy(normalizedVector, copy, 30);
            return copy;
        }

        /// <summary>
        /// Get working (raw) vector
        /// </summary>
        public float[] GetWorkingVector()
        {
            var copy = new float[30];
            Array.Copy(workingVector, copy, 30);
            return copy;
        }

        /// <summary>
        /// Check if vector is provisional (incomplete assessment)
        /// </summary>
        public bool IsProvisional()
        {
            return isProvisional;
        }

        /// <summary>
        /// Get vector magnitude (strength/confidence)
        /// </summary>
        public float GetVectorMagnitude()
        {
            float magnitude = 0f;
            for (int i = 0; i < 30; i++)
            {
                magnitude += normalizedVector[i] * normalizedVector[i];
            }
            return Mathf.Sqrt(magnitude);
        }

        /// <summary>
        /// Get completion ratio (0-1)
        /// </summary>
        public float GetCompletionRatio()
        {
            int answeredCount = 0;
            for (int i = 0; i < 112; i++)
            {
                if (questionResponses[i] > 0) answeredCount++;
            }
            return (float)answeredCount / 112f;
        }

        /// <summary>
        /// Force rebuild vector (for debugging or recovery)
        /// </summary>
        public void ForceRebuildVector()
        {
            if (playerDataManager != null && playerDataManager.IsDataLoaded)
            {
                questionResponses = playerDataManager.GetAllQuestionResponses();
            }

            RebuildVectorFromResponses();
            
            Debug.Log("[VectorBuilder] Force rebuilt vector");
        }

        /// <summary>
        /// Reset vector to zero state
        /// </summary>
        public void ResetVector()
        {
            for (int i = 0; i < workingVector.Length; i++)
            {
                workingVector[i] = 0f;
                normalizedVector[i] = 0f;
            }

            for (int i = 0; i < questionResponses.Length; i++)
            {
                questionResponses[i] = 0;
                responseChanged[i] = false;
            }

            isProvisional = true;
            
            Debug.Log("[VectorBuilder] Vector reset");
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