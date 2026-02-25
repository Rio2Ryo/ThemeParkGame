// ============================================================
// ThemeParkGame - VisitorInfoPanel
// 来場者詳細ポップアップ：名前、タイプ、パラメータバー、
// 感情状態、所持金、訪問履歴、AI会話ボタン
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ThemeParkGame.Core;

namespace ThemeParkGame.UI
{
    /// <summary>
    /// 来場者情報パネル。
    /// プレイヤーが来場者をタップした時に表示される詳細ポップアップ。
    /// 来場者の全パラメータ、感情状態、訪問履歴を可視化する。
    /// </summary>
    public class VisitorInfoPanel : MonoBehaviour
    {
        // ============================================================
        // ヘッダー情報
        // ============================================================

        [Header("ヘッダー")]
        [SerializeField] private TextMeshProUGUI visitorNameText;
        [SerializeField] private TextMeshProUGUI visitorTypeText;
        [SerializeField] private Image visitorPortrait;
        [SerializeField] private Image visitorTypeBadge;

        // ============================================================
        // パラメータバー
        // ============================================================

        [Header("パラメータバー")]
        [SerializeField] private ParameterBarUI happinessBar;
        [SerializeField] private ParameterBarUI hungerBar;
        [SerializeField] private ParameterBarUI thirstBar;
        [SerializeField] private ParameterBarUI toiletBar;
        [SerializeField] private ParameterBarUI energyBar;
        [SerializeField] private ParameterBarUI nauseaBar;
        [SerializeField] private ParameterBarUI excitementBar;

        // ============================================================
        // 感情バブル
        // ============================================================

        [Header("感情バブル")]
        [SerializeField] private Image emotionBubbleIcon;
        [SerializeField] private Image emotionBubbleBackground;
        [SerializeField] private TextMeshProUGUI emotionText;
        [SerializeField] private TextMeshProUGUI behaviorStateText;

        // ============================================================
        // 所持金・訪問履歴
        // ============================================================

        [Header("所持金")]
        [SerializeField] private TextMeshProUGUI cashRemainingText;

        [Header("訪問履歴")]
        [SerializeField] private Transform visitedListContainer;
        [SerializeField] private GameObject visitedItemPrefab;
        [SerializeField] private ScrollRect visitedListScrollRect;
        [SerializeField] private TextMeshProUGUI visitedCountText;

        // ============================================================
        // アクションボタン
        // ============================================================

        [Header("アクションボタン")]
        [SerializeField] private Button talkButton;
        [SerializeField] private TextMeshProUGUI talkButtonText;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button followButton;
        [SerializeField] private TextMeshProUGUI followButtonText;

        // ============================================================
        // パネルアニメーション
        // ============================================================

        [Header("アニメーション")]
        [SerializeField] private Animator panelAnimator;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float fadeInDuration = 0.3f;

        // ============================================================
        // 内部状態
        // ============================================================

        /// <summary>現在表示中の来場者ID</summary>
        private int _currentVisitorId = -1;

        /// <summary>フォロー中かどうか</summary>
        private bool _isFollowing;

        /// <summary>訪問履歴の生成済みUI要素</summary>
        private readonly List<GameObject> _spawnedVisitedItems = new();

        // ============================================================
        // Unity ライフサイクル
        // ============================================================

