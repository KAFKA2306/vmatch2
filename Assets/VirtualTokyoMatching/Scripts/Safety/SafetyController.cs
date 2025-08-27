using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using TMPro;

namespace VirtualTokyoMatching
{
    /// <summary>
    /// Handles privacy, safety, and moderation features for the matching system.
    /// Manages public sharing controls, blocking, muting, and emergency features.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SafetyController : UdonSharpBehaviour
    {
        [Header("Dependencies")]
        public PlayerDataManager playerDataManager;
        public PublicProfilePublisher publicProfilePublisher;
        public SessionRoomManager sessionRoomManager;

        [Header("Privacy Controls")]
        public GameObject privacyPanel;
        public Toggle publicSharingToggle;
        public Toggle provisionalSharingToggle;
        public Button hideProfileButton;
        public Button showProfileButton;
        public TextMeshProUGUI privacyStatusText;

        [Header("Safety UI")]
        public GameObject safetyPanel;
        public Button mutePlayerButton;
        public Button blockPlayerButton;
        public Button reportPlayerButton;
        public Button emergencyExitButton;
        public Button resetDataButton;

        [Header("Confirmation Dialogs")]
        public GameObject confirmDialog;
        public TextMeshProUGUI confirmText;
        public Button confirmYesButton;
        public Button confirmNoButton;

        [Header("Terms and Warnings")]
        public GameObject termsPanel;
        public GameObject warningPanel;
        public TextMeshProUGUI warningText;
        public Button acknowledgeButton;
        public Button openTermsButton;
        public Button closeTermsButton;

        [Header("User Lists")]
        public GameObject userListPanel;
        public Transform mutedUsersList;
        public Transform blockedUsersList;
        public GameObject userEntryPrefab;

        [Header("Settings")]
        public bool showWarningsOnStartup = true;
        public bool requireTermsAgreement = true;
        public Color warningColor = Color.red;
        public Color safeColor = Color.green;

        // State management
        private VRCPlayerApi targetPlayer;
        private string pendingConfirmAction;
        private System.Action pendingConfirmCallback;
        private bool hasAgreedToTerms = false;
        private bool isPublicSharingEnabled = false;
        private bool isProvisionalSharingEnabled = true;

        // User management
        private System.Collections.Generic.List<string> mutedPlayers = new System.Collections.Generic.List<string>();
        private System.Collections.Generic.List<string> blockedPlayers = new System.Collections.Generic.List<string>();

        void Start()
        {
            InitializeSafetyUI();
            LoadSafetySettings();
            
            if (requireTermsAgreement && !hasAgreedToTerms)
            {
                ShowTermsAgreement();
            }
            else if (showWarningsOnStartup)
            {
                ShowStartupWarning();
            }
        }

        private void InitializeSafetyUI()
        {
            // Set up privacy controls
            if (publicSharingToggle != null)
                publicSharingToggle.onValueChanged.AddListener(OnPublicSharingToggled);

            if (provisionalSharingToggle != null)
                provisionalSharingToggle.onValueChanged.AddListener(OnProvisionalSharingToggled);

            if (hideProfileButton != null)
                hideProfileButton.onClick.AddListener(HideProfile);

            if (showProfileButton != null)
                showProfileButton.onClick.AddListener(ShowProfile);

            // Set up safety buttons
            if (mutePlayerButton != null)
                mutePlayerButton.onClick.AddListener(() => RequestPlayerAction("mute"));

            if (blockPlayerButton != null)
                blockPlayerButton.onClick.AddListener(() => RequestPlayerAction("block"));

            if (reportPlayerButton != null)
                reportPlayerButton.onClick.AddListener(() => RequestPlayerAction("report"));

            if (emergencyExitButton != null)
                emergencyExitButton.onClick.AddListener(EmergencyExit);

            if (resetDataButton != null)
                resetDataButton.onClick.AddListener(() => RequestDataReset());

            // Set up confirmation dialog
            if (confirmYesButton != null)
                confirmYesButton.onClick.AddListener(ConfirmAction);

            if (confirmNoButton != null)
                confirmNoButton.onClick.AddListener(CancelAction);

            // Set up terms/warning dialogs
            if (acknowledgeButton != null)
                acknowledgeButton.onClick.AddListener(AcknowledgeWarning);

            if (openTermsButton != null)
                openTermsButton.onClick.AddListener(ShowTerms);

            if (closeTermsButton != null)
                closeTermsButton.onClick.AddListener(HideTerms);

            // Hide panels initially
            if (privacyPanel != null) privacyPanel.SetActive(false);
            if (safetyPanel != null) safetyPanel.SetActive(false);
            if (confirmDialog != null) confirmDialog.SetActive(false);
            if (termsPanel != null) termsPanel.SetActive(false);
            if (warningPanel != null) warningPanel.SetActive(false);
            if (userListPanel != null) userListPanel.SetActive(false);
        }

        private void LoadSafetySettings()
        {
            if (playerDataManager != null && playerDataManager.IsDataLoaded)
            {
                // Load privacy settings from PlayerData flags
                int flags = playerDataManager.Flags;
                isPublicSharingEnabled = (flags & 1) != 0;
                isProvisionalSharingEnabled = (flags & 2) != 0;

                // Load terms agreement status
                hasAgreedToTerms = (flags & 4) != 0;

                UpdatePrivacyUI();
            }
        }

        /// <summary>
        /// Show privacy control panel
        /// </summary>
        public void ShowPrivacyControls()
        {
            if (privacyPanel != null)
            {
                privacyPanel.SetActive(true);
                UpdatePrivacyUI();
            }
        }

        /// <summary>
        /// Hide privacy control panel
        /// </summary>
        public void HidePrivacyControls()
        {
            if (privacyPanel != null) privacyPanel.SetActive(false);
        }

        private void UpdatePrivacyUI()
        {
            if (publicSharingToggle != null)
                publicSharingToggle.isOn = isPublicSharingEnabled;

            if (provisionalSharingToggle != null)
                provisionalSharingToggle.isOn = isProvisionalSharingEnabled;

            string statusText = isPublicSharingEnabled ? 
                "プロフィール公開中 (推薦リストに表示されます)" : 
                "プロフィール非公開 (推薦リストに表示されません)";

            if (privacyStatusText != null)
            {
                privacyStatusText.text = statusText;
                privacyStatusText.color = isPublicSharingEnabled ? safeColor : warningColor;
            }
        }

        private void OnPublicSharingToggled(bool enabled)
        {
            isPublicSharingEnabled = enabled;
            
            if (enabled)
            {
                ShowProfile();
            }
            else
            {
                HideProfile();
            }
        }

        private void OnProvisionalSharingToggled(bool enabled)
        {
            isProvisionalSharingEnabled = enabled;
            SavePrivacySettings();
        }

        /// <summary>
        /// Enable public profile sharing
        /// </summary>
        public void ShowProfile()
        {
            if (publicProfilePublisher != null)
            {
                publicProfilePublisher.EnablePublicSharing();
            }

            isPublicSharingEnabled = true;
            SavePrivacySettings();
            UpdatePrivacyUI();

            Debug.Log("[SafetyController] Profile sharing enabled");
        }

        /// <summary>
        /// Disable public profile sharing
        /// </summary>
        public void HideProfile()
        {
            if (publicProfilePublisher != null)
            {
                publicProfilePublisher.DisablePublicSharing();
            }

            isPublicSharingEnabled = false;
            SavePrivacySettings();
            UpdatePrivacyUI();

            Debug.Log("[SafetyController] Profile sharing disabled");
        }

        private void SavePrivacySettings()
        {
            if (playerDataManager != null)
            {
                int flags = 0;
                if (isPublicSharingEnabled) flags |= 1;
                if (isProvisionalSharingEnabled) flags |= 2;
                if (hasAgreedToTerms) flags |= 4;

                playerDataManager.SetFlags(flags);
            }
        }

        /// <summary>
        /// Show safety panel with player-specific actions
        /// </summary>
        public void ShowSafetyPanel(VRCPlayerApi player = null)
        {
            targetPlayer = player;

            if (safetyPanel != null)
            {
                safetyPanel.SetActive(true);
                
                // Update button states based on target player
                bool hasTarget = targetPlayer != null && targetPlayer.IsValid();
                
                if (mutePlayerButton != null)
                {
                    mutePlayerButton.interactable = hasTarget;
                    var buttonText = mutePlayerButton.GetComponentInChildren<TextMeshProUGUI>();
                    if (buttonText != null)
                    {
                        buttonText.text = hasTarget ? $"{targetPlayer.displayName}をミュート" : "プレイヤーミュート";
                    }
                }

                if (blockPlayerButton != null)
                {
                    blockPlayerButton.interactable = hasTarget;
                    var buttonText = blockPlayerButton.GetComponentInChildren<TextMeshProUGUI>();
                    if (buttonText != null)
                    {
                        buttonText.text = hasTarget ? $"{targetPlayer.displayName}をブロック" : "プレイヤーブロック";
                    }
                }

                if (reportPlayerButton != null)
                {
                    reportPlayerButton.interactable = hasTarget;
                    var buttonText = reportPlayerButton.GetComponentInChildren<TextMeshProUGUI>();
                    if (buttonText != null)
                    {
                        buttonText.text = hasTarget ? $"{targetPlayer.displayName}を報告" : "プレイヤー報告";
                    }
                }
            }
        }

        /// <summary>
        /// Hide safety panel
        /// </summary>
        public void HideSafetyPanel()
        {
            if (safetyPanel != null) safetyPanel.SetActive(false);
            targetPlayer = null;
        }

        private void RequestPlayerAction(string action)
        {
            if (targetPlayer == null || !targetPlayer.IsValid()) return;

            string actionText = "";
            switch (action)
            {
                case "mute":
                    actionText = $"{targetPlayer.displayName}さんをミュートしますか？\n（音声が聞こえなくなります）";
                    break;
                case "block":
                    actionText = $"{targetPlayer.displayName}さんをブロックしますか？\n（今後推薦されなくなります）";
                    break;
                case "report":
                    actionText = $"{targetPlayer.displayName}さんを不適切な行為で報告しますか？";
                    break;
            }

            ShowConfirmation(actionText, () => ExecutePlayerAction(action));
        }

        private void ExecutePlayerAction(string action)
        {
            if (targetPlayer == null || !targetPlayer.IsValid()) return;

            string playerName = targetPlayer.displayName;
            
            switch (action)
            {
                case "mute":
                    MutePlayer(targetPlayer);
                    ShowWarning($"{playerName}さんをミュートしました", 3f);
                    break;
                    
                case "block":
                    BlockPlayer(targetPlayer);
                    ShowWarning($"{playerName}さんをブロックしました", 3f);
                    break;
                    
                case "report":
                    ReportPlayer(targetPlayer);
                    ShowWarning($"{playerName}さんを報告しました", 3f);
                    break;
            }

            HideSafetyPanel();
        }

        private void MutePlayer(VRCPlayerApi player)
        {
            if (player == null || !player.IsValid()) return;

            // Add to muted list
            string playerName = player.displayName;
            if (!mutedPlayers.Contains(playerName))
            {
                mutedPlayers.Add(playerName);
            }

            // Use VRChat's built-in mute functionality
            // Note: This would require VRChat SDK specific implementation
            Debug.Log($"[SafetyController] Muted player: {playerName}");
        }

        private void BlockPlayer(VRCPlayerApi player)
        {
            if (player == null || !player.IsValid()) return;

            string playerName = player.displayName;
            if (!blockedPlayers.Contains(playerName))
            {
                blockedPlayers.Add(playerName);
            }

            // This would integrate with the recommendation system to filter out blocked players
            Debug.Log($"[SafetyController] Blocked player: {playerName}");
        }

        private void ReportPlayer(VRCPlayerApi player)
        {
            if (player == null || !player.IsValid()) return;

            // This would integrate with VRChat's reporting system
            // For now, just log the report
            Debug.Log($"[SafetyController] Reported player: {player.displayName}");
        }

        /// <summary>
        /// Emergency exit from current session
        /// </summary>
        public void EmergencyExit()
        {
            // Immediately exit any active session
            if (sessionRoomManager != null && sessionRoomManager.IsInSession())
            {
                sessionRoomManager.ForceEndCurrentSession();
            }

            // Hide all profiles immediately
            HideProfile();

            // Show safety message
            ShowWarning("緊急退避しました。安全な場所に移動しています...", 5f);

            Debug.Log("[SafetyController] Emergency exit executed");
        }

        /// <summary>
        /// Request complete data reset
        /// </summary>
        public void RequestDataReset()
        {
            string confirmText = "全てのデータをリセットしますか？\n" +
                               "・診断結果\n" +
                               "・プロフィール情報\n" +
                               "・プライバシー設定\n" +
                               "\nこの操作は取り消せません。";
            
            ShowConfirmation(confirmText, ExecuteDataReset);
        }

        private void ExecuteDataReset()
        {
            // Hide profile first
            HideProfile();

            // Reset PlayerData
            if (playerDataManager != null)
            {
                playerDataManager.ResetPlayerData();
            }

            // Clear local lists
            mutedPlayers.Clear();
            blockedPlayers.Clear();

            // Reset settings
            isPublicSharingEnabled = false;
            isProvisionalSharingEnabled = true;
            hasAgreedToTerms = false;

            UpdatePrivacyUI();

            ShowWarning("全てのデータがリセットされました", 3f);
            
            Debug.Log("[SafetyController] Complete data reset executed");
        }

        private void ShowConfirmation(string message, System.Action callback)
        {
            if (confirmDialog != null && confirmText != null)
            {
                confirmText.text = message;
                confirmDialog.SetActive(true);
                pendingConfirmCallback = callback;
            }
        }

        private void ConfirmAction()
        {
            if (confirmDialog != null) confirmDialog.SetActive(false);
            
            if (pendingConfirmCallback != null)
            {
                pendingConfirmCallback.Invoke();
                pendingConfirmCallback = null;
            }
        }

        private void CancelAction()
        {
            if (confirmDialog != null) confirmDialog.SetActive(false);
            pendingConfirmCallback = null;
        }

        /// <summary>
        /// Show warning message temporarily
        /// </summary>
        public void ShowWarning(string message, float duration = 3f)
        {
            if (warningPanel != null && warningText != null)
            {
                warningText.text = message;
                warningPanel.SetActive(true);
                
                if (duration > 0)
                {
                    SendCustomEventDelayedSeconds(nameof(HideWarning), duration);
                }
            }
        }

        public void HideWarning()
        {
            if (warningPanel != null) warningPanel.SetActive(false);
        }

        private void ShowStartupWarning()
        {
            string warningMessage = "【重要】\n" +
                                  "・個人情報の共有は避けてください\n" +
                                  "・不適切な行為を受けた場合は報告してください\n" +
                                  "・いつでもプロフィール公開を停止できます\n" +
                                  "・緊急時は緊急退避ボタンをご利用ください";
            
            ShowWarning(warningMessage, 10f);
        }

        private void ShowTermsAgreement()
        {
            if (termsPanel != null)
            {
                termsPanel.SetActive(true);
            }
        }

        private void ShowTerms()
        {
            // This would show detailed terms of service
            ShowWarning("利用規約の詳細画面は実装中です", 3f);
        }

        private void HideTerms()
        {
            if (termsPanel != null) termsPanel.SetActive(false);
        }

        private void AcknowledgeWarning()
        {
            hasAgreedToTerms = true;
            SavePrivacySettings();
            
            if (warningPanel != null) warningPanel.SetActive(false);
            if (termsPanel != null) termsPanel.SetActive(false);
        }

        /// <summary>
        /// Check if player is blocked
        /// </summary>
        public bool IsPlayerBlocked(VRCPlayerApi player)
        {
            if (player == null || !player.IsValid()) return false;
            return blockedPlayers.Contains(player.displayName);
        }

        /// <summary>
        /// Check if player is muted
        /// </summary>
        public bool IsPlayerMuted(VRCPlayerApi player)
        {
            if (player == null || !player.IsValid()) return false;
            return mutedPlayers.Contains(player.displayName);
        }

        /// <summary>
        /// Get current privacy status
        /// </summary>
        public bool IsPublicSharingEnabled()
        {
            return isPublicSharingEnabled;
        }

        /// <summary>
        /// Get provisional sharing status
        /// </summary>
        public bool IsProvisionalSharingEnabled()
        {
            return isProvisionalSharingEnabled;
        }

        /// <summary>
        /// Show user management lists
        /// </summary>
        public void ShowUserLists()
        {
            if (userListPanel != null)
            {
                userListPanel.SetActive(true);
                UpdateUserLists();
            }
        }

        private void UpdateUserLists()
        {
            // Update muted users list
            if (mutedUsersList != null)
            {
                ClearUserList(mutedUsersList);
                foreach (string playerName in mutedPlayers)
                {
                    CreateUserListEntry(mutedUsersList, playerName, "ミュート解除", () => UnmutePlayer(playerName));
                }
            }

            // Update blocked users list
            if (blockedUsersList != null)
            {
                ClearUserList(blockedUsersList);
                foreach (string playerName in blockedPlayers)
                {
                    CreateUserListEntry(blockedUsersList, playerName, "ブロック解除", () => UnblockPlayer(playerName));
                }
            }
        }

        private void ClearUserList(Transform listTransform)
        {
            for (int i = listTransform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(listTransform.GetChild(i).gameObject);
            }
        }

        private void CreateUserListEntry(Transform parent, string playerName, string buttonText, System.Action callback)
        {
            if (userEntryPrefab == null) return;

            var entry = Instantiate(userEntryPrefab, parent);
            
            var nameText = entry.GetComponentInChildren<TextMeshProUGUI>();
            if (nameText != null) nameText.text = playerName;

            var button = entry.GetComponentInChildren<Button>();
            if (button != null)
            {
                button.onClick.AddListener(() => callback.Invoke());
                var buttonTextComponent = button.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonTextComponent != null) buttonTextComponent.text = buttonText;
            }

            entry.SetActive(true);
        }

        private void UnmutePlayer(string playerName)
        {
            mutedPlayers.Remove(playerName);
            UpdateUserLists();
            ShowWarning($"{playerName}さんのミュートを解除しました", 2f);
        }

        private void UnblockPlayer(string playerName)
        {
            blockedPlayers.Remove(playerName);
            UpdateUserLists();
            ShowWarning($"{playerName}さんのブロックを解除しました", 2f);
        }

        /// <summary>
        /// Hide user management panel
        /// </summary>
        public void HideUserLists()
        {
            if (userListPanel != null) userListPanel.SetActive(false);
        }

        // Event handlers for PlayerData loaded
        public void OnPlayerDataLoaded()
        {
            LoadSafetySettings();
        }
    }
}