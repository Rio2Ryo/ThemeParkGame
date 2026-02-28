# ThemeParkGame ステータスレポート

## 最終更新: 2026-02-28

---

# Part A: 次期開発 機能提案一覧

## 現状のシステム概要（105スクリプト / 10シナリオ / 5ゾーン / 6カテゴリ+21アトラクション）

**既存コアシステム**: GameManager, TimeManager, EconomyManager, ParkManager, WeatherSystem, SaveSystem, ScenarioManager
**来場者**: VisitorAI(欲求駆動FSM), VisitorParameters, VIPVisitorSystem, Hooligan/HooliganManager, EmotionBubble
**スタッフ**: 8職種(Mechanic/Cleaner/Entertainer/Guard/Scientist/Doctor/Vendor/Gardener), ストライキシステム
**アトラクション**: 6カテゴリ × 5ゾーン + 新規3種, アップグレードパス, 事故/故障システム
**経済**: PricingSystem, SaleCampaignSystem, LoanInvestmentUI, FinancialReport
**AI連携**: LLM会話(Claude/OpenAI/Gemini), NPCDialogue, DynamicQuestSystem, WordOfMouth, SNSReputation
**その他**: RivalParkSystem, ParkExpansion(土地購入), ParkEventSystem(4種), ChallengeSystem, AchievementSystem, TutorialSystem, CoopManager, LeaderboardManager, SocialShareSystem, AccessibilitySystem

---

## HIGH優先度（ゲーム体験の根幹を強化）

### H1. 夜間パレード＆ナイトショーシステム
**概要**: 夜間(18:00-22:00)にパレードルートを設定し、光と音のナイトパレードを自動開催。専用のパレードフロート（山車）を建設・カスタマイズ可能。
**理由**: TimeManagerに昼夜サイクルがあるが夜間の特別コンテンツが皆無。テーマパークの華であるナイトパレードがないのは大きな欠落。来場者の滞在時間延長 → 収益増にも直結。
**影響範囲**: 新規 ParadeSystem.cs, ParadeRoute.cs / PathwaySystem拡張 / VisitorAI(鑑賞行動追加) / ParkEventSystem連携 / AudioManager(パレード楽曲)
**工数目安**: 大

### H2. スタッフスキルツリー＆育成システム
**概要**: スタッフに経験値・レベル・スキルツリーを導入。例: メカニック → 「高速修理」「予防保全」分岐, エンターテイナー → 「群衆魅了」「VIP接待」分岐。レベルアップで給与要求も上がるトレードオフ。
**理由**: 8職種のスタッフが存在するが成長要素がなく使い捨て感がある。育成のやりこみ要素はリプレイ性を大きく高める。ストライキシステムとの組み合わせで「ベテラン離反」のドラマも生まれる。
**影響範囲**: StaffMember拡張(XP,Level,Skills) / 各Staff職種クラス / StaffManager / RuntimeStaffPanel / SaveSystem
**工数目安**: 大

### H3. 災害イベントシステム（地震・停電・パンデミック）
**概要**: 低確率で発生する大規模災害イベント。地震(アトラクション損傷+来場者パニック)、停電(夜間営業不能+アトラクション停止)、パンデミック(来場者激減+Doctor需要急増)。事前投資（耐震工事・非常電源・衛生設備）で被害軽減可能。
**理由**: 台風/雷雨の天候システムが整ったが、プレイヤーの危機管理を試すイベントがまだ少ない。AccidentEventSystemは個別事故のみで、パーク全体を揺るがすイベントがない。投資判断のジレンマが戦略性を深める。
**影響範囲**: 新規 DisasterSystem.cs / AccidentEventSystem連携 / ParkManager / EconomyManager / VisitorAI(パニック行動) / DoctorStaff拡張
**工数目安**: 大