        private void OnEnable()
        {
            SubscribeToEvents();
            BindButtons();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        private void Update()
        {
            // パネル表示中は来場者データをリアルタイム更新
            if (_currentVisitorId >= 0)
            {
                RefreshVisitorData(_currentVisitorId);
            }
        }

        // ============================================================
        // イベント購読
        // ============================================================

        private void SubscribeToEvents()
        {
            GameEvents.OnVisitorSelected += HandleVisitorSelected;
            GameEvents.OnVisitorEmotionChanged += HandleEmotionChanged;
            GameEvents.OnVisitorHappinessChanged += HandleHappinessChanged;
            GameEvents.OnVisitorLeavePark += HandleVisitorLeftPark;
        }

        private void UnsubscribeFromEvents()
        {
            GameEvents.OnVisitorSelected -= HandleVisitorSelected;
            GameEvents.OnVisitorEmotionChanged -= HandleEmotionChanged;
            GameEvents.OnVisitorHappinessChanged -= HandleHappinessChanged;
            GameEvents.OnVisitorLeavePark -= HandleVisitorLeftPark;
        }

        /// <summary>ボタンのイベントをバインドする</summary>
        private void BindButtons()
        {
            talkButton?.onClick.AddListener(OnTalkClicked);
            closeButton?.onClick.AddListener(OnCloseClicked);
            followButton?.onClick.AddListener(OnFollowClicked);
        }

        // ============================================================
        // パネル表示制御
        // ============================================================

        /// <summary>来場者選択イベントハンドラ - パネルを開く</summary>
        private void HandleVisitorSelected(int visitorId)
        {
            ShowPanel(visitorId);
        }

        /// <summary>指定来場者の情報パネルを表示する</summary>
        public void ShowPanel(int visitorId)
        {
            _currentVisitorId = visitorId;
            _isFollowing = false;

            gameObject.SetActive(true);

            // フェードインアニメーション
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                LeanTweenHelper.FadeIn(canvasGroup, fadeInDuration);
            }

            if (panelAnimator != null)
            {
                panelAnimator.SetTrigger("Open");
            }

            // データ取得と表示
            PopulateVisitorInfo(visitorId);

            WebGLOptimizer.LogVerbose($"[VisitorInfoPanel] 来場者情報パネルを表示: ID={visitorId}");
        }

        /// <summary>パネルを閉じる</summary>
        public void ClosePanel()
        {
            _currentVisitorId = -1;
            _isFollowing = false;

            if (panelAnimator != null)
            {
                panelAnimator.SetTrigger("Close");
            }

            // アニメーション後に非アクティブ化
            // （AnimatorのStateEventで呼び出すか、コルーチンで遅延させる）
            gameObject.SetActive(false);

            ClearVisitedList();
        }

        // ============================================================
        // データ表示
        // ============================================================

        /// <summary>来場者情報を取得してUIに反映する</summary>
        private void PopulateVisitorInfo(int visitorId)
        {
            var visitorManager = GameManager.Instance?.VisitorManager;
            if (visitorManager == null) return;

            // VisitorManagerから来場者データを取得
            var data = visitorManager.GetVisitorData(visitorId);
            if (data == null)
            {
                Debug.LogWarning($"[VisitorInfoPanel] 来場者データが見つかりません: ID={visitorId}");
                ClosePanel();
                return;
            }

            // ヘッダー情報
            if (visitorNameText != null)
                visitorNameText.text = data.Name;

            if (visitorTypeText != null)
                visitorTypeText.text = GetVisitorTypeDisplayName(data.Type);

            if (visitorPortrait != null && data.Portrait != null)
                visitorPortrait.sprite = data.Portrait;

            // パラメータバー
            UpdateParameterBars(data);

            // 感情バブル
            UpdateEmotionDisplay(data.CurrentEmotion, data.CurrentBehaviorState);

            // 所持金
            if (cashRemainingText != null)
                cashRemainingText.text = $"所持金: ¥{data.CashRemaining:N0}";

            // 訪問履歴
            PopulateVisitedList(data.VisitedAttractions);

            // 会話ボタンの有効/無効
            UpdateTalkButtonState(data);
        }

        /// <summary>来場者データをリアルタイム更新する（パネル表示中毎フレーム呼ばれる）</summary>
        private void RefreshVisitorData(int visitorId)
        {
            var visitorManager = GameManager.Instance?.VisitorManager;
            if (visitorManager == null) return;

            var data = visitorManager.GetVisitorData(visitorId);
            if (data == null) return;

            // パラメータバーのスムーズ更新
            UpdateParameterBars(data);

            // 所持金更新
            if (cashRemainingText != null)
                cashRemainingText.text = $"所持金: ¥{data.CashRemaining:N0}";
        }

