// ============================================================
// ThemeParkGame - AudioManager
// オーディオシステム（BGM・SE・環境音の管理）
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace ThemeParkGame.Core
{
    /// <summary>
    /// BGM・SE・環境音を一元管理するオーディオマネージャー。
    /// テーマゾーンに応じたBGM切り替え、天候に応じた環境音、
    /// アトラクション・来場者のSE再生を担当する。
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource seSource;
        [SerializeField] private AudioSource ambientSource;

        [Header("Volume Settings")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float seVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float ambientVolume = 0.5f;

        [Header("BGM Clips")]
        [SerializeField] private AudioClip mainMenuBGM;
        [SerializeField] private AudioClip gameplayBGM;
        [SerializeField] private AudioClip buildModeBGM;

        /// <summary>BGMクリップのキャッシュ</summary>
        private readonly Dictionary<string, AudioClip> _clipCache = new Dictionary<string, AudioClip>();

        public float MasterVolume
        {
            get => masterVolume;
            set
            {
                masterVolume = Mathf.Clamp01(value);
                UpdateAllVolumes();
            }
        }

        public float BGMVolume
        {
            get => bgmVolume;
            set
            {
                bgmVolume = Mathf.Clamp01(value);
                if (bgmSource != null) bgmSource.volume = bgmVolume * masterVolume;
            }
        }

        public float SEVolume
        {
            get => seVolume;
            set
            {
                seVolume = Mathf.Clamp01(value);
                if (seSource != null) seSource.volume = seVolume * masterVolume;
            }
        }

        public float AmbientVolume
        {
            get => ambientVolume;
            set
            {
                ambientVolume = Mathf.Clamp01(value);
                if (ambientSource != null) ambientSource.volume = ambientVolume * masterVolume;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureAudioSources();
            SubscribeToEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        private void EnsureAudioSources()
        {
            if (bgmSource == null)
            {
                bgmSource = gameObject.AddComponent<AudioSource>();
                bgmSource.loop = true;
                bgmSource.playOnAwake = false;
            }
            if (seSource == null)
            {
                seSource = gameObject.AddComponent<AudioSource>();
                seSource.playOnAwake = false;
            }
            if (ambientSource == null)
            {
                ambientSource = gameObject.AddComponent<AudioSource>();
                ambientSource.loop = true;
                ambientSource.playOnAwake = false;
            }

            UpdateAllVolumes();
        }

        private void SubscribeToEvents()
        {
            GameEvents.OnWeatherChanged += OnWeatherChanged;
            GameEvents.OnAttractionBrokenDown += OnAttractionBrokenDown;
            GameEvents.OnAttractionAccident += OnAttractionAccident;
            GameEvents.OnCertificateAwarded += OnCertificateAwarded;
            GameEvents.OnResearchCompleted += OnResearchCompleted;
        }

        private void UnsubscribeFromEvents()
        {
            GameEvents.OnWeatherChanged -= OnWeatherChanged;
            GameEvents.OnAttractionBrokenDown -= OnAttractionBrokenDown;
            GameEvents.OnAttractionAccident -= OnAttractionAccident;
            GameEvents.OnCertificateAwarded -= OnCertificateAwarded;
            GameEvents.OnResearchCompleted -= OnResearchCompleted;
        }

        private void UpdateAllVolumes()
        {
            if (bgmSource != null) bgmSource.volume = bgmVolume * masterVolume;
            if (seSource != null) seSource.volume = seVolume * masterVolume;
            if (ambientSource != null) ambientSource.volume = ambientVolume * masterVolume;
        }

        /// <summary>BGMを再生する</summary>
        public void PlayBGM(AudioClip clip, float fadeTime = 1f)
        {
            if (bgmSource == null || clip == null) return;
            if (bgmSource.clip == clip && bgmSource.isPlaying) return;

            bgmSource.clip = clip;
            bgmSource.Play();
        }

        /// <summary>BGMを停止する</summary>
        public void StopBGM()
        {
            if (bgmSource != null) bgmSource.Stop();
        }

        /// <summary>効果音を再生する</summary>
        public void PlaySE(AudioClip clip)
        {
            if (seSource == null || clip == null) return;
            seSource.PlayOneShot(clip, seVolume * masterVolume);
        }

        /// <summary>指定位置に3D効果音を再生する</summary>
        public void PlaySEAtPosition(AudioClip clip, Vector3 position)
        {
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, position, seVolume * masterVolume);
        }

        /// <summary>環境音を再生する</summary>
        public void PlayAmbient(AudioClip clip)
        {
            if (ambientSource == null || clip == null) return;
            ambientSource.clip = clip;
            ambientSource.Play();
        }

        /// <summary>リソースフォルダからオーディオクリップをロードして再生する</summary>
        public void PlaySEByName(string clipName)
        {
            if (!_clipCache.TryGetValue(clipName, out AudioClip clip))
            {
                clip = Resources.Load<AudioClip>($"Audio/SE/{clipName}");
                if (clip != null) _clipCache[clipName] = clip;
            }
            if (clip != null) PlaySE(clip);
        }

        /// <summary>ゲーム状態に応じたBGMを再生する</summary>
        public void PlayBGMForGameState(GameState state)
        {
            switch (state)
            {
                case GameState.MainMenu:
                    PlayBGM(mainMenuBGM);
                    break;
                case GameState.Playing:
                    PlayBGM(gameplayBGM);
                    break;
                case GameState.BuildMode:
                    PlayBGM(buildModeBGM);
                    break;
            }
        }

        // イベントハンドラ

        private void OnWeatherChanged(Weather weather)
        {
            PlaySEByName($"weather_{weather.ToString().ToLower()}");
        }

        private void OnAttractionBrokenDown(int id)
        {
            PlaySEByName("attraction_breakdown");
        }

        private void OnAttractionAccident(int id)
        {
            PlaySEByName("attraction_accident");
        }

        private void OnCertificateAwarded(CertificateCategory category)
        {
            PlaySEByName("certificate_awarded");
        }

        private void OnResearchCompleted(string researchId)
        {
            PlaySEByName("research_complete");
        }
    }
}
