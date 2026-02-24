// ============================================================
// ThemeParkGame - VisitorParameters
// 来場者の全パラメータを管理するデータクラス
// 幸福度・興奮度・吐き気・空腹・喉の渇き・トイレ欲求・所持金を統合管理
// ============================================================

using System;
using UnityEngine;
using ThemeParkGame.Core;

namespace ThemeParkGame.Visitor
{
    /// <summary>
    /// 来場者の内部パラメータ一式を保持・管理するデータクラス。
    /// 各パラメータは0-100の範囲でクランプされ、ティックごとの自動増減ロジックを持つ。
    ///
    /// 【ゲームデザインメモ】
    /// - 幸福度(Happiness)が最重要指標。パークの評価に直結する。
    /// - 吐き気(Nausea)80以上で嘔吐イベント発生。清掃スタッフが必要になる。
    /// - トイレ欲求(ToiletNeed)95以上で「お漏らし」イベント。幸福度が大幅低下。
    /// - 空腹・喉の渇きは時間経過で自然増加し、ショップへ誘導する経済ドライバー。
    /// - 喉の渇きは暑い天候(Hot)で増加速度が1.5倍になる。
    /// - VisitorTypeごとに所持金の初期値と消費傾向が異なる。
    /// </summary>
    [Serializable]
    public class VisitorParameters
    {
        // ---- 定数定義 ----

        /// <summary>パラメータの最小値</summary>
        public const float MinValue = 0f;

        /// <summary>パラメータの最大値</summary>
        public const float MaxValue = 100f;

        /// <summary>嘔吐が発生する吐き気の閾値</summary>
        public const float NauseaVomitThreshold = 80f;

        /// <summary>お漏らしが発生するトイレ欲求の閾値</summary>
        public const float ToiletAccidentThreshold = 95f;

        /// <summary>退園を検討し始める幸福度の閾値</summary>
        public const float HappinessLeaveThreshold = 30f;

        /// <summary>空腹でショップを探し始める閾値</summary>
        public const float HungerSeekThreshold = 60f;

        /// <summary>喉の渇きでショップを探し始める閾値</summary>
        public const float ThirstSeekThreshold = 60f;

        /// <summary>トイレを探し始める閾値</summary>
        public const float ToiletSeekThreshold = 70f;

        /// <summary>休憩を求める興奮度の低下閾値</summary>
        public const float ExcitementRestThreshold = 15f;

        // ---- ティックごとの増減レート（1秒あたり） ----

        /// <summary>空腹の自然増加レート（毎秒）</summary>
        private const float HungerIncreaseRate = 0.8f;

        /// <summary>喉の渇きの自然増加レート（毎秒）</summary>
        private const float ThirstIncreaseRate = 1.0f;

        /// <summary>暑い天候での喉の渇き倍率</summary>
        private const float HotWeatherThirstMultiplier = 1.5f;

        /// <summary>トイレ欲求の自然増加レート（毎秒）</summary>
        private const float ToiletNeedIncreaseRate = 0.6f;

        /// <summary>吐き気の自然回復レート（毎秒）</summary>
        private const float NauseaDecayRate = 0.5f;

        /// <summary>興奮度の自然減衰レート（毎秒）</summary>
        private const float ExcitementDecayRate = 0.3f;

        /// <summary>幸福度が不快要因で減少するレート（毎秒）</summary>
        private const float HappinessDecayFromDiscomfort = 0.2f;

        // ---- パラメータフィールド ----

        [Header("主要パラメータ")]
        [SerializeField, Range(0f, 100f)]
        private float happiness = 70f;

        [SerializeField, Range(0f, 100f)]
        private float excitement = 50f;

        [SerializeField, Range(0f, 100f)]
        private float nausea = 0f;

        [SerializeField, Range(0f, 100f)]
        private float hunger = 10f;

        [SerializeField, Range(0f, 100f)]
        private float thirst = 10f;

        [SerializeField, Range(0f, 100f)]
        private float toiletNeed = 5f;

        [Header("満足度スコア")]
        [SerializeField, Range(0f, 100f)]
        private float satisfaction = 50f;

