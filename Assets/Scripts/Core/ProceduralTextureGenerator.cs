// ============================================================
// ThemeParkGame - ProceduralTextureGenerator v3
// 高品質プロシージャルテクスチャ + 法線マップ + PBRマテリアル
// FBMノイズ・Sobelフィルター法線マップ・キャッシュ済みマテリアル
// ============================================================

using UnityEngine;

namespace ThemeParkGame.Core
{
    /// <summary>
    /// 高品質プロシージャルテクスチャをCPU上で生成する。
    /// FBM(Fractal Brownian Motion)ノイズ、法線マップ自動生成、
    /// PBRマテリアルキャッシュを提供。WebGL対応。
    /// </summary>
    public static class ProceduralTextureGenerator
    {
        private const int TEX_SIZE = 256;
        private const int TEX_SMALL = 128;
        private const int TEX_TINY = 64;

        // ============================================================
        // キャッシュ: アルベドテクスチャ
        // ============================================================

        private static Texture2D _brick, _metal, _grass, _wood;
        private static Texture2D _concrete, _tile, _stripe, _checker;
        private static Texture2D _stucco, _roofTile;

        // キャッシュ: 法線マップ
        private static Texture2D _brickN, _metalN, _grassN, _woodN;
        private static Texture2D _concreteN, _tileN, _stuccoN, _roofTileN;

        // キャッシュ: PBRマテリアル
        private static Material _brickWallMat, _metalRideMat, _woodPanelMat;
        private static Material _tileFloorMat, _grassGroundMat, _concretePathMat;
        private static Material _stuccoWallMat, _roofTileMat, _glassMat;

        // キャッシュ: カスタムシェーダーマテリアル
        private static Material _toonDefaultMat, _waterMat, _foliageMat;
        private static Material _emissiveYellowMat, _emissiveRedMat, _emissiveBlueMat;

        // ============================================================
        // アルベドテクスチャ プロパティ
        // ============================================================

        public static Texture2D Brick    { get { if (_brick == null) _brick = GenerateBrick(); return _brick; } }
        public static Texture2D Metal    { get { if (_metal == null) _metal = GenerateMetal(); return _metal; } }
        public static Texture2D Grass    { get { if (_grass == null) _grass = GenerateGrass(); return _grass; } }
        public static Texture2D Wood     { get { if (_wood == null) _wood = GenerateWood(); return _wood; } }
        public static Texture2D Concrete { get { if (_concrete == null) _concrete = GenerateConcrete(); return _concrete; } }
        public static Texture2D Tile     { get { if (_tile == null) _tile = GenerateTile(); return _tile; } }
        public static Texture2D Stripe   { get { if (_stripe == null) _stripe = GenerateStripe(); return _stripe; } }
        public static Texture2D Checker  { get { if (_checker == null) _checker = GenerateChecker(); return _checker; } }
        public static Texture2D Stucco   { get { if (_stucco == null) _stucco = GenerateStucco(); return _stucco; } }
        public static Texture2D RoofTileTex { get { if (_roofTile == null) _roofTile = GenerateRoofTile(); return _roofTile; } }

        // ============================================================
        // 法線マップ プロパティ
        // ============================================================

        public static Texture2D BrickNormal    { get { if (_brickN == null) _brickN = GenerateNormalMap(Brick, 2.5f); return _brickN; } }
        public static Texture2D MetalNormal    { get { if (_metalN == null) _metalN = GenerateNormalMap(Metal, 0.6f); return _metalN; } }
        public static Texture2D GrassNormal    { get { if (_grassN == null) _grassN = GenerateNormalMap(Grass, 1.2f); return _grassN; } }
        public static Texture2D WoodNormal     { get { if (_woodN == null) _woodN = GenerateNormalMap(Wood, 1.8f); return _woodN; } }
        public static Texture2D ConcreteNormal { get { if (_concreteN == null) _concreteN = GenerateNormalMap(Concrete, 0.8f); return _concreteN; } }
        public static Texture2D TileNormal     { get { if (_tileN == null) _tileN = GenerateNormalMap(Tile, 2.0f); return _tileN; } }
        public static Texture2D StuccoNormal   { get { if (_stuccoN == null) _stuccoN = GenerateNormalMap(Stucco, 1.5f); return _stuccoN; } }
        public static Texture2D RoofTileNormal { get { if (_roofTileN == null) _roofTileN = GenerateNormalMap(RoofTileTex, 2.5f); return _roofTileN; } }

