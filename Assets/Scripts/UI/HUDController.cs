// ============================================================
// ThemeParkGame - HUDController
// メインHUDオーバーレイ：資金、日時、パーク評価、速度制御、
// 来場者数、天候、ゴールデンチケット、通知ベル、視点切替、建設ボタン
// ============================================================

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ThemeParkGame.Core;

namespace ThemeParkGame.UI
{
    /// <summary>
    /// メインHUDコントローラー。
    /// 画面上部に常時表示されるオーバーレイUIを管理し、
    /// プレイヤーに経営状況をリアルタイムで提供する。
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        // ============================================================
        // トップバー要素
        // ============================================================

        [Header("トップバー - 資金表示")]
        [SerializeField] private TextMeshProUGUI moneyText;
        [Tooltip("資金変動時のアニメーション表示用")]
        [SerializeField] private TextMeshProUGUI moneyDeltaText;

        [Header("トップバー - 日時表示")]
        [SerializeField] private TextMeshProUGUI dateTimeText;

        [Header("トップバー - パーク評価")]
        [SerializeField] private Image[] ratingStars;
        [SerializeField] private Sprite starFilledSprite;
        [SerializeField] private Sprite starEmptySprite;
        [SerializeField] private Sprite starHalfSprite;
        private const int MaxStars = 5;

        // ============================================================
        // 速度制御
        // ============================================================

        [Header("速度制御ボタン")]
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button speed1xButton;
        [SerializeField] private Button speed2xButton;
        [SerializeField] private Button speed3xButton;
        [SerializeField] private Image pauseButtonIcon;
        [SerializeField] private Image speed1xButtonIcon;
        [SerializeField] private Image speed2xButtonIcon;
        [SerializeField] private Image speed3xButtonIcon;

        [Tooltip("選択中の速度ボタンに適用する色")]
        [SerializeField] private Color activeSpeedColor = new Color(0.2f, 0.8f, 0.4f, 1f);
        [SerializeField] private Color inactiveSpeedColor = Color.white;

        // ============================================================
        // 来場者数・天候・ゴールデンチケット
        // ============================================================

        [Header("来場者数")]
        [SerializeField] private TextMeshProUGUI visitorCountText;
        [SerializeField] private Image visitorTrendIcon;

        [Header("天候インジケータ")]
        [SerializeField] private Image weatherIcon;
        [SerializeField] private TextMeshProUGUI weatherText;
        [SerializeField] private Sprite[] weatherSprites; // Sunny=0, Cloudy=1, Rainy=2, Snowy=3, Hot=4

        [Header("ゴールデンチケット")]
        [SerializeField] private TextMeshProUGUI goldenTicketCountText;
        [SerializeField] private Image goldenTicketIcon;
        [SerializeField] private Animator goldenTicketAnimator;

        // ============================================================
        // 通知ベル
        // ============================================================

        [Header("通知ベル")]
        [SerializeField] private Button notificationBellButton;
        [SerializeField] private TextMeshProUGUI unreadCountText;
        [SerializeField] private GameObject unreadBadge;
        [SerializeField] private Animator bellAnimator;

        // ============================================================
        // 視点切替・建設モード
        // ============================================================

        [Header("視点切替")]
        [SerializeField] private Button viewModeToggleButton;
        [SerializeField] private TextMeshProUGUI viewModeText;
        [SerializeField] private Image viewModeIcon;

        [Header("建設モード")]
        [SerializeField] private Button buildModeButton;
        [SerializeField] private TextMeshProUGUI buildModeButtonText;

        // ============================================================
        // 内部状態
        // ============================================================

        /// <summary>現在の来場者数（UIアニメーション用にキャッシュ）</summary>
        private int _currentVisitorCount;

        /// <summary>未読通知数</summary>
        private int _unreadNotificationCount;

        /// <summary>現在の表示資金（スムーズなカウントアップ用）</summary>
        private float _displayedMoney;
        private float _targetMoney;

        /// <summary>資金変動表示の残り時間</summary>
        private float _moneyDeltaDisplayTimer;
        private const float MoneyDeltaDisplayDuration = 2f;

