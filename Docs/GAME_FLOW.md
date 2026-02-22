# ThemeParkGame - Game State Flow

## 1. Game State Machine

```
                          ┌─────────────┐
                          │  MainMenu   │
                          └──────┬──────┘
                                 │
              ┌──────────────────┼──────────────────┐
              ▼                  ▼                   ▼
     ┌────────────────┐  ┌──────────────┐  ┌────────────────┐
     │ ScenarioSelect │  │ Quick Start  │  │ Load Save      │
     └───────┬────────┘  └──────┬───────┘  └───────┬────────┘
             │                  │                   │
             └──────────────────┼───────────────────┘
                                ▼
                       ┌─────────────────┐
              ┌───────►│    Playing      │◄───────┐
              │        │  (GodView)      │        │
              │        └───┬───┬───┬─────┘        │
              │            │   │   │               │
     ┌────────┴──┐    ┌────┘   │   └─────┐   ┌────┴─────┐
     │  Paused   │    ▼        ▼         ▼   │  Paused  │
     │           │ ┌──────┐ ┌────────┐ ┌─────┴────┐     │
     └───────────┘ │Build │ │Resident│ │FirstPerson│     │
                   │Mode  │ │View    │ │Mode       │     │
                   └──┬───┘ └───┬────┘ └────┬─────┘     │
                      │         │            │           │
                      └─────────┴────────────┘           │
                                │                        │
                         (ESC / Back)                     │
                                └────────────────────────┘
```

### State Definitions

| State | Enum | Description |
|-------|------|-------------|
| MainMenu | `GameState.MainMenu` | タイトル画面。新規ゲーム / ロード / シナリオ選択 |
| Playing | `GameState.Playing` | 通常プレイ（経営モード、GodView） |
| Paused | `GameState.Paused` | 一時停止。`Time.timeScale = 0` |
| ScenarioSelect | `GameState.ScenarioSelect` | シナリオ選択画面 |
| BuildMode | `GameState.BuildMode` | 建設モード。グリッド表示、配置プレビュー有効 |
| FirstPersonMode | `GameState.FirstPersonMode` | アトラクション搭乗視点 |
| ResidentMode | `GameState.ResidentMode` | 住人視点（来場者に話しかけられる） |

### State Transitions

| From | To | Trigger | Code Reference |
|------|----|---------|----------------|
| MainMenu | Playing | `GameManager.StartNewGame()` | `GameManager.cs:88` |
| MainMenu | ScenarioSelect | UI button | - |
| ScenarioSelect | Playing | `GameManager.StartScenario()` | `GameManager.cs:107` |
| Playing | Paused | `GameManager.PauseGame()` | `GameManager.cs:168` |
| Paused | Playing | `GameManager.ResumeGame()` | `GameManager.cs:176` |
| Playing | BuildMode | `GameManager.EnterBuildMode()` | `GameManager.cs:186` |
| BuildMode | Playing | `GameManager.ExitBuildMode()` | `GameManager.cs:191` |
| Playing | ResidentView | `GameManager.SwitchViewMode(ResidentView)` | `GameManager.cs:133` |
| Playing | FirstPerson | `GameManager.SwitchViewMode(FirstPerson)` | `GameManager.cs:133` |
| Any ViewMode | GodView | `GameManager.SwitchViewMode(GodView)` | `GameManager.cs:133` |

---

## 2. Game Speed Control

```
 Speed 0          Speed 1          Speed 2          Speed 3
 ┌────────┐       ┌────────┐       ┌────────┐       ┌────────┐
 │ Pause  │ ──►   │ Normal │ ──►   │  x2    │ ──►   │  x3    │
 │ ||     │       │  >     │       │  >>    │       │  >>>   │
 └────────┘       └────────┘       └────────┘       └────────┘
 timeScale=0      timeScale=1      timeScale=2      timeScale=3
```

- `GameManager.SpeedLevel` (0-3) で制御
- HUDController のボタンから切り替え
- Pause中はゲーム内時間が完全停止

---

## 3. Day/Night Cycle & Park Operations

```
 0:00    7:00   8:00                              22:00  23:00  24:00
  │       │      │                                  │      │      │
  ├───────┼──────┼──────────────────────────────────┼──────┼──────┤
  │ Night │ Prep │         Park Open                │Close │ Maint│
  │(sleep)│      │  Visitors spawn & interact       │      │      │
  │       │      │  Economy active                  │      │      │
  └───────┴──────┴──────────────────────────────────┴──────┴──────┘
```

