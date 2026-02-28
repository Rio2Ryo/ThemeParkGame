// ============================================================
// ThemeParkGame - AI Conversation Manager
// NPC会話・LLM統合・クエスト生成の統括マネージャー
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ThemeParkGame.Core;
using ThemeParkGame.Visitor;

namespace ThemeParkGame.AI
{
    /// <summary>
    /// AI会話システムの統括マネージャー。
    /// LLM APIクライアントの管理、NPC性格の生成・キャッシュ、
    /// 会話セッションの制御、クエスト生成の調整を担当する。
    /// GameManagerのサブシステムとして初期化される。
    /// </summary>
    public class AIConversationManager : MonoBehaviour
    {
        [Header("LLM Settings")]
        [SerializeField] private LLMProvider preferredProvider = LLMProvider.GeminiFlash;
        [SerializeField] private string apiKey = "";
        [SerializeField] private float maxBudgetUSD = 5f;
        [SerializeField] private int maxConcurrentRequests = 3;

        [Header("Conversation Settings")]
        [SerializeField] private int maxConversationHistory = 10;
        #pragma warning disable CS0414
        [SerializeField] private float conversationTimeout = 60f;
        #pragma warning restore CS0414
        [SerializeField] private int maxPersonalityCacheSize = 50;

        // LLM Client
        private LLMApiClientBase _llmClient;
        private LLMProviderConfig _providerConfig;
        private readonly APIUsageTracker _usageTracker = new APIUsageTracker();
        private int _activeRequestCount;

        // Personality cache
        private readonly Dictionary<int, NPCPersonality> _personalityCache =
            new Dictionary<int, NPCPersonality>();

        // Active conversations
        private readonly Dictionary<int, ConversationSession> _activeSessions =
            new Dictionary<int, ConversationSession>();

        // Sub-systems
        private DynamicQuestSystem _questSystem;
        private SNSReputationSystem _snsSystem;
        private ConversationUI _conversationUI;
        private NPCDialogueSystem _dialogueSystem;

        // ---- Public Properties ----

        /// <summary>LLM APIが利用可能かどうか</summary>
        public bool IsAPIAvailable =>
            _llmClient != null &&
            !string.IsNullOrEmpty(_providerConfig?.ApiKey) &&
            !_usageTracker.IsBudgetExceeded &&
            _activeRequestCount < maxConcurrentRequests;

        /// <summary>API使用量トラッカー</summary>
        public APIUsageTracker UsageTracker => _usageTracker;

        /// <summary>SNSレピュテーションシステム</summary>
        public SNSReputationSystem SNSSystem => _snsSystem;

        /// <summary>ダイナミッククエストシステム</summary>
        public DynamicQuestSystem QuestSystem => _questSystem;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize()
        {
            // APIキーをPlayerPrefsから読み込み（セキュリティ上、ビルド時に埋め込まない）
            if (string.IsNullOrEmpty(apiKey))
            {
                apiKey = PlayerPrefs.GetString("LLM_API_KEY", "");
            }

            // LLMクライアントを初期化
            _providerConfig = LLMProviderConfig.CreateDefault(preferredProvider);
            _providerConfig.ApiKey = apiKey;
            _llmClient = LLMClientFactory.Create(_providerConfig);

            _usageTracker.BudgetLimitUSD = maxBudgetUSD;

            // サブシステムの取得/追加
            _questSystem = GetComponent<DynamicQuestSystem>();
            if (_questSystem == null)
                _questSystem = gameObject.AddComponent<DynamicQuestSystem>();

            _snsSystem = GetComponent<SNSReputationSystem>();
            if (_snsSystem == null)
                _snsSystem = gameObject.AddComponent<SNSReputationSystem>();

            if (_conversationUI == null)
                _conversationUI = FindObjectOfType<ConversationUI>();
            _dialogueSystem = NPCDialogueSystem.Instance;

            WebGLOptimizer.LogVerbose($"[AIConversationManager] 初期化完了 (Provider: {preferredProvider}, " +
                      $"API Available: {!string.IsNullOrEmpty(apiKey)})");
        }

