// ============================================================
// ThemeParkGame - ProceduralTextureGenerator
// プロシージャルテクスチャ生成ユーティリティ
// ランタイムでTexture2Dを生成し、マテリアルに適用する
// WebGL対応 - Shader不使用、純粋なCPUテクスチャ生成
// ============================================================

using UnityEngine;

namespace ThemeParkGame.Core
{
    /// <summary>
    /// プロシージャルテクスチャをCPU上で生成する。
    /// WebGL対応のため、ComputeShaderやカスタムShaderを使わず
    /// Texture2D.SetPixels32で直接ピクセルを書き込む。
    /// テクスチャサイズは64x64または128x128に抑えてメモリ節約。
    /// </summary>
    public static class ProceduralTextureGenerator
    {
        private const int TEX_SIZE = 128;
        private const int TEX_SMALL = 64;

        // キャッシュされたテクスチャ（2回目以降は再生成しない）
        private static Texture2D _brick;
        private static Texture2D _metal;
        private static Texture2D _grass;
        private static Texture2D _wood;
        private static Texture2D _concrete;
        private static Texture2D _tile;
        private static Texture2D _stripe;
        private static Texture2D _checker;

        /// <summary>レンガテクスチャ</summary>
        public static Texture2D Brick
        {
            get
            {
                if (_brick == null) _brick = GenerateBrick();
                return _brick;
            }
        }

        /// <summary>金属テクスチャ</summary>
        public static Texture2D Metal
        {
            get
            {
                if (_metal == null) _metal = GenerateMetal();
                return _metal;
            }
        }

        /// <summary>芝生テクスチャ</summary>
        public static Texture2D Grass
        {
            get
            {
                if (_grass == null) _grass = GenerateGrass();
                return _grass;
            }
        }

        /// <summary>木材テクスチャ</summary>
        public static Texture2D Wood
        {
            get
            {
                if (_wood == null) _wood = GenerateWood();
                return _wood;
            }
        }

        /// <summary>コンクリートテクスチャ</summary>
        public static Texture2D Concrete
        {
            get
            {
                if (_concrete == null) _concrete = GenerateConcrete();
                return _concrete;
            }
        }

        /// <summary>タイルテクスチャ</summary>
        public static Texture2D Tile
        {
            get
            {
                if (_tile == null) _tile = GenerateTile();
                return _tile;
            }
        }

        /// <summary>ストライプテクスチャ</summary>
        public static Texture2D Stripe
        {
            get
            {
                if (_stripe == null) _stripe = GenerateStripe();
                return _stripe;
            }
        }

        /// <summary>チェッカーテクスチャ</summary>
        public static Texture2D Checker
        {
            get
            {
                if (_checker == null) _checker = GenerateChecker();
                return _checker;
            }
        }

        // ============================================================
        // レンガ
        // ============================================================