        [Header("所持金")]
        [SerializeField]
        private float cash = 100f;

        // ---- プロパティ ----

        /// <summary>
        /// 幸福度（0-100）。パーク評価の最重要指標。
        /// 【ゲームデザイン】全ての不快要素が低下させ、楽しい体験が上昇させる。
        /// 20未満で退園検討。高いほどリピーター率UP、口コミ効果UP。
        /// </summary>
        public float Happiness
        {
            get => happiness;
            private set => happiness = Mathf.Clamp(value, MinValue, MaxValue);
        }

        /// <summary>
        /// 興奮度（0-100）。アトラクション搭乗で上昇、時間経過で減衰。
        /// 【ゲームデザイン】高すぎると吐き気に変換される（特にKidsとSenior）。
        /// 低すぎると退屈して幸福度が下がる。適度な刺激の維持が重要。
        /// </summary>
        public float Excitement
        {
            get => excitement;
            private set => excitement = Mathf.Clamp(value, MinValue, MaxValue);
        }

        /// <summary>
        /// 吐き気（0-100）。激しいアトラクション搭乗で上昇、時間経過で回復。
        /// 【ゲームデザイン】80以上で嘔吐イベント発生。地面が汚れ、清掃スタッフが必要。
        /// 周囲の来場者の幸福度にも悪影響。Kids/Seniorは吐き気が溜まりやすい。
        /// </summary>
        public float Nausea
        {
            get => nausea;
            private set => nausea = Mathf.Clamp(value, MinValue, MaxValue);
        }

        /// <summary>
        /// 空腹度（0-100）。時間経過で自然増加。
        /// 【ゲームデザイン】60以上でフードショップを探し始める。
        /// 高いほど幸福度が低下し、食べ物を買った時の満足度が高い。
        /// ショップ経済の主要ドライバー。
        /// </summary>
        public float Hunger
        {
            get => hunger;
            private set => hunger = Mathf.Clamp(value, MinValue, MaxValue);
        }

        /// <summary>
        /// 喉の渇き（0-100）。時間経過で自然増加、暑い天候で加速。
        /// 【ゲームデザイン】60以上でドリンクショップを探し始める。
        /// Hot天候で1.5倍速で増加するため、天候に合わせたドリンクショップ配置が重要。
        /// </summary>
        public float Thirst
        {
            get => thirst;
            private set => thirst = Mathf.Clamp(value, MinValue, MaxValue);
        }

        /// <summary>
        /// トイレ欲求（0-100）。時間経過で自然増加、飲食後に加速。
        /// 【ゲームデザイン】70以上でトイレを探し始める。95以上で「お漏らし」発生。
        /// お漏らしは幸福度-30、周囲にも悪影響。トイレの適切な配置が必須。
        /// </summary>
        public float ToiletNeed
        {
            get => toiletNeed;
            private set => toiletNeed = Mathf.Clamp(value, MinValue, MaxValue);
        }

        /// <summary>
        /// 累積満足度スコア（0-100）。アトラクション体験・食事・サービスで加算される。
        /// 【ゲームデザイン】Happinessは瞬間的な気分、Satisfactionは累積的な体験評価。
        /// 高い満足度の来場者はSNS投稿でパークの知名度を上げ、リピーターになる。
        /// </summary>
        public float Satisfaction
        {
            get => satisfaction;
            private set => satisfaction = Mathf.Clamp(value, MinValue, MaxValue);
        }

        /// <summary>
        /// 所持金。VisitorTypeにより初期値が異なる。
        /// 【ゲームデザイン】0になると何も買えず不満が溜まり退園する。
        /// VIPは高額、Kidsは少額。価格設定のバランスが経営の鍵。
        /// </summary>
        public float Cash
        {
            get => cash;
            private set => cash = Mathf.Max(0f, value);
        }

        // ---- 状態クエリプロパティ ----

        /// <summary>嘔吐寸前または嘔吐中か</summary>
        public bool IsAboutToVomit => Nausea >= NauseaVomitThreshold;

