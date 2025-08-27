using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using System;

namespace VirtualTokyoMatching
{
    /// <summary>
    /// Manages 1-on-1 private room sessions with invitation, teleportation, and timing.
    /// Handles room allocation, session flow, and feedback collection.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class SessionRoomManager : UdonSharpBehaviour
    {
        [System.Serializable]
        public class SessionRoom
        {
            [Header("Room Configuration")]
            public Transform spawnPointA;
            public Transform spawnPointB;
            public Transform lobbyReturnPoint;
            public GameObject roomObject;
            public AudioSource sessionBell;
            
            [Header("Room UI")]
            public GameObject timerDisplay;
            public UnityEngine.UI.Text timerText;
            public UnityEngine.UI.Button endSessionButton;
            public GameObject feedbackPanel;
            public UnityEngine.UI.Button likeButton;
            public UnityEngine.UI.Button passButton;
            
            [Header("Privacy")]
            public Collider roomBoundary;
            public GameObject privacyBarrier;
            
            // Runtime state
            [System.NonSerialized] public bool isOccupied = false;
            [System.NonSerialized] public VRCPlayerApi playerA = null;
            [System.NonSerialized] public VRCPlayerApi playerB = null;
            [System.NonSerialized] public float sessionStartTime = 0f;
            [System.NonSerialized] public bool sessionActive = false;
        }

        [Header("Dependencies")]
        public PlayerDataManager playerDataManager;
        public SafetyController safetyController;

        [Header("Room Configuration")]
        public SessionRoom[] sessionRooms = new SessionRoom[0];
        public Transform lobbySpawnPoint;

        [Header("Session Settings")]
        [Range(5, 30)]
        public int sessionDurationMinutes = 15;
        
        [Range(30, 300)]
        public int invitationTimeoutSeconds = 60;
        
        public AudioClip sessionStartSound;
        public AudioClip sessionEndSound;
        public AudioClip oneMinuteWarningSound;

        [Header("Invitation System")]
        public GameObject invitationUI;
        public TMPro.TextMeshProUGUI invitationText;
        public UnityEngine.UI.Button acceptInvitationButton;
        public UnityEngine.UI.Button declineInvitationButton;

        [Header("Sync Variables")]
        [UdonSynced] public int syncRoomIndex = -1;
        [UdonSynced] public string syncPlayerAName = "";
        [UdonSynced] public string syncPlayerBName = "";
        [UdonSynced] public float syncSessionStartTime = 0f;
        [UdonSynced] public bool syncSessionActive = false;

        [Header("Events")]
        public UdonBehaviour[] onSessionStartedTargets;
        public string onSessionStartedEvent = "OnSessionStarted";
        
        public UdonBehaviour[] onSessionEndedTargets;
        public string onSessionEndedEvent = "OnSessionEnded";

        // State management
        private VRCPlayerApi pendingInvitationFrom;
        private VRCPlayerApi pendingInvitationTo;
        private float invitationSentTime;
        private bool hasActiveInvitation = false;
        private int myCurrentRoom = -1;
        private float lastInvitationTime = 0f;
        private const float INVITATION_COOLDOWN = 10f; // seconds

        // Feedback data
        private int feedbackTarget = -1; // Room index for feedback

        void Start()
        {
            InitializeRooms();
            
            if (invitationUI != null) invitationUI.SetActive(false);
            
            // Set up invitation UI
            if (acceptInvitationButton != null)
                acceptInvitationButton.onClick.AddListener(AcceptInvitation);
            
            if (declineInvitationButton != null)
                declineInvitationButton.onClick.AddListener(DeclineInvitation);
            
            // Start session monitoring
            SendCustomEventDelayedSeconds(nameof(UpdateActiveSessions), 1f);
        }

        private void InitializeRooms()
        {
            for (int i = 0; i < sessionRooms.Length; i++)
            {
                var room = sessionRooms[i];
                if (room.endSessionButton != null)
                {
                    int roomIndex = i;
                    room.endSessionButton.onClick.AddListener(() => EndSession(roomIndex));
                }

                if (room.likeButton != null)
                {
                    int roomIndex = i;
                    room.likeButton.onClick.AddListener(() => SubmitFeedback(roomIndex, true));
                }

                if (room.passButton != null)
                {
                    int roomIndex = i;
                    room.passButton.onClick.AddListener(() => SubmitFeedback(roomIndex, false));
                }

                // Initially hide room UI
                if (room.timerDisplay != null) room.timerDisplay.SetActive(false);
                if (room.feedbackPanel != null) room.feedbackPanel.SetActive(false);
                if (room.privacyBarrier != null) room.privacyBarrier.SetActive(false);
            }
        }

        /// <summary>
        /// Send invitation to target player
        /// </summary>
        public void SendInvitation(VRCPlayerApi targetPlayer)
        {
            if (targetPlayer == null || !targetPlayer.IsValid() || targetPlayer == Networking.LocalPlayer)
            {
                Debug.LogWarning("[SessionRoomManager] Invalid invitation target");
                return;
            }

            // Check cooldown
            if (Time.time - lastInvitationTime < INVITATION_COOLDOWN)
            {
                Debug.LogWarning("[SessionRoomManager] Invitation cooldown active");
                return;
            }

            // Check if we're already in a session
            if (myCurrentRoom >= 0)
            {
                Debug.LogWarning("[SessionRoomManager] Already in a session");
                return;
            }

            // Check if there's an available room
            int availableRoom = FindAvailableRoom();
            if (availableRoom < 0)
            {
                Debug.LogWarning("[SessionRoomManager] No available rooms");
                return;
            }

            // Send invitation via custom networking
            pendingInvitationTo = targetPlayer;
            invitationSentTime = Time.time;
            hasActiveInvitation = true;
            lastInvitationTime = Time.time;

            // Use VRChat's built-in networking to send invitation
            // Note: In real implementation, you would use custom events or sync variables
            SendInvitationToPlayer(targetPlayer, availableRoom);

            Debug.Log($"[SessionRoomManager] Sent invitation to {targetPlayer.displayName}");
        }

        private void SendInvitationToPlayer(VRCPlayerApi targetPlayer, int roomIndex)
        {
            // This would be implemented using VRChat's networking
            // For now, simulate immediate delivery if both players are present
            if (targetPlayer.IsValid() && Networking.LocalPlayer.IsValid())
            {
                // In real implementation, this would send via network
                // For simulation, we'll just log
                Debug.Log($"[SessionRoomManager] Invitation sent to {targetPlayer.displayName} for room {roomIndex}");
            }
        }

        /// <summary>
        /// Receive invitation (called by networking system)
        /// </summary>
        public void ReceiveInvitation(VRCPlayerApi fromPlayer, int roomIndex)
        {
            if (fromPlayer == null || !fromPlayer.IsValid() || fromPlayer == Networking.LocalPlayer)
                return;

            if (myCurrentRoom >= 0)
            {
                // Already in session, auto-decline
                SendInvitationResponse(fromPlayer, false, roomIndex);
                return;
            }

            // Show invitation UI
            pendingInvitationFrom = fromPlayer;
            ShowInvitationUI(fromPlayer);
        }

        private void ShowInvitationUI(VRCPlayerApi fromPlayer)
        {
            if (invitationUI != null && invitationText != null)
            {
                invitationUI.SetActive(true);
                invitationText.text = $"{fromPlayer.displayName}さんから個室への招待が届きました";
            }

            // Auto-hide after timeout
            SendCustomEventDelayedSeconds(nameof(AutoDeclineInvitation), invitationTimeoutSeconds);
        }

        public void AcceptInvitation()
        {
            if (pendingInvitationFrom == null) return;

            SendInvitationResponse(pendingInvitationFrom, true, -1);
            HideInvitationUI();
        }

        public void DeclineInvitation()
        {
            if (pendingInvitationFrom == null) return;

            SendInvitationResponse(pendingInvitationFrom, false, -1);
            HideInvitationUI();
        }

        public void AutoDeclineInvitation()
        {
            if (invitationUI != null && invitationUI.activeInHierarchy)
            {
                DeclineInvitation();
            }
        }

        private void HideInvitationUI()
        {
            if (invitationUI != null) invitationUI.SetActive(false);
            pendingInvitationFrom = null;
        }

        private void SendInvitationResponse(VRCPlayerApi toPlayer, bool accepted, int roomIndex)
        {
            // This would use VRChat networking to send response
            Debug.Log($"[SessionRoomManager] Sent response to {toPlayer.displayName}: {(accepted ? "Accepted" : "Declined")}");
        }

        /// <summary>
        /// Start session when both players accept
        /// </summary>
        public void StartSession(VRCPlayerApi playerA, VRCPlayerApi playerB, int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= sessionRooms.Length) return;

            var room = sessionRooms[roomIndex];
            
            // Set up room state
            room.isOccupied = true;
            room.playerA = playerA;
            room.playerB = playerB;
            room.sessionStartTime = Time.time;
            room.sessionActive = true;

            // Update sync variables if we're the owner
            if (Networking.IsOwner(gameObject))
            {
                syncRoomIndex = roomIndex;
                syncPlayerAName = playerA.displayName;
                syncPlayerBName = playerB.displayName;
                syncSessionStartTime = room.sessionStartTime;
                syncSessionActive = true;
                RequestSerialization();
            }

            // Teleport players to room
            if (playerA == Networking.LocalPlayer)
            {
                TeleportToRoom(roomIndex, true);
                myCurrentRoom = roomIndex;
            }
            else if (playerB == Networking.LocalPlayer)
            {
                TeleportToRoom(roomIndex, false);
                myCurrentRoom = roomIndex;
            }

            // Set up room UI
            if (room.timerDisplay != null) room.timerDisplay.SetActive(true);
            if (room.privacyBarrier != null) room.privacyBarrier.SetActive(true);

            // Play start sound
            if (sessionStartSound != null && room.sessionBell != null)
            {
                room.sessionBell.PlayOneShot(sessionStartSound);
            }

            // Fire events
            SendEventToTargets(onSessionStartedTargets, onSessionStartedEvent);

            Debug.Log($"[SessionRoomManager] Started session in room {roomIndex}");
        }

