// ============================================================
// ThemeParkGame - StageSelectUI
// シナリオモードのステージ選択画面
// 3カ国(Japan/USA/France) × 各3ステージ構成
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using ThemeParkGame.Core;
using ThemeParkGame.Park;

namespace ThemeParkGame.UI
{
    /// <summary>
    /// シナリオモードのステージ選択UI。
    /// 国を選択後、ステージ（難易度）を選んでゲームを開始する。
    /// 3カ国 × 3ステージ = 9シナリオ構成。
    ///
    /// Japan: Stage1(Easy/France相当), Stage2(Normal/Japan), Stage3(Hard/Russia)
    /// USA:   Stage1(Easy/China),      Stage2(Normal/UK),    Stage3(Hard/USA)
    /// France:Stage1(Easy/India),      Stage2(Normal/Japan), Stage3(Hard/Brazil)
    /// </summary>
    public class StageSelectUI : MonoBehaviour
    {
        // カラー定数
        private static readonly Color BgDark = new Color(0.05f, 0.07f, 0.14f, 0.95f);
        private static readonly Color Gold = new Color(0.95f, 0.88f, 0.45f);
        private static readonly Color Muted = new Color(0.6f, 0.65f, 0.75f);
        private static readonly Color JapanColor = new Color(0.85f, 0.25f, 0.3f);
        private static readonly Color USAColor = new Color(0.2f, 0.4f, 0.8f);
        private static readonly Color FranceColor = new Color(0.2f, 0.45f, 0.75f);

        // ステージ定義
        private struct StageDefinition
        {
            public ScenarioCountry Country;
            public string StageName;
            public string Description;
            public string Objectives;
            public Color StageColor;
        }

        // 3カ国 × 3ステージの定義
        private static readonly StageDefinition[][] AllStages = new StageDefinition[][]
        {
            // Japan
            new StageDefinition[]
            {
                new StageDefinition
                {
                    Country = ScenarioCountry.France,
                    StageName = "Stage 1 - 入門",
                    Description = "初期資金$120,000 / 制限なし",
                    Objectives = "来場者500人 & 月間利益$5,000",
                    StageColor = new Color(0.3f, 0.7f, 0.4f)
                },
                new StageDefinition
                {
                    Country = ScenarioCountry.Japan,
                    StageName = "Stage 2 - 挑戦",
                    Description = "初期資金$60,000 / 8年以内",
                    Objectives = "パーク評価70 & 来場者1,000人 & 快適性認定証",
                    StageColor = new Color(0.8f, 0.7f, 0.2f)
                },
                new StageDefinition
                {
                    Country = ScenarioCountry.Russia,
                    StageName = "Stage 3 - 極限",
                    Description = "初期資金$20,000 / 5年以内",
                    Objectives = "全5認定証 & ゴールデンチケット3枚",
                    StageColor = new Color(0.8f, 0.2f, 0.2f)
                },
            },
            // USA
            new StageDefinition[]
            {
                new StageDefinition
                {
                    Country = ScenarioCountry.China,
                    StageName = "Stage 1 - 入門",
                    Description = "初期資金$80,000 / 制限なし",
                    Objectives = "アトラクション5基 & 来場者300人",
                    StageColor = new Color(0.3f, 0.7f, 0.4f)
                },
                new StageDefinition
                {
                    Country = ScenarioCountry.UnitedKingdom,
                    StageName = "Stage 2 - 挑戦",
                    Description = "初期資金$50,000 / 8年以内",
                    Objectives = "月間利益$15,000 & パーク評価60 & 来場者800人",
                    StageColor = new Color(0.8f, 0.7f, 0.2f)
                },
                new StageDefinition
                {
                    Country = ScenarioCountry.UnitedStates,
                    StageName = "Stage 3 - 極限",
                    Description = "初期資金$40,000 / 6年以内",
                    Objectives = "来場者2,000人 & 総収入$500,000 & ゾーンアンロック",
                    StageColor = new Color(0.8f, 0.2f, 0.2f)
                },
            },
            // France
            new StageDefinition[]
            {
                new StageDefinition
                {
                    Country = ScenarioCountry.India,
                    StageName = "Stage 1 - 入門",
                    Description = "初期資金$90,000 / 制限なし",
                    Objectives = "来場者600人 & 平均満足度70%",
                    StageColor = new Color(0.3f, 0.7f, 0.4f)
                },
                new StageDefinition
                {
                    Country = ScenarioCountry.Egypt,
                    StageName = "Stage 2 - 挑戦",
                    Description = "初期資金$100,000 / 制限なし",
                    Objectives = "来場者400人 & 総収入$80,000",
                    StageColor = new Color(0.8f, 0.7f, 0.2f)
                },
                new StageDefinition
                {
                    Country = ScenarioCountry.Brazil,
                    StageName = "Stage 3 - 極限",
                    Description = "初期資金$35,000 / 6年以内",
                    Objectives = "ムード認定証 & 興奮度認定証 & 来場者1,500人",
                    StageColor = new Color(0.8f, 0.2f, 0.2f)
                },
            },
        };