        /// <summary>トイレ事故寸前か</summary>
        public bool IsAboutToHaveAccident => ToiletNeed >= ToiletAccidentThreshold;

        /// <summary>退園を検討するほど不幸か</summary>
        public bool WantsToLeave => Happiness < HappinessLeaveThreshold;

        /// <summary>お腹が空いているか</summary>
        public bool IsHungry => Hunger >= HungerSeekThreshold;

        /// <summary>喉が渇いているか</summary>
        public bool IsThirsty => Thirst >= ThirstSeekThreshold;

        /// <summary>トイレに行きたいか</summary>
        public bool NeedsToilet => ToiletNeed >= ToiletSeekThreshold;

        /// <summary>退屈しているか</summary>
        public bool IsBored => Excitement < ExcitementRestThreshold;

        /// <summary>お金が残っているか</summary>
        public bool HasMoney => Cash > 0f;

        /// <summary>
        /// 全体的な満足度スコア（0-100）。
        /// 累積満足度(60%)と瞬間的な幸福度(40%)の加重平均に不快ペナルティを適用。
        /// </summary>
        public float OverallSatisfaction
        {
            get
            {
                float discomfortPenalty = (Hunger + Thirst + Nausea + ToiletNeed) * 0.04f;
                float excitementBonus = Excitement * 0.08f;
                float combined = Satisfaction * 0.6f + Happiness * 0.4f;
                return Mathf.Clamp(combined - discomfortPenalty + excitementBonus, MinValue, MaxValue);
            }
        }

        // ---- 初期化 ----

        /// <summary>
        /// VisitorTypeに応じた初期パラメータを設定する。
        /// 【ゲームデザイン】タイプごとに所持金・初期幸福度・耐性が異なり、
        /// プレイヤーに多様な来場者への対応を求める。
        /// </summary>
        public void Initialize(VisitorType type)
        {
            switch (type)
            {
                case VisitorType.Kids:
                    // キッズ: 所持金少なめ、幸福度高め（テンション高い）、吐きやすい
                    Cash = UnityEngine.Random.Range(30f, 80f);
                    Happiness = UnityEngine.Random.Range(75f, 90f);
                    Excitement = UnityEngine.Random.Range(60f, 80f);
                    break;

                case VisitorType.Young:
                    // ヤング: 所持金普通、スリル好き、吐き気に強い
                    Cash = UnityEngine.Random.Range(80f, 150f);
                    Happiness = UnityEngine.Random.Range(60f, 80f);
                    Excitement = UnityEngine.Random.Range(40f, 60f);
                    break;

                case VisitorType.Family:
                    // ファミリー: 所持金多め、幸福度普通、全体的にバランス型
                    Cash = UnityEngine.Random.Range(150f, 300f);
                    Happiness = UnityEngine.Random.Range(65f, 80f);
                    Excitement = UnityEngine.Random.Range(30f, 50f);
                    break;

                case VisitorType.Couple:
                    // カップル: 所持金やや多め、幸福度高め
                    Cash = UnityEngine.Random.Range(120f, 250f);
                    Happiness = UnityEngine.Random.Range(70f, 85f);
                    Excitement = UnityEngine.Random.Range(40f, 60f);
                    break;

                case VisitorType.Senior:
                    // シニア: 所持金多め、興奮度低め、吐きやすい、トイレ頻度高い
                    Cash = UnityEngine.Random.Range(100f, 200f);
                    Happiness = UnityEngine.Random.Range(55f, 70f);
                    Excitement = UnityEngine.Random.Range(20f, 40f);
                    break;

                case VisitorType.VIP:
                    // VIP: 所持金潤沢、期待値が高い（初期幸福度やや低め）
                    Cash = UnityEngine.Random.Range(500f, 1000f);
                    Happiness = UnityEngine.Random.Range(50f, 65f);
                    Excitement = UnityEngine.Random.Range(30f, 50f);
                    break;
            }

            // 共通初期値
            Satisfaction = 50f;
            Nausea = 0f;
            Hunger = UnityEngine.Random.Range(5f, 20f);
            Thirst = UnityEngine.Random.Range(5f, 20f);
            ToiletNeed = UnityEngine.Random.Range(0f, 15f);
        }

