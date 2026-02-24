// ============================================================
// ThemeParkGame - Conversation UI Controller
// NPC会話インターフェース制御
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ThemeParkGame.Core;

namespace ThemeParkGame.AI
{
    /// <summary>
    /// Represents a single message bubble in the conversation UI.
    /// </summary>
    [Serializable]
    public class ChatBubbleData
    {
        public string SpeakerName;
        public string Message;
        public bool IsPlayerMessage;
        public float Timestamp;
    }

    /// <summary>
    /// Controls the in-game conversation UI between the player and NPCs.
    /// Manages chat bubbles, text input, typing animation, quick replies,
    /// and conversation history scrolling.
    /// Only active when the game is in ResidentView mode.
    /// </summary>
    public class ConversationUI : MonoBehaviour
    {
        // ================================================================
        // Serialized UI References
        // ================================================================

        [Header("UI Panels")]
        [SerializeField] private GameObject conversationPanel;
        [SerializeField] private GameObject inputPanel;
        [SerializeField] private GameObject quickReplyPanel;

        [Header("Chat Display")]
        [SerializeField] private ScrollRect chatScrollRect;
        [SerializeField] private RectTransform chatContentParent;
        [SerializeField] private Text npcNameLabel;
        [SerializeField] private Image npcPortrait;

        [Header("Prefabs")]
        [SerializeField] private GameObject playerBubblePrefab;
        [SerializeField] private GameObject npcBubblePrefab;
        [SerializeField] private GameObject quickReplyButtonPrefab;

        [Header("Input")]
        [SerializeField] private InputField playerInputField;
        [SerializeField] private Button sendButton;
        [SerializeField] private Button closeButton;

        [Header("Typing Animation")]
        [SerializeField] private GameObject typingIndicator;
        [SerializeField] private float typingCharDelay = 0.03f;
        [SerializeField] private float typingStartDelay = 0.3f;

        [Header("Quick Reply Settings")]
        [SerializeField] private int maxQuickReplies = 4;

        // ================================================================
        // State
        // ================================================================

        private AIConversationManager _aiManager;
        private int _currentNpcId = -1;
        private bool _isConversationActive;
        #pragma warning disable CS0414
        private bool _isTypingAnimationRunning;
        #pragma warning restore CS0414
        private bool _isWaitingForResponse;
        private readonly List<ChatBubbleData> _chatHistory = new List<ChatBubbleData>();
        private readonly List<GameObject> _spawnedBubbles = new List<GameObject>();
        private readonly List<GameObject> _spawnedQuickReplies = new List<GameObject>();
        private Coroutine _typingCoroutine;

        /// <summary>Whether a conversation is currently open and visible.</summary>
        public bool IsConversationActive => _isConversationActive;

        /// <summary>The visitor ID of the current conversation partner.</summary>
        public int CurrentNpcId => _currentNpcId;

        /// <summary>Fired when the player sends a message.</summary>
        public event Action<int, string> OnPlayerMessageSent;

        /// <summary>Fired when the conversation is closed by the player.</summary>
        public event Action<int> OnConversationClosed;

        // ================================================================
        // Quick Reply Templates (Japanese)
        // ================================================================

        /// <summary>Default quick reply options when starting a conversation.</summary>
        private static readonly string[] DefaultQuickReplies = new[]
        {
            "こんにちは！楽しんでますか？",
            "おすすめのアトラクションを教えましょうか？",
            "何かお困りですか？",
            "パークの感想を聞かせてください！"
        };

        /// <summary>Quick replies when the NPC seems happy.</summary>
        private static readonly string[] HappyQuickReplies = new[]
        {
            "それは良かったです！",
            "他に楽しみたいことはありますか？",
            "写真を撮りましょうか？",
            "おすすめのお土産がありますよ！"
        };

        /// <summary>Quick replies when the NPC seems unhappy.</summary>
        private static readonly string[] UnhappyQuickReplies = new[]
        {
            "どうされましたか？",
            "何かお手伝いできることはありますか？",
            "ご不便をおかけして申し訳ありません。",
            "改善に努めます！ご意見ありがとうございます。"
        };

        /// <summary>Quick replies when the NPC is looking for something.</summary>
        private static readonly string[] SearchingQuickReplies = new[]
        {
            "ご案内しましょうか？",
            "地図をお見せしましょうか？",
            "こちらの方向ですよ！",
            "一緒に探しましょう！"
        };

        // ================================================================
        // Initialization
        // ================================================================

        private void Awake()
        {
            _aiManager = GetComponentInParent<AIConversationManager>();
            if (_aiManager == null)
            {
                _aiManager = FindObjectOfType<AIConversationManager>();
            }
        }

