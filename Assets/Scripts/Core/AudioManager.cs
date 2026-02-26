// ============================================================
// ThemeParkGame - AudioManager
// オーディオシステム（BGM・SE・環境音の管理）
// ProceduralAudioLibraryで生成した波形をイベント駆動で再生
// ============================================================

using UnityEngine;

namespace ThemeParkGame.Core
{
    /// <summary>
    /// BGM・SE・環境音を一元管理するオーディオマネージャー。
    /// ProceduralAudioLibraryで生成した波形データを使い、
    /// ゲームイベントに応じて自動的に音を再生する。
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        // ---- AudioSource ----
        private AudioSource bgmSource;
        private AudioSource seSource;
        private AudioSource ambientSource;

        // ---- 音量設定 ----
        private float masterVolume = 1f;
        private float bgmVolume = 0.5f;
        private float seVolume = 0.65f;
        private float ambientVolume = 0.35f;

        // ---- プロシージャル生成クリップ ----
        private AudioClip clipMenuBGM;
        private AudioClip clipGameplayBGM;
        private AudioClip clipGameOverBGM;
        private AudioClip clipFutureCityBGM;
        private AudioClip clipCrowdAmbient;
        private AudioClip clipRainAmbient;
        private AudioClip clipCheer;
        private AudioClip clipAttractionRide;
        private AudioClip clipCash;
        private AudioClip clipClick;
        private AudioClip clipBreakdownAlarm;
        private AudioClip clipAccidentAlarm;
        private AudioClip clipBuildComplete;
        private AudioClip clipGoldenTicket;
        private AudioClip clipVIPArrive;
        private AudioClip clipVomit;
        private AudioClip clipResearchComplete;
        private AudioClip clipWeatherChange;

        // ---- 環境音の状態 ----
        #pragma warning disable CS0414
        private bool isPlayingCrowd;
        #pragma warning restore CS0414
        private bool isPlayingRain;

        // ---- 前回のゲーム状態（BGM切替用） ----
        private GameState lastBGMState = GameState.MainMenu;

        // ---- プロパティ ----

        public float MasterVolume
        {
            get => masterVolume;
            set { masterVolume = Mathf.Clamp01(value); UpdateAllVolumes(); SaveVolumePrefs(); }
        }

        public float BGMVolume
        {
            get => bgmVolume;
            set { bgmVolume = Mathf.Clamp01(value); UpdateAllVolumes(); SaveVolumePrefs(); }
        }

        public float SEVolume
        {
            get => seVolume;
            set { seVolume = Mathf.Clamp01(value); UpdateAllVolumes(); SaveVolumePrefs(); }
        }

        public float AmbientVolume
        {
            get => ambientVolume;
            set { ambientVolume = Mathf.Clamp01(value); UpdateAllVolumes(); SaveVolumePrefs(); }
        }

        // ================================================================
        // Unity ライフサイクル
        // ================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadVolumePrefs();
            SetupAudioSources();
            GenerateAllClips();
            SubscribeToEvents();

