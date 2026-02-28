# ThemeParkGame コンテンツ拡張レポート

## 実装日: 2026-02-28

---

## 1. アトラクション3種追加

| ID | 名前 | カテゴリ | ゾーン | 興奮度 | 嘔吐率 | 定員 | 時間 | 建設費 | 維持費 | 価格 | サイズ |
|---|---|---|---|---|---|---|---|---|---|---|---|
| `HW_HORROR_HOUSE` | ダークネスホラーハウス | ShowAttraction | HalloweenWorld | 6.5 | 0.08 | 20 | 180s | 9000 | 350 | 400 | 4x5 |
| `WL_WATER_RIDE` | スプラッシュアドベンチャー | RideAttraction | Wonderland | 7.0 | 0.12 | 16 | 120s | 11000 | 450 | 450 | 5x6 |
| `FC_4D_THEATER` | ネクストディメンション4D | ShowAttraction | FutureCity | 6.0 | 0.15 | 40 | 150s | 10000 | 400 | 500 | 5x4 |

各アトラクションに2段階のアップグレードパスを定義済み。

## 2. 極端天候イベントシステム

### 新天候タイプ
- **台風 (Typhoon)**: 来場者×0.1、幸福度-5/h、維持費×2.5、評価-10、スポーン間隔×5.0
- **雷雨 (Thunderstorm)**: 来場者×0.3、幸福度-3/h、維持費×2.0、評価-5、スポーン間隔×3.0

### 季節別出現確率
| 季節 | 台風 | 雷雨 |
|---|---|---|
| 春 | 0.02 | 0.07 |
| 夏 | 0.05 | 0.10 |
| 秋 | 0.10 | 0.10 |
| 冬 | 0.00 | 0.02 |

### 視覚エフェクト
- **台風**: 雨パーティクル3倍emission + 横方向velocity(8-15) + 暗い画面オーバーレイ(alpha 0.4) + 濃霧
- **雷雨**: 雨パーティクル + 不定期ホワイトフラッシュ(3-10秒間隔) + 暗めのライティング

### 来場者への影響
- 台風時: 幸福度-15（即時）、パラメータ毎時雨の2.5倍ペナルティ
- 雷雨時: 幸福度-10（即時）、パラメータ毎時雨の1.5倍ペナルティ
- 両方とも悪天候判定に追加（シェルター避難行動トリガー）

## 3. VIPゲスト特別対応システム拡張

### VIPランクシステム
| ランク | 出現率 | 所持金倍率 | 満足閾値 | 報酬金 | 知名度 | チケット |
|---|---|---|---|---|---|---|
| Silver | 60% | 2倍 | 65 | $500 | 2.0 | 1枚 |
| Gold | 30% | 4倍 | 55 | $1000 | 3.0 | 1枚 |
| Platinum | 10% | 6倍 | 45 | $2000 | 5.0 | 2枚 |

### 新VIPリクエストタイプ
| リクエスト | 達成条件 |
|---|---|
| ExclusiveRide | アトラクション2回以上搭乗 + 幸福度70以上 |
| PhotoWithMascot | エンターテイナー鑑賞 + ショップ1回以上利用 |
| GourmetExperience | 食事 + 飲料 + お土産の3種ショップ利用 |

## 修正ファイル一覧

| # | ファイル | 変更内容 |
|---|---|---|
| 1 | `Assets/Scripts/Core/GameEnums.cs` | Weather enum に Typhoon, Thunderstorm 追加 |
| 2 | `Assets/Scripts/Attraction/AttractionDatabase.cs` | 3アトラクション登録追加 |
| 3 | `Assets/Resources/Config/attractions.json` | 3 JSONエントリ追加 |
| 4 | `Assets/Scripts/Park/WeatherSystem.cs` | 台風/雷雨の効果・季節確率・表示名 |
| 5 | `Assets/Scripts/Core/WeatherEffectController.cs` | 台風/雷雨の視覚エフェクト+雷フラッシュ |
| 6 | `Assets/Scripts/Visitor/VisitorManager.cs` | スポーン倍率に台風5.0/雷雨3.0追加 |
| 7 | `Assets/Scripts/Visitor/VisitorAI.cs` | 天候別幸福度ペナルティ+悪天候判定拡張 |
| 8 | `Assets/Scripts/Visitor/VisitorParameters.cs` | 台風/雷雨のパラメータ毎時ペナルティ |
| 9 | `Assets/Scripts/Visitor/VIPVisitorSystem.cs` | VIPランク+新リクエスト3種+ランク別報酬 |
| 10 | `Assets/Scripts/Core/RuntimeHUD.cs` | WeatherLabel に台風/雷雨追加 |
| 11 | `Assets/Scripts/UI/WeatherForecastUI.cs` | 天候アイコン TYP/THD 追加 |
| 12 | `Assets/Scripts/UI/HUDController.cs` | 天候名に台風/雷雨追加 |
| 13 | `Assets/Scripts/Core/NotificationSystem.cs` | WeatherName に台風/雷雨追加 |
| 14 | `Assets/Scripts/Core/LocalizationData.cs` | ローカライズ定数+GetWeatherName拡張 |
| 15 | `Assets/Scripts/Core/AudioManager.cs` | 台風/雷雨時も雨音環境音を再生 |
| 16 | `Assets/Scripts/Core/AchievementSystem.cs` | 嵐サバイバル判定に台風/雷雨追加 |
| 17 | `Assets/Scripts/Staff/GardenerStaff.cs` | 台風/雷雨も雨扱いに |
| 18 | `Assets/Scripts/AI/NPCDialogueSystem.cs` | 天候コメント+天候名に台風/雷雨追加 |
| 19 | `Assets/Scripts/AI/PromptTemplates.cs` | AI天候説明テンプレート追加 |
| 20 | `status-report.md` | 本レポート |