### H4. カスタムコースター設計ツール
**概要**: GForceカテゴリのアトラクションについて、コースのレイアウト（上昇・降下・ループ・旋回）をプレイヤーがノードベースで設計可能に。設計に応じて興奮度・嘔吐率・安全性が動的に算出。テスト走行プレビュー付き。
**理由**: テーマパーク経営シミュの花形機能。現在は定型アトラクションの配置のみで、プレイヤーの創造性を活かす場がない。RollerCoaster Tycoonシリーズの最大の魅力がこの機能。
**影響範囲**: 新規 CoasterDesigner.cs, CoasterTrack.cs, CoasterPhysics.cs / AttractionDatabase拡張 / RuntimeBuildPanel / ProceduralMeshGenerator連携
**工数目安**: 特大

### H5. リアルタイム対戦モード（パーク経営バトル）

**概要**: CoopManager基盤を拡張し、2人のプレイヤーが同一マップ内で隣接するパークをそれぞれ経営して競い合う対戦モード。共通の来場者プール（同じ客を取り合う）、相手パークの価格・評判が自パークに影響、期間終了時の総合スコアで勝敗決定。妨害アクション（広告攻勢で相手の客を奪う、スタッフ引き抜き）あり。
**理由**: CoopManagerにポーリングベースの同期・ルーム管理・アクション送信の基盤がすべて揃っているが、協力モードのみで対戦がない。RivalParkSystemのAI対戦を「対人」に昇格させるだけでゲームの寿命が飛躍的に延びる。LeaderboardManagerとの連動でランキング戦も可能。WebGL環境でもポーリング同期で実現可能。
**影響範囲**: CoopManager拡張(対戦ルーム・勝敗判定) / RivalParkSystem(人間プレイヤー対応) / VisitorManager(共有来場者プール) / LeaderboardManager(対戦ランキング) / 新規 PvPMatchSystem.cs / RuntimeHUD(対戦スコアボード)
**工数目安**: 特大

### H6. アトラクション経年劣化＆メンテナンスサイクルシステム
**概要**: アトラクションに「状態(Condition)」パラメータ(0-100%)を導入。稼働時間の累積で自然劣化し、Conditionが下がるほど故障率UP・興奮度DOWN・安全性DOWN。メカニックによる定期点検（軽整備:30分停止/Condition+20）と大規模オーバーホール（半日停止/Condition全回復+寿命延長）の2段階メンテナンスを選択可能。放置するとCondition 20%以下で強制停止→修理費3倍。アトラクションごとに耐久性パラメータ（安価なものほど劣化が速い）を持たせ、アップグレードで耐久性も向上。
**理由**: AccidentEventSystemに事故イベントがあるが「ランダム発生」で戦略性がない。経年劣化の概念を入れることで、メカニック配置・メンテナンス計画・建て替え判断という持続的な経営判断が生まれる。テーマパーク経営シムの中核メカニクスの一つ（Planet Coaster/OpenRCT2が実装済み）。現在のAttraction.csにUpgradeLevel/MaintenanceCostは既にあり、Conditionパラメータの追加で自然に拡張可能。
**影響範囲**: Attraction.cs拡張(Condition,Durability,LastMaintenance) / MechanicStaff拡張(点検/オーバーホール行動) / StaffManager(メンテナンススケジュール) / AccidentEventSystem(Condition依存事故率) / RuntimeBuildPanel(状態表示) / NotificationSystem(劣化アラート)
**工数目安**: 大

---

## MEDIUM優先度（ゲームプレイの幅を拡張）

### M1. 来場者グループ行動システム
**概要**: 現在の来場者は全員個人行動。家族(2-5人)やカップル(2人)、学校遠足(10-20人)などのグループ単位で来場し、グループリーダーの意思決定に追従。グループ割引・グループ写真スポットなどの連動。
**理由**: VisitorTypeにFamily/Coupleがあるが行動は全員バラバラで不自然。グループ行動で「家族向けパーク」「デート向けパーク」などのコンセプト経営が意味を持つ。
**影響範囲**: 新規 VisitorGroup.cs / VisitorAI拡張 / VisitorManager拡張 / PricingSystem(グループ割引)
**工数目安**: 中

