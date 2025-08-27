using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace VirtualTokyoMatching
{
    /// <summary>
    /// Performance monitoring and throttling system to maintain target frame rates.
    /// Manages distributed processing budgets across all systems.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PerfGuard : UdonSharpBehaviour
    {
        [Header("Dependencies")]
        public PerformanceSettings performanceSettings;

        [Header("Frame Rate Monitoring")]
        [SerializeField] private float currentFPS = 60f;
        [SerializeField] private float averageFPS = 60f;
        [SerializeField] private float targetFPS = 72f;
        [SerializeField] private bool isUnderperforming = false;

        [Header("Calculation Budget")]
        [SerializeField] private int maxCalculationsPerFrame = 10;
        [SerializeField] private int calculationsUsedThisFrame = 0;
        [SerializeField] private int totalCalculationsThisSecond = 0;

        [Header("Performance History")]
        private float[] fpsHistory = new float[60]; // Last 60 frames
        private int fpsHistoryIndex = 0;
        private float lastFPSUpdateTime = 0f;
        private float lastSecondTime = 0f;

        [Header("Adaptive Settings")]
        [SerializeField] private float performanceScore = 1f; // 0-1, lower = worse performance
        [SerializeField] private int adaptiveCalculationBudget = 10;

        // Events
        public UdonBehaviour[] onPerformanceChangedTargets;
        public string onPerformanceChangedEvent = "OnPerformanceChanged";

        void Start()
        {
            LoadSettings();
            InitializeMonitoring();
        }

        void Update()
        {
            UpdateFPSMonitoring();
            UpdateCalculationBudget();
            CheckPerformanceThresholds();
        }

        private void LoadSettings()
        {
            if (performanceSettings != null)
            {
                maxCalculationsPerFrame = performanceSettings.GetMaxCalculationsPerFrame();
                targetFPS = performanceSettings.GetTargetFrameRate();
                adaptiveCalculationBudget = maxCalculationsPerFrame;
            }
            else
            {
                // Fallback defaults
                maxCalculationsPerFrame = 10;
                targetFPS = 72f;
                adaptiveCalculationBudget = maxCalculationsPerFrame;
            }

            Debug.Log($"[PerfGuard] Initialized - Target FPS: {targetFPS}, Max calculations/frame: {maxCalculationsPerFrame}");
        }

        private void InitializeMonitoring()
        {
            for (int i = 0; i < fpsHistory.Length; i++)
            {
                fpsHistory[i] = targetFPS;
            }

            currentFPS = targetFPS;
            averageFPS = targetFPS;
            lastFPSUpdateTime = Time.time;
            lastSecondTime = Time.time;
        }

        private void UpdateFPSMonitoring()
        {
            float currentTime = Time.time;
            float deltaTime = Time.unscaledDeltaTime;

            // Calculate current FPS
            if (deltaTime > 0f)
            {
                currentFPS = 1f / deltaTime;
            }

            // Update FPS history every frame
            fpsHistory[fpsHistoryIndex] = currentFPS;
            fpsHistoryIndex = (fpsHistoryIndex + 1) % fpsHistory.Length;

            // Calculate average FPS every 0.1 seconds
            if (currentTime - lastFPSUpdateTime >= 0.1f)
            {
                CalculateAverageFPS();
                UpdatePerformanceScore();
                lastFPSUpdateTime = currentTime;
            }

            // Reset per-second counters
            if (currentTime - lastSecondTime >= 1f)
            {
                totalCalculationsThisSecond = 0;
                lastSecondTime = currentTime;
            }
        }

        private void CalculateAverageFPS()
        {
            float sum = 0f;
            for (int i = 0; i < fpsHistory.Length; i++)
            {
                sum += fpsHistory[i];
            }
            averageFPS = sum / fpsHistory.Length;
        }

        private void UpdatePerformanceScore()
        {
            // Calculate performance score based on how close we are to target FPS
            if (averageFPS >= targetFPS)
            {
                performanceScore = 1f; // Perfect performance
            }
            else
            {
                // Linear scaling from target down to half target
                float minFPS = targetFPS * 0.5f;
                performanceScore = Mathf.Clamp01((averageFPS - minFPS) / (targetFPS - minFPS));
            }
        }

        private void UpdateCalculationBudget()
        {
            // Adapt calculation budget based on performance
            if (performanceScore >= 0.9f)
            {
                // Good performance, can use full budget
                adaptiveCalculationBudget = maxCalculationsPerFrame;
            }
            else if (performanceScore >= 0.7f)
            {
                // Moderate performance, reduce budget slightly
                adaptiveCalculationBudget = Mathf.Max(1, (int)(maxCalculationsPerFrame * 0.7f));
            }
            else if (performanceScore >= 0.5f)
            {
                // Poor performance, significant reduction
                adaptiveCalculationBudget = Mathf.Max(1, (int)(maxCalculationsPerFrame * 0.4f));
            }
            else
            {
                // Very poor performance, minimal budget
                adaptiveCalculationBudget = 1;
            }

            // Reset frame counter at start of each frame
            if (Time.frameCount != lastFrameCount)
            {
                calculationsUsedThisFrame = 0;
                lastFrameCount = Time.frameCount;
            }
        }

        private int lastFrameCount = -1;

        private void CheckPerformanceThresholds()
        {
            bool wasUnderperforming = isUnderperforming;
            isUnderperforming = averageFPS < (targetFPS * 0.8f); // 20% below target

            if (wasUnderperforming != isUnderperforming)
            {
                SendEventToTargets(onPerformanceChangedTargets, onPerformanceChangedEvent);
                
                if (isUnderperforming)
                {
                    Debug.LogWarning($"[PerfGuard] Performance below threshold - FPS: {averageFPS:F1}, Target: {targetFPS}");
                }
                else
                {
                    Debug.Log($"[PerfGuard] Performance recovered - FPS: {averageFPS:F1}");
                }
            }
        }

        /// <summary>
        /// Request calculation budget from the frame allocation
        /// </summary>
        /// <param name="requestedCalculations">Number of calculations needed</param>
        /// <returns>Number of calculations granted (may be less than requested)</returns>
        public int RequestCalculationBudget(int requestedCalculations)
        {
            int available = adaptiveCalculationBudget - calculationsUsedThisFrame;
            int granted = Mathf.Min(requestedCalculations, available);
            
            calculationsUsedThisFrame += granted;
            totalCalculationsThisSecond += granted;
            
            return granted;
        }

        /// <summary>
        /// Check if we can perform any calculations this frame
        /// </summary>
        public bool CanCalculateThisFrame()
        {
            return calculationsUsedThisFrame < adaptiveCalculationBudget;
        }

        /// <summary>
        /// Get maximum calculations allowed per frame
        /// </summary>
        public int GetMaxCalculationsThisFrame()
        {
            return adaptiveCalculationBudget - calculationsUsedThisFrame;
        }

        /// <summary>
        /// Get current frame rate
        /// </summary>
        public float GetCurrentFPS()
        {
            return currentFPS;
        }

        /// <summary>
        /// Get average frame rate over recent history
        /// </summary>
        public float GetAverageFPS()
        {
            return averageFPS;
        }

        /// <summary>
        /// Get target frame rate
        /// </summary>
        public float GetTargetFPS()
        {
            return targetFPS;
        }

        /// <summary>
        /// Get performance score (0-1, higher is better)
        /// </summary>
        public float GetPerformanceScore()
        {
            return performanceScore;
        }

        /// <summary>
        /// Check if performance is below acceptable threshold
        /// </summary>
        public bool IsUnderperforming()
        {
            return isUnderperforming;
        }

        /// <summary>
        /// Get calculations used this frame
        /// </summary>
        public int GetCalculationsUsedThisFrame()
        {
            return calculationsUsedThisFrame;
        }

        /// <summary>
        /// Get total calculations this second
        /// </summary>
        public int GetCalculationsThisSecond()
        {
            return totalCalculationsThisSecond;
        }

        /// <summary>
        /// Get current calculation budget for this frame
        /// </summary>
        public int GetCurrentCalculationBudget()
        {
            return adaptiveCalculationBudget;
        }

        /// <summary>
        /// Force performance recalculation (for debugging)
        /// </summary>
        public void RecalculatePerformanceMetrics()
        {
            CalculateAverageFPS();
            UpdatePerformanceScore();
            UpdateCalculationBudget();
            CheckPerformanceThresholds();
        }

        /// <summary>
        /// Reset performance history (for debugging)
        /// </summary>
        public void ResetPerformanceHistory()
        {
            InitializeMonitoring();
            Debug.Log("[PerfGuard] Performance history reset");
        }

        /// <summary>
        /// Get performance statistics as formatted string
        /// </summary>
        public string GetPerformanceStats()
        {
            return $"FPS: {averageFPS:F1}/{targetFPS:F0} | Score: {(performanceScore * 100):F0}% | " +
                   $"Budget: {calculationsUsedThisFrame}/{adaptiveCalculationBudget} | " +
                   $"Underperforming: {(isUnderperforming ? "YES" : "NO")}";
        }

        /// <summary>
        /// Adjust target FPS at runtime (for testing)
        /// </summary>
        public void SetTargetFPS(float newTarget)
        {
            targetFPS = Mathf.Clamp(newTarget, 30f, 144f);
            Debug.Log($"[PerfGuard] Target FPS changed to {targetFPS}");
        }

        /// <summary>
        /// Adjust max calculations per frame at runtime (for tuning)
        /// </summary>
        public void SetMaxCalculationsPerFrame(int newMax)
        {
            maxCalculationsPerFrame = Mathf.Clamp(newMax, 1, 100);
            adaptiveCalculationBudget = maxCalculationsPerFrame;
            Debug.Log($"[PerfGuard] Max calculations per frame changed to {maxCalculationsPerFrame}");
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

        // Debug UI (optional)
        void OnGUI()
        {
            if (performanceSettings != null && performanceSettings.showFrameTimeUI)
            {
                GUI.Box(new Rect(10, 10, 300, 100), "Performance Monitor");
                GUI.Label(new Rect(20, 30, 280, 20), GetPerformanceStats());
                GUI.Label(new Rect(20, 50, 280, 20), $"Frame Time: {(Time.unscaledDeltaTime * 1000):F1}ms");
                GUI.Label(new Rect(20, 70, 280, 20), $"Calculations/sec: {totalCalculationsThisSecond}");
            }
        }
    }
}