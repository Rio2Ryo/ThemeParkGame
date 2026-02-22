# ThemeParkGame

Theme Park World (Bullfrog, 1999) をベースにした、AI機能搭載のスマートフォン向けテーマパーク経営シミュレーションゲーム。

## ゲーム概要

プレイヤーはテーマパークのオーナーとなり、アトラクションの建設・スタッフの雇用・価格設定・研究開発を通じてパークを発展させます。来場者はAI駆動の行動ロジックを持ち、LLM連携によるリアルタイム会話やSNS口コミシステムなど、現代的なAI機能を搭載しています。

## ゲーム仕様

### テーマゾーン (4種)
- **Lost Kingdom** - ロストキングダム
- **Halloween World** - ハロウィーンワールド
- **Wonderland** - ワンダーランド
- **Space Zone** - スペースゾーン

### 来場者パラメーター
| パラメーター | 説明 |
|---|---|
| Happiness (満足度) | パーク体験による総合満足度 |
| Excitement (興奮度) | アトラクション搭乗による興奮 |
| Nausea (吐き気) | 回転系アトラクションによる吐き気 |
| Hunger (空腹度) | 時間経過で上昇、フードショップで回復 |
| Thirst (喉の渇き) | 時間経過で上昇、ドリンクショップで回復 |
| Toilet (トイレ欲求) | 時間経過で上昇、トイレで回復 |
| Cash (所持金) | アトラクション・ショップで消費 |

### 来場者タイプ
- **Kids** - キッズ: 穏やかなアトラクションを好む
- **Young** - ヤング: スリル系を好む
- **Family** - ファミリー: バランス重視
- **Couple** - カップル
- **Senior** - シニア
- **VIP** - 特別な要求を持つVIP来場者

### スタッフ (5種)
| スタッフ | 役割 |
|---|---|
| Mechanic (メカニック) | アトラクションの修理・定期点検 |
| Cleaner (スイーパー) | パーク内の清掃・トイレ清掃 |
| Entertainer (エンターテイナー) | 来場者を楽しませてHappiness向上 |
| Guard (ガードマン) | フーリガン対応・治安維持 |
| Scientist (サイエンティスト) | 新アトラクション・ショップの研究開発 |

### 収益モデル
- **入場料**: パーク入場時に徴収
- **アトラクション利用料**: 各アトラクションごとに価格設定可能
- **ショップ売上**: フード・ドリンク・お土産の販売

価格設定が来場者の満足度に影響し、高すぎると不満、安すぎると赤字になります。

### AI/LLM機能 (差別化ポイント)
- **NPC会話システム**: 住人視点モードでNPCに話しかけるとLLM (Gemini/GPT/Claude) を使ったリアルタイム会話が可能
- **SNSレピュテーション**: 仮想SNSに来場者が口コミを投稿し、評判スコアが来場者数に影響
- **ダイナミッククエスト**: NPC会話から自動的にクエスト（迷子探し・おすすめ案内等）を生成

### アトラクションシステム
- 6カテゴリ: G系 / 縦回転 / 横回転 / 展望系 / 見せ物系 / 乗り物系
- 乗車サイクル: 乗客待ち → 乗車 → 運転 → 降車
- 故障・事故: メンテナンス不足で故障率上昇、放置すると事故に発展
- アップグレード: 最大3段階、興奮度・定員・故障率が改善

### 天候システム
- 晴れ / 曇り / 雨 / 雪 / 猛暑
- 天候が来場者の行動パターンと来園率に影響

## 技術構成

### プラットフォーム
- Unity (C#)
- ターゲット: iOS / Android

### プロジェクト構造
```
Assets/Scripts/
  Core/       - GameManager, TimeManager, SaveSystem, AudioManager, InputManager, TutorialSystem
  Visitor/    - VisitorAI, VisitorManager, VisitorParameters, VisitorProfile, EmotionBubble
  Staff/      - StaffManager, StaffMember, 各スタッフ種別 (Mechanic/Cleaner/Entertainer/Guard/Scientist)
  Attraction/ - Attraction, FacilityBase, AttractionData, Shop, ResearchManager, FacilityDirt
  Economy/    - EconomyManager, FinancialReport, PricingSystem
  Park/       - ParkManager, ParkRating, WeatherSystem, ScenarioData
  AI/         - AIConversationManager, LLMApiClient, NPCPersonality, PromptTemplates,
                ConversationUI, DynamicQuestSystem, SNSReputationSystem
  UI/         - HUDController, BuildPanelUI, StaffPanelUI, VisitorInfoPanel
```

### LLM統合
3つのプロバイダーに対応:
- **Gemini Flash** (推奨): 低レイテンシ・低コストでリアルタイムNPC会話に最適
- **OpenAI GPT-4o-mini**: 高品質な応答
- **Claude**: Anthropic APIによる応答

## 開発状況

### 実装済み
- [x] コアゲームループ (GameManager, TimeManager)
- [x] 来場者AI行動システム (行動状態マシン, 欲求管理, 感情バブル)
- [x] 来場者パラメーター管理 (Happiness/Excitement/Nausea/Hunger/Thirst/Toilet/Cash)
- [x] スタッフ5種の個別ロジック (巡回, タスク割り当て, ストライキ)
- [x] アトラクション運営 (乗車サイクル, 故障/事故, アップグレード)
- [x] ショップシステム (フード/ドリンク/お土産)
- [x] 経済システム (収支管理, ローン, 財務レポート)
- [x] 研究開発システム (アンロックツリー)
- [x] 天候システム (季節サイクル, 天候イベント)
- [x] パーク評価システム (5カテゴリ認定証)
- [x] シナリオモード (10カ国, 3難易度)
- [x] LLM API統合 (Gemini/OpenAI/Claude 3プロバイダー対応)
- [x] NPC会話UI (チャットバブル, クイックリプライ)
- [x] SNSレピュテーションシステム (口コミ生成, トレンド分析)
- [x] ダイナミッククエスト生成 (会話からのクエスト自動生成)
- [x] セーブ/ロードシステム
- [x] タッチ入力管理 (タップ/ドラッグ/ピンチズーム)
- [x] チュートリアルシステム
- [x] オーディオマネージャー
- [x] 建設パネルUI
- [x] スタッフ管理パネルUI
- [x] 来場者情報パネル
- [x] HUD (資金表示, 時間表示, ゲーム速度制御)

### 今後の課題
- [ ] Unity Sceneの構築・プレハブ作成
- [ ] 3Dモデル・アニメーションアセット
- [ ] オーディオアセット (BGM/SE)
- [ ] ビルドパイプライン (iOS/Android)
- [ ] パフォーマンス最適化 (オブジェクトプール, LOD)
- [ ] ローカライズ (英語対応)
- [ ] 課金システム統合

## 統計
- スクリプト数: 47ファイル
- 総コード行数: 約23,000行

## ライセンス
Private repository
