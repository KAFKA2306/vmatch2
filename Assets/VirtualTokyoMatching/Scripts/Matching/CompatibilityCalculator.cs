using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using System.Collections.Generic;

namespace VirtualTokyoMatching
{
    /// <summary>
    /// Calculates compatibility between users based on 6D reduced vectors.
    /// Implements distributed processing with performance throttling.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CompatibilityCalculator : UdonSharpBehaviour
    {
        [System.Serializable]
        public class CompatibilityResult
        {
            public VRCPlayerApi player;
            public PublicProfilePublisher profile;
            public float similarity;
            public float confidence;
            public bool isProvisional;
            public float completionPercentage;
            
            public CompatibilityResult(VRCPlayerApi p, PublicProfilePublisher prof, float sim, float conf, bool prov, float comp)
            {
                player = p;
                profile = prof;
                similarity = sim;
                confidence = conf;
                isProvisional = prov;
                completionPercentage = comp;
            }
        }

        [Header("Dependencies")]
        public PerfGuard perfGuard;
        public PlayerDataManager playerDataManager;
        public VectorConfiguration vectorConfig;

        [Header("Events")]
        public UdonBehaviour[] onCalculationCompleteTargets;
        public string onCalculationCompleteEvent = "OnCompatibilityCalculated";
        
        public UdonBehaviour[] onRecommendationsUpdatedTargets;
        public string onRecommendationsUpdatedEvent = "OnRecommendationsUpdated";

        [Header("Settings")]
        [Range(1, 10)]
        public int maxRecommendations = 3;
        
        [Range(0.1f, 5f)]
        public float recalculationInterval = 2f;
        
        [Range(0.001f, 0.1f)]
        public float epsilon = 0.001f; // For numerical stability

        [Header("Status")]
        [SerializeField] private bool isCalculating = false;
        [SerializeField] private int currentCalculationIndex = 0;
        [SerializeField] private float lastCalculationTime = 0f;

        // Data storage
        private PublicProfilePublisher[] allProfiles = new PublicProfilePublisher[0];
        private CompatibilityResult[] topRecommendations = new CompatibilityResult[0];
        private CompatibilityResult[] allResults = new CompatibilityResult[0];
        
        // Calculation queue
        private List<PublicProfilePublisher> calculationQueue = new List<PublicProfilePublisher>();
        private int queueIndex = 0;

        // Performance tracking
        private int calculationsThisFrame = 0;
        private bool needsFullRecalculation = true;

        void Start()
        {
            // Initialize arrays
            topRecommendations = new CompatibilityResult[maxRecommendations];
            
            // Start periodic updates
            SendCustomEventDelayedSeconds(nameof(PeriodicUpdate), recalculationInterval);
        }

        public void PeriodicUpdate()
        {
            // Check if we need to recalculate
            float currentTime = Time.time;
            if (currentTime - lastCalculationTime >= recalculationInterval)
            {
                TriggerRecalculation();
            }

            // Schedule next update
            SendCustomEventDelayedSeconds(nameof(PeriodicUpdate), recalculationInterval);
        }

        /// <summary>
        /// Trigger full recalculation (called when profiles change)
        /// </summary>
        public void TriggerRecalculation()
        {
            if (isCalculating) return;

            needsFullRecalculation = true;
            StartCalculation();
        }

        /// <summary>
        /// Trigger incremental update (when local vector changes)
        /// </summary>
        public void TriggerIncrementalUpdate()
        {
            if (isCalculating) return;

            // Only recalculate if we have existing profiles
            if (allProfiles != null && allProfiles.Length > 0)
            {
                StartCalculation();
            }
        }

        private void StartCalculation()
        {
            if (isCalculating || !CanCalculate()) return;

            // Find all public profiles
            RefreshProfileList();
            
            if (allProfiles.Length == 0)
            {
                // No profiles to calculate
                ClearRecommendations();
                return;
            }

            isCalculating = true;
            currentCalculationIndex = 0;
            calculationsThisFrame = 0;
            lastCalculationTime = Time.time;

            // Prepare results array
            allResults = new CompatibilityResult[allProfiles.Length];

            Debug.Log($"[CompatibilityCalculator] Starting calculation for {allProfiles.Length} profiles");

            // Start distributed calculation
            ContinueCalculation();
        }

        public void ContinueCalculation()
        {
            if (!isCalculating) return;

            // Check frame budget
            int maxCalculationsThisFrame = perfGuard != null ? perfGuard.GetMaxCalculationsThisFrame() : 5;
            
            while (currentCalculationIndex < allProfiles.Length && calculationsThisFrame < maxCalculationsThisFrame)
            {
                CalculateSingleCompatibility(currentCalculationIndex);
                currentCalculationIndex++;
                calculationsThisFrame++;
            }

            if (currentCalculationIndex >= allProfiles.Length)
            {
                // Calculation complete
                FinishCalculation();
            }
            else
            {
                // Continue next frame
                calculationsThisFrame = 0;
                SendCustomEventDelayedFrames(nameof(ContinueCalculation), 1);
            }
        }

        private void CalculateSingleCompatibility(int index)
        {
            var profile = allProfiles[index];
            if (profile == null || !profile.IsPublic())
            {
                allResults[index] = null;
                return;
            }

            // Get profile owner
            var player = Networking.GetOwner(profile.gameObject);
            if (player == null || !player.IsValid() || player == Networking.LocalPlayer)
            {
                allResults[index] = null;
                return;
            }

            // Get vectors
            float[] myVector = GetMyVector6D();
            float[] theirVector = profile.GetCurrent6DVector();

            if (myVector == null || theirVector == null)
            {
                allResults[index] = null;
                return;
            }

            // Calculate cosine similarity
            float similarity = CalculateCosineSimilarity(myVector, theirVector);
            
            // Calculate confidence based on vector magnitudes
            float myMagnitude = CalculateMagnitude(myVector);
            float theirMagnitude = CalculateMagnitude(theirVector);
            float confidence = Mathf.Min(myMagnitude, theirMagnitude);

            // Create result
            allResults[index] = new CompatibilityResult(
                player,
                profile,
                similarity,
                confidence,
                profile.IsProvisional(),
                profile.GetCompletionPercentage()
            );
        }

        private void FinishCalculation()
        {
            isCalculating = false;

            // Filter and sort results
            var validResults = new List<CompatibilityResult>();
            
            for (int i = 0; i < allResults.Length; i++)
            {
                if (allResults[i] != null && allResults[i].similarity > 0.1f) // Minimum similarity threshold
                {
                    validResults.Add(allResults[i]);
                }
            }

            // Sort by similarity (descending)
            validResults.Sort((a, b) => b.similarity.CompareTo(a.similarity));

            // Update top recommendations
            for (int i = 0; i < maxRecommendations; i++)
            {
                topRecommendations[i] = i < validResults.Count ? validResults[i] : null;
            }

            // Fire events
            SendEventToTargets(onCalculationCompleteTargets, onCalculationCompleteEvent);
            SendEventToTargets(onRecommendationsUpdatedTargets, onRecommendationsUpdatedEvent);

            Debug.Log($"[CompatibilityCalculator] Calculation complete. Found {validResults.Count} valid matches, top {GetValidRecommendationCount()} recommended");
        }

        private void RefreshProfileList()
        {
            var profiles = new List<PublicProfilePublisher>();
            
            // Find all PublicProfilePublisher components in the scene
            var allPublishers = FindObjectsOfType<PublicProfilePublisher>();
            
            foreach (var publisher in allPublishers)
            {
                if (publisher != null && publisher.IsPublic())
                {
                    var owner = Networking.GetOwner(publisher.gameObject);
                    if (owner != null && owner.IsValid() && owner != Networking.LocalPlayer)
                    {
                        profiles.Add(publisher);
                    }
                }
            }

            allProfiles = profiles.ToArray();
        }

        private float[] GetMyVector6D()
        {
            // Try to find our own PublicProfilePublisher
            var myPublisher = GetComponentInChildren<PublicProfilePublisher>();
            if (myPublisher != null && myPublisher.IsPublic())
            {
                return myPublisher.GetCurrent6DVector();
            }

            // Fallback: calculate from VectorBuilder if available
            var vectorBuilder = FindObjectOfType<VectorBuilder>();
            if (vectorBuilder != null)
            {
                var vector30D = vectorBuilder.GetNormalizedVector();
                return ReduceTo6D(vector30D);
            }

            return null;
        }

        private float[] ReduceTo6D(float[] vector30D)
        {
            if (vector30D == null || vector30D.Length != 30 || vectorConfig == null) return null;

            // Use VectorConfiguration projection matrix for proper 30D->6D reduction
            var result = new float[6];
            for (int i = 0; i < 6; i++)
            {
                float sum = 0f;
                for (int j = 0; j < 30; j++)
                {
                    float projectionWeight = vectorConfig.GetProjectionWeight(j, i);
                    sum += vector30D[j] * projectionWeight;
                }
                result[i] = sum;
            }
            return result;
        }

        private float CalculateCosineSimilarity(float[] vectorA, float[] vectorB)
        {
            if (vectorA.Length != vectorB.Length) return 0f;

            float dotProduct = 0f;
            float magnitudeA = 0f;
            float magnitudeB = 0f;

            for (int i = 0; i < vectorA.Length; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                magnitudeA += vectorA[i] * vectorA[i];
                magnitudeB += vectorB[i] * vectorB[i];
            }

            magnitudeA = Mathf.Sqrt(magnitudeA);
            magnitudeB = Mathf.Sqrt(magnitudeB);

            float denominator = magnitudeA * magnitudeB;
            if (denominator < epsilon) return 0f;

            float similarity = dotProduct / denominator;
            
            // Convert from [-1, 1] to [0, 1] for easier interpretation
            return (similarity + 1f) / 2f;
        }

        private float CalculateMagnitude(float[] vector)
        {
            float sum = 0f;
            for (int i = 0; i < vector.Length; i++)
            {
                sum += vector[i] * vector[i];
            }
            return Mathf.Sqrt(sum);
        }

        private bool CanCalculate()
        {
            // Check if we have our own data
            if (playerDataManager == null || !playerDataManager.IsDataLoaded)
                return false;

            // Check if we have enough progress to calculate
            float progress = playerDataManager.GetCompletionPercentage();
            return progress > 0.1f; // At least 10% complete
        }

        private void ClearRecommendations()
        {
            for (int i = 0; i < topRecommendations.Length; i++)
            {
                topRecommendations[i] = null;
            }

            SendEventToTargets(onRecommendationsUpdatedTargets, onRecommendationsUpdatedEvent);
        }

        /// <summary>
        /// Get top compatibility recommendations
        /// </summary>
        public CompatibilityResult[] GetTopRecommendations()
        {
            var results = new CompatibilityResult[maxRecommendations];
            for (int i = 0; i < maxRecommendations; i++)
            {
                results[i] = topRecommendations[i];
            }
            return results;
        }

        /// <summary>
        /// Get specific recommendation by index
        /// </summary>
        public CompatibilityResult GetRecommendation(int index)
        {
            if (index >= 0 && index < topRecommendations.Length)
                return topRecommendations[index];
            return null;
        }

        /// <summary>
        /// Get count of valid recommendations
        /// </summary>
        public int GetValidRecommendationCount()
        {
            int count = 0;
            for (int i = 0; i < topRecommendations.Length; i++)
            {
                if (topRecommendations[i] != null) count++;
            }
            return count;
        }

        /// <summary>
        /// Check if calculation is in progress
        /// </summary>
        public bool IsCalculating()
        {
            return isCalculating;
        }

        /// <summary>
        /// Get calculation progress (0-1)
        /// </summary>
        public float GetCalculationProgress()
        {
            if (!isCalculating || allProfiles.Length == 0) return 1f;
            return (float)currentCalculationIndex / allProfiles.Length;
        }

        /// <summary>
        /// Force recalculation (for debugging)
        /// </summary>
        public void ForceRecalculation()
        {
            if (isCalculating) return;
            
            needsFullRecalculation = true;
            lastCalculationTime = 0f;
            TriggerRecalculation();
        }

        // Event handlers for profile changes
        public void OnProfilePublished()
        {
            SendCustomEventDelayedSeconds(nameof(TriggerRecalculation), 0.5f);
        }

        public void OnProfileHidden()
        {
            SendCustomEventDelayedSeconds(nameof(TriggerRecalculation), 0.5f);
        }

        public void OnVectorUpdated()
        {
            TriggerIncrementalUpdate();
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