        /// <summary>資金カウントアップ速度</summary>
        private const float MoneyLerpSpeed = 8f;

        /// <summary>ボタンバインド済みフラグ（多重バインド防止）</summary>
        private bool _buttonsAreBound;

        // ============================================================
        // Unity ライフサイクル
        // ============================================================

        private void Awake()
        {
            ValidateReferences();
        }

        private void OnEnable()
        {
            SubscribeToEvents();
            BindButtons();
        }

        private void OnDisable()
        {
            UnbindButtons();
            UnsubscribeFromEvents();
        }

        private void Start()
        {
            // 初期状態を反映
            RefreshAllDisplays();
        }

        private void Update()
        {
            UpdateDateTimeDisplay();
            UpdateMoneyAnimation();
            UpdateMoneyDeltaFade();
        }

        // ============================================================
        // 初期化・バリデーション
        // ============================================================

        /// <summary>SerializeField参照が設定されているか検証する</summary>
        private void ValidateReferences()
        {
            if (moneyText == null)
                WebGLOptimizer.LogWarning("[HUDController] moneyText が未設定です");
            if (dateTimeText == null)
                WebGLOptimizer.LogWarning("[HUDController] dateTimeText が未設定です");
            if (visitorCountText == null)
                WebGLOptimizer.LogWarning("[HUDController] visitorCountText が未設定です");
        }

        /// <summary>全表示を最新状態に更新する</summary>
        private void RefreshAllDisplays()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            // 資金
            if (gm.EconomyManager != null)
            {
                _targetMoney = gm.EconomyManager.CurrentMoney;
                _displayedMoney = _targetMoney;
                UpdateMoneyText(_displayedMoney);
            }

            // 速度
            UpdateSpeedButtonVisuals(gm.SpeedLevel);

            // ゴールデンチケット
            UpdateGoldenTicketDisplay(gm.GoldenTickets);

            // 視点モード
            UpdateViewModeDisplay(gm.CurrentViewMode);