        private static Texture2D GenerateBrick()
        {
            var tex = new Texture2D(TEX_SIZE, TEX_SIZE, TextureFormat.RGBA32, false);
            var pixels = new Color32[TEX_SIZE * TEX_SIZE];

            Color32 brickColor1 = new Color32(178, 80, 50, 255);
            Color32 brickColor2 = new Color32(165, 72, 45, 255);
            Color32 brickColor3 = new Color32(190, 88, 55, 255);
            Color32 mortarColor = new Color32(210, 200, 185, 255);

            int brickW = 32;
            int brickH = 16;
            int mortarW = 2;

            for (int y = 0; y < TEX_SIZE; y++)
            {
                for (int x = 0; x < TEX_SIZE; x++)
                {
                    int row = y / brickH;
                    int offset = (row % 2 == 0) ? 0 : brickW / 2;
                    int lx = (x + offset) % brickW;
                    int ly = y % brickH;

                    bool isMortar = lx < mortarW || ly < mortarW;

                    if (isMortar)
                    {
                        pixels[y * TEX_SIZE + x] = mortarColor;
                    }
                    else
                    {
                        // 少しノイズを加えてリアルに
                        float noise = SimpleNoise(x * 0.1f, y * 0.1f);
                        Color32 bc = (noise > 0.6f) ? brickColor3 :
                                     (noise > 0.3f) ? brickColor1 : brickColor2;
                        // 微細なバリエーション
                        byte r = (byte)Mathf.Clamp(bc.r + (int)(noise * 20f - 10f), 0, 255);
                        byte g = (byte)Mathf.Clamp(bc.g + (int)(noise * 15f - 7f), 0, 255);
                        byte b = (byte)Mathf.Clamp(bc.b + (int)(noise * 15f - 7f), 0, 255);
                        pixels[y * TEX_SIZE + x] = new Color32(r, g, b, 255);
                    }
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.name = "ProceduralBrick";
            return tex;
        }

        // ============================================================
        // 金属
        // ============================================================

        private static Texture2D GenerateMetal()
        {
            var tex = new Texture2D(TEX_SIZE, TEX_SIZE, TextureFormat.RGBA32, false);
            var pixels = new Color32[TEX_SIZE * TEX_SIZE];

            for (int y = 0; y < TEX_SIZE; y++)
            {
                for (int x = 0; x < TEX_SIZE; x++)
                {
                    // ブラシヘアライン効果（横方向のストリーク）
                    float streak = SimpleNoise(x * 0.02f, y * 2f);
                    float baseGrey = 0.65f + streak * 0.15f;

                    // 微細なスポットノイズ
                    float spot = SimpleNoise(x * 0.5f, y * 0.5f);
                    baseGrey += (spot - 0.5f) * 0.05f;

                    // リベットのようなディンプル
                    bool rivet = (x % 32 < 3 && y % 32 < 3);
                    if (rivet) baseGrey *= 0.8f;

                    byte v = (byte)Mathf.Clamp(baseGrey * 255f, 0, 255);
                    pixels[y * TEX_SIZE + x] = new Color32(v, v, (byte)Mathf.Min(v + 10, 255), 255);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.name = "ProceduralMetal";
            return tex;
        }

        // ============================================================
        // 芝生
        // ============================================================

        private static Texture2D GenerateGrass()
        {
            var tex = new Texture2D(TEX_SIZE, TEX_SIZE, TextureFormat.RGBA32, false);
            var pixels = new Color32[TEX_SIZE * TEX_SIZE];

            for (int y = 0; y < TEX_SIZE; y++)
            {
                for (int x = 0; x < TEX_SIZE; x++)
                {
                    float n1 = SimpleNoise(x * 0.08f, y * 0.08f);
                    float n2 = SimpleNoise(x * 0.3f, y * 0.3f);
                    float n3 = SimpleNoise(x * 0.6f + 100f, y * 0.6f + 100f);

                    float green = 0.40f + n1 * 0.25f + n2 * 0.1f;
                    float red = green * 0.55f + n3 * 0.05f;
                    float blue = green * 0.25f;

                    // 草の葉のような縦ストリーク
                    if (n3 > 0.7f)
                    {
                        green += 0.1f;
                        red -= 0.02f;
                    }

                    byte r = (byte)Mathf.Clamp(red * 255f, 0, 255);
                    byte g = (byte)Mathf.Clamp(green * 255f, 0, 255);
                    byte b = (byte)Mathf.Clamp(blue * 255f, 0, 255);
                    pixels[y * TEX_SIZE + x] = new Color32(r, g, b, 255);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.name = "ProceduralGrass";
            return tex;
        }

        // ============================================================
        // 木材
        // ============================================================

        private static Texture2D GenerateWood()
        {
            var tex = new Texture2D(TEX_SIZE, TEX_SIZE, TextureFormat.RGBA32, false);
            var pixels = new Color32[TEX_SIZE * TEX_SIZE];

            for (int y = 0; y < TEX_SIZE; y++)
            {
                for (int x = 0; x < TEX_SIZE; x++)
                {
                    // 木目パターン: 年輪のような同心円風
                    float dist = Mathf.Sqrt((x - 64f) * (x - 64f) * 0.01f + (y - 64f) * (y - 64f) * 0.5f);
                    float ring = Mathf.Sin(dist * 0.8f + SimpleNoise(x * 0.05f, y * 0.05f) * 5f);
                    ring = ring * 0.5f + 0.5f;

                    float grain = SimpleNoise(x * 0.03f, y * 0.5f) * 0.2f;

                    float baseR = 0.55f + ring * 0.15f + grain;
                    float baseG = 0.38f + ring * 0.12f + grain * 0.8f;
                    float baseB = 0.22f + ring * 0.08f + grain * 0.5f;

                    byte r = (byte)Mathf.Clamp(baseR * 255f, 0, 255);
                    byte g = (byte)Mathf.Clamp(baseG * 255f, 0, 255);
                    byte b = (byte)Mathf.Clamp(baseB * 255f, 0, 255);
                    pixels[y * TEX_SIZE + x] = new Color32(r, g, b, 255);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.name = "ProceduralWood";
            return tex;
        }

        // ============================================================
        // コンクリート
        // ============================================================

        private static Texture2D GenerateConcrete()
        {
            var tex = new Texture2D(TEX_SMALL, TEX_SMALL, TextureFormat.RGBA32, false);
            var pixels = new Color32[TEX_SMALL * TEX_SMALL];

            for (int y = 0; y < TEX_SMALL; y++)
            {
                for (int x = 0; x < TEX_SMALL; x++)
                {
                    float n = SimpleNoise(x * 0.15f, y * 0.15f);
                    float v = 0.72f + n * 0.12f;
                    float n2 = SimpleNoise(x * 0.6f + 50f, y * 0.6f + 50f);
                    v += (n2 - 0.5f) * 0.05f;

                    byte bv = (byte)Mathf.Clamp(v * 255f, 0, 255);
                    pixels[y * TEX_SMALL + x] = new Color32(bv, bv, (byte)Mathf.Min(bv + 3, 255), 255);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.name = "ProceduralConcrete";
            return tex;
        }

        // ============================================================
        // タイル
        // ============================================================

        private static Texture2D GenerateTile()
        {
            var tex = new Texture2D(TEX_SIZE, TEX_SIZE, TextureFormat.RGBA32, false);
            var pixels = new Color32[TEX_SIZE * TEX_SIZE];

            Color32 tileColor = new Color32(230, 230, 235, 255);
            Color32 groutColor = new Color32(195, 195, 190, 255);

            int tileSize = 16;
            int groutWidth = 1;

            for (int y = 0; y < TEX_SIZE; y++)
            {
                for (int x = 0; x < TEX_SIZE; x++)
                {
                    int lx = x % tileSize;
                    int ly = y % tileSize;

                    if (lx < groutWidth || ly < groutWidth)
                    {
                        pixels[y * TEX_SIZE + x] = groutColor;
                    }
                    else
                    {
                        float n = SimpleNoise(x * 0.3f, y * 0.3f);
                        byte r = (byte)Mathf.Clamp(tileColor.r + (int)(n * 10f - 5f), 0, 255);
                        byte g = (byte)Mathf.Clamp(tileColor.g + (int)(n * 10f - 5f), 0, 255);
                        byte b = (byte)Mathf.Clamp(tileColor.b + (int)(n * 10f - 5f), 0, 255);
                        pixels[y * TEX_SIZE + x] = new Color32(r, g, b, 255);
                    }
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.name = "ProceduralTile";
            return tex;
        }

        // ============================================================
        // ストライプ（テント・ひさし用）
        // ============================================================

        private static Texture2D GenerateStripe()
        {
            var tex = new Texture2D(TEX_SMALL, TEX_SMALL, TextureFormat.RGBA32, false);
            var pixels = new Color32[TEX_SMALL * TEX_SMALL];

            Color32 col1 = new Color32(220, 40, 50, 255); // 赤
            Color32 col2 = new Color32(245, 245, 240, 255); // 白

            int stripeWidth = 8;

            for (int y = 0; y < TEX_SMALL; y++)
            {
                for (int x = 0; x < TEX_SMALL; x++)
                {
                    bool isStripe1 = ((x + y) / stripeWidth) % 2 == 0;
                    pixels[y * TEX_SMALL + x] = isStripe1 ? col1 : col2;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.name = "ProceduralStripe";
            return tex;
        }

        // ============================================================
        // チェッカーボード
        // ============================================================

        private static Texture2D GenerateChecker()
        {
            var tex = new Texture2D(TEX_SMALL, TEX_SMALL, TextureFormat.RGBA32, false);
            var pixels = new Color32[TEX_SMALL * TEX_SMALL];

            int checkSize = 8;
            Color32 col1 = new Color32(240, 240, 240, 255);
            Color32 col2 = new Color32(40, 40, 45, 255);

            for (int y = 0; y < TEX_SMALL; y++)
            {
                for (int x = 0; x < TEX_SMALL; x++)
                {
                    bool isA = ((x / checkSize) + (y / checkSize)) % 2 == 0;
                    pixels[y * TEX_SMALL + x] = isA ? col1 : col2;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.name = "ProceduralChecker";
            return tex;
        }

        // ============================================================
        // マテリアル生成ヘルパー
        // ============================================================

        /// <summary>テクスチャ付きStandardマテリアルを作成</summary>
        public static Material CreateTexturedMaterial(Texture2D texture, Color tint,
            float metallic = 0f, float smoothness = 0.5f, float tileScale = 1f)
        {
            var shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("UI/Default");
            if (shader == null) return new Material(Shader.Find("Sprites/Default")) { color = tint };

            var mat = new Material(shader);
            mat.color = tint;
            mat.mainTexture = texture;
            mat.mainTextureScale = new Vector2(tileScale, tileScale);

            if (shader.name == "Standard")
            {
                mat.SetFloat("_Metallic", metallic);
                mat.SetFloat("_Glossiness", smoothness);
            }

            return mat;
        }

        /// <summary>地面用の芝生マテリアルを作成</summary>
        public static Material CreateGroundMaterial()
        {
            return CreateTexturedMaterial(Grass, new Color(0.85f, 1f, 0.85f), 0f, 0.2f, 10f);
        }

        /// <summary>通路用のコンクリートマテリアルを作成</summary>
        public static Material CreatePathMaterial()
        {
            return CreateTexturedMaterial(Concrete, new Color(0.9f, 0.88f, 0.85f), 0f, 0.3f, 5f);
        }

        /// <summary>レンガ壁マテリアルを作成</summary>
        public static Material CreateBrickMaterial()
        {
            return CreateTexturedMaterial(Brick, Color.white, 0f, 0.25f, 2f);
        }

        /// <summary>金属マテリアルを作成</summary>
        public static Material CreateMetalMaterial(Color tint)
        {
            return CreateTexturedMaterial(Metal, tint, 0.75f, 0.7f, 1f);
        }

        /// <summary>木材マテリアルを作成</summary>
        public static Material CreateWoodMaterial()
        {
            return CreateTexturedMaterial(Wood, Color.white, 0f, 0.3f, 2f);
        }

        // ============================================================
        // ノイズ関数
        // ============================================================

        /// <summary>シンプルなバリューノイズ（0〜1）</summary>
        private static float SimpleNoise(float x, float y)
        {
            // 整数ハッシュベースの擬似ノイズ
            int ix = Mathf.FloorToInt(x);
            int iy = Mathf.FloorToInt(y);
            float fx = x - ix;
            float fy = y - iy;

            // Smoothstep
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);

            float n00 = Hash(ix, iy);
            float n10 = Hash(ix + 1, iy);
            float n01 = Hash(ix, iy + 1);
            float n11 = Hash(ix + 1, iy + 1);

            float nx0 = Mathf.Lerp(n00, n10, fx);
            float nx1 = Mathf.Lerp(n01, n11, fx);
            return Mathf.Lerp(nx0, nx1, fy);
        }

        /// <summary>整数座標ハッシュ（0〜1のfloatを返す）</summary>
        private static float Hash(int x, int y)
        {
            int h = x * 374761393 + y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return (h & 0x7FFFFFFF) / (float)0x7FFFFFFF;
        }
    }
}
