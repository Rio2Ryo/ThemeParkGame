// ============================================================
// RuntimeHUD - CoasterDesign partial
// カスタムコースター設計パネル（Phase 7: H4）
// CoasterDesignSystem の RailElement ベース設計UIを提供
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ThemeParkGame.Attraction;

namespace ThemeParkGame.Core
{
    public partial class RuntimeHUD
    {
        // ---- コースター設計パネル ----
        private GameObject _coasterDesignPanel;
        private Text _cdTitle;
        private Text _cdSpecsText;
        private Text _cdRailListText;
        private Text _cdQualityText;
        private Text _cdCommentText;
        private Text _cdCostText;
        private readonly List<GameObject> _cdRailButtons = new List<GameObject>();

        // ================================================================
        // コースター設計パネル構築
        // ================================================================

        private void BuildCoasterDesignPanel(RectTransform root)
        {
            float panelW = 620f, panelH = 700f;

            _coasterDesignPanel = new GameObject("CoasterDesignPanel");
            _coasterDesignPanel.transform.SetParent(root, false);
            var rt = _coasterDesignPanel.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(panelW, panelH);

            var bgImg = _coasterDesignPanel.AddComponent<Image>();
            bgImg.color = new Color(0.05f, 0.07f, 0.15f, 0.97f);
            bgImg.raycastTarget = true;

            float y = 10f;

            // タイトル
            _cdTitle = MakeLabel(rt, "Title", "コースター設計ツール", 24, Gold,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            SetAnchoredTopLeft(_cdTitle.rectTransform, 10f, y, panelW - 20f, 30f);
            y += 38f;

            // ---- レール要素パレット ----
            var paletteLabel = MakeLabel(rt, "PaletteLabel", "--- レール要素 ---", 13, Muted,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            SetAnchoredTopLeft(paletteLabel.rectTransform, 10f, y, panelW - 20f, 18f);
            y += 22f;

            // レール要素ボタンを配置
            float btnW = 90f, btnH = 32f, btnGap = 4f;
            int col = 0;
            float startX = 15f;
            float btnY = y;

            var railDefs = new[]
            {
                new { Type = RailElementType.Straight,     Label = "直線" },
                new { Type = RailElementType.GentleCurve,  Label = "緩カーブ" },
                new { Type = RailElementType.SharpCurve,   Label = "急カーブ" },
                new { Type = RailElementType.SmallHill,    Label = "小丘" },
                new { Type = RailElementType.MediumHill,   Label = "中丘" },
                new { Type = RailElementType.LargeHill,    Label = "大丘" },
                new { Type = RailElementType.Loop,         Label = "ループ" },
                new { Type = RailElementType.Corkscrew,    Label = "コークスクリュー" },
                new { Type = RailElementType.Helix,        Label = "ヘリックス" },
                new { Type = RailElementType.Brake,        Label = "ブレーキ" },
                new { Type = RailElementType.LaunchBoost,  Label = "加速ブースト" },
                new { Type = RailElementType.Drop,         Label = "急降下" },
                new { Type = RailElementType.Tunnel,       Label = "トンネル" },
                new { Type = RailElementType.WaterSplash,  Label = "スプラッシュ" },
            };

            foreach (var railDef in railDefs)
            {
                float bx = startX + col * (btnW + btnGap);
                float by = btnY;

                var btnGo = MakePanel(rt, $"Rail_{railDef.Type}", btnW, btnH,
                    new Color(0.15f, 0.3f, 0.45f));
                var btnRt = btnGo.GetComponent<RectTransform>();
                SetAnchoredTopLeft(btnRt, bx, by, btnW, btnH);
                var btnImg = btnGo.GetComponent<Image>();
                btnImg.raycastTarget = true;

                var btn = btnGo.AddComponent<Button>();
                btn.targetGraphic = btnImg;

                var capturedType = railDef.Type;
                btn.onClick.AddListener(() =>
                {
                    CoasterDesignSystem.Instance?.AddRailElement(capturedType);
                    RefreshCoasterDesignPanel();
                });

                var lblText = MakeLabel(btnRt, "L", railDef.Label, 11,
                    Color.white, FontStyle.Normal, TextAnchor.MiddleCenter);
                StretchFill(lblText.rectTransform);

                _cdRailButtons.Add(btnGo);

                col++;
                if (col >= 6)
                {
                    col = 0;
                    btnY += btnH + btnGap;
                }
            }

            if (col > 0) btnY += btnH + btnGap;
            y = btnY + 6f;

            // ---- 速度クラス選択 ----
            var speedLabel = MakeLabel(rt, "SpeedLabel", "速度:", 13, Cyan,
                FontStyle.Bold, TextAnchor.MiddleLeft);
            SetAnchoredTopLeft(speedLabel.rectTransform, 15f, y, 40f, 26f);

            var speedClasses = new[] {
                CoasterSpeedClass.Slow, CoasterSpeedClass.Medium,
                CoasterSpeedClass.Fast, CoasterSpeedClass.Extreme
            };
            float sx = 60f;
            foreach (var sc in speedClasses)
            {
                var sBtnGo = MakePanel(rt, $"Speed_{sc}", 85f, 26f, new Color(0.2f, 0.35f, 0.5f));
                SetAnchoredTopLeft(sBtnGo.GetComponent<RectTransform>(), sx, y, 85f, 26f);
                sBtnGo.GetComponent<Image>().raycastTarget = true;
                var sBtn = sBtnGo.AddComponent<Button>();
                sBtn.targetGraphic = sBtnGo.GetComponent<Image>();
                var capturedSc = sc;
                sBtn.onClick.AddListener(() =>
                {
                    CoasterDesignSystem.Instance?.SetSpeedClass(capturedSc);
                    RefreshCoasterDesignPanel();
                });
                var sLbl = MakeLabel(sBtnGo.GetComponent<RectTransform>(), "L",
                    CoasterDesignSystem.GetSpeedClassName(sc), 10,
                    Color.white, FontStyle.Normal, TextAnchor.MiddleCenter);
                StretchFill(sLbl.rectTransform);
                sx += 89f;
            }
            y += 30f;

            // ---- 高低差クラス選択 ----
            var heightLabel = MakeLabel(rt, "HeightLabel", "高低差:", 13, Cyan,
                FontStyle.Bold, TextAnchor.MiddleLeft);
            SetAnchoredTopLeft(heightLabel.rectTransform, 15f, y, 50f, 26f);

            var heightClasses = new[] {
                CoasterHeightClass.Low, CoasterHeightClass.Medium,
                CoasterHeightClass.High, CoasterHeightClass.Extreme
            };
            float hx = 60f;
            foreach (var hc in heightClasses)
            {
                var hBtnGo = MakePanel(rt, $"Height_{hc}", 85f, 26f, new Color(0.2f, 0.35f, 0.5f));
                SetAnchoredTopLeft(hBtnGo.GetComponent<RectTransform>(), hx, y, 85f, 26f);
                hBtnGo.GetComponent<Image>().raycastTarget = true;
                var hBtn = hBtnGo.AddComponent<Button>();
                hBtn.targetGraphic = hBtnGo.GetComponent<Image>();
                var capturedHc = hc;
                hBtn.onClick.AddListener(() =>
                {
                    CoasterDesignSystem.Instance?.SetHeightClass(capturedHc);
                    RefreshCoasterDesignPanel();
                });
                var hLbl = MakeLabel(hBtnGo.GetComponent<RectTransform>(), "L",
                    CoasterDesignSystem.GetHeightClassName(hc), 10,
                    Color.white, FontStyle.Normal, TextAnchor.MiddleCenter);
                StretchFill(hLbl.rectTransform);
                hx += 89f;
            }
            y += 34f;

            // ---- 区切り線 ----
            var sep = MakeLabel(rt, "Sep", "────────────────────────────", 10, Muted,
                FontStyle.Normal, TextAnchor.MiddleCenter);
            SetAnchoredTopLeft(sep.rectTransform, 10f, y, panelW - 20f, 14f);
            y += 18f;

            // ---- 現在のレール構成 ----
            _cdRailListText = MakeLabel(rt, "RailList", "レール: (なし)", 13, Color.white,
                FontStyle.Normal, TextAnchor.UpperLeft);
            _cdRailListText.horizontalOverflow = HorizontalWrapMode.Wrap;
            SetAnchoredTopLeft(_cdRailListText.rectTransform, 15f, y, panelW - 30f, 60f);
            y += 64f;

            // 末尾レール削除ボタン
            var removeBtn = MakePanel(rt, "RemoveBtn", 120f, 26f, new Color(0.5f, 0.25f, 0.25f));
            SetAnchoredTopLeft(removeBtn.GetComponent<RectTransform>(), 15f, y, 120f, 26f);
            removeBtn.GetComponent<Image>().raycastTarget = true;
            var rmBtn = removeBtn.AddComponent<Button>();
            rmBtn.targetGraphic = removeBtn.GetComponent<Image>();
            rmBtn.onClick.AddListener(() =>
            {
                var cds = CoasterDesignSystem.Instance;
                if (cds?.CurrentDesign != null && cds.CurrentDesign.RailElements.Count > 0)
                {
                    cds.RemoveRailElement(cds.CurrentDesign.RailElements.Count - 1);
                    RefreshCoasterDesignPanel();
                }
            });
            var rmLbl = MakeLabel(removeBtn.GetComponent<RectTransform>(), "L", "末尾を削除", 12,
                Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(rmLbl.rectTransform);

            // 全削除（新規デザイン開始）ボタン
            var clearBtn = MakePanel(rt, "ClearBtn", 100f, 26f, new Color(0.5f, 0.2f, 0.2f));
            SetAnchoredTopLeft(clearBtn.GetComponent<RectTransform>(), 145f, y, 100f, 26f);
            clearBtn.GetComponent<Image>().raycastTarget = true;
            var clrBtn = clearBtn.AddComponent<Button>();
            clrBtn.targetGraphic = clearBtn.GetComponent<Image>();
            clrBtn.onClick.AddListener(() =>
            {
                CoasterDesignSystem.Instance?.StartNewDesign();
                RefreshCoasterDesignPanel();
            });
            var clrLbl = MakeLabel(clearBtn.GetComponent<RectTransform>(), "L", "全削除", 12,
                Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(clrLbl.rectTransform);

            // 車両数変更ボタン
            var trainBtn = MakePanel(rt, "TrainBtn", 120f, 26f, new Color(0.3f, 0.3f, 0.55f));
            SetAnchoredTopLeft(trainBtn.GetComponent<RectTransform>(), 255f, y, 120f, 26f);
            trainBtn.GetComponent<Image>().raycastTarget = true;
            var tBtn = trainBtn.AddComponent<Button>();
            tBtn.targetGraphic = trainBtn.GetComponent<Image>();
            tBtn.onClick.AddListener(() =>
            {
                var cds = CoasterDesignSystem.Instance;
                if (cds?.CurrentDesign != null)
                {
                    int next = (cds.CurrentDesign.TrainCount % 3) + 1;
                    cds.SetTrainCount(next);
                    RefreshCoasterDesignPanel();
                }
            });
            var tLbl = MakeLabel(trainBtn.GetComponent<RectTransform>(), "L", "車両数 変更", 12,
                Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(tLbl.rectTransform);
            y += 32f;

            // ---- スペック表示 ----
            _cdSpecsText = MakeLabel(rt, "Specs",
                "興奮度: --- | 嘔吐率: --- | 乗車時間: ---", 14,
                Cyan, FontStyle.Normal, TextAnchor.MiddleLeft);
            SetAnchoredTopLeft(_cdSpecsText.rectTransform, 15f, y, panelW - 30f, 22f);
            y += 24f;

            _cdCostText = MakeLabel(rt, "Cost", "建設費: --- | 維持費: --- | チケット: ---", 14,
                Green, FontStyle.Normal, TextAnchor.MiddleLeft);
            SetAnchoredTopLeft(_cdCostText.rectTransform, 15f, y, panelW - 30f, 22f);
            y += 26f;

            // ---- 設計品質スコア ----
            _cdQualityText = MakeLabel(rt, "Quality", "設計品質: ---", 16, Gold,
                FontStyle.Bold, TextAnchor.MiddleLeft);
            SetAnchoredTopLeft(_cdQualityText.rectTransform, 15f, y, panelW - 30f, 22f);
            y += 24f;

            _cdCommentText = MakeLabel(rt, "Comment", "", 13, Muted,
                FontStyle.Normal, TextAnchor.MiddleLeft);
            _cdCommentText.horizontalOverflow = HorizontalWrapMode.Wrap;
            SetAnchoredTopLeft(_cdCommentText.rectTransform, 15f, y, panelW - 30f, 36f);
            y += 40f;

            // ---- 建設ボタン ----
            var buildBtn = MakePanel(rt, "BuildCoasterBtn", 200f, 40f, new Color(0.2f, 0.55f, 0.3f));
            var buildBtnRt = buildBtn.GetComponent<RectTransform>();
            buildBtnRt.anchorMin = buildBtnRt.anchorMax = new Vector2(0.5f, 0f);
            buildBtnRt.pivot = new Vector2(0.5f, 0f);
            buildBtnRt.anchoredPosition = new Vector2(-60f, 12f);
            buildBtn.GetComponent<Image>().raycastTarget = true;
            var bBtn = buildBtn.AddComponent<Button>();
            bBtn.targetGraphic = buildBtn.GetComponent<Image>();
            bBtn.onClick.AddListener(() =>
            {
                CoasterDesignSystem.Instance?.FinalizeAndBuild();
                RefreshCoasterDesignPanel();
            });
            var bLbl = MakeLabel(buildBtnRt, "L", "建設する", 18, Color.white,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(bLbl.rectTransform);

            // 閉じるボタン
            var closeGo = MakePanel(rt, "CloseBtn", 100f, 40f, new Color(0.4f, 0.42f, 0.5f));
            var closeRt = closeGo.GetComponent<RectTransform>();
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(0.5f, 0f);
            closeRt.pivot = new Vector2(0.5f, 0f);
            closeRt.anchoredPosition = new Vector2(80f, 12f);
            closeGo.GetComponent<Image>().raycastTarget = true;
            var closeBtn = closeGo.AddComponent<Button>();
            closeBtn.targetGraphic = closeGo.GetComponent<Image>();
            closeBtn.onClick.AddListener(() => _coasterDesignPanel.SetActive(false));
            var closeLbl = MakeLabel(closeRt, "L", "閉じる", 16, Color.white,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(closeLbl.rectTransform);

            _coasterDesignPanel.SetActive(false);
        }

        // ================================================================
        // パネル更新
        // ================================================================

        private void RefreshCoasterDesignPanel()
        {
            var cds = CoasterDesignSystem.Instance;
            if (cds == null || cds.CurrentDesign == null) return;

            var design = cds.CurrentDesign;

            // レール構成テキスト
            if (design.RailElements.Count == 0)
            {
                _cdRailListText.text = "レール: (なし) -- 上のボタンで要素を追加";
            }
            else
            {
                var sb = new System.Text.StringBuilder("レール: ");
                for (int i = 0; i < design.RailElements.Count; i++)
                {
                    if (i > 0) sb.Append(" > ");
                    sb.Append(CoasterDesignSystem.GetElementDisplayName(design.RailElements[i].Type));
                }
                sb.Append($" ({design.RailElements.Count}/20)");
                _cdRailListText.text = sb.ToString();
            }

            // スペック
            _cdSpecsText.text = $"興奮度: {design.CalculatedExcitement:F1}/10 | " +
                                $"嘔吐率: {design.CalculatedNausea:P0} | " +
                                $"乗車時間: {design.CalculatedRideDuration:F0}秒 | " +
                                $"車両: {design.TrainCount}編成";

            // コスト
            _cdCostText.text = $"建設費: ${design.CalculatedBuildCost:N0} | " +
                               $"維持費: ${design.CalculatedMaintenanceCost:N0}/月 | " +
                               $"チケット: ${design.CalculatedTicketPrice:N0}";

            // 設計品質スコア
            float qualityPct = design.DesignQualityScore;
            Color qualityColor = qualityPct >= 70f ? Green :
                                 qualityPct >= 40f ? Yellow : Red;
            _cdQualityText.text = $"設計品質: {qualityPct:F0}/100  [{CoasterDesignSystem.GetSpeedClassName(design.SpeedClass)}] [{CoasterDesignSystem.GetHeightClassName(design.HeightClass)}]";
            _cdQualityText.color = qualityColor;

            // デザインコメント
            _cdCommentText.text = design.DesignComment ?? "";
            _cdCommentText.color = qualityPct >= 50f ? Green : Muted;
        }

        /// <summary>コースター設計パネルを開く</summary>
        public void ShowCoasterDesignPanel()
        {
            if (_coasterDesignPanel == null) return;

            // デザインシステムが新規デザインを持っていなければ開始
            var cds = CoasterDesignSystem.Instance;
            if (cds != null && cds.CurrentDesign == null)
            {
                cds.StartNewDesign();
            }

            _coasterDesignPanel.SetActive(true);
            RefreshCoasterDesignPanel();
        }
    }
}