            // 通知バッジ
            UpdateNotificationBadge();
        }

        // ============================================================
        // イベント購読
        // ============================================================

        private void SubscribeToEvents()
        {
            GameEvents.OnMoneyChanged += HandleMoneyChanged;
            GameEvents.OnVisitorEnterPark += HandleVisitorEntered;
            GameEvents.OnVisitorLeavePark += HandleVisitorLeft;
            GameEvents.OnWeatherChanged += HandleWeatherChanged;
            GameEvents.OnGoldenTicketEarned += HandleGoldenTicketEarned;
            GameEvents.OnViewModeChanged += HandleViewModeChanged;
        }

        private void UnsubscribeFromEvents()
        {
            GameEvents.OnMoneyChanged -= HandleMoneyChanged;
            GameEvents.OnVisitorEnterPark -= HandleVisitorEntered;
            GameEvents.OnVisitorLeavePark -= HandleVisitorLeft;
            GameEvents.OnWeatherChanged -= HandleWeatherChanged;
            GameEvents.OnGoldenTicketEarned -= HandleGoldenTicketEarned;
            GameEvents.OnViewModeChanged -= HandleViewModeChanged;
        }

        // ============================================================
        // ボタンバインド
        // ============================================================

        private void BindButtons()
        {
            if (_buttonsAreBound) return;
            _buttonsAreBound = true;

            // 速度制御ボタン（ラムダのため RemoveAllListeners で一括クリア後に追加）
            pauseButton?.onClick.RemoveListener(OnPauseClicked);
            pauseButton?.onClick.AddListener(OnPauseClicked);
            if (speed1xButton != null) { speed1xButton.onClick.RemoveAllListeners(); speed1xButton.onClick.AddListener(() => OnSpeedClicked(1)); }
            if (speed2xButton != null) { speed2xButton.onClick.RemoveAllListeners(); speed2xButton.onClick.AddListener(() => OnSpeedClicked(2)); }
            if (speed3xButton != null) { speed3xButton.onClick.RemoveAllListeners(); speed3xButton.onClick.AddListener(() => OnSpeedClicked(3)); }

            // 通知ベル
            notificationBellButton?.onClick.RemoveListener(OnNotificationBellClicked);
            notificationBellButton?.onClick.AddListener(OnNotificationBellClicked);

            // 視点切替
            viewModeToggleButton?.onClick.RemoveListener(OnViewModeToggleClicked);
            viewModeToggleButton?.onClick.AddListener(OnViewModeToggleClicked);

            // 建設モード
            buildModeButton?.onClick.RemoveListener(OnBuildModeClicked);
            buildModeButton?.onClick.AddListener(OnBuildModeClicked);
        }

        /// <summary>ボタンのイベントを解除する</summary>
        private void UnbindButtons()
        {
            if (!_buttonsAreBound) return;
            _buttonsAreBound = false;

            pauseButton?.onClick.RemoveListener(OnPauseClicked);
            speed1xButton?.onClick.RemoveAllListeners();
            speed2xButton?.onClick.RemoveAllListeners();
            speed3xButton?.onClick.RemoveAllListeners();
            notificationBellButton?.onClick.RemoveListener(OnNotificationBellClicked);
            viewModeToggleButton?.onClick.RemoveListener(OnViewModeToggleClicked);
            buildModeButton?.onClick.RemoveListener(OnBuildModeClicked);
        }

        // ============================================================
        // 速度制御
        // ============================================================

        /// <summary>一時停止ボタン押下</summary>
        private void OnPauseClicked()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (gm.IsPaused)
            {
                gm.ResumeGame();
                UpdateSpeedButtonVisuals(gm.SpeedLevel);
            }
            else
            {
                gm.PauseGame();
                UpdateSpeedButtonVisuals(0);
            }
        }

        /// <summary>速度変更ボタン押下（1x/2x/3x）</summary>
        private void OnSpeedClicked(int speedLevel)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            // 一時停止中の場合は再開してから速度を設定
            if (gm.IsPaused)
            {
                gm.ResumeGame();
            }

            gm.SpeedLevel = speedLevel;
            UpdateSpeedButtonVisuals(speedLevel);
        }

        /// <summary>速度ボタンのビジュアル状態を更新する</summary>
        private void UpdateSpeedButtonVisuals(int activeLevel)
        {
            SetButtonColor(pauseButtonIcon, activeLevel == 0);
            SetButtonColor(speed1xButtonIcon, activeLevel == 1);
            SetButtonColor(speed2xButtonIcon, activeLevel == 2);
            SetButtonColor(speed3xButtonIcon, activeLevel == 3);
        }

        /// <summary>ボタンアイコンの色を選択状態に応じて設定する</summary>
        private void SetButtonColor(Image icon, bool isActive)
        {
            if (icon != null)
            {
                icon.color = isActive ? activeSpeedColor : inactiveSpeedColor;
            }
        }

        // ============================================================
        // 資金表示
        // ============================================================

        /// <summary>資金変更イベントハンドラ</summary>
        private void HandleMoneyChanged(float newAmount)
        {
            float delta = newAmount - _targetMoney;
            _targetMoney = newAmount;

            // 変動テキスト表示
            ShowMoneyDelta(delta);
        }

        /// <summary>資金カウントアップアニメーションを更新する</summary>
        private void UpdateMoneyAnimation()
        {
            if (Mathf.Approximately(_displayedMoney, _targetMoney)) return;

            _displayedMoney = Mathf.Lerp(_displayedMoney, _targetMoney, Time.unscaledDeltaTime * MoneyLerpSpeed);

            // 差が十分小さければスナップ
            if (Mathf.Abs(_displayedMoney - _targetMoney) < 1f)
            {
                _displayedMoney = _targetMoney;
            }

            UpdateMoneyText(_displayedMoney);
        }

        /// <summary>資金テキストを更新する</summary>
        private void UpdateMoneyText(float amount)
        {
            if (moneyText != null)
            {
                // 日本円形式で表示（例: ¥1,234,567）
                moneyText.text = $"¥{amount:N0}";

                // 赤字の場合は赤色で表示
                moneyText.color = amount < 0 ? Color.red : Color.white;
            }
        }

        /// <summary>資金変動額をポップアップ表示する</summary>
        private void ShowMoneyDelta(float delta)
        {
            if (moneyDeltaText == null || Mathf.Approximately(delta, 0f)) return;

            string sign = delta > 0 ? "+" : "";
            moneyDeltaText.text = $"{sign}¥{delta:N0}";
            moneyDeltaText.color = delta > 0 ? Color.green : Color.red;
            moneyDeltaText.gameObject.SetActive(true);
            _moneyDeltaDisplayTimer = MoneyDeltaDisplayDuration;
        }

        /// <summary>資金変動テキストのフェードアウトを管理する</summary>
        private void UpdateMoneyDeltaFade()
        {
            if (moneyDeltaText == null || !moneyDeltaText.gameObject.activeSelf) return;

            _moneyDeltaDisplayTimer -= Time.unscaledDeltaTime;
            if (_moneyDeltaDisplayTimer <= 0f)
            {
                moneyDeltaText.gameObject.SetActive(false);
            }
            else if (_moneyDeltaDisplayTimer < 0.5f)
            {
                // フェードアウト
                Color c = moneyDeltaText.color;
                c.a = _moneyDeltaDisplayTimer / 0.5f;
                moneyDeltaText.color = c;
            }
        }

        // ============================================================
        // 日時表示
        // ============================================================

        /// <summary>日時テキストを毎フレーム更新する</summary>
        private void UpdateDateTimeDisplay()
        {
            if (dateTimeText == null) return;

            var tm = GameManager.Instance?.TimeManager;
            if (tm == null) return;

            dateTimeText.text = tm.GetFormattedDateTime();
        }

        // ============================================================
        // パーク評価（星表示）
        // ============================================================

        /// <summary>
        /// パーク評価を星表示で更新する。
        /// rating: 0.0〜5.0の評価値
        /// </summary>
        public void UpdateParkRating(float rating)
        {
            if (ratingStars == null) return;

            rating = Mathf.Clamp(rating, 0f, MaxStars);

            for (int i = 0; i < ratingStars.Length && i < MaxStars; i++)
            {
                if (ratingStars[i] == null) continue;

                float threshold = i + 1f;
                if (rating >= threshold)
                {
                    // 満点の星
                    ratingStars[i].sprite = starFilledSprite;
                }
                else if (rating >= threshold - 0.5f)
                {
                    // 半分の星
                    ratingStars[i].sprite = starHalfSprite;
                }
                else
                {
                    // 空の星
                    ratingStars[i].sprite = starEmptySprite;
                }
            }
        }

        // ============================================================
        // 来場者数
        // ============================================================

        /// <summary>来場者入園イベントハンドラ</summary>
        private void HandleVisitorEntered(int visitorId)
        {
            _currentVisitorCount++;
            UpdateVisitorCountDisplay();
        }

        /// <summary>来場者退園イベントハンドラ</summary>
        private void HandleVisitorLeft(int visitorId)
        {
            _currentVisitorCount = Mathf.Max(0, _currentVisitorCount - 1);
            UpdateVisitorCountDisplay();
        }

        /// <summary>来場者数テキストを更新する</summary>
        private void UpdateVisitorCountDisplay()
        {
            if (visitorCountText != null)
            {
                visitorCountText.text = $"来場者: {_currentVisitorCount}人";
            }
        }

        /// <summary>来場者数を外部から直接設定する（初期同期用）</summary>
        public void SetVisitorCount(int count)
        {
            _currentVisitorCount = count;
            UpdateVisitorCountDisplay();
        }

        // ============================================================
        // 天候インジケータ
        // ============================================================

        /// <summary>天候変更イベントハンドラ</summary>
        private void HandleWeatherChanged(Weather weather)
        {
            UpdateWeatherDisplay(weather);
        }

        /// <summary>天候表示を更新する</summary>
        private void UpdateWeatherDisplay(Weather weather)
        {
            // アイコン更新
            if (weatherIcon != null && weatherSprites != null)
            {
                int index = (int)weather;
                if (index >= 0 && index < weatherSprites.Length)
                {
                    weatherIcon.sprite = weatherSprites[index];
                }
            }

            // テキスト更新
            if (weatherText != null)
            {
                weatherText.text = GetWeatherDisplayName(weather);
            }
        }

        /// <summary>天候の日本語表示名を取得する</summary>
        private string GetWeatherDisplayName(Weather weather)
        {
            return weather switch
            {
                Weather.Sunny  => "晴れ",
                Weather.Cloudy => "くもり",
                Weather.Rainy  => "雨",
                Weather.Snowy  => "雪",
                Weather.Hot    => "猛暑",
                _              => "不明"
            };
        }

        // ============================================================
        // ゴールデンチケット
        // ============================================================

        /// <summary>ゴールデンチケット獲得イベントハンドラ</summary>
        private void HandleGoldenTicketEarned(int totalCount)
        {
            UpdateGoldenTicketDisplay(totalCount);

            // 獲得アニメーション再生
            if (goldenTicketAnimator != null)
            {
                goldenTicketAnimator.SetTrigger("Earned");
            }
        }

        /// <summary>ゴールデンチケット数表示を更新する</summary>
        private void UpdateGoldenTicketDisplay(int count)
        {
            if (goldenTicketCountText != null)
            {
                goldenTicketCountText.text = $"×{count}";
            }
        }

        // ============================================================
        // 通知ベル
        // ============================================================

        /// <summary>通知ベル押下ハンドラ</summary>
        private void OnNotificationBellClicked()
        {
            // 未読カウントをリセット
            _unreadNotificationCount = 0;
            UpdateNotificationBadge();

            // NotificationSystemの通知ログを開くイベントを発行
            // （NotificationSystemが購読して処理する）
            WebGLOptimizer.LogVerbose("[HUDController] 通知ログを開きます");
        }

        /// <summary>未読通知数を加算する（NotificationSystemから呼ばれる）</summary>
        public void IncrementUnreadCount()
        {
            _unreadNotificationCount++;
            UpdateNotificationBadge();

            // ベルの揺れアニメーション再生
            if (bellAnimator != null)
            {
                bellAnimator.SetTrigger("Ring");
            }
        }

        /// <summary>通知バッジの表示状態を更新する</summary>
        private void UpdateNotificationBadge()
        {
            if (unreadBadge != null)
            {
                unreadBadge.SetActive(_unreadNotificationCount > 0);
            }

            if (unreadCountText != null)
            {
                // 100件以上は "99+" と表示
                unreadCountText.text = _unreadNotificationCount > 99
                    ? "99+"
                    : _unreadNotificationCount.ToString();
            }
        }

        // ============================================================
        // 視点切替
        // ============================================================

        /// <summary>視点切替ボタン押下ハンドラ</summary>
        private void OnViewModeToggleClicked()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            // GodView ⇔ ResidentView のトグル
            ViewMode newMode = gm.CurrentViewMode == ViewMode.GodView
                ? ViewMode.ResidentView
                : ViewMode.GodView;

            gm.SwitchViewMode(newMode);
        }

        /// <summary>視点モード変更イベントハンドラ</summary>
        private void HandleViewModeChanged(ViewMode mode)
        {
            UpdateViewModeDisplay(mode);
        }

        /// <summary>視点モード表示を更新する</summary>
        private void UpdateViewModeDisplay(ViewMode mode)
        {
            if (viewModeText != null)
            {
                viewModeText.text = mode switch
                {
                    ViewMode.GodView      => "経営モード",
                    ViewMode.ResidentView => "住人モード",
                    ViewMode.FirstPerson  => "搭乗モード",
                    _                     => "不明"
                };
            }
        }

        // ============================================================
        // 建設モード
        // ============================================================

        /// <summary>建設モードボタン押下ハンドラ</summary>
        private void OnBuildModeClicked()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (gm.CurrentState == GameState.BuildMode)
            {
                gm.ExitBuildMode();
                UpdateBuildModeButtonState(false);
            }
            else
            {
                gm.EnterBuildMode();
                UpdateBuildModeButtonState(true);
            }
        }

        /// <summary>建設モードボタンの表示状態を更新する</summary>
        private void UpdateBuildModeButtonState(bool isActive)
        {
            if (buildModeButtonText != null)
            {
                buildModeButtonText.text = isActive ? "建設モード終了" : "建設モード";
            }
        }
    }
}