        private void Start()
        {
            // Wire up UI buttons
            if (sendButton != null)
                sendButton.onClick.AddListener(OnSendButtonClicked);

            if (closeButton != null)
                closeButton.onClick.AddListener(OnCloseButtonClicked);

            if (playerInputField != null)
                playerInputField.onEndEdit.AddListener(OnInputFieldEndEdit);

            // Listen for view mode changes
            GameEvents.OnViewModeChanged += HandleViewModeChanged;

            // Start hidden
            SetConversationPanelVisible(false);
        }

        // ================================================================
        // Public API
        // ================================================================

        /// <summary>
        /// Opens the conversation UI for a specific NPC.
        /// Should only be called when in ResidentView mode.
        /// </summary>
        public void OpenConversation(int npcId, string npcName, Sprite portrait = null)
        {
            // Only allow conversations in ResidentView
            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentViewMode != ViewMode.ResidentView)
            {
                Debug.LogWarning("[ConversationUI] Conversations only available in ResidentView mode.");
                return;
            }

            if (_isConversationActive && _currentNpcId == npcId) return;

            // Close existing conversation if any
            if (_isConversationActive)
            {
                CloseConversation();
            }

            _currentNpcId = npcId;
            _isConversationActive = true;
            _isWaitingForResponse = false;

            // Set NPC display info
            if (npcNameLabel != null)
                npcNameLabel.text = npcName;
            if (npcPortrait != null && portrait != null)
                npcPortrait.sprite = portrait;

            // Clear previous history display
            ClearChatBubbles();
            _chatHistory.Clear();

            // Show the UI
            SetConversationPanelVisible(true);
            ShowQuickReplies(DefaultQuickReplies);

            // Focus input field
            if (playerInputField != null)
            {
                playerInputField.text = "";
                playerInputField.ActivateInputField();
            }

            Debug.Log($"[ConversationUI] Opened conversation with NPC #{npcId} ({npcName})");
        }

        /// <summary>
        /// Closes the current conversation and hides the UI.
        /// </summary>
        public void CloseConversation()
        {
            if (!_isConversationActive) return;

            int closedNpcId = _currentNpcId;

            // Stop any running typing animation
            if (_typingCoroutine != null)
            {
                StopCoroutine(_typingCoroutine);
                _typingCoroutine = null;
            }

            _isConversationActive = false;
            _isTypingAnimationRunning = false;
            _isWaitingForResponse = false;
            _currentNpcId = -1;

            SetConversationPanelVisible(false);
            HideTypingIndicator();

            OnConversationClosed?.Invoke(closedNpcId);
            Debug.Log($"[ConversationUI] Closed conversation with NPC #{closedNpcId}");
        }

        /// <summary>
        /// Displays an NPC response in the chat UI with typing animation.
        /// </summary>
        public void DisplayNPCResponse(string message)
        {
            if (!_isConversationActive) return;

            _isWaitingForResponse = false;
            HideTypingIndicator();

            var bubbleData = new ChatBubbleData
            {
                SpeakerName = npcNameLabel != null ? npcNameLabel.text : "NPC",
                Message = message,
                IsPlayerMessage = false,
                Timestamp = Time.time
            };
            _chatHistory.Add(bubbleData);

            // Start typing animation
            _typingCoroutine = StartCoroutine(TypewriterAnimation(message, bubbleData));
        }

        /// <summary>
        /// Displays a system message (e.g., quest notification) in the chat.
        /// </summary>
        public void DisplaySystemMessage(string message)
        {
            if (!_isConversationActive) return;

            var bubbleData = new ChatBubbleData
            {
                SpeakerName = "システム",
                Message = message,
                IsPlayerMessage = false,
                Timestamp = Time.time
            };
            _chatHistory.Add(bubbleData);

            SpawnChatBubble(bubbleData, message);
        }

        /// <summary>
        /// Updates the quick reply suggestions based on current conversation context.
        /// </summary>
        public void UpdateQuickReplies(float npcHappiness, EmotionBubbleType? currentEmotion = null)
        {
            string[] replies;

            // Select replies based on NPC state
            if (currentEmotion.HasValue)
            {
                switch (currentEmotion.Value)
                {
                    case EmotionBubbleType.Lost:
                    case EmotionBubbleType.LookingForExit:
                    case EmotionBubbleType.LookingForToilet:
                    case EmotionBubbleType.LookingForFood:
                        replies = SearchingQuickReplies;
                        break;

                    case EmotionBubbleType.FoodTastesBad:
                    case EmotionBubbleType.TooExpensive:
                    case EmotionBubbleType.TooDirty:
                    case EmotionBubbleType.NotExcitingEnough:
                    case EmotionBubbleType.LongWait:
                    case EmotionBubbleType.Boring:
                        replies = UnhappyQuickReplies;
                        break;

                    case EmotionBubbleType.BestRide:
                    case EmotionBubbleType.BestShop:
                    case EmotionBubbleType.LovingIt:
                    case EmotionBubbleType.Interesting:
                        replies = HappyQuickReplies;
                        break;

                    default:
                        replies = npcHappiness >= 60f ? HappyQuickReplies : DefaultQuickReplies;
                        break;
                }
            }
            else
            {
                if (npcHappiness >= 70f)
                    replies = HappyQuickReplies;
                else if (npcHappiness <= 30f)
                    replies = UnhappyQuickReplies;
                else
                    replies = DefaultQuickReplies;
            }

            ShowQuickReplies(replies);
        }

