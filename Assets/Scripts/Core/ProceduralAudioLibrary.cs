// ============================================================
// ThemeParkGame - ProceduralAudioLibrary
// コードによるオーディオクリップ動的生成
// WebGLビルドでオーディオアセットなしで動作する
// ============================================================

using UnityEngine;

namespace ThemeParkGame.Core
{
    /// <summary>
    /// プロシージャル（手続き的）にオーディオクリップを生成するライブラリ。
    /// 正弦波・ノイズ・エンベロープを組み合わせて各種SE/BGMを作成する。
    /// </summary>
    public static class ProceduralAudioLibrary
    {
        private const int SampleRate = 44100;

        // ================================================================
        // BGM生成
        // ================================================================

        /// <summary>メインメニュー用BGM（ゆったりしたアルペジオ、8秒ループ）</summary>
        public static AudioClip GenerateMenuBGM()
        {
            float duration = 8f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            // C major -> Am -> F -> G のコード進行（各2秒）
            float[][] chords = new float[][]
            {
                new float[] { 261.63f, 329.63f, 392.00f },  // C major
                new float[] { 220.00f, 261.63f, 329.63f },  // Am
                new float[] { 349.23f, 440.00f, 523.25f },  // F major
                new float[] { 392.00f, 493.88f, 587.33f },  // G major
            };

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                int chordIndex = Mathf.FloorToInt(t / 2f) % chords.Length;
                float[] chord = chords[chordIndex];

                float localT = (t % 2f);

                // アルペジオ: 各音を順番に鳴らす
                float val = 0f;
                for (int n = 0; n < chord.Length; n++)
                {
                    float noteStart = n * 0.3f;
                    float noteT = localT - noteStart;
                    if (noteT < 0f) continue;

                    float env = Mathf.Exp(-noteT * 1.2f); // 減衰エンベロープ
                    val += Mathf.Sin(2f * Mathf.PI * chord[n] * noteT) * env * 0.15f;
                    // オクターブ上をかすかに加える
                    val += Mathf.Sin(2f * Mathf.PI * chord[n] * 2f * noteT) * env * 0.05f;
                }

                // パッド（和音の持続音）
                for (int n = 0; n < chord.Length; n++)
                {
                    val += Mathf.Sin(2f * Mathf.PI * chord[n] * 0.5f * t) * 0.04f;
                }

                data[i] = Mathf.Clamp(val, -1f, 1f);
            }

            return CreateClip("MenuBGM", data, duration);
        }

        /// <summary>ゲームプレイ用BGM（リズミカルな16秒ループ）</summary>
        public static AudioClip GenerateGameplayBGM()
        {
            float duration = 16f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            // コード進行（各4秒）: C -> F -> Am -> G
            float[][] chords = new float[][]
            {
                new float[] { 130.81f, 164.81f, 196.00f },  // C (低め)
                new float[] { 174.61f, 220.00f, 261.63f },  // F
                new float[] { 110.00f, 130.81f, 164.81f },  // Am
                new float[] { 196.00f, 246.94f, 293.66f },  // G
            };

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                int chordIndex = Mathf.FloorToInt(t / 4f) % chords.Length;
                float[] chord = chords[chordIndex];
                float localT = t % 4f;

                float val = 0f;

                // ベースライン（ルート音）
                float bassFreq = chord[0];
                val += SawWave(bassFreq * t) * 0.08f;

                // リズム（8分音符の刻み）
                float beatT = (t * 4f) % 1f; // 4 beats per second -> 16th note feel
                float beatEnv = beatT < 0.1f ? 1f : Mathf.Exp(-(beatT - 0.1f) * 8f);

                // 8分音符リズムパターン
                float eighthNote = (t * 2f) % 1f;
                float rhythmEnv = eighthNote < 0.05f ? 1f : Mathf.Exp(-(eighthNote - 0.05f) * 12f);

                // パーカッション風（ノイズ）
                val += PseudoNoise(i) * rhythmEnv * 0.06f;

                // コード音（パッド）
                for (int n = 0; n < chord.Length; n++)
                {
                    val += Mathf.Sin(2f * Mathf.PI * chord[n] * t) * 0.06f;
                }

                // メロディライン（4小節パターン）
                float melodyNote = GetMelodyNote(localT);
                if (melodyNote > 0f)
                {
                    float melodyT = localT % 0.5f;
                    float melodyEnv = Mathf.Exp(-melodyT * 3f);
                    val += Mathf.Sin(2f * Mathf.PI * melodyNote * t) * melodyEnv * 0.12f;
                    val += Mathf.Sin(2f * Mathf.PI * melodyNote * 2f * t) * melodyEnv * 0.04f;
                }

                data[i] = Mathf.Clamp(val, -1f, 1f);
            }