        // ---- ティック更新 ----

        /// <summary>
        /// 毎フレーム呼ばれるパラメータの自動増減処理。
        /// deltaTimeはTime.deltaTimeを受け取る。
        /// </summary>
        /// <param name="deltaTime">経過時間（秒）</param>
        /// <param name="currentWeather">現在の天候（喉の渇き加速判定）</param>
        /// <param name="visitorType">来場者タイプ（タイプ別補正）</param>
        public void Tick(float deltaTime, Weather currentWeather, VisitorType visitorType)
        {
            // 空腹: 自然増加
            float hungerRate = HungerIncreaseRate * GetHungerMultiplier(visitorType);
            Hunger += hungerRate * deltaTime;

            // 喉の渇き: 自然増加（暑い天候で加速）
            float thirstRate = ThirstIncreaseRate;
            if (currentWeather == Weather.Hot)
            {
                thirstRate *= HotWeatherThirstMultiplier;
            }
            Thirst += thirstRate * deltaTime;

            // トイレ欲求: 自然増加（シニアは加速）
            float toiletRate = ToiletNeedIncreaseRate * GetToiletMultiplier(visitorType);
            ToiletNeed += toiletRate * deltaTime;

            // 吐き気: 自然回復（安静時）
            if (Nausea > 0f)
            {
                Nausea -= NauseaDecayRate * deltaTime;
            }

            // 興奮度: 自然減衰
            if (Excitement > 0f)
            {
                Excitement -= ExcitementDecayRate * deltaTime;
            }

            // 幸福度: 不快要因による自然減少
            float discomfortLevel = CalculateDiscomfortLevel();
            if (discomfortLevel > 0f)
            {
                Happiness -= HappinessDecayFromDiscomfort * discomfortLevel * deltaTime;
            }

            // パークイベントボーナス: 開催中イベントの幸福度/満足度パッシブ加算
            if (GameManager.Instance != null && GameManager.Instance.ParkEventSystem != null)
            {
                var eventSys = GameManager.Instance.ParkEventSystem;
                float happyBonus = eventSys.CurrentHappinessBonus;
                float satBonus = eventSys.CurrentSatisfactionBonus;

                // 1秒あたり微量加算（ボーナス値/60で1分あたりに正規化）
                if (happyBonus > 0f)
                    Happiness += (happyBonus / 60f) * deltaTime;
                if (satBonus > 0f)
                    Satisfaction += (satBonus / 60f) * deltaTime;
            }
        }

        // ---- パラメータ変更メソッド ----

        /// <summary>幸福度を加算する（負値で減算）</summary>
        public void ModifyHappiness(float amount)
        {
            float oldValue = Happiness;
            Happiness += amount;
            if (Mathf.Abs(Happiness - oldValue) > 0.01f)
            {
                // イベント通知は呼び出し元（VisitorAI）で行う
            }
        }

        /// <summary>興奮度を加算する</summary>
        public void ModifyExcitement(float amount)
        {
            Excitement += amount;
        }

        /// <summary>
        /// 吐き気を加算する。VisitorTypeに応じた耐性補正を適用。
        /// 【ゲームデザイン】Kids/Seniorは1.5倍、Youngは0.7倍の吐き気蓄積。
        /// </summary>
        public void ModifyNausea(float amount, VisitorType visitorType)
        {
            float multiplier = GetNauseaSensitivity(visitorType);
            Nausea += amount * multiplier;
        }

        /// <summary>空腹度を加算する（負値で減算＝食事）</summary>
        public void ModifyHunger(float amount)
        {
            Hunger += amount;
        }

        /// <summary>喉の渇きを加算する（負値で減算＝飲料）</summary>
        public void ModifyThirst(float amount)
        {
            Thirst += amount;
        }

        /// <summary>トイレ欲求を加算する（負値で減算＝トイレ使用後）</summary>
        public void ModifyToiletNeed(float amount)
        {
            ToiletNeed += amount;
        }

