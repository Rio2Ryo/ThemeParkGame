// ============================================================
// RuntimeHUD - ParkInfo partial
// 混雑度、イベント、パーク評価、ゾーン、SNS、アラート、お祝いエフェクト
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using ThemeParkGame.AI;
using ThemeParkGame.Park;

namespace ThemeParkGame.Core
{
    public partial class RuntimeHUD
    {
        // ---- 通路混雑度表示 ----
        private Text _congestionText;
        private Image _congestionBarFill;

        private PathwaySystem _cachedPathwaySystem;
        private float _pathwayCacheTimer;

        // ---- イベント表示 ----
        private Text _eventText;
        private GameObject _eventPanel;
        private float _eventBlinkTimer;

        // ---- パーク評価表示 ----
        private Text _ratingStarsText;
        private Text _ratingScoreText;
        private Text _ratingLabelText;
        private Text _ratingTrendText;
        private Text[] _ratingCatTexts;
        private Image[] _ratingCatBars;

        // ---- ゾーンパネル ----
        private GameObject _zoneBar;
        private readonly Button[] _zoneButtons = new Button[4];
        private readonly Text[] _zoneLabels = new Text[4];

        // ---- SNSフィードパネル ----
        private GameObject _snsPanel;
        private Text _snsReputationText;
        private Text _snsReputationBar;
        private Text _snsSpawnText;
        private Text[] _snsTrendTexts;
        private Text[] _snsFeedTexts;
        private Text[] _snsFeedSentiments;
        private Button _snsToggleBtn;
        private bool _snsPanelExpanded;

        private Image _snsRepFillImg;
        private float _snsRepBarWidth;

        // ---- アラートバー ----
        private GameObject _alertBar;
        private Image _alertBarBg;
        private Image _alertBarAccent;
        private Text _alertBarIcon;
        private Text _alertBarText;
        private Text _alertBarCount;
        private float _alertBarBlinkTimer;

        // ---- お祝いエフェクト ----
        private ParticleSystem _fireworksPS;
        private GameObject _screenFlash;
        private Image _screenFlashImage;
        private float _screenFlashTimer;
        private const float ScreenFlashDuration = 0.6f;

        // ================================================================
        // 通路混雑度表示
        // ================================================================

        private void UpdateCongestionDisplay()
        {
            if (_congestionText == null) return;

            // PathwaySystemのキャッシュ（毎フレーム FindObjectOfType は重いので3秒間隔）
            _pathwayCacheTimer -= Time.deltaTime;
            if (_pathwayCacheTimer <= 0f || _cachedPathwaySystem == null)
            {
                _cachedPathwaySystem = FindObjectOfType<PathwaySystem>();
                _pathwayCacheTimer = 3f;
            }

            if (_cachedPathwaySystem == null)
            {
                _congestionText.text = "通路: ---";
                return;
            }

            float avg = _cachedPathwaySystem.AverageCongestion;
            string label = PathwaySystem.GetCongestionLabel(avg);
            Color displayColor = PathwaySystem.GetCongestionDisplayColor(avg);

            _congestionText.text = $"通路: {label}";
            _congestionText.color = displayColor;

            // バーの更新
            if (_congestionBarFill != null)
            {
                var fillRt = _congestionBarFill.rectTransform;
                fillRt.sizeDelta = new Vector2(60f * avg, 0f);
                _congestionBarFill.color = displayColor;
            }
        }

        // ================================================================
        // イベント表示
        // ================================================================

        private void BuildEventPanel(RectTransform root)
        {
            float panelW = 400f;
            float panelH = 24f;

            _eventPanel = MakePanel(root, "EventPanel", panelW, panelH,
                new Color(0.12f, 0.08f, 0.22f, 0.88f));
            var rt = _eventPanel.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(10f, -122f);

            _eventText = MakeLabel(rt, "EventText", "", 12,
                new Color(1f, 0.85f, 0.4f), FontStyle.Bold, TextAnchor.MiddleLeft);
            PlaceInParent(_eventText.rectTransform, 8f, panelH, panelW - 16f, panelH, new Vector2(0f, 1f));

            _eventPanel.SetActive(false);
        }

        private void UpdateEventPanel()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.ParkEventSystem == null)
            {
                if (_eventPanel != null) _eventPanel.SetActive(false);
                return;
            }

            var eventSys = gm.ParkEventSystem;
            if (eventSys.ActiveEventCount == 0)
            {
                if (_eventPanel != null) _eventPanel.SetActive(false);
                return;
            }

            _eventPanel.SetActive(true);
            _eventText.text = eventSys.GetActiveEventsSummary();