            return CreateClip("GameplayBGM", data, duration);
        }

        /// <summary>ゲームオーバー用BGM（しんみりした4秒ループ）</summary>
        public static AudioClip GenerateGameOverBGM()
        {
            float duration = 4f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            // Dm -> Gm の暗いコード
            float[] freqs = { 146.83f, 174.61f, 220.00f, 196.00f, 233.08f, 293.66f };

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float val = 0f;

                int half = t < 2f ? 0 : 3;
                for (int n = 0; n < 3; n++)
                {
                    float env = Mathf.Exp(-t * 0.3f);
                    val += Mathf.Sin(2f * Mathf.PI * freqs[half + n] * 0.5f * t) * env * 0.08f;
                }

                data[i] = Mathf.Clamp(val, -1f, 1f);
            }

            return CreateClip("GameOverBGM", data, duration);
        }

        // ================================================================
        // 環境音生成
        // ================================================================

        /// <summary>群衆のざわめき環境音（4秒ループ）</summary>
        public static AudioClip GenerateCrowdAmbient()
        {
            float duration = 4f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            // ローパスフィルタ付きノイズで群衆感を出す
            float prev = 0f;
            float alpha = 0.02f; // カットオフ低め = こもった音

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float noise = PseudoNoise(i) * 0.3f;

                // ローパスフィルタ
                prev = prev + alpha * (noise - prev);

                // ゆっくりした音量変動（ざわめき感）
                float modulation = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 0.5f * t)
                                       * Mathf.Sin(2f * Mathf.PI * 0.3f * t + 1f);

                data[i] = prev * modulation;
            }

            // ループ境界のクロスフェード
            CrossFade(data, SampleRate / 10);

            return CreateClip("CrowdAmbient", data, duration);
        }

        /// <summary>雨音環境音（4秒ループ）</summary>
        public static AudioClip GenerateRainAmbient()
        {
            float duration = 4f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            float prev = 0f;
            float alpha = 0.08f; // やや高めのカットオフ = サラサラ感

            for (int i = 0; i < samples; i++)
            {
                float noise = PseudoNoise(i + 99999) * 0.25f;
                prev = prev + alpha * (noise - prev);

                // 雨粒のランダムなアクセント
                if (PseudoNoise(i * 7) > 0.97f)
                {
                    prev += 0.15f * PseudoNoise(i + 12345);
                }

                data[i] = Mathf.Clamp(prev, -1f, 1f);
            }

            CrossFade(data, SampleRate / 10);
            return CreateClip("RainAmbient", data, duration);
        }

        // ================================================================
        // SE生成
        // ================================================================

        /// <summary>来場者の歓声SE（0.8秒）</summary>
        public static AudioClip GenerateCheerSE()
        {
            float duration = 0.8f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                // アタック速め、ディケイ緩め
                float env = t < 0.05f ? t / 0.05f : Mathf.Exp(-(t - 0.05f) * 3f);

                // 複数の高周波ノイズ + 声のような正弦波
                float noise = PseudoNoise(i) * 0.3f;
                float voice1 = Mathf.Sin(2f * Mathf.PI * 800f * t) * 0.15f;
                float voice2 = Mathf.Sin(2f * Mathf.PI * 1200f * t + 0.5f) * 0.1f;
                float voice3 = Mathf.Sin(2f * Mathf.PI * 600f * t) * 0.1f;

                data[i] = (noise + voice1 + voice2 + voice3) * env;
            }

            return CreateClip("CheerSE", data, duration);
        }

        /// <summary>アトラクション動作音SE（1.5秒）</summary>
        public static AudioClip GenerateAttractionRideSE()
        {
            float duration = 1.5f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float env = t < 0.1f ? t / 0.1f : (t < 1.2f ? 1f : Mathf.Exp(-(t - 1.2f) * 5f));

                // 上昇する周波数（ジェットコースター風）
                float freq = 100f + 400f * (t / duration);
                float val = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.2f;

                // 機械的な振動
                val += Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.1f;

                // 風切り音（ノイズ）
                val += PseudoNoise(i) * 0.08f * (t / duration);

                data[i] = val * env;
            }

            return CreateClip("AttractionRideSE", data, duration);
        }

        /// <summary>お金のSE: チャリン（0.3秒）</summary>
        public static AudioClip GenerateCashSE()
        {
            float duration = 0.3f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 15f);

                // 高い金属音
                float val = Mathf.Sin(2f * Mathf.PI * 2400f * t) * 0.2f;
                val += Mathf.Sin(2f * Mathf.PI * 3600f * t) * 0.15f;
                val += Mathf.Sin(2f * Mathf.PI * 4800f * t) * 0.08f;

                data[i] = val * env;
            }

            return CreateClip("CashSE", data, duration);
        }

        /// <summary>UIボタンクリックSE（0.08秒）</summary>
        public static AudioClip GenerateClickSE()
        {
            float duration = 0.08f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 60f);
                data[i] = Mathf.Sin(2f * Mathf.PI * 1000f * t) * env * 0.3f;
            }

            return CreateClip("ClickSE", data, duration);
        }

        /// <summary>故障アラームSE（1.2秒）</summary>
        public static AudioClip GenerateBreakdownAlarmSE()
        {
            float duration = 1.2f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                // 2音交互（サイレン風）
                float freq = ((int)(t * 6f) % 2 == 0) ? 440f : 660f;
                float env = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 3f * t);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.25f * env;
            }

            return CreateClip("BreakdownAlarmSE", data, duration);
        }

        /// <summary>事故アラームSE（2秒、より激しい）</summary>
        public static AudioClip GenerateAccidentAlarmSE()
        {
            float duration = 2f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                // 3音で急いだサイレン
                float freq = ((int)(t * 8f) % 3) switch
                {
                    0 => 520f,
                    1 => 780f,
                    _ => 660f
                };
                float env = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 4f * t);
                float noise = PseudoNoise(i) * 0.05f;
                data[i] = (Mathf.Sin(2f * Mathf.PI * freq * t) * 0.3f + noise) * env;
            }

            return CreateClip("AccidentAlarmSE", data, duration);
        }

        /// <summary>建設完了SE（0.6秒、上昇アルペジオ）</summary>
        public static AudioClip GenerateBuildCompleteSE()
        {
            float duration = 0.6f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.50f }; // C5, E5, G5, C6

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float val = 0f;

                for (int n = 0; n < notes.Length; n++)
                {
                    float noteStart = n * 0.12f;
                    float noteT = t - noteStart;
                    if (noteT < 0f) continue;

                    float env = Mathf.Exp(-noteT * 5f);
                    val += Mathf.Sin(2f * Mathf.PI * notes[n] * noteT) * env * 0.2f;
                }

                data[i] = Mathf.Clamp(val, -1f, 1f);
            }

            return CreateClip("BuildCompleteSE", data, duration);
        }

        /// <summary>ゴールデンチケット獲得SE（1.2秒、ファンファーレ）</summary>
        public static AudioClip GenerateGoldenTicketSE()
        {
            float duration = 1.2f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            // ファンファーレ: C -> E -> G -> C(oct)
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.50f };
            float[] starts = { 0f, 0.2f, 0.4f, 0.6f };

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float val = 0f;

                for (int n = 0; n < notes.Length; n++)
                {
                    float noteT = t - starts[n];
                    if (noteT < 0f) continue;

                    float env = noteT < 0.02f ? noteT / 0.02f : Mathf.Exp(-noteT * 2.5f);
                    // ブラス風（基音 + 倍音強め）
                    val += Mathf.Sin(2f * Mathf.PI * notes[n] * noteT) * env * 0.15f;
                    val += Mathf.Sin(2f * Mathf.PI * notes[n] * 2f * noteT) * env * 0.08f;
                    val += Mathf.Sin(2f * Mathf.PI * notes[n] * 3f * noteT) * env * 0.04f;
                }

                data[i] = Mathf.Clamp(val, -1f, 1f);
            }

            return CreateClip("GoldenTicketSE", data, duration);
        }

        /// <summary>VIP到着SE（0.8秒、華やかな音）</summary>
        public static AudioClip GenerateVIPArriveSE()
        {
            float duration = 0.8f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float env = t < 0.03f ? t / 0.03f : Mathf.Exp(-t * 3f);

                // 和音（明るい）
                float val = Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.12f;
                val += Mathf.Sin(2f * Mathf.PI * 1108.73f * t) * 0.1f; // C#6
                val += Mathf.Sin(2f * Mathf.PI * 1318.51f * t) * 0.08f; // E6

                // きらめき効果
                val += Mathf.Sin(2f * Mathf.PI * 3000f * t) * Mathf.Exp(-t * 20f) * 0.1f;

                data[i] = val * env;
            }

            return CreateClip("VIPArriveSE", data, duration);
        }

        /// <summary>嘔吐SE（0.6秒）</summary>
        public static AudioClip GenerateVomitSE()
        {
            float duration = 0.6f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            float prev = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float env = t < 0.05f ? t / 0.05f : (t < 0.3f ? 1f : Mathf.Exp(-(t - 0.3f) * 5f));

                // 低いうなり + ノイズ
                float val = Mathf.Sin(2f * Mathf.PI * 80f * t) * 0.2f;
                val += Mathf.Sin(2f * Mathf.PI * 120f * t + Mathf.Sin(t * 30f)) * 0.15f;
                val += PseudoNoise(i) * 0.1f;

                // ローパス
                prev = prev + 0.05f * (val - prev);
                data[i] = prev * env;
            }

            return CreateClip("VomitSE", data, duration);
        }

        /// <summary>研究完了SE（0.8秒、ひらめき音）</summary>
        public static AudioClip GenerateResearchCompleteSE()
        {
            float duration = 0.8f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;

                // 上昇グリッサンド
                float freq = 400f + 1200f * (t / duration);
                float env = t < 0.02f ? t / 0.02f : Mathf.Exp(-t * 2f);
                float val = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.2f;

                // きらめき
                val += Mathf.Sin(2f * Mathf.PI * 2000f * t) * Mathf.Exp(-t * 8f) * 0.1f;

                data[i] = val;
            }

            return CreateClip("ResearchCompleteSE", data, duration);
        }

        /// <summary>天候変化SE（0.5秒、風の音）</summary>
        public static AudioClip GenerateWeatherChangeSE()
        {
            float duration = 0.5f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            float prev = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Sin(Mathf.PI * t / duration); // 山型エンベロープ

                float noise = PseudoNoise(i + 77777) * 0.3f;
                prev = prev + 0.03f * (noise - prev);

                data[i] = prev * env;
            }

            return CreateClip("WeatherChangeSE", data, duration);
        }

        /// <summary>未来都市エリアBGM（シンセウェーブ風16秒ループ）</summary>
        public static AudioClip GenerateFutureCityBGM()
        {
            float duration = 16f;
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];

            // SFコード進行: Am -> Dm -> Em -> Am
            float[][] chords = new float[][]
            {
                new float[] { 220.00f, 261.63f, 329.63f },  // Am
                new float[] { 146.83f, 174.61f, 220.00f },  // Dm
                new float[] { 164.81f, 196.00f, 246.94f },  // Em
                new float[] { 220.00f, 261.63f, 329.63f },  // Am
            };

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                int chordIndex = Mathf.FloorToInt(t / 4f) % chords.Length;
                float[] chord = chords[chordIndex];

                float val = 0f;

                // シンセベース（オクーブ下のパルス波）
                float bassFreq = chord[0] * 0.5f;
                float pulse = Mathf.Sin(2f * Mathf.PI * bassFreq * t) > 0f ? 0.08f : -0.08f;
                val += pulse * 0.5f;

                // アルペジオ（16分音符風）
                float arpT = (t * 4f) % 1f;
                int arpNote = Mathf.FloorToInt(arpT * 4f) % 3;
                float arpFreq = chord[arpNote] * 2f;
                float arpEnv = Mathf.Exp(-((arpT * 4f) % 1f) * 6f);
                val += Mathf.Sin(2f * Mathf.PI * arpFreq * t) * arpEnv * 0.1f;

                // パッド（LFO付きコード）
                float lfo = 1f + 0.3f * Mathf.Sin(2f * Mathf.PI * 0.25f * t);
                for (int n = 0; n < chord.Length; n++)
                {
                    val += Mathf.Sin(2f * Mathf.PI * chord[n] * lfo * t) * 0.04f;
                }

                // ハイハット風リズム
                float beat16 = (t * 8f) % 1f;
                if (beat16 < 0.02f)
                    val += PseudoNoise(i) * 0.05f;

                data[i] = Mathf.Clamp(val, -1f, 1f);
            }

            CrossFade(data, SampleRate / 2);
            return CreateClip("FutureCityBGM", data, duration);
        }

        // ================================================================
        // ヘルパー
        // ================================================================

        /// <summary>メロディの音程を返す（0=無音）</summary>
        private static float GetMelodyNote(float localT)
        {
            // 4秒のメロディパターン（0.5秒ごとに1音）
            int noteIndex = Mathf.FloorToInt(localT / 0.5f) % 8;
            float[] melody = {
                523.25f, 587.33f, 659.25f, 523.25f,  // C5 D5 E5 C5
                659.25f, 698.46f, 783.99f, 0f          // E5 F5 G5 (rest)
            };
            return melody[noteIndex];
        }

        /// <summary>ノコギリ波（-1〜1）</summary>
        private static float SawWave(float phase)
        {
            return 2f * (phase - Mathf.Floor(phase + 0.5f));
        }

        /// <summary>決定論的な疑似ノイズ（-1〜1）</summary>
        private static float PseudoNoise(int seed)
        {
            // シンプルなハッシュ関数
            seed = (seed << 13) ^ seed;
            seed = seed * (seed * seed * 15731 + 789221) + 1376312589;
            return 1.0f - (seed & 0x7fffffff) / 1073741824.0f;
        }

        /// <summary>ループ用クロスフェード</summary>
        private static void CrossFade(float[] data, int fadeSamples)
        {
            if (fadeSamples > data.Length / 2) fadeSamples = data.Length / 4;

            for (int i = 0; i < fadeSamples; i++)
            {
                float fade = (float)i / fadeSamples;
                int endIndex = data.Length - fadeSamples + i;
                data[i] = data[i] * fade + data[endIndex] * (1f - fade);
            }
        }

        /// <summary>AudioClipを生成する</summary>
        private static AudioClip CreateClip(string name, float[] data, float duration)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