### Daily Timeline

| Time | Event | System |
|------|-------|--------|
| 0:00-7:00 | 夜間 - スタッフ休憩・メンテナンス | `TimeManager` |
| 7:00-8:00 | 準備時間 - 最終チェック | `TimeManager` |
| 8:00 | **開園** - `OnParkOpenTimeReached` / `GameEvents.FireParkOpened()` | `TimeManager.cs:67` |
| 8:00-22:00 | 営業中 - 来場者スポーン・行動・経済活動 | All systems |
| 22:00 | **閉園** - `OnParkCloseTimeReached` / `GameEvents.FireParkClosed()` | `TimeManager.cs:75` |
| 22:00-24:00 | 来場者退出・日次収支計算 | `VisitorManager`, `EconomyManager` |
| 24:00 | **日付更新** - `OnDayChanged` | `TimeManager` |

### Monthly/Yearly Events

| Trigger | Event | Details |
|---------|-------|---------|
| 月初め | `OnMonthChanged` | スタッフ給与支払い、施設メンテ費計上、天候更新 |
| 年初め | `OnYearChanged` / `FireParkYearPassed()` | 年次レポート生成、認定証判定 |

---

## 4. Visitor Lifecycle

```
                   Spawn
                     │
                     ▼
               ┌───────────┐
               │   Enter    │──► OnVisitorEnterPark
               │   Park     │
               └─────┬─────┘
                     │
                     ▼
              ┌──────────────┐
         ┌───►│    Idle      │◄──────────────────────┐
         │    │ (決定待ち)    │                        │
         │    └──┬───┬───┬───┘                        │
         │       │   │   │                            │
         │   ┌───┘   │   └────────┐                   │
         │   ▼       ▼            ▼                   │
    ┌────┴──────┐ ┌──────────┐ ┌───────────┐          │
    │Walk to    │ │Walk to   │ │Walk to    │          │
    │Attraction │ │Shop      │ │Toilet     │          │
    └────┬──────┘ └────┬─────┘ └─────┬─────┘          │
         │             │             │                 │
         ▼             ▼             ▼                 │
    ┌──────────┐  ┌─────────┐  ┌──────────┐           │
    │Queue     │  │Eating/  │  │Using     │           │
    │Waiting   │  │Drinking │  │Toilet    │           │
    └────┬─────┘  └────┬────┘  └────┬─────┘           │
         │             │            │                  │
         ▼             └────────────┴──────────────────┘
    ┌──────────┐
    │Riding    │
    │Attraction│─► (satisfaction eval)
    └────┬─────┘
         │
         ▼
    ┌──────────────┐      ┌─────────────┐
    │ Happiness    │  No  │  Vomiting   │
    │ check        ├─────►│  (nausea)   │
    │ Leave?       │      └──────┬──────┘
    └──┬───────────┘             │
       │ Yes                     │
       ▼                         │
    ┌──────────┐                 │
    │Leaving   │◄────────────────┘
    │Park      │──► OnVisitorLeavePark
    └──────────┘
```

### Visitor Decision Priority (VisitorAI)

1. **トイレ** (toiletRate >= 0.8) - 最優先
2. **嘔吐** (nausea >= 0.8) - 制御不能
3. **空腹/渇き** (hunger/thirst >= 0.7) - 高い方を優先
4. **興奮を求める** - アトラクションへ
5. **休憩/散策** - ベンチ or ランダム移動

### Visitor Happiness Factors

| Factor | Impact | Source |
|--------|--------|--------|
| アトラクション満足度 | +5~+25 | excitementRating, queue wait time |
| 食事/飲み物 | +3~+8 | price vs quality ratio |
| トイレ不足 | -5~-15 per incident | toiletRate threshold exceeded |
| 汚い環境 | -3~-10 | FacilityDirt level |
| 高すぎる価格 | -5~-20 | priceSensitivity vs actual price |
| 待ち時間 | -2~-15 | queue > maxQueueWaitMinutes |
| エンターテイナー | +3~+10 | skill level dependent |
| 天候（悪天候） | -2~-5 | rain/snow without shelter |

### Visitor Leave Conditions

- happiness <= `leaveThreshold` (20)
- 所持金 <= 0
- 全アトラクション乗車済み & 満足
- 閉園時間

---

## 5. Staff Lifecycle

