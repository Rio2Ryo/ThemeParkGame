# ThemeParkGame

Theme Park World (Bullfrog, 1999) をベースにした、AI機能搭載のスマートフォン向けテーマパーク経営シミュレーションゲーム。

## ゲーム概要

プレイヤーはテーマパークのオーナーとなり、アトラクションの建設・スタッフの雇用・価格設定・研究開発を通じてパークを発展させます。来場者はAI駆動の行動ロジック（NavMesh + 状態マシン）を持ち、LLM連携によるリアルタイムNPC会話やSNS口コミシステムなど、現代的なAI機能を搭載しています。

## 目次

- [ゲーム仕様](#ゲーム仕様)
- [システムアーキテクチャ](#システムアーキテクチャ)
- [プロジェクト構造](#プロジェクト構造)
- [スクリプト一覧](#スクリプト一覧)
- [コンフィグデータ](#コンフィグデータ)
- [セットアップ手順](#セットアップ手順)
- [開発状況](#開発状況)

---

## ゲーム仕様

### テーマゾーン (4種)

| ゾーン | 説明 | 特徴 |
|---|---|---|
| **Lost Kingdom** | ロストキングダム | 古代遺跡テーマ、探検系アトラクション |
| **Halloween World** | ハロウィーンワールド | ホラーテーマ、スリル系アトラクション |
| **Wonderland** | ワンダーランド | ファンタジーテーマ、ファミリー向け |
| **Space Zone** | スペースゾーン | 宇宙テーマ、ハイテク系アトラクション |

### 来場者パラメーター

| パラメーター | 範囲 | 説明 |
|---|---|---|
| Happiness (満足度) | 0〜100 | パーク体験による総合満足度 |
| Excitement (興奮度) | 0〜100 | アトラクション搭乗による興奮 |
| Nausea (吐き気) | 0〜100 | 回転系アトラクションによる吐き気 |
| Hunger (空腹度) | 0〜100 | 時間経過で上昇、フードショップで回復 |
| Thirst (喉の渇き) | 0〜100 | 時間経過で上昇、ドリンクショップで回復 |
| Toilet (トイレ欲求) | 0〜100 | 時間経過で上昇、トイレで回復 |
| Cash (所持金) | 通貨 | アトラクション・ショップで消費 |

パラメーターが閾値を超えると行動状態が変化し、来場者が自律的にショップやトイレを探します。

### 来場者タイプ (6種)

| タイプ | 特徴 | 初期所持金 | 好みの強度 |
|---|---|---|---|
| **Kids** | 穏やかなアトラクションを好む | $30〜50 | 低スリル志向 |
| **Young** | スリル系を好む、SNS投稿率が高い | $50〜80 | 高スリル志向 |
| **Family** | バランス重視、子供向け施設を評価 | $80〜120 | バランス型 |
| **Couple** | 雰囲気重視、展望系を好む | $60〜100 | ムード志向 |
| **Senior** | ゆったり系を好む、清潔度に敏感 | $40〜70 | 低刺激志向 |
| **VIP** | 高い要求と所持金、特別扱いを期待 | $150〜300 | 全体的に高水準 |

### スタッフ (5種)

| スタッフ | 役割 | 主な機能 |
|---|---|---|
| **Mechanic** | アトラクション修理・定期点検 | 故障検知 → 移動 → 修理、巡回ルート設定 |
| **Cleaner** | パーク内清掃・トイレ清掃 | ゴミ検知 → 清掃、FacilityDirtレベル管理 |
| **Entertainer** | 来場者を楽しませる | パフォーマンス → Happiness上昇、テーマ衣装 |
| **Guard** | 治安維持・フーリガン対応 | パトロール → 問題検知 → 対処 |
| **Scientist** | 新アトラクション・ショップの研究開発 | 研究所配属 → 研究ポイント蓄積 → アンロック |

全スタッフ共通: 疲労システム（勤務→疲労蓄積→休憩要求→ストライキリスク）、給与支払い、スキルレベル、パトロール経路設定。

### 収益モデル

```
収入 = 入場料 + Σ(アトラクション利用料) + Σ(ショップ売上)
支出 = Σ(スタッフ給与) + メンテナンス費 + 研究費 + ローン返済
```

- **入場料**: パーク入場時に徴収（$5〜$50の範囲で設定可能）
- **アトラクション利用料**: 各アトラクションごとに個別価格設定
- **ショップ売上**: フード・ドリンク・お土産（原価率に基づく利益率）

価格設定が来場者の満足度に影響し、高すぎると不満、安すぎると赤字になります。`PricingSystem`が適正価格のサジェストを提供します。

### AI/LLM機能 (差別化ポイント)

| 機能 | 説明 |
|---|---|
| **NPC会話システム** | 住人視点モードでNPCをタップするとLLM（Gemini/GPT/Claude）を使ったリアルタイム会話が可能。NPCPersonalityに基づく性格・口調の個性化 |
| **SNSレピュテーション** | 仮想SNSに来場者が口コミを投稿。感情分析によるセンチメントスコアが来場者数に影響。トレンドワード検出 |
| **ダイナミッククエスト** | NPC会話から自動的にクエスト（迷子探し・おすすめ案内等）を生成。報酬付きのミニミッション |

### アトラクションシステム

**6カテゴリ:**
- G系 (GForce) - ジェットコースター等
- 縦回転 (VerticalSpin) - フリーフォール等
- 横回転 (HorizontalSpin) - コーヒーカップ等
- 展望系 (Observation) - 観覧車等
- 見せ物系 (ShowRide) - お化け屋敷等
- 乗り物系 (TransportRide) - モノレール等

**乗車サイクル:** 乗客待ち → 乗車 → 運転 → 降車 → メンテナンスチェック

**故障・事故:**
- メンテナンス不足で故障率が上昇
- 故障放置で事故に発展（Happiness大幅低下、来場者退園）
- メカニックによる修理で復旧

**アップグレード:** 最大3段階。興奮度UP・定員UP・故障率DOWN。

### 天候システム

| 天候 | 来場者への影響 |
|---|---|
| 晴れ (Sunny) | 通常の来場率 |
| 曇り (Cloudy) | やや来場率低下 |
| 雨 (Rainy) | 来場率低下、屋内アトラクション人気UP |
| 雪 (Snowy) | 大幅な来場率低下 |
| 猛暑 (HeatWave) | ドリンク消費UP、喉の渇き上昇速度UP |

季節サイクルに連動した天候変化。確率テーブルに基づくランダム遷移。

### シナリオモード

10カ国のシナリオ × 3難易度（Easy / Normal / Hard）:
日本、アメリカ、イギリス、フランス、ドイツ、オーストラリア、ブラジル、エジプト、中国、カナダ

各シナリオに固有の目標（来場者数、評価スコア、利益額）と制約条件。

---

## システムアーキテクチャ

### 全体構成図

```
┌─────────────────────────────────────────────────────────┐
│                      GameManager                         │
│  (ゲームループ統括・状態管理・サブシステム初期化)           │
├──────┬──────┬──────┬──────┬──────┬──────┬───────┬────────┤
│ Time │Visit │Staff │Econo │Park  │Attra │  AI   │  UI    │
│Manag │orMgr │Manag │myMgr │Manag │ction │Conver │Control │
│ er   │      │ er   │      │ er   │/Shop │sation │ lers   │
└──┬───┴──┬───┴──┬───┴──┬───┴──┬───┴──┬───┴───┬───┴───┬────┘
   │      │      │      │      │      │       │       │
   ▼      ▼      ▼      ▼      ▼      ▼       ▼       ▼
 日時  Visitor Staff  収支  Park  施設   LLM   HUD
 管理   AI    AI    管理  Rating 管理  Client  Build
 速度  NavMesh 巡回  ローン Weather研究  NPC    Staff
 制御  パラメ  疲労  財務  シナリオ      Quest  Visitor
       ータ  ストライキ      認定証      SNS    InfoPanel
```

### イベント駆動アーキテクチャ

`GameEvents` クラスを中央イベントバスとして使用。各サブシステムはイベントの発行と購読で疎結合に連携します。

```
主要イベントフロー:

[VisitorAI] --OnVisitorEnterPark--> [VisitorManager] [SNSSystem]
[Attraction] --OnAttractionBrokenDown--> [MechanicStaff] [AudioManager]
[EconomyManager] --OnDayFinancialReport--> [HUDController] [ParkRating]
[WeatherSystem] --OnWeatherChanged--> [VisitorManager] [AudioManager]
[InputManager] --OnObjectTapped--> [VisitorInfoPanel] [BuildPanelUI]
[ResearchManager] --OnResearchCompleted--> [BuildPanelUI] [AudioManager]
```

### セーブ/ロードシステム

- JSON形式でゲーム状態をシリアライズ
- PlayerPrefsを使用したスロット管理（最大3スロット）
- 保存対象: 時間、経済、パーク、研究、天候、スタッフ状態

---

## プロジェクト構造

```
ThemeParkGame/
├── Assets/
│   ├── Scripts/
│   │   ├── Core/           # ゲームの中核システム (27ファイル)
│   │   ├── Visitor/        # 来場者AI・パラメーター (8ファイル)
│   │   ├── Staff/          # スタッフAI・管理 (10ファイル)
│   │   ├── Attraction/     # アトラクション・施設・研究 (12ファイル)
│   │   ├── Economy/        # 経済・財務システム (5ファイル)
│   │   ├── Park/           # パーク管理・天候・シナリオ (10ファイル)
│   │   ├── AI/             # LLM統合・NPC会話・SNS (8ファイル)
│   │   └── UI/             # ユーザーインターフェース (9ファイル)
│   └── Resources/
│       └── Config/         # ゲームバランス設定JSON (8+ファイル)
├── ProjectSettings/
└── README.md
```

---

## スクリプト一覧

### Core/ - コアシステム (27ファイル, 17,533行)

| ファイル | 行数 | 説明 |
|---|---|---|
| `GameManager.cs` | 201 | ゲームループ統括。状態管理（MainMenu/Playing/Paused/BuildMode）、サブシステムの初期化・更新、ゲーム速度制御 |
| `GameEnums.cs` | 215 | 全ゲーム列挙型の定義。GameState, ThemeZone, VisitorType, StaffType, Weather, AttractionCategory, FacilityType等 |
| `GameEvents.cs` | 140 | 中央イベントバス。全サブシステム間のイベント発行・購読を仲介するstaticクラス |
| `TimeManager.cs` | 146 | ゲーム内時間管理。日付・時刻の進行、1x/2x/3x速度制御、日替わり・月替わりイベント発火 |
| `SaveSystem.cs` | 271 | JSON + PlayerPrefsによるセーブ/ロード。3スロット対応、オートセーブ |
| `AudioManager.cs` | 239 | BGM・SE・環境音の一元管理。テーマゾーン別BGM切替、天候連動環境音、イベントSE |
| `InputManager.cs` | 256 | スマホ向けタッチ入力。タップ/ドラッグ/ピンチズーム検出、カメラ制御、オブジェクト選択Raycast |
| `TutorialSystem.cs` | 350 | 12ステップのインタラクティブチュートリアル。UIハイライト、操作ガイド、イベント連動自動進行 |
| `NotificationSystem.cs` | 863 | 通知キュー管理。トースト通知、重要度別フィルタリング、ゲーム内通知一覧 |
| `ProceduralAudioLibrary.cs` | 638 | プロシージャルオーディオ生成。アトラクション効果音・環境音をリアルタイム合成 |
| `RuntimeHUD.cs` | 4,536 | ランタイムHUD統合制御。動的UI生成、パネル管理、通知表示、ゲーム状態連動 |
| `RuntimeGameSetup.cs` | 937 | ランタイムゲーム初期化。シーン構築、マネージャー配置、初期パーク生成 |
| `AchievementSystem.cs` | 1,183 | 実績システム。105バッジ定義、進捗追跡、アンロック判定、報酬付与 |
| `AccidentEventSystem.cs` | 548 | アクシデント・事故システム。故障エスカレーション、事故発生、被害算出、安全対策 |
| `ChallengeSystem.cs` | 573 | チャレンジシステム。デイリー/ウィークリーチャレンジ、目標設定、報酬管理 |
| `ScenarioManager.cs` | 308 | シナリオ進行管理。目標チェック、クリア判定、報酬付与 |
| `GameBootstrapper.cs` | 724 | ゲーム起動シーケンス統括。依存関係解決、サブシステム初期化順序制御 |
| `SceneBootstrapper.cs` | 394 | シーン自動構築。GameManager未存在時の自動初期化、環境オブジェクト生成 |
| `WebGLOptimizer.cs` | 81 | WebGLビルド最適化。メモリ管理、描画バッチング、ブラウザ互換設定 |
| `AccessibilitySystem.cs` | 384 | アクセシビリティ対応。フォントサイズ調整、色覚補助、UI読み上げ支援 |
| `SocialShareSystem.cs` | 283 | ソーシャル共有。スクリーンショット撮影、SNS投稿テンプレート、共有API連携 |
| `AlertMonitor.cs` | 387 | アラート監視。パーク状態の異常検知、緊急通知、自動対処提案 |
| `CloudSaveManager.cs` | 348 | クラウドセーブ。オンラインバックアップ、デバイス間同期、コンフリクト解決 |
| `CoopManager.cs` | 593 | 協力プレイ管理。マルチプレイヤーセッション、パーク共同運営 |
| `FirstPersonCamera.cs` | 397 | 一人称カメラ。来場者視点モード、パーク内ウォークスルー |
| `LeaderboardManager.cs` | 411 | リーダーボード管理。スコアランキング、シナリオ別記録、オンライン順位 |
| `WeatherEffectController.cs` | 629 | 天候エフェクト制御。パーティクル雨/雪、ライティング変化、環境音連動 |

### Visitor/ - 来場者システム (8ファイル, 5,490行)

| ファイル | 行数 | 説明 |
|---|---|---|
| `VisitorAI.cs` | 1,211 | 来場者AI行動制御。NavMeshAgent移動、状態マシン（Idle/Walking/Queueing/Riding/Shopping/LeavePark等）、施設探索・選択ロジック |
| `VisitorManager.cs` | 630 | 来場者のスポーン・プール管理。パーク評判・天候に連動した来場率制御、最大来場者数管理 |
| `VisitorParameters.cs` | 557 | 7パラメーターの統合管理。時間経過による自動変動、閾値判定、イベント通知 |
| `VisitorProfile.cs` | 628 | 来場者の個人情報。名前・年齢・性格特性・訪問目的・体験記憶の管理 |
| `EmotionBubble.cs` | 409 | 来場者の頭上に表示する感情バブルUI。状態・パラメーターに応じたアイコン選択・表示制御 |
| `VIPVisitorSystem.cs` | 351 | VIP来場者管理。特別待遇ロジック、専用キュー、VIP満足度追跡、特別報酬 |
| `VisitorStateMachine.cs` | 339 | 来場者状態マシン。状態遷移ロジックの分離、遷移条件管理、デバッグ支援 |
| `VisitorVisualController.cs` | 365 | 来場者ビジュアル制御。外見バリエーション、アニメーション連動、タイプ別ビジュアル |

### Staff/ - スタッフシステム (10ファイル, 4,246行)

| ファイル | 行数 | 説明 |
|---|---|---|
| `StaffManager.cs` | 794 | スタッフの雇用・解雇・配置・給与支払いの統括。経済システムとの連携 |
| `StaffMember.cs` | 715 | スタッフAI基盤クラス。巡回移動、疲労・士気管理、ストライキ判定、スキルレベル |
| `MechanicStaff.cs` | 356 | メカニック固有ロジック。故障アトラクション検知 → 移動 → 修理プロセス |
| `CleanerStaff.cs` | 392 | スイーパー固有ロジック。ゴミ・汚れ検知 → 清掃、FacilityDirtレベル参照 |
| `EntertainerStaff.cs` | 381 | エンターテイナー固有ロジック。パフォーマンス実行 → 周辺来場者のHappiness上昇 |
| `GuardStaff.cs` | 367 | ガードマン固有ロジック。パトロール → フーリガン検知 → 対処 |
| `ScientistStaff.cs` | 360 | サイエンティスト固有ロジック。研究所配属 → ResearchManagerへの進捗送信 |
| `DoctorStaff.cs` | 139 | ドクター固有ロジック。体調不良の来場者を検知 → 診察 → 回復処理 |
| `GardenerStaff.cs` | 140 | ガーデナー固有ロジック。植栽管理 → パーク美観スコア向上 |
| `VendorStaff.cs` | 164 | ベンダー固有ロジック。移動販売 → 来場者への直接販売、売上管理 |

### Attraction/ - 施設・アトラクション (12ファイル, 4,870行)

| ファイル | 行数 | 説明 |
|---|---|---|
| `Attraction.cs` | 708 | アトラクション運営制御。乗車サイクル（待機→乗車→運転→降車）、故障/事故判定、アップグレード、収益計算 |
| `AttractionData.cs` | 264 | ScriptableObjectによるアトラクションマスターデータ定義。ステータス、コスト、カテゴリ |
| `AttractionDatabase.cs` | 852 | テーマゾーン別・カテゴリ別のアトラクション定義管理。JSONからのデータロード |
| `AttractionManager.cs` | 719 | アトラクションのライフサイクル管理。建設→稼働→故障→修理、一覧管理、統計集計 |
| `FacilityBase.cs` | 251 | 全施設（アトラクション・ショップ・トイレ等）の共通基底クラス。配置、有効/無効、ID管理 |
| `FacilityDirt.cs` | 53 | 施設の汚れレベル追跡。使用による汚れ蓄積、クリーナーによる清掃、時間減衰 |
| `BenchFacility.cs` | 49 | ベンチ施設。来場者の休憩ポイント、疲労回復 |
| `ToiletFacility.cs` | 48 | トイレ施設。来場者のトイレ欲求解消、清掃状態管理 |
| `GenericFacility.cs` | 64 | 汎用施設基底。装飾・案内板等の共通ロジック |
| `ResearchItem.cs` | 208 | 研究ツリーの各ノード定義。前提条件、コスト、研究時間、アンロック対象 |
| `ResearchManager.cs` | 692 | 研究ツリー進行の統括。サイエンティストからの進捗受信、アンロック判定、3ティア管理 |
| `Shop.cs` | 462 | ショップ運営制御。フード/ドリンク/お土産の販売、在庫管理、価格設定、来場者の購入処理 |

### Economy/ - 経済システム (5ファイル, 2,522行)

| ファイル | 行数 | 説明 |
|---|---|---|
| `EconomyManager.cs` | 682 | 収支管理の統括。資金管理、ローンシステム、日次/月次収支計算、支出カテゴリ追跡 |
| `FinancialReport.cs` | 262 | 財務レポートデータ構造。日次・月次・年次の収支サマリー生成 |
| `PricingSystem.cs` | 503 | 価格設定支援。適正価格のサジェスト、来場者満足度への価格影響計算、需要弾力性モデル |
| `SaleCampaignSystem.cs` | 421 | セールキャンペーン管理。期間限定割引、プロモーション効果、来場者数ブースト |
| `LoanInvestmentUI.cs` | 552 | ローン・投資UI。融資申請、返済管理、投資案件表示 |

### Park/ - パーク管理 (10ファイル, 5,387行)

| ファイル | 行数 | 説明 |
|---|---|---|
| `ParkManager.cs` | 754 | パーク全体の状態管理。施設一覧、テーマゾーン管理、開園/閉園制御、総合評価 |
| `ParkRating.cs` | 517 | パーク評価システム。5カテゴリ（Thrill/Beauty/Variety/Value/Cleanliness）、認定証授与 |
| `ParkRatingEvaluator.cs` | 364 | パーク評価詳細計算。各カテゴリのスコアリング、重み付け、改善提案生成 |
| `ParkExpansionSystem.cs` | 551 | パーク拡張システム。土地購入、新エリア開放、拡張コスト計算、段階的アンロック |
| `RivalParkSystem.cs` | 396 | ライバルパークAI。競合パークのシミュレーション、市場シェア競争、差別化戦略 |
| `ParkEventSystem.cs` | 874 | パークイベント管理。季節イベント、特別イベント、ランダムイベント発生・効果 |
| `PathwaySystem.cs` | 665 | 通路システム。パーク内動線管理、来場者フロー最適化、混雑検知 |
| `WeatherSystem.cs` | 501 | 天候シミュレーション。季節サイクル、確率遷移テーブル、天候イベント発火 |
| `ScenarioData.cs` | 213 | シナリオモードのデータ定義。開始条件、目標、制約、報酬 |
| `ScenarioDatabase.cs` | 492 | 10カ国シナリオの管理。JSON読み込み、シナリオ選択UI連携 |

### AI/ - LLM統合・AI会話 (8ファイル, 4,514行)

| ファイル | 行数 | 説明 |
|---|---|---|
| `AIConversationManager.cs` | 436 | AI会話統括マネージャー。LLMクライアント管理、会話セッション制御、PersonalityキャッシュLRU |
| `LLMApiClient.cs` | 759 | LLM APIクライアント。Gemini Flash/OpenAI GPT-4o-mini/Claude対応、リクエスト管理、使用量追跡 |
| `NPCPersonality.cs` | 573 | NPC性格システム。来場者タイプに基づく性格生成、口調・話題・記憶の管理、システムプロンプト生成 |
| `PromptTemplates.cs` | 354 | LLMプロンプトテンプレート集。NPC会話、SNS投稿生成、クエスト生成用のプロンプト定義 |
| `ConversationUI.cs` | 676 | 会話UI制御。チャットバブル表示、プレイヤー入力、クイックリプライ選択肢 |
| `DynamicQuestSystem.cs` | 630 | ダイナミッククエスト生成。NPC会話テキストからのクエスト検出、報酬計算、進捗管理 |
| `SNSReputationSystem.cs` | 658 | 仮想SNSシステム。来場者の口コミ自動生成、センチメント分析、トレンド検出、評判スコア |
| `WordOfMouthSystem.cs` | 370 | 口コミシステム。来場者間の口コミ伝播、評判拡散モデル、来場者獲得への影響 |

### UI/ - ユーザーインターフェース (9ファイル, 6,181行)

| ファイル | 行数 | 説明 |
|---|---|---|
| `HUDController.cs` | 643 | メインHUD制御。資金表示、来場者数、天候アイコン、時間・速度制御、通知ベル、視点切替ボタン |
| `BuildPanelUI.cs` | 872 | 建設パネルUI。カテゴリタブ（アトラクション/ショップ/施設）、配置プレビュー、グリッドスナップ、コスト判定、ResearchManager連携 |
| `StaffPanelUI.cs` | 840 | スタッフ管理パネル。雇用/解雇、一覧表示、パトロールエリア指定、休憩指示、給与設定 |
| `VisitorInfoPanel.cs` | 692 | 来場者情報パネル。パラメーターバー表示、感情状態、所持金、訪問履歴、AI会話ボタン、カメラ追従 |
| `RuntimeBuildPanel.cs` | 1,313 | ランタイム建設パネル。動的UI生成、アンロック状態連動、カテゴリフィルタ |
| `RuntimeStaffPanel.cs` | 588 | ランタイムスタッフパネル。動的スタッフ一覧、リアルタイム状態表示 |
| `SpecialEventUI.cs` | 457 | 特別イベントUI。イベント告知、参加確認、報酬表示、カウントダウン |
| `StageSelectUI.cs` | 525 | ステージ選択UI。シナリオ一覧、難易度選択、クリア状況表示 |
| `WeatherForecastUI.cs` | 231 | 天気予報UI。今後の天候表示、天候アイコン、来場者影響予測 |

### 統計サマリー

| カテゴリ | ファイル数 | 行数 |
|---|---|---|
| Core | 27 | 17,533 |
| Visitor | 8 | 5,490 |
| Staff | 10 | 4,246 |
| Attraction | 12 | 4,870 |
| Economy | 5 | 2,522 |
| Park | 10 | 5,387 |
| AI | 8 | 4,514 |
| UI | 9 | 6,181 |
| **合計** | **89** | **50,743** |

---

## コンフィグデータ

ゲームバランスの設定はすべて `Assets/Resources/Config/` 配下のJSONファイルで管理されます。

| ファイル | 説明 |
|---|---|
| `attractions.json` | 24種のアトラクション定義（4ゾーン×6種、ステータス・コスト・グリッドサイズ） |
| `shops.json` | 16種のショップ定義（4ゾーン×4種: フード・ドリンク・お土産・フード2） |
| `facilities.json` | 16種の施設定義（トイレ・ベンチ・ゴミ箱・案内板・通路・装飾・スタッフルーム・研究所） |
| `visitor_config.json` | 6タイプの来場者定義（初期パラメーター・好み・所持金範囲） |
| `staff_config.json` | 5種のスタッフ定義（給与・スキル・疲労設定） |
| `research_tree.json` | 35項目の研究ツリー（3ティア、前提条件チェーン） |
| `scenarios.json` | 10カ国のシナリオ（3難易度、目標・初期資金・制約） |
| `balance_config.json` | ゲーム全体のバランス設定（経済・天候・メンテナンス・評価・認定証・ゴールデンチケット・時間・アップグレード） |
| `events.json` | ランダムイベント定義（来場者急増・設備故障・VIP来園等） |
| `pricing_presets.json` | 難易度別の推奨価格設定プリセット |
| `initial_park.json` | チュートリアル完了後の初期パーク構成テンプレート |
| `weather_patterns.json` | 月別・季節別の天候確率テーブル |
| `quest_templates.json` | ダイナミッククエストのテンプレート定義 |

### LLM統合

3つのプロバイダーに対応:

| プロバイダー | モデル | 特徴 |
|---|---|---|
| **Gemini Flash** (推奨) | gemini-1.5-flash | 低レイテンシ・低コスト、リアルタイムNPC会話に最適 |
| **OpenAI** | gpt-4o-mini | 高品質な応答、自然な会話 |
| **Claude** | claude-3-haiku | Anthropic API、バランスの良い応答 |

APIキーは `PlayerPrefs` に保存（ビルドに埋め込まない）。使用量トラッキングと予算上限設定に対応。

---

## セットアップ手順

### 必要環境

- **Unity**: 2021.3.35f1 LTS
- **ターゲットプラットフォーム**: iOS / Android
- **LLM API**: Gemini / OpenAI / Claude のいずれかのAPIキー（NPC会話機能使用時）

### プロジェクトを開く

```bash
# リポジトリをクローン
git clone https://github.com/Rio2Ryo/ThemeParkGame.git

# Unity Hubでプロジェクトを追加
# Unity Hub → 「Add」→ ThemeParkGame フォルダを選択
```

### Unity での初期設定

1. **Unity Hub** でプロジェクトを開く
2. **Build Settings** → **Switch Platform** で iOS / Android / WebGL に切り替え
3. **Player Settings** で以下を設定:
   - Company Name / Product Name
   - Bundle Identifier
   - Minimum API Level (Android 7.0+ / iOS 14.0+)

### SampleScene のセットアップ

新しいシーンを作成するか、`Assets/Scenes/SampleScene.unity` を開いて以下のGameObjectを配置します。
`SceneBootstrapper` コンポーネントをシーンに1つ配置しておくと、GameManager が存在しない場合に自動初期化されます。

#### 方法A: SceneBootstrapper による自動構築（推奨）

1. 空のシーンを作成: **File → New Scene → Empty Scene**
2. 空の GameObject を作成: **GameObject → Create Empty** → 名前を `Bootstrapper` に変更
3. `SceneBootstrapper` コンポーネントをアタッチ
4. Play → GameManager・カメラ・ライト・地面が自動生成される

SceneBootstrapper は以下を自動作成します:
- `GameManager` (全サブシステムを含むシングルトン)
- `MainCamera` (God View: 位置 0,30,-20 / 角度 60°)
- `DirectionalLight` (太陽光)
- `Ground` (100×100 の地面メッシュ)
- `ParkEntrance` (パーク出口タグ付き)

#### 方法B: 手動でシーンを構築

以下の階層構造でGameObjectを配置してください:

```
SampleScene
├── --- Managers ---
│   ├── GameManager          ← GameManager.cs をアタッチ
│   │   (自動追加: TimeManager, EconomyManager, ParkManager,
│   │    VisitorManager, StaffManager, ResearchManager,
│   │    WeatherSystem, AIConversationManager, AttractionManager)
│   ├── AudioManager         ← AudioManager.cs をアタッチ
│   └── InputManager         ← InputManager.cs をアタッチ
│
├── --- Environment ---
│   ├── Main Camera          [Camera, AudioListener]
│   ├── Directional Light    [Light]
│   ├── Ground               [MeshRenderer, BoxCollider] Layer:Ground
│   └── ParkEntrance         Tag: "ParkExit"
│
├── --- Park Content ---
│   ├── Attractions          (アトラクション配置の親オブジェクト)
│   ├── Shops                (ショップ配置の親オブジェクト)
│   └── Facilities           (トイレ・ベンチ等の親オブジェクト)
│
├── --- Entities ---
│   ├── Visitors             (来場者インスタンスの親)
│   └── Staff                (スタッフインスタンスの親)
│
├── --- UI ---
│   ├── Canvas               [Canvas, CanvasScaler, GraphicRaycaster]
│   │   ├── HUD              ← HUDController.cs
│   │   ├── BuildPanel       ← BuildPanelUI.cs (初期非表示)
│   │   ├── StaffPanel       ← StaffPanelUI.cs (初期非表示)
│   │   ├── VisitorInfoPanel ← VisitorInfoPanel.cs (初期非表示)
│   │   ├── ConversationUI   ← ConversationUI.cs (初期非表示)
│   │   └── TutorialPanel    ← TutorialSystem.cs
│   └── EventSystem          [EventSystem, StandaloneInputModule]
│
└── --- Navigation ---
    └── NavMeshSurface       [NavMeshSurface] Ground上にBake
```

> **重要**: `GameManager` は `GetOrAddComponent<T>()` により、必要なサブシステムを自動的に同一 GameObject に追加します。手動でサブシステムをアタッチする必要はありません。

### マネージャー配置ガイド

| コンポーネント | 配置先 | 説明 |
|---|---|---|
| `GameManager` | GameManager オブジェクト | シングルトン。全サブシステムを自動追加。DontDestroyOnLoad |
| `AttractionManager` | GameManager に自動追加 | アトラクションの建設→稼働→故障→修理のライフサイクル管理 |
| `StaffManager` | GameManager に自動追加 | スタッフ雇用・配置・給与・疲労管理 |
| `VisitorManager` | GameManager に自動追加 | 来場者スポーン・プール管理。Inspector で `visitorPrefab` を設定 |
| `EconomyManager` | GameManager に自動追加 | 収支管理・ローン・財務レポート |
| `ParkManager` | GameManager に自動追加 | グリッドベース施設配置・ゾーン管理 |
| `AudioManager` | 専用オブジェクト (DontDestroyOnLoad) | BGM/SE/環境音。Inspector で AudioClip を設定 |
| `InputManager` | 専用オブジェクト | タッチ/マウス入力検出・カメラ制御 |

### アトラクション・施設の配置

1. `Attractions` 親オブジェクトの子として空のGameObjectを作成
2. `Attraction` コンポーネントをアタッチ
3. Inspector で `AttractionData` (ScriptableObject) を設定
4. `BoxCollider` を設定（自動追加される）
5. Play → `AttractionManager.RegisterAttraction()` で自動登録

ショップ・トイレ・ベンチも同様に、対応するコンポーネント (`Shop`, `ToiletFacility`, `BenchFacility`) をアタッチして配置します。

### 来場者 (Visitor) プレハブ

`VisitorManager` の Inspector で `visitorPrefab` に来場者プレハブを設定してください。プレハブには以下が必要です:

- `NavMeshAgent` — パス探索用
- `CapsuleCollider` — 当たり判定
- `VisitorAI` — 行動AI (自動で VisitorParameters, VisitorProfile を内包)
- `EmotionBubble` (子オブジェクト) — 感情バブル表示

### NavMesh の設定

1. Ground オブジェクトを選択
2. **Window → AI → Navigation** を開く
3. Ground を **Navigation Static** に設定
4. **Bake** タブで NavMesh をベイク
5. 各施設の `BoxCollider` が **NavMesh Obstacle** として機能

### LLM APIキーの設定

ゲーム内設定画面、または Unity Editor の Inspector から `AIConversationManager` の `apiKey` フィールドにAPIキーを入力。

```
Gemini: https://aistudio.google.com/app/apikey
OpenAI: https://platform.openai.com/api-keys
Claude: https://console.anthropic.com/
```

### ビルド (ローカル)

```bash
# Unity Editor → File → Build Settings
# Platform: iOS / Android / WebGL
# Build → 出力先を選択
```

### WebGL ビルド & GitHub Pages デプロイ (CI/CD)

GitHub Actions（GameCI）による自動ビルド・デプロイが設定されています。
`main` または `master` ブランチへの push で自動的に WebGL ビルドが実行され、GitHub Pages にデプロイされます。

> ワークフローファイルは `.github/workflows/build-webgl.yml` に設定済みです。詳細は [WEBGL_SETUP.md](WEBGL_SETUP.md) を参照。

**公開URL:** https://rio2ryo.github.io/ThemeParkGame/

#### GitHub Secrets の設定 (必須)

リポジトリの **Settings → Secrets and variables → Actions** で以下のシークレットを追加してください:

| シークレット名 | 説明 | 取得方法 |
|---|---|---|
| `UNITY_LICENSE` | Unityライセンスファイル (.ulf) の内容 | 下記手順を参照 |
| `UNITY_EMAIL` | Unity アカウントのメールアドレス | Unity ID に登録したメール |
| `UNITY_PASSWORD` | Unity アカウントのパスワード | Unity ID のパスワード |

#### UNITY_LICENSE の取得手順

1. `UNITY_LICENSE` を未設定のままワークフローを実行する
2. ワークフローが失敗し、Artifacts に `Unity_Activation_File` (.alf) が生成される
3. https://license.unity3d.com/manual にアクセスし、.alf ファイルをアップロード
4. 「Unity Personal」を選択してライセンスファイル (.ulf) をダウンロード
5. .ulf ファイルの内容をすべてコピーし、GitHub Secrets の `UNITY_LICENSE` に貼り付け
6. ワークフローを再実行

詳細は [WEBGL_SETUP.md](WEBGL_SETUP.md) を参照してください。

---

## 開発状況

### 実装済み

- [x] コアゲームループ (GameManager, TimeManager)
- [x] 来場者AI行動システム (状態マシン, NavMesh移動, 欲求管理, 感情バブル)
- [x] 来場者パラメーター管理 (Happiness/Excitement/Nausea/Hunger/Thirst/Toilet/Cash)
- [x] スタッフ5種の個別ロジック (巡回, タスク割り当て, 疲労, ストライキ)
- [x] アトラクション運営 (乗車サイクル, 故障/事故, アップグレード3段階)
- [x] ショップシステム (フード/ドリンク/お土産, 在庫管理)
- [x] 経済システム (収支管理, ローン, 財務レポート, 価格サジェスト)
- [x] 研究開発システム (35項目, 3ティア, 前提条件チェーン)
- [x] 天候システム (季節サイクル, 5天候タイプ, 来場率影響)
- [x] パーク評価システム (5カテゴリ認定証)
- [x] シナリオモード (10カ国, 3難易度)
- [x] LLM API統合 (Gemini/OpenAI/Claude 3プロバイダー対応)
- [x] NPC会話UI (チャットバブル, クイックリプライ, 性格キャッシュ)
- [x] SNSレピュテーションシステム (口コミ生成, センチメント分析, トレンド)
- [x] ダイナミッククエスト生成 (会話からのクエスト自動生成, 報酬)
- [x] セーブ/ロードシステム (JSON, 3スロット)
- [x] タッチ入力管理 (タップ/ドラッグ/ピンチズーム, マウスフォールバック)
- [x] チュートリアルシステム (12ステップ, イベント連動)
- [x] オーディオマネージャー (BGM/SE/環境音, ゾーン別BGM)
- [x] 建設パネルUI (カテゴリタブ, プレビュー, グリッドスナップ)
- [x] スタッフ管理パネルUI (雇用/解雇, パトロール設定)
- [x] 来場者情報パネル (パラメーター表示, AI会話, カメラ追従)
- [x] HUD (資金/来場者/天候/時間/速度制御/通知)
- [x] ゲームバランスJSON設定 (8+設定ファイル)
- [x] VIPシステム (VIP来場者、特別待遇、専用キュー、VIP満足度追跡)
- [x] 実績システム (105バッジ、進捗追跡、アンロック判定、報酬付与)
- [x] チャレンジシステム (デイリー/ウィークリーチャレンジ、目標設定、報酬管理)
- [x] パーク拡張システム (土地購入、新エリア開放、段階的アンロック)
- [x] アクシデント・事故システム (故障エスカレーション、事故発生、被害算出、安全対策)
- [x] 口コミシステム (来場者間の口コミ伝播、評判拡散モデル、来場者獲得への影響)
- [x] ライバルパークシステム (競合パークAI、市場シェア競争、差別化戦略)
- [x] セールキャンペーンシステム (期間限定割引、プロモーション効果、来場者数ブースト)
- [x] ランタイムHUD (動的UI生成、パネル管理、通知表示、ゲーム状態連動)
- [x] プロシージャルオーディオ (アトラクション効果音・環境音のリアルタイム合成)
- [x] 天気予報UI (今後の天候表示、来場者影響予測)
- [x] 特別イベントUI (イベント告知、参加確認、報酬表示、カウントダウン)
- [x] アクセシビリティ対応 (フォントサイズ調整、色覚補助、UI読み上げ支援)
- [x] ソーシャル共有 (スクリーンショット撮影、SNS投稿テンプレート)
- [x] パークイベントシステム (季節イベント、特別イベント、ランダムイベント)
- [x] 通路システム (パーク内動線管理、来場者フロー最適化、混雑検知)
- [x] クラウドセーブ (オンラインバックアップ、デバイス間同期)
- [x] リーダーボード (スコアランキング、シナリオ別記録)
- [x] 一人称カメラ (来場者視点モード、パーク内ウォークスルー)
- [x] ゲームブートストラッパー (起動シーケンス統括、依存関係解決)
- [x] WebGL最適化 (メモリ管理、描画バッチング、ブラウザ互換設定)

### 今後の課題

- [ ] Unity Sceneの構築・プレハブ作成
- [ ] 3Dモデル・アニメーションアセット
- [ ] オーディオアセット (BGM/SE)
- [ ] NavMeshの設定
- [x] WebGL ビルド & GitHub Pages デプロイ (GitHub Actions CI/CD `.github/workflows/build-webgl.yml`)
- [x] VIP・実績・チャレンジシステム
- [x] パーク拡張・ライバルパーク
- [x] アクシデント・事故・口コミシステム
- [x] セールキャンペーン・ランタイムHUD
- [x] アクセシビリティ・ソーシャル共有
- [x] 天気予報UI・特別イベントUI
- [x] プロシージャルオーディオ・WebGL最適化
- [ ] ビルドパイプライン (iOS/Android ネイティブ)
- [ ] パフォーマンス最適化 (WebGLビルドサイズ10MB目標, LOD)
- [ ] ローカライズ (英語対応)
- [ ] 課金システム統合
- [ ] 実機テスト・デバッグ

---

## 統計

- スクリプト数: **89ファイル**
- 総コード行数: **50,743行**
- コンフィグJSON: **13ファイル**
- サポートLLMプロバイダー: **3種** (Gemini/OpenAI/Claude)
- アトラクション種類: **24種** (4ゾーン × 6カテゴリ)
- シナリオ数: **30** (10カ国 × 3難易度)
- 研究ツリー項目: **35項目** (3ティア)
- 実績バッジ: **105種**
- Unity バージョン: **2021.3.35f1 LTS**

## ライセンス

Private repository - All rights reserved.
