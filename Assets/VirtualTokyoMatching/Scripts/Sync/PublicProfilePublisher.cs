using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using System;

namespace VirtualTokyoMatching
{
    /// <summary>
    /// Manages publishing condensed user profile data for public matching.
    /// Handles 30D->6D reduction, sync variable management, and privacy controls.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PublicProfilePublisher : UdonSharpBehaviour
    {
        [Header("Dependencies")]
        public PlayerDataManager playerDataManager;
        public VectorBuilder vectorBuilder;
        public VectorConfiguration vectorConfig;
        public ValuesSummaryGenerator summaryGenerator;

        [Header("Events")]
        public UdonBehaviour[] onProfilePublishedTargets;
        public string onProfilePublishedEvent = "OnProfilePublished";
        
        public UdonBehaviour[] onProfileHiddenTargets;
        public string onProfileHiddenEvent = "OnProfileHidden";

        [Header("Sync Data - 6D Reduced Vector")]
        [UdonSynced] public float red_0 = 0f;
        [UdonSynced] public float red_1 = 0f;
        [UdonSynced] public float red_2 = 0f;
        [UdonSynced] public float red_3 = 0f;
        [UdonSynced] public float red_4 = 0f;
        [UdonSynced] public float red_5 = 0f;

        [Header("Sync Data - Profile Info")]
        [UdonSynced] public string tags = "";
        [UdonSynced] public string headline = "";
        [UdonSynced] public string display_name = "";

        [Header("Sync Data - Status")]
        [UdonSynced] public bool partial_flag = true;
        [UdonSynced] public float progress_pct = 0f;
        [UdonSynced] public bool is_public = false;

        [Header("Local State")]
        [SerializeField] private bool isPublishing = false;
        [SerializeField] private float lastPublishTime = 0f;
        [SerializeField] private bool wasPublic = false;

        // Performance throttling
        private const float MIN_PUBLISH_INTERVAL = 1f; // Minimum seconds between publishes
        private const float AUTO_PUBLISH_INTERVAL = 5f; // Auto-publish interval when answering

        // Data caching
        private float[] lastPublished6D = new float[6];
        private float lastPublishedProgress = 0f;

        void Start()
        {
            // Initialize local player's display name
            if (Networking.IsOwner(gameObject) && Networking.LocalPlayer.IsValid())
            {
                display_name = Networking.LocalPlayer.displayName;
            }

            // Clear sync data initially
            ClearSyncData();
        }

        public void OnPlayerDataLoaded()
        {
            if (!Networking.IsOwner(gameObject)) return;

            // Update public sharing status from PlayerData
            bool shouldBePublic = playerDataManager != null && playerDataManager.IsPublicSharingEnabled;
            
            if (shouldBePublic)
            {
                PublishProfile();
            }
            else
            {
                HideProfile();
            }
        }

        public void OnVectorUpdated()
        {
            if (!Networking.IsOwner(gameObject) || !is_public) return;

            // Throttle auto-updates during assessment
            float currentTime = Time.time;
            if (currentTime - lastPublishTime < AUTO_PUBLISH_INTERVAL) return;

            PublishProfile();
        }

        public void OnVectorFinalized()
        {
            if (!Networking.IsOwner(gameObject)) return;

            // Always publish when vector is finalized
            PublishProfile();
        }

        /// <summary>
        /// Enable public sharing and publish profile
        /// </summary>
        public void EnablePublicSharing()
        {
            if (!Networking.IsOwner(gameObject)) return;

            // Update PlayerData flags
            if (playerDataManager != null)
            {
                int flags = playerDataManager.Flags;
                flags |= 1; // Set public sharing bit
                playerDataManager.SetFlags(flags);
            }

            PublishProfile();
        }

        /// <summary>
        /// Disable public sharing and hide profile
        /// </summary>
        public void DisablePublicSharing()
        {
            if (!Networking.IsOwner(gameObject)) return;

            // Update PlayerData flags
            if (playerDataManager != null)
            {
                int flags = playerDataManager.Flags;
                flags &= ~1; // Clear public sharing bit
                playerDataManager.SetFlags(flags);
            }

            HideProfile();
        }

        /// <summary>
        /// Publish current profile to sync variables
        /// </summary>
        public void PublishProfile()
        {
            if (!Networking.IsOwner(gameObject) || isPublishing) return;

            // Check if we have data to publish
            if (vectorBuilder == null || !vectorBuilder.isActiveAndEnabled) return;

            float currentTime = Time.time;
            if (currentTime - lastPublishTime < MIN_PUBLISH_INTERVAL) return;

            isPublishing = true;

            try
            {
                // Get 30D vector
                float[] vector30D = vectorBuilder.GetNormalizedVector();
                if (vector30D == null || vector30D.Length != 30)
                {
                    Debug.LogWarning("[PublicProfilePublisher] Invalid 30D vector");
                    return;
                }

                // Reduce to 6D
                float[] vector6D = ReduceTo6D(vector30D);
                
                // Update 6D sync variables
                red_0 = vector6D[0];
                red_1 = vector6D[1];
                red_2 = vector6D[2];
                red_3 = vector6D[3];
                red_4 = vector6D[4];
                red_5 = vector6D[5];

                // Update profile info
                if (summaryGenerator != null)
                {
                    tags = string.Join(",", summaryGenerator.GenerateTags(vector30D));
                    headline = summaryGenerator.GenerateHeadline(vector30D);
                }

                // Update status
                partial_flag = vectorBuilder.IsProvisional();
                progress_pct = vectorBuilder.GetCompletionRatio();
                is_public = true;

                // Update display name
                if (Networking.LocalPlayer.IsValid())
                {
                    display_name = Networking.LocalPlayer.displayName;
                }

                // Request sync
                RequestSerialization();
                
                lastPublishTime = currentTime;
                Array.Copy(vector6D, lastPublished6D, 6);
                lastPublishedProgress = progress_pct;

                SendEventToTargets(onProfilePublishedTargets, onProfilePublishedEvent);
                
                Debug.Log($"[PublicProfilePublisher] Published profile - Progress: {(progress_pct * 100):F1}%, Provisional: {partial_flag}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PublicProfilePublisher] Error publishing profile: {e.Message}");
            }
            finally
            {
                isPublishing = false;
            }
        }

        /// <summary>
        /// Hide profile by clearing sync variables
        /// </summary>
        public void HideProfile()
        {
            if (!Networking.IsOwner(gameObject)) return;

            ClearSyncData();
            RequestSerialization();

            SendEventToTargets(onProfileHiddenTargets, onProfileHiddenEvent);
            
            Debug.Log("[PublicProfilePublisher] Profile hidden");
        }

        private void ClearSyncData()
        {
            red_0 = red_1 = red_2 = red_3 = red_4 = red_5 = 0f;
            tags = "";
            headline = "";
            partial_flag = true;
            progress_pct = 0f;
            is_public = false;
        }

        /// <summary>
        /// Reduce 30D vector to 6D using projection matrix
        /// </summary>
        private float[] ReduceTo6D(float[] vector30D)
        {
            var result = new float[6];

            if (vectorConfig == null)
            {
                // Fallback: simple averaging into 6 groups
                for (int i = 0; i < 6; i++)
                {
                    float sum = 0f;
                    int count = 0;
                    
                    for (int j = i * 5; j < (i + 1) * 5 && j < 30; j++)
                    {
                        sum += vector30D[j];
                        count++;
                    }
                    
                    result[i] = count > 0 ? sum / count : 0f;
                }
            }
            else
            {
                // Use configured projection matrix
                for (int reducedAxis = 0; reducedAxis < 6; reducedAxis++)
                {
                    float sum = 0f;
                    for (int axis30 = 0; axis30 < 30; axis30++)
                    {
                        sum += vector30D[axis30] * vectorConfig.GetProjectionWeight(axis30, reducedAxis);
                    }
                    result[reducedAxis] = Mathf.Clamp(sum, -1f, 1f);
                }
            }

            return result;
        }

        /// <summary>
        /// Get current 6D reduced vector
        /// </summary>
        public float[] GetCurrent6DVector()
        {
            return new float[] { red_0, red_1, red_2, red_3, red_4, red_5 };
        }

        /// <summary>
        /// Get current tags array
        /// </summary>
        public string[] GetCurrentTags()
        {
            if (string.IsNullOrEmpty(tags)) return new string[0];
            
            return tags.Split(',');
        }

        /// <summary>
        /// Check if this profile is currently public
        /// </summary>
        public bool IsPublic()
        {
            return is_public;
        }

        /// <summary>
        /// Check if this profile is provisional
        /// </summary>
        public bool IsProvisional()
        {
            return partial_flag;
        }

        /// <summary>
        /// Get completion percentage (0-100)
        /// </summary>
        public float GetCompletionPercentage()
        {
            return progress_pct * 100f;
        }

        /// <summary>
        /// Get profile owner's display name
        /// </summary>
        public string GetDisplayName()
        {
            return display_name;
        }

        /// <summary>
        /// Get profile headline
        /// </summary>
        public string GetHeadline()
        {
            return headline;
        }

        /// <summary>
        /// Force refresh profile (for debugging)
        /// </summary>
        public void ForceRefreshProfile()
        {
            if (!Networking.IsOwner(gameObject)) return;

            lastPublishTime = 0f; // Reset throttle
            
            if (is_public)
            {
                PublishProfile();
            }
        }

        /// <summary>
        /// Check if profile data has changed significantly
        /// </summary>
        private bool HasSignificantChanges(float[] newVector6D, float newProgress)
        {
            // Check 6D vector changes
            for (int i = 0; i < 6; i++)
            {
                if (Mathf.Abs(newVector6D[i] - lastPublished6D[i]) > 0.1f)
                    return true;
            }

            // Check progress changes
            if (Mathf.Abs(newProgress - lastPublishedProgress) > 0.05f) // 5% change
                return true;

            return false;
        }

        public override void OnDeserialization()
        {
            // Handle when sync data is received from network
            // This is automatically called when sync variables are updated
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            // Clear data when ownership changes to prevent stale data
            if (Networking.IsOwner(gameObject))
            {
                ClearSyncData();
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