        private void Start()
        {
            Initialize();

            // イベント購読
            GameEvents.OnVisitorEnterPark += OnVisitorEnterPark;
            GameEvents.OnVisitorLeavePark += OnVisitorLeavePark;
            GameEvents.OnNPCConversationStarted += OnConversationStarted;
            GameEvents.OnNPCConversationEnded += OnConversationEnded;

            // ConversationUI → AIConversationManager 接続
            WireConversationUI();
        }

        private void OnDestroy()
        {
            GameEvents.OnVisitorEnterPark -= OnVisitorEnterPark;
            GameEvents.OnVisitorLeavePark -= OnVisitorLeavePark;
            GameEvents.OnNPCConversationStarted -= OnConversationStarted;
            GameEvents.OnNPCConversationEnded -= OnConversationEnded;

            UnwireConversationUI();
        }

        /// <summary>ConversationUIのイベントを購読してメッセージを中継する</summary>
        private void WireConversationUI()
        {
            if (_conversationUI == null)
                _conversationUI = FindObjectOfType<ConversationUI>(); // lazy cache

            if (_conversationUI != null)
            {
                _conversationUI.OnPlayerMessageSent += HandlePlayerMessageFromUI;
                _conversationUI.OnConversationClosed += HandleConversationClosedFromUI;
                WebGLOptimizer.LogVerbose("[AIConversationManager] ConversationUI 接続完了");
            }
        }

        private void UnwireConversationUI()
        {
            if (_conversationUI != null)
            {
                _conversationUI.OnPlayerMessageSent -= HandlePlayerMessageFromUI;
                _conversationUI.OnConversationClosed -= HandleConversationClosedFromUI;
            }
        }

        /// <summary>ConversationUIからのプレイヤーメッセージをSendPlayerMessageに中継</summary>
        private void HandlePlayerMessageFromUI(int visitorId, string message)
        {
            // セッションが未開始なら自動開始
            if (!_activeSessions.ContainsKey(visitorId))
            {
                StartPlayerConversation(visitorId);
            }
            SendPlayerMessage(visitorId, message);
        }

        /// <summary>ConversationUIからの会話終了をEndConversationに中継</summary>
        private void HandleConversationClosedFromUI(int visitorId)
        {
            EndConversation(visitorId);
        }

        // ================================================================
        // Personality Management
        // ================================================================

        /// <summary>来場者のNPC性格を取得する（なければ生成してキャッシュ）</summary>
        public NPCPersonality GetOrCreatePersonality(int visitorId, VisitorType visitorType)
        {
            if (_personalityCache.TryGetValue(visitorId, out NPCPersonality existing))
            {
                return existing;
            }

            var personality = NPCPersonality.Generate(visitorId, visitorType);
            CachePersonality(visitorId, personality);
            return personality;
        }

        /// <summary>来場者のNPC性格が存在するか確認する</summary>
        public bool TryGetPersonality(int visitorId, out NPCPersonality personality)
        {
            return _personalityCache.TryGetValue(visitorId, out personality);
        }

        private void CachePersonality(int visitorId, NPCPersonality personality)
        {
            // キャッシュサイズ上限に達したら最も古いエントリを削除
            if (_personalityCache.Count >= maxPersonalityCacheSize)
            {
                // 簡易LRU: 最初のキーを削除
                var enumerator = _personalityCache.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    _personalityCache.Remove(enumerator.Current.Key);
                }
            }
            _personalityCache[visitorId] = personality;
        }

        // ================================================================
        // Conversation Sessions
        // ================================================================

        /// <summary>
        /// プレイヤーがNPCに話しかけた時の会話開始処理。
        /// 住人視点モードでNPCをタップした時にVisitorAIから呼ばれる。
        /// </summary>
        public void StartPlayerConversation(int visitorId)
        {
            if (_activeSessions.ContainsKey(visitorId))
            {
                Debug.LogWarning($"[AIConversation] 既に会話中: Visitor {visitorId}");
                return;
            }

            var session = new ConversationSession
            {
                VisitorId = visitorId,
                StartTime = Time.time,
                Messages = new List<LLMMessage>()
            };

            // システムプロンプトの設定
            if (TryGetPersonality(visitorId, out NPCPersonality personality))
            {
                string systemPrompt = personality.GenerateSystemPrompt();
                session.Messages.Add(new LLMMessage("system", systemPrompt));
                session.PersonalityName = personality.DisplayName;
            }

            _activeSessions[visitorId] = session;

            // 最初の挨拶をLLMで生成
            if (IsAPIAvailable)
            {
                var greetingMessages = new List<LLMMessage>(session.Messages);
                greetingMessages.Add(new LLMMessage("user",
                    "（プレイヤーが近づいてきた。自然な挨拶をしてください）"));

                StartCoroutine(SendAndHandleResponse(visitorId, greetingMessages));
            }
            else
            {
                // API利用不可時はNPCDialogueSystemのテンプレート応答
                string greeting;
                if (_dialogueSystem != null && personality != null)
                {
                    greeting = _dialogueSystem.GetDialogue(
                        visitorId, personality.Style, DialogueCategory.Greeting).Text;
                }
                else
                {
                    greeting = GetFallbackGreeting(personality);
                }
                HandleNPCResponse(visitorId, greeting);
            }

            GameEvents.FireNPCConversationStarted(visitorId, "player_initiated");
        }

