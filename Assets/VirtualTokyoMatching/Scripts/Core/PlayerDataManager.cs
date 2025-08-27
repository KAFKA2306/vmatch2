using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using System;

namespace VirtualTokyoMatching
{
    /// <summary>
    /// Manages persistent PlayerData storage and retrieval for user assessment data.
    /// Handles incremental saves, resume functionality, and data validation.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PlayerDataManager : UdonSharpBehaviour
    {
        [Header("Data Keys")]
        private const string KEY_PREFIX = "vtm_";
        private const string KEY_PROGRESS = KEY_PREFIX + "progress";
        private const string KEY_FLAGS = KEY_PREFIX + "flags";
        private const string KEY_LAST_ACTIVE = KEY_PREFIX + "last_active";

        [Header("Events")]
        public UdonBehaviour[] onDataLoadedTargets;
        public string onDataLoadedEvent = "OnPlayerDataLoaded";
        
        public UdonBehaviour[] onDataSavedTargets;
        public string onDataSavedEvent = "OnPlayerDataSaved";
        
        public UdonBehaviour[] onDataResetTargets;
        public string onDataResetEvent = "OnPlayerDataReset";

        [Header("Status")]
        [SerializeField] private bool isDataLoaded = false;
        [SerializeField] private bool isSaving = false;
        [SerializeField] private int currentProgress = 0;
        [SerializeField] private int flags = 0;
        [SerializeField] private int lastActive = 0;

        // Question responses (1-5, 0=unanswered)
        private int[] questionResponses = new int[112];
        
        // 30D vector data (-1 to +1)
        private float[] vector30D = new float[30];
        
        // Retry parameters
        private int loadRetryCount = 0;
        private const int MAX_RETRY_COUNT = 3;
        private const float RETRY_DELAY = 2f;

        // Properties
        public bool IsDataLoaded => isDataLoaded;
        public bool IsSaving => isSaving;
        public int CurrentProgress => currentProgress;
        public int Flags => flags;
        public bool IsPublicSharingEnabled => (flags & 1) != 0;
        public bool IsProvisionalSharingEnabled => (flags & 2) != 0;

        void Start()
        {
            InitializeData();
            LoadPlayerData();
        }

        /// <summary>
        /// Initialize arrays and default values
        /// </summary>
        private void InitializeData()
        {
            for (int i = 0; i < questionResponses.Length; i++)
            {
                questionResponses[i] = 0; // 0 = unanswered
            }

            for (int i = 0; i < vector30D.Length; i++)
            {
                vector30D[i] = 0f;
            }

            currentProgress = 0;
            flags = 0;
            lastActive = 0;
        }

        /// <summary>
        /// Load player data with retry mechanism
        /// </summary>
        public void LoadPlayerData()
        {
            if (isSaving) return;

            loadRetryCount = 0;
            TryLoadData();
        }

        private void TryLoadData()
        {
            Debug.Log($"[PlayerDataManager] Loading player data (attempt {loadRetryCount + 1})");

            try
            {
                LoadProgress();
                LoadFlags();
                LoadLastActive();
                LoadQuestionResponses();
                LoadVector30D();

                isDataLoaded = true;
                SendEventToTargets(onDataLoadedTargets, onDataLoadedEvent);
                
                Debug.Log($"[PlayerDataManager] Data loaded successfully. Progress: {currentProgress}/112");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayerDataManager] Error loading data: {e.Message}");
                HandleLoadFailure();
            }
        }

        private void HandleLoadFailure()
        {
            loadRetryCount++;
            
            if (loadRetryCount < MAX_RETRY_COUNT)
            {
                Debug.Log($"[PlayerDataManager] Retrying data load in {RETRY_DELAY} seconds...");
                SendCustomEventDelayedSeconds(nameof(TryLoadData), RETRY_DELAY);
            }
            else
            {
                Debug.LogWarning("[PlayerDataManager] Max retry count reached. Using default data.");
                InitializeData();
                isDataLoaded = true;
                SendEventToTargets(onDataLoadedTargets, onDataLoadedEvent);
            }
        }