```
    Hire (StaffPanelUI)
         │
         ▼
    ┌───────────┐
    │   Idle    │──► 待機中（担当タスクなし）
    └─────┬─────┘
          │ タスク検出
          ▼
    ┌───────────────┐
    │ MovingToTask  │──► NavMeshAgent で目標へ移動
    └───────┬───────┘
            │ 到着
            ▼
    ┌───────────────┐
    │   Working     │──► タスク実行中（疲労蓄積）
    └───────┬───────┘
            │ 完了 or 疲労高
            ▼
    ┌───────────────┐                    ┌────────────┐
    │ Fatigue check │──► fatigue >= 90 ──►│ OnStrike   │
    │               │                    │ (8h停止)   │
    └───────┬───────┘                    └────────────┘
            │ fatigue >= 70
            ▼
    ┌───────────────┐
    │   Resting     │──► StaffRoom で回復
    └───────┬───────┘
            │ fatigue <= 20
            ▼
         [Idle へ戻る]
```

### Staff Types & Responsibilities

| Type | Primary Task | Patrol Behavior |
|------|-------------|-----------------|
| Mechanic | アトラクション修理・点検 | 担当エリアのアトラクション巡回 |
| Cleaner | 清掃・嘔吐物処理 | 担当エリア巡回、汚れ検出で急行 |
| Entertainer | パフォーマンス | 待ち列付近で演技、来場者happiness+3~10 |
| Guard | 治安維持・迷子案内 | 広範囲巡回、破壊行為抑止 |
| Scientist | 研究開発 | 研究所に常駐 |

### Fatigue System

```
 0%                    70%              90%        100%
  ├─────────────────────┼────────────────┼──────────┤
  │    Normal Work      │   Warning      │ Strike!  │
  │                     │  (bubble表示)   │ (8h停止) │
  └─────────────────────┴────────────────┴──────────┘
       ← Rest recovers ─
```

- 作業中: +0.08/秒 (スキルで軽減)
- 待機中: +0.02/秒
- 休憩中: -0.3/秒 (StaffRoom)
- ストライキ: パーク評価 -5、8時間作業不能

---

## 6. Economy Flow

```
                         ┌─────────────────────────┐
                         │     Economy Manager      │
                         └────┬───────────────┬─────┘
                              │               │
                     ┌────────┴───┐     ┌─────┴────────┐
                     │  Revenue   │     │  Expenses     │
                     └────────────┘     └──────────────┘
                           │                    │
         ┌─────────────────┼────────────┐       │
         │                 │            │       │
    ┌────┴─────┐   ┌──────┴───┐  ┌─────┴──┐   │
    │Entrance  │   │Attraction│  │ Shop   │   │
    │Fee       │   │Tickets   │  │Sales   │   │
    │(per head)│   │(per ride)│  │        │   │
    └──────────┘   └──────────┘  └────────┘   │
                                              │
         ┌────────────────────────────────────┼──────────┐
         │                │                   │          │
    ┌────┴─────┐   ┌──────┴───┐  ┌───────────┴┐  ┌─────┴──────┐
    │Staff     │   │Maintenan.│  │ Build     │  │Loan       │
    │Salary    │   │Cost      │  │ Cost      │  │Interest   │
    │(monthly) │   │(monthly) │  │ (one-time)│  │(monthly)  │
    └──────────┘   └──────────┘  └───────────┘  └───────────┘
```

### Revenue Sources

| Source | Frequency | Calculation |
|--------|-----------|-------------|
| 入場料 | 来場者ごと | `entranceFee × visitorCount` |
| アトラクション料 | 乗車ごと | `ticketPrice × ridersPerCycle` |
| ショップ売上 | 購入ごと | `sellingPrice - wholesalePrice` |

### Expense Types

| Expense | Frequency | Calculation |
|---------|-----------|-------------|
| スタッフ給与 | 月次 | `baseSalary × staffCount` per type |
| メンテナンス費 | 月次 | `maintenanceCostPerMonth` per facility |
| パーク固定費 | 月次 | `parkBaseRunningCost` |
| 建設費 | 一回 | `buildCost` per facility |
| アップグレード費 | 一回 | `buildCost × level2/3CostMultiplier` |
| 研究費 | 一回 | per research item |
| ローン利息 | 月次 | `loanAmount × monthlyLoanInterestRate` |
| 修理費 | 発生時 | `repairCostPerConditionPoint × damageAmount` |

### Bankruptcy Flow

```
 Current Money
      │
      │ < 0 (赤字)
      ▼
 ┌──────────────┐    自動借入
 │ Loan offered │───────────────► 継続
 └──────┬───────┘
        │ 借入上限到達
        ▼
 ┌──────────────────┐
 │ Warning: -30,000 │──► 警告表示
 └──────┬───────────┘
        │ < -50,000
        ▼
 ┌──────────────┐
 │  Bankruptcy  │──► ゲームオーバー
 └──────────────┘
```