        /// <summary>パラメータバーを更新する</summary>
        private void UpdateParameterBars(VisitorDisplayData data)
        {
            happinessBar?.SetValue(data.Happiness, "幸福度");
            hungerBar?.SetValue(data.Hunger, "空腹");
            thirstBar?.SetValue(data.Thirst, "のどの渇き");
            toiletBar?.SetValue(data.ToiletUrgency, "トイレ");
            energyBar?.SetValue(data.Energy, "体力");
            nauseaBar?.SetValue(data.Nausea, "酔い");
            excitementBar?.SetValue(data.Excitement, "興奮度");
        }

        /// <summary>感情バブル表示を更新する</summary>
        private void UpdateEmotionDisplay(EmotionBubbleType emotion, VisitorBehaviorState behaviorState)
        {
            if (emotionText != null)
            {
                emotionText.text = GetEmotionDisplayText(emotion);
            }

            if (emotionBubbleBackground != null)
            {
                emotionBubbleBackground.color = GetEmotionBubbleColor(emotion);
            }

            if (behaviorStateText != null)
            {
                behaviorStateText.text = GetBehaviorStateDisplayText(behaviorState);
            }
        }

        // ============================================================
        // 訪問履歴
        // ============================================================

        /// <summary>訪問済みアトラクション一覧を表示する</summary>
        private void PopulateVisitedList(List<VisitedAttractionEntry> visitedAttractions)
        {
            ClearVisitedList();

            if (visitedListContainer == null || visitedItemPrefab == null) return;
            if (visitedAttractions == null || visitedAttractions.Count == 0)
            {
                if (visitedCountText != null)
                    visitedCountText.text = "訪問履歴: まだありません";
                return;
            }

            if (visitedCountText != null)
                visitedCountText.text = $"訪問履歴: {visitedAttractions.Count}件";

            foreach (var entry in visitedAttractions)
            {
                GameObject itemObj = Instantiate(visitedItemPrefab, visitedListContainer);
                _spawnedVisitedItems.Add(itemObj);

                TextMeshProUGUI nameText = itemObj.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
                TextMeshProUGUI ratingText = itemObj.transform.Find("RatingText")?.GetComponent<TextMeshProUGUI>();
                Image ratingIcon = itemObj.transform.Find("RatingIcon")?.GetComponent<Image>();

                if (nameText != null) nameText.text = entry.AttractionName;
                if (ratingText != null)
                {
                    string ratingStr = entry.SatisfactionRating switch
                    {
                        >= 0.8f => "最高！",
                        >= 0.6f => "楽しい",
                        >= 0.4f => "普通",
                        >= 0.2f => "微妙…",
                        _       => "つまらない"
                    };
                    ratingText.text = ratingStr;
                }
            }
        }

        /// <summary>訪問履歴リストをクリアする</summary>
        private void ClearVisitedList()
        {
            foreach (var item in _spawnedVisitedItems)
            {
                if (item != null) Destroy(item);
            }
            _spawnedVisitedItems.Clear();
        }

        // ============================================================
        // ボタンアクション
        // ============================================================

        /// <summary>「話す」ボタン押下 - AI会話を開始する</summary>
        private void OnTalkClicked()
        {
            if (_currentVisitorId < 0) return;

            var gm = GameManager.Instance;
            if (gm == null) return;

            // 住人モードに切り替え（まだでない場合）
            if (gm.CurrentViewMode != ViewMode.ResidentView)
            {
                gm.SwitchViewMode(ViewMode.ResidentView);
            }

            // AI会話開始イベント発火
            GameEvents.FireNPCConversationStarted(_currentVisitorId, "visitor_greeting");

            WebGLOptimizer.LogVerbose($"[VisitorInfoPanel] 来場者 ID={_currentVisitorId} との会話を開始");
        }

