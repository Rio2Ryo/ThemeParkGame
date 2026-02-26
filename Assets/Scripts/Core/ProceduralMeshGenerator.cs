// ============================================================
// ThemeParkGame - ProceduralMeshGenerator v3
// 高品質プロシージャルメッシュ + PBRテクスチャマテリアル適用
// ポリゴン数大幅増加・法線マップ対応・建築ディテール強化
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace ThemeParkGame.Core
{
    /// <summary>
    /// 高品質プロシージャルメッシュ生成。
    /// 全メッシュのポリゴン数を大幅増加し、PBRテクスチャマテリアルを適用。
    /// アトラクション・施設・キャラクターの3Dモデルをランタイム生成する。
    /// </summary>
    public static class ProceduralMeshGenerator
    {
        // ============================================================
        // カラーパレット - 遊園地らしい鮮やかな配色
        // ============================================================

        public static class Palette
        {
            // アトラクション
            public static readonly Color CoasterRed    = new Color(0.90f, 0.15f, 0.20f);
            public static readonly Color CoasterRail   = new Color(0.75f, 0.75f, 0.80f);
            public static readonly Color FerrisBlue    = new Color(0.20f, 0.45f, 0.90f);
            public static readonly Color FerrisGold    = new Color(0.95f, 0.80f, 0.25f);
            public static readonly Color MerryPink     = new Color(0.95f, 0.50f, 0.60f);
            public static readonly Color MerryGold     = new Color(0.95f, 0.85f, 0.30f);
            public static readonly Color SpinYellow    = new Color(0.95f, 0.85f, 0.20f);
            public static readonly Color SpinOrange    = new Color(1.00f, 0.55f, 0.15f);
            public static readonly Color HauntPurple   = new Color(0.30f, 0.15f, 0.40f);
            public static readonly Color HauntGrey     = new Color(0.35f, 0.33f, 0.38f);

            // ショップ
            public static readonly Color FoodOrange    = new Color(1.00f, 0.55f, 0.15f);
            public static readonly Color DrinkCyan     = new Color(0.20f, 0.80f, 0.90f);
            public static readonly Color SouvenirPink  = new Color(0.95f, 0.45f, 0.70f);
            public static readonly Color ShopRoof      = new Color(0.65f, 0.25f, 0.15f);
            public static readonly Color ShopAwning    = new Color(0.95f, 0.92f, 0.85f);

            // 施設
            public static readonly Color ToiletWhite   = new Color(0.95f, 0.95f, 0.97f);
            public static readonly Color ToiletBlue    = new Color(0.40f, 0.65f, 0.90f);
            public static readonly Color BenchBrown    = new Color(0.55f, 0.35f, 0.18f);
            public static readonly Color BenchMetal    = new Color(0.45f, 0.45f, 0.50f);
            public static readonly Color StaffGreen    = new Color(0.35f, 0.65f, 0.40f);

            // 環境
            public static readonly Color GrassGreen    = new Color(0.30f, 0.70f, 0.25f);
            public static readonly Color PathGrey      = new Color(0.72f, 0.70f, 0.68f);
            public static readonly Color WoodBrown     = new Color(0.55f, 0.38f, 0.22f);
            public static readonly Color SkyBlue       = new Color(0.53f, 0.81f, 0.98f);

            // 来場者
            public static readonly Color[] VisitorColors = {
                new Color(0.95f, 0.45f, 0.45f),
                new Color(0.45f, 0.75f, 0.95f),
                new Color(0.45f, 0.90f, 0.50f),
                new Color(0.95f, 0.85f, 0.35f),
                new Color(0.80f, 0.50f, 0.90f),
                new Color(0.95f, 0.65f, 0.35f),
                new Color(0.70f, 0.85f, 0.95f),
                new Color(0.95f, 0.70f, 0.80f),
            };

            // スタッフ
            public static readonly Color StaffMechanic     = new Color(1.00f, 0.50f, 0.10f);
            public static readonly Color StaffCleaner      = new Color(0.20f, 0.85f, 0.30f);
            public static readonly Color StaffEntertainer  = new Color(0.90f, 0.25f, 0.85f);
            public static readonly Color StaffGuard        = new Color(0.20f, 0.25f, 0.75f);
            public static readonly Color StaffScientist    = new Color(0.95f, 0.95f, 0.40f);
            public static readonly Color StaffDoctor       = new Color(1.00f, 1.00f, 1.00f);
            public static readonly Color StaffVendor       = new Color(0.90f, 0.55f, 0.20f);
            public static readonly Color StaffGardener     = new Color(0.30f, 0.65f, 0.30f);

            public static readonly Color VIPGold = new Color(1.00f, 0.84f, 0.00f);
        }

        // セグメント数定数（品質レベル）
        private const int SEG_LOW = 12;
        private const int SEG_MED = 20;
        private const int SEG_HIGH = 28;
        private const int SEG_ULTRA = 36;

        // ============================================================
        // アトラクション生成
        // ============================================================

        /// <summary>ジェットコースターのメッシュを生成（高品質版）</summary>
        public static GameObject CreateRollerCoaster(string name)
        {
            var root = new GameObject(name);

            // トラック（ヘリカルパスに沿ったチューブ）
            int trackPoints = 80;
            Vector3[] trackPath = new Vector3[trackPoints];
            for (int i = 0; i < trackPoints; i++)
            {
                float t = (float)i / (trackPoints - 1);
                float angle = t * Mathf.PI * 3f;
                float radius = 2.2f + Mathf.Sin(t * Mathf.PI * 2f) * 0.6f;
                float height = Mathf.Sin(t * Mathf.PI) * 5.5f + 1.2f + Mathf.Sin(t * Mathf.PI * 4f) * 1.8f;
                trackPath[i] = new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
            }

            // レール2本（金属テクスチャ）
            var leftRail = CreateTubeAlongPath(trackPath, 0.09f, SEG_LOW, new Vector3(-0.18f, 0f, 0f));
            leftRail.name = "LeftRail";
            leftRail.transform.SetParent(root.transform, false);
            ApplyPBR(leftRail, ProceduralTextureGenerator.MetalRideMaterial);

            var rightRail = CreateTubeAlongPath(trackPath, 0.09f, SEG_LOW, new Vector3(0.18f, 0f, 0f));
            rightRail.name = "RightRail";
            rightRail.transform.SetParent(root.transform, false);
            ApplyPBR(rightRail, ProceduralTextureGenerator.MetalRideMaterial);

            // クロスタイ（枕木 - 木材テクスチャ）
            for (int i = 0; i < trackPoints - 1; i += 3)
            {
                var tie = CreateBoxGameObject(new Vector3(0.55f, 0.06f, 0.09f));
                tie.name = $"Tie_{i}";
                tie.transform.SetParent(root.transform, false);
                tie.transform.position = trackPath[i];
                if (i + 1 < trackPoints)
                {
                    Vector3 dir = (trackPath[i + 1] - trackPath[i]).normalized;
                    if (dir != Vector3.zero) tie.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                }
                ApplyPBR(tie, ProceduralTextureGenerator.WoodPanelMaterial);
            }

            // サポート柱（金属テクスチャ）
            for (int i = 0; i < trackPoints; i += 6)
            {
                float h = trackPath[i].y;
                if (h < 0.5f) continue;
                var pillar = CreateCylinderGameObject(0.1f, h, SEG_MED);
                pillar.name = $"Pillar_{i}";
                pillar.transform.SetParent(root.transform, false);
                pillar.transform.position = new Vector3(trackPath[i].x, h * 0.5f, trackPath[i].z);
                ApplyPBR(pillar, ProceduralTextureGenerator.MetalRideMaterial);

                // クロスブレース
                if (h > 2f)
                {
                    var brace = CreateCylinderGameObject(0.04f, h * 0.6f, 8);
                    brace.name = $"Brace_{i}";
                    brace.transform.SetParent(root.transform, false);
                    brace.transform.position = new Vector3(trackPath[i].x + 0.15f, h * 0.4f, trackPath[i].z);
                    brace.transform.localRotation = Quaternion.Euler(0f, 0f, 25f);
                    ApplyMaterial(brace, Palette.BenchMetal, 0.6f, 0.5f);
                }
            }

            // コースターカー（3両）
            for (int c = 0; c < 3; c++)
            {
                int idx = Mathf.Min(c * 4, trackPoints - 2);
                var car = CreateBoxGameObject(new Vector3(0.5f, 0.35f, 0.3f));
                car.name = $"Car_{c}";
                car.transform.SetParent(root.transform, false);
                car.transform.position = trackPath[idx] + Vector3.up * 0.25f;
                Vector3 dir = (trackPath[idx + 1] - trackPath[idx]).normalized;
                if (dir != Vector3.zero) car.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                ApplyMaterial(car, Palette.CoasterRed, 0.1f, 0.6f);
            }

            // ステーションプラットフォーム（コンクリートテクスチャ）
            var station = CreateBoxGameObject(new Vector3(3.5f, 0.35f, 2.5f));
            station.name = "Station";
            station.transform.SetParent(root.transform, false);
            station.transform.localPosition = new Vector3(trackPath[0].x, 0.18f, trackPath[0].z);
            ApplyPBR(station, ProceduralTextureGenerator.ConcretePathMaterial);

            // ステーション屋根
            var stationRoof = CreateBoxGameObject(new Vector3(4f, 0.12f, 3f));
            stationRoof.name = "StationRoof";
            stationRoof.transform.SetParent(root.transform, false);
            stationRoof.transform.localPosition = new Vector3(trackPath[0].x, 3.2f, trackPath[0].z);
            ApplyPBR(stationRoof, ProceduralTextureGenerator.RoofTileMaterial);

            // 屋根支柱4本
            for (int i = 0; i < 4; i++)
            {
                float ox = (i % 2 == 0) ? -1.5f : 1.5f;
                float oz = (i < 2) ? -1f : 1f;
                var rp = CreateCylinderGameObject(0.07f, 3f, SEG_MED);
                rp.name = $"RoofPillar_{i}";
                rp.transform.SetParent(root.transform, false);
                rp.transform.localPosition = new Vector3(trackPath[0].x + ox, 1.6f, trackPath[0].z + oz);
                ApplyPBR(rp, ProceduralTextureGenerator.MetalRideMaterial);
            }

            return root;
        }

        /// <summary>観覧車のメッシュを生成（高品質版）</summary>
        public static GameObject CreateFerrisWheel(string name)
        {
            var root = new GameObject(name);
            float wheelRadius = 5.5f;
            int gondolaCount = 16;

            // A字型支柱（左右）
            for (int side = -1; side <= 1; side += 2)
            {
                float sx = side * 1.6f;

                var legF = CreateCylinderGameObject(0.18f, 13f, SEG_MED);
                legF.name = $"LegFront_{(side > 0 ? "R" : "L")}";
                legF.transform.SetParent(root.transform, false);
                legF.transform.localPosition = new Vector3(sx, 6.5f, -0.9f);
                legF.transform.localRotation = Quaternion.Euler(0f, 0f, side * 5f);
                ApplyPBR(legF, ProceduralTextureGenerator.MetalRideMaterial);

                var legB = CreateCylinderGameObject(0.18f, 13f, SEG_MED);
                legB.name = $"LegBack_{(side > 0 ? "R" : "L")}";
                legB.transform.SetParent(root.transform, false);
                legB.transform.localPosition = new Vector3(sx, 6.5f, 0.9f);
                legB.transform.localRotation = Quaternion.Euler(0f, 0f, side * 5f);
                ApplyPBR(legB, ProceduralTextureGenerator.MetalRideMaterial);

                // 横ブレース
                var xbrace = CreateCylinderGameObject(0.08f, 2f, SEG_LOW);
                xbrace.name = $"XBrace_{(side > 0 ? "R" : "L")}";
                xbrace.transform.SetParent(root.transform, false);
                xbrace.transform.localPosition = new Vector3(sx, 4f, 0f);
                xbrace.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                ApplyMaterial(xbrace, Palette.FerrisBlue, 0.5f, 0.5f);
            }

            // 中央ハブ
            var hub = CreateCylinderGameObject(0.6f, 0.5f, SEG_HIGH);
            hub.name = "Hub";
            hub.transform.SetParent(root.transform, false);
            hub.transform.localPosition = new Vector3(0f, 11.5f, 0f);
            hub.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ApplyMaterial(hub, Palette.FerrisGold, 0.7f, 0.8f);

            // スポーク
            for (int i = 0; i < gondolaCount; i++)
            {
                float angle = (float)i / gondolaCount * Mathf.PI * 2f;
                float cx = Mathf.Cos(angle) * wheelRadius * 0.5f;
                float cy = Mathf.Sin(angle) * wheelRadius * 0.5f + 11.5f;

                var spoke = CreateCylinderGameObject(0.06f, wheelRadius, SEG_LOW);
                spoke.name = $"Spoke_{i}";
                spoke.transform.SetParent(root.transform, false);
                spoke.transform.localPosition = new Vector3(0f, cy, 0f);
                spoke.transform.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
                ApplyMaterial(spoke, Palette.FerrisBlue, 0.4f, 0.5f);
            }

            // リムセグメント
            int rimSeg = 48;
            for (int i = 0; i < rimSeg; i++)
            {
                float a1 = (float)i / rimSeg * Mathf.PI * 2f;
                float a2 = (float)(i + 1) / rimSeg * Mathf.PI * 2f;
                Vector3 p1 = new Vector3(0f, Mathf.Sin(a1) * wheelRadius + 11.5f, Mathf.Cos(a1) * wheelRadius);
                Vector3 p2 = new Vector3(0f, Mathf.Sin(a2) * wheelRadius + 11.5f, Mathf.Cos(a2) * wheelRadius);

                float segLen = Vector3.Distance(p1, p2);
                var rs = CreateCylinderGameObject(0.1f, segLen, SEG_LOW);
                rs.name = $"Rim_{i}";
                rs.transform.SetParent(root.transform, false);
                rs.transform.localPosition = (p1 + p2) * 0.5f;
                rs.transform.up = (p2 - p1).normalized;
                ApplyMaterial(rs, Palette.FerrisGold, 0.6f, 0.7f);
            }

            // ゴンドラ（高品質版 - 丸みを帯びた形状）
            for (int i = 0; i < gondolaCount; i++)
            {
                float angle = (float)i / gondolaCount * Mathf.PI * 2f;
                float gy = Mathf.Sin(angle) * wheelRadius + 11.5f;
                float gz = Mathf.Cos(angle) * wheelRadius;

                // ゴンドラ本体
                var gondola = CreateBoxGameObject(new Vector3(0.9f, 1.0f, 0.7f));
                gondola.name = $"Gondola_{i}";
                gondola.transform.SetParent(root.transform, false);
                gondola.transform.localPosition = new Vector3(0f, gy - 0.7f, gz);
                Color gc = (i % 4) switch { 0 => Palette.CoasterRed, 1 => Palette.FerrisBlue, 2 => Palette.FerrisGold, _ => Palette.MerryPink };
                ApplyMaterial(gondola, gc, 0.1f, 0.5f);

                // ゴンドラ屋根
                var gRoof = CreateCylinderGameObject(0.5f, 0.08f, SEG_LOW);
                gRoof.name = $"GondolaRoof_{i}";
                gRoof.transform.SetParent(root.transform, false);
                gRoof.transform.localPosition = new Vector3(0f, gy - 0.15f, gz);
                ApplyMaterial(gRoof, gc * 0.8f, 0.1f, 0.4f);

                // ワイヤー
                var wire = CreateCylinderGameObject(0.025f, 0.7f, 6);
                wire.name = $"Wire_{i}";
                wire.transform.SetParent(root.transform, false);
                wire.transform.localPosition = new Vector3(0f, gy - 0.25f, gz);
                ApplyMaterial(wire, Palette.BenchMetal, 0.5f, 0.5f);
            }

            // 基礎プラットフォーム
            var platform = CreateBoxGameObject(new Vector3(5.5f, 0.35f, 5.5f));
            platform.name = "Platform";
            platform.transform.SetParent(root.transform, false);
            platform.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            ApplyPBR(platform, ProceduralTextureGenerator.ConcretePathMaterial);

            // 入口ゲート
            var gate = CreateBoxGameObject(new Vector3(3f, 2.8f, 0.3f));
            gate.name = "Gate";
            gate.transform.SetParent(root.transform, false);
            gate.transform.localPosition = new Vector3(0f, 1.4f, -3f);
            ApplyPBR(gate, ProceduralTextureGenerator.BrickWallMaterial);

            return root;
        }

        /// <summary>メリーゴーランドのメッシュを生成（高品質版）</summary>
        public static GameObject CreateMerryGoRound(string name)
        {
            var root = new GameObject(name);
            float platformRadius = 3.5f;
            int horseCount = 10;

            // 回転台（多角形で滑らかな円盤）
            var platform = CreateCylinderGameObject(platformRadius, 0.45f, SEG_ULTRA);
            platform.name = "Platform";
            platform.transform.SetParent(root.transform, false);
            platform.transform.localPosition = new Vector3(0f, 0.23f, 0f);
            ApplyMaterial(platform, Palette.MerryPink, 0.1f, 0.5f);

            // デコリング
            var decoRing = CreateCylinderGameObject(platformRadius + 0.2f, 0.18f, SEG_ULTRA);
            decoRing.name = "DecoRing";
            decoRing.transform.SetParent(root.transform, false);
            decoRing.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            ApplyMaterial(decoRing, Palette.FerrisGold, 0.6f, 0.7f);

            // 中央ポール
            var pole = CreateCylinderGameObject(0.22f, 5.5f, SEG_MED);
            pole.name = "CenterPole";
            pole.transform.SetParent(root.transform, false);
            pole.transform.localPosition = new Vector3(0f, 2.75f, 0f);
            ApplyMaterial(pole, Palette.FerrisGold, 0.7f, 0.8f);

            // 円錐屋根
            var roof = CreateConeGameObject(platformRadius + 0.6f, 0.12f, 2.2f, SEG_ULTRA);
            roof.name = "Roof";
            roof.transform.SetParent(root.transform, false);
            roof.transform.localPosition = new Vector3(0f, 5.5f, 0f);
            ApplyMaterial(roof, new Color(0.90f, 0.25f, 0.30f), 0.1f, 0.4f);

            // 屋根のフリル
            var frill = CreateCylinderGameObject(platformRadius + 0.8f, 0.18f, SEG_ULTRA);
            frill.name = "RoofFrill";
            frill.transform.SetParent(root.transform, false);
            frill.transform.localPosition = new Vector3(0f, 5.45f, 0f);
            ApplyMaterial(frill, Palette.ShopAwning, 0f, 0.3f);

            // 屋根トップ飾り
            var finial = CreateSphereGameObject(0.3f, SEG_MED);
            finial.name = "Finial";
            finial.transform.SetParent(root.transform, false);
            finial.transform.localPosition = new Vector3(0f, 7.2f, 0f);
            ApplyMaterial(finial, Palette.FerrisGold, 0.8f, 0.9f);

            // 馬
            for (int i = 0; i < horseCount; i++)
            {
                float angle = (float)i / horseCount * Mathf.PI * 2f;
                float hx = Mathf.Cos(angle) * (platformRadius - 0.7f);
                float hz = Mathf.Sin(angle) * (platformRadius - 0.7f);
                float horseH = (i % 2 == 0) ? 2.0f : 2.6f;

                // ポール（金属テクスチャ）
                var horsePole = CreateCylinderGameObject(0.045f, 5f, SEG_LOW);
                horsePole.name = $"HorsePole_{i}";
                horsePole.transform.SetParent(root.transform, false);
                horsePole.transform.localPosition = new Vector3(hx, 2.9f, hz);
                ApplyMaterial(horsePole, Palette.FerrisGold, 0.7f, 0.8f);

                // 馬の体（楕円球）
                var body = CreateSphereGameObject(0.38f, SEG_MED);
                body.name = $"Horse_{i}";
                body.transform.SetParent(root.transform, false);
                body.transform.localPosition = new Vector3(hx, horseH, hz);
                body.transform.localScale = new Vector3(0.5f, 0.7f, 1.3f);
                body.transform.localRotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg + 90f, 0f);

                Color hc = (i % 3) switch { 0 => new Color(0.95f, 0.95f, 0.90f), 1 => new Color(0.60f, 0.40f, 0.25f), _ => new Color(0.30f, 0.30f, 0.32f) };
                ApplyMaterial(body, hc, 0f, 0.4f);

                // 馬の頭
                var head = CreateSphereGameObject(0.15f, SEG_LOW);
                head.name = $"HorseHead_{i}";
                head.transform.SetParent(root.transform, false);
                float headFwd = 0.35f;
                float headX = hx + Mathf.Cos(angle + Mathf.PI * 0.5f) * headFwd * (angle > Mathf.PI ? -1f : 1f);
                float headZ = hz + Mathf.Sin(angle + Mathf.PI * 0.5f) * headFwd * (angle > Mathf.PI ? -1f : 1f);
                head.transform.localPosition = new Vector3(headX, horseH + 0.3f, headZ);
                head.transform.localScale = new Vector3(0.6f, 0.8f, 1f);
                ApplyMaterial(head, hc * 0.95f, 0f, 0.4f);

                // サドル（装飾）
                var saddle = CreateCylinderGameObject(0.2f, 0.05f, SEG_LOW);
                saddle.name = $"Saddle_{i}";
                saddle.transform.SetParent(root.transform, false);
                saddle.transform.localPosition = new Vector3(hx, horseH + 0.15f, hz);
                Color saddleColor = (i % 4) switch { 0 => Palette.CoasterRed, 1 => Palette.FerrisBlue, 2 => Palette.FerrisGold, _ => Palette.MerryPink };
                ApplyMaterial(saddle, saddleColor, 0f, 0.5f);
            }

            // ライト装飾（屋根の縁に小球）
            for (int i = 0; i < 24; i++)
            {
                float a = (float)i / 24 * Mathf.PI * 2f;
                float lx = Mathf.Cos(a) * (platformRadius + 0.5f);
                float lz = Mathf.Sin(a) * (platformRadius + 0.5f);
                var light = CreateSphereGameObject(0.06f, 8);
                light.name = $"Light_{i}";
                light.transform.SetParent(root.transform, false);
                light.transform.localPosition = new Vector3(lx, 5.35f, lz);
                Color lc = (i % 3) switch { 0 => Color.yellow, 1 => Color.red, _ => Color.cyan };
                ApplyMaterial(light, lc, 0f, 0.9f);
            }

            return root;
        }

        /// <summary>コーヒーカップ型回転アトラクション（高品質版）</summary>
        public static GameObject CreateSpinningCups(string name)
        {
            var root = new GameObject(name);
            int cupCount = 8;
            float plateRadius = 3.5f;

            var platform = CreateCylinderGameObject(plateRadius, 0.35f, SEG_ULTRA);
            platform.name = "Platform";
            platform.transform.SetParent(root.transform, false);
            platform.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            ApplyMaterial(platform, Palette.SpinYellow, 0.1f, 0.5f);

            // 中央装飾
            var centerPot = CreateConeGameObject(0.65f, 0.35f, 1.6f, SEG_MED);
            centerPot.name = "CenterPot";
            centerPot.transform.SetParent(root.transform, false);
            centerPot.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            ApplyMaterial(centerPot, Palette.SpinOrange, 0.3f, 0.5f);

            // 天蓋
            var canopy = CreateConeGameObject(plateRadius + 0.6f, plateRadius + 0.3f, 0.6f, SEG_ULTRA);
            canopy.name = "Canopy";
            canopy.transform.SetParent(root.transform, false);
            canopy.transform.localPosition = new Vector3(0f, 4.2f, 0f);
            ApplyMaterial(canopy, new Color(0.95f, 0.30f, 0.35f), 0.1f, 0.3f);

            // 天蓋支柱
            var canopyPole = CreateCylinderGameObject(0.12f, 4.2f, SEG_MED);
            canopyPole.name = "CanopyPole";
            canopyPole.transform.SetParent(root.transform, false);
            canopyPole.transform.localPosition = new Vector3(0f, 2.1f, 0f);
            ApplyMaterial(canopyPole, Palette.FerrisGold, 0.6f, 0.7f);

            // カップ
            for (int i = 0; i < cupCount; i++)
            {
                float angle = (float)i / cupCount * Mathf.PI * 2f;
                float cx = Mathf.Cos(angle) * (plateRadius - 1.3f);
                float cz = Mathf.Sin(angle) * (plateRadius - 1.3f);

                var subPlate = CreateCylinderGameObject(0.75f, 0.12f, SEG_MED);
                subPlate.name = $"SubPlate_{i}";
                subPlate.transform.SetParent(root.transform, false);
                subPlate.transform.localPosition = new Vector3(cx, 0.38f, cz);
                ApplyMaterial(subPlate, Palette.SpinOrange, 0f, 0.4f);

                var cup = CreateConeGameObject(0.32f, 0.6f, 0.85f, SEG_MED);
                cup.name = $"Cup_{i}";
                cup.transform.SetParent(root.transform, false);
                cup.transform.localPosition = new Vector3(cx, 0.85f, cz);
                Color cc = (i % 4) switch { 0 => Palette.CoasterRed, 1 => Palette.FerrisBlue, 2 => Palette.MerryPink, _ => Palette.SpinYellow };
                ApplyMaterial(cup, cc, 0.1f, 0.5f);

                var handle = CreateCylinderGameObject(0.16f, 0.06f, SEG_LOW);
                handle.name = $"Handle_{i}";
                handle.transform.SetParent(root.transform, false);
                handle.transform.localPosition = new Vector3(cx, 1.5f, cz);
                ApplyMaterial(handle, Palette.FerrisGold, 0.7f, 0.8f);
            }

            return root;
        }

        /// <summary>お化け屋敷のメッシュを生成（高品質版）</summary>
        public static GameObject CreateHauntedHouse(string name)
        {
            var root = new GameObject(name);

            // メイン建物（レンガテクスチャ）
            var body = CreateBoxGameObject(new Vector3(5.5f, 4.5f, 4.5f));
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 2.25f, 0f);
            ApplyPBR(body, ProceduralTextureGenerator.CreateTintedPBRMaterial(
                ProceduralTextureGenerator.Brick, ProceduralTextureGenerator.BrickNormal,
                new Color(0.5f, 0.45f, 0.5f), 0f, 0.2f, 2f));

            // 屋根
            var roof = CreateConeGameObject(4f, 0.05f, 2.8f, 4);
            roof.name = "Roof";
            roof.transform.SetParent(root.transform, false);
            roof.transform.localPosition = new Vector3(0f, 5.9f, 0f);
            roof.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            ApplyMaterial(roof, Palette.HauntPurple, 0f, 0.3f);

            // タワー
            var tower = CreateCylinderGameObject(0.7f, 7f, SEG_MED);
            tower.name = "Tower";
            tower.transform.SetParent(root.transform, false);
            tower.transform.localPosition = new Vector3(2.2f, 3.5f, 1.6f);
            ApplyPBR(tower, ProceduralTextureGenerator.CreateTintedPBRMaterial(
                ProceduralTextureGenerator.Stucco, ProceduralTextureGenerator.StuccoNormal,
                new Color(0.5f, 0.48f, 0.52f), 0f, 0.2f, 2f));

            // 塔屋根
            var towerRoof = CreateConeGameObject(0.9f, 0.05f, 1.8f, SEG_MED);
            towerRoof.name = "TowerRoof";
            towerRoof.transform.SetParent(root.transform, false);
            towerRoof.transform.localPosition = new Vector3(2.2f, 7.9f, 1.6f);
            ApplyMaterial(towerRoof, Palette.HauntPurple, 0f, 0.3f);

            // ドア（木材テクスチャ）
            var door = CreateBoxGameObject(new Vector3(1.2f, 2.3f, 0.12f));
            door.name = "Door";
            door.transform.SetParent(root.transform, false);
            door.transform.localPosition = new Vector3(0f, 1.15f, -2.3f);
            ApplyPBR(door, ProceduralTextureGenerator.WoodPanelMaterial);

            // ドアフレーム
            var doorFrame = CreateBoxGameObject(new Vector3(1.5f, 2.6f, 0.15f));
            doorFrame.name = "DoorFrame";
            doorFrame.transform.SetParent(root.transform, false);
            doorFrame.transform.localPosition = new Vector3(0f, 1.3f, -2.28f);
            ApplyMaterial(doorFrame, new Color(0.2f, 0.15f, 0.1f), 0f, 0.2f);

            // 窓（ガラスマテリアル）× 4
            for (int i = 0; i < 4; i++)
            {
                float wx = (i % 2 == 0) ? -1.4f : 1.4f;
                float wy = (i < 2) ? 2.8f : 3.8f;

                // 窓フレーム
                var wf = CreateBoxGameObject(new Vector3(0.8f, 0.8f, 0.08f));
                wf.name = $"WindowFrame_{i}";
                wf.transform.SetParent(root.transform, false);
                wf.transform.localPosition = new Vector3(wx, wy, -2.28f);
                ApplyMaterial(wf, new Color(0.2f, 0.15f, 0.1f), 0f, 0.2f);

                // ガラス
                var window = CreateBoxGameObject(new Vector3(0.6f, 0.6f, 0.05f));
                window.name = $"Window_{i}";
                window.transform.SetParent(root.transform, false);
                window.transform.localPosition = new Vector3(wx, wy, -2.32f);
                ApplyPBR(window, ProceduralTextureGenerator.GlassMaterial);
            }

            // フェンス
            for (int i = 0; i < 8; i++)
            {
                float fx = -3f + i * 0.85f;
                var post = CreateCylinderGameObject(0.04f, 1.2f, 8);
                post.name = $"Fence_{i}";
                post.transform.SetParent(root.transform, false);
                post.transform.localPosition = new Vector3(fx, 0.6f, -3f);
                ApplyMaterial(post, new Color(0.2f, 0.2f, 0.22f), 0.5f, 0.4f);
            }

            return root;
        }

        /// <summary>ショップ建物メッシュを生成（高品質版）</summary>
        public static GameObject CreateShopBuilding(string name, Color mainColor)
        {
            var root = new GameObject(name);

            // 本体（スタッコ壁テクスチャ + カラーティント）
            var body = CreateBoxGameObject(new Vector3(4f, 3.2f, 4f));
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            ApplyPBR(body, ProceduralTextureGenerator.CreateTintedPBRMaterial(
                ProceduralTextureGenerator.Stucco, ProceduralTextureGenerator.StuccoNormal,
                mainColor, 0f, 0.25f, 2f));

            // 屋根（屋根瓦テクスチャ）
            var roof = CreateBoxGameObject(new Vector3(4.5f, 0.35f, 4.5f));
            roof.name = "Roof";
            roof.transform.SetParent(root.transform, false);
            roof.transform.localPosition = new Vector3(0f, 3.4f, 0f);
            ApplyPBR(roof, ProceduralTextureGenerator.RoofTileMaterial);

            // コーニス（屋根と壁の境目の装飾帯）
            var cornice = CreateBoxGameObject(new Vector3(4.3f, 0.15f, 4.3f));
            cornice.name = "Cornice";
            cornice.transform.SetParent(root.transform, false);
            cornice.transform.localPosition = new Vector3(0f, 3.12f, 0f);
            ApplyMaterial(cornice, Color.white, 0f, 0.4f);

            // ひさし（ストライプテクスチャ）
            var awning = CreateBoxGameObject(new Vector3(4.2f, 0.08f, 1.4f));
            awning.name = "Awning";
            awning.transform.SetParent(root.transform, false);
            awning.transform.localPosition = new Vector3(0f, 2.6f, -2.3f);
            awning.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
            var awningMat = ProceduralTextureGenerator.CreateTexturedMaterial(
                ProceduralTextureGenerator.Stripe, mainColor * 1.1f, 0f, 0.3f, 2f);
            ApplyPBR(awning, awningMat);

            // 看板
            var sign = CreateBoxGameObject(new Vector3(2.8f, 0.7f, 0.12f));
            sign.name = "Sign";
            sign.transform.SetParent(root.transform, false);
            sign.transform.localPosition = new Vector3(0f, 3.9f, -2f);
            ApplyPBR(sign, ProceduralTextureGenerator.WoodPanelMaterial);

            // ショーウィンドウ（ガラスマテリアル）
            var display = CreateBoxGameObject(new Vector3(2.2f, 1.4f, 0.06f));
            display.name = "Display";
            display.transform.SetParent(root.transform, false);
            display.transform.localPosition = new Vector3(0f, 1.3f, -2.02f);
            ApplyPBR(display, ProceduralTextureGenerator.GlassMaterial);

            // ウィンドウフレーム
            var wFrame = CreateBoxGameObject(new Vector3(2.4f, 1.6f, 0.08f));
            wFrame.name = "WindowFrame";
            wFrame.transform.SetParent(root.transform, false);
            wFrame.transform.localPosition = new Vector3(0f, 1.3f, -2.0f);
            ApplyPBR(wFrame, ProceduralTextureGenerator.WoodPanelMaterial);

            // ドア
            var door = CreateBoxGameObject(new Vector3(0.9f, 2.2f, 0.08f));
            door.name = "Door";
            door.transform.SetParent(root.transform, false);
            door.transform.localPosition = new Vector3(-1.3f, 1.1f, -2.02f);
            ApplyPBR(door, ProceduralTextureGenerator.WoodPanelMaterial);

            return root;
        }

        /// <summary>トイレ建物メッシュを生成（高品質版）</summary>
        public static GameObject CreateToiletBuilding(string name)
        {
            var root = new GameObject(name);

            // 本体（タイルテクスチャ）
            var body = CreateBoxGameObject(new Vector3(3.5f, 2.8f, 3.5f));
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            ApplyPBR(body, ProceduralTextureGenerator.TileFloorMaterial);

            // 屋根
            var roof = CreateBoxGameObject(new Vector3(3.8f, 0.25f, 3.8f));
            roof.name = "Roof";
            roof.transform.SetParent(root.transform, false);
            roof.transform.localPosition = new Vector3(0f, 2.95f, 0f);
            ApplyMaterial(roof, Palette.ToiletBlue, 0f, 0.4f);

            // コーニス
            var cornice = CreateBoxGameObject(new Vector3(3.7f, 0.1f, 3.7f));
            cornice.name = "Cornice";
            cornice.transform.SetParent(root.transform, false);
            cornice.transform.localPosition = new Vector3(0f, 2.78f, 0f);
            ApplyMaterial(cornice, Palette.ToiletBlue * 0.8f, 0f, 0.4f);

            // ドア（木材テクスチャ）× 2
            for (int i = 0; i < 2; i++)
            {
                float dx = (i == 0) ? -0.75f : 0.75f;
                var door = CreateBoxGameObject(new Vector3(0.85f, 2.1f, 0.06f));
                door.name = $"Door_{i}";
                door.transform.SetParent(root.transform, false);
                door.transform.localPosition = new Vector3(dx, 1.05f, -1.78f);
                ApplyPBR(door, ProceduralTextureGenerator.WoodPanelMaterial);

                // ドアフレーム
                var frame = CreateBoxGameObject(new Vector3(0.95f, 2.3f, 0.08f));
                frame.name = $"DoorFrame_{i}";
                frame.transform.SetParent(root.transform, false);
                frame.transform.localPosition = new Vector3(dx, 1.15f, -1.76f);
                ApplyMaterial(frame, Palette.ToiletBlue * 0.7f, 0f, 0.3f);
            }

            // WCサイン
            var wcSign = CreateBoxGameObject(new Vector3(1.8f, 0.45f, 0.06f));
            wcSign.name = "WCSign";
            wcSign.transform.SetParent(root.transform, false);
            wcSign.transform.localPosition = new Vector3(0f, 3.1f, -1.78f);
            ApplyMaterial(wcSign, Palette.ToiletBlue, 0.1f, 0.5f);

            return root;
        }

        /// <summary>ベンチメッシュを生成（高品質版）</summary>
        public static GameObject CreateBench(string name)
        {
            var root = new GameObject(name);

            // 座面（木材テクスチャ）
            var seat = CreateBoxGameObject(new Vector3(2f, 0.09f, 0.55f));
            seat.name = "Seat";
            seat.transform.SetParent(root.transform, false);
            seat.transform.localPosition = new Vector3(0f, 0.52f, 0f);
            ApplyPBR(seat, ProceduralTextureGenerator.WoodPanelMaterial);

            // 座面の板（2枚に分割して質感アップ）
            var seat2 = CreateBoxGameObject(new Vector3(2f, 0.09f, 0.52f));
            seat2.name = "Seat2";
            seat2.transform.SetParent(root.transform, false);
            seat2.transform.localPosition = new Vector3(0f, 0.48f, 0.02f);
            ApplyPBR(seat2, ProceduralTextureGenerator.WoodPanelMaterial);

            // 背もたれ
            var back = CreateBoxGameObject(new Vector3(2f, 0.55f, 0.07f));
            back.name = "Back";
            back.transform.SetParent(root.transform, false);
            back.transform.localPosition = new Vector3(0f, 0.78f, -0.24f);
            back.transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);
            ApplyPBR(back, ProceduralTextureGenerator.WoodPanelMaterial);

            // 脚（金属）× 2
            for (int i = 0; i < 2; i++)
            {
                float lx = (i == 0) ? -0.75f : 0.75f;
                var leg = CreateBoxGameObject(new Vector3(0.07f, 0.52f, 0.45f));
                leg.name = $"Leg_{i}";
                leg.transform.SetParent(root.transform, false);
                leg.transform.localPosition = new Vector3(lx, 0.26f, 0f);
                ApplyPBR(leg, ProceduralTextureGenerator.MetalRideMaterial);
            }

            // アームレスト
            for (int i = 0; i < 2; i++)
            {
                float ax = (i == 0) ? -0.95f : 0.95f;
                var arm = CreateBoxGameObject(new Vector3(0.06f, 0.3f, 0.06f));
                arm.name = $"Arm_{i}";
                arm.transform.SetParent(root.transform, false);
                arm.transform.localPosition = new Vector3(ax, 0.7f, -0.1f);
                ApplyPBR(arm, ProceduralTextureGenerator.MetalRideMaterial);
            }

            return root;
        }

        /// <summary>スタッフルーム建物メッシュを生成（高品質版）</summary>
        public static GameObject CreateStaffRoom(string name)
        {
            var root = new GameObject(name);

            // 本体（レンガテクスチャ + 緑ティント）
            var body = CreateBoxGameObject(new Vector3(5.5f, 3.2f, 4.5f));
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            ApplyPBR(body, ProceduralTextureGenerator.CreateTintedPBRMaterial(
                ProceduralTextureGenerator.Stucco, ProceduralTextureGenerator.StuccoNormal,
                new Color(0.7f, 0.85f, 0.7f), 0f, 0.25f, 2f));

            // 屋根（屋根瓦テクスチャ）
            var roof = CreateBoxGameObject(new Vector3(6f, 0.35f, 5f));
            roof.name = "Roof";
            roof.transform.SetParent(root.transform, false);
            roof.transform.localPosition = new Vector3(0f, 3.4f, 0f);
            ApplyPBR(roof, ProceduralTextureGenerator.RoofTileMaterial);

            // コーニス
            var cornice = CreateBoxGameObject(new Vector3(5.8f, 0.12f, 4.8f));
            cornice.name = "Cornice";
            cornice.transform.SetParent(root.transform, false);
            cornice.transform.localPosition = new Vector3(0f, 3.15f, 0f);
            ApplyMaterial(cornice, Color.white, 0f, 0.4f);

            // 窓 × 3（ガラスマテリアル）
            for (int i = 0; i < 3; i++)
            {
                float wx = (i - 1) * 1.6f;
                var window = CreateBoxGameObject(new Vector3(0.85f, 0.9f, 0.06f));
                window.name = $"Window_{i}";
                window.transform.SetParent(root.transform, false);
                window.transform.localPosition = new Vector3(wx, 2.1f, -2.28f);
                ApplyPBR(window, ProceduralTextureGenerator.GlassMaterial);

                var wf = CreateBoxGameObject(new Vector3(0.95f, 1.0f, 0.08f));
                wf.name = $"WindowFrame_{i}";
                wf.transform.SetParent(root.transform, false);
                wf.transform.localPosition = new Vector3(wx, 2.1f, -2.26f);
                ApplyPBR(wf, ProceduralTextureGenerator.WoodPanelMaterial);
            }

            // ドア（木材テクスチャ）
            var door = CreateBoxGameObject(new Vector3(1.1f, 2.4f, 0.06f));
            door.name = "Door";
            door.transform.SetParent(root.transform, false);
            door.transform.localPosition = new Vector3(0f, 1.2f, -2.28f);
            ApplyPBR(door, ProceduralTextureGenerator.WoodPanelMaterial);

            return root;
        }

        // ============================================================
        // キャラクターメッシュ（高品質版）
        // ============================================================

        /// <summary>来場者のスタイライズドメッシュを生成（高品質版）</summary>
        public static GameObject CreateVisitorMesh(Color bodyColor)
        {
            var root = new GameObject("VisitorVisual");

            // 胴体
            var body = CreateSphereGameObject(0.28f, SEG_MED);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            body.transform.localScale = new Vector3(1f, 1.4f, 0.85f);
            ApplyMaterial(body, bodyColor, 0f, 0.4f);

            // 頭
            var head = CreateSphereGameObject(0.2f, SEG_MED);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            ApplyMaterial(head, new Color(0.95f, 0.82f, 0.72f), 0f, 0.4f);

            // 左腕
            var armL = CreateCylinderGameObject(0.045f, 0.35f, SEG_LOW);
            armL.name = "ArmL";
            armL.transform.SetParent(root.transform, false);
            armL.transform.localPosition = new Vector3(-0.22f, 0.75f, 0f);
            armL.transform.localRotation = Quaternion.Euler(0f, 0f, 15f);
            ApplyMaterial(armL, bodyColor * 0.9f, 0f, 0.3f);

            // 右腕
            var armR = CreateCylinderGameObject(0.045f, 0.35f, SEG_LOW);
            armR.name = "ArmR";
            armR.transform.SetParent(root.transform, false);
            armR.transform.localPosition = new Vector3(0.22f, 0.75f, 0f);
            armR.transform.localRotation = Quaternion.Euler(0f, 0f, -15f);
            ApplyMaterial(armR, bodyColor * 0.9f, 0f, 0.3f);

            // 左足
            var legL = CreateCylinderGameObject(0.065f, 0.42f, SEG_LOW);
            legL.name = "LegL";
            legL.transform.SetParent(root.transform, false);
            legL.transform.localPosition = new Vector3(-0.1f, 0.21f, 0f);
            ApplyMaterial(legL, new Color(0.25f, 0.25f, 0.35f), 0f, 0.3f);

            // 右足
            var legR = CreateCylinderGameObject(0.065f, 0.42f, SEG_LOW);
            legR.name = "LegR";
            legR.transform.SetParent(root.transform, false);
            legR.transform.localPosition = new Vector3(0.1f, 0.21f, 0f);
            ApplyMaterial(legR, new Color(0.25f, 0.25f, 0.35f), 0f, 0.3f);

            // 靴
            for (int i = 0; i < 2; i++)
            {
                float sx = (i == 0) ? -0.1f : 0.1f;
                var shoe = CreateSphereGameObject(0.07f, 8);
                shoe.name = $"Shoe_{i}";
                shoe.transform.SetParent(root.transform, false);
                shoe.transform.localPosition = new Vector3(sx, 0.04f, 0.02f);
                shoe.transform.localScale = new Vector3(1f, 0.6f, 1.3f);
                ApplyMaterial(shoe, new Color(0.15f, 0.12f, 0.1f), 0f, 0.4f);
            }

            return root;
        }

        /// <summary>スタッフのスタイライズドメッシュを生成（高品質版）</summary>
        public static GameObject CreateStaffMesh(Color bodyColor)
        {
            var root = CreateVisitorMesh(bodyColor);
            root.name = "StaffVisual";

            // 帽子
            var hat = CreateCylinderGameObject(0.22f, 0.14f, SEG_MED);
            hat.name = "Hat";
            hat.transform.SetParent(root.transform, false);
            hat.transform.localPosition = new Vector3(0f, 1.48f, 0f);
            ApplyMaterial(hat, bodyColor * 0.7f, 0f, 0.4f);

            // 帽子つば
            var brim = CreateCylinderGameObject(0.28f, 0.035f, SEG_MED);
            brim.name = "Brim";
            brim.transform.SetParent(root.transform, false);
            brim.transform.localPosition = new Vector3(0f, 1.42f, 0f);
            ApplyMaterial(brim, bodyColor * 0.7f, 0f, 0.4f);

            // ベルト
            var belt = CreateCylinderGameObject(0.29f, 0.05f, SEG_MED);
            belt.name = "Belt";
            belt.transform.SetParent(root.transform, false);
            belt.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            ApplyMaterial(belt, new Color(0.15f, 0.12f, 0.1f), 0.1f, 0.5f);

            return root;
        }

        // ============================================================
        // プリミティブ生成ヘルパー
        // ============================================================

        /// <summary>パスに沿ったチューブメッシュを生成</summary>
        private static GameObject CreateTubeAlongPath(Vector3[] path, float radius, int radialSegments, Vector3 offset)
        {
            var go = new GameObject("Tube");
            var mf = go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();

            int pathLen = path.Length;
            int vertCount = pathLen * radialSegments;
            int triCount = (pathLen - 1) * radialSegments * 6;

            var vertices = new Vector3[vertCount];
            var normals = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var triangles = new int[triCount];

            for (int i = 0; i < pathLen; i++)
            {
                Vector3 forward = Vector3.forward;
                if (i < pathLen - 1) forward = (path[i + 1] - path[i]).normalized;
                else if (i > 0) forward = (path[i] - path[i - 1]).normalized;
                if (forward == Vector3.zero) forward = Vector3.forward;

                Vector3 up = Vector3.up;
                if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.99f) up = Vector3.right;
                Vector3 right = Vector3.Cross(forward, up).normalized;
                up = Vector3.Cross(right, forward).normalized;

                for (int j = 0; j < radialSegments; j++)
                {
                    float angle = (float)j / radialSegments * Mathf.PI * 2f;
                    Vector3 normal = (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)).normalized;
                    int idx = i * radialSegments + j;
                    vertices[idx] = path[i] + offset + normal * radius;
                    normals[idx] = normal;
                    uvs[idx] = new Vector2((float)j / radialSegments, (float)i / pathLen);
                }
            }

            int ti = 0;
            for (int i = 0; i < pathLen - 1; i++)
            {
                for (int j = 0; j < radialSegments; j++)
                {
                    int cur = i * radialSegments + j;
                    int next = i * radialSegments + (j + 1) % radialSegments;
                    int curNext = (i + 1) * radialSegments + j;
                    int nextNext = (i + 1) * radialSegments + (j + 1) % radialSegments;
                    triangles[ti++] = cur; triangles[ti++] = curNext; triangles[ti++] = next;
                    triangles[ti++] = next; triangles[ti++] = curNext; triangles[ti++] = nextNext;
                }
            }

            var mesh = new Mesh();
            mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uvs; mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mf.mesh = mesh;
            return go;
        }

        /// <summary>ボックスメッシュのGameObjectを生成</summary>
        public static GameObject CreateBoxGameObject(Vector3 size)
        {
            var go = new GameObject("Box");
            var mf = go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            float x = size.x * 0.5f, y = size.y * 0.5f, z = size.z * 0.5f;

            Vector3[] vertices = {
                new(-x,-y,-z), new(-x,y,-z), new(x,y,-z), new(x,-y,-z),
                new(x,-y,z), new(x,y,z), new(-x,y,z), new(-x,-y,z),
                new(-x,y,-z), new(-x,y,z), new(x,y,z), new(x,y,-z),
                new(-x,-y,z), new(-x,-y,-z), new(x,-y,-z), new(x,-y,z),
                new(-x,-y,z), new(-x,y,z), new(-x,y,-z), new(-x,-y,-z),
                new(x,-y,-z), new(x,y,-z), new(x,y,z), new(x,-y,z),
            };
            Vector3[] normals = {
                -Vector3.forward,-Vector3.forward,-Vector3.forward,-Vector3.forward,
                Vector3.forward,Vector3.forward,Vector3.forward,Vector3.forward,
                Vector3.up,Vector3.up,Vector3.up,Vector3.up,
                -Vector3.up,-Vector3.up,-Vector3.up,-Vector3.up,
                -Vector3.right,-Vector3.right,-Vector3.right,-Vector3.right,
                Vector3.right,Vector3.right,Vector3.right,Vector3.right,
            };
            Vector2[] uvs = new Vector2[24];
            for (int i = 0; i < 6; i++)
            {
                uvs[i*4]=new Vector2(0,0); uvs[i*4+1]=new Vector2(0,1);
                uvs[i*4+2]=new Vector2(1,1); uvs[i*4+3]=new Vector2(1,0);
            }
            int[] tris = new int[36];
            for (int i = 0; i < 6; i++)
            {
                int b=i*4;
                tris[i*6]=b; tris[i*6+1]=b+1; tris[i*6+2]=b+2;
                tris[i*6+3]=b; tris[i*6+4]=b+2; tris[i*6+5]=b+3;
            }

            var mesh = new Mesh();
            mesh.vertices=vertices; mesh.normals=normals; mesh.uv=uvs; mesh.triangles=tris;
            mesh.RecalculateBounds();
            mf.mesh = mesh;
            return go;
        }

        /// <summary>シリンダーメッシュのGameObjectを生成</summary>
        public static GameObject CreateCylinderGameObject(float radius, float height, int segments)
        {
            var go = new GameObject("Cylinder");
            var mf = go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();

            var verts = new List<Vector3>(); var norms = new List<Vector3>();
            var uvList = new List<Vector2>(); var tris = new List<int>();
            float halfH = height * 0.5f;

            // 側面
            for (int i = 0; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                float cx = Mathf.Cos(a) * radius, cz = Mathf.Sin(a) * radius;
                Vector3 n = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)).normalized;
                float u = (float)i / segments;
                verts.Add(new Vector3(cx,-halfH,cz)); norms.Add(n); uvList.Add(new Vector2(u,0f));
                verts.Add(new Vector3(cx, halfH,cz)); norms.Add(n); uvList.Add(new Vector2(u,1f));
            }
            for (int i = 0; i < segments; i++)
            {
                int b=i*2;
                tris.Add(b); tris.Add(b+1); tris.Add(b+3);
                tris.Add(b); tris.Add(b+3); tris.Add(b+2);
            }

            // 上面
            int tc = verts.Count;
            verts.Add(new Vector3(0f,halfH,0f)); norms.Add(Vector3.up); uvList.Add(new Vector2(0.5f,0.5f));
            for (int i = 0; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a)*radius,halfH,Mathf.Sin(a)*radius));
                norms.Add(Vector3.up);
                uvList.Add(new Vector2(Mathf.Cos(a)*0.5f+0.5f, Mathf.Sin(a)*0.5f+0.5f));
            }
            for (int i = 0; i < segments; i++) { tris.Add(tc); tris.Add(tc+1+i); tris.Add(tc+2+i); }

            // 下面
            int bc = verts.Count;
            verts.Add(new Vector3(0f,-halfH,0f)); norms.Add(-Vector3.up); uvList.Add(new Vector2(0.5f,0.5f));
            for (int i = 0; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a)*radius,-halfH,Mathf.Sin(a)*radius));
                norms.Add(-Vector3.up);
                uvList.Add(new Vector2(Mathf.Cos(a)*0.5f+0.5f, Mathf.Sin(a)*0.5f+0.5f));
            }
            for (int i = 0; i < segments; i++) { tris.Add(bc); tris.Add(bc+2+i); tris.Add(bc+1+i); }

            var mesh = new Mesh();
            mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0,uvList); mesh.SetTriangles(tris,0);
            mesh.RecalculateBounds();
            mf.mesh = mesh;
            return go;
        }

        /// <summary>截頭円錐(コーン)メッシュのGameObjectを生成</summary>
        public static GameObject CreateConeGameObject(float bottomRadius, float topRadius, float height, int segments)
        {
            var go = new GameObject("Cone");
            var mf = go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();

            var verts = new List<Vector3>(); var norms = new List<Vector3>();
            var uvList = new List<Vector2>(); var tris = new List<int>();
            float halfH = height * 0.5f;
            float slopeA = Mathf.Atan2(bottomRadius - topRadius, height);
            float ny = Mathf.Sin(slopeA), nr = Mathf.Cos(slopeA);

            for (int i = 0; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                Vector3 n = new Vector3(c*nr, ny, s*nr).normalized;
                float u = (float)i / segments;
                verts.Add(new Vector3(c*bottomRadius,-halfH,s*bottomRadius)); norms.Add(n); uvList.Add(new Vector2(u,0f));
                verts.Add(new Vector3(c*topRadius,halfH,s*topRadius)); norms.Add(n); uvList.Add(new Vector2(u,1f));
            }
            for (int i = 0; i < segments; i++)
            {
                int b=i*2;
                tris.Add(b); tris.Add(b+1); tris.Add(b+3);
                tris.Add(b); tris.Add(b+3); tris.Add(b+2);
            }

            // 下面
            int bc = verts.Count;
            verts.Add(new Vector3(0f,-halfH,0f)); norms.Add(-Vector3.up); uvList.Add(new Vector2(0.5f,0.5f));
            for (int i = 0; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a)*bottomRadius,-halfH,Mathf.Sin(a)*bottomRadius));
                norms.Add(-Vector3.up); uvList.Add(new Vector2(Mathf.Cos(a)*0.5f+0.5f,Mathf.Sin(a)*0.5f+0.5f));
            }
            for (int i = 0; i < segments; i++) { tris.Add(bc); tris.Add(bc+2+i); tris.Add(bc+1+i); }

            if (topRadius > 0.01f)
            {
                int tc = verts.Count;
                verts.Add(new Vector3(0f,halfH,0f)); norms.Add(Vector3.up); uvList.Add(new Vector2(0.5f,0.5f));
                for (int i = 0; i <= segments; i++)
                {
                    float a = (float)i / segments * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Cos(a)*topRadius,halfH,Mathf.Sin(a)*topRadius));
                    norms.Add(Vector3.up); uvList.Add(new Vector2(Mathf.Cos(a)*0.5f+0.5f,Mathf.Sin(a)*0.5f+0.5f));
                }
                for (int i = 0; i < segments; i++) { tris.Add(tc); tris.Add(tc+1+i); tris.Add(tc+2+i); }
            }

            var mesh = new Mesh();
            mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0,uvList); mesh.SetTriangles(tris,0);
            mesh.RecalculateBounds();
            mf.mesh = mesh;
            return go;
        }

        /// <summary>球メッシュのGameObjectを生成</summary>
        public static GameObject CreateSphereGameObject(float radius, int segments)
        {
            var go = new GameObject("Sphere");
            var mf = go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();

            int rings = segments, slices = segments * 2;
            var verts = new List<Vector3>(); var norms = new List<Vector3>();
            var uvList = new List<Vector2>(); var tris = new List<int>();

            for (int lat = 0; lat <= rings; lat++)
            {
                float theta = (float)lat / rings * Mathf.PI;
                float sinT = Mathf.Sin(theta), cosT = Mathf.Cos(theta);
                for (int lon = 0; lon <= slices; lon++)
                {
                    float phi = (float)lon / slices * Mathf.PI * 2f;
                    Vector3 n = new Vector3(sinT * Mathf.Cos(phi), cosT, sinT * Mathf.Sin(phi));
                    verts.Add(n * radius); norms.Add(n);
                    uvList.Add(new Vector2((float)lon / slices, (float)lat / rings));
                }
            }
            for (int lat = 0; lat < rings; lat++)
            {
                for (int lon = 0; lon < slices; lon++)
                {
                    int c = lat * (slices + 1) + lon, n = c + slices + 1;
                    tris.Add(c); tris.Add(n); tris.Add(c+1);
                    tris.Add(c+1); tris.Add(n); tris.Add(n+1);
                }
            }

            var mesh = new Mesh();
            mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0,uvList); mesh.SetTriangles(tris,0);
            mesh.RecalculateBounds();
            mf.mesh = mesh;
            return go;
        }

        // ============================================================
        // マテリアル適用
        // ============================================================

        /// <summary>Standard Shaderマテリアルを適用する（フラットカラー）</summary>
        public static void ApplyMaterial(GameObject go, Color color, float metallic = 0f, float smoothness = 0.5f)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;

            var shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("UI/Default");
            if (shader == null) return;

            var mat = new Material(shader);
            mat.color = color;

            if (shader.name == "Standard")
            {
                mat.SetFloat("_Metallic", metallic);
                mat.SetFloat("_Glossiness", smoothness);

                if (color.a < 1f)
                {
                    mat.SetFloat("_Mode", 3);
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = 3000;
                }
            }

            renderer.material = mat;
        }

        /// <summary>PBRマテリアル（テクスチャ+法線マップ付き）を直接適用する</summary>
        public static void ApplyPBR(GameObject go, Material mat)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null && mat != null) renderer.material = mat;
        }
    }
}