        // ============================================================
        // PBRマテリアル プロパティ
        // ============================================================

        /// <summary>レンガ壁マテリアル（法線マップ付き）</summary>
        public static Material BrickWallMaterial
        {
            get { if (_brickWallMat == null) _brickWallMat = CreatePBRMaterial(Brick, BrickNormal, new Color(1f, 0.95f, 0.92f), 0f, 0.25f, 3f); return _brickWallMat; }
        }

        /// <summary>金属ライドマテリアル（法線マップ付き・メタリック）</summary>
        public static Material MetalRideMaterial
        {
            get { if (_metalRideMat == null) _metalRideMat = CreatePBRMaterial(Metal, MetalNormal, new Color(0.85f, 0.85f, 0.9f), 0.8f, 0.7f, 2f); return _metalRideMat; }
        }

        /// <summary>木材パネルマテリアル（法線マップ付き）</summary>
        public static Material WoodPanelMaterial
        {
            get { if (_woodPanelMat == null) _woodPanelMat = CreatePBRMaterial(Wood, WoodNormal, new Color(1f, 0.95f, 0.9f), 0f, 0.3f, 2f); return _woodPanelMat; }
        }

        /// <summary>タイル床マテリアル（法線マップ付き）</summary>
        public static Material TileFloorMaterial
        {
            get { if (_tileFloorMat == null) _tileFloorMat = CreatePBRMaterial(Tile, TileNormal, Color.white, 0f, 0.6f, 4f); return _tileFloorMat; }
        }

        /// <summary>芝生地面マテリアル（法線マップ付き）</summary>
        public static Material GrassGroundMaterial
        {
            get { if (_grassGroundMat == null) _grassGroundMat = CreatePBRMaterial(Grass, GrassNormal, new Color(0.9f, 1f, 0.9f), 0f, 0.15f, 12f); return _grassGroundMat; }
        }

        /// <summary>コンクリート通路マテリアル（法線マップ付き）</summary>
        public static Material ConcretePathMaterial
        {
            get { if (_concretePathMat == null) _concretePathMat = CreatePBRMaterial(Concrete, ConcreteNormal, new Color(0.92f, 0.90f, 0.88f), 0f, 0.25f, 8f); return _concretePathMat; }
        }

        /// <summary>スタッコ壁マテリアル（法線マップ付き・カラーティント可能）</summary>
        public static Material StuccoWallMaterial
        {
            get { if (_stuccoWallMat == null) _stuccoWallMat = CreatePBRMaterial(Stucco, StuccoNormal, Color.white, 0f, 0.2f, 3f); return _stuccoWallMat; }
        }

        /// <summary>屋根瓦マテリアル（法線マップ付き）</summary>
        public static Material RoofTileMaterial
        {
            get { if (_roofTileMat == null) _roofTileMat = CreatePBRMaterial(RoofTileTex, RoofTileNormal, new Color(0.85f, 0.35f, 0.2f), 0f, 0.35f, 4f); return _roofTileMat; }
        }

        /// <summary>ガラスマテリアル（半透明・高スムーズネス）</summary>
        public static Material GlassMaterial
        {
            get { if (_glassMat == null) _glassMat = CreateGlassMat(); return _glassMat; }
        }

        // ============================================================
        // 法線マップ生成（Sobelフィルター）
        // ============================================================

        /// <summary>
        /// アルベドテクスチャから法線マップを生成する。
        /// Sobelフィルターで勾配を計算し、タンジェントスペース法線に変換。
        /// </summary>
        public static Texture2D GenerateNormalMap(Texture2D source, float strength = 1f)
        {
            int w = source.width, h = source.height;
            var normal = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var srcPixels = source.GetPixels();
            var dstPixels = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // Sobelフィルター: 3x3カーネルで勾配計算
                    float tl = Luminance(srcPixels, w, h, x - 1, y - 1);
                    float t  = Luminance(srcPixels, w, h, x,     y - 1);
                    float tr = Luminance(srcPixels, w, h, x + 1, y - 1);
                    float l  = Luminance(srcPixels, w, h, x - 1, y);
                    float r  = Luminance(srcPixels, w, h, x + 1, y);
                    float bl = Luminance(srcPixels, w, h, x - 1, y + 1);
                    float b  = Luminance(srcPixels, w, h, x,     y + 1);
                    float br = Luminance(srcPixels, w, h, x + 1, y + 1);

                    float dx = (tr + 2f * r + br) - (tl + 2f * l + bl);
                    float dy = (bl + 2f * b + br) - (tl + 2f * t + tr);

                    dx *= strength;
                    dy *= strength;

                    Vector3 n = new Vector3(-dx, -dy, 1f).normalized;

                    // 法線を0-1にパック
                    dstPixels[y * w + x] = new Color(
                        n.x * 0.5f + 0.5f,
                        n.y * 0.5f + 0.5f,
                        n.z * 0.5f + 0.5f,
                        1f
                    );
                }
            }

