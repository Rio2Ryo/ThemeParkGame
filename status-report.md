# ThemeParkGame ステータスレポート

## 最終更新: 2026-03-01（PS1仕様ギャップ5機能実装）

---

# PS1「新テーマパーク」グラフィック改善 (3エージェント並列実装)

## 実装完了項目

### Agent1: アイソメトリックカメラ ✅
- **GameCameraController.cs** 新規作成
- カメラをOrthographic投影に変更（Perspective廃止）
- 角度を(26.565°, 45°, 0°)に固定 — 完璧な2:1アイソメトリック
- orthographicSize=12でパーク全体を俯瞰
- WASD/矢印キーでパン、マウスホイールでズーム(5〜25)
- FirstPersonCamera復帰時に自動でアイソメトリック設定を復元
- GameBootstrapper/SceneBootstrapper更新済み

### Agent2: PS1風ステータスバー ✅
- **RuntimeHUD_Scoreboard.cs** 全面書き換え
- 旧スコアボード(740x80 入場者/収益/満足度)を廃止
- 旧InfoBar(740x28 資金/時間/天候/スタッフ/混雑度)を廃止
- 新PS1ステータスバー: 全幅54px、濃紺背景(#102060)
- 3セクション: 総資金(黄ラベル+白数値) | 年度(黄) | 月日(白)
- 速度パネルもPS1バー直下に移動

### Agent3: 来場者デフォルメ + 草地ディザリング ✅
- **ProceduralMeshGenerator.cs** 頭部スケール0.2→0.36(1.8倍)
- 超デフォルメ/チビ体型で「新テーマパーク」の来場者スプライトを再現
- **ProceduralTextureGenerator.cs** GenerateGrass全面書き換え
- 3色パレット(#3A8C3A/#4CA64C/#5AB85A) + 4x4 Bayerディザリング
- FilterMode.Pointでドット感を維持
- タイルスケール12→8でディザパターンを視認可能に

---

# Part A: 次期開発 機能提案一覧

## 現状のシステム概要（120スクリプト / 73,711行 / 10シナリオ / 5ゾーン / 6カテゴリ+24アトラクション）

**既存コアシステム**: GameManager, TimeManager, EconomyManager, ParkManager, WeatherSystem, SaveSystem, ScenarioManager
**来場者**: VisitorAI(欲求駆動FSM), VisitorParameters, VIPVisitorSystem, Hooligan/HooliganManager, EmotionBubble, **RepeaterSystem(リピーター)**, **GroupBehaviorSystem(グループ行動)**
**スタッフ**: 8職種(Mechanic/Cleaner/Entertainer/Guard/Scientist/Doctor/Vendor/Gardener), ストライキシステム, **シフト管理(M4)**
**アトラクション**: 6カテゴリ × 5ゾーン + 新規3種(24基), アップグレードパス, 事故/故障システム, **経年劣化(H6)**, **ファストパス(M3)**, **インタラクティブアトラクション(M8)**
**経済**: PricingSystem, SaleCampaignSystem, LoanInvestmentUI, FinancialReport, **FastPassSystem**, **MarketingSystem**
**AI連携**: LLM会話(Claude/OpenAI/Gemini), NPCDialogue, DynamicQuestSystem, WordOfMouth, SNSReputation
**パレード/デコ**: **ParadeSystem(ナイトパレード)**, **DecorationSystem(季節デコレーション)**, **フォトスポット**
**災害/交通**: **DisasterEventSystem(5種災害)**, **ParkTransportSystem(3路線)**
**対戦/Co-op**: **PvPMatchSystem(2人対戦)**, **CoopScenarioSystem(共同シナリオ3種)**, CoopManager(ポーリング同期), LeaderboardManager
**その他**: RivalParkSystem + **RivalDiplomacySystem(スパイ/提携/買収)**, ParkExpansion(土地購入), ParkEventSystem(4種), ChallengeSystem, AchievementSystem(107件), TutorialSystem(15ステップ), SocialShareSystem, AccessibilitySystem(色覚/テキスト/フラッシュ軽減/ナレーション), **MobileUIOptimizer(レスポンシブUI)**

---

## HIGH優先度（ゲーム体験の根幹を強化）

### H1. 夜間パレード＆ナイトショーシステム ✅ Phase 3完了
**概要**: 夜間(18:00-22:00)にパレードルートを設定し、光と音のナイトパレードを自動開催。専用のパレードフロート（山車）を建設・カスタマイズ可能。
**実装**: ParadeSystem.cs(658行) — フロート5種、18:00-22:00自動開催、来場者自動鑑賞(WatchingParade)、ParkEventSystem連携
**コミット**: `abaa549`

### H2. スタッフスキルツリー＆育成システム ✅ Phase 4完了
**概要**: スタッフに経験値・レベル・スキルツリーを導入。例: メカニック → 「高速修理」「予防保全」分岐, エンターテイナー → 「群衆魅了」「VIP接待」分岐。レベルアップで給与要求も上がるトレードオフ。
**理由**: 8職種のスタッフが存在するが成長要素がなく使い捨て感がある。育成のやりこみ要素はリプレイ性を大きく高める。ストライキシステムとの組み合わせで「ベテラン離反」のドラマも生まれる。
**影響範囲**: StaffMember拡張(XP,Level,Skills) / 各Staff職種クラス / StaffManager / RuntimeStaffPanel / SaveSystem
**工数目安**: 大

### H3. 災害イベントシステム（地震・停電・パンデミック） ✅ Phase 5完了
**概要**: 低確率で発生する大規模災害イベント。地震(アトラクション損傷+来場者パニック)、停電(夜間営業不能+アトラクション停止)、パンデミック(来場者激減+Doctor需要急増)。事前投資（耐震工事・非常電源・衛生設備）で被害軽減可能。
**理由**: 台風/雷雨の天候システムが整ったが、プレイヤーの危機管理を試すイベントがまだ少ない。AccidentEventSystemは個別事故のみで、パーク全体を揺るがすイベントがない。投資判断のジレンマが戦略性を深める。
**影響範囲**: 新規 DisasterSystem.cs / AccidentEventSystem連携 / ParkManager / EconomyManager / VisitorAI(パニック行動) / DoctorStaff拡張
**工数目安**: 大

### H4. カスタムコースター設計ツール ⬜ 未実装（Phase 7予定）
**概要**: GForceカテゴリのアトラクションについて、コースのレイアウト（上昇・降下・ループ・旋回）をプレイヤーがノードベースで設計可能に。設計に応じて興奮度・嘔吐率・安全性が動的に算出。テスト走行プレビュー付き。
**理由**: テーマパーク経営シミュの花形機能。現在は定型アトラクションの配置のみで、プレイヤーの創造性を活かす場がない。RollerCoaster Tycoonシリーズの最大の魅力がこの機能。
**影響範囲**: 新規 CoasterDesigner.cs, CoasterTrack.cs, CoasterPhysics.cs / AttractionDatabase拡張 / RuntimeBuildPanel / ProceduralMeshGenerator連携
**工数目安**: 特大

### H5. リアルタイム対戦モード（パーク経営バトル） ✅ Phase 6完了

**概要**: CoopManager基盤を拡張し、2人のプレイヤーが同一マップ内で隣接するパークをそれぞれ経営して競い合う対戦モード。共通の来場者プール（同じ客を取り合う）、相手パークの価格・評判が自パークに影響、期間終了時の総合スコアで勝敗決定。妨害アクション（広告攻勢で相手の客を奪う、スタッフ引き抜き）あり。
**理由**: CoopManagerにポーリングベースの同期・ルーム管理・アクション送信の基盤がすべて揃っているが、協力モードのみで対戦がない。RivalParkSystemのAI対戦を「対人」に昇格させるだけでゲームの寿命が飛躍的に延びる。LeaderboardManagerとの連動でランキング戦も可能。WebGL環境でもポーリング同期で実現可能。
**影響範囲**: CoopManager拡張(対戦ルーム・勝敗判定) / RivalParkSystem(人間プレイヤー対応) / VisitorManager(共有来場者プール) / LeaderboardManager(対戦ランキング) / 新規 PvPMatchSystem.cs / RuntimeHUD(対戦スコアボード)
**工数目安**: 特大

### H6. アトラクション経年劣化＆メンテナンスサイクルシステム ✅ Phase 2完了
**概要**: アトラクションに「状態(Condition)」パラメータ(0-100%)を導入。稼働時間の累積で自然劣化し、Conditionが下がるほど故障率UP・興奮度DOWN・安全性DOWN。メカニックによる定期点検（軽整備:30分停止/Condition+20）と大規模オーバーホール（半日停止/Condition全回復+寿命延長）の2段階メンテナンスを選択可能。放置するとCondition 20%以下で強制停止→修理費3倍。アトラクションごとに耐久性パラメータ（安価なものほど劣化が速い）を持たせ、アップグレードで耐久性も向上。
**理由**: AccidentEventSystemに事故イベントがあるが「ランダム発生」で戦略性がない。経年劣化の概念を入れることで、メカニック配置・メンテナンス計画・建て替え判断という持続的な経営判断が生まれる。テーマパーク経営シムの中核メカニクスの一つ（Planet Coaster/OpenRCT2が実装済み）。現在のAttraction.csにUpgradeLevel/MaintenanceCostは既にあり、Conditionパラメータの追加で自然に拡張可能。
**影響範囲**: Attraction.cs拡張(Condition,Durability,LastMaintenance) / MechanicStaff拡張(点検/オーバーホール行動) / StaffManager(メンテナンススケジュール) / AccidentEventSystem(Condition依存事故率) / RuntimeBuildPanel(状態表示) / NotificationSystem(劣化アラート)
**工数目安**: 大

---

## MEDIUM優先度（ゲームプレイの幅を拡張）

### M1. 来場者グループ行動システム ✅ Phase 5完了
**概要**: 現在の来場者は全員個人行動。家族(2-5人)やカップル(2人)、学校遠足(10-20人)などのグループ単位で来場し、グループリーダーの意思決定に追従。グループ割引・グループ写真スポットなどの連動。
**理由**: VisitorTypeにFamily/Coupleがあるが行動は全員バラバラで不自然。グループ行動で「家族向けパーク」「デート向けパーク」などのコンセプト経営が意味を持つ。
**影響範囲**: 新規 VisitorGroup.cs / VisitorAI拡張 / VisitorManager拡張 / PricingSystem(グループ割引)
**工数目安**: 中

### M2. 季節デコレーション＆テーマ着せ替え ✅ Phase 3完了
**概要**: パーク全体や個別ゾーンに季節・イベントテーマのデコレーションを適用。春(桜)、夏(ひまわり)、秋(紅葉)、冬(クリスマス)、ハロウィン特別。デコレーション投資で来場者の幸福度・SNS映え度UP。
**理由**: ParkEventSystemに季節イベントがあるが視覚的変化がない。GardenerStaffの園芸とも連携し、ゾーン評価のムードカテゴリを底上げする仕組みに。WeatherEffectControllerのライティング変更基盤を流用可能。
**影響範囲**: 新規 DecorationSystem.cs / ParkEffectsManager拡張 / GardenerStaff連携 / ParkRating(ムード評価) / SNSReputationSystem(映えボーナス)
**工数目安**: 中

### M3. アトラクション連動チケットシステム（ファストパス/フリーパス） ✅ Phase 2完了
**概要**: 通常チケットに加え、ファストパス(特定アトラクション優先搭乗)、フリーパス(全アトラクション乗り放題)、ゾーン限定パスなどのチケット商品を導入。価格設定は自由。行列短縮 → VIP/高所得者の満足度UP。
**理由**: PricingSystemが入場料のみで、テーマパーク特有の多段階チケット設計がない。行列待ち(WaitingInQueue)システムが整っているので、優先搭乗ロジックの追加で実現可能。VIPシステムとの親和性も高い。
**影響範囲**: PricingSystem拡張 / VisitorAI(チケット判定) / Attraction(優先キュー) / VIPVisitorSystem連携 / RuntimeHUD
**工数目安**: 中

### M4. スタッフ配置ゾーン指定＆シフト管理 ✅ Phase 2完了
**概要**: スタッフの担当ゾーンと勤務シフト(朝番/昼番/夜番)を指定可能に。深夜割増給与、連勤によるモチベーション低下、シフト不足時のサービス品質低下。自動最適配置のAIアシスト機能付き。
**理由**: 8職種が存在し5ゾーンがあるが、スタッフの配置戦略がほぼない。ゾーン×シフトの組み合わせで経営判断の奥行きが増す。ストライキシステムとの連動も自然。
**影響範囲**: StaffMember拡張(Zone, Shift) / StaffManager拡張 / RuntimeStaffPanel / TimeManager連携
**工数目安**: 中

### M5. 来場者レビュー＆口コミ詳細化 ✅ Phase 4完了
**概要**: 退園時の来場者がテキストベースの詳細レビューを投稿。「ホラーハウスが最高！でもトイレが少なすぎ…」のようなLLM生成レビュー。レビュー傾向の集計ダッシュボード。WordOfMouthSystemと連携し、良レビューが新規来場者を呼ぶ。
**理由**: WordOfMouthSystem/SNSReputationSystemが存在するがテキスト内容が薄い。LLM連携基盤(Claude/OpenAI/Gemini)が整っているので高品質レビュー生成が実現可能。プレイヤーへのフィードバック情報としても有用。
**影響範囲**: VisitorProfile拡張(Review) / WordOfMouthSystem拡張 / SNSReputationSystem / LLMApiClient連携 / RuntimeHUD_ParkInfo(レビューパネル)
**工数目安**: 中

### M6. ライバルパーク強化（スパイ・提携・買収） ✅ Phase 6完了
**概要**: RivalParkSystemを拡張し、ライバルパークへのスパイ派遣(相手の価格・アトラクション情報入手)、業務提携(共同イベント・相互送客)、最終的な買収(大金で相手パークを統合)を追加。
**理由**: RivalParkSystemは価格競争のみで、プレイヤーが能動的に対抗する手段が少ない。外交要素を加えることで「競争vs協力」の戦略的選択が生まれ、終盤のゲームプレイが豊かに。
**影響範囲**: RivalParkSystem拡張 / StaffMember(スパイ任務) / EconomyManager / ParkEventSystem(共同イベント)
**工数目安**: 中

### M7. AI来場者パーソナリティ深化＆インフルエンサーシステム ✅ Phase 4完了
**概要**: 現在のPersonalityTraits（外向性/冒険心/忍耐力/倹約度/好奇心の5軸）をVisitorAIの行動判断により深く反映させる。さらに新VisitorType「Influencer」を追加し、園内のSNS映えスポットやアトラクション体験をリアルタイムにSNS投稿。投稿がバズると来場者スポーン率にブースト。逆に悪体験投稿は炎上リスク。倹約度の高い来場者は安い代替を探し回る、冒険心の低い来場者はホラーハウスを絶対避けるなど、性格が行動ツリーに直結する。
**理由**: PersonalityTraitsは5軸が定義済みだが、VisitorAIのDecideNextAction()での実利用がごくわずか（アトラクション好みのみ）。LLM会話用のDescribe()は充実しているが、ゲームプレイ上の行動に反映されていないのが惜しい。インフルエンサーはWordOfMouthSystem/SNSReputationSystemとの自然な接続点になり、「SNS戦略」という新しい経営軸を生む。
**影響範囲**: VisitorType追加(Influencer) / VisitorProfile拡張 / VisitorAI(DecideNextAction全面見直し) / SNSReputationSystem(インフルエンサー投稿) / WordOfMouthSystem / PricingSystem(倹約度連動)
**工数目安**: 中〜大

### M8. インタラクティブアトラクション種別（参加型ライド） ✅ Phase 5完了
**概要**: 新AttractionCategory「Interactive」を追加。シューティングライド（ライド中にターゲットを撃ちスコア取得）、脱出ゲーム型（制限時間内に謎解き）、ARトレジャーハント（パーク内を歩き回り宝探し）など、来場者の行動がスコア・報酬に影響するアトラクション群。来場者の好奇心(Curiosity)・冒険心(Adventurousness)特性と連動し、高スコアで幸福度ボーナス。SNSでスコア自慢→バイラル効果。
**理由**: 現在の6カテゴリ（GForce/VerticalRotation/HorizontalRotation/Observation/Show/Ride）はすべて「乗るだけ」の受動型で、来場者が能動的に参加する要素がゼロ。現代のテーマパークでは体験型アトラクションが主流（USJのハリポッター、TDRのバズ・ライトイヤー等）。DynamicQuestSystemのクエスト生成基盤を活用すれば、ARトレジャーハントの実装コストを抑えられる。
**影響範囲**: AttractionCategory追加(Interactive) / AttractionDatabase(3-4種追加) / attractions.json / Attraction.cs(スコアシステム) / VisitorAI(参加行動) / DynamicQuestSystem連携(トレジャーハント) / SNSReputationSystem(スコアシェア)
**工数目安**: 大

### M9. マーケティング＆広告キャンペーンシステム ✅ Phase 4完了
**概要**: パークの集客を能動的に行うマーケティングシステム。TV CM(高コスト・全VisitorType集客ブースト)、Web広告(中コスト・Young/Couple特化)、チラシ配布(低コスト・Family/Kids特化)、インフルエンサー招待(M7連動・SNS拡散倍増)の4チャネル。キャンペーン期間(1日/3日/7日)を選択し、予算投下で来場者スポーン率・特定VisitorType比率を操作。費用対効果はパーク評価とキャンペーン組み合わせで変動（高評価パークのTV CMは効果2倍）。過剰広告は「期待値インフレ」を起こし、実体験とのギャップで満足度ペナルティ。
**理由**: SaleCampaignSystemは値引きキャンペーンのみで「客を呼ぶ」能動的手段がない。来場者スポーンはWeather/ParkEvent/ParkRatingの受動要因のみに依存しており、プレイヤーが戦略的に集客をコントロールできない。テーマパーク経営の重要な柱である「マーケティング」が完全に欠落。EconomyManagerの支出カテゴリにMarketing枠を追加し、FinancialReportのExpenseBreakdownで可視化。
**影響範囲**: 新規 MarketingSystem.cs / SaleCampaignSystem連携 / EconomyManager拡張(Marketing支出) / VisitorManager(キャンペーンスポーンブースト) / FinancialReport拡張 / SNSReputationSystem連携 / RuntimeHUD(キャンペーンパネル)
**工数目安**: 中

### M10. 園内交通システム（モノレール・パークトレイン） ✅ Phase 5完了
**概要**: 大規模パーク向けの内部交通手段。モノレール(高コスト・高速・高定員・駅2-4箇所設置)とパークトレイン(低コスト・低速・8駅まで・景観ルート)の2種。来場者はゾーン間移動時に疲労度(Fatigue)閾値を超えると自動的に乗車を選択。乗車中は疲労度回復＋幸福度微増（車窓パーク観覧効果）。路線はPathwaySystem上にプレイヤーが設定。混雑セグメントを通る路線は利用率が高く、混雑緩和に貢献。建設コスト・維持費はあるが、来場者の滞在時間延長（疲労で早期退園を防ぐ）→収益増のリターンあり。
**理由**: PathwaySystemが混雑度をリアルタイム追跡しているが、混雑緩和手段がゼロ。広大なパーク（ParkExpansionで3段階拡張済み）では端から端への移動で疲労度が限界に達し、まだ体験していないゾーンを諦めて退園する来場者が発生する。VisitorParametersの疲労度ペナルティが厳しいため、交通システムは「疲労対策」として経営上の意味を持つ。FirstPersonCameraで乗車ビューも提供でき没入感もUP。
**影響範囲**: 新規 ParkTransitSystem.cs, TransitRoute.cs / PathwaySystem連携(路線設定) / VisitorAI拡張(乗車判断・疲労回復行動) / VisitorParameters(乗車中の疲労回復) / ParkExpansionSystem連携 / RuntimeBuildPanel(路線建設UI) / FirstPersonCamera(乗車ビュー)
**工数目安**: 大

---

## LOW優先度（ポリッシュ・没入感向上）

### L1. パーク内BGMゾーン別カスタマイズ ✅ Phase 1完了
**概要**: ゾーンごとに異なるBGMを再生。ロストキングダム(冒険オーケストラ)、ハロウィーンワールド(ホラーアンビエント)、ワンダーランド(メルヘンワルツ)等。ProceduralAudioLibraryを活用して動的に生成。
**理由**: AudioManagerが天候切替対応済みだがゾーン別BGMがない。テーマゾーンの没入感が大幅に向上。
**影響範囲**: AudioManager拡張 / ProceduralAudioLibrary連携 / カメラ位置検出
**工数目安**: 小

### L2. 来場者写真撮影スポット ✅ Phase 3完了
**概要**: パーク内にフォトスポット施設を配置可能。来場者が立ち寄って「撮影」するとSNS映え度UP。SocialShareSystemと連携し、人気フォトスポットのスクリーンショットが自動生成。
**理由**: SocialShareSystem(スクリーンショット機能)が存在するがゲームプレイとの連動が薄い。施設タイプの追加は小規模で実現可能。
**影響範囲**: FacilityType追加(PhotoSpot) / VisitorAI(撮影行動) / SNSReputationSystem / SocialShareSystem
**工数目安**: 小

### L3. 実績システム拡充（隠し実績・コレクション要素） ✅ Phase 1完了
**概要**: AchievementSystemに隠し実績(台風を3回乗り越える、Platinumゲスト5人満足など)とコレクション要素(全アトラクション制覇、全ゾーン解放など)を追加。実績報酬としてゴールデンチケットやデコレーション解放。
**理由**: AchievementSystemが存在するがコンテンツ量が不十分。新しく追加した台風/VIPランク/新アトラクションに対応する実績がない。
**影響範囲**: AchievementSystem拡張 / RuntimeHUD(実績通知)
**工数目安**: 小

### L4. チュートリアル拡張（インタラクティブガイド） ✅ Phase 2完了
**概要**: TutorialSystemの既存フレームワーク上に、新機能（台風対策・VIPランク対応・新アトラクション活用法）のステップバイステップガイドを追加。初心者向けの「おすすめパーク設計」テンプレート機能。
**理由**: 新コンテンツが増えるほど初見プレイヤーの学習コストが上がる。TutorialSystem基盤は整っているのでコンテンツ追加のみで実現可能。
**影響範囲**: TutorialSystem(ステップデータ追加) / RuntimeHUD
**工数目安**: 小

### L5. Co-opモード実コンテンツ追加（共同シナリオ） ✅ Phase 6完了
**概要**: CoopManagerの基盤上に、2人プレイ専用シナリオを追加。プレイヤーAがアトラクション担当、プレイヤーBがスタッフ・経済担当のような役割分担型。
**理由**: CoopManagerが基盤のみで実際のマルチプレイコンテンツがない。WebGL環境のため通信制約はあるが、ターンベース的な非同期協力は実現可能。
**影響範囲**: CoopManager拡張 / ScenarioDatabase(共同シナリオ追加) / RuntimeHUD
**工数目安**: 中（基盤はあるが同期ロジックが必要）

### L6. アクセシビリティ設定の充実 ✅ Phase 1完了
**概要**: AccessibilitySystemを拡張し、色覚多様性対応(カラーフィルター切替)、テキストサイズ調整、画面フラッシュ無効化(雷雨エフェクト)、音声読み上げ対応。
**実装**: 色覚フィルター4モード、テキストサイズ3段階、フラッシュ軽減、ナレーションログ(Rキー)
**コミット**: `6de591f`

### L7. モバイルUI最適化＆レスポンシブレイアウト ✅ Phase 6完了
**概要**: InputManagerにタッチ操作基盤があるが、RuntimeHUD(1920x1080基準)のUIレイアウトをモバイル画面に最適化。ボタンサイズ拡大、パネル折りたたみ、ジェスチャー操作（ピンチズーム強化・スワイプメニュー）、縦画面モード対応。CanvasScalerのmatchWidthOrHeightを画面比率で動的切替。
**理由**: WebGL対応でブラウザプレイ可能だが、スマホブラウザでのUIが実質使い物にならない。InputManagerのHandleTouchInput()/HandleSingleTouch()は実装済みで、UIレイアウト調整が主な作業。CanvasScalerの基盤もある。モバイルユーザー獲得はDAU増加に直結。
**影響範囲**: RuntimeHUD全partial(レスポンシブ化) / InputManager(ジェスチャー拡張) / CanvasScaler設定 / RuntimeBuildPanel(タッチ最適化) / RuntimeStaffPanel
**工数目安**: 中

### L8. 来場者リピートシステム＆長期記憶 ✅ Phase 3完了
**概要**: 退園した来場者の体験データ（訪問回数、乗ったアトラクション、満足度履歴、LLM会話サマリー）をVisitorProfileに永続保存。高満足度(80+)の来場者は30-60日後にリピーター(再来園)として登場し、前回の記憶を持つ。リピーターは「前回楽しかったアトラクションにまた乗りたい」「前回食べたフードを再注文」などの再訪行動を取る。リピート回数に応じて「常連ボーナス」（所持金UP・SNS口コミ効果UP）。逆に前回不満だった点が改善されていないと即退園リスク。NPCDialogueSystemの会話サマリー（ConversationSummaryPrompt）を来場者の長期記憶として活用し、再来園時に「前回○○の話をしたのを覚えてる？」のような会話が可能。
**理由**: 現在の来場者は退園後に消滅し二度と戻らない「使い捨て」。パーク改善の成果を実感できる「リピーター増加」という指標がなく、経営の長期的な意味付けが弱い。VisitorProfile/SaveSystem/NPCDialogueのConversationSummaryPromptの基盤はすべて揃っており、永続化レイヤーの追加が主な作業。
**影響範囲**: VisitorProfile拡張(訪問履歴・記憶データ) / VisitorManager(リピーター生成ロジック) / VisitorAI(再訪行動パターン) / SaveSystem(来場者永続データ) / NPCDialogueSystem(記憶参照会話) / WordOfMouthSystem(リピーター口コミ) / RuntimeHUD(リピート率表示)
**工数目安**: 中

### L9. 待ち列エンターテイメント＆キューイング演出 ✅ Phase 1完了
**概要**: アトラクションの待ち列(WaitingInQueue状態)をゲームプレイ要素として強化。①キューエリアテーマ装飾（投資で行列空間をテーマ化→待ち幸福度ペナルティ軽減）、②キューエンターテイナー配置（Entertainerの新任務として行列横で芸を披露→待ち幸福度ペナルティをゼロ近くに）、③推定待ち時間ディスプレイ（待ち時間表示→忍耐力(Patience)の低い来場者が長い列を避けて別のアトラクションへ→混雑自動分散）。投資レベル(なし/基本テーマ化/フル演出)の3段階。
**理由**: VisitorBehaviorState.WaitingInQueueは行列待ちの幸福度ペナルティが常に一定（VisitorParametersで毎時減少）で、プレイヤーが改善する手段がない。Entertainerは広場での芸しかなく、行列整理という現実のテーマパークの重要業務がない。PersonalityTraitsのPatience軸が行列判断に未使用（M7で行動全般に反映予定だが、キュー特化の演出はここで独立）。混雑緩和と満足度維持の両方に効く費用対効果の高い投資となる。
**影響範囲**: Attraction.cs拡張(QueueThemeLevel,QueueEntertainment) / VisitorAI(待ち行動改善・列選択AI) / VisitorParameters(テーマ化による待ちペナルティ係数) / EntertainerStaff拡張(キュー配置行動) / RuntimeBuildPanel(キューアップグレードUI)
**工数目安**: 中

---

## 推奨実装ロードマップ（改訂版 v3）

| フェーズ | 内容 | ステータス |
|---|---|---|
| Phase 1 | L3(実績拡充) + L6(アクセシビリティ) + L1(ゾーンBGM) + L9(待ち列演出) | ✅ **完了** `5c5eec9` `6de591f` |
| Phase 2 | H6(経年劣化) + M3(ファストパス) + M4(シフト管理) + L4(チュートリアル) | ✅ **完了** `553f4c9` |
| Phase 3 | H1(ナイトパレード) + M2(デコレーション) + L2(フォトスポット) + L8(リピーター) | ✅ **完了** `abaa549` |
| Phase 4 | H2(スタッフ育成) + M7(AIパーソナリティ) + M5(レビュー) + M9(マーケティング) | ✅ **完了** |
| Phase 5 | H3(災害イベント) + M8(インタラクティブ) + M1(グループ行動) + M10(園内交通) | ✅ **完了** |
| Phase 6 | M6(ライバル強化) + L5(Co-op) + H5(対戦モード) + L7(モバイルUI) | ✅ **完了** |
| Phase 7 | H4(コースター設計) | ⬜ 未着手 |

### 進捗サマリー（全25件）
- **完了**: 24件 / 25件（96%）
  - HIGH: 5/6完了（H1✅ H2✅ H3✅ H5✅ H6✅ / H4⬜）
  - MEDIUM: 10/10完了（M1✅ M2✅ M3✅ M4✅ M5✅ M6✅ M7✅ M8✅ M9✅ M10✅）
  - LOW: 9/9完了（L1✅ L2✅ L3✅ L4✅ L5✅ L6✅ L7✅ L8✅ L9✅）
- **未実装**: 1件（H4: カスタムコースター設計ツール = Phase 7）

---

# Part B: 直近の実装完了レポート（2026-02-28）

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

---

# Part C: Phase 1 実装完了レポート（2026-02-28）

## Phase 1: L3(実績拡充) + L6(アクセシビリティ) + L1(ゾーン別BGM) + L9(待ち列演出) — 全完了

### L3. 実績拡充 — AchievementSystem 新実績12件追加（計95件 → 107件）

**来場者系 (+4件)**
| ID | タイトル | 条件 |
|---|---|---|
| `peak_200` | 超超満員 | 同時来場者200人以上 |
| `vip_platinum` | プラチナの信頼 | PlatinumランクVIPを満足させた |
| `vip_5_platinum` | プラチナクラブ | PlatinumVIPを5人満足させた |
| `vip_all_ranks` | VIPマスター | 全VIPランク(Silver/Gold/Platinum)を満足 |

**経済系 (+1件)**
| ID | タイトル | 条件 |
|---|---|---|
| `revenue_10m` | テーマパーク王 | 総収益$10,000,000達成 |

**パーク系 (+3件)**
| ID | タイトル | 条件 |
|---|---|---|
| `all_zones_built` | 五大陸制覇 | 全5ゾーンにアトラクション建設 |
| `attraction_all_categories` | カテゴリーマスター | 全6カテゴリのアトラクション建設 |
| `expansion_max` | 大帝国 | パーク拡張を最大まで実行 |

**特殊系 (+4件、うち隠し実績2件)**
| ID | タイトル | 条件 |
|---|---|---|
| `typhoon_survive_3` | 台風サバイバー | 台風を3回乗り越えた |
| `thunderstorm_survive_5` | 雷雨の勇者 | 雷雨を5回乗り越えた |
| `weather_all_types` | 全天候体験 | 全7種類の天候を経験 |
| `hidden_typhoon_happy` | 嵐の中の幸せ | 台風中に平均幸福度80以上(隠し) |
| `hidden_no_breakdown_30` | 完璧なパーク | 30日間故障ゼロ(隠し) |

**VIPVisitorSystem拡張**: ランク別満足トラッキング(`PlatinumVIPsServed`, `HasServedAllRanks`)を追加

### L1. ゾーン別BGM — AudioManager全5ゾーン対応

**新規BGMクリップ (4曲)**
| ゾーン | クリップ名 | 音楽スタイル |
|---|---|---|
| LostKingdom | LostKingdomBGM | 冒険オーケストラ（Dm調マーチ+英雄メロディ） |
| HalloweenWorld | HalloweenWorldBGM | ホラーアンビエント（低音ドローン+ゴースト音+心拍リズム） |
| Wonderland | WonderlandBGM | メルヘンワルツ（3拍子+オルゴール+鈴の音） |
| SpaceZone | SpaceZoneBGM | 宇宙エレクトロニカ（LFOパッド+スターダストアルペジオ） |
| FutureCity | FutureCityBGM | (既存) シンセウェーブ |

**自動切替ロジック**
- `AudioManager.UpdateZoneBGMFromCamera()`: カメラ位置に最も近い施設のゾーンを判定し、BGMを自動切替（2秒間隔チェック）
- `ParkManager.GetZoneAtCameraPosition()`: 配置済み施設との距離から最寄りゾーンを返す新メソッド
- Playing状態でのみゾーンBGM切替が有効

### L9. 待ち列エンターテイメント — キューテーマ化+エンターテイナー配置+待ち時間表示

**Attraction.cs拡張**
| プロパティ | 説明 |
|---|---|
| `QueueThemeLevel` | 0=なし, 1=基本テーマ化(30%軽減), 2=フル演出(70%軽減) |
| `HasQueueEntertainer` | エンターテイナーが付近でパフォーマンス中なら+25%軽減 |
| `QueuePenaltyReduction` | テーマ+エンターテイナーの合算ペナルティ軽減率 |
| `EstimatedWaitTime` | 推定待ち時間（秒）。キュー人数÷定員×(稼働+乗降時間) |
| `TryUpgradeQueueTheme()` | テーマレベルアップグレード（コスト500/1500） |

**VisitorAI拡張**
- `ExecuteQueueWaiting`: テーマ化キューで忍耐限界延長 + 幸福度ペナルティ軽減
- アトラクション選択: `QueueThemeLevel`ボーナス + `EstimatedWaitTime`による忍耐限界フィルタ

**EntertainerStaff拡張**
- パフォーマンス中に近くのアトラクションの`HasQueueEntertainer`フラグを自動設定
- パフォーマンス完了時にフラグをリセット

### L6. アクセシビリティ設定の充実 — AccessibilitySystem 全面拡張

**色覚多様性対応（カラーフィルター切替）**
| モード | フィルター | 説明 |
|---|---|---|
| None | なし | フィルターOFF |
| Protanopia | シアン系(alpha 0.10) | 1型色覚（赤色覚異常）補助 |
| Deuteranopia | マゼンタ系(alpha 0.10) | 2型色覚（緑色覚異常）補助 |
| Tritanopia | アンバー系(alpha 0.08) | 3型色覚（青色覚異常）補助 |

- 専用Canvas(sortingOrder=44)に全画面オーバーレイで色補正
- 切替ボタンで4モードをサイクル

**テキストサイズ3段階**
| レベル | スケール | 対象 |
|---|---|---|
| 通常 | 1.0x | — |
| 大 | 1.3x | fontSize 16以下のText |
| 特大 | 1.6x | fontSize 16以下のText |

- 旧`LargeFont`設定からの自動移行対応

**画面フラッシュ軽減**
- ON時、WeatherEffectController.UpdateThunderFlash()の雷雨ホワイトフラッシュを完全抑制
- 雷雨の暗い画面・雨パーティクル・フォグ等の演出は維持（フラッシュのみ除去）

**ナレーションログ（読み上げ支援）**
- GameEvents連携で主要イベントを自動テキスト化:
  - 天候変化、アトラクション故障/修理、VIP来場、パークイベント開始/終了、研究完了、事故発生
- Rキーでログパネル表示/非表示（最新15件）
- ゲーム内時刻のタイムスタンプ付き

**設定UI拡張**
- 設定項目: 6項目（ハイコントラスト / テキストサイズ / 色覚フィルター / フラッシュ軽減 / ナレーション / キーボード操作）
- 全設定PlayerPrefs永続化
- ショートカット一覧にRキー追加

## Phase 1 修正ファイル一覧

| # | ファイル | 変更内容 |
|---|---|---|
| 1 | `Assets/Scripts/Core/AchievementSystem.cs` | 新実績12件+トラッキング変数+チェックロジック |
| 2 | `Assets/Scripts/Visitor/VIPVisitorSystem.cs` | ランク別満足カウンター+公開プロパティ |
| 3 | `Assets/Scripts/Core/ProceduralAudioLibrary.cs` | ゾーンBGM4曲のプロシージャル生成メソッド |
| 4 | `Assets/Scripts/Core/AudioManager.cs` | ゾーンBGMクリップ+自動切替ロジック |
| 5 | `Assets/Scripts/Park/ParkManager.cs` | GetZoneAtCameraPosition()追加 |
| 6 | `Assets/Scripts/Attraction/Attraction.cs` | QueueThemeLevel+EstimatedWaitTime+アップグレード |
| 7 | `Assets/Scripts/Visitor/VisitorAI.cs` | キューテーマ効果+推定待ち時間フィルタ |
| 8 | `Assets/Scripts/Staff/EntertainerStaff.cs` | キューエンターテイナーフラグ自動管理 |
| 9 | `Assets/Scripts/Core/AccessibilitySystem.cs` | 色覚フィルター+テキスト3段階+フラッシュ軽減+ナレーション |
| 10 | `Assets/Scripts/Core/WeatherEffectController.cs` | フラッシュ軽減チェック追加 |

---

# Part D: Phase 2 実装完了レポート（2026-03-01）

## Phase 2: H6(経年劣化) + M3(ファストパス) + M4(シフト管理) + L4(チュートリアル) — 全完了

### H6. アトラクション経年劣化&メンテナンスサイクル

**コンディションシステム (0-100%)**
| パラメータ | 説明 |
|---|---|
| `Condition` | アトラクションの状態（100%=新品、0%=完全劣化） |
| `ConditionLossPerCycle` | 1運行サイクルごとのコンディション低下量（AttractionDataで設定、デフォルト0.15） |
| `Durability` | 耐久度（AttractionDataで設定、1.0=標準、2.0=高耐久＝劣化半減） |

**コンディション閾値と効果**
| 閾値 | 効果 |
|---|---|
| 100-70% | 正常運行。魅力度ペナルティなし |
| 70-50% | 魅力度が低下し始める（来場者に劣化が見える） |
| 50%以下 | 故障確率が急上昇（50%で×1.0→0%で×4.0） |
| 20%以下 | **強制運行停止**。オーバーホール必須 |

**2段階メンテナンス**
| タイプ | 実行者 | 時間 | 効果 |
|---|---|---|---|
| 定期点検 | メカニック（自動巡回） | 5秒÷効率 | コンディション+20%回復、故障率リセット |
| オーバーホール | メカニック（手動/自動） | 30秒÷効率 | コンディション100%完全回復、強制停止解除 |

**MechanicStaff拡張**
- タスク優先順位: 修理 > オーバーホール(30%以下) > 定期点検
- オーバーホール対象自動検出（パトロールエリア内のコンディション30%以下アトラクション）
- `IsUnderOverhaul`フラグで重複オーバーホール防止

### M3. ファストパス/フリーパスチケットシステム

**チケットタイプ**
| タイプ | 説明 | 効果 | 価格 |
|---|---|---|---|
| FastPass | 特定アトラクション優先搭乗 | 1回限り、ファストパスキューから優先乗車 | アトラクション価格×2.0倍 |
| FreePass | 全アトラクション乗り放題 | 当日有効、チケット料金免除 | $2,000 |
| ZonePass | 特定ゾーン内乗り放題 | 当日有効、ゾーン内チケット免除 | $800 |

**システム機能**
- `FastPassSystem.cs` (新規): チケット購入・保有管理・消費判定・販売統計・UI
- Attraction.cs: `_fastPassQueue`（優先キュー）追加、乗車時にファストパスキュー→通常キューの順で乗車
- 搭乗時にチケット自動消費（FastPass=1回消費、FreePass/ZonePass=消費なし）
- プレイヤーが各チケットのON/OFF・価格を設定可能

### M4. スタッフシフト管理&ゾーン配置

**ShiftType enum追加**
| シフト | 時間帯 | 給与倍率 | 疲労倍率 |
|---|---|---|---|
| Morning | 6:00-14:00 | ×1.0 | ×1.0 |
| Day | 14:00-22:00 | ×1.0 | ×1.0 |
| Night | 22:00-6:00 | ×1.25 (夜間割増) | ×1.2 |
| AllDay | 終日 | ×1.5 (終日割増) | ×1.5 |

**新機能**
- `StaffMember.IsOnDuty`: ゲーム内時刻に基づく勤務時間内判定
- シフト時間外のスタッフは自動的に休息状態に移行
- 連続勤務3日以上で追加疲労ペナルティ(×1.3)
- `StaffManager.AutoOptimizeShifts()`: Morning/Day/Night均等配置の自動最適化
- `StaffMember.AssignedZone`: テーマゾーン別担当割り当て
- `StaffManager.GetOnDutyStaffCount()`: 時間帯別の勤務中スタッフ数取得

### L4. チュートリアル拡張 — TutorialSystem 上級ステップ5件追加

**新チュートリアルステップ**
| ステップ | タイトル | 内容 |
|---|---|---|
| WeatherTips | 天候変化に備えよう | 台風/雷雨/猛暑の影響と室内アトラクションの優位性 |
| MaintenanceTips | アトラクションのメンテナンス | コンディション閾値、点検とオーバーホールの違い |
| TicketSystem | チケットシステム活用 | ファストパス/フリーパス/ゾーンパスの使い分け |
| VIPGuest | VIPゲストをもてなそう | VIPランク別報酬とリクエスト達成のコツ |
| StaffShift | スタッフシフト管理 | シフト時間帯、夜間割増、連続勤務の注意点 |

チュートリアルステップ: 10件 → 15件（+5件）

## Phase 2 修正ファイル一覧

| # | ファイル | 変更内容 |
|---|---|---|
| 1 | `Assets/Scripts/Core/GameEnums.cs` | ShiftType enum追加（Morning/Day/Night/AllDay） |
| 2 | `Assets/Scripts/Attraction/AttractionData.cs` | Durability, ConditionLossPerCycle追加 |
| 3 | `Assets/Scripts/Attraction/Attraction.cs` | コンディションシステム+ファストパスキュー+優先搭乗+料金免除 |
| 4 | `Assets/Scripts/Staff/MechanicStaff.cs` | オーバーホール機能（検出/実行/完了）追加 |
| 5 | `Assets/Scripts/Economy/FastPassSystem.cs` | **新規**: チケット購入/保有/消費/統計/UI管理 |
| 6 | `Assets/Scripts/Staff/StaffMember.cs` | シフト/ゾーン/連続勤務/勤務時間判定追加 |
| 7 | `Assets/Scripts/Staff/StaffManager.cs` | シフト設定/ゾーン設定/自動最適化/勤務中カウント |
| 8 | `Assets/Scripts/Core/TutorialSystem.cs` | 上級チュートリアル5ステップ追加 |
| 9 | `status-report.md` | Phase 2完了レポート追加 |

---

# Part E: Phase 3 実装完了レポート（2026-03-01）

## Phase 3: H1(ナイトパレード) + M2(デコレーション) + L2(フォトスポット) + L8(リピーター) — 全完了

### H1. ナイトパレードシステム

**ParadeSystem.cs (新規: 658行)**

| パラメータ | 値 |
|---|---|
| パレード時間帯 | 18:00〜22:00 |
| パレード速度 | 3.0 m/s |
| 鑑賞範囲 | 半径15m |
| 鑑賞時間 | 20秒 |
| 幸福度ボーナス | +10〜20（鑑賞完了時） |
| 興奮度ボーナス | +15〜30（鑑賞完了時） |

**パレードフロート（5種）**
| フロート名 | テーマ | 興奮度 | 幸福度 | 価格 |
|---|---|---|---|---|
| ロイヤルキャッスル号 | LostKingdom | 3 | 5 | $3,000 |
| ゴーストシップ号 | HalloweenWorld | 4 | 4 | $3,500 |
| マジカルドリーム号 | Wonderland | 2 | 6 | $2,500 |
| コスモライナー号 | SpaceZone | 5 | 3 | $4,000 |
| ネオンシティ号 | FutureCity | 4 | 5 | $4,500 |

**システム機能**
- フロート購入・管理（EconomyManager連携）
- パレードルート定義（ルートノード＋停止時間）
- 18:00自動開始 / 22:00自動終了
- 来場者の自動鑑賞行動（WatchingParade状態）
- ParkEventSystemとの連携（night_paradeイベント: 幸福+15, 収益×1.3, 評価+5）
- パレード鑑賞者数カウント・統計

**来場者AI拡張**
- `VisitorBehaviorState.WatchingParade` 追加
- パレード開催中は近くの来場者が自動的に鑑賞
- VisitorType別の鑑賞確率（Kids:60%, Family:50%, その他:30%）
- 鑑賞完了で幸福度+10〜20、興奮度+15〜30

### M2. 季節デコレーションシステム

**DecorationSystem.cs (新規: 566行)**

**季節テーマ（6種）**
| テーマ | 月 | 表示名 |
|---|---|---|
| Spring | 3〜5月 | 春の花 |
| Summer | 6〜8月 | 夏祭り |
| Autumn | 9, 11月 | 秋の紅葉 |
| Winter | 1〜2月 | 冬のイルミネーション |
| Halloween | 10月 | ハロウィン |
| Christmas | 12月 | クリスマス |

**デコレーションレベル（4段階）**
| レベル | コスト | ムード | SNS |
|---|---|---|---|
| None | - | 0 | 0 |
| Basic | $500 | +2 | +1 |
| Enhanced | $1,500 | +5 | +3 |
| Premium | $3,000 | +10 | +5 |

**ゾーン別デコレーション**
- 5ゾーン（LostKingdom/HalloweenWorld/Wonderland/SpaceZone/FutureCity）それぞれに独立設定
- ゾーンごとのアップグレード（EconomyManager連携）
- 園芸師シナジー: 園芸師が作業したゾーンのデコレーション効果+30%

**GardenerStaff連携**
- 園芸作業完了時にDecorationSystem.ApplyGardenerSynergy()呼び出し
- AssignedZone（M4シフト管理）から作業ゾーンを推定

### L2. フォトスポット施設

**GameEnums拡張**
- `FacilityType.PhotoSpot` 追加
- `VisitorBehaviorState.TakingPhoto` 追加

**来場者AI拡張**
- 幸福度55以上で3%の確率でフォトスポットを探す
- 撮影時間: 5秒（無料）
- 撮影完了で幸福度+5〜12

**SNSレピュテーション連携**
- `SNSReputationSystem.ApplyPhotoSpotBoost()` 追加
- フォトスポット撮影は必ずポジティブ投稿を生成
- 投稿テンプレート5種（映えスポット系）
- いいね20-80件、リツイート5-30件（通常投稿より多め）
- パーク評判スコアへの効率的なブースト手段

### L8. 来場者リピートシステム＆長期記憶

**RepeaterSystem.cs (新規: 579行)**

**リピーター登録条件**
| パラメータ | 値 |
|---|---|
| 最低満足度 | 60以上 |
| 最短再来園日 | 退園後30日 |
| 最長再来園日 | 退園後60日 |
| 基本再来園確率 | 15% (per check cycle) |
| 1日最大リピーター | 5人 |

**ロイヤリティシステム**
- 来園ごとに+10ロイヤリティポイント（満足度ボーナス付）
- ロイヤリティが高いほど再来園確率UP（+0.5% per point）
- 高満足度ほど再来園間隔が短い

**リピーターボーナス**
| 項目 | 計算式 |
|---|---|
| 所持金倍率 | 1.0 + 0.1 × min(来園回数, 5) |
| SNS口コミ倍率 | 1.0 + 0.15 × min(来園回数, 5) |
| 来園時幸福度 | +10ボーナス |

**VisitorProfile拡張**
- `GetTopAttractionNames(count)`: 満足度上位N件のアトラクション名取得
- `GetWorstAttractionName()`: 最低満足度アトラクション名取得
- `TotalSpent`: 総支出額トラッキング
- `VisitedAttractionCount`: 体験アトラクション数

**VisitorManager連携**
- `RepeaterSystem.OnRepeaterSpawnRequested`イベント購読
- リピーター自動スポーン（通常スポーンと別枠）
- リピーターデータ（お気に入りアトラクション・所持金ボーナス）を来場者AIに設定

**VisitorAI連携**
- `SetRepeaterData()`: リピーターフラグ設定+お気に入りアトラクション情報
- 退園時に`RepeaterSystem.RecordVisitorDeparture()`で記録

## Phase 3 修正ファイル一覧

| # | ファイル | 変更内容 |
|---|---|---|
| 1 | `Assets/Scripts/Core/GameEnums.cs` | WatchingParade/TakingPhoto/PhotoSpot追加 |
| 2 | `Assets/Scripts/Park/ParadeSystem.cs` | **新規**: ナイトパレード管理（フロート/ルート/統計） |
| 3 | `Assets/Scripts/Park/DecorationSystem.cs` | **新規**: 季節デコレーション管理（ゾーン別/園芸師シナジー） |
| 4 | `Assets/Scripts/Visitor/RepeaterSystem.cs` | **新規**: リピーター管理（記録/スポーン/ボーナス） |
| 5 | `Assets/Scripts/Visitor/VisitorAI.cs` | パレード鑑賞+フォト撮影+リピーター行動 |
| 6 | `Assets/Scripts/Visitor/VisitorProfile.cs` | リピーター用ヘルパー（Top/Worst/TotalSpent） |
| 7 | `Assets/Scripts/Visitor/VisitorManager.cs` | リピータースポーンハンドラ+イベント購読 |
| 8 | `Assets/Scripts/Park/ParkEventSystem.cs` | night_paradeイベントデータ追加 |
| 9 | `Assets/Scripts/Staff/GardenerStaff.cs` | デコレーションシナジー+ゾーン推定 |
| 10 | `Assets/Scripts/AI/SNSReputationSystem.cs` | ApplyPhotoSpotBoost()追加 |
| 11 | `status-report.md` | Phase 3完了レポート追加 |

---

# Part F: Phase 4 実装完了レポート（2026-03-01）

## Phase 4: H2(スタッフ育成) + M7(AIパーソナリティ) + M5(レビュー) + M9(マーケティング) — 全完了

### H2. スタッフスキルツリー＆育成システム

| 項目 | 内容 |
|---|---|
| 新規ファイル | `StaffSkillTreeSystem.cs` |
| スキル総数 | 48個（8職種 × 2分岐 × 3段階） |
| コスト | Tier0: $500, Tier1: $1,200, Tier2: $2,500 |
| 分岐パターン | 各職種に分岐A（攻撃的/速度系）と分岐B（防御的/効率系） |
| 連携 | StaffMember.WorkEfficiencyMultiplier がスキルツリーボーナスを加算 |
| API | CanUnlockSkill, UnlockSkill, HasSkill, GetCombinedBonus |

**職種別スキル例:**
- メカニック: 高速修理系(A) / 予防保全系(B)
- エンターテイナー: 群衆魅了系(A) / VIP接待系(B)
- クリーナー: 高速清掃系(A) / 衛生管理系(B)
- ガードマン: 追跡系(A) / 抑止系(B)
- サイエンティスト: 研究加速系(A) / 応用研究系(B)
- ドクター: 治療系(A) / 予防系(B)
- 販売員: セールス系(A) / サービス系(B)
- 園芸師: 美観系(A) / 効率系(B)

### M7. AI来場者パーソナリティ深化＆インフルエンサーシステム

| 項目 | 内容 |
|---|---|
| 新VisitorType | `Influencer`（SNS拡散型来場者） |
| 出現条件 | 知名度40以上 + 基本1%（マーケティングで増加） |
| 行動特徴 | フォト撮影率15%（通常3%）、パレード鑑賞率70%、お土産購入率×2.5 |
| SNS影響 | 必ず退園時投稿、いいね100-500、リツイート30-150 |
| 修正ファイル | GameEnums, VisitorProfile, VisitorAI, VisitorParameters, VisitorManager, SNSReputationSystem |

### M5. 来場者レビュー＆口コミ詳細化

| 項目 | 内容 |
|---|---|
| 新規ファイル | `VisitorReviewSystem.cs` |
| レビュー確率 | 40%の来場者が退園時にレビュー投稿 |
| 評価方式 | 1-5星（幸福度→星変換） |
| テンプレート | 20種以上の日本語レビューテンプレート（ポジ/ニュートラル/ネガ） |
| ダッシュボード | 平均評価、タグ集計、センチメント分布 |
| SNS連携 | SNSReputationSystem.AddTemplatePostに橋渡し |
| クエリAPI | GetTopTags, GetRecentReviews, GetReviewsByRating |

### M9. マーケティング＆広告キャンペーンシステム

| 項目 | 内容 |
|---|---|
| 新規ファイル | `MarketingSystem.cs` |
| チャネル4種 | TV CM($500/日), Web広告($300/日), チラシ($150/日), インフルエンサー招待($400/日) |
| 期間 | 1日, 3日, 7日（長期ほど倍率減衰） |
| 同時最大 | 3キャンペーン |
| 過剰広告 | 2件超で期待値インフレ（満足度ペナルティ） |
| タイプ特化 | Web→Young×2.0/Couple×1.8、チラシ→Family×2.0/Kids×1.8 |
| パーク評価連携 | 評価70以上でTV CM効果2倍 |

### 変更ファイル一覧

| # | ファイル | 変更内容 |
|---|---|---|
| 1 | `Assets/Scripts/Core/GameEnums.cs` | Influencer, StaffSkillId(48個), MarketingChannel追加 |
| 2 | `Assets/Scripts/Staff/StaffSkillTreeSystem.cs` | **新規**: スキルツリー管理（48スキル、解放・ボーナス計算） |
| 3 | `Assets/Scripts/Visitor/VisitorReviewSystem.cs` | **新規**: レビューシステム（テンプレート20種、ダッシュボード） |
| 4 | `Assets/Scripts/Economy/MarketingSystem.cs` | **新規**: マーケティング（4チャネル、キャンペーン管理） |
| 5 | `Assets/Scripts/Staff/StaffMember.cs` | WorkEfficiencyMultiplierにスキルツリーボーナス連携 |
| 6 | `Assets/Scripts/Visitor/VisitorAI.cs` | Influencer行動（フォト15%/パレード70%/お土産2.5倍/SNS必投稿） |
| 7 | `Assets/Scripts/Visitor/VisitorProfile.cs` | Influencer性格・年齢・グループ・好み追加 |
| 8 | `Assets/Scripts/Visitor/VisitorParameters.cs` | Influencer初期値・倍率追加 |
| 9 | `Assets/Scripts/Visitor/VisitorManager.cs` | Influencerスポーン判定+マーケティング倍率連携 |
| 10 | `Assets/Scripts/AI/SNSReputationSystem.cs` | ApplyInfluencerViralPost()追加 |
| 11 | `status-report.md` | Phase 4完了レポート追加 |

---

# Part G: Phase 5 実装完了レポート（2026-03-01）

## Phase 5: H3(災害イベント) + M8(インタラクティブ) + M1(グループ行動) + M10(園内交通) — 全完了

### H3. 災害イベントシステム

**DisasterEventSystem.cs (新規: ~600行)**

| 災害タイプ | 持続時間 | 来場者影響 | パーク影響 |
|---|---|---|---|
| Earthquake (地震) | 30秒 | パニック、幸福度-20 | アトラクション損傷、修理費増大 |
| PowerOutage (停電) | 60秒 | 幸福度-20 | 全アトラクション停止、照明消失 |
| Pandemic (パンデミック) | 300秒 | 幸福度-20、スポーン激減 | スタッフ欠勤、Doctor需要増 |
| Fire (火災) | 45秒 | パニック避難、幸福度-20 | ゾーン封鎖、緊急対応 |
| Flood (洪水) | 60秒 | 幸福度-20 | 低地ゾーン浸水、屋外停止 |

**3段階フェーズ**: Warning(事前通知) → Active(被害進行) → Recovery(復旧作業)

**VisitorAI連携**:
- 災害Active中 → CheckEmergencyConditions()で即座にEvacuating状態へ遷移
- 幸福度-20の即時ペナルティ
- 避難行動は出口へのナビゲーション（LeavingParkと同じ移動ロジック）

**VisitorManager連携**:
- 災害中のスポーンブロック率をCalculateSpawnIntervalに反映

### M8. インタラクティブアトラクション種別

**InteractiveAttractionSystem.cs (新規: 541行)**

| アトラクション名 | モード | ゾーン | 定員 | 時間 | 価格 |
|---|---|---|---|---|---|
| ダークハント | ShootingRide | HalloweenWorld | 12 | 180s | $350 |
| アドベンチャーナビ | SteeringRide | LostKingdom | 8 | 150s | $400 |
| ストーリーシアター | VoteShow | Wonderland | 30 | 200s | $300 |
| レーシングサンダー | CompetitiveRide | SpaceZone | 8 | 120s | $450 |
| スプラッシュバトル | ShootingRide | Wonderland | 16 | 150s | $350 |
| ミステリーラビリンス | SteeringRide | FutureCity | 6 | 240s | $500 |

**InteractiveMode enum**: ShootingRide / SteeringRide / VoteShow / CompetitiveRide

**スコアシステム**: セッション管理、スコア計算、満足度ボーナス

### M1. 来場者グループ行動システム

**GroupBehaviorSystem.cs (新規: ~530行)**

| グループタイプ | 人数 | リーダー | 行動パターン |
|---|---|---|---|
| Family | 2-5人 | 親1人 | 子供のペースに合わせる、休憩頻度高 |
| Couple | 2人 | ランダム | 同一行動、ロマンチックスポット優先 |
| FriendGroup | 2-6人 | 最外向的 | スリル系優先、分裂可能 |
| SchoolTrip | 10-20人 | 先生1人 | 教育系優先、集団行動厳守 |

**GroupRole enum**: Leader(意思決定者) / Follower(追従) / Child(特別行動)

**機能**:
- リーダー追従による集団移動
- グループ同期意思決定
- グループ分裂・再合流メカニクス

### M10. 園内交通システム

**ParkTransportSystem.cs (新規: 640行)**

| 路線名 | タイプ | 建設費 | 運賃 | 定員 | 幸福度ボーナス |
|---|---|---|---|---|---|
| パークモノレール | Monorail | $20,000 | $50 | 40人 | +8 |
| おさんぽトレイン | ParkTrain | $10,000 | $20 | 60人 | +5 |
| フューチャーシャトル | Shuttle | $8,000 | $30 | 20人 | +3 |

**TransportType enum**: Monorail / ParkTrain / Shuttle

**駅管理**: ゾーン別駅、待ち時間追跡、乗車統計

**VisitorAI連携**:
- 疲労時(幸福度70未満)に8%の確率で園内交通を利用判断
- TransportStation施設タグでナビゲーション
- RidingTransport状態(15秒) → 乗車完了で幸福度+3〜8

**日次処理**: 維持費自動支払い、乗客数・収入レポート

### GameEnums.cs 追加項目

| enum | 追加値 |
|---|---|
| VisitorBehaviorState | RidingTransport, Evacuating |
| FacilityType | TransportStation |
| DisasterType | Earthquake, PowerOutage, Pandemic, Fire, Flood |
| DisasterPhase | Warning, Active, Recovery |
| TransportType | Monorail, ParkTrain, Shuttle |
| InteractiveMode | ShootingRide, SteeringRide, VoteShow, CompetitiveRide |
| GroupRole | Leader, Follower, Child |
| ParkEventType | Disaster |

## Phase 5 修正ファイル一覧

| # | ファイル | 変更内容 |
|---|---|---|
| 1 | `Assets/Scripts/Core/GameEnums.cs` | DisasterType/Phase, TransportType, InteractiveMode, GroupRole, RidingTransport/Evacuating, TransportStation追加 |
| 2 | `Assets/Scripts/Park/DisasterEventSystem.cs` | **新規**: 5種災害イベント管理（3フェーズ、難易度連動） |
| 3 | `Assets/Scripts/Attraction/InteractiveAttractionSystem.cs` | **新規**: 6種インタラクティブアトラクション（スコア、セッション管理） |
| 4 | `Assets/Scripts/Visitor/GroupBehaviorSystem.cs` | **新規**: 4種グループ行動（リーダー追従、分裂、同期意思決定） |
| 5 | `Assets/Scripts/Park/ParkTransportSystem.cs` | **新規**: 3路線園内交通（駅管理、運賃、統計） |
| 6 | `Assets/Scripts/Park/ParkEventSystem.cs` | ParkEventTypeにDisaster追加 |
| 7 | `Assets/Scripts/Visitor/VisitorAI.cs` | RidingTransport/Evacuating状態、災害避難チェック、交通利用判断、施設タグマップ拡張 |
| 8 | `Assets/Scripts/Visitor/VisitorManager.cs` | 災害時スポーンブロック率連携 |
| 9 | `status-report.md` | Phase 5完了レポート追加 |

---

# Part H: Phase 6 実装完了レポート（2026-03-01）

## Phase 6: M6(ライバル強化) + L5(Co-op) + H5(対戦モード) + L7(モバイルUI) — 全完了

### M6. ライバルパーク強化（スパイ・提携・買収）

**RivalDiplomacySystem.cs (新規: 705行)**

| アクション | コスト | 効果 | 条件 |
|---|---|---|---|
| スパイ派遣 | $1,000 | 70%成功でライバル情報5分間開示。失敗で関係Hostile化 | 中立以上 |
| 業務提携 | 無料 | 競争ペナルティ50%減+イベント来場者+10% | 中立、自動/50%受諾 |
| 提携解消 | 無料 | 提携終了、関係Neutral化 | 提携中 |
| 買収 | Rating×$500 | ライバル閉園、知名度+10、ゴールデンチケット | 自スコア>相手 |
| 広告攻勢 | $2,000 | 60秒間ライバルから来場者10%追加奪取 | CD120秒 |

**外交状態**: Neutral / Hostile / Partner / Acquired

**RivalParkSystem連携**: CalculateCompetitionPenaltyで外交修正適用、ForceSpawnRival/GetRivalById追加

### H5. リアルタイム対戦モード

**PvPMatchSystem.cs (新規: 876行)**

| 項目 | 内容 |
|---|---|
| モード | 2人対戦、CoopManager基盤 |
| 試合時間 | 30分（リアルタイム） |
| 初期資金 | $50,000 |
| 共有来場者 | 200人プール |
| 勝利条件 | 加重スコア（収益30% + 評価25% + 来場者25% + 幸福度20%） |

**妨害アクション（最大5回/試合）**

| タイプ | コスト | 持続 | 効果 |
|---|---|---|---|
| 広告攻勢 | $3,000 | 60秒 | 相手来場者15%奪取 |
| スタッフ引抜 | $5,000 | 90秒 | 相手スタッフ効率-20% |
| 価格競争 | $2,000 | 60秒 | 相手満足度-10 |
| イベント横取 | $4,000 | 即時 | 相手アクティブイベント中止 |

**LeaderboardManager連携**: 対戦結果をランキングに自動送信

### L5. Co-opモード実コンテンツ追加

**CoopScenarioSystem.cs (新規: 659行)**

| シナリオ | 難易度 | 時間 | 資金 | 目標 |
|---|---|---|---|---|
| テーマパーク再建 | Easy | 20分 | $30,000 | 来場者50/評価40/収益$10K |
| 嵐を乗り越えろ | Normal | 25分 | $50,000 | 来場者80/評価55/収益$25K |
| 究極のテーマパーク | Hard | 30分 | $80,000 | 来場者150/評価75/収益$100K |

**役割分担**:
- AttractionManager: アトラクション建設/アップグレード/イベント管理
- StaffEconomyManager: スタッフ雇用/経済管理/マーケティング

**CoopManager拡張**: PlayerId/PlayerName公開、IsPvPMode/IsScenarioModeフラグ追加

### L7. モバイルUI最適化

**MobileUIOptimizer.cs (新規: 741行)**

| 機能 | Desktop | Tablet | Mobile |
|---|---|---|---|
| CanvasScaler match | 0.5 | 0.65 | 1.0 |
| ボタンスケール | 1.0x | 1.2x | 1.5x (44dp最小) |
| フォントスケール | 1.0x | 1.15x | 1.3x |
| パネル折りたたみ | 無効 | 無効 | 自動 |
| ハンバーガーメニュー | 非表示 | 非表示 | 表示 |
| セーフエリア対応 | 無効 | 無効 | 有効 |

**スワイプジェスチャー**: 左=次パネル、右=閉じる、上=展開（50px最小、0.3秒以内）

**InputManager拡張**: OnSwipeGesture/OnThreeFingerTapイベント追加

### GameEnums.cs 追加項目

| enum | 追加値 |
|---|---|
| RivalDiplomacyAction | SendSpy, ProposePartnership, BreakPartnership, AttemptAcquisition, LaunchAdBlitz |
| RivalRelationState | Neutral, Hostile, Partner, Acquired |
| PvPMatchState | Lobby, Countdown, InProgress, Finished |
| PvPSabotageType | AdBlitz, StaffPoaching, PriceWar, EventSteal |
| CoopRole | AttractionManager, StaffEconomyManager, FullAccess |
| UILayoutMode | Desktop, Tablet, Mobile |

## Phase 6 修正ファイル一覧

| # | ファイル | 変更内容 |
|---|---|---|
| 1 | `Assets/Scripts/Core/GameEnums.cs` | 6 enum追加（外交/PvP/Co-op/UI） |
| 2 | `Assets/Scripts/Park/RivalDiplomacySystem.cs` | **新規**: スパイ・提携・買収・広告攻勢（外交UI付き） |
| 3 | `Assets/Scripts/Core/PvPMatchSystem.cs` | **新規**: 2人対戦モード（妨害4種、スコアリング、対戦HUD） |
| 4 | `Assets/Scripts/Core/CoopScenarioSystem.cs` | **新規**: 共同シナリオ3種（役割分担、進捗追跡） |
| 5 | `Assets/Scripts/UI/MobileUIOptimizer.cs` | **新規**: レスポンシブUI（3レイアウト、スワイプ、セーフエリア） |
| 6 | `Assets/Scripts/Park/RivalParkSystem.cs` | 外交ペナルティ修正+ForceSpawnRival+GetRivalById |
| 7 | `Assets/Scripts/Core/CoopManager.cs` | PlayerId/Name公開+PvP/Scenarioモードフラグ |
| 8 | `Assets/Scripts/Core/InputManager.cs` | スワイプジェスチャー検出+3本指タップイベント |
| 9 | `Assets/Scripts/Core/RuntimeHUD.cs` | MobileUIOptimizer自動初期化 |
| 10 | `status-report.md` | Phase 6完了レポート追加 |

---

## PS1「新テーマパーク」仕様ギャップ実装 — 5機能 (3エージェント並列実装)

### Feature ①: 入場料高額→来場者ゼロロジック ✅ [HIGH]
- **PricingSystem.cs** `GetEntranceFeeElasticityMultiplier()` 修正
  - 旧: `Mathf.Max(0.1f, ...)` で最低10%（ゼロにならない）
  - 新: 3段階弾力性モデル
    - 推奨以下: 最大1.3倍の集客ボーナス
    - 推奨の1〜3倍: 線形に1.0→0.05へ低下
    - 推奨の3倍超: **完全に来場者ゼロ**（PS1仕様再現）
- **VisitorManager.cs** `CalculateSpawnInterval()` に価格弾力性統合
  - elasticity <= 0 → interval=9999（スポーン完全停止）
  - 低弾力性 → interval増加（来場者減少）

### Feature ②: 広告過剰→清潔評価悪化ペナルティ ✅ [HIGH]
- **MarketingSystem.cs** 新メソッド `GetCleanlinessPenaltyMultiplier()` 追加
  - 同時キャンペーン数 > OverAdvertisingThreshold(2) → 清潔度ペナルティ発動
  - 超過1件あたり15%の清潔度低下（最低30%まで）
  - PS1「新テーマパーク」の「広告やりすぎ→ゴミ評価悪化」仕様再現
- **ParkRatingEvaluator.cs** `CalculateCleanliness()` 修正
  - baseCleanliness × adPenalty で清潔度を補正

### Feature ③: 年次決算UI強化 ✅ [HIGH]
- **RuntimeHUD_Economy.cs** 新パネル `BuildAnnualReportPanel()` (550x600)
  - 年間総収入/支出/純利益/利益率
  - 来場者数・平均支出
  - 収入内訳: 入場料/アトラクション/ショップ/その他（金額+割合%）
  - 支出内訳: 人件費/メンテナンス/研究/建設/ローン/その他
  - 月別利益ASCIIバーチャート（█ブロック文字使用）
  - 前年比成長率（Green=成長/Red=減少）
  - 最優秀月/最低月
  - パーク評価5カテゴリスコア表示
- **RuntimeHUD.cs** Start/OnDestroyに `OnYearChanged += ShowAnnualReport` 購読追加

### Feature ④: バスシステム ✅ [MEDIUM]
- **BusSystem.cs** 新規作成（Singleton MonoBehaviour）
  - バス停配置→来場者スポーンボーナス（PS1仕様再現）
  - 逓減効果モデル: +10%/停 × 0.85^n（最大5停で+35%）
  - 建設費$500、月間維持費$50/停
  - AddBusStop/RemoveBusStop/RestoreFromSave API
- **VisitorManager.cs** `CalculateSpawnInterval()` にバスボーナス統合
  - interval /= busMultiplier でスポーン加速

### Feature ⑤: 消費者団体視察イベント ✅ [MEDIUM]
- **ParkEventSystem.cs** に `ConsumerInspection` イベント追加
  - ParkEventType enum に新タイプ追加
  - 60秒ごとに3%確率で発生（同月1回まで）
  - 視察結果は3段階（パーク総合評価に基づく）:
    - 75以上: 知名度+5ボーナス + 推薦リスト掲載通知
    - 50-74: 改善提案（弱いカテゴリを指摘）
    - 50未満: 知名度ペナルティ（最大-5）+ 営業許可警告

### PS1仕様ギャップ 修正ファイル一覧

| # | ファイル | 変更内容 |
|---|---|---|
| 1 | `Assets/Scripts/Economy/PricingSystem.cs` | 3段階弾力性モデル（3倍超→来場者ゼロ） |
| 2 | `Assets/Scripts/Economy/MarketingSystem.cs` | `GetCleanlinessPenaltyMultiplier()` 新規追加 |
| 3 | `Assets/Scripts/Park/ParkRatingEvaluator.cs` | CalculateCleanliness()に広告ペナルティ統合 |
| 4 | `Assets/Scripts/Visitor/VisitorManager.cs` | 価格弾力性+バスボーナスをSpawnIntervalに統合 |
| 5 | `Assets/Scripts/Core/RuntimeHUD_Economy.cs` | 年次決算パネル新規（550x600、内訳+トレンド+評価） |
| 6 | `Assets/Scripts/Core/RuntimeHUD.cs` | BuildAnnualReportPanel統合+年末イベント購読 |
| 7 | `Assets/Scripts/Park/BusSystem.cs` | **新規**: バスシステム（逓減ボーナス+維持費） |
| 8 | `Assets/Scripts/Park/ParkEventSystem.cs` | 消費者団体視察イベント（3段階評価結果） |