            // ショー開催中は点滅エフェクト
            _eventBlinkTimer += Time.deltaTime;
            bool hasShow = false;
            foreach (var ae in eventSys.CurrentEvents)
            {
                if (ae.Data.Type == ParkEventType.SpecialShow)
                {
                    hasShow = true;
                    break;
                }
            }

            if (hasShow)
            {
                float blink = (Mathf.Sin(_eventBlinkTimer * 4f) + 1f) * 0.5f;
                _eventText.color = Color.Lerp(
                    new Color(1f, 0.85f, 0.4f),
                    new Color(1f, 0.5f, 0.2f),
                    blink);
            }
            else
            {
                _eventText.color = new Color(1f, 0.85f, 0.4f);
            }
        }

        // ================================================================
        // パーク評価パネル
        // ================================================================

        private void BuildRatingPanel(RectTransform root)
        {
            float panelW = 240f;
            float panelH = 170f;

            var bg = MakePanel(root, "RatingPanel", panelW, panelH, BgDark);
            var rt = bg.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-10f, -52f);

            // ヘッダー
            var header = MakeLabel(rt, "RHeader", "パーク評価", 14, Cyan, FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceInParent(header.rectTransform, 0f, panelH - 2f, panelW, 18f, new Vector2(0f, 1f));

            // 星テキスト（大きめ）
            _ratingStarsText = MakeLabel(rt, "Stars", "-----", 22, new Color(1f, 0.85f, 0.3f),
                FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceInParent(_ratingStarsText.rectTransform, 0f, panelH - 22f, panelW, 24f, new Vector2(0f, 1f));

            // スコア + ラベル
            _ratingScoreText = MakeLabel(rt, "Score", "0.0", 13, Color.white,
                FontStyle.Normal, TextAnchor.MiddleLeft);
            PlaceInParent(_ratingScoreText.rectTransform, 8f, panelH - 48f, 80f, 16f, new Vector2(0f, 1f));

            _ratingLabelText = MakeLabel(rt, "Label", "", 13, Muted,
                FontStyle.Normal, TextAnchor.MiddleCenter);
            PlaceInParent(_ratingLabelText.rectTransform, 85f, panelH - 48f, 100f, 16f, new Vector2(0f, 1f));

            _ratingTrendText = MakeLabel(rt, "Trend", "-", 13, Muted,
                FontStyle.Bold, TextAnchor.MiddleRight);
            PlaceInParent(_ratingTrendText.rectTransform, panelW - 40f, panelH - 48f, 30f, 16f, new Vector2(0f, 1f));

            // カテゴリバー（5行）
            var categories = new[] {
                CertificateCategory.Fame, CertificateCategory.Safety,
                CertificateCategory.Comfort, CertificateCategory.Excitement,
                CertificateCategory.Mood
            };
            _ratingCatTexts = new Text[5];
            _ratingCatBars = new Image[5];

            float barMaxW = 100f;
            float barH = 8f;
            float lineH = 18f;
            float startY = panelH - 68f;

            for (int i = 0; i < 5; i++)
            {
                float y = startY - i * lineH;
                string catName = ParkRatingEvaluator.GetCategoryLabel(categories[i]);

                _ratingCatTexts[i] = MakeLabel(rt, $"Cat{i}", $"{catName}: 0", 11, Muted,
                    FontStyle.Normal, TextAnchor.MiddleLeft);
                PlaceInParent(_ratingCatTexts[i].rectTransform, 8f, y, 90f, lineH, new Vector2(0f, 1f));

                // バー背景
                var barBg = MakePanel(rt, $"CatBarBg{i}", barMaxW, barH, new Color(0.15f, 0.15f, 0.2f));
                var barBgRt = barBg.GetComponent<RectTransform>();
                barBgRt.anchorMin = barBgRt.anchorMax = new Vector2(0f, 0f);
                barBgRt.pivot = new Vector2(0f, 0.5f);
                barBgRt.anchoredPosition = new Vector2(panelW - barMaxW - 10f, y - lineH * 0.5f);

                // バーフィル
                var fill = MakePanel(barBgRt, $"CatBarFill{i}", 0f, barH, GetCategoryColor(i));
                var fillRt = fill.GetComponent<RectTransform>();
                fillRt.anchorMin = new Vector2(0f, 0f);
                fillRt.anchorMax = new Vector2(0f, 1f);
                fillRt.pivot = new Vector2(0f, 0.5f);
                fillRt.anchoredPosition = Vector2.zero;
                fillRt.sizeDelta = new Vector2(0f, 0f);
                _ratingCatBars[i] = fill.GetComponent<Image>();
            }
        }

        private static Color GetCategoryColor(int idx)
        {
            switch (idx)
            {
                case 0: return new Color(0.9f, 0.7f, 0.2f);  // Fame: 金
                case 1: return new Color(0.3f, 0.8f, 0.4f);  // Safety: 緑
                case 2: return new Color(0.4f, 0.7f, 0.9f);  // Comfort: 水色
                case 3: return new Color(0.9f, 0.4f, 0.3f);  // Excitement: 赤
                case 4: return new Color(0.8f, 0.5f, 0.9f);  // Mood: 紫
                default: return Color.white;
            }
        }

        private void UpdateRatingPanel()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.ParkManager == null || gm.ParkManager.Rating == null) return;

            var evaluator = gm.ParkRatingEvaluator;
            var rating = gm.ParkManager.Rating;
            float overall = rating.OverallRating;

            // 星表示
            if (_ratingStarsText != null)
            {
                float stars = evaluator != null ? evaluator.StarRating : ParkRatingEvaluator.ScoreToStars(overall);
                _ratingStarsText.text = ParkRatingEvaluator.StarsToText(stars);
            }

            // スコア
            if (_ratingScoreText != null)
                _ratingScoreText.text = $"{overall:F1}";

            // ラベル
            if (_ratingLabelText != null)
                _ratingLabelText.text = ParkRatingEvaluator.GetRatingLabel(overall);

            // トレンド
            if (_ratingTrendText != null && evaluator != null)
            {
                string arrow = ParkRatingEvaluator.GetTrendArrow(evaluator.Trend);
                _ratingTrendText.text = arrow;
                _ratingTrendText.color = evaluator.Trend > 0.5f ? new Color(0.4f, 0.95f, 0.5f)
                    : evaluator.Trend < -0.5f ? new Color(0.95f, 0.4f, 0.4f) : Muted;
            }

            // カテゴリバー
            var categories = new[] {
                CertificateCategory.Fame, CertificateCategory.Safety,
                CertificateCategory.Comfort, CertificateCategory.Excitement,
                CertificateCategory.Mood
            };

            float barMaxW = 100f;
            for (int i = 0; i < 5; i++)
            {
                float score = rating.GetCategoryScore(categories[i]);
                bool cert = rating.IsCertificateAwarded(categories[i]);
                string certMark = cert ? " [C]" : "";
                string catName = ParkRatingEvaluator.GetCategoryLabel(categories[i]);

                if (_ratingCatTexts[i] != null)
                {
                    _ratingCatTexts[i].text = $"{catName}: {score:F0}{certMark}";
                    _ratingCatTexts[i].color = cert ? new Color(0.95f, 0.88f, 0.45f) : Muted;
                }

                if (_ratingCatBars[i] != null)
                {
                    var barRt = _ratingCatBars[i].rectTransform;
                    barRt.sizeDelta = new Vector2(barMaxW * Mathf.Clamp01(score / 100f), 0f);
                }
            }
        }

        // ================================================================
        // ゾーンバー
        // ================================================================

        private void BuildZoneBar(RectTransform root)
        {
            _zoneBar = new GameObject("ZoneBar");
            _zoneBar.transform.SetParent(root, false);
            var rt = _zoneBar.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(10f, -52f);
            rt.sizeDelta = new Vector2(490f, 28f);

            var bgImg = _zoneBar.AddComponent<Image>();
            bgImg.color = new Color(0.05f, 0.07f, 0.12f, 0.85f);
            bgImg.raycastTarget = false;

            string[] zoneNames = { LocalizationData.ZoneLostKingdom, LocalizationData.ZoneHalloweenWorld, LocalizationData.ZoneWonderland, LocalizationData.ZoneSpaceZone };
            ThemeZone[] zones = { ThemeZone.LostKingdom, ThemeZone.HalloweenWorld,
                                  ThemeZone.Wonderland, ThemeZone.SpaceZone };

            for (int i = 0; i < 4; i++)
            {
                float x = 4f + i * 122f;
                var btnGo = MakePanel(rt, $"Zone{i}", 118f, 22f, new Color(0.2f, 0.2f, 0.2f, 0.8f));
                var btnRt = btnGo.GetComponent<RectTransform>();
                btnRt.anchorMin = btnRt.anchorMax = new Vector2(0f, 0.5f);
                btnRt.pivot = new Vector2(0f, 0.5f);
                btnRt.anchoredPosition = new Vector2(x, 0f);

                var btnImg = btnGo.GetComponent<Image>();
                btnImg.raycastTarget = true;
                _zoneButtons[i] = btnGo.AddComponent<Button>();
                _zoneButtons[i].targetGraphic = btnImg;

                int idx = i;
                ThemeZone zoneCapture = zones[i];
                _zoneButtons[i].onClick.AddListener(() => OnZoneButtonClicked(zoneCapture));

                _zoneLabels[i] = MakeLabel(btnRt, "L", zoneNames[i], 11,
                    Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
                StretchFill(_zoneLabels[i].rectTransform);
            }
        }

        private void OnZoneButtonClicked(ThemeZone zone)
        {
            var pm = GameManager.Instance?.ParkManager;
            if (pm == null) return;

            if (pm.IsZoneUnlocked(zone))
            {
                NotificationSystem.Instance?.Notify($"{ZoneNameJa(zone)} は既にアンロック済みです", NotifLevel.Info);
                return;
            }

            int cost = pm.GetZoneUnlockCost(zone);
            int tickets = GameManager.Instance.GoldenTickets;
            if (tickets < cost)
            {
                NotificationSystem.Instance?.Notify(
                    $"{ZoneNameJa(zone)} のアンロックにはゴールデンチケット{cost}枚が必要です (現在: {tickets}枚)",
                    NotifLevel.Warning);
                return;
            }

            if (pm.UnlockZone(zone))
            {
                NotificationSystem.Instance?.Notify(
                    $"{ZoneNameJa(zone)} をアンロックしました！", NotifLevel.Success);
            }
        }

        private void UpdateZoneBar()
        {
            if (_zoneBar == null) return;
            var pm = GameManager.Instance?.ParkManager;
            if (pm == null) return;

            ThemeZone[] zones = { ThemeZone.LostKingdom, ThemeZone.HalloweenWorld,
                                  ThemeZone.Wonderland, ThemeZone.SpaceZone };
            Color unlocked = new Color(0.15f, 0.45f, 0.25f, 0.9f);
            Color locked = new Color(0.3f, 0.2f, 0.15f, 0.85f);

            for (int i = 0; i < 4; i++)
            {
                bool isUnlocked = pm.IsZoneUnlocked(zones[i]);
                var img = _zoneButtons[i].GetComponent<Image>();
                img.color = isUnlocked ? unlocked : locked;

                if (!isUnlocked)
                {
                    int cost = pm.GetZoneUnlockCost(zones[i]);
                    _zoneLabels[i].text = $"[未開放] x{cost}";
                    _zoneLabels[i].color = new Color(0.7f, 0.5f, 0.3f);
                }
                else
                {
                    string[] shortNames = { LocalizationData.ZoneLostKingdom, LocalizationData.ZoneHalloweenWorld, LocalizationData.ZoneWonderland, LocalizationData.ZoneSpaceZone };
                    _zoneLabels[i].text = shortNames[i];
                    _zoneLabels[i].color = Color.white;
                }
            }
        }

        private static string ZoneNameJa(ThemeZone zone)
        {
            switch (zone)
            {
                case ThemeZone.LostKingdom: return "ロストキングダム";
                case ThemeZone.HalloweenWorld: return "ハロウィーンワールド";
                case ThemeZone.Wonderland: return "ワンダーランド";
                case ThemeZone.SpaceZone: return "スペースゾーン";
                default: return zone.ToString();
            }
        }

        // ================================================================
        // SNSフィードパネル（画面左下）
        // ================================================================

        private void BuildSNSPanel(RectTransform root)
        {
            float panelW = 300f;
            float panelH = 260f;

            var bg = MakePanel(root, "SNSPanel", panelW, panelH, BgDark);
            _snsPanel = bg;
            var rt = bg.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(10f, 50f);

            // ヘッダー（クリックで展開/折りたたみ）
            var headerBg = MakePanel(rt, "SNSHeader", panelW, 24f, new Color(0.12f, 0.14f, 0.22f));
            var headerRt = headerBg.GetComponent<RectTransform>();
            headerRt.anchorMin = headerRt.anchorMax = new Vector2(0f, 1f);
            headerRt.pivot = new Vector2(0f, 1f);
            headerRt.anchoredPosition = Vector2.zero;
            var headerImg = headerBg.GetComponent<Image>();
            headerImg.raycastTarget = true;
            _snsToggleBtn = headerBg.AddComponent<Button>();
            _snsToggleBtn.targetGraphic = headerImg;
            var hbc = _snsToggleBtn.colors;
            hbc.highlightedColor = new Color(0.18f, 0.2f, 0.3f);
            hbc.pressedColor = new Color(0.08f, 0.1f, 0.18f);
            _snsToggleBtn.colors = hbc;
            _snsToggleBtn.onClick.AddListener(() => { _snsPanelExpanded = !_snsPanelExpanded; });

            var headerLabel = MakeLabel(headerRt, "Title", "SNS フィード", 13,
                new Color(0.5f, 0.8f, 1f), FontStyle.Bold, TextAnchor.MiddleLeft);
            var hlRt = headerLabel.rectTransform;
            hlRt.anchorMin = hlRt.anchorMax = new Vector2(0f, 0.5f);
            hlRt.pivot = new Vector2(0f, 0.5f);
            hlRt.anchoredPosition = new Vector2(8f, 0f);
            hlRt.sizeDelta = new Vector2(120f, 20f);

            // レピュテーションメーター（ヘッダー右側）
            _snsReputationText = MakeLabel(headerRt, "Rep", "評判: 50", 12,
                Color.white, FontStyle.Bold, TextAnchor.MiddleRight);
            var repRt = _snsReputationText.rectTransform;
            repRt.anchorMin = repRt.anchorMax = new Vector2(1f, 0.5f);
            repRt.pivot = new Vector2(1f, 0.5f);
            repRt.anchoredPosition = new Vector2(-8f, 0f);
            repRt.sizeDelta = new Vector2(100f, 20f);

            // レピュテーションバー
            float barY = panelH - 30f;
            var barBg = MakePanel(rt, "RepBarBg", panelW - 16f, 6f, new Color(0.15f, 0.15f, 0.2f));
            var barBgRt = barBg.GetComponent<RectTransform>();
            barBgRt.anchorMin = barBgRt.anchorMax = new Vector2(0f, 1f);
            barBgRt.pivot = new Vector2(0f, 1f);
            barBgRt.anchoredPosition = new Vector2(8f, -26f);

            _snsReputationBar = MakeLabel(rt, "RepBarFill", "", 1, Color.clear,
                FontStyle.Normal, TextAnchor.MiddleLeft);
            // Use image instead
            var repFillGo = MakePanel(barBgRt, "Fill", 0f, 6f, new Color(0.3f, 0.7f, 1f));
            var repFillRt = repFillGo.GetComponent<RectTransform>();
            repFillRt.anchorMin = new Vector2(0f, 0f);
            repFillRt.anchorMax = new Vector2(0f, 1f);
            repFillRt.pivot = new Vector2(0f, 0.5f);
            repFillRt.anchoredPosition = Vector2.zero;
            // Store image ref via tag on text
            _snsReputationBar.text = "repfill";
            // We'll update width directly; store ref differently
            // Actually, let's use the Image component
            Destroy(_snsReputationBar.gameObject);
            _snsReputationBar = null;
            // Replace with a proper approach
            var repFillImg = repFillGo.GetComponent<Image>();

            // Spawn multiplier text
            _snsSpawnText = MakeLabel(rt, "SpawnMul", "", 11,
                Muted, FontStyle.Normal, TextAnchor.MiddleLeft);
            var spRt = _snsSpawnText.rectTransform;
            spRt.anchorMin = spRt.anchorMax = new Vector2(0f, 1f);
            spRt.pivot = new Vector2(0f, 1f);
            spRt.anchoredPosition = new Vector2(8f, -36f);
            spRt.sizeDelta = new Vector2(panelW - 16f, 14f);

            // トレンドトピック（3行）
            var trendHeader = MakeLabel(rt, "TrendH", LocalizationData.SNSTrending, 11,
                new Color(0.9f, 0.78f, 0.25f), FontStyle.Bold, TextAnchor.MiddleLeft);
            var thRt = trendHeader.rectTransform;
            thRt.anchorMin = thRt.anchorMax = new Vector2(0f, 1f);
            thRt.pivot = new Vector2(0f, 1f);
            thRt.anchoredPosition = new Vector2(8f, -54f);
            thRt.sizeDelta = new Vector2(panelW - 16f, 14f);

            _snsTrendTexts = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                _snsTrendTexts[i] = MakeLabel(rt, $"Trend{i}", "", 11,
                    Color.white, FontStyle.Normal, TextAnchor.MiddleLeft);
                var tRt = _snsTrendTexts[i].rectTransform;
                tRt.anchorMin = tRt.anchorMax = new Vector2(0f, 1f);
                tRt.pivot = new Vector2(0f, 1f);
                tRt.anchoredPosition = new Vector2(12f, -70f - i * 15f);
                tRt.sizeDelta = new Vector2(panelW - 24f, 14f);
            }

            // フィード（最新5件）
            var feedHeader = MakeLabel(rt, "FeedH", "最近の投稿", 11,
                new Color(0.5f, 0.8f, 1f), FontStyle.Bold, TextAnchor.MiddleLeft);
            var fhRt = feedHeader.rectTransform;
            fhRt.anchorMin = fhRt.anchorMax = new Vector2(0f, 1f);
            fhRt.pivot = new Vector2(0f, 1f);
            fhRt.anchoredPosition = new Vector2(8f, -118f);
            fhRt.sizeDelta = new Vector2(panelW - 16f, 14f);

            _snsFeedTexts = new Text[5];
            _snsFeedSentiments = new Text[5];
            for (int i = 0; i < 5; i++)
            {
                float fy = -134f - i * 24f;

                // センチメント表示
                _snsFeedSentiments[i] = MakeLabel(rt, $"FSent{i}", "", 11,
                    Green, FontStyle.Bold, TextAnchor.MiddleCenter);
                var sRt = _snsFeedSentiments[i].rectTransform;
                sRt.anchorMin = sRt.anchorMax = new Vector2(0f, 1f);
                sRt.pivot = new Vector2(0f, 1f);
                sRt.anchoredPosition = new Vector2(8f, fy);
                sRt.sizeDelta = new Vector2(14f, 22f);

                // 投稿テキスト
                _snsFeedTexts[i] = MakeLabel(rt, $"FPost{i}", "", 11,
                    Color.white, FontStyle.Normal, TextAnchor.MiddleLeft);
                _snsFeedTexts[i].horizontalOverflow = HorizontalWrapMode.Wrap;
                var fRt = _snsFeedTexts[i].rectTransform;
                fRt.anchorMin = fRt.anchorMax = new Vector2(0f, 1f);
                fRt.pivot = new Vector2(0f, 1f);
                fRt.anchoredPosition = new Vector2(24f, fy);
                fRt.sizeDelta = new Vector2(panelW - 36f, 22f);
            }

            // Store repFillImg reference via a trick: use _snsReputationBar as carrier
            // Instead, store as a field
            _snsRepFillImg = repFillImg;
            _snsRepBarWidth = panelW - 16f;

            _snsPanelExpanded = true;
        }

        private void UpdateSNSPanel()
        {
            if (_snsPanel == null) return;

            var gm = GameManager.Instance;
            if (gm == null) { _snsPanel.SetActive(false); return; }

            var snsSystem = gm.AIManager?.SNSSystem;
            if (snsSystem == null) { _snsPanel.SetActive(false); return; }

            _snsPanel.SetActive(true);

            // 折りたたみ時はヘッダーだけ表示
            var panelRt = _snsPanel.GetComponent<RectTransform>();
            if (_snsPanelExpanded)
                panelRt.sizeDelta = new Vector2(300f, 260f);
            else
                panelRt.sizeDelta = new Vector2(300f, 24f);

            // レピュテーション
            float rep = snsSystem.Reputation;
            Color repColor = rep >= 70f ? Green : rep >= 40f ? Yellow : Red;
            _snsReputationText.text = $"評判: {rep:F0}";
            _snsReputationText.color = repColor;

            // バー
            if (_snsRepFillImg != null)
            {
                float ratio = Mathf.Clamp01(rep / 100f);
                _snsRepFillImg.rectTransform.sizeDelta = new Vector2(_snsRepBarWidth * ratio, 0f);
                _snsRepFillImg.color = repColor;
            }

            if (!_snsPanelExpanded) return;

            // スポーン倍率
            float spawnMul = snsSystem.VisitorSpawnMultiplier;
            _snsSpawnText.text = $"来場倍率: x{spawnMul:F2}  投稿数: {snsSystem.Feed.Count}";

            // トレンドトピック
            var trends = snsSystem.TrendingTopics;
            for (int i = 0; i < 3; i++)
            {
                if (i < trends.Count)
                {
                    var t = trends[i];
                    string sentIcon = t.OverallSentiment == PostSentiment.Positive ? "+" :
                                      t.OverallSentiment == PostSentiment.Negative ? "-" : "=";
                    Color tColor = t.OverallSentiment == PostSentiment.Positive ? Green :
                                   t.OverallSentiment == PostSentiment.Negative ? Red : Muted;
                    _snsTrendTexts[i].text = $"#{i + 1} {t.TopicName} ({t.MentionCount}件) [{sentIcon}]";
                    _snsTrendTexts[i].color = tColor;
                }
                else
                {
                    _snsTrendTexts[i].text = "";
                }
            }

            // フィード
            var feed = snsSystem.Feed;
            for (int i = 0; i < 5; i++)
            {
                if (i < feed.Count)
                {
                    var post = feed[i];

                    // テキスト（30文字でカット）
                    string content = post.Content;
                    if (content.Length > 30) content = content.Substring(0, 30) + "...";
                    _snsFeedTexts[i].text = $"{post.AuthorName}: {content}";

                    // センチメント
                    switch (post.Sentiment)
                    {
                        case PostSentiment.Positive:
                            _snsFeedSentiments[i].text = "+";
                            _snsFeedSentiments[i].color = Green;
                            break;
                        case PostSentiment.Negative:
                            _snsFeedSentiments[i].text = "-";
                            _snsFeedSentiments[i].color = Red;
                            break;
                        default:
                            _snsFeedSentiments[i].text = "=";
                            _snsFeedSentiments[i].color = Muted;
                            break;
                    }
                }
                else
                {
                    _snsFeedTexts[i].text = "";
                    _snsFeedSentiments[i].text = "";
                }
            }
        }

        // ================================================================
        // アラートバー（画面下部中央）
        // ================================================================

        private void BuildAlertBar(RectTransform root)
        {
            float barW = 600f;
            float barH = 36f;

            _alertBar = new GameObject("AlertBar");
            _alertBar.transform.SetParent(root, false);
            var rt = _alertBar.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 8f);
            rt.sizeDelta = new Vector2(barW, barH);

            _alertBarBg = _alertBar.AddComponent<Image>();
            _alertBarBg.color = new Color(0.08f, 0.06f, 0.14f, 0.92f);
            _alertBarBg.raycastTarget = true;

            // クリックでログパネルを開く
            var btn = _alertBar.AddComponent<Button>();
            btn.targetGraphic = _alertBarBg;
            var bc = btn.colors;
            bc.highlightedColor = new Color(0.14f, 0.12f, 0.22f, 0.95f);
            bc.pressedColor = new Color(0.06f, 0.04f, 0.10f, 0.95f);
            btn.colors = bc;
            btn.onClick.AddListener(OnEventLogClicked);

            // 左端アクセントライン
            var accentGo = MakePanel(rt, "Accent", 4f, barH, new Color(0.9f, 0.78f, 0.25f));
            _alertBarAccent = accentGo.GetComponent<Image>();
            var accentRt = accentGo.GetComponent<RectTransform>();
            accentRt.anchorMin = accentRt.anchorMax = new Vector2(0f, 0.5f);
            accentRt.pivot = new Vector2(0f, 0.5f);
            accentRt.anchoredPosition = Vector2.zero;

            // アイコン
            _alertBarIcon = MakeLabel(rt, "Icon", "[!]", 16,
                new Color(0.9f, 0.78f, 0.25f), FontStyle.Bold, TextAnchor.MiddleCenter);
            var iRt = _alertBarIcon.rectTransform;
            iRt.anchorMin = iRt.anchorMax = new Vector2(0f, 0.5f);
            iRt.pivot = new Vector2(0f, 0.5f);
            iRt.anchoredPosition = new Vector2(12f, 0f);
            iRt.sizeDelta = new Vector2(28f, 28f);

            // メッセージ
            _alertBarText = MakeLabel(rt, "Msg", "", 14,
                Color.white, FontStyle.Normal, TextAnchor.MiddleLeft);
            _alertBarText.horizontalOverflow = HorizontalWrapMode.Wrap;
            var mRt = _alertBarText.rectTransform;
            mRt.anchorMin = mRt.anchorMax = new Vector2(0f, 0.5f);
            mRt.pivot = new Vector2(0f, 0.5f);
            mRt.anchoredPosition = new Vector2(42f, 0f);
            mRt.sizeDelta = new Vector2(barW - 120f, 28f);

            // カウント
            _alertBarCount = MakeLabel(rt, "Count", "", 13,
                new Color(0.7f, 0.72f, 0.8f), FontStyle.Bold, TextAnchor.MiddleRight);
            var cRt = _alertBarCount.rectTransform;
            cRt.anchorMin = cRt.anchorMax = new Vector2(1f, 0.5f);
            cRt.pivot = new Vector2(1f, 0.5f);
            cRt.anchoredPosition = new Vector2(-10f, 0f);
            cRt.sizeDelta = new Vector2(60f, 28f);

            _alertBar.SetActive(false);
        }

        private void UpdateAlertBar()
        {
            if (_alertBar == null) return;

            var monitor = AlertMonitor.Instance;
            if (monitor == null || monitor.TotalAlertCount == 0)
            {
                _alertBar.SetActive(false);
                return;
            }

            _alertBar.SetActive(true);

            string summary = monitor.GetSummaryText();
            _alertBarText.text = summary;

            int total = monitor.TotalAlertCount;
            int danger = monitor.DangerCount;
            _alertBarCount.text = total > 1 ? $"{total}件" : "";

            // レベルに応じた色
            NotifLevel level = monitor.HighestLevel;
            Color accentColor;
            switch (level)
            {
                case NotifLevel.Danger:
                    accentColor = new Color(0.9f, 0.3f, 0.25f);
                    break;
                case NotifLevel.Warning:
                    accentColor = new Color(0.9f, 0.78f, 0.25f);
                    break;
                default:
                    accentColor = new Color(0.3f, 0.6f, 0.85f);
                    break;
            }

            _alertBarAccent.color = accentColor;
            _alertBarIcon.color = accentColor;

            // Dangerレベルのとき点滅
            if (level == NotifLevel.Danger)
            {
                _alertBarBlinkTimer += Time.unscaledDeltaTime * 3f;
                float blink = (Mathf.Sin(_alertBarBlinkTimer) + 1f) * 0.5f;
                _alertBarBg.color = Color.Lerp(
                    new Color(0.08f, 0.06f, 0.14f, 0.92f),
                    new Color(0.25f, 0.06f, 0.06f, 0.95f),
                    blink * 0.5f);
                _alertBarIcon.text = blink > 0.5f ? "[X]" : "[!]";
            }
            else
            {
                _alertBarBlinkTimer = 0f;
                _alertBarBg.color = new Color(0.08f, 0.06f, 0.14f, 0.92f);
                _alertBarIcon.text = "[!]";
            }
        }

        // ================================================================
        // お祝いエフェクト（ゴールデンチケット・認定証取得時）
        // ================================================================

        private void OnGoldenTicketCelebration(int count)
        {
            TriggerScreenFlash(new Color(1f, 0.85f, 0.2f, 0.5f));
            SpawnFireworks(new Color(1f, 0.9f, 0.3f), new Color(1f, 0.6f, 0.1f));
        }

        private void OnCertificateCelebration(CertificateCategory category)
        {
            TriggerScreenFlash(new Color(0.3f, 0.7f, 1f, 0.4f));
            SpawnFireworks(new Color(0.4f, 0.8f, 1f), new Color(0.2f, 0.5f, 1f));
        }

        private void TriggerScreenFlash(Color flashColor)
        {
            if (_screenFlash == null && _canvasRoot != null)
            {
                _screenFlash = new GameObject("ScreenFlash");
                _screenFlash.transform.SetParent(_canvasRoot, false);
                var rt = _screenFlash.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                _screenFlashImage = _screenFlash.AddComponent<Image>();
                _screenFlashImage.raycastTarget = false;
                _screenFlash.SetActive(false);
            }

            if (_screenFlashImage != null)
            {
                _screenFlashImage.color = flashColor;
                _screenFlash.SetActive(true);
                _screenFlash.transform.SetAsLastSibling();
                _screenFlashTimer = ScreenFlashDuration;
            }
        }

        private void UpdateScreenFlash()
        {
            if (_screenFlashTimer <= 0f) return;

            _screenFlashTimer -= Time.unscaledDeltaTime;
            if (_screenFlashTimer <= 0f)
            {
                if (_screenFlash != null) _screenFlash.SetActive(false);
                return;
            }

            if (_screenFlashImage != null)
            {
                var c = _screenFlashImage.color;
                c.a = Mathf.Clamp01(_screenFlashTimer / ScreenFlashDuration) * 0.5f;
                _screenFlashImage.color = c;
            }
        }

        private void SpawnFireworks(Color colorA, Color colorB)
        {
            if (_fireworksPS == null)
            {
                var go = new GameObject("FireworksEffect");
                go.transform.SetParent(transform, false);
                _fireworksPS = go.AddComponent<ParticleSystem>();

                var main = _fireworksPS.main;
                main.duration = 2f;
                main.loop = false;
                main.startLifetime = 1.5f;
                main.startSpeed = 12f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
                main.maxParticles = 200;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.gravityModifier = 0.5f;

                var emission = _fireworksPS.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[]
                {
                    new ParticleSystem.Burst(0f, 60),
                    new ParticleSystem.Burst(0.3f, 50),
                    new ParticleSystem.Burst(0.6f, 40)
                });

                var shape = _fireworksPS.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 2f;

                var colorOverLifetime = _fireworksPS.colorOverLifetime;
                colorOverLifetime.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
                );
                colorOverLifetime.color = gradient;

                var sizeOverLifetime = _fireworksPS.sizeOverLifetime;
                sizeOverLifetime.enabled = true;
                sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                    AnimationCurve.EaseInOut(0f, 1f, 1f, 0.1f));

                // レンダラー設定
                var renderer = go.GetComponent<ParticleSystemRenderer>();
                renderer.material = new Material(Shader.Find("Particles/Standard Unlit"))
                {
                    color = Color.white
                };
                renderer.renderMode = ParticleSystemRenderMode.Billboard;

                _fireworksPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            // 色を更新してカメラ前で発射
            var m = _fireworksPS.main;
            m.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);

            if (Camera.main != null)
            {
                _fireworksPS.transform.position = Camera.main.transform.position
                    + Camera.main.transform.forward * 20f
                    + Vector3.up * 5f;
            }

            _fireworksPS.Clear();
            _fireworksPS.Play();
        }
    }
}