            normal.SetPixels(dstPixels);
            normal.Apply();
            normal.wrapMode = TextureWrapMode.Repeat;
            normal.filterMode = FilterMode.Bilinear;
            normal.name = source.name + "_Normal";
            return normal;
        }

        private static float Luminance(Color[] pixels, int w, int h, int x, int y)
        {
            x = ((x % w) + w) % w;
            y = ((y % h) + h) % h;
            Color c = pixels[y * w + x];
            return c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
        }

        // ============================================================
        // PBRマテリアル生成
        // ============================================================

        /// <summary>
        /// Standard Shader PBRマテリアルを生成する。
        /// アルベド + 法線マップ + メタリック/スムーズネス設定。
        /// </summary>
        public static Material CreatePBRMaterial(Texture2D albedo, Texture2D normalMap,
            Color tint, float metallic, float smoothness, float tileScale)
        {
            var shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("UI/Default");
            if (shader == null) return new Material(Shader.Find("Sprites/Default")) { color = tint };

            var mat = new Material(shader);
            mat.color = tint;

            if (albedo != null)
            {
                mat.mainTexture = albedo;
                mat.mainTextureScale = new Vector2(tileScale, tileScale);
            }

            if (shader.name == "Standard")
            {
                mat.SetFloat("_Metallic", metallic);
                mat.SetFloat("_Glossiness", smoothness);

                if (normalMap != null)
                {
                    mat.SetTexture("_BumpMap", normalMap);
                    mat.SetFloat("_BumpScale", 1f);
                    mat.EnableKeyword("_NORMALMAP");
                }
            }

            return mat;
        }

        /// <summary>カラーティント付きPBRマテリアル（テクスチャベースに色を被せる）</summary>
        public static Material CreateTintedPBRMaterial(Texture2D albedo, Texture2D normalMap,
            Color tint, float metallic, float smoothness, float tileScale)
        {
            var mat = CreatePBRMaterial(albedo, normalMap, tint, metallic, smoothness, tileScale);
            return mat;
        }

        private static Material CreateGlassMat()
        {
            var shader = Shader.Find("Standard");
            if (shader == null) return new Material(Shader.Find("Sprites/Default"));

            var mat = new Material(shader);
            mat.color = new Color(0.7f, 0.85f, 0.95f, 0.35f);

            if (shader.name == "Standard")
            {
                mat.SetFloat("_Metallic", 0.1f);
                mat.SetFloat("_Glossiness", 0.95f);
                mat.SetFloat("_Mode", 3);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
            }

            return mat;
        }

        // ============================================================
        // 後方互換ヘルパー
        // ============================================================

        public static Material CreateTexturedMaterial(Texture2D texture, Color tint,
            float metallic = 0f, float smoothness = 0.5f, float tileScale = 1f)
        {
            return CreatePBRMaterial(texture, null, tint, metallic, smoothness, tileScale);
        }

        public static Material CreateGroundMaterial() => GrassGroundMaterial;
        public static Material CreatePathMaterial() => ConcretePathMaterial;
        public static Material CreateBrickMaterial() => BrickWallMaterial;
        public static Material CreateWoodMaterial() => WoodPanelMaterial;

        public static Material CreateMetalMaterial(Color tint)
        {
            return CreatePBRMaterial(Metal, MetalNormal, tint, 0.75f, 0.7f, 2f);
        }

        // ============================================================
        // ノイズ関数
        // ============================================================

        /// <summary>
        /// Fractal Brownian Motion ノイズ（マルチオクターブ）。
        /// 自然な質感表現に最適。
        /// </summary>
        public static float FBM(float x, float y, int octaves = 4,
            float lacunarity = 2f, float persistence = 0.5f)
        {
            float value = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float maxValue = 0f;

            for (int i = 0; i < octaves; i++)
            {
                value += SimpleNoise(x * frequency, y * frequency) * amplitude;
                maxValue += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            return value / maxValue;
        }

        /// <summary>シンプルなバリューノイズ（0〜1）</summary>
        public static float SimpleNoise(float x, float y)
        {
            int ix = Mathf.FloorToInt(x);
            int iy = Mathf.FloorToInt(y);
            float fx = x - ix;
            float fy = y - iy;

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

        private static float Hash(int x, int y)
        {
            int h = x * 374761393 + y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return (h & 0x7FFFFFFF) / (float)0x7FFFFFFF;
        }

        // ============================================================
        // テクスチャ生成: レンガ（FBM改良版）
        // ============================================================

        private static Texture2D GenerateBrick()
        {
            var tex = new Texture2D(TEX_SIZE, TEX_SIZE, TextureFormat.RGBA32, false);
            var pixels = new Color32[TEX_SIZE * TEX_SIZE];

            Color32 brick1 = new Color32(185, 82, 52, 255);
            Color32 brick2 = new Color32(168, 72, 45, 255);
            Color32 brick3 = new Color32(198, 90, 58, 255);
            Color32 brick4 = new Color32(155, 68, 40, 255);
            Color32 mortar = new Color32(215, 205, 190, 255);

            int bW = 64, bH = 32, mW = 3;

            for (int y = 0; y < TEX_SIZE; y++)
            {
                for (int x = 0; x < TEX_SIZE; x++)
                {
                    int row = y / bH;
                    int offset = (row % 2 == 0) ? 0 : bW / 2;
                    int lx = (x + offset) % bW;
                    int ly = y % bH;

                    bool isMortar = lx < mW || ly < mW;

                    if (isMortar)
                    {
                        // モルタルにも微妙なバリエーション
                        float mn = FBM(x * 0.1f, y * 0.1f, 2);
                        byte mr = (byte)Mathf.Clamp(mortar.r + (int)(mn * 15f - 7f), 0, 255);
                        byte mg = (byte)Mathf.Clamp(mortar.g + (int)(mn * 15f - 7f), 0, 255);
                        byte mb = (byte)Mathf.Clamp(mortar.b + (int)(mn * 12f - 6f), 0, 255);
                        pixels[y * TEX_SIZE + x] = new Color32(mr, mg, mb, 255);
                    }
                    else
                    {
                        // FBMノイズで自然なレンガ表面
                        float noise = FBM(x * 0.08f, y * 0.08f, 3);
                        float fine = FBM(x * 0.4f + 100f, y * 0.4f + 100f, 2);

                        Color32 bc;
                        if (noise > 0.65f) bc = brick3;
                        else if (noise > 0.4f) bc = brick1;
                        else if (noise > 0.2f) bc = brick2;
                        else bc = brick4;

                        byte r = (byte)Mathf.Clamp(bc.r + (int)(fine * 25f - 12f), 0, 255);
                        byte g = (byte)Mathf.Clamp(bc.g + (int)(fine * 18f - 9f), 0, 255);
                        byte b = (byte)Mathf.Clamp(bc.b + (int)(fine * 18f - 9f), 0, 255);

                        // レンガのエッジ付近を暗く（ベベル効果）
                        float edgeDist = Mathf.Min(lx - mW, ly - mW, bW - lx, bH - ly);
                        if (edgeDist < 4f)
                        {
                            float darken = 1f - (4f - edgeDist) * 0.04f;
                            r = (byte)(r * darken);
                            g = (byte)(g * darken);
                            b = (byte)(b * darken);
                        }

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
        // テクスチャ生成: 金属（FBM改良版）
        // ============================================================

        private static Texture2D GenerateMetal()
        {
            var tex = new Texture2D(TEX_SIZE, TEX_SIZE, TextureFormat.RGBA32, false);
            var pixels = new Color32[TEX_SIZE * TEX_SIZE];

            for (int y = 0; y < TEX_SIZE; y++)
            {
                for (int x = 0; x < TEX_SIZE; x++)
                {
                    // ブラシヘアライン + FBMマイクロディテール
                    float streak = FBM(x * 0.015f, y * 3f, 3);
                    float micro = FBM(x * 0.5f + 200f, y * 0.5f + 200f, 2);
                    float baseGrey = 0.62f + streak * 0.18f + micro * 0.06f;

                    // 反射ハイライト
                    float highlight = FBM(x * 0.03f + 50f, y * 0.03f + 50f, 2);
                    if (highlight > 0.7f) baseGrey += 0.05f;

                    // リベットパターン
                    int rx = x % 64, ry = y % 64;
                    float rivetDist = Mathf.Sqrt((rx - 4f) * (rx - 4f) + (ry - 4f) * (ry - 4f));
                    if (rivetDist < 3f)
                    {
                        baseGrey *= 0.75f + rivetDist * 0.08f;
                    }

                    // パネル境界線（薄い溝）
                    if (x % 64 < 1 || y % 64 < 1) baseGrey *= 0.85f;

                    byte v = (byte)Mathf.Clamp(baseGrey * 255f, 0, 255);
                    pixels[y * TEX_SIZE + x] = new Color32(v, v, (byte)Mathf.Min(v + 8, 255), 255);
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
        // テクスチャ生成: 芝生（マルチオクターブFBM）
        // ============================================================

        private static Texture2D GenerateGrass()
        {
            var tex = new Texture2D(TEX_SIZE, TEX_SIZE, TextureFormat.RGBA32, false);
            var pixels = new Color32[TEX_SIZE * TEX_SIZE];

            for (int y = 0; y < TEX_SIZE; y++)
            {
                for (int x = 0; x < TEX_SIZE; x++)
                {
                    float n1 = FBM(x * 0.04f, y * 0.04f, 4);
                    float n2 = FBM(x * 0.15f + 50f, y * 0.15f + 50f, 3);
                    float n3 = FBM(x * 0.5f + 100f, y * 0.5f + 100f, 2);

                    float green = 0.42f + n1 * 0.22f + n2 * 0.08f;
                    float red = green * 0.52f + n3 * 0.04f;
                    float blue = green * 0.22f;

                    // 草の葉のストリーク
                    float blade = FBM(x * 0.08f, y * 1.5f, 2);
                    if (blade > 0.65f)
                    {
                        green += 0.08f;
                        red -= 0.02f;
                    }

                    // 花のドット
                    float flower = FBM(x * 0.3f + 200f, y * 0.3f + 200f, 2);
                    if (flower > 0.88f)
                    {
                        red += 0.2f;
                        green -= 0.05f;
                        blue += 0.05f;
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
        // テクスチャ生成: 木材（FBM年輪パターン）
        // ============================================================

        private static Texture2D GenerateWood()
        {
            var tex = new Texture2D(TEX_SIZE, TEX_SIZE, TextureFormat.RGBA32, false);
            var pixels = new Color32[TEX_SIZE * TEX_SIZE];

            for (int y = 0; y < TEX_SIZE; y++)
            {
                for (int x = 0; x < TEX_SIZE; x++)
                {
                    float cx = (x - TEX_SIZE * 0.5f) * 0.01f;
                    float cy = (y - TEX_SIZE * 0.5f) * 0.5f;
                    float dist = Mathf.Sqrt(cx * cx + cy * cy);

                    // 年輪 + FBMによる歪み
                    float warp = FBM(x * 0.03f, y * 0.03f, 3) * 5f;
                    float ring = Mathf.Sin(dist * 0.8f + warp);
                    ring = ring * 0.5f + 0.5f;

                    // 木目の細かい筋
                    float grain = FBM(x * 0.02f, y * 0.6f, 3) * 0.18f;
                    float fine = FBM(x * 0.08f, y * 2f, 2) * 0.08f;

                    float baseR = 0.58f + ring * 0.14f + grain + fine;
                    float baseG = 0.40f + ring * 0.11f + grain * 0.8f + fine * 0.7f;
                    float baseB = 0.24f + ring * 0.08f + grain * 0.5f + fine * 0.4f;

                    // 節（ノット）
                    float knotX = (x % 128) - 64f;
                    float knotY = (y % 128) - 64f;
                    float knotDist = Mathf.Sqrt(knotX * knotX + knotY * knotY);
                    if (knotDist < 12f)
                    {
                        float knotRing = Mathf.Sin(knotDist * 0.5f) * 0.5f + 0.5f;
                        baseR = Mathf.Lerp(baseR, 0.35f + knotRing * 0.15f, Mathf.Max(0f, 1f - knotDist / 12f));
                        baseG = Mathf.Lerp(baseG, 0.25f + knotRing * 0.1f, Mathf.Max(0f, 1f - knotDist / 12f));
                        baseB = Mathf.Lerp(baseB, 0.15f + knotRing * 0.05f, Mathf.Max(0f, 1f - knotDist / 12f));
                    }

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
        // テクスチャ生成: コンクリート
        // ============================================================

        private static Texture2D GenerateConcrete()
        {
            var tex = new Texture2D(TEX_SMALL, TEX_SMALL, TextureFormat.RGBA32, false);
            var pixels = new Color32[TEX_SMALL * TEX_SMALL];

            for (int y = 0; y < TEX_SMALL; y++)
            {
                for (int x = 0; x < TEX_SMALL; x++)
                {
                    float n = FBM(x * 0.1f, y * 0.1f, 3);
                    float n2 = FBM(x * 0.4f + 50f, y * 0.4f + 50f, 2);
                    float v = 0.72f + n * 0.12f + (n2 - 0.5f) * 0.04f;

                    // 小さなピットホール
                    float pit = FBM(x * 0.8f + 150f, y * 0.8f + 150f, 2);
                    if (pit > 0.82f) v -= 0.06f;

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
        // テクスチャ生成: タイル
        // ============================================================

        private static Texture2D GenerateTile()
        {
            var tex = new Texture2D(TEX_SIZE, TEX_SIZE, TextureFormat.RGBA32, false);
            var pixels = new Color32[TEX_SIZE * TEX_SIZE];

            Color32 tileColor = new Color32(232, 232, 238, 255);
            Color32 groutColor = new Color32(190, 190, 185, 255);

            int tileSize = 32;
            int groutW = 2;

            for (int y = 0; y < TEX_SIZE; y++)
            {
                for (int x = 0; x < TEX_SIZE; x++)
                {
                    int lx = x % tileSize;
                    int ly = y % tileSize;
                    bool isGrout = lx < groutW || ly < groutW;

                    if (isGrout)
                    {
                        float gn = FBM(x * 0.2f, y * 0.2f, 2);
                        byte gr = (byte)Mathf.Clamp(groutColor.r + (int)(gn * 10f - 5f), 0, 255);
                        byte gg = (byte)Mathf.Clamp(groutColor.g + (int)(gn * 10f - 5f), 0, 255);
                        byte gb = (byte)Mathf.Clamp(groutColor.b + (int)(gn * 10f - 5f), 0, 255);
                        pixels[y * TEX_SIZE + x] = new Color32(gr, gg, gb, 255);
                    }
                    else
                    {
                        float n = FBM(x * 0.15f + lx * 0.01f, y * 0.15f + ly * 0.01f, 2);
                        byte r = (byte)Mathf.Clamp(tileColor.r + (int)(n * 14f - 7f), 0, 255);
                        byte g = (byte)Mathf.Clamp(tileColor.g + (int)(n * 14f - 7f), 0, 255);
                        byte b = (byte)Mathf.Clamp(tileColor.b + (int)(n * 14f - 7f), 0, 255);

                        // タイルエッジのハイライト
                        float edgeDist = Mathf.Min(lx - groutW, ly - groutW);
                        if (edgeDist < 2f) { r = (byte)Mathf.Min(r + 8, 255); g = (byte)Mathf.Min(g + 8, 255); b = (byte)Mathf.Min(b + 8, 255); }

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
        // テクスチャ生成: ストライプ
        // ============================================================

        private static Texture2D GenerateStripe()
        {
            var tex = new Texture2D(TEX_TINY, TEX_TINY, TextureFormat.RGBA32, false);
            var pixels = new Color32[TEX_TINY * TEX_TINY];

            Color32 col1 = new Color32(220, 40, 50, 255);
            Color32 col2 = new Color32(248, 248, 242, 255);
            int sw = 8;

            for (int y = 0; y < TEX_TINY; y++)
                for (int x = 0; x < TEX_TINY; x++)
                    pixels[y * TEX_TINY + x] = ((x + y) / sw % 2 == 0) ? col1 : col2;

            tex.SetPixels32(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.name = "ProceduralStripe";
            return tex;
        }

        // ============================================================
        // テクスチャ生成: チェッカーボード
        // ============================================================

        private static Texture2D GenerateChecker()
        {
            var tex = new Texture2D(TEX_TINY, TEX_TINY, TextureFormat.RGBA32, false);
            var pixels = new Color32[TEX_TINY * TEX_TINY];

            int cs = 8;
            Color32 a = new Color32(240, 240, 240, 255);
            Color32 b = new Color32(40, 40, 45, 255);

            for (int y = 0; y < TEX_TINY; y++)
                for (int x = 0; x < TEX_TINY; x++)
                    pixels[y * TEX_TINY + x] = ((x / cs + y / cs) % 2 == 0) ? a : b;

            tex.SetPixels32(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.name = "ProceduralChecker";
            return tex;
        }

        // ============================================================
        // テクスチャ生成: スタッコ（漆喰壁）
        // ============================================================

        private static Texture2D GenerateStucco()
        {
            var tex = new Texture2D(TEX_SIZE, TEX_SIZE, TextureFormat.RGBA32, false);
            var pixels = new Color32[TEX_SIZE * TEX_SIZE];

            for (int y = 0; y < TEX_SIZE; y++)
            {
                for (int x = 0; x < TEX_SIZE; x++)
                {
                    float n1 = FBM(x * 0.06f, y * 0.06f, 4);
                    float n2 = FBM(x * 0.25f + 80f, y * 0.25f + 80f, 3);
                    float n3 = FBM(x * 0.8f + 160f, y * 0.8f + 160f, 2);

                    float v = 0.88f + n1 * 0.08f + n2 * 0.04f;

                    // ザラザラした表面
                    v += (n3 - 0.5f) * 0.03f;

                    // ひび割れ風のライン
                    float crack = FBM(x * 0.02f + 300f, y * 0.02f + 300f, 3);
                    if (crack > 0.78f && crack < 0.80f) v -= 0.08f;

                    byte bv = (byte)Mathf.Clamp(v * 255f, 0, 255);
                    pixels[y * TEX_SIZE + x] = new Color32(bv, (byte)Mathf.Max(bv - 2, 0), (byte)Mathf.Max(bv - 4, 0), 255);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.name = "ProceduralStucco";
            return tex;
        }

        // ============================================================
        // テクスチャ生成: 屋根瓦
        // ============================================================

        private static Texture2D GenerateRoofTile()
        {
            var tex = new Texture2D(TEX_SIZE, TEX_SIZE, TextureFormat.RGBA32, false);
            var pixels = new Color32[TEX_SIZE * TEX_SIZE];

            int tW = 48, tH = 24;

            for (int y = 0; y < TEX_SIZE; y++)
            {
                for (int x = 0; x < TEX_SIZE; x++)
                {
                    int row = y / tH;
                    int offset = (row % 2 == 0) ? 0 : tW / 2;
                    int lx = (x + offset) % tW;
                    int ly = y % tH;

                    // 瓦の丸みカーブ
                    float curve = Mathf.Sin((float)lx / tW * Mathf.PI) * 0.12f;
                    float shadow = Mathf.Max(0f, 1f - (float)ly / tH * 0.3f);

                    float n = FBM(x * 0.08f, y * 0.08f, 3);
                    float v = 0.60f + curve + shadow * 0.1f + n * 0.08f;

                    // 瓦の境界を暗く
                    if (ly < 2 || lx < 1) v -= 0.12f;

                    byte r = (byte)Mathf.Clamp(v * 255f, 0, 255);
                    byte g = (byte)Mathf.Clamp(v * 180f, 0, 255);
                    byte b = (byte)Mathf.Clamp(v * 140f, 0, 255);
                    pixels[y * TEX_SIZE + x] = new Color32(r, g, b, 255);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.name = "ProceduralRoofTile";
            return tex;
        }

        // ============================================================
        // カスタムシェーダーマテリアル
        // ============================================================

        /// <summary>トゥーンシェーダーマテリアル（デフォルト白）</summary>
        public static Material ToonDefaultMaterial
        {
            get { if (_toonDefaultMat == null) _toonDefaultMat = CreateToonMaterial(Color.white, Brick, BrickNormal); return _toonDefaultMat; }
        }

        /// <summary>水面マテリアル</summary>
        public static Material WaterMaterial
        {
            get { if (_waterMat == null) _waterMat = CreateWaterMaterial(); return _waterMat; }
        }

        /// <summary>草木マテリアル（風揺れ付き）</summary>
        public static Material FoliageMaterial
        {
            get { if (_foliageMat == null) _foliageMat = CreateFoliageMaterial(); return _foliageMat; }
        }

        /// <summary>発光マテリアル（黄色 - 街灯用）</summary>
        public static Material EmissiveYellowMaterial
        {
            get { if (_emissiveYellowMat == null) _emissiveYellowMat = CreateEmissiveMaterial(new Color(1f, 0.9f, 0.5f), 2f); return _emissiveYellowMat; }
        }

        /// <summary>発光マテリアル（赤 - 装飾用）</summary>
        public static Material EmissiveRedMaterial
        {
            get { if (_emissiveRedMat == null) _emissiveRedMat = CreateEmissiveMaterial(new Color(1f, 0.3f, 0.2f), 1.8f); return _emissiveRedMat; }
        }

        /// <summary>発光マテリアル（青 - 装飾用）</summary>
        public static Material EmissiveBlueMaterial
        {
            get { if (_emissiveBlueMat == null) _emissiveBlueMat = CreateEmissiveMaterial(new Color(0.3f, 0.6f, 1f), 1.8f); return _emissiveBlueMat; }
        }

        /// <summary>トゥーンシェーダーマテリアルを生成</summary>
        public static Material CreateToonMaterial(Color tint, Texture2D albedo = null, Texture2D normalMap = null,
            float tileScale = 2f)
        {
            var shader = Shader.Find("ThemeParkGame/Toon");
            if (shader == null) return CreatePBRMaterial(albedo, normalMap, tint, 0f, 0.3f, tileScale);

            var mat = new Material(shader);
            mat.SetColor("_Color", tint);
            mat.SetColor("_ShadowColor", new Color(0.55f, 0.5f, 0.65f, 1f));
            mat.SetFloat("_ShadowThreshold", 0.45f);
            mat.SetFloat("_ShadowSoftness", 0.06f);
            mat.SetColor("_RimColor", new Color(1f, 0.95f, 0.85f, 1f));
            mat.SetFloat("_RimPower", 3f);
            mat.SetFloat("_TileScale", tileScale);

            if (albedo != null) mat.SetTexture("_MainTex", albedo);
            if (normalMap != null) mat.SetTexture("_BumpMap", normalMap);

            return mat;
        }

        /// <summary>水面シェーダーマテリアルを生成</summary>
        public static Material CreateWaterMaterial()
        {
            var shader = Shader.Find("ThemeParkGame/Water");
            if (shader == null)
            {
                // フォールバック: 半透明Standard
                var fallback = CreateGlassMat();
                fallback.color = new Color(0.2f, 0.5f, 0.8f, 0.6f);
                return fallback;
            }

            var mat = new Material(shader);
            mat.SetColor("_ShallowColor", new Color(0.3f, 0.75f, 0.92f, 0.65f));
            mat.SetColor("_DeepColor", new Color(0.05f, 0.25f, 0.55f, 0.85f));
            mat.SetColor("_FoamColor", new Color(0.9f, 0.95f, 1f, 0.9f));
            mat.SetFloat("_WaveSpeed", 1f);
            mat.SetFloat("_WaveScale", 4f);
            mat.SetFloat("_WaveHeight", 0.06f);
            return mat;
        }

        /// <summary>草木シェーダーマテリアルを生成</summary>
        public static Material CreateFoliageMaterial(Color? leafColor = null)
        {
            var shader = Shader.Find("ThemeParkGame/Foliage");
            if (shader == null)
            {
                Color c = leafColor ?? new Color(0.25f, 0.65f, 0.2f);
                return CreatePBRMaterial(null, null, c, 0f, 0.15f, 1f);
            }

            var mat = new Material(shader);
            Color lc = leafColor ?? new Color(0.25f, 0.65f, 0.2f);
            mat.SetColor("_Color", lc);
            mat.SetColor("_ColorVariation", new Color(lc.r + 0.1f, lc.g + 0.1f, lc.b - 0.05f, 1f));
            mat.SetColor("_ShadowColor", new Color(lc.r * 0.5f, lc.g * 0.5f, lc.b * 0.5f, 1f));
            mat.SetFloat("_WindSpeed", 1.2f);
            mat.SetFloat("_WindStrength", 0.07f);
            return mat;
        }

        /// <summary>発光シェーダーマテリアルを生成</summary>
        public static Material CreateEmissiveMaterial(Color emissionColor, float intensity = 2f)
        {
            var shader = Shader.Find("ThemeParkGame/Emissive");
            if (shader == null)
            {
                // フォールバック: Standard with emission
                var fallback = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default"));
                fallback.color = emissionColor;
                if (fallback.shader.name == "Standard")
                {
                    fallback.EnableKeyword("_EMISSION");
                    fallback.SetColor("_EmissionColor", emissionColor * intensity);
                    fallback.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                }
                return fallback;
            }

            var mat = new Material(shader);
            mat.SetColor("_Color", emissionColor);
            mat.SetColor("_EmissionColor", emissionColor);
            mat.SetFloat("_EmissionIntensity", intensity);
            mat.SetFloat("_PulseSpeed", 1.5f);
            mat.SetFloat("_PulseMin", 0.6f);
            return mat;
        }
    }
}