        private void LoadProgress()
        {
            if (Networking.LocalPlayer.IsValid())
            {
                var playerData = Networking.LocalPlayer.GetPlayerData();
                if (playerData.HasKey(KEY_PROGRESS))
                {
                    currentProgress = (int)playerData.GetFloat(KEY_PROGRESS);
                }
            }
        }

        private void LoadFlags()
        {
            if (Networking.LocalPlayer.IsValid())
            {
                var playerData = Networking.LocalPlayer.GetPlayerData();
                if (playerData.HasKey(KEY_FLAGS))
                {
                    flags = (int)playerData.GetFloat(KEY_FLAGS);
                }
            }
        }

        private void LoadLastActive()
        {
            if (Networking.LocalPlayer.IsValid())
            {
                var playerData = Networking.LocalPlayer.GetPlayerData();
                if (playerData.HasKey(KEY_LAST_ACTIVE))
                {
                    lastActive = (int)playerData.GetFloat(KEY_LAST_ACTIVE);
                }
            }
        }

        private void LoadQuestionResponses()
        {
            if (!Networking.LocalPlayer.IsValid()) return;

            var playerData = Networking.LocalPlayer.GetPlayerData();
            
            for (int i = 0; i < 112; i++)
            {
                string key = $"{KEY_PREFIX}q_{i:D3}";
                if (playerData.HasKey(key))
                {
                    questionResponses[i] = (int)playerData.GetFloat(key);
                }
            }
        }

        private void LoadVector30D()
        {
            if (!Networking.LocalPlayer.IsValid()) return;

            var playerData = Networking.LocalPlayer.GetPlayerData();
            
            for (int i = 0; i < 30; i++)
            {
                string key = $"{KEY_PREFIX}v_{i:D2}";
                if (playerData.HasKey(key))
                {
                    vector30D[i] = playerData.GetFloat(key);
                }
            }
        }