### M2. 季節デコレーション＆テーマ着せ替え
**概要**: パーク全体や個別ゾーンに季節・イベントテーマのデコレーションを適用。春(桜)、夏(ひまわり)、秋(紅葉)、冬(クリスマス)、ハロウィン特別。デコレーション投資で来場者の幸福度・SNS映え度UP。
**理由**: ParkEventSystemに季節イベントがあるが視覚的変化がない。GardenerStaffの園芸とも連携し、ゾーン評価のムードカテゴリを底上げする仕組みに。WeatherEffectControllerのライティング変更基盤を流用可能。
**影響範囲**: 新規 DecorationSystem.cs / ParkEffectsManager拡張 / GardenerStaff連携 / ParkRating(ムード評価) / SNSReputationSystem(映えボーナス)
**工数目安**: 中

### M3. アトラクション連動チケットシステム（ファストパス/フリーパス）
**概要**: 通常チケットに加え、ファストパス(特定アトラクション優先搭乗)、フリーパス(全アトラクション乗り放題)、ゾーン限定パスなどのチケット商品を導入。価格設定は自由。行列短縮 → VIP/高所得者の満足度UP。
**理由**: PricingSystemが入場料のみで、テーマパーク特有の多段階チケット設計がない。行列待ち(WaitingInQueue)システムが整っているので、優先搭乗ロジックの追加で実現可能。VIPシステムとの親和性も高い。
**影響範囲**: PricingSystem拡張 / VisitorAI(チケット判定) / Attraction(優先キュー) / VIPVisitorSystem連携 / RuntimeHUD
**工数目安**: 中

### M4. スタッフ配置ゾーン指定＆シフト管理
**概要**: スタッフの担当ゾーンと勤務シフト(朝番/昼番/夜番)を指定可能に。深夜割増給与、連勤によるモチベーション低下、シフト不足時のサービス品質低下。自動最適配置のAIアシスト機能付き。
**理由**: 8職種が存在し5ゾーンがあるが、スタッフの配置戦略がほぼない。ゾーン×シフトの組み合わせで経営判断の奥行きが増す。ストライキシステムとの連動も自然。
**影響範囲**: StaffMember拡張(Zone, Shift) / StaffManager拡張 / RuntimeStaffPanel / TimeManager連携
**工数目安**: 中

### M5. 来場者レビュー＆口コミ詳細化
**概要**: 退園時の来場者がテキストベースの詳細レビューを投稿。「ホラーハウスが最高！でもトイレが少なすぎ…」のようなLLM生成レビュー。レビュー傾向の集計ダッシュボード。WordOfMouthSystemと連携し、良レビューが新規来場者を呼ぶ。
**理由**: WordOfMouthSystem/SNSReputationSystemが存在するがテキスト内容が薄い。LLM連携基盤(Claude/OpenAI/Gemini)が整っているので高品質レビュー生成が実現可能。プレイヤーへのフィードバック情報としても有用。
**影響範囲**: VisitorProfile拡張(Review) / WordOfMouthSystem拡張 / SNSReputationSystem / LLMApiClient連携 / RuntimeHUD_ParkInfo(レビューパネル)
**工数目安**: 中

### M6. ライバルパーク強化（スパイ・提携・買収）
**概要**: RivalParkSystemを拡張し、ライバルパークへのスパイ派遣(相手の価格・アトラクション情報入手)、業務提携(共同イベント・相互送客)、最終的な買収(大金で相手パークを統合)を追加。
**理由**: RivalParkSystemは価格競争のみで、プレイヤーが能動的に対抗する手段が少ない。外交要素を加えることで「競争vs協力」の戦略的選択が生まれ、終盤のゲームプレイが豊かに。
**影響範囲**: RivalParkSystem拡張 / StaffMember(スパイ任務) / EconomyManager / ParkEventSystem(共同イベント)
**工数目安**: 中