        /// <summary>
        /// Shows the typing indicator to signal that the NPC is "thinking."
        /// </summary>
        public void ShowWaitingForResponse()
        {
            _isWaitingForResponse = true;
            ShowTypingIndicator();
            SetInputInteractable(false);
        }

        /// <summary>
        /// Returns the full conversation history as formatted text.
        /// </summary>
        public string GetConversationHistoryText()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var bubble in _chatHistory)
            {
                string prefix = bubble.IsPlayerMessage ? "プレイヤー" : bubble.SpeakerName;
                sb.AppendLine($"{prefix}: {bubble.Message}");
            }
            return sb.ToString();
        }

        // ================================================================
        // Input Handling
        // ================================================================

        private void OnSendButtonClicked()
        {
            if (playerInputField == null) return;

            string message = playerInputField.text.Trim();
            if (string.IsNullOrEmpty(message)) return;

            SendPlayerMessage(message);
        }

        private void OnInputFieldEndEdit(string text)
        {
            // Submit on Enter key
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                string message = text.Trim();
                if (!string.IsNullOrEmpty(message))
                {
                    SendPlayerMessage(message);
                }
            }
        }

        private void OnCloseButtonClicked()
        {
            CloseConversation();
        }

        private void SendPlayerMessage(string message)
        {
            if (_isWaitingForResponse) return;
            if (!_isConversationActive) return;

            // Add player message to history and display
            var bubbleData = new ChatBubbleData
            {
                SpeakerName = "プレイヤー",
                Message = message,
                IsPlayerMessage = true,
                Timestamp = Time.time
            };
            _chatHistory.Add(bubbleData);
            SpawnChatBubble(bubbleData, message);

            // Clear input field
            if (playerInputField != null)
            {
                playerInputField.text = "";
                playerInputField.ActivateInputField();
            }

            // Show typing indicator while waiting for NPC response
            ShowWaitingForResponse();

            // Notify the AI conversation manager
            OnPlayerMessageSent?.Invoke(_currentNpcId, message);
        }

        private void OnQuickReplyClicked(string replyText)
        {
            SendPlayerMessage(replyText);
        }

        // ================================================================
        // UI Rendering
        // ================================================================

        /// <summary>
        /// Spawns a chat bubble in the scroll view.
        /// </summary>
        private void SpawnChatBubble(ChatBubbleData data, string displayText)
        {
            GameObject prefab = data.IsPlayerMessage ? playerBubblePrefab : npcBubblePrefab;
            if (prefab == null || chatContentParent == null)
            {
                Debug.LogWarning("[ConversationUI] Missing bubble prefab or content parent.");
                return;
            }

            GameObject bubble = Instantiate(prefab, chatContentParent);
            _spawnedBubbles.Add(bubble);

            // Find and set the text component
            Text bubbleText = bubble.GetComponentInChildren<Text>();
            if (bubbleText != null)
            {
                bubbleText.text = displayText;
            }

            // Find and set the name label (if exists)
            Transform nameTransform = bubble.transform.Find("NameLabel");
            if (nameTransform != null)
            {
                Text nameText = nameTransform.GetComponent<Text>();
                if (nameText != null)
                {
                    nameText.text = data.SpeakerName;
                }
            }

            // Scroll to bottom
            StartCoroutine(ScrollToBottom());
        }

        /// <summary>
        /// Displays NPC response with typewriter effect.
        /// Characters appear one at a time for a natural conversation feel.
        /// </summary>
        private IEnumerator TypewriterAnimation(string fullMessage, ChatBubbleData data)
        {
            _isTypingAnimationRunning = true;

            // Initial delay before typing starts
            yield return new WaitForSecondsRealtime(typingStartDelay);

            if (!_isConversationActive)
            {
                _isTypingAnimationRunning = false;
                yield break;
            }

            // Spawn the bubble with empty text
            GameObject prefab = npcBubblePrefab;
            if (prefab == null || chatContentParent == null)
            {
                _isTypingAnimationRunning = false;
                yield break;
            }

            GameObject bubble = Instantiate(prefab, chatContentParent);
            _spawnedBubbles.Add(bubble);

            Text bubbleText = bubble.GetComponentInChildren<Text>();
            if (bubbleText == null)
            {
                _isTypingAnimationRunning = false;
                yield break;
            }

            // Typewriter effect
            var displayedText = new System.Text.StringBuilder();
            for (int i = 0; i < fullMessage.Length; i++)
            {
                if (!_isConversationActive)
                {
                    _isTypingAnimationRunning = false;
                    yield break;
                }

                displayedText.Append(fullMessage[i]);
                bubbleText.text = displayedText.ToString();

                // Scroll to bottom as text grows
                if (chatScrollRect != null)
                {
                    Canvas.ForceUpdateCanvases();
                    chatScrollRect.verticalNormalizedPosition = 0f;
                }

                yield return new WaitForSecondsRealtime(typingCharDelay);
            }

            _isTypingAnimationRunning = false;
            SetInputInteractable(true);

            // Re-enable input
            if (playerInputField != null)
            {
                playerInputField.ActivateInputField();
            }

            _typingCoroutine = null;
        }

        /// <summary>Shows quick reply buttons.</summary>
        private void ShowQuickReplies(string[] replies)
        {
            ClearQuickReplies();

            if (quickReplyPanel == null || quickReplyButtonPrefab == null) return;

            quickReplyPanel.SetActive(true);

            int count = Mathf.Min(replies.Length, maxQuickReplies);
            for (int i = 0; i < count; i++)
            {
                string replyText = replies[i];
                GameObject buttonObj = Instantiate(quickReplyButtonPrefab, quickReplyPanel.transform);
                _spawnedQuickReplies.Add(buttonObj);

                // Set button text
                Text btnText = buttonObj.GetComponentInChildren<Text>();
                if (btnText != null)
                {
                    btnText.text = replyText;
                }

                // Wire up click handler
                Button btn = buttonObj.GetComponent<Button>();
                if (btn != null)
                {
                    string capturedText = replyText; // Capture for closure
                    btn.onClick.AddListener(() => OnQuickReplyClicked(capturedText));
                }
            }
        }

        // ================================================================
        // UI Helpers
        // ================================================================

        private void SetConversationPanelVisible(bool visible)
        {
            if (conversationPanel != null)
                conversationPanel.SetActive(visible);
            if (inputPanel != null)
                inputPanel.SetActive(visible);
        }

        private void SetInputInteractable(bool interactable)
        {
            if (playerInputField != null)
                playerInputField.interactable = interactable;
            if (sendButton != null)
                sendButton.interactable = interactable;

            // Also toggle quick reply buttons
            foreach (var btn in _spawnedQuickReplies)
            {
                if (btn != null)
                {
                    Button button = btn.GetComponent<Button>();
                    if (button != null) button.interactable = interactable;
                }
            }
        }

        private void ShowTypingIndicator()
        {
            if (typingIndicator != null)
                typingIndicator.SetActive(true);
        }

        private void HideTypingIndicator()
        {
            if (typingIndicator != null)
                typingIndicator.SetActive(false);
        }

        private void ClearChatBubbles()
        {
            foreach (var bubble in _spawnedBubbles)
            {
                if (bubble != null) Destroy(bubble);
            }
            _spawnedBubbles.Clear();
        }

        private void ClearQuickReplies()
        {
            foreach (var btn in _spawnedQuickReplies)
            {
                if (btn != null) Destroy(btn);
            }
            _spawnedQuickReplies.Clear();
        }

        private IEnumerator ScrollToBottom()
        {
            // Wait one frame for layout rebuild
            yield return null;
            if (chatScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                chatScrollRect.verticalNormalizedPosition = 0f;
            }
        }

        /// <summary>Handles view mode changes - close conversation if leaving ResidentView.</summary>
        private void HandleViewModeChanged(ViewMode newMode)
        {
            if (newMode != ViewMode.ResidentView && _isConversationActive)
            {
                Debug.Log("[ConversationUI] Closing conversation due to view mode change.");
                CloseConversation();
            }
        }

        // ================================================================
        // Cleanup
        // ================================================================

        private void OnDestroy()
        {
            GameEvents.OnViewModeChanged -= HandleViewModeChanged;

            if (sendButton != null)
                sendButton.onClick.RemoveAllListeners();
            if (closeButton != null)
                closeButton.onClick.RemoveAllListeners();

            ClearChatBubbles();
            ClearQuickReplies();
        }
    }
}