        /// <summary>「閉じる」ボタン押下</summary>
        private void OnCloseClicked()
        {
            ClosePanel();
        }

        /// <summary>「フォロー」ボタン押下 - カメラで来場者を追跡する</summary>
        private void OnFollowClicked()
        {
            _isFollowing = !_isFollowing;

            if (followButtonText != null)
            {
                followButtonText.text = _isFollowing ? "フォロー解除" : "フォローする";
            }

            // カメラシステムにフォロー対象を通知
            if (_isFollowing && _currentVisitorId >= 0)
            {
                GameEvents.FireCameraFollowRequested(_currentVisitorId);
            }
            WebGLOptimizer.LogVerbose($"[VisitorInfoPanel] フォロー{(_isFollowing ? "開始" : "解除")}: ID={_currentVisitorId}");
        }

        /// <summary>会話ボタンの有効/無効を更新する</summary>
        private void UpdateTalkButtonState(VisitorDisplayData data)
        {
            if (talkButton == null) return;

            // 退園中・搭乗中など会話不可能な状態をチェック
            bool canTalk = data.CurrentBehaviorState != VisitorBehaviorState.LeavingPark
                        && data.CurrentBehaviorState != VisitorBehaviorState.RidingAttraction
                        && data.CurrentBehaviorState != VisitorBehaviorState.Vomiting
                        && data.CurrentBehaviorState != VisitorBehaviorState.TalkingToPlayer;

            talkButton.interactable = canTalk;

            if (talkButtonText != null)
            {
                talkButtonText.text = canTalk ? "話しかける" : "会話不可";
            }
        }

        // ============================================================
        // イベントハンドラ
        // ============================================================

        /// <summary>感情変化イベントハンドラ</summary>
        private void HandleEmotionChanged(int visitorId, EmotionBubbleType emotion)
        {
            if (visitorId != _currentVisitorId) return;

            var data = GameManager.Instance?.VisitorManager?.GetVisitorData(visitorId);
            if (data != null)
            {
                UpdateEmotionDisplay(emotion, data.CurrentBehaviorState);
            }
        }

        /// <summary>幸福度変化イベントハンドラ</summary>
        private void HandleHappinessChanged(int visitorId, float happiness)
        {
            if (visitorId != _currentVisitorId) return;
            happinessBar?.SetValue(happiness, "幸福度");
        }

        /// <summary>来場者退園イベントハンドラ - 表示中の来場者が退園した場合パネルを閉じる</summary>
        private void HandleVisitorLeftPark(int visitorId)
        {
            if (visitorId == _currentVisitorId)
            {
                WebGLOptimizer.LogVerbose($"[VisitorInfoPanel] 表示中の来場者が退園しました: ID={visitorId}");
                ClosePanel();
            }
        }

        // ============================================================
        // 表示名ヘルパー
        // ============================================================

        /// <summary>来場者タイプの日本語表示名を取得する</summary>
        private string GetVisitorTypeDisplayName(VisitorType type)
        {
            return type switch
            {
                VisitorType.Kids   => "キッズ",
                VisitorType.Young  => "ヤング",
                VisitorType.Family => "ファミリー",
                VisitorType.Couple => "カップル",
                VisitorType.Senior => "シニア",
                VisitorType.VIP    => "VIP",
                _                  => "不明"
            };
        }

