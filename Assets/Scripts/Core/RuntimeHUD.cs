// ============================================================
// ThemeParkGame - RuntimeHUD
// IMGUI (OnGUI) ベースのランタイムHUDオーバーレイ
// TextMeshProやCanvas参照なしで基本情報を表示する
// ============================================================

using UnityEngine;
using ThemeParkGame.Attraction;
using ThemeParkGame.Visitor;

namespace ThemeParkGame.Core
{
    /// <summary>
    /// プレハブ/Canvas設定なしで動作するOnGUIベースのHUD。
    /// 入場者数・収益・満足度・ゲーム速度をリアルタイム表示する。
    /// アトラクション稼働状況パネルを含む。
    /// WebGLデモ用の軽量実装。
    /// </summary>
    public class RuntimeHUD : MonoBehaviour
    {
        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _smallLabelStyle;
        private GUIStyle _greenLabelStyle;
        private GUIStyle _yellowLabelStyle;
        private GUIStyle _redLabelStyle;
        private bool _stylesInitialized;

        // 表示データキャッシュ（毎フレーム更新は重いので0.5秒ごと）
        private float _updateTimer;
        private const float UpdateInterval = 0.5f;

        private string _moneyText = "---";
        private string _visitorText = "---";
        private string _happinessText = "---";
        private string _timeText = "---";
        private string _revenueText = "---";
        private string _totalRevenueText = "---";
        private string _weatherText = "---";
        private string _staffText = "---";
        private int _currentSpeed = 1;

        // アトラクションキャッシュ
        private Attraction.Attraction[] _attractions;
        private float _attractionCacheTimer;
        private const float AttractionCacheInterval = 2f;

        // 来場者状態キャッシュ
        private int _ridingCount;
        private int _waitingCount;
        private int _walkingToAttrCount;
        private int _shoppingCount;
        private int _idleCount;
        private int _leavingCount;