        /// <summary>プレイヤーの入力メッセージを処理する</summary>
        public void SendPlayerMessage(int visitorId, string message)
        {
            if (!_activeSessions.TryGetValue(visitorId, out ConversationSession session))
            {
                Debug.LogWarning($"[AIConversation] アクティブセッションなし: Visitor {visitorId}");
                return;
            }

            session.Messages.Add(new LLMMessage("user", message));

            // 会話履歴が長すぎる場合は古いメッセージを削除
            TrimConversationHistory(session);

            if (IsAPIAvailable)
            {
                StartCoroutine(SendAndHandleResponse(visitorId, session.Messages));
            }
            else
            {
                // NPCDialogueSystemでコンテキスト応答を生成
                string fallback;
                if (_dialogueSystem != null && TryGetPersonality(visitorId, out NPCPersonality p2))
                {
                    fallback = _dialogueSystem.GetFallbackResponse(visitorId, p2.Style, message, 50f);
                }
                else
                {
                    fallback = "うーん、ちょっと考え中...";
                }
                HandleNPCResponse(visitorId, fallback);
            }
        }

        /// <summary>会話を終了する</summary>
        public void EndConversation(int visitorId)
        {
            if (_activeSessions.TryGetValue(visitorId, out ConversationSession session))
            {
                // 会話内容をパーソナリティの記憶に記録
                if (TryGetPersonality(visitorId, out NPCPersonality personality))
                {
                    string summary = $"プレイヤーと会話（{session.Messages.Count}メッセージ）";
                    personality.ConversationMemories.Add(summary);
                }

                _activeSessions.Remove(visitorId);
                GameEvents.FireNPCConversationEnded(visitorId, "conversation_ended");
            }
        }

        // ================================================================
        // LLM Request Management
        // ================================================================

        /// <summary>LLMリクエストを送信してレスポンスを処理するコルーチン</summary>
        public IEnumerator SendLLMRequest(List<LLMMessage> messages, Action<LLMResponse> callback)
        {
            if (_llmClient == null || !IsAPIAvailable)
            {
                callback?.Invoke(new LLMResponse
                {
                    Success = false,
                    ErrorMessage = "LLM API is not available"
                });
                yield break;
            }

            _activeRequestCount++;

            LLMResponse response = null;
            yield return _llmClient.SendRequest(messages, r => response = r);

            _activeRequestCount--;

            if (response != null)
            {
                _usageTracker.RecordRequest(response, preferredProvider);
            }

            callback?.Invoke(response);
        }

        private IEnumerator SendAndHandleResponse(int visitorId, List<LLMMessage> messages)
        {
            _activeRequestCount++;

            LLMResponse response = null;
            yield return _llmClient.SendRequest(messages, r => response = r);

            _activeRequestCount--;

            if (response != null)
            {
                _usageTracker.RecordRequest(response, preferredProvider);

                if (response.Success)
                {
                    HandleNPCResponse(visitorId, response.Content);

                    // 会話セッションにアシスタント応答を追加
                    if (_activeSessions.TryGetValue(visitorId, out ConversationSession session))
                    {
                        session.Messages.Add(new LLMMessage("assistant", response.Content));

                        // クエスト生成の可能性をチェック
                        if (_questSystem != null && TryGetPersonality(visitorId, out NPCPersonality p))
                        {
                            _questSystem.TryDetectQuestFromDialogue(visitorId, response.Content, p);
                        }
                    }
                }
                else
                {
                    Debug.LogWarning($"[AIConversation] LLM応答失敗: {response.ErrorMessage}");
                    HandleNPCResponse(visitorId, "あ、ちょっと考え中...えっと...");
                }
            }
        }