        private static readonly string[] CountryNames = { "JAPAN", "USA", "FRANCE" };
        private static readonly string[] CountryIcons = { "[JP]", "[US]", "[FR]" };
        private static readonly Color[] CountryColors = { JapanColor, USAColor, FranceColor };

        private RectTransform _root;
        private GameObject _countryPanel;
        private GameObject _stagePanel;
        private int _selectedCountryIndex = -1;

        /// <summary>ステージ選択UIを構築して表示する</summary>
        public static StageSelectUI Show(RectTransform canvasRoot)
        {
            var go = new GameObject("StageSelectUI");
            go.transform.SetParent(canvasRoot, false);
            var ui = go.AddComponent<StageSelectUI>();
            ui._root = go.AddComponent<RectTransform>();
            ui._root.anchorMin = Vector2.zero;
            ui._root.anchorMax = Vector2.one;
            ui._root.offsetMin = Vector2.zero;
            ui._root.offsetMax = Vector2.zero;
            ui.BuildCountrySelect();
            return ui;
        }

        /// <summary>UI を閉じてタイトル画面に戻る</summary>
        public void Close()
        {
            Destroy(gameObject);
        }

        // ================================================================
        // 国選択画面
        // ================================================================

        private void BuildCountrySelect()
        {
            if (_countryPanel != null) Destroy(_countryPanel);
            if (_stagePanel != null) Destroy(_stagePanel);

            _countryPanel = new GameObject("CountryPanel");
            _countryPanel.transform.SetParent(_root, false);
            var rt = _countryPanel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // 背景
            var bgImg = _countryPanel.AddComponent<Image>();
            bgImg.color = new Color(0.03f, 0.04f, 0.1f, 0.98f);
            bgImg.raycastTarget = true;

            // タイトル
            var title = MakeLabel(rt, "Title", "SCENARIO MODE", 48, Gold,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -40f);
            titleRt.sizeDelta = new Vector2(600f, 60f);

            var subtitle = MakeLabel(rt, "Subtitle", "- 国を選択してください -", 22, Muted,
                FontStyle.Normal, TextAnchor.MiddleCenter);
            var subRt = subtitle.rectTransform;
            subRt.anchorMin = subRt.anchorMax = new Vector2(0.5f, 1f);
            subRt.pivot = new Vector2(0.5f, 1f);
            subRt.anchoredPosition = new Vector2(0f, -105f);
            subRt.sizeDelta = new Vector2(500f, 30f);

            // 3カ国ボタン
            float cardW = 280f;
            float cardH = 360f;
            float gap = 30f;
            float totalW = cardW * 3f + gap * 2f;
            float startX = -totalW / 2f + cardW / 2f;

            for (int i = 0; i < 3; i++)
            {
                float cx = startX + i * (cardW + gap);
                CreateCountryCard(rt, i, cx, -60f, cardW, cardH);
            }

            // 戻るボタン
            CreateBackButton(rt, "BACK", new Vector2(0f, -520f), () =>
            {
                Close();
                // タイトル画面を再生成
                GameBootstrapper.RecreateStartScreen();
            });
        }