        /// <summary>
        /// Save player data incrementally
        /// </summary>
        public void SavePlayerData()
        {
            if (!Networking.LocalPlayer.IsValid() || isSaving) return;

            isSaving = true;
            
            try
            {
                SaveProgress();
                SaveFlags();
                SaveLastActive();
                SaveQuestionResponses();
                SaveVector30D();

                SendEventToTargets(onDataSavedTargets, onDataSavedEvent);
                Debug.Log("[PlayerDataManager] Data saved successfully");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayerDataManager] Error saving data: {e.Message}");
            }
            finally
            {
                isSaving = false;
            }
        }

        private void SaveProgress()
        {
            var playerData = Networking.LocalPlayer.GetPlayerData();
            playerData.SetFloat(KEY_PROGRESS, currentProgress);
        }

        private void SaveFlags()
        {
            var playerData = Networking.LocalPlayer.GetPlayerData();
            playerData.SetFloat(KEY_FLAGS, flags);
        }

        private void SaveLastActive()
        {
            lastActive = (int)(DateTime.Now.Ticks / 10000000); // Unix timestamp approximation
            var playerData = Networking.LocalPlayer.GetPlayerData();
            playerData.SetFloat(KEY_LAST_ACTIVE, lastActive);
        }

        private void SaveQuestionResponses()
        {
            var playerData = Networking.LocalPlayer.GetPlayerData();
            
            for (int i = 0; i < 112; i++)
            {
                if (questionResponses[i] > 0) // Only save answered questions
                {
                    string key = $"{KEY_PREFIX}q_{i:D3}";
                    playerData.SetFloat(key, questionResponses[i]);
                }
            }
        }

        private void SaveVector30D()
        {
            var playerData = Networking.LocalPlayer.GetPlayerData();
            
            for (int i = 0; i < 30; i++)
            {
                string key = $"{KEY_PREFIX}v_{i:D2}";
                playerData.SetFloat(key, vector30D[i]);
            }
        }

        /// <summary>
        /// Save single question response immediately
        /// </summary>
        public void SaveQuestionResponse(int questionIndex, int response)
        {
            if (questionIndex < 0 || questionIndex >= 112 || response < 1 || response > 5) return;

            questionResponses[questionIndex] = response;
            
            // Update progress
            int answeredCount = 0;
            for (int i = 0; i < 112; i++)
            {
                if (questionResponses[i] > 0) answeredCount++;
            }
            currentProgress = answeredCount;

            // Save immediately
            if (Networking.LocalPlayer.IsValid())
            {
                var playerData = Networking.LocalPlayer.GetPlayerData();
                string key = $"{KEY_PREFIX}q_{questionIndex:D3}";
                playerData.SetFloat(key, response);
                playerData.SetFloat(KEY_PROGRESS, currentProgress);
                
                Debug.Log($"[PlayerDataManager] Saved Q{questionIndex + 1} = {response}, Progress: {currentProgress}/112");
            }
        }

        /// <summary>
        /// Get question response
        /// </summary>
        public int GetQuestionResponse(int questionIndex)
        {
            if (questionIndex < 0 || questionIndex >= 112) return 0;
            return questionResponses[questionIndex];
        }

        /// <summary>
        /// Get all question responses
        /// </summary>
        public int[] GetAllQuestionResponses()
        {
            var copy = new int[112];
            Array.Copy(questionResponses, copy, 112);
            return copy;
        }

        /// <summary>
        /// Update 30D vector data
        /// </summary>
        public void UpdateVector30D(float[] newVector)
        {
            if (newVector == null || newVector.Length != 30) return;

            Array.Copy(newVector, vector30D, 30);
        }

        /// <summary>
        /// Get 30D vector data
        /// </summary>
        public float[] GetVector30D()
        {
            var copy = new float[30];
            Array.Copy(vector30D, copy, 30);
            return copy;
        }

        /// <summary>
        /// Set flags (public sharing, provisional sharing, etc.)
        /// </summary>
        public void SetFlags(int newFlags)
        {
            flags = newFlags;
            if (Networking.LocalPlayer.IsValid())
            {
                var playerData = Networking.LocalPlayer.GetPlayerData();
                playerData.SetFloat(KEY_FLAGS, flags);
            }
        }

        /// <summary>
        /// Get next unanswered question index
        /// </summary>
        public int GetNextUnansweredQuestion()
        {
            for (int i = 0; i < 112; i++)
            {
                if (questionResponses[i] == 0) return i;
            }
            return -1; // All questions answered
        }

        /// <summary>
        /// Check if assessment is complete
        /// </summary>
        public bool IsAssessmentComplete()
        {
            return currentProgress >= 112;
        }

        /// <summary>
        /// Reset all player data
        /// </summary>
        public void ResetPlayerData()
        {
            Debug.Log("[PlayerDataManager] Resetting all player data");

            InitializeData();
            
            if (Networking.LocalPlayer.IsValid())
            {
                var playerData = Networking.LocalPlayer.GetPlayerData();
                
                // Clear all keys
                playerData.SetFloat(KEY_PROGRESS, 0);
                playerData.SetFloat(KEY_FLAGS, 0);
                playerData.SetFloat(KEY_LAST_ACTIVE, 0);
                
                for (int i = 0; i < 112; i++)
                {
                    string key = $"{KEY_PREFIX}q_{i:D3}";
                    playerData.SetFloat(key, 0);
                }
                
                for (int i = 0; i < 30; i++)
                {
                    string key = $"{KEY_PREFIX}v_{i:D2}";
                    playerData.SetFloat(key, 0);
                }
            }

            isDataLoaded = true;
            SendEventToTargets(onDataResetTargets, onDataResetEvent);
        }

        /// <summary>
        /// Get completion percentage (0-1)
        /// </summary>
        public float GetCompletionPercentage()
        {
            return (float)currentProgress / 112f;
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