        private void HandleNPCResponse(int visitorId, string message)
        {
            // VisitorAIに発言を通知
            GameEvents.FireVisitorSaidSomething(visitorId, message);

            // ConversationUIに表示
            if (_conversationUI != null && _conversationUI.IsConversationActive)
            {
                _conversationUI.DisplayNPCResponse(message);
            }
        }

        private void TrimConversationHistory(ConversationSession session)
        {
            // システムプロンプトは保持し、古いメッセージを削除
            while (session.Messages.Count > maxConversationHistory + 1)
            {
                // index 0 = system, 1以降 = user/assistant
                session.Messages.RemoveAt(1);
            }
        }

        private string GetFallbackGreeting(NPCPersonality personality)
        {
            if (personality == null) return "こんにちは！";

            return personality.Style switch
            {
                ConversationStyle.Formal => "こんにちは。今日はとても良い天気ですね。",
                ConversationStyle.Casual => "あ、こんにちは！楽しんでる？",
                ConversationStyle.Childlike => "ねーねー！ここ楽しいね！",
                ConversationStyle.PoliteElderly => "あら、こんにちは。素敵なパークですわね。",
                ConversationStyle.Enthusiastic => "うわー！こんにちは！今日めっちゃ楽しい！！",
                _ => "こんにちは！"
            };
        }

        // ================================================================
        // Event Handlers
        // ================================================================

        private void OnVisitorEnterPark(int visitorId)
        {
            // 一部の来場者にNPC性格を事前生成（パフォーマンス分散）
            if (UnityEngine.Random.value < 0.3f)
            {
                // VisitorManagerから実際のVisitorTypeを取得する
                VisitorType visitorType = VisitorType.Family; // デフォルト
                if (GameManager.Instance != null && GameManager.Instance.VisitorManager != null)
                {
                    var visitorAI = GameManager.Instance.VisitorManager.FindVisitorById(visitorId);
                    if (visitorAI != null)
                    {
                        visitorType = visitorAI.Type;
                    }
                }
                GetOrCreatePersonality(visitorId, visitorType);
            }
        }

        private void OnVisitorLeavePark(int visitorId)
        {
            // 退園時に会話セッションをクリーンアップ
            EndConversation(visitorId);

            // 性格キャッシュからも削除（メモリ節約）
            _personalityCache.Remove(visitorId);
        }

        private void OnConversationStarted(int visitorId, string topic)
        {
            WebGLOptimizer.LogVerbose($"[AIConversation] 会話開始: Visitor {visitorId}, Topic: {topic}");
        }

        private void OnConversationEnded(int visitorId, string summary)
        {
            WebGLOptimizer.LogVerbose($"[AIConversation] 会話終了: Visitor {visitorId}, Summary: {summary}");
        }

        // ================================================================
        // API Key Management
        // ================================================================

        /// <summary>APIキーを設定して保存する</summary>
        public void SetAPIKey(string key)
        {
            apiKey = key;
            PlayerPrefs.SetString("LLM_API_KEY", key);
            PlayerPrefs.Save();

            // クライアントを再初期化
            _providerConfig.ApiKey = key;
            _llmClient = LLMClientFactory.Create(_providerConfig);

            WebGLOptimizer.LogVerbose("[AIConversation] APIキーが更新されました");
        }

        /// <summary>LLMプロバイダーを切り替える</summary>
        public void SetProvider(LLMProvider provider)
        {
            preferredProvider = provider;
            _providerConfig = LLMProviderConfig.CreateDefault(provider);
            _providerConfig.ApiKey = apiKey;
            _llmClient = LLMClientFactory.Create(_providerConfig);

            WebGLOptimizer.LogVerbose($"[AIConversation] プロバイダー切替: {provider}");
        }

        // ================================================================
        // Inner Types
        // ================================================================

        /// <summary>アクティブな会話セッションの状態</summary>
        private class ConversationSession
        {
            public int VisitorId;
            public string PersonalityName;
            public float StartTime;
            public List<LLMMessage> Messages;
        }
    }
}