---

## 7. Research & Unlock Flow

```
 ┌────────────┐     ┌──────────────┐     ┌─────────────┐
 │  Locked    │────►│  Available   │────►│ InProgress  │
 │            │     │ (条件充足)    │     │ (研究中)     │
 └────────────┘     └──────────────┘     └──────┬──────┘
                                                │ 完了
                                                ▼
                                         ┌─────────────┐
                                         │  Completed  │
                                         │ (建設可能)   │
                                         └─────────────┘
```

### Research Requirements

| State | Condition |
|-------|-----------|
| Locked → Available | 前提研究が完了 + 必要なゾーンが解放済み |
| Available → InProgress | サイエンティスト配置 + 研究所建設済み + 研究費支払い |
| InProgress → Completed | 必要日数経過（スキルレベルで短縮） |

### Zone Unlock Flow

```
 LostKingdom (初期解放)
      │
      │ ¥80,000 + パーク評価 >= 35
      ▼
 HalloweenWorld
      │
      │ ¥120,000 + パーク評価 >= 50
      ▼
 Wonderland
      │
      │ ¥200,000 + パーク評価 >= 65
      ▼
 SpaceZone
```

---

## 8. Attraction Lifecycle

```
                Build
                  │
                  ▼
           ┌──────────────┐
           │Construction  │──► (upgradeConstructionDays)
           └──────┬───────┘
                  │ 完了
                  ▼
         ┌────────────────────┐
    ┌───►│WaitingForRiders    │◄───────────────────┐
    │    └────────┬───────────┘                     │
    │             │ 乗客充足 or タイムアウト          │
    │             ▼                                 │
    │    ┌────────────────┐                         │
    │    │   Loading      │──► 乗車処理              │
    │    └────────┬───────┘                         │
    │             │                                 │
    │             ▼                                 │
    │    ┌────────────────┐                         │
    │    │   Running      │──► rideDurationSeconds  │
    │    └────────┬───────┘                         │
    │             │                                 │
    │             ▼                                 │
    │    ┌────────────────┐                         │
    │    │  Unloading     │──► 降車 + 満足度計算     │
    │    └────────┬───────┘                         │
    │             │                                 │
    │             └─────────────────────────────────┘
    │
    │    ┌────────────────┐
    │    │  BrokenDown    │──► メカニック待ち
    │    └────────┬───────┘
    │             │ 修理完了
    │             │
    └─────────────┘

              (放置)
                │
                ▼
         ┌──────────────┐
         │   Accident   │──► parkRating -15, reputation -20
         └──────────────┘
```

### Condition Degradation

```
 100%                          50%                30%        0%
  ├────────────────────────────┼──────────────────┼──────────┤
  │        Normal Operation    │  Warning         │ Critical │
  │  breakdownChance: base     │  chance x1.5     │ chance x3│
  │                            │                  │ accident │
  └────────────────────────────┴──────────────────┴──────────┘
       -0.08/ride, -0.3/day
```

---

## 9. Weather System Flow

```
 月初め: monthly_weather_probabilities で基本確率決定
            │
            ▼
 4時間ごと: weather_transition_rules で遷移判定
            │
            ▼
 天候変更 ──► GameEvents.FireWeatherChanged()
            │
            ├──► visitor spawn rate 変更
            ├──► outdoor/indoor popularity 変更
            ├──► thirst rate 変更 (HeatWave)
            └──► breakdown multiplier 変更 (Rainy)
```

---

## 10. Event System (GameEvents)

### Event Flow Diagram

```
 ┌──────────────┐     ┌──────────────┐     ┌──────────────┐
 │  Source      │     │  GameEvents  │     │  Listeners   │
 │  System      │────►│  (static)    │────►│  (各UI/System)│
 └──────────────┘     └──────────────┘     └──────────────┘

 Examples:
 VisitorAI ──Fire──► OnVisitorEnterPark ──► HUDController (count update)
                                        ──► EconomyManager (entrance fee)
                                        ──► SNSReputationSystem

 Attraction ──Fire──► OnAttractionBrokenDown ──► HUDController (notification)
                                              ──► StaffManager (mechanic dispatch)
                                              ──► ParkRating (safety penalty)
```

### Key Event Chains