        /// <summary>満足度スコアを加算する（負値で減算）。変化量を返す。</summary>
        public float ModifySatisfaction(float amount)
        {
            float old = Satisfaction;
            Satisfaction += amount;
            return Satisfaction - old;
        }

        /// <summary>所持金を使う。残高不足の場合falseを返す。</summary>
        public bool SpendCash(float amount)
        {
            if (amount < 0f)
            {
                Debug.LogWarning("[VisitorParameters] SpendCash called with negative amount.");
                return false;
            }
            if (Cash < amount) return false;
            Cash -= amount;
            return true;
        }

        /// <summary>所持金を追加する（拾い物、払い戻し等）</summary>
        public void AddCash(float amount)
        {
            if (amount < 0f)
            {
                Debug.LogWarning("[VisitorParameters] AddCash called with negative amount.");
                return;
            }
            Cash += amount;
        }

        /// <summary>
        /// 食事後の効果を適用する。
        /// 【ゲームデザイン】食事は空腹を大幅に下げるが、トイレ欲求を少し上げる。
        /// 食事の質が高いほど幸福度ボーナスが大きい。
        /// </summary>
        /// <param name="hungerReduction">空腹減少量</param>
        /// <param name="qualityBonus">食事の質ボーナス（幸福度に加算）</param>
        public void ApplyFoodEffect(float hungerReduction, float qualityBonus)
        {
            ModifyHunger(-hungerReduction);
            ModifyHappiness(qualityBonus);
            // 食事後はトイレ欲求が少し上がる
            ModifyToiletNeed(hungerReduction * 0.15f);
            // 食事の満足度加算（空腹時ほど高い）
            float satBonus = qualityBonus * 0.1f + (Hunger < 20f ? 2f : 0f);
            ModifySatisfaction(satBonus);
        }

        /// <summary>
        /// 飲料後の効果を適用する。
        /// 【ゲームデザイン】飲料は喉の渇きを下げ、トイレ欲求をやや上げる。
        /// </summary>
        /// <param name="thirstReduction">渇き減少量</param>
        /// <param name="qualityBonus">飲料の質ボーナス</param>
        public void ApplyDrinkEffect(float thirstReduction, float qualityBonus)
        {
            ModifyThirst(-thirstReduction);
            ModifyHappiness(qualityBonus);
            // 飲料後はトイレ欲求が上がる
            ModifyToiletNeed(thirstReduction * 0.2f);
            // 飲料の満足度加算
            ModifySatisfaction(qualityBonus * 0.08f);
        }

        /// <summary>
        /// トイレ使用後の効果を適用する。
        /// 【ゲームデザイン】トイレ使用でトイレ欲求リセット。清潔なトイレは幸福度ボーナス。
        /// </summary>
        /// <param name="cleanlinessBonus">トイレの清潔度ボーナス</param>
        public void ApplyToiletEffect(float cleanlinessBonus)
        {
            ToiletNeed = 0f;
            ModifyHappiness(cleanlinessBonus);
        }

        /// <summary>
        /// アトラクション搭乗後の効果を適用する。
        /// 【ゲームデザイン】スリル度に応じて興奮度UP・吐き気UP。
        /// 満足度はアトラクションの質と来場者の好みで決まる。
        /// </summary>
        /// <param name="excitementGain">興奮度増加量</param>
        /// <param name="nauseaGain">吐き気増加量</param>
        /// <param name="satisfactionGain">満足度（幸福度への加算）</param>
        /// <param name="visitorType">来場者タイプ（吐き気耐性計算用）</param>
        public void ApplyRideEffect(float excitementGain, float nauseaGain, float satisfactionGain, VisitorType visitorType)
        {
            ModifyExcitement(excitementGain);
            ModifyNausea(nauseaGain, visitorType);
            ModifyHappiness(satisfactionGain);

            // 満足度スコアに搭乗結果を反映（satisfactionGainは0-100なので正規化）
            float satDelta = satisfactionGain * 0.15f;
            // 嘔吐寸前ならペナルティ
            if (IsAboutToVomit) satDelta -= 5f;
            ModifySatisfaction(satDelta);
        }