        /// <summary>感情バブルの日本語表示テキストを取得する</summary>
        private string GetEmotionDisplayText(EmotionBubbleType emotion)
        {
            return emotion switch
            {
                EmotionBubbleType.LookingForExit    => "出口を探している",
                EmotionBubbleType.LookingForToilet  => "トイレを探している",
                EmotionBubbleType.LookingForFood    => "食べ物を探している",
                EmotionBubbleType.Lost              => "迷子になっている",
                EmotionBubbleType.Hungry            => "お腹がすいた",
                EmotionBubbleType.Thirsty           => "のどが渇いた",
                EmotionBubbleType.CurrentlyEating   => "食事中",
                EmotionBubbleType.RodeAllRides      => "全部乗った！",
                EmotionBubbleType.NoMoney           => "お金がない…",
                EmotionBubbleType.Resting           => "休憩中",
                EmotionBubbleType.FoodTastesBad     => "まずい！",
                EmotionBubbleType.TooExpensive      => "高すぎる！",
                EmotionBubbleType.TooDirty          => "汚い！",
                EmotionBubbleType.NotExcitingEnough => "つまらない",
                EmotionBubbleType.LongWait          => "待ち時間が長い",
                EmotionBubbleType.Interesting       => "面白い！",
                EmotionBubbleType.Average           => "まあまあ",
                EmotionBubbleType.Boring            => "退屈…",
                EmotionBubbleType.BestRide          => "最高の乗り物！",
                EmotionBubbleType.BestShop          => "最高のお店！",
                EmotionBubbleType.LovingIt          => "大満足！",
                _                                   => ""
            };
        }

        /// <summary>感情バブルの背景色を取得する</summary>
        private Color GetEmotionBubbleColor(EmotionBubbleType emotion)
        {
            // EmotionBubbleColorの分類に基づいて色を返す
            return emotion switch
            {
                // 緑系 - 迷子・探索
                EmotionBubbleType.LookingForExit or
                EmotionBubbleType.LookingForToilet or
                EmotionBubbleType.LookingForFood or
                EmotionBubbleType.Lost
                    => new Color(0.4f, 0.8f, 0.4f, 0.9f),

                // 黄系 - 食欲・渇き
                EmotionBubbleType.Hungry or
                EmotionBubbleType.Thirsty
                    => new Color(1f, 0.9f, 0.3f, 0.9f),

                // 水色系 - 現在の状態
                EmotionBubbleType.CurrentlyEating or
                EmotionBubbleType.RodeAllRides or
                EmotionBubbleType.NoMoney or
                EmotionBubbleType.Resting
                    => new Color(0.6f, 0.85f, 1f, 0.9f),

                // 灰色系 - 不満
                EmotionBubbleType.FoodTastesBad or
                EmotionBubbleType.TooExpensive or
                EmotionBubbleType.TooDirty or
                EmotionBubbleType.NotExcitingEnough or
                EmotionBubbleType.LongWait
                    => new Color(0.6f, 0.6f, 0.6f, 0.9f),

                // 白系 - 評価
                EmotionBubbleType.Interesting or
                EmotionBubbleType.Average or
                EmotionBubbleType.Boring
                    => new Color(1f, 1f, 1f, 0.9f),

                // 青系 - 最高評価
                EmotionBubbleType.BestRide or
                EmotionBubbleType.BestShop or
                EmotionBubbleType.LovingIt
                    => new Color(0.3f, 0.5f, 1f, 0.9f),

                _ => Color.white
            };
        }

        /// <summary>行動状態の日本語表示テキストを取得する</summary>
        private string GetBehaviorStateDisplayText(VisitorBehaviorState state)
        {
            return state switch
            {
                VisitorBehaviorState.Idle                 => "うろうろしている",
                VisitorBehaviorState.WalkingToAttraction  => "アトラクションへ移動中",
                VisitorBehaviorState.WaitingInQueue       => "並んでいる",
                VisitorBehaviorState.RidingAttraction     => "搭乗中",
                VisitorBehaviorState.WalkingToShop        => "お店へ移動中",
                VisitorBehaviorState.Eating               => "食事中",
                VisitorBehaviorState.Drinking             => "飲み物を飲んでいる",
                VisitorBehaviorState.WalkingToToilet      => "トイレへ急いでいる",
                VisitorBehaviorState.UsingToilet          => "トイレ使用中",
                VisitorBehaviorState.Resting              => "ベンチで休憩中",
                VisitorBehaviorState.WatchingEntertainment => "ショーを見ている",
                VisitorBehaviorState.LookingAtMap         => "地図を見ている",
                VisitorBehaviorState.Vomiting             => "気分が悪い…",
                VisitorBehaviorState.LeavingPark          => "帰宅中",
                VisitorBehaviorState.TalkingToPlayer      => "プレイヤーと会話中",
                _                                         => "不明"
            };
        }
    }