        private void CreateCountryCard(RectTransform parent, int countryIndex,
            float x, float y, float w, float h)
        {
            var card = MakePanel(parent, $"Country_{countryIndex}", w, h, BgDark);
            var cardRt = card.GetComponent<RectTransform>();
            cardRt.anchorMin = cardRt.anchorMax = new Vector2(0.5f, 1f);
            cardRt.pivot = new Vector2(0.5f, 1f);
            cardRt.anchoredPosition = new Vector2(x, y - 80f);

            // 国フラグ（テキストアイコン）
            var icon = MakeLabel(cardRt, "Icon", CountryIcons[countryIndex], 48, CountryColors[countryIndex],
                FontStyle.Bold, TextAnchor.MiddleCenter);
            var iconRt = icon.rectTransform;
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 1f);
            iconRt.pivot = new Vector2(0.5f, 1f);
            iconRt.anchoredPosition = new Vector2(0f, -20f);
            iconRt.sizeDelta = new Vector2(w, 60f);

            // 国名
            var name = MakeLabel(cardRt, "Name", CountryNames[countryIndex], 32, Color.white,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            var nameRt = name.rectTransform;
            nameRt.anchorMin = nameRt.anchorMax = new Vector2(0.5f, 1f);
            nameRt.pivot = new Vector2(0.5f, 1f);
            nameRt.anchoredPosition = new Vector2(0f, -85f);
            nameRt.sizeDelta = new Vector2(w, 40f);

            // ステージ概要
            string stagesText = "";
            for (int s = 0; s < 3; s++)
            {
                var stage = AllStages[countryIndex][s];
                bool cleared = ScenarioManager.IsCleared(stage.Country);
                string mark = cleared ? "[CLEAR]" : "";
                stagesText += $"  Stage {s + 1}: {stage.Objectives} {mark}\n";
            }

            var desc = MakeLabel(cardRt, "Desc", stagesText, 13, Muted,
                FontStyle.Normal, TextAnchor.UpperLeft);
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            var descRt = desc.rectTransform;
            descRt.anchorMin = descRt.anchorMax = new Vector2(0.5f, 1f);
            descRt.pivot = new Vector2(0.5f, 1f);
            descRt.anchoredPosition = new Vector2(0f, -135f);
            descRt.sizeDelta = new Vector2(w - 20f, 140f);

            // 「選択」ボタン
            var selectBtn = MakePanel(cardRt, "SelectBtn", w - 40f, 50f, CountryColors[countryIndex]);
            var selectRt = selectBtn.GetComponent<RectTransform>();
            selectRt.anchorMin = selectRt.anchorMax = new Vector2(0.5f, 0f);
            selectRt.pivot = new Vector2(0.5f, 0f);
            selectRt.anchoredPosition = new Vector2(0f, 15f);

            var selectImg = selectBtn.GetComponent<Image>();
            selectImg.raycastTarget = true;
            var btn = selectBtn.AddComponent<Button>();
            btn.targetGraphic = selectImg;
            var colors = btn.colors;
            colors.highlightedColor = CountryColors[countryIndex] * 1.2f;
            colors.pressedColor = CountryColors[countryIndex] * 0.7f;
            btn.colors = colors;

            var selectLabel = MakeLabel(selectRt, "Label", "SELECT", 22, Color.white,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(selectLabel.rectTransform);

            int idx = countryIndex;
            btn.onClick.AddListener(() => OnCountrySelected(idx));
        }

        // ================================================================
        // ステージ選択画面
        // ================================================================

        private void OnCountrySelected(int countryIndex)
        {
            _selectedCountryIndex = countryIndex;
            if (_countryPanel != null) _countryPanel.SetActive(false);
            BuildStageSelect(countryIndex);
        }

        private void BuildStageSelect(int countryIndex)
        {
            if (_stagePanel != null) Destroy(_stagePanel);

            _stagePanel = new GameObject("StagePanel");
            _stagePanel.transform.SetParent(_root, false);
            var rt = _stagePanel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bgImg = _stagePanel.AddComponent<Image>();
            bgImg.color = new Color(0.03f, 0.04f, 0.1f, 0.98f);
            bgImg.raycastTarget = true;

            // タイトル
            var title = MakeLabel(rt, "Title",
                $"{CountryIcons[countryIndex]} {CountryNames[countryIndex]} - STAGES",
                42, CountryColors[countryIndex], FontStyle.Bold, TextAnchor.MiddleCenter);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -40f);
            titleRt.sizeDelta = new Vector2(700f, 55f);

            // 3ステージカード
            var stages = AllStages[countryIndex];
            float cardW = 580f;
            float cardH = 100f;
            float gap = 16f;

            for (int s = 0; s < 3; s++)
            {
                float cy = -120f - s * (cardH + gap);
                CreateStageCard(rt, countryIndex, s, stages[s], cy, cardW, cardH);
            }

            // 戻るボタン
            CreateBackButton(rt, "BACK", new Vector2(0f, -490f), () =>
            {
                if (_stagePanel != null) Destroy(_stagePanel);
                if (_countryPanel != null) _countryPanel.SetActive(true);
            });
        }