### M7. AI来場者パーソナリティ深化＆インフルエンサーシステム
**概要**: 現在のPersonalityTraits（外向性/冒険心/忍耐力/倹約度/好奇心の5軸）をVisitorAIの行動判断により深く反映させる。さらに新VisitorType「Influencer」を追加し、園内のSNS映えスポットやアトラクション体験をリアルタイムにSNS投稿。投稿がバズると来場者スポーン率にブースト。逆に悪体験投稿は炎上リスク。倹約度の高い来場者は安い代替を探し回る、冒険心の低い来場者はホラーハウスを絶対避けるなど、性格が行動ツリーに直結する。
**理由**: PersonalityTraitsは5軸が定義済みだが、VisitorAIのDecideNextAction()での実利用がごくわずか（アトラクション好みのみ）。LLM会話用のDescribe()は充実しているが、ゲームプレイ上の行動に反映されていないのが惜しい。インフルエンサーはWordOfMouthSystem/SNSReputationSystemとの自然な接続点になり、「SNS戦略」という新しい経営軸を生む。
**影響範囲**: VisitorType追加(Influencer) / VisitorProfile拡張 / VisitorAI(DecideNextAction全面見直し) / SNSReputationSystem(インフルエンサー投稿) / WordOfMouthSystem / PricingSystem(倹約度連動)
**工数目安**: 中〜大

### M8. インタラクティブアトラクション種別（参加型ライド）
**概要**: 新AttractionCategory「Interactive」を追加。シューティングライド（ライド中にターゲットを撃ちスコア取得）、脱出ゲーム型（制限時間内に謎解き）、ARトレジャーハント（パーク内を歩き回り宝探し）など、来場者の行動がスコア・報酬に影響するアトラクション群。来場者の好奇心(Curiosity)・冒険心(Adventurousness)特性と連動し、高スコアで幸福度ボーナス。SNSでスコア自慢→バイラル効果。
**理由**: 現在の6カテゴリ（GForce/VerticalRotation/HorizontalRotation/Observation/Show/Ride）はすべて「乗るだけ」の受動型で、来場者が能動的に参加する要素がゼロ。現代のテーマパークでは体験型アトラクションが主流（USJのハリポッター、TDRのバズ・ライトイヤー等）。DynamicQuestSystemのクエスト生成基盤を活用すれば、ARトレジャーハントの実装コストを抑えられる。
**影響範囲**: AttractionCategory追加(Interactive) / AttractionDatabase(3-4種追加) / attractions.json / Attraction.cs(スコアシステム) / VisitorAI(参加行動) / DynamicQuestSystem連携(トレジャーハント) / SNSReputationSystem(スコアシェア)
**工数目安**: 大

### M9. マーケティング＆広告キャンペーンシステム
**概要**: パークの集客を能動的に行うマーケティングシステム。TV CM(高コスト・全VisitorType集客ブースト)、Web広告(中コスト・Young/Couple特化)、チラシ配布(低コスト・Family/Kids特化)、インフルエンサー招待(M7連動・SNS拡散倍増)の4チャネル。キャンペーン期間(1日/3日/7日)を選択し、予算投下で来場者スポーン率・特定VisitorType比率を操作。費用対効果はパーク評価とキャンペーン組み合わせで変動（高評価パークのTV CMは効果2倍）。過剰広告は「期待値インフレ」を起こし、実体験とのギャップで満足度ペナルティ。
**理由**: SaleCampaignSystemは値引きキャンペーンのみで「客を呼ぶ」能動的手段がない。来場者スポーンはWeather/ParkEvent/ParkRatingの受動要因のみに依存しており、プレイヤーが戦略的に集客をコントロールできない。テーマパーク経営の重要な柱である「マーケティング」が完全に欠落。EconomyManagerの支出カテゴリにMarketing枠を追加し、FinancialReportのExpenseBreakdownで可視化。
**影響範囲**: 新規 MarketingSystem.cs / SaleCampaignSystem連携 / EconomyManager拡張(Marketing支出) / VisitorManager(キャンペーンスポーンブースト) / FinancialReport拡張 / SNSReputationSystem連携 / RuntimeHUD(キャンペーンパネル)
**工数目安**: 中

