// ============================================================
// ThemeParkGame - ProceduralMeshGenerator
// プロシージャルメッシュ生成ユーティリティ
// PrimitiveType.Cubeから脱却し、アトラクション固有の3Dモデルを生成
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace ThemeParkGame.Core
{
    /// <summary>
    /// プロシージャルメッシュ生成。Unity Mesh APIを使用して
    /// アトラクション・施設・キャラクターの3Dモデルをランタイム生成する。
    /// ProBuilder不要 - 純粋なMesh APIで動作するためWebGL対応。
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
                new Color(0.95f, 0.45f, 0.45f), // 赤
                new Color(0.45f, 0.75f, 0.95f), // 青
                new Color(0.45f, 0.90f, 0.50f), // 緑
                new Color(0.95f, 0.85f, 0.35f), // 黄
                new Color(0.80f, 0.50f, 0.90f), // 紫
                new Color(0.95f, 0.65f, 0.35f), // オレンジ
                new Color(0.70f, 0.85f, 0.95f), // 水色
                new Color(0.95f, 0.70f, 0.80f), // ピンク
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

            // VIP
            public static readonly Color VIPGold = new Color(1.00f, 0.84f, 0.00f);
        }

        // ============================================================
        // アトラクション生成
        // ============================================================

        /// <summary>ジェットコースターのメッシュを生成</summary>
        public static GameObject CreateRollerCoaster(string name)
        {
            var root = new GameObject(name);

            // トラック（ヘリカルパスに沿ったチューブ）
            int trackPoints = 60;
            Vector3[] trackPath = new Vector3[trackPoints];
            for (int i = 0; i < trackPoints; i++)
            {
                float t = (float)i / (trackPoints - 1);
                float angle = t * Mathf.PI * 3f; // 1.5周
                float radius = 2.0f + Mathf.Sin(t * Mathf.PI * 2f) * 0.5f;
                float height = Mathf.Sin(t * Mathf.PI) * 5f + 1f + Mathf.Sin(t * Mathf.PI * 4f) * 1.5f;
                trackPath[i] = new Vector3(
                    Mathf.Cos(angle) * radius,
                    height,
                    Mathf.Sin(angle) * radius
                );
            }

            // レール2本（左右にオフセット）
            var leftRail = CreateTubeAlongPath(trackPath, 0.08f, 8, new Vector3(-0.15f, 0f, 0f));
            leftRail.name = "LeftRail";
            leftRail.transform.SetParent(root.transform, false);
            ApplyMaterial(leftRail, Palette.CoasterRail, 0.7f, 0.8f);

            var rightRail = CreateTubeAlongPath(trackPath, 0.08f, 8, new Vector3(0.15f, 0f, 0f));
            rightRail.name = "RightRail";
            rightRail.transform.SetParent(root.transform, false);
            ApplyMaterial(rightRail, Palette.CoasterRail, 0.7f, 0.8f);

            // クロスタイ（枕木）
            for (int i = 0; i < trackPoints - 1; i += 3)
            {
                var tie = CreateBoxGameObject(new Vector3(0.5f, 0.06f, 0.08f));
                tie.name = $"Tie_{i}";
                tie.transform.SetParent(root.transform, false);
                tie.transform.position = trackPath[i];
                if (i + 1 < trackPoints)
                {
                    Vector3 dir = (trackPath[i + 1] - trackPath[i]).normalized;
                    if (dir != Vector3.zero)
                        tie.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                }
                ApplyMaterial(tie, Palette.WoodBrown, 0f, 0.3f);
            }

            // サポート柱
            for (int i = 0; i < trackPoints; i += 8)
            {
                float h = trackPath[i].y;
                if (h < 0.5f) continue;
                var pillar = CreateCylinderGameObject(0.08f, h, 8);
                pillar.name = $"Pillar_{i}";
                pillar.transform.SetParent(root.transform, false);
                pillar.transform.position = new Vector3(trackPath[i].x, h * 0.5f, trackPath[i].z);
                ApplyMaterial(pillar, Palette.BenchMetal, 0.6f, 0.6f);
            }

            // ステーションプラットフォーム
            var station = CreateBoxGameObject(new Vector3(3f, 0.3f, 2f));
            station.name = "Station";
            station.transform.SetParent(root.transform, false);
            station.transform.localPosition = new Vector3(trackPath[0].x, 0.15f, trackPath[0].z);
            ApplyMaterial(station, Palette.CoasterRed, 0f, 0.4f);

            // ステーション屋根
            var stationRoof = CreateBoxGameObject(new Vector3(3.5f, 0.1f, 2.5f));
            stationRoof.name = "StationRoof";
            stationRoof.transform.SetParent(root.transform, false);
            stationRoof.transform.localPosition = new Vector3(trackPath[0].x, 3f, trackPath[0].z);
            ApplyMaterial(stationRoof, Palette.ShopRoof, 0f, 0.3f);

            // 屋根支柱4本
            for (int i = 0; i < 4; i++)
            {
                float ox = (i % 2 == 0) ? -1.3f : 1.3f;
                float oz = (i < 2) ? -0.9f : 0.9f;
                var roofPillar = CreateCylinderGameObject(0.06f, 2.7f, 6);
                roofPillar.name = $"RoofPillar_{i}";
                roofPillar.transform.SetParent(root.transform, false);
                roofPillar.transform.localPosition = new Vector3(
                    trackPath[0].x + ox, 1.5f, trackPath[0].z + oz);
                ApplyMaterial(roofPillar, Palette.BenchMetal, 0.5f, 0.5f);
            }

            return root;
        }

        /// <summary>観覧車のメッシュを生成</summary>
        public static GameObject CreateFerrisWheel(string name)
        {
            var root = new GameObject(name);
            float wheelRadius = 5f;
            int gondolaCount = 12;

            // A字型支柱（左右）
            for (int side = -1; side <= 1; side += 2)
            {
                float sx = side * 1.5f;

                // 前脚
                var legFront = CreateCylinderGameObject(0.15f, 12f, 8);
                legFront.name = $"LegFront_{(side > 0 ? "R" : "L")}";
                legFront.transform.SetParent(root.transform, false);
                legFront.transform.localPosition = new Vector3(sx, 6f, -0.8f);
                legFront.transform.localRotation = Quaternion.Euler(0f, 0f, side * 5f);
                ApplyMaterial(legFront, Palette.FerrisBlue, 0.5f, 0.6f);

                // 後脚
                var legBack = CreateCylinderGameObject(0.15f, 12f, 8);
                legBack.name = $"LegBack_{(side > 0 ? "R" : "L")}";
                legBack.transform.SetParent(root.transform, false);
                legBack.transform.localPosition = new Vector3(sx, 6f, 0.8f);
                legBack.transform.localRotation = Quaternion.Euler(0f, 0f, side * 5f);
                ApplyMaterial(legBack, Palette.FerrisBlue, 0.5f, 0.6f);
            }

            // 中央ハブ
            var hub = CreateCylinderGameObject(0.5f, 0.4f, 16);
            hub.name = "Hub";
            hub.transform.SetParent(root.transform, false);
            hub.transform.localPosition = new Vector3(0f, 10.5f, 0f);
            hub.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ApplyMaterial(hub, Palette.FerrisGold, 0.7f, 0.8f);

            // スポーク（放射状の棒）
            for (int i = 0; i < gondolaCount; i++)
            {
                float angle = (float)i / gondolaCount * Mathf.PI * 2f;
                float cx = Mathf.Cos(angle) * wheelRadius * 0.5f;
                float cy = Mathf.Sin(angle) * wheelRadius * 0.5f + 10.5f;

                var spoke = CreateCylinderGameObject(0.05f, wheelRadius, 6);
                spoke.name = $"Spoke_{i}";
                spoke.transform.SetParent(root.transform, false);
                spoke.transform.localPosition = new Vector3(0f, cy, 0f);
                float spokeAngle = angle * Mathf.Rad2Deg;
                spoke.transform.localRotation = Quaternion.Euler(0f, 0f, spokeAngle);
                ApplyMaterial(spoke, Palette.FerrisBlue, 0.4f, 0.5f);
            }

            // リムセグメント
            int rimSegments = 36;
            for (int i = 0; i < rimSegments; i++)
            {
                float a1 = (float)i / rimSegments * Mathf.PI * 2f;
                float a2 = (float)(i + 1) / rimSegments * Mathf.PI * 2f;
                Vector3 p1 = new Vector3(0f, Mathf.Sin(a1) * wheelRadius + 10.5f, Mathf.Cos(a1) * wheelRadius);
                Vector3 p2 = new Vector3(0f, Mathf.Sin(a2) * wheelRadius + 10.5f, Mathf.Cos(a2) * wheelRadius);

                float segLen = Vector3.Distance(p1, p2);
                var rimSeg = CreateCylinderGameObject(0.08f, segLen, 6);
                rimSeg.name = $"Rim_{i}";
                rimSeg.transform.SetParent(root.transform, false);
                rimSeg.transform.localPosition = (p1 + p2) * 0.5f;
                rimSeg.transform.up = (p2 - p1).normalized;
                ApplyMaterial(rimSeg, Palette.FerrisGold, 0.6f, 0.7f);
            }

            // ゴンドラ
            for (int i = 0; i < gondolaCount; i++)
            {
                float angle = (float)i / gondolaCount * Mathf.PI * 2f;
                float gx = 0f;
                float gy = Mathf.Sin(angle) * wheelRadius + 10.5f;
                float gz = Mathf.Cos(angle) * wheelRadius;

                // ゴンドラ本体（小さなボックス）
                var gondola = CreateBoxGameObject(new Vector3(0.8f, 0.8f, 0.6f));
                gondola.name = $"Gondola_{i}";
                gondola.transform.SetParent(root.transform, false);
                gondola.transform.localPosition = new Vector3(gx, gy - 0.6f, gz);

                Color gondolaColor = (i % 4) switch
                {
                    0 => Palette.CoasterRed,
                    1 => Palette.FerrisBlue,
                    2 => Palette.FerrisGold,
                    _ => Palette.MerryPink
                };
                ApplyMaterial(gondola, gondolaColor, 0f, 0.4f);

                // ゴンドラ吊りワイヤー
                var wire = CreateCylinderGameObject(0.02f, 0.6f, 4);
                wire.name = $"Wire_{i}";
                wire.transform.SetParent(root.transform, false);
                wire.transform.localPosition = new Vector3(gx, gy - 0.2f, gz);
                ApplyMaterial(wire, Palette.BenchMetal, 0.5f, 0.5f);
            }

            // 基礎プラットフォーム
            var platform = CreateBoxGameObject(new Vector3(5f, 0.3f, 5f));
            platform.name = "Platform";
            platform.transform.SetParent(root.transform, false);
            platform.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            ApplyMaterial(platform, Palette.PathGrey, 0f, 0.3f);

            return root;
        }

        /// <summary>メリーゴーランドのメッシュを生成</summary>
        public static GameObject CreateMerryGoRound(string name)
        {
            var root = new GameObject(name);
            float platformRadius = 3f;
            int horseCount = 8;

            // 回転台（円盤）
            var platform = CreateCylinderGameObject(platformRadius, 0.4f, 24);
            platform.name = "Platform";
            platform.transform.SetParent(root.transform, false);
            platform.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            ApplyMaterial(platform, Palette.MerryPink, 0f, 0.5f);

            // デコリング（台の縁）
            var decoRing = CreateCylinderGameObject(platformRadius + 0.15f, 0.15f, 24);
            decoRing.name = "DecoRing";
            decoRing.transform.SetParent(root.transform, false);
            decoRing.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            ApplyMaterial(decoRing, Palette.FerrisGold, 0.6f, 0.7f);

            // 中央ポール
            var pole = CreateCylinderGameObject(0.2f, 5f, 12);
            pole.name = "CenterPole";
            pole.transform.SetParent(root.transform, false);
            pole.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            ApplyMaterial(pole, Palette.FerrisGold, 0.7f, 0.8f);

            // 円錐屋根
            var roof = CreateConeGameObject(platformRadius + 0.5f, 0.1f, 2f, 24);
            roof.name = "Roof";
            roof.transform.SetParent(root.transform, false);
            roof.transform.localPosition = new Vector3(0f, 5f, 0f);
            ApplyMaterial(roof, new Color(0.90f, 0.25f, 0.30f), 0f, 0.4f);

            // 屋根のフリル（少し大きめのリング）
            var frill = CreateCylinderGameObject(platformRadius + 0.7f, 0.15f, 24);
            frill.name = "RoofFrill";
            frill.transform.SetParent(root.transform, false);
            frill.transform.localPosition = new Vector3(0f, 4.95f, 0f);
            ApplyMaterial(frill, Palette.ShopAwning, 0f, 0.3f);

            // 屋根トップの球
            var finial = CreateSphereGameObject(0.25f, 12);
            finial.name = "Finial";
            finial.transform.SetParent(root.transform, false);
            finial.transform.localPosition = new Vector3(0f, 6.8f, 0f);
            ApplyMaterial(finial, Palette.FerrisGold, 0.8f, 0.9f);

            // 馬ポール＆馬
            for (int i = 0; i < horseCount; i++)
            {
                float angle = (float)i / horseCount * Mathf.PI * 2f;
                float hx = Mathf.Cos(angle) * (platformRadius - 0.6f);
                float hz = Mathf.Sin(angle) * (platformRadius - 0.6f);
                float horseHeight = (i % 2 == 0) ? 2.0f : 2.5f;

                // ポール
                var horsePole = CreateCylinderGameObject(0.04f, 4.5f, 6);
                horsePole.name = $"HorsePole_{i}";
                horsePole.transform.SetParent(root.transform, false);
                horsePole.transform.localPosition = new Vector3(hx, 2.65f, hz);
                ApplyMaterial(horsePole, Palette.FerrisGold, 0.7f, 0.8f);

                // 馬の体（楕円体＝スケールされた球）
                var horseBody = CreateSphereGameObject(0.35f, 8);
                horseBody.name = $"Horse_{i}";
                horseBody.transform.SetParent(root.transform, false);
                horseBody.transform.localPosition = new Vector3(hx, horseHeight, hz);
                horseBody.transform.localScale = new Vector3(0.5f, 0.7f, 1.2f);
                horseBody.transform.localRotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg + 90f, 0f);
                Color horseColor = (i % 3) switch
                {
                    0 => new Color(0.95f, 0.95f, 0.90f),
                    1 => new Color(0.60f, 0.40f, 0.25f),
                    _ => new Color(0.25f, 0.25f, 0.28f)
                };
                ApplyMaterial(horseBody, horseColor, 0f, 0.4f);
            }

            return root;
        }

        /// <summary>コーヒーカップ型回転アトラクション</summary>
        public static GameObject CreateSpinningCups(string name)
        {
            var root = new GameObject(name);
            int cupCount = 6;
            float plateRadius = 3f;

            // メインプラットフォーム
            var platform = CreateCylinderGameObject(plateRadius, 0.3f, 24);
            platform.name = "Platform";
            platform.transform.SetParent(root.transform, false);
            platform.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            ApplyMaterial(platform, Palette.SpinYellow, 0f, 0.5f);

            // 中央ポットのような装飾
            var centerPot = CreateConeGameObject(0.6f, 0.3f, 1.5f, 12);
            centerPot.name = "CenterPot";
            centerPot.transform.SetParent(root.transform, false);
            centerPot.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            ApplyMaterial(centerPot, Palette.SpinOrange, 0.3f, 0.5f);

            // 天蓋（フラットな傘）
            var canopy = CreateConeGameObject(plateRadius + 0.5f, plateRadius + 0.3f, 0.5f, 24);
            canopy.name = "Canopy";
            canopy.transform.SetParent(root.transform, false);
            canopy.transform.localPosition = new Vector3(0f, 4f, 0f);
            ApplyMaterial(canopy, new Color(0.95f, 0.30f, 0.35f), 0f, 0.3f);

            // 天蓋支柱
            var canopyPole = CreateCylinderGameObject(0.1f, 4f, 8);
            canopyPole.name = "CanopyPole";
            canopyPole.transform.SetParent(root.transform, false);
            canopyPole.transform.localPosition = new Vector3(0f, 2f, 0f);
            ApplyMaterial(canopyPole, Palette.FerrisGold, 0.6f, 0.7f);

            // カップ
            for (int i = 0; i < cupCount; i++)
            {
                float angle = (float)i / cupCount * Mathf.PI * 2f;
                float cx = Mathf.Cos(angle) * (plateRadius - 1.2f);
                float cz = Mathf.Sin(angle) * (plateRadius - 1.2f);

                // サブプラットフォーム
                var subPlate = CreateCylinderGameObject(0.7f, 0.1f, 12);
                subPlate.name = $"SubPlate_{i}";
                subPlate.transform.SetParent(root.transform, false);
                subPlate.transform.localPosition = new Vector3(cx, 0.35f, cz);
                ApplyMaterial(subPlate, Palette.SpinOrange, 0f, 0.4f);

                // カップ本体（逆截頭円錐）
                var cup = CreateConeGameObject(0.3f, 0.55f, 0.8f, 12);
                cup.name = $"Cup_{i}";
                cup.transform.SetParent(root.transform, false);
                cup.transform.localPosition = new Vector3(cx, 0.8f, cz);
                Color cupColor = (i % 4) switch
                {
                    0 => Palette.CoasterRed,
                    1 => Palette.FerrisBlue,
                    2 => Palette.MerryPink,
                    _ => Palette.SpinYellow
                };
                ApplyMaterial(cup, cupColor, 0f, 0.5f);

                // ハンドル（小さなトーラスの代わりにリング）
                var handle = CreateCylinderGameObject(0.15f, 0.05f, 8);
                handle.name = $"Handle_{i}";
                handle.transform.SetParent(root.transform, false);
                handle.transform.localPosition = new Vector3(cx, 1.4f, cz);
                ApplyMaterial(handle, Palette.FerrisGold, 0.7f, 0.8f);
            }

            return root;
        }

        /// <summary>お化け屋敷のメッシュを生成</summary>
        public static GameObject CreateHauntedHouse(string name)
        {
            var root = new GameObject(name);

            // メインの建物
            var body = CreateBoxGameObject(new Vector3(5f, 4f, 4f));
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 2f, 0f);
            ApplyMaterial(body, Palette.HauntGrey, 0f, 0.2f);

            // 三角屋根（プリズム形状 → 平たいコーンで近似）
            var roof = CreateConeGameObject(3.5f, 0.05f, 2.5f, 4);
            roof.name = "Roof";
            roof.transform.SetParent(root.transform, false);
            roof.transform.localPosition = new Vector3(0f, 5.25f, 0f);
            roof.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            ApplyMaterial(roof, Palette.HauntPurple, 0f, 0.3f);

            // タワー（角の塔）
            var tower = CreateCylinderGameObject(0.6f, 6f, 8);
            tower.name = "Tower";
            tower.transform.SetParent(root.transform, false);
            tower.transform.localPosition = new Vector3(2f, 3f, 1.5f);
            ApplyMaterial(tower, Palette.HauntGrey, 0f, 0.2f);

            // タワー屋根（尖塔）
            var towerRoof = CreateConeGameObject(0.8f, 0.05f, 1.5f, 8);
            towerRoof.name = "TowerRoof";
            towerRoof.transform.SetParent(root.transform, false);
            towerRoof.transform.localPosition = new Vector3(2f, 7f, 1.5f);
            ApplyMaterial(towerRoof, Palette.HauntPurple, 0f, 0.3f);

            // ドア
            var door = CreateBoxGameObject(new Vector3(1f, 2f, 0.1f));
            door.name = "Door";
            door.transform.SetParent(root.transform, false);
            door.transform.localPosition = new Vector3(0f, 1f, -2.05f);
            ApplyMaterial(door, new Color(0.15f, 0.08f, 0.05f), 0f, 0.2f);

            // 窓×4
            for (int i = 0; i < 4; i++)
            {
                float wx = (i % 2 == 0) ? -1.2f : 1.2f;
                float wy = (i < 2) ? 2.5f : 3.5f;
                var window = CreateBoxGameObject(new Vector3(0.6f, 0.6f, 0.05f));
                window.name = $"Window_{i}";
                window.transform.SetParent(root.transform, false);
                window.transform.localPosition = new Vector3(wx, wy, -2.05f);
                ApplyMaterial(window, new Color(0.8f, 0.6f, 0.1f, 0.5f), 0f, 0.9f);
            }

            return root;
        }

        /// <summary>ショップ建物メッシュを生成</summary>
        public static GameObject CreateShopBuilding(string name, Color mainColor)
        {
            var root = new GameObject(name);

            // 本体
            var body = CreateBoxGameObject(new Vector3(3.5f, 3f, 3.5f));
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            ApplyMaterial(body, mainColor, 0f, 0.4f);

            // 屋根（少し広い箱）
            var roof = CreateBoxGameObject(new Vector3(4f, 0.3f, 4f));
            roof.name = "Roof";
            roof.transform.SetParent(root.transform, false);
            roof.transform.localPosition = new Vector3(0f, 3.15f, 0f);
            ApplyMaterial(roof, Palette.ShopRoof, 0f, 0.3f);

            // ひさし（前面）
            var awning = CreateBoxGameObject(new Vector3(3.8f, 0.08f, 1.2f));
            awning.name = "Awning";
            awning.transform.SetParent(root.transform, false);
            awning.transform.localPosition = new Vector3(0f, 2.5f, -2.1f);
            awning.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
            ApplyMaterial(awning, Palette.ShopAwning, 0f, 0.3f);

            // 看板
            var sign = CreateBoxGameObject(new Vector3(2.5f, 0.6f, 0.1f));
            sign.name = "Sign";
            sign.transform.SetParent(root.transform, false);
            sign.transform.localPosition = new Vector3(0f, 3.7f, -1.8f);
            ApplyMaterial(sign, Palette.FerrisGold, 0.3f, 0.5f);

            // ショーウィンドウ
            var display = CreateBoxGameObject(new Vector3(2f, 1.2f, 0.05f));
            display.name = "Display";
            display.transform.SetParent(root.transform, false);
            display.transform.localPosition = new Vector3(0f, 1.2f, -1.78f);
            ApplyMaterial(display, new Color(0.7f, 0.85f, 0.95f, 0.6f), 0f, 0.9f);

            return root;
        }

        /// <summary>トイレ建物メッシュを生成</summary>
        public static GameObject CreateToiletBuilding(string name)
        {
            var root = new GameObject(name);

            var body = CreateBoxGameObject(new Vector3(3f, 2.5f, 3f));
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            ApplyMaterial(body, Palette.ToiletWhite, 0f, 0.5f);

            var roof = CreateBoxGameObject(new Vector3(3.3f, 0.2f, 3.3f));
            roof.name = "Roof";
            roof.transform.SetParent(root.transform, false);
            roof.transform.localPosition = new Vector3(0f, 2.6f, 0f);
            ApplyMaterial(roof, Palette.ToiletBlue, 0f, 0.4f);

            // ドア×2
            for (int i = 0; i < 2; i++)
            {
                float dx = (i == 0) ? -0.7f : 0.7f;
                var door = CreateBoxGameObject(new Vector3(0.8f, 2f, 0.05f));
                door.name = $"Door_{i}";
                door.transform.SetParent(root.transform, false);
                door.transform.localPosition = new Vector3(dx, 1f, -1.53f);
                ApplyMaterial(door, Palette.ToiletBlue, 0f, 0.3f);
            }

            // WCサイン
            var wcSign = CreateBoxGameObject(new Vector3(1.5f, 0.4f, 0.05f));
            wcSign.name = "WCSign";
            wcSign.transform.SetParent(root.transform, false);
            wcSign.transform.localPosition = new Vector3(0f, 2.8f, -1.53f);
            ApplyMaterial(wcSign, Palette.ToiletBlue, 0f, 0.4f);

            return root;
        }

        /// <summary>ベンチメッシュを生成</summary>
        public static GameObject CreateBench(string name)
        {
            var root = new GameObject(name);

            // 座面
            var seat = CreateBoxGameObject(new Vector3(1.8f, 0.08f, 0.5f));
            seat.name = "Seat";
            seat.transform.SetParent(root.transform, false);
            seat.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            ApplyMaterial(seat, Palette.BenchBrown, 0f, 0.3f);

            // 背もたれ
            var back = CreateBoxGameObject(new Vector3(1.8f, 0.5f, 0.06f));
            back.name = "Back";
            back.transform.SetParent(root.transform, false);
            back.transform.localPosition = new Vector3(0f, 0.75f, -0.22f);
            back.transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);
            ApplyMaterial(back, Palette.BenchBrown, 0f, 0.3f);

            // 脚×2
            for (int i = 0; i < 2; i++)
            {
                float lx = (i == 0) ? -0.7f : 0.7f;
                var leg = CreateBoxGameObject(new Vector3(0.06f, 0.5f, 0.4f));
                leg.name = $"Leg_{i}";
                leg.transform.SetParent(root.transform, false);
                leg.transform.localPosition = new Vector3(lx, 0.25f, 0f);
                ApplyMaterial(leg, Palette.BenchMetal, 0.6f, 0.5f);
            }

            return root;
        }

        /// <summary>スタッフルーム建物メッシュを生成</summary>
        public static GameObject CreateStaffRoom(string name)
        {
            var root = new GameObject(name);

            var body = CreateBoxGameObject(new Vector3(5f, 3f, 4f));
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            ApplyMaterial(body, Palette.StaffGreen, 0f, 0.4f);

            var roof = CreateBoxGameObject(new Vector3(5.5f, 0.3f, 4.5f));
            roof.name = "Roof";
            roof.transform.SetParent(root.transform, false);
            roof.transform.localPosition = new Vector3(0f, 3.15f, 0f);
            ApplyMaterial(roof, Palette.ShopRoof, 0f, 0.3f);

            // 窓×3
            for (int i = 0; i < 3; i++)
            {
                float wx = (i - 1) * 1.5f;
                var window = CreateBoxGameObject(new Vector3(0.8f, 0.8f, 0.05f));
                window.name = $"Window_{i}";
                window.transform.SetParent(root.transform, false);
                window.transform.localPosition = new Vector3(wx, 2f, -2.03f);
                ApplyMaterial(window, new Color(0.6f, 0.8f, 0.95f, 0.6f), 0f, 0.8f);
            }

            // ドア
            var door = CreateBoxGameObject(new Vector3(1f, 2.2f, 0.05f));
            door.name = "Door";
            door.transform.SetParent(root.transform, false);
            door.transform.localPosition = new Vector3(0f, 1.1f, -2.03f);
            ApplyMaterial(door, Palette.WoodBrown, 0f, 0.3f);

            return root;
        }

        // ============================================================
        // キャラクターメッシュ
        // ============================================================

        /// <summary>来場者のスタイライズドメッシュを生成</summary>
        public static GameObject CreateVisitorMesh(Color bodyColor)
        {
            var root = new GameObject("VisitorVisual");

            // 胴体（カプセル風 → スケールした球）
            var body = CreateSphereGameObject(0.25f, 10);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            body.transform.localScale = new Vector3(1f, 1.4f, 0.8f);
            ApplyMaterial(body, bodyColor, 0f, 0.4f);

            // 頭
            var head = CreateSphereGameObject(0.18f, 10);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            ApplyMaterial(head, new Color(0.95f, 0.82f, 0.72f), 0f, 0.4f);

            // 左足
            var legL = CreateCylinderGameObject(0.06f, 0.4f, 6);
            legL.name = "LegL";
            legL.transform.SetParent(root.transform, false);
            legL.transform.localPosition = new Vector3(-0.1f, 0.2f, 0f);
            ApplyMaterial(legL, new Color(0.25f, 0.25f, 0.35f), 0f, 0.3f);

            // 右足
            var legR = CreateCylinderGameObject(0.06f, 0.4f, 6);
            legR.name = "LegR";
            legR.transform.SetParent(root.transform, false);
            legR.transform.localPosition = new Vector3(0.1f, 0.2f, 0f);
            ApplyMaterial(legR, new Color(0.25f, 0.25f, 0.35f), 0f, 0.3f);

            return root;
        }

        /// <summary>スタッフのスタイライズドメッシュを生成</summary>
        public static GameObject CreateStaffMesh(Color bodyColor)
        {
            var root = CreateVisitorMesh(bodyColor);
            root.name = "StaffVisual";

            // 帽子（フラットなシリンダー）
            var hat = CreateCylinderGameObject(0.2f, 0.12f, 10);
            hat.name = "Hat";
            hat.transform.SetParent(root.transform, false);
            hat.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            ApplyMaterial(hat, bodyColor * 0.7f, 0f, 0.4f);

            // 帽子のつば
            var brim = CreateCylinderGameObject(0.25f, 0.03f, 10);
            brim.name = "Brim";
            brim.transform.SetParent(root.transform, false);
            brim.transform.localPosition = new Vector3(0f, 1.36f, 0f);
            ApplyMaterial(brim, bodyColor * 0.7f, 0f, 0.4f);

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
            var mr = go.AddComponent<MeshRenderer>();

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
                if (i < pathLen - 1)
                    forward = (path[i + 1] - path[i]).normalized;
                else if (i > 0)
                    forward = (path[i] - path[i - 1]).normalized;

                if (forward == Vector3.zero) forward = Vector3.forward;

                Vector3 up = Vector3.up;
                if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.99f)
                    up = Vector3.right;

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

                    triangles[ti++] = cur;
                    triangles[ti++] = curNext;
                    triangles[ti++] = next;

                    triangles[ti++] = next;
                    triangles[ti++] = curNext;
                    triangles[ti++] = nextNext;
                }
            }

            var mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mf.mesh = mesh;

            return go;
        }

        /// <summary>ボックスメッシュのGameObjectを生成</summary>
        public static GameObject CreateBoxGameObject(Vector3 size)
        {
            var go = new GameObject("Box");
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();

            float x = size.x * 0.5f, y = size.y * 0.5f, z = size.z * 0.5f;

            Vector3[] vertices = {
                // Front
                new(-x, -y, -z), new(-x, y, -z), new(x, y, -z), new(x, -y, -z),
                // Back
                new(x, -y, z), new(x, y, z), new(-x, y, z), new(-x, -y, z),
                // Top
                new(-x, y, -z), new(-x, y, z), new(x, y, z), new(x, y, -z),
                // Bottom
                new(-x, -y, z), new(-x, -y, -z), new(x, -y, -z), new(x, -y, z),
                // Left
                new(-x, -y, z), new(-x, y, z), new(-x, y, -z), new(-x, -y, -z),
                // Right
                new(x, -y, -z), new(x, y, -z), new(x, y, z), new(x, -y, z),
            };

            Vector3[] normals = {
                -Vector3.forward, -Vector3.forward, -Vector3.forward, -Vector3.forward,
                Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward,
                Vector3.up, Vector3.up, Vector3.up, Vector3.up,
                -Vector3.up, -Vector3.up, -Vector3.up, -Vector3.up,
                -Vector3.right, -Vector3.right, -Vector3.right, -Vector3.right,
                Vector3.right, Vector3.right, Vector3.right, Vector3.right,
            };

            Vector2[] uvs = new Vector2[24];
            for (int i = 0; i < 6; i++)
            {
                uvs[i * 4 + 0] = new Vector2(0, 0);
                uvs[i * 4 + 1] = new Vector2(0, 1);
                uvs[i * 4 + 2] = new Vector2(1, 1);
                uvs[i * 4 + 3] = new Vector2(1, 0);
            }

            int[] triangles = new int[36];
            for (int i = 0; i < 6; i++)
            {
                int b = i * 4;
                triangles[i * 6 + 0] = b;
                triangles[i * 6 + 1] = b + 1;
                triangles[i * 6 + 2] = b + 2;
                triangles[i * 6 + 3] = b;
                triangles[i * 6 + 4] = b + 2;
                triangles[i * 6 + 5] = b + 3;
            }

            var mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mf.mesh = mesh;

            return go;
        }

        /// <summary>シリンダーメッシュのGameObjectを生成</summary>
        public static GameObject CreateCylinderGameObject(float radius, float height, int segments)
        {
            var go = new GameObject("Cylinder");
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvList = new List<Vector2>();
            var tris = new List<int>();

            float halfH = height * 0.5f;

            // 側面
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                Vector3 normal = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)).normalized;
                float u = (float)i / segments;

                verts.Add(new Vector3(x, -halfH, z));
                norms.Add(normal);
                uvList.Add(new Vector2(u, 0f));

                verts.Add(new Vector3(x, halfH, z));
                norms.Add(normal);
                uvList.Add(new Vector2(u, 1f));
            }

            for (int i = 0; i < segments; i++)
            {
                int b = i * 2;
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 3);
                tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
            }

            // 上面
            int topCenter = verts.Count;
            verts.Add(new Vector3(0f, halfH, 0f));
            norms.Add(Vector3.up);
            uvList.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(angle) * radius, halfH, Mathf.Sin(angle) * radius));
                norms.Add(Vector3.up);
                uvList.Add(new Vector2(Mathf.Cos(angle) * 0.5f + 0.5f, Mathf.Sin(angle) * 0.5f + 0.5f));
            }
            for (int i = 0; i < segments; i++)
            {
                tris.Add(topCenter);
                tris.Add(topCenter + 1 + i);
                tris.Add(topCenter + 2 + i);
            }

            // 下面
            int botCenter = verts.Count;
            verts.Add(new Vector3(0f, -halfH, 0f));
            norms.Add(-Vector3.up);
            uvList.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(angle) * radius, -halfH, Mathf.Sin(angle) * radius));
                norms.Add(-Vector3.up);
                uvList.Add(new Vector2(Mathf.Cos(angle) * 0.5f + 0.5f, Mathf.Sin(angle) * 0.5f + 0.5f));
            }
            for (int i = 0; i < segments; i++)
            {
                tris.Add(botCenter);
                tris.Add(botCenter + 2 + i);
                tris.Add(botCenter + 1 + i);
            }

            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvList);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mf.mesh = mesh;

            return go;
        }

        /// <summary>截頭円錐(コーン)メッシュのGameObjectを生成</summary>
        public static GameObject CreateConeGameObject(float bottomRadius, float topRadius, float height, int segments)
        {
            var go = new GameObject("Cone");
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvList = new List<Vector2>();
            var tris = new List<int>();

            float halfH = height * 0.5f;
            float slopeAngle = Mathf.Atan2(bottomRadius - topRadius, height);
            float ny = Mathf.Sin(slopeAngle);
            float nr = Mathf.Cos(slopeAngle);

            // 側面
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float cosA = Mathf.Cos(angle);
                float sinA = Mathf.Sin(angle);
                Vector3 normal = new Vector3(cosA * nr, ny, sinA * nr).normalized;
                float u = (float)i / segments;

                verts.Add(new Vector3(cosA * bottomRadius, -halfH, sinA * bottomRadius));
                norms.Add(normal);
                uvList.Add(new Vector2(u, 0f));

                verts.Add(new Vector3(cosA * topRadius, halfH, sinA * topRadius));
                norms.Add(normal);
                uvList.Add(new Vector2(u, 1f));
            }

            for (int i = 0; i < segments; i++)
            {
                int b = i * 2;
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 3);
                tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
            }

            // 下面
            int botCenter = verts.Count;
            verts.Add(new Vector3(0f, -halfH, 0f));
            norms.Add(-Vector3.up);
            uvList.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(angle) * bottomRadius, -halfH, Mathf.Sin(angle) * bottomRadius));
                norms.Add(-Vector3.up);
                uvList.Add(new Vector2(Mathf.Cos(angle) * 0.5f + 0.5f, Mathf.Sin(angle) * 0.5f + 0.5f));
            }
            for (int i = 0; i < segments; i++)
            {
                tris.Add(botCenter);
                tris.Add(botCenter + 2 + i);
                tris.Add(botCenter + 1 + i);
            }

            // 上面（topRadius > 0の場合）
            if (topRadius > 0.01f)
            {
                int topCenter = verts.Count;
                verts.Add(new Vector3(0f, halfH, 0f));
                norms.Add(Vector3.up);
                uvList.Add(new Vector2(0.5f, 0.5f));
                for (int i = 0; i <= segments; i++)
                {
                    float angle = (float)i / segments * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Cos(angle) * topRadius, halfH, Mathf.Sin(angle) * topRadius));
                    norms.Add(Vector3.up);
                    uvList.Add(new Vector2(Mathf.Cos(angle) * 0.5f + 0.5f, Mathf.Sin(angle) * 0.5f + 0.5f));
                }
                for (int i = 0; i < segments; i++)
                {
                    tris.Add(topCenter);
                    tris.Add(topCenter + 1 + i);
                    tris.Add(topCenter + 2 + i);
                }
            }

            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvList);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mf.mesh = mesh;

            return go;
        }

        /// <summary>球メッシュのGameObjectを生成</summary>
        public static GameObject CreateSphereGameObject(float radius, int segments)
        {
            var go = new GameObject("Sphere");
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();

            int rings = segments;
            int slices = segments * 2;

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvList = new List<Vector2>();
            var tris = new List<int>();

            for (int lat = 0; lat <= rings; lat++)
            {
                float theta = (float)lat / rings * Mathf.PI;
                float sinT = Mathf.Sin(theta);
                float cosT = Mathf.Cos(theta);

                for (int lon = 0; lon <= slices; lon++)
                {
                    float phi = (float)lon / slices * Mathf.PI * 2f;
                    float sinP = Mathf.Sin(phi);
                    float cosP = Mathf.Cos(phi);

                    Vector3 n = new Vector3(sinT * cosP, cosT, sinT * sinP);
                    verts.Add(n * radius);
                    norms.Add(n);
                    uvList.Add(new Vector2((float)lon / slices, (float)lat / rings));
                }
            }

            for (int lat = 0; lat < rings; lat++)
            {
                for (int lon = 0; lon < slices; lon++)
                {
                    int c = lat * (slices + 1) + lon;
                    int n = c + slices + 1;

                    tris.Add(c); tris.Add(n); tris.Add(c + 1);
                    tris.Add(c + 1); tris.Add(n); tris.Add(n + 1);
                }
            }

            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvList);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mf.mesh = mesh;

            return go;
        }

        // ============================================================
        // マテリアル適用
        // ============================================================

        /// <summary>Standard Shaderマテリアルを適用する</summary>
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

                // 半透明の場合
                if (color.a < 1f)
                {
                    mat.SetFloat("_Mode", 3); // Transparent
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
    }
}