    // ============================================================
    // パラメータバーUIコンポーネント
    // ============================================================

    /// <summary>
    /// 来場者パラメータバーの共通UIコンポーネント。
    /// Slider + ラベル + 値テキストを一体管理する。
    /// </summary>
    [Serializable]
    public class ParameterBarUI
    {
        [SerializeField] private Slider slider;
        [SerializeField] private TextMeshProUGUI labelText;
        [SerializeField] private TextMeshProUGUI valueText;
        [SerializeField] private Image fillImage;

        /// <summary>バーの閾値に応じた色のグラデーション</summary>
        [SerializeField] private Gradient colorGradient;

        /// <summary>スムーズ補間速度</summary>
        private const float LerpSpeed = 5f;

        /// <summary>バーの値を設定する（スムーズ補間あり）</summary>
        public void SetValue(float value, string label = null)
        {
            if (slider != null)
            {
                slider.value = Mathf.Lerp(slider.value, value, Time.unscaledDeltaTime * LerpSpeed);
            }

            if (labelText != null && label != null)
            {
                labelText.text = label;
            }

            if (valueText != null)
            {
                valueText.text = $"{Mathf.RoundToInt(value * 100)}%";
            }

            // バーの色を閾値に応じて変更
            if (fillImage != null && colorGradient != null)
            {
                fillImage.color = colorGradient.Evaluate(value);
            }
        }
    }

    // ============================================================
    // 来場者表示用データクラス
    // ============================================================

    /// <summary>
    /// UIに表示するための来場者データ。
    /// VisitorManagerから取得される軽量なデータ構造。
    /// </summary>
    public class VisitorDisplayData
    {
        public int VisitorId;
        public string Name;
        public VisitorType Type;
        public Sprite Portrait;

        // パラメータ (0.0 - 1.0)
        public float Happiness;
        public float Hunger;
        public float Thirst;
        public float ToiletUrgency;
        public float Energy;
        public float Nausea;
        public float Excitement;

        // 状態
        public EmotionBubbleType CurrentEmotion;
        public VisitorBehaviorState CurrentBehaviorState;
        public float CashRemaining;

        // 訪問履歴
        public List<VisitedAttractionEntry> VisitedAttractions = new();
    }

    /// <summary>訪問済みアトラクションの記録</summary>
    public class VisitedAttractionEntry
    {
        public int AttractionId;
        public string AttractionName;
        public float SatisfactionRating; // 0.0 - 1.0
    }

    // ============================================================
    // LeanTweenヘルパー（簡易フェードアニメーション）
    // ============================================================

    /// <summary>CanvasGroupのフェードアニメーション用ヘルパー</summary>
    public static class LeanTweenHelper
    {
        /// <summary>CanvasGroupのアルファを0→1にフェードインする</summary>
        public static void FadeIn(CanvasGroup canvasGroup, float duration)
        {
            if (canvasGroup == null) return;
            // LeanTweenまたはDOTweenが導入されている場合はそちらを使用
            // フォールバック: コルーチンベースの簡易実装
            canvasGroup.alpha = 1f; // 即座に表示（Tweenライブラリ未導入時のフォールバック）
        }

        /// <summary>CanvasGroupのアルファを1→0にフェードアウトする</summary>
        public static void FadeOut(CanvasGroup canvasGroup, float duration)
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = 0f;
        }
    }
}