### M10. 園内交通システム（モノレール・パークトレイン）
**概要**: 大規模パーク向けの内部交通手段。モノレール(高コスト・高速・高定員・駅2-4箇所設置)とパークトレイン(低コスト・低速・8駅まで・景観ルート)の2種。来場者はゾーン間移動時に疲労度(Fatigue)閾値を超えると自動的に乗車を選択。乗車中は疲労度回復＋幸福度微増（車窓パーク観覧効果）。路線はPathwaySystem上にプレイヤーが設定。混雑セグメントを通る路線は利用率が高く、混雑緩和に貢献。建設コスト・維持費はあるが、来場者の滞在時間延長（疲労で早期退園を防ぐ）→収益増のリターンあり。
**理由**: PathwaySystemが混雑度をリアルタイム追跡しているが、混雑緩和手段がゼロ。広大なパーク（ParkExpansionで3段階拡張済み）では端から端への移動で疲労度が限界に達し、まだ体験していないゾーンを諦めて退園する来場者が発生する。VisitorParametersの疲労度ペナルティが厳しいため、交通システムは「疲労対策」として経営上の意味を持つ。FirstPersonCameraで乗車ビューも提供でき没入感もUP。
**影響範囲**: 新規 ParkTransitSystem.cs, TransitRoute.cs / PathwaySystem連携(路線設定) / VisitorAI拡張(乗車判断・疲労回復行動) / VisitorParameters(乗車中の疲労回復) / ParkExpansionSystem連携 / RuntimeBuildPanel(路線建設UI) / FirstPersonCamera(乗車ビュー)
**工数目安**: 大

---

## LOW優先度（ポリッシュ・没入感向上）

### L1. パーク内BGMゾーン別カスタマイズ
**概要**: ゾーンごとに異なるBGMを再生。ロストキングダム(冒険オーケストラ)、ハロウィーンワールド(ホラーアンビエント)、ワンダーランド(メルヘンワルツ)等。ProceduralAudioLibraryを活用して動的に生成。
**理由**: AudioManagerが天候切替対応済みだがゾーン別BGMがない。テーマゾーンの没入感が大幅に向上。
**影響範囲**: AudioManager拡張 / ProceduralAudioLibrary連携 / カメラ位置検出
**工数目安**: 小

### L2. 来場者写真撮影スポット
**概要**: パーク内にフォトスポット施設を配置可能。来場者が立ち寄って「撮影」するとSNS映え度UP。SocialShareSystemと連携し、人気フォトスポットのスクリーンショットが自動生成。
**理由**: SocialShareSystem(スクリーンショット機能)が存在するがゲームプレイとの連動が薄い。施設タイプの追加は小規模で実現可能。
**影響範囲**: FacilityType追加(PhotoSpot) / VisitorAI(撮影行動) / SNSReputationSystem / SocialShareSystem
**工数目安**: 小

### L3. 実績システム拡充（隠し実績・コレクション要素）
**概要**: AchievementSystemに隠し実績(台風を3回乗り越える、Platinumゲスト5人満足など)とコレクション要素(全アトラクション制覇、全ゾーン解放など)を追加。実績報酬としてゴールデンチケットやデコレーション解放。
**理由**: AchievementSystemが存在するがコンテンツ量が不十分。新しく追加した台風/VIPランク/新アトラクションに対応する実績がない。
**影響範囲**: AchievementSystem拡張 / RuntimeHUD(実績通知)
**工数目安**: 小

