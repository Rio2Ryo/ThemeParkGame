// ============================================================
// ThemeParkGame - RuntimeHUD
// IMGUI (OnGUI) ベースのランタイムHUDオーバーレイ
// TextMeshProやCanvas参照なしで基本情報を表示する
// ============================================================

using UnityEngine;
using ThemeParkGame.Visitor;

namespace ThemeParkGame.Core
{
    /// <summary>
    /// プレハブ/Canvas設定なしで動作するOnGUIベースのHUD。
    /// 入場者数・収益・満足度・ゲーム速度をリアルタイム表示する。
    /// WebGLデモ用の軽量実装。
    /// </summary>
    public class RuntimeHUD : MonoBehaviour
    {
        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _smallLabelStyle;
        private bool _stylesInitialized;

        // 表示データキャッシュ（毎フレーム更新は重いので0.5秒ごと）
        private float _updateTimer;
        private const float UpdateInterval = 0.5f;

        private string _moneyText = "---";
        private string _visitorText = "---";
        private string _happinessText = "---";
        private string _timeText = "---";
        private string _revenueText = "---";
        private string _weatherText = "---";
        private int _currentSpeed = 1;

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
                fontSize = 16,
                normal = { textColor = Color.white },
                fontStyle = FontStyle.Normal
            };

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
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
                _revenueText = $"当月収入: ${gm.EconomyManager.CurrentMonthRevenue:N0}  支出: ${gm.EconomyManager.CurrentMonthExpenses:N0}";
            }

            // 来場者
            if (gm.VisitorManager != null)
            {
                int active = gm.VisitorManager.ActiveVisitorCount;
                int today = gm.VisitorManager.TotalVisitorsToday;
                _visitorText = $"{active} 人 (本日計: {today})";
                _happinessText = $"{gm.VisitorManager.AverageHappiness:F0}%";
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
                _weatherText = GetWeatherEmoji(gm.WeatherSystem.CurrentWeather);
            }

            _currentSpeed = gm.SpeedLevel;
        }

        private void OnGUI()
        {
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.CurrentState != GameState.Playing) return;

            InitStyles();

            float sw = Screen.width;
            float sh = Screen.height;

            // --- トップバー ---
            float topBarHeight = 90f;
            float topBarWidth = Mathf.Min(sw - 20f, 700f);
            float topBarX = (sw - topBarWidth) * 0.5f;

            GUI.Box(new Rect(topBarX, 5f, topBarWidth, topBarHeight), "", _boxStyle);

            float x = topBarX + 12f;
            float y = 10f;

            // ヘッダー行: 時間 + 天候
            GUI.Label(new Rect(x, y, topBarWidth - 24f, 24f), $"{_weatherText}  {_timeText}", _headerStyle);
            y += 28f;

            // 情報行1: 資金 | 来場者 | 満足度
            float colW = (topBarWidth - 36f) / 3f;
            GUI.Label(new Rect(x, y, colW, 22f), $"資金: {_moneyText}", _labelStyle);
            GUI.Label(new Rect(x + colW, y, colW, 22f), $"来場者: {_visitorText}", _labelStyle);
            GUI.Label(new Rect(x + colW * 2f, y, colW, 22f), $"満足度: {_happinessText}", _labelStyle);
            y += 26f;

            // 情報行2: 収支
            GUI.Label(new Rect(x, y, topBarWidth - 24f, 18f), _revenueText, _smallLabelStyle);

            // --- 速度コントロール (右上) ---
            float speedBoxW = 200f;
            float speedBoxH = 40f;
            float speedX = sw - speedBoxW - 10f;
            float speedY = topBarHeight + 15f;

            GUI.Box(new Rect(speedX, speedY, speedBoxW, speedBoxH), "", _boxStyle);

            float btnW = 42f;
            float btnX = speedX + 8f;
            float btnY = speedY + 6f;

            if (DrawSpeedButton(btnX, btnY, btnW, "||", _currentSpeed == 0))
                GameManager.Instance.SpeedLevel = 0;
            btnX += btnW + 4f;

            if (DrawSpeedButton(btnX, btnY, btnW, ">", _currentSpeed == 1))
                GameManager.Instance.SpeedLevel = 1;
            btnX += btnW + 4f;

            if (DrawSpeedButton(btnX, btnY, btnW, ">>", _currentSpeed == 2))
                GameManager.Instance.SpeedLevel = 2;
            btnX += btnW + 4f;

            if (DrawSpeedButton(btnX, btnY, btnW, ">>>", _currentSpeed == 3))
                GameManager.Instance.SpeedLevel = 3;

            // --- 左下: 状態別来場者数 ---
            if (GameManager.Instance.VisitorManager != null)
            {
                var vm = GameManager.Instance.VisitorManager;
                float infoW = 220f;
                float infoH = 140f;
                float infoX = 10f;
                float infoY = sh - infoH - 10f;

                GUI.Box(new Rect(infoX, infoY, infoW, infoH), "", _boxStyle);

                float ly = infoY + 6f;
                GUI.Label(new Rect(infoX + 8f, ly, infoW - 16f, 20f), "来場者状況", _headerStyle);
                ly += 24f;

                var counts = vm.GetVisitorCountByState();
                int riding = 0, waiting = 0, shopping = 0, idle = 0;
                foreach (var kv in counts)
                {
                    switch (kv.Key)
                    {
                        case VisitorBehaviorState.RidingAttraction: riding += kv.Value; break;
                        case VisitorBehaviorState.WaitingInQueue: waiting += kv.Value; break;
                        case VisitorBehaviorState.Eating:
                        case VisitorBehaviorState.Drinking:
                        case VisitorBehaviorState.WalkingToShop:
                            shopping += kv.Value; break;
                        case VisitorBehaviorState.Idle:
                        case VisitorBehaviorState.Resting:
                            idle += kv.Value; break;
                    }
                }

                GUI.Label(new Rect(infoX + 8f, ly, infoW, 18f), $"搭乗中: {riding}", _smallLabelStyle); ly += 20f;
                GUI.Label(new Rect(infoX + 8f, ly, infoW, 18f), $"待ち行列: {waiting}", _smallLabelStyle); ly += 20f;
                GUI.Label(new Rect(infoX + 8f, ly, infoW, 18f), $"買い物中: {shopping}", _smallLabelStyle); ly += 20f;
                GUI.Label(new Rect(infoX + 8f, ly, infoW, 18f), $"散策/休憩: {idle}", _smallLabelStyle);
            }
        }

        private bool DrawSpeedButton(float bx, float by, float bw, string label, bool active)
        {
            var prevColor = GUI.backgroundColor;
            if (active)
                GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
            bool pressed = GUI.Button(new Rect(bx, by, bw, 28f), label, _buttonStyle);
            GUI.backgroundColor = prevColor;
            return pressed;
        }

        private string GetWeatherEmoji(Weather weather)
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