        /// <summary>
        /// 嘔吐イベント後の効果を適用する。
        /// 【ゲームデザイン】嘔吐で吐き気は大幅低下するが、幸福度が激減する。
        /// </summary>
        public void ApplyVomitEffect()
        {
            Nausea = Mathf.Max(0f, Nausea - 50f);
            ModifyHappiness(-25f);
        }

        /// <summary>
        /// トイレ事故イベント後の効果を適用する。
        /// 【ゲームデザイン】お漏らしは最も恥ずかしいイベント。幸福度が大幅低下。
        /// </summary>
        public void ApplyAccidentEffect()
        {
            ToiletNeed = 0f;
            ModifyHappiness(-30f);
        }

        /// <summary>パラメータのスナップショットを文字列で返す（デバッグ用）</summary>
        public override string ToString()
        {
            return $"[HP:{Happiness:F0} SAT:{Satisfaction:F0} EX:{Excitement:F0} NA:{Nausea:F0} " +
                   $"HU:{Hunger:F0} TH:{Thirst:F0} TL:{ToiletNeed:F0} $:{Cash:F0}]";
        }

        // ---- 内部ヘルパー ----

        /// <summary>現在の不快度を計算する（0-1の正規化値）</summary>
        private float CalculateDiscomfortLevel()
        {
            float discomfort = 0f;

            // 各不快要因を0-1に正規化して合算
            if (Hunger > HungerSeekThreshold)
                discomfort += (Hunger - HungerSeekThreshold) / (MaxValue - HungerSeekThreshold);

            if (Thirst > ThirstSeekThreshold)
                discomfort += (Thirst - ThirstSeekThreshold) / (MaxValue - ThirstSeekThreshold);

            if (Nausea > 30f)
                discomfort += (Nausea - 30f) / (MaxValue - 30f);

            if (ToiletNeed > ToiletSeekThreshold)
                discomfort += (ToiletNeed - ToiletSeekThreshold) / (MaxValue - ToiletSeekThreshold);

            // 0-1にクランプ
            return Mathf.Clamp01(discomfort / 4f) * 4f; // 最大4倍速で幸福度減少
        }

        /// <summary>
        /// VisitorType別の吐き気感受性を返す。
        /// 【ゲームデザイン】Kidsは酔いやすく、Youngは酔いにくい。
        /// </summary>
        private float GetNauseaSensitivity(VisitorType type)
        {
            switch (type)
            {
                case VisitorType.Kids:   return 1.5f;  // 酔いやすい
                case VisitorType.Young:  return 0.7f;  // 酔いにくい
                case VisitorType.Family: return 1.0f;
                case VisitorType.Couple: return 0.9f;
                case VisitorType.Senior: return 1.5f;  // 酔いやすい
                case VisitorType.VIP:    return 1.0f;
                default: return 1.0f;
            }
        }

        /// <summary>VisitorType別の空腹増加倍率</summary>
        private float GetHungerMultiplier(VisitorType type)
        {
            switch (type)
            {
                case VisitorType.Kids:   return 1.3f;  // お腹が空きやすい
                case VisitorType.Young:  return 1.1f;
                case VisitorType.Family: return 1.0f;
                case VisitorType.Couple: return 0.9f;
                case VisitorType.Senior: return 0.8f;
                case VisitorType.VIP:    return 1.0f;
                default: return 1.0f;
            }
        }

        /// <summary>VisitorType別のトイレ欲求増加倍率</summary>
        private float GetToiletMultiplier(VisitorType type)
        {
            switch (type)
            {
                case VisitorType.Kids:   return 1.2f;
                case VisitorType.Young:  return 0.9f;
                case VisitorType.Family: return 1.0f;
                case VisitorType.Couple: return 1.0f;
                case VisitorType.Senior: return 1.4f;  // トイレが近い
                case VisitorType.VIP:    return 1.0f;
                default: return 1.0f;
            }
        }
    }
}