### L4. チュートリアル拡張（インタラクティブガイド）
**概要**: TutorialSystemの既存フレームワーク上に、新機能（台風対策・VIPランク対応・新アトラクション活用法）のステップバイステップガイドを追加。初心者向けの「おすすめパーク設計」テンプレート機能。
**理由**: 新コンテンツが増えるほど初見プレイヤーの学習コストが上がる。TutorialSystem基盤は整っているのでコンテンツ追加のみで実現可能。
**影響範囲**: TutorialSystem(ステップデータ追加) / RuntimeHUD
**工数目安**: 小

### L5. Co-opモード実コンテンツ追加（共同シナリオ）
**概要**: CoopManagerの基盤上に、2人プレイ専用シナリオを追加。プレイヤーAがアトラクション担当、プレイヤーBがスタッフ・経済担当のような役割分担型。
**理由**: CoopManagerが基盤のみで実際のマルチプレイコンテンツがない。WebGL環境のため通信制約はあるが、ターンベース的な非同期協力は実現可能。
**影響範囲**: CoopManager拡張 / ScenarioDatabase(共同シナリオ追加) / RuntimeHUD
**工数目安**: 中（基盤はあるが同期ロジックが必要）

### L6. アクセシビリティ設定の充実
**概要**: AccessibilitySystemを拡張し、色覚多様性対応(カラーフィルター切替)、テキストサイズ調整、画面フラッシュ無効化(雷雨エフェクト)、音声読み上げ対応。
**理由**: 雷雨のホワイトフラッシュ追加により、光過敏性の来場者対応が必要。AccessibilitySystemの基盤がある。
**影響範囲**: AccessibilitySystem拡張 / WeatherEffectController(フラッシュ抑制) / FontManager / RuntimeHUD
**工数目安**: 小

### L7. モバイルUI最適化＆レスポンシブレイアウト
**概要**: InputManagerにタッチ操作基盤があるが、RuntimeHUD(1920x1080基準)のUIレイアウトをモバイル画面に最適化。ボタンサイズ拡大、パネル折りたたみ、ジェスチャー操作（ピンチズーム強化・スワイプメニュー）、縦画面モード対応。CanvasScalerのmatchWidthOrHeightを画面比率で動的切替。
**理由**: WebGL対応でブラウザプレイ可能だが、スマホブラウザでのUIが実質使い物にならない。InputManagerのHandleTouchInput()/HandleSingleTouch()は実装済みで、UIレイアウト調整が主な作業。CanvasScalerの基盤もある。モバイルユーザー獲得はDAU増加に直結。
**影響範囲**: RuntimeHUD全partial(レスポンシブ化) / InputManager(ジェスチャー拡張) / CanvasScaler設定 / RuntimeBuildPanel(タッチ最適化) / RuntimeStaffPanel
**工数目安**: 中

### L8. 来場者リピートシステム＆長期記憶
**概要**: 退園した来場者の体験データ（訪問回数、乗ったアトラクション、満足度履歴、LLM会話サマリー）をVisitorProfileに永続保存。高満足度(80+)の来場者は30-60日後にリピーター(再来園)として登場し、前回の記憶を持つ。リピーターは「前回楽しかったアトラクションにまた乗りたい」「前回食べたフードを再注文」などの再訪行動を取る。リピート回数に応じて「常連ボーナス」（所持金UP・SNS口コミ効果UP）。逆に前回不満だった点が改善されていないと即退園リスク。NPCDialogueSystemの会話サマリー（ConversationSummaryPrompt）を来場者の長期記憶として活用し、再来園時に「前回○○の話をしたのを覚えてる？」のような会話が可能。
**理由**: 現在の来場者は退園後に消滅し二度と戻らない「使い捨て」。パーク改善の成果を実感できる「リピーター増加」という指標がなく、経営の長期的な意味付けが弱い。VisitorProfile/SaveSystem/NPCDialogueのConversationSummaryPromptの基盤はすべて揃っており、永続化レイヤーの追加が主な作業。
**影響範囲**: VisitorProfile拡張(訪問履歴・記憶データ) / VisitorManager(リピーター生成ロジック) / VisitorAI(再訪行動パターン) / SaveSystem(来場者永続データ) / NPCDialogueSystem(記憶参照会話) / WordOfMouthSystem(リピーター口コミ) / RuntimeHUD(リピート率表示)
**工数目安**: 中