| Trigger | Event | Subscribers |
|---------|-------|-------------|
| 来場者入園 | `OnVisitorEnterPark` | HUD, Economy, SNS |
| 来場者退園 | `OnVisitorLeavePark` | HUD, SNS (review post) |
| アトラクション故障 | `OnAttractionBrokenDown` | HUD, StaffManager, ParkRating |
| スタッフストライキ | `OnStaffWentOnStrike` | HUD, ParkRating |
| 天候変化 | `OnWeatherChanged` | HUD, VisitorManager, ParkRating |
| 研究完了 | `OnResearchCompleted` | HUD, BuildPanelUI (unlock item) |
| ゾーン解放 | `OnThemeZoneUnlocked` | HUD, BuildPanelUI, VisitorManager |
| 資金変動 | `OnMoneyChanged` | HUD |
| 視点切替 | `OnViewModeChanged` | Camera, HUD, InputManager |
| AI会話開始 | `OnNPCConversationStarted` | ConversationUI, DynamicQuestSystem |

---

## 11. View Mode Flow

```
 ┌──────────────────────────────────────────────────────────────┐
 │                     GodView (経営モード)                      │
 │  ・俯瞰カメラ（パン・ズーム・回転）                              │
 │  ・施設配置・スタッフ管理・経営判断                               │
 │  ・HUD全表示                                                 │
 └───────┬──────────────────────────────────┬───────────────────┘
         │ タップ来場者 + "住人視点"          │ タップアトラクション
         ▼                                 │ + "搭乗"
 ┌───────────────────────┐                  ▼
 │  ResidentView         │          ┌───────────────────────┐
 │  (住人視点モード)       │          │  FirstPersonMode      │
 │  ・三人称追従カメラ     │          │  (一人称視点)          │
 │  ・来場者に話しかける   │          │  ・アトラクション視点   │
 │  ・AI会話 + クエスト    │          │  ・乗車体験           │
 │  ・散策自由            │          │  ・自動復帰           │
 └───────────────────────┘          └───────────────────────┘
```

---

## 12. Save/Load Flow

```
 Save Trigger
 (手動 or オートセーブ)
        │
        ▼
 ┌──────────────┐
 │ SaveSystem   │──► 全Manager状態をシリアライズ
 │ .SaveGame()  │    ├── TimeManager (日時)
 └──────┬───────┘    ├── EconomyManager (資金・ローン)
        │            ├── ParkManager (施設・ゾーン)
        ▼            ├── VisitorManager (来場者)
 ┌──────────────┐    ├── StaffManager (スタッフ)
 │ JSON File    │    ├── ResearchManager (研究)
 │ (persistent) │    ├── WeatherSystem (天候)
 └──────────────┘    └── SNSReputationSystem (評判)
        │
        ▼
 GameEvents.FireGameSaved()

 Load Trigger
        │
        ▼
 ┌──────────────┐
 │ SaveSystem   │──► JSON読み込み → 各Managerへ復元
 │ .LoadGame()  │
 └──────┬───────┘
        │
        ▼
 GameEvents.FireGameLoaded()
```

---

## 13. Tutorial Flow

```
 Step 1: 通路を敷こう
    │ (5タイル配置)
    ▼
 Step 2: アトラクション建設
    │ (1つ建設)
    ▼
 Step 3: ショップ建設
    │ (1つ建設)
    ▼
 Step 4: トイレ建設
    │ (1つ配置)
    ▼
 Step 5: スタッフ雇用
    │ (2人雇用)
    ▼
 Step 6: 開園！
    │ (速度1x設定)
    ▼
 Step 7: 来場者観察
    │ (3人タップ)
    ▼
 Step 8: 2つ目のアトラクション
    │ (1つ建設)
    ▼
 Tutorial Complete → 報酬 + フリープレイへ
```

---

## 14. Scenario Completion Flow

```
 シナリオ開始
      │
      ▼
 ┌──────────────────────┐
 │  Objectives Monitor  │──► 各目標の進捗を毎日チェック
 └──────────┬───────────┘
            │
      ┌─────┼──────┐
      │     │      │
      ▼     ▼      ▼
  [目標1] [目標2] [目標N]
      │     │      │
      └─────┼──────┘
            │ 全目標達成
            ▼
 ┌──────────────────────┐
 │  Scenario Complete   │──► 報酬 + ゴールデンチケット
 └──────────────────────┘

            │ 時間切れ or 破産
            ▼
 ┌──────────────────────┐
 │  Scenario Failed     │──► リトライ or メニューへ
 └──────────────────────┘
```