        private void CreateStageCard(RectTransform parent, int countryIndex, int stageIndex,
            StageDefinition stage, float y, float w, float h)
        {
            bool cleared = ScenarioManager.IsCleared(stage.Country);

            var card = MakePanel(parent, $"Stage_{stageIndex}", w, h,
                cleared ? new Color(0.08f, 0.15f, 0.08f, 0.9f) : BgDark);
            var cardRt = card.GetComponent<RectTransform>();
            cardRt.anchorMin = cardRt.anchorMax = new Vector2(0.5f, 1f);
            cardRt.pivot = new Vector2(0.5f, 1f);
            cardRt.anchoredPosition = new Vector2(0f, y);

            // 左に難易度マーカー
            var marker = MakePanel(cardRt, "Marker", 6f, h - 10f, stage.StageColor);
            var markerRt = marker.GetComponent<RectTransform>();
            markerRt.anchorMin = markerRt.anchorMax = new Vector2(0f, 0.5f);
            markerRt.pivot = new Vector2(0f, 0.5f);
            markerRt.anchoredPosition = new Vector2(8f, 0f);

            // ステージ名
            string nameText = cleared ? $"{stage.StageName}  [CLEAR]" : stage.StageName;
            var nameLabel = MakeLabel(cardRt, "Name", nameText, 22, Color.white,
                FontStyle.Bold, TextAnchor.MiddleLeft);
            var nameRt = nameLabel.rectTransform;
            nameRt.anchorMin = nameRt.anchorMax = new Vector2(0f, 1f);
            nameRt.pivot = new Vector2(0f, 1f);
            nameRt.anchoredPosition = new Vector2(22f, -6f);
            nameRt.sizeDelta = new Vector2(w - 160f, 30f);

            // 説明
            var descLabel = MakeLabel(cardRt, "Desc", stage.Description, 14, Muted,
                FontStyle.Normal, TextAnchor.MiddleLeft);
            var descRt = descLabel.rectTransform;
            descRt.anchorMin = descRt.anchorMax = new Vector2(0f, 1f);
            descRt.pivot = new Vector2(0f, 1f);
            descRt.anchoredPosition = new Vector2(22f, -36f);
            descRt.sizeDelta = new Vector2(w - 160f, 22f);

            // 目標
            var objLabel = MakeLabel(cardRt, "Obj", $"目標: {stage.Objectives}", 13, Gold,
                FontStyle.Normal, TextAnchor.MiddleLeft);
            var objRt = objLabel.rectTransform;
            objRt.anchorMin = objRt.anchorMax = new Vector2(0f, 1f);
            objRt.pivot = new Vector2(0f, 1f);
            objRt.anchoredPosition = new Vector2(22f, -58f);
            objRt.sizeDelta = new Vector2(w - 160f, 22f);

            // 開始ボタン
            var startBtnGo = MakePanel(cardRt, "StartBtn", 120f, 44f, stage.StageColor);
            var startRt = startBtnGo.GetComponent<RectTransform>();
            startRt.anchorMin = startRt.anchorMax = new Vector2(1f, 0.5f);
            startRt.pivot = new Vector2(1f, 0.5f);
            startRt.anchoredPosition = new Vector2(-15f, 0f);

            var startImg = startBtnGo.GetComponent<Image>();
            startImg.raycastTarget = true;
            var btn = startBtnGo.AddComponent<Button>();
            btn.targetGraphic = startImg;
            var colors = btn.colors;
            colors.highlightedColor = stage.StageColor * 1.3f;
            colors.pressedColor = stage.StageColor * 0.6f;
            btn.colors = colors;

            var startLabel = MakeLabel(startRt, "Label",
                cleared ? "REPLAY" : "START", 20, Color.white,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(startLabel.rectTransform);

            var country = stage.Country;
            btn.onClick.AddListener(() => OnStageStart(country));
        }

        // ================================================================
        // ステージ開始
        // ================================================================

        private void OnStageStart(ScenarioCountry country)
        {
            if (GameManager.Instance == null) return;

            GameManager.Instance.StartScenario(country);

            // ScenarioManagerに登録
            if (ScenarioManager.Instance != null)
            {
                var scenarioData = ScenarioDatabase.GetScenario(country);
                if (scenarioData != null)
                    ScenarioManager.Instance.StartScenario(scenarioData);
            }

            Destroy(gameObject);
        }

        // ================================================================
        // 共通UI部品
        // ================================================================

        private void CreateBackButton(RectTransform parent, string label, Vector2 pos,
            UnityEngine.Events.UnityAction onClick)
        {
            var btnGo = MakePanel(parent, "BackBtn", 200f, 50f, new Color(0.3f, 0.32f, 0.4f, 0.9f));
            var btnRt = btnGo.GetComponent<RectTransform>();
            btnRt.anchorMin = btnRt.anchorMax = new Vector2(0.5f, 1f);
            btnRt.pivot = new Vector2(0.5f, 1f);
            btnRt.anchoredPosition = pos;

            var img = btnGo.GetComponent<Image>();
            img.raycastTarget = true;
            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.4f, 0.42f, 0.5f);
            colors.pressedColor = new Color(0.2f, 0.22f, 0.3f);
            btn.colors = colors;
            btn.onClick.AddListener(onClick);

            var txt = MakeLabel(btnRt, "Label", label, 22, Color.white,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(txt.rectTransform);
        }

        // ================================================================
        // ヘルパー
        // ================================================================

        private static Font CachedFont()
        {
            return FontManager.Regular;
        }

        private static GameObject MakePanel(RectTransform parent, string name, float w, float h, Color bg)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
            var img = go.AddComponent<Image>();
            img.color = bg;
            img.raycastTarget = false;
            return go;
        }

        private static Text MakeLabel(RectTransform parent, string name, string content,
            int fontSize, Color color, FontStyle style, TextAnchor align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var t = go.AddComponent<Text>();
            t.text = content;
            t.font = CachedFont();
            t.fontSize = fontSize;
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private static void StretchFill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
