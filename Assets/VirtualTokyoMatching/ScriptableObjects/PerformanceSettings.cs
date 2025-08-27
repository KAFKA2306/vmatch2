using UnityEngine;

namespace VirtualTokyoMatching
{
    /// <summary>
    /// ScriptableObject containing performance tuning parameters for distributed processing
    /// and frame rate optimization.
    /// </summary>
    [CreateAssetMenu(fileName = "PerformanceSettings", menuName = "VTM/Performance Settings")]
    public class PerformanceSettings : ScriptableObject
    {
        [Header("Frame Rate Limits")]
        [Range(1, 100)]
        public int maxCalculationsPerFrame = 10; // K value for PerfGuard

        [Range(30, 120)]
        public int targetFrameRatePC = 72;

        [Range(30, 90)]
        public int targetFrameRateQuest = 60;

        [Header("Calculation Thresholds")]
        [Range(1, 10)]
        public int maxFullRecalculationsPC = 5; // seconds

        [Range(1, 15)]
        public int maxFullRecalculationsQuest = 10; // seconds

        [Range(0.1f, 2f)]
        public float incrementalUpdateInterval = 0.5f; // seconds between incremental updates

        [Header("Memory Optimization")]
        [Range(10, 100)]
        public int maxCachedProfiles = 50;

        [Range(1, 20)]
        public int maxRecommendationsToCalculate = 10;

        [Range(1, 5)]
        public int recommendationsToDisplay = 3;

        [Header("Network Optimization")]
        [Range(0.1f, 5f)]
        public float syncDataUpdateRate = 1f; // seconds between sync updates

        [Range(1, 10)]
        public int maxSyncUpdatesPerFrame = 3;

        [Header("Quality Settings")]
        [Range(512, 4096)]
        public int maxTextureResolutionPC = 2048;

        [Range(256, 1024)]
        public int maxTextureResolutionQuest = 1024;

        public bool useMipmaps = true;
        public bool enableLightBaking = true;

        [Header("Debug Settings")]
        public bool enablePerformanceLogging = false;
        public bool showFrameTimeUI = false;

        [Range(1f, 60f)]
        public float performanceLogInterval = 5f; // seconds

        /// <summary>
        /// Get max calculations per frame based on current platform
        /// </summary>
        public int GetMaxCalculationsPerFrame()
        {
#if UNITY_ANDROID
            return Mathf.Max(1, maxCalculationsPerFrame / 2); // Reduce for Quest
#else
            return maxCalculationsPerFrame;
#endif
        }

        /// <summary>
        /// Get target frame rate based on current platform
        /// </summary>
        public int GetTargetFrameRate()
        {
#if UNITY_ANDROID
            return targetFrameRateQuest;
#else
            return targetFrameRatePC;
#endif
        }

        /// <summary>
        /// Get max full recalculation time based on current platform
        /// </summary>
        public int GetMaxFullRecalculationTime()
        {
#if UNITY_ANDROID
            return maxFullRecalculationsQuest;
#else
            return maxFullRecalculationsPC;
#endif
        }

        /// <summary>
        /// Get max texture resolution based on current platform
        /// </summary>
        public int GetMaxTextureResolution()
        {
#if UNITY_ANDROID
            return maxTextureResolutionQuest;
#else
            return maxTextureResolutionPC;
#endif
        }

        /// <summary>
        /// Check if we should use performance optimizations for current platform
        /// </summary>
        public bool ShouldOptimizeForMobile()
        {
#if UNITY_ANDROID
            return true;
#else
            return false;
#endif
        }

        private void OnValidate()
        {
            // Ensure Quest settings are not higher than PC settings
            targetFrameRateQuest = Mathf.Min(targetFrameRateQuest, targetFrameRatePC);
            maxFullRecalculationsQuest = Mathf.Max(maxFullRecalculationsQuest, maxFullRecalculationsPC);
            maxTextureResolutionQuest = Mathf.Min(maxTextureResolutionQuest, maxTextureResolutionPC);

            // Ensure reasonable values
            maxCalculationsPerFrame = Mathf.Max(1, maxCalculationsPerFrame);
            recommendationsToDisplay = Mathf.Min(recommendationsToDisplay, maxRecommendationsToCalculate);
            maxRecommendationsToCalculate = Mathf.Min(maxRecommendationsToCalculate, maxCachedProfiles);
        }
    }
}