        private void InitStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTex(2, 2, new Color(0.05f, 0.08f, 0.15f, 0.88f)) }
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                normal = { textColor = Color.white },
                fontStyle = FontStyle.Normal
            };

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.9f, 0.85f, 0.5f) },
                alignment = TextAnchor.MiddleCenter
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };

            _smallLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.7f, 0.7f, 0.8f) }
            };

            _greenLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.4f, 0.9f, 0.4f) }
            };

            _yellowLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.9f, 0.9f, 0.3f) }
            };

            _redLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.9f, 0.3f, 0.3f) }
            };
        }

        private void Update()
        {
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.CurrentState != GameState.Playing) return;

            _updateTimer -= Time.unscaledDeltaTime;
            if (_updateTimer > 0f) return;
            _updateTimer = UpdateInterval;

            UpdateData();
        }

        private void UpdateData()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            // 資金
            if (gm.EconomyManager != null)
            {
                float money = gm.EconomyManager.CurrentMoney;
                _moneyText = $"${money:N0}";
                _totalRevenueText = $"総収益: ${gm.EconomyManager.TotalRevenueEarned:N0}";
                _revenueText = $"月収: ${gm.EconomyManager.CurrentMonthRevenue:N0}  月支出: ${gm.EconomyManager.CurrentMonthExpenses:N0}";
            }

            // 来場者
            if (gm.VisitorManager != null)
            {
                int active = gm.VisitorManager.ActiveVisitorCount;
                int today = gm.VisitorManager.TotalVisitorsToday;
                _visitorText = $"{active} ({today})";
                _happinessText = $"{gm.VisitorManager.AverageHappiness:F0}%";

                // 来場者状態集計
                var counts = gm.VisitorManager.GetVisitorCountByState();
                _ridingCount = 0; _waitingCount = 0; _walkingToAttrCount = 0;
                _shoppingCount = 0; _idleCount = 0; _leavingCount = 0;

                foreach (var kv in counts)
                {
                    switch (kv.Key)
                    {
                        case VisitorBehaviorState.RidingAttraction:
                            _ridingCount += kv.Value; break;
                        case VisitorBehaviorState.WaitingInQueue:
                            _waitingCount += kv.Value; break;
                        case VisitorBehaviorState.WalkingToAttraction:
                            _walkingToAttrCount += kv.Value; break;
                        case VisitorBehaviorState.Eating:
                        case VisitorBehaviorState.Drinking:
                        case VisitorBehaviorState.WalkingToShop:
                            _shoppingCount += kv.Value; break;
                        case VisitorBehaviorState.LeavingPark:
                            _leavingCount += kv.Value; break;
                        default:
                            _idleCount += kv.Value; break;
                    }
                }
            }

            // 時間
            if (gm.TimeManager != null)
            {
                var tm = gm.TimeManager;
                int hour = (int)tm.CurrentHour;
                int min = (int)((tm.CurrentHour - hour) * 60);
                _timeText = $"Y{tm.CurrentYear} M{tm.CurrentMonth} D{tm.CurrentDay} {hour:D2}:{min:D2}";
            }

            // 天候
            if (gm.WeatherSystem != null)
            {
                _weatherText = GetWeatherLabel(gm.WeatherSystem.CurrentWeather);
            }

            // スタッフ
            if (gm.StaffManager != null)
            {
                _staffText = $"Staff: {gm.StaffManager.TotalStaffCount}名";
            }

            _currentSpeed = gm.SpeedLevel;

            // アトラクション情報のキャッシュ更新（2秒ごと）
            _attractionCacheTimer -= UpdateInterval;
            if (_attractionCacheTimer <= 0f)
            {
                _attractionCacheTimer = AttractionCacheInterval;
                _attractions = FindObjectsOfType<Attraction.Attraction>();
            }
        }

        private void OnGUI()
        {
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.CurrentState != GameState.Playing) return;

            InitStyles();

            float sw = Screen.width;
            float sh = Screen.height;

            DrawTopBar(sw);
            DrawSpeedControls(sw);
            DrawVisitorPanel(sh);
            DrawAttractionPanel(sw, sh);
        }

        // ================================================================
        // トップバー（資金・来場者・満足度・時間）
        // ================================================================

        private void DrawTopBar(float sw)
        {
            float topBarHeight = 100f;
            float topBarWidth = Mathf.Min(sw - 20f, 720f);
            float topBarX = (sw - topBarWidth) * 0.5f;

            GUI.Box(new Rect(topBarX, 5f, topBarWidth, topBarHeight), "", _boxStyle);

            float x = topBarX + 12f;
            float y = 10f;

            // ヘッダー行: 時間 + 天候
            GUI.Label(new Rect(x, y, topBarWidth - 24f, 22f), $"{_weatherText}  {_timeText}", _headerStyle);
            y += 26f;

            // 情報行1: 資金 | 来場者 | 満足度
            float colW = (topBarWidth - 36f) / 3f;
            GUI.Label(new Rect(x, y, colW, 20f), $"資金: {_moneyText}", _labelStyle);
            GUI.Label(new Rect(x + colW, y, colW, 20f), $"入場者: {_visitorText}", _labelStyle);
            GUI.Label(new Rect(x + colW * 2f, y, colW, 20f), $"満足度: {_happinessText}", _labelStyle);
            y += 22f;

            // 情報行2: 総収益
            GUI.Label(new Rect(x, y, topBarWidth - 24f, 18f), _totalRevenueText, _greenLabelStyle);
            y += 18f;

            // 情報行3: 月次収支 + スタッフ数
            float halfW = (topBarWidth - 24f) * 0.6f;
            GUI.Label(new Rect(x, y, halfW, 18f), _revenueText, _smallLabelStyle);
            GUI.Label(new Rect(x + halfW, y, topBarWidth - 24f - halfW, 18f), _staffText, _smallLabelStyle);
        }

        // ================================================================
        // 速度コントロール
        // ================================================================

        private void DrawSpeedControls(float sw)
        {
            float speedBoxW = 220f;
            float speedBoxH = 40f;
            float speedX = sw - speedBoxW - 10f;
            float speedY = 112f;

            GUI.Box(new Rect(speedX, speedY, speedBoxW, speedBoxH), "", _boxStyle);

            float btnW = 46f;
            float btnX = speedX + 8f;
            float btnY = speedY + 6f;

            if (DrawSpeedButton(btnX, btnY, btnW, "||", _currentSpeed == 0))
                GameManager.Instance.SpeedLevel = 0;
            btnX += btnW + 4f;

            if (DrawSpeedButton(btnX, btnY, btnW, "x1", _currentSpeed == 1))
                GameManager.Instance.SpeedLevel = 1;
            btnX += btnW + 4f;

            if (DrawSpeedButton(btnX, btnY, btnW, "x2", _currentSpeed == 2))
                GameManager.Instance.SpeedLevel = 2;
            btnX += btnW + 4f;

            if (DrawSpeedButton(btnX, btnY, btnW, "x5", _currentSpeed == 5))
                GameManager.Instance.SpeedLevel = 5;
        }

        // ================================================================
        // 左下: 来場者状況パネル
        // ================================================================

        private void DrawVisitorPanel(float sh)
        {
            if (GameManager.Instance.VisitorManager == null) return;

            float infoW = 210f;
            float infoH = 240f;
            float infoX = 10f;
            float infoY = sh - infoH - 10f;

            GUI.Box(new Rect(infoX, infoY, infoW, infoH), "", _boxStyle);

            float ly = infoY + 6f;
            float lx = infoX + 8f;
            float lw = infoW - 16f;

            GUI.Label(new Rect(lx, ly, lw, 20f), "来場者状況", _headerStyle);
            ly += 24f;

            // 状態カウント + カラードット（VisitorVisualControllerの色に対応）
            DrawColorStatLine(lx, ly, lw, "移動中", _walkingToAttrCount, new Color(0.3f, 0.8f, 0.5f)); ly += 18f;
            DrawColorStatLine(lx, ly, lw, "待ち行列", _waitingCount, new Color(1.0f, 0.85f, 0.2f)); ly += 18f;
            DrawColorStatLine(lx, ly, lw, "搭乗中", _ridingCount, new Color(1.0f, 0.45f, 0.1f)); ly += 18f;
            DrawColorStatLine(lx, ly, lw, "買い物/食事", _shoppingCount, new Color(0.9f, 0.6f, 0.8f)); ly += 18f;
            DrawColorStatLine(lx, ly, lw, "散策/休憩", _idleCount, new Color(0.3f, 0.6f, 1.0f)); ly += 18f;
            DrawColorStatLine(lx, ly, lw, "退園中", _leavingCount, new Color(0.5f, 0.5f, 0.5f)); ly += 24f;

            // カラー凡例ヘッダー
            GUI.Label(new Rect(lx, ly, lw, 16f), "色 = 来場者の状態", _smallLabelStyle);
        }

        private void DrawStatLine(float lx, float ly, float lw, string label, int count, GUIStyle style)
        {
            GUI.Label(new Rect(lx, ly, lw - 40f, 18f), label, style);
            GUI.Label(new Rect(lx + lw - 50f, ly, 50f, 18f), count.ToString(), style);
        }

        /// <summary>カラードット付き統計行を描画する</summary>
        private void DrawColorStatLine(float lx, float ly, float lw, string label, int count, Color dotColor)
        {
            // カラードット (12x12)
            var prevColor = GUI.color;
            GUI.color = dotColor;
            GUI.DrawTexture(new Rect(lx, ly + 3f, 12f, 12f), Texture2D.whiteTexture);
            GUI.color = prevColor;

            // ラベル
            GUI.Label(new Rect(lx + 16f, ly, lw - 66f, 18f), label, _smallLabelStyle);
            GUI.Label(new Rect(lx + lw - 50f, ly, 50f, 18f), count.ToString(), _labelStyle);
        }

        // ================================================================
        // 右下: アトラクション稼働状況パネル
        // ================================================================

        private void DrawAttractionPanel(float sw, float sh)
        {
            if (_attractions == null || _attractions.Length == 0) return;

            float panelW = 280f;
            float lineH = 20f;
            float headerH = 28f;
            float padding = 8f;
            float panelH = headerH + (_attractions.Length * (lineH * 2 + 4f)) + padding * 2;
            float panelX = sw - panelW - 10f;
            float panelY = sh - panelH - 10f;

            GUI.Box(new Rect(panelX, panelY, panelW, panelH), "", _boxStyle);

            float ly = panelY + padding;
            float lx = panelX + padding;
            float lw = panelW - padding * 2;

            GUI.Label(new Rect(lx, ly, lw, 22f), "Attraction Status", _headerStyle);
            ly += headerH;

            foreach (var attr in _attractions)
            {
                if (attr == null) continue;

                // 名前と状態
                string stateName = GetCycleStateName(attr.CurrentCycleState);
                GUIStyle stateStyle = GetCycleStateStyle(attr.CurrentCycleState);

                GUI.Label(new Rect(lx, ly, lw, lineH), attr.DisplayName, _labelStyle);
                ly += lineH;

                // 状態 | 行列 | 乗車数
                string detail = $"  {stateName}  Queue:{attr.QueueLength}/{attr.MaxQueueLength}  Rides:{attr.TotalRiderCount}";
                GUI.Label(new Rect(lx, ly, lw, lineH), detail, stateStyle);
                ly += lineH + 4f;
            }
        }

        // ================================================================
        // ヘルパー
        // ================================================================

        private bool DrawSpeedButton(float bx, float by, float bw, string label, bool active)
        {
            var prevColor = GUI.backgroundColor;
            if (active)
                GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
            bool pressed = GUI.Button(new Rect(bx, by, bw, 28f), label, _buttonStyle);
            GUI.backgroundColor = prevColor;
            return pressed;
        }

        private string GetWeatherLabel(Weather weather)
        {
            switch (weather)
            {
                case Weather.Sunny: return "[Sunny]";
                case Weather.Cloudy: return "[Cloudy]";
                case Weather.Rainy: return "[Rainy]";
                case Weather.Snowy: return "[Snowy]";
                case Weather.Hot: return "[Hot]";
                default: return "";
            }
        }

        private string GetCycleStateName(RideCycleState state)
        {
            switch (state)
            {
                case RideCycleState.WaitingForRiders: return "[Waiting]";
                case RideCycleState.Loading: return "[Loading]";
                case RideCycleState.Running: return "[Running]";
                case RideCycleState.Unloading: return "[Unloading]";
                case RideCycleState.BrokenDown: return "[BROKEN]";
                case RideCycleState.Accident: return "[ACCIDENT]";
                default: return "[---]";
            }
        }

        private GUIStyle GetCycleStateStyle(RideCycleState state)
        {
            switch (state)
            {
                case RideCycleState.Running: return _greenLabelStyle;
                case RideCycleState.Loading:
                case RideCycleState.Unloading: return _yellowLabelStyle;
                case RideCycleState.BrokenDown:
                case RideCycleState.Accident: return _redLabelStyle;
                default: return _smallLabelStyle;
            }
        }

        private static Texture2D MakeTex(int width, int height, Color col)
        {
            var pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            var result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}