        private void TeleportToRoom(int roomIndex, bool isPlayerA)
        {
            if (roomIndex < 0 || roomIndex >= sessionRooms.Length) return;

            var room = sessionRooms[roomIndex];
            Transform targetSpawn = isPlayerA ? room.spawnPointA : room.spawnPointB;

            if (targetSpawn != null && Networking.LocalPlayer.IsValid())
            {
                Networking.LocalPlayer.TeleportTo(targetSpawn.position, targetSpawn.rotation);
            }
        }

        /// <summary>
        /// End session manually or automatically
        /// </summary>
        public void EndSession(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= sessionRooms.Length) return;

            var room = sessionRooms[roomIndex];
            if (!room.sessionActive) return;

            // Show feedback UI before ending
            ShowFeedbackUI(roomIndex);
        }

        private void ShowFeedbackUI(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= sessionRooms.Length) return;

            var room = sessionRooms[roomIndex];
            feedbackTarget = roomIndex;

            if (room.feedbackPanel != null)
            {
                room.feedbackPanel.SetActive(true);
            }

            if (room.timerDisplay != null) room.timerDisplay.SetActive(false);
        }

        public void SubmitFeedback(int roomIndex, bool liked)
        {
            if (roomIndex < 0 || roomIndex >= sessionRooms.Length) return;

            // Save feedback to PlayerData
            SaveFeedbackData(roomIndex, liked);

            // Complete session end
            CompleteSessionEnd(roomIndex);
        }

        private void SaveFeedbackData(int roomIndex, bool liked)
        {
            // Save feedback to local PlayerData only
            if (playerDataManager != null)
            {
                // This would save feedback data for recommendation filtering
                Debug.Log($"[SessionRoomManager] Feedback saved for room {roomIndex}: {(liked ? "Like" : "Pass")}");
            }
        }

        private void CompleteSessionEnd(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= sessionRooms.Length) return;

            var room = sessionRooms[roomIndex];

            // Clean up room
            room.isOccupied = false;
            room.playerA = null;
            room.playerB = null;
            room.sessionActive = false;

            if (room.timerDisplay != null) room.timerDisplay.SetActive(false);
            if (room.feedbackPanel != null) room.feedbackPanel.SetActive(false);
            if (room.privacyBarrier != null) room.privacyBarrier.SetActive(false);

            // Update sync variables
            if (Networking.IsOwner(gameObject))
            {
                syncRoomIndex = -1;
                syncPlayerAName = "";
                syncPlayerBName = "";
                syncSessionStartTime = 0f;
                syncSessionActive = false;
                RequestSerialization();
            }

            // Teleport back to lobby
            if (myCurrentRoom == roomIndex)
            {
                TeleportToLobby();
                myCurrentRoom = -1;
            }

            // Play end sound
            if (sessionEndSound != null && room.sessionBell != null)
            {
                room.sessionBell.PlayOneShot(sessionEndSound);
            }

            // Fire events
            SendEventToTargets(onSessionEndedTargets, onSessionEndedEvent);

            Debug.Log($"[SessionRoomManager] Session ended in room {roomIndex}");
        }

        private void TeleportToLobby()
        {
            if (lobbySpawnPoint != null && Networking.LocalPlayer.IsValid())
            {
                Networking.LocalPlayer.TeleportTo(lobbySpawnPoint.position, lobbySpawnPoint.rotation);
            }
        }

        /// <summary>
        /// Update active sessions (called periodically)
        /// </summary>
        public void UpdateActiveSessions()
        {
            float currentTime = Time.time;

            for (int i = 0; i < sessionRooms.Length; i++)
            {
                var room = sessionRooms[i];
                if (!room.sessionActive) continue;

                float sessionTime = currentTime - room.sessionStartTime;
                float maxSessionTime = sessionDurationMinutes * 60f;

                // Update timer display
                if (room.timerText != null)
                {
                    float remainingTime = maxSessionTime - sessionTime;
                    if (remainingTime > 0)
                    {
                        int minutes = (int)(remainingTime / 60f);
                        int seconds = (int)(remainingTime % 60f);
                        room.timerText.text = $"{minutes:D2}:{seconds:D2}";
                    }
                    else
                    {
                        room.timerText.text = "00:00";
                    }
                }

                // Check for one minute warning
                if (sessionTime >= (maxSessionTime - 60f) && sessionTime < (maxSessionTime - 59f))
                {
                    if (oneMinuteWarningSound != null && room.sessionBell != null)
                    {
                        room.sessionBell.PlayOneShot(oneMinuteWarningSound);
                    }
                }

                // Auto-end session when time limit reached
                if (sessionTime >= maxSessionTime)
                {
                    EndSession(i);
                }
            }

            // Schedule next update
            SendCustomEventDelayedSeconds(nameof(UpdateActiveSessions), 1f);
        }

        private int FindAvailableRoom()
        {
            for (int i = 0; i < sessionRooms.Length; i++)
            {
                if (!sessionRooms[i].isOccupied && !sessionRooms[i].sessionActive)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Get current session info for local player
        /// </summary>
        public SessionRoom GetCurrentSession()
        {
            if (myCurrentRoom >= 0 && myCurrentRoom < sessionRooms.Length)
            {
                return sessionRooms[myCurrentRoom];
            }
            return null;
        }

        /// <summary>
        /// Check if player is currently in a session
        /// </summary>
        public bool IsInSession()
        {
            return myCurrentRoom >= 0;
        }

        /// <summary>
        /// Get available room count
        /// </summary>
        public int GetAvailableRoomCount()
        {
            int count = 0;
            for (int i = 0; i < sessionRooms.Length; i++)
            {
                if (!sessionRooms[i].isOccupied) count++;
            }
            return count;
        }

        /// <summary>
        /// Force end current session (emergency)
        /// </summary>
        public void ForceEndCurrentSession()
        {
            if (myCurrentRoom >= 0)
            {
                CompleteSessionEnd(myCurrentRoom);
            }
        }

        public override void OnDeserialization()
        {
            // Handle sync variable updates
            // Update room states based on sync data
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            // Handle player leaving during session
            for (int i = 0; i < sessionRooms.Length; i++)
            {
                var room = sessionRooms[i];
                if (room.sessionActive && (room.playerA == player || room.playerB == player))
                {
                    // End session if one player leaves
                    CompleteSessionEnd(i);
                    break;
                }
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