            // メニューBGM開始
            PlayBGM(clipMenuBGM);
            WebGLOptimizer.LogVerbose("[AudioManager] Initialized with procedural audio");
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // ゲーム状態に応じたBGM自動切替
            if (GameManager.Instance != null)
            {
                GameState current = GameManager.Instance.CurrentState;
                if (current != lastBGMState)
                {
                    lastBGMState = current;
                    OnGameStateChanged(current);
                }
            }
        }

        // ================================================================
        // AudioSource セットアップ
        // ================================================================

        private void SetupAudioSources()
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
            bgmSource.priority = 0; // 最高優先度

            seSource = gameObject.AddComponent<AudioSource>();
            seSource.playOnAwake = false;
            seSource.priority = 128;

            ambientSource = gameObject.AddComponent<AudioSource>();
            ambientSource.loop = true;
            ambientSource.playOnAwake = false;
            ambientSource.priority = 64;

            UpdateAllVolumes();
        }

        // ================================================================
        // プロシージャルクリップ生成
        // ================================================================

        private void GenerateAllClips()
        {
            // BGM
            clipMenuBGM = ProceduralAudioLibrary.GenerateMenuBGM();
            clipGameplayBGM = ProceduralAudioLibrary.GenerateGameplayBGM();
            clipGameOverBGM = ProceduralAudioLibrary.GenerateGameOverBGM();
            clipFutureCityBGM = ProceduralAudioLibrary.GenerateFutureCityBGM();

            // 環境音
            clipCrowdAmbient = ProceduralAudioLibrary.GenerateCrowdAmbient();
            clipRainAmbient = ProceduralAudioLibrary.GenerateRainAmbient();

            // SE
            clipCheer = ProceduralAudioLibrary.GenerateCheerSE();
            clipAttractionRide = ProceduralAudioLibrary.GenerateAttractionRideSE();
            clipCash = ProceduralAudioLibrary.GenerateCashSE();
            clipClick = ProceduralAudioLibrary.GenerateClickSE();
            clipBreakdownAlarm = ProceduralAudioLibrary.GenerateBreakdownAlarmSE();
            clipAccidentAlarm = ProceduralAudioLibrary.GenerateAccidentAlarmSE();
            clipBuildComplete = ProceduralAudioLibrary.GenerateBuildCompleteSE();
            clipGoldenTicket = ProceduralAudioLibrary.GenerateGoldenTicketSE();
            clipVIPArrive = ProceduralAudioLibrary.GenerateVIPArriveSE();
            clipVomit = ProceduralAudioLibrary.GenerateVomitSE();
            clipResearchComplete = ProceduralAudioLibrary.GenerateResearchCompleteSE();
            clipWeatherChange = ProceduralAudioLibrary.GenerateWeatherChangeSE();

            WebGLOptimizer.LogVerbose("[AudioManager] All procedural audio clips generated");
        }

        // ================================================================
        // 再生API
        // ================================================================

        /// <summary>BGMを再生する（同じクリップなら何もしない）</summary>
        public void PlayBGM(AudioClip clip)
        {
            if (bgmSource == null || clip == null) return;
            if (bgmSource.clip == clip && bgmSource.isPlaying) return;

            bgmSource.clip = clip;
            bgmSource.volume = bgmVolume * masterVolume;
            bgmSource.Play();
        }

        /// <summary>BGMを停止する</summary>
        public void StopBGM()
        {
            if (bgmSource != null) bgmSource.Stop();
        }

        /// <summary>効果音をワンショット再生する</summary>
        public void PlaySE(AudioClip clip)
        {
            if (seSource == null || clip == null) return;
            seSource.PlayOneShot(clip, seVolume * masterVolume);
        }

        /// <summary>UIクリック音を再生する</summary>
        public void PlayClickSE()
        {
            PlaySE(clipClick);
        }

        /// <summary>テーマゾーンに応じたBGMに切り替える</summary>
        public void PlayZoneBGM(ThemeZone zone)
        {
            switch (zone)
            {
                case ThemeZone.FutureCity:
                    PlayBGM(clipFutureCityBGM);
                    break;
                default:
                    PlayBGM(clipGameplayBGM);
                    break;
            }
        }

        /// <summary>環境音を再生する</summary>
        public void PlayAmbient(AudioClip clip)
        {
            if (ambientSource == null || clip == null) return;
            if (ambientSource.clip == clip && ambientSource.isPlaying) return;

            ambientSource.clip = clip;
            ambientSource.volume = ambientVolume * masterVolume;
            ambientSource.Play();
        }

        /// <summary>環境音を停止する</summary>
        public void StopAmbient()
        {
            if (ambientSource != null) ambientSource.Stop();
            isPlayingCrowd = false;
            isPlayingRain = false;
        }

        // ================================================================
        // 音量更新
        // ================================================================

        private void UpdateAllVolumes()
        {
            if (bgmSource != null) bgmSource.volume = bgmVolume * masterVolume;
            if (seSource != null) seSource.volume = seVolume * masterVolume;
            if (ambientSource != null) ambientSource.volume = ambientVolume * masterVolume;
        }

        private void LoadVolumePrefs()
        {
            masterVolume = PlayerPrefs.GetFloat("AUDIO_MASTER", 1f);
            bgmVolume = PlayerPrefs.GetFloat("AUDIO_BGM", 0.5f);
            seVolume = PlayerPrefs.GetFloat("AUDIO_SE", 0.65f);
            ambientVolume = PlayerPrefs.GetFloat("AUDIO_AMBIENT", 0.35f);
        }

        private void SaveVolumePrefs()
        {
            PlayerPrefs.SetFloat("AUDIO_MASTER", masterVolume);
            PlayerPrefs.SetFloat("AUDIO_BGM", bgmVolume);
            PlayerPrefs.SetFloat("AUDIO_SE", seVolume);
            PlayerPrefs.SetFloat("AUDIO_AMBIENT", ambientVolume);
            PlayerPrefs.Save();
        }

        // ================================================================
        // ゲーム状態変化ハンドラ
        // ================================================================

        private void OnGameStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.MainMenu:
                    PlayBGM(clipMenuBGM);
                    StopAmbient();
                    break;

                case GameState.Playing:
                    PlayBGM(clipGameplayBGM);
                    StartCrowdAmbient();
                    break;

                case GameState.Paused:
                    // BGMそのまま、音量少し下げる
                    if (bgmSource != null)
                        bgmSource.volume = bgmVolume * masterVolume * 0.4f;
                    break;

                case GameState.BuildMode:
                    // ゲームプレイBGMを継続（音量少し下げる）
                    if (bgmSource != null)
                        bgmSource.volume = bgmVolume * masterVolume * 0.5f;
                    break;

                case GameState.GameOver:
                    PlayBGM(clipGameOverBGM);
                    StopAmbient();
                    break;
            }
        }

        // ================================================================
        // 環境音制御
        // ================================================================

        private void StartCrowdAmbient()
        {
            if (!isPlayingRain)
            {
                PlayAmbient(clipCrowdAmbient);
                isPlayingCrowd = true;
            }
        }

        private void SwitchToRainAmbient()
        {
            PlayAmbient(clipRainAmbient);
            isPlayingRain = true;
            isPlayingCrowd = false;
        }

        private void SwitchToCrowdAmbient()
        {
            PlayAmbient(clipCrowdAmbient);
            isPlayingCrowd = true;
            isPlayingRain = false;
        }

        // ================================================================
        // イベント購読
        // ================================================================

        private void SubscribeToEvents()
        {
            // パーク
            GameEvents.OnParkOpened += OnParkOpened;
            GameEvents.OnParkClosed += OnParkClosed;

            // 天候
            GameEvents.OnWeatherChanged += OnWeatherChanged;

            // 来場者
            GameEvents.OnVisitorEnterPark += OnVisitorEnterPark;
            GameEvents.OnVisitorVomited += OnVisitorVomited;
            GameEvents.OnVisitorHadAccident += OnVisitorHadAccident;

            // VIP
            GameEvents.OnVIPArrived += OnVIPArrived;

            // アトラクション
            GameEvents.OnAttractionBuilt += OnAttractionBuilt;
            GameEvents.OnAttractionBrokenDown += OnAttractionBrokenDown;
            GameEvents.OnAttractionAccident += OnAttractionAccident;
            GameEvents.OnAttractionRepaired += OnAttractionRepaired;
            GameEvents.OnAttractionUpgraded += OnAttractionUpgraded;

            // 経済
            GameEvents.OnRevenueEarned += OnRevenueEarned;

            // ゴールデンチケット
            GameEvents.OnGoldenTicketEarned += OnGoldenTicketEarned;

            // 認定証
            GameEvents.OnCertificateAwarded += OnCertificateAwarded;

            // 研究
            GameEvents.OnResearchCompleted += OnResearchCompleted;
        }

        private void UnsubscribeFromEvents()
        {
            GameEvents.OnParkOpened -= OnParkOpened;
            GameEvents.OnParkClosed -= OnParkClosed;
            GameEvents.OnWeatherChanged -= OnWeatherChanged;
            GameEvents.OnVisitorEnterPark -= OnVisitorEnterPark;
            GameEvents.OnVisitorVomited -= OnVisitorVomited;
            GameEvents.OnVisitorHadAccident -= OnVisitorHadAccident;
            GameEvents.OnVIPArrived -= OnVIPArrived;
            GameEvents.OnAttractionBuilt -= OnAttractionBuilt;
            GameEvents.OnAttractionBrokenDown -= OnAttractionBrokenDown;
            GameEvents.OnAttractionAccident -= OnAttractionAccident;
            GameEvents.OnAttractionRepaired -= OnAttractionRepaired;
            GameEvents.OnAttractionUpgraded -= OnAttractionUpgraded;
            GameEvents.OnRevenueEarned -= OnRevenueEarned;
            GameEvents.OnGoldenTicketEarned -= OnGoldenTicketEarned;
            GameEvents.OnCertificateAwarded -= OnCertificateAwarded;
            GameEvents.OnResearchCompleted -= OnResearchCompleted;
        }

        // ================================================================
        // イベントハンドラ
        // ================================================================

        private void OnParkOpened()
        {
            PlayBGM(clipGameplayBGM);
            StartCrowdAmbient();
        }

        private void OnParkClosed()
        {
            StopAmbient();
        }

        private void OnWeatherChanged(Weather weather)
        {
            PlaySE(clipWeatherChange);

            // 雨天 → 雨音環境音に切替、それ以外 → 群衆音に戻す
            if (weather == Weather.Rainy || weather == Weather.Snowy)
            {
                SwitchToRainAmbient();
            }
            else if (isPlayingRain)
            {
                SwitchToCrowdAmbient();
            }
        }

        // ---- 来場者の歓声SE（一定確率で歓声を鳴らし連続再生を防ぐ） ----
        private float lastCheerTime;
        private const float CheerCooldown = 5f;

        private void OnVisitorEnterPark(int visitorId)
        {
            // 5人に1人くらいの割合で歓声
            if (Time.time - lastCheerTime > CheerCooldown && visitorId % 5 == 0)
            {
                PlaySE(clipCheer);
                lastCheerTime = Time.time;
            }
        }

        private void OnVisitorVomited(int visitorId)
        {
            PlaySE(clipVomit);
        }

        private void OnVisitorHadAccident(int visitorId)
        {
            // トイレ事故は嘔吐SEを流用（控えめに）
            PlaySE(clipVomit);
        }

        private void OnVIPArrived(int vipId)
        {
            PlaySE(clipVIPArrive);
        }

        private void OnAttractionBuilt(int attractionId)
        {
            PlaySE(clipBuildComplete);
        }

        private void OnAttractionBrokenDown(int attractionId)
        {
            PlaySE(clipBreakdownAlarm);
        }

        private void OnAttractionAccident(int attractionId)
        {
            PlaySE(clipAccidentAlarm);
        }

        private void OnAttractionRepaired(int attractionId)
        {
            PlaySE(clipBuildComplete); // 修理完了音 = 建設完了音を流用
        }

        private void OnAttractionUpgraded(int attractionId)
        {
            PlaySE(clipBuildComplete);
        }

        // ---- 収益SE（チャリン音、連続再生を抑制） ----
        private float lastCashTime;
        private const float CashCooldown = 1.5f;

        private void OnRevenueEarned(float amount)
        {
            if (Time.time - lastCashTime > CashCooldown)
            {
                PlaySE(clipCash);
                lastCashTime = Time.time;
            }
        }

        private void OnGoldenTicketEarned(int count)
        {
            PlaySE(clipGoldenTicket);
        }

        private void OnCertificateAwarded(CertificateCategory category)
        {
            PlaySE(clipGoldenTicket); // 認定証はゴールデンチケットSEを流用
        }

        private void OnResearchCompleted(string researchId)
        {
            PlaySE(clipResearchComplete);
        }

        // ================================================================
        // アトラクション搭乗音（外部から呼び出し可能）
        // ================================================================

        /// <summary>アトラクション搭乗開始時に呼ぶ</summary>
        public void PlayAttractionRideSE()
        {
            PlaySE(clipAttractionRide);
        }

        /// <summary>歓声SEを再生する（アトラクション降車後など）</summary>
        public void PlayCheerSE()
        {
            PlaySE(clipCheer);
        }

        /// <summary>実績解除SEを再生する（ゴールデンチケットSE流用）</summary>
        public void PlayAchievementSE()
        {
            PlaySE(clipGoldenTicket);
        }
    }
}