### L9. 待ち列エンターテイメント＆キューイング演出
**概要**: アトラクションの待ち列(WaitingInQueue状態)をゲームプレイ要素として強化。①キューエリアテーマ装飾（投資で行列空間をテーマ化→待ち幸福度ペナルティ軽減）、②キューエンターテイナー配置（Entertainerの新任務として行列横で芸を披露→待ち幸福度ペナルティをゼロ近くに）、③推定待ち時間ディスプレイ（待ち時間表示→忍耐力(Patience)の低い来場者が長い列を避けて別のアトラクションへ→混雑自動分散）。投資レベル(なし/基本テーマ化/フル演出)の3段階。
**理由**: VisitorBehaviorState.WaitingInQueueは行列待ちの幸福度ペナルティが常に一定（VisitorParametersで毎時減少）で、プレイヤーが改善する手段がない。Entertainerは広場での芸しかなく、行列整理という現実のテーマパークの重要業務がない。PersonalityTraitsのPatience軸が行列判断に未使用（M7で行動全般に反映予定だが、キュー特化の演出はここで独立）。混雑緩和と満足度維持の両方に効く費用対効果の高い投資となる。
**影響範囲**: Attraction.cs拡張(QueueThemeLevel,QueueEntertainment) / VisitorAI(待ち行動改善・列選択AI) / VisitorParameters(テーマ化による待ちペナルティ係数) / EntertainerStaff拡張(キュー配置行動) / RuntimeBuildPanel(キューアップグレードUI)
**工数目安**: 中

---

## 推奨実装ロードマップ（改訂版 v3）

| フェーズ | 内容 | 工期目安 |
|---|---|---|
| Phase 1 | L3(実績拡充) + L6(アクセシビリティ) + L1(ゾーンBGM) + L9(待ち列演出) | 小〜中 |
| Phase 2 | H6(経年劣化) + M3(ファストパス) + M4(シフト管理) + L4(チュートリアル) | 中〜大 |
| Phase 3 | H1(ナイトパレード) + M2(デコレーション) + L2(フォトスポット) + L8(リピーター) | 中〜大 |
| Phase 4 | H2(スタッフ育成) + M7(AIパーソナリティ) + M5(レビュー) + M9(マーケティング) | 大 |
| Phase 5 | H3(災害イベント) + M8(インタラクティブ) + M1(グループ行動) + M10(園内交通) | 大 |
| Phase 6 | M6(ライバル強化) + L5(Co-op) + H5(対戦モード) + L7(モバイルUI) | 大（マルチ統合） |
| Phase 7 | H4(コースター設計) | 特大（独立開発） |

### 提案サマリー（全25件）
- **HIGH**: 6件（H1〜H6）— パレード / スタッフ育成 / 災害 / コースター設計 / 対戦モード / **経年劣化メンテナンス**
- **MEDIUM**: 10件（M1〜M10）— グループ行動 / デコ / チケット / シフト / レビュー / ライバル / AIパーソナリティ / 参加型アトラクション / **マーケティング** / **園内交通**
- **LOW**: 9件（L1〜L9）— BGM / フォトスポット / 実績 / チュートリアル / Co-op / アクセシビリティ / モバイルUI / **リピーター** / **待ち列演出**

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
