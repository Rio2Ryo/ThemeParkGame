// ============================================================
// ThemeParkGame - Prefab Generator
// Unityエディタ拡張: メニューから全プレハブを自動生成
// メニュー: ThemeParkGame > Generate Prefabs
// ============================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using ThemeParkGame.Visitor;
using ThemeParkGame.Staff;
using ThemeParkGame.Attraction;

namespace ThemeParkGame.Editor
{
    public static class PrefabGenerator
    {
        private const string PrefabRoot = "Assets/Prefabs";

        [MenuItem("ThemeParkGame/Generate Prefabs", false, 30)]
        public static void GenerateAllPrefabs()
        {
            if (!EditorUtility.DisplayDialog(
                "Prefab Generator",
                "ThemeParkGame用のプレハブを一括生成します。\n" +
                "既存の同名プレハブは上書きされます。\n\n続行しますか？",
                "生成する", "キャンセル"))
            {
                return;
            }

            int count = GenerateAllPrefabsInternal();

            Debug.Log($"[PrefabGenerator] 完了: {count} 件のプレハブを生成しました");
            EditorUtility.DisplayDialog(
                "Prefab Generator",
                $"プレハブ生成完了\n\n生成数: {count} 件\n保存先: {PrefabRoot}/",
                "OK"
            );
        }

        /// <summary>ダイアログなしでプレハブ生成を実行する（Full Setupから呼び出し用）</summary>
        internal static int GenerateAllPrefabsInternal()
        {
            EnsureDirectories();

            int count = 0;
            count += GenerateVisitorPrefabs();
            count += GenerateStaffPrefabs();
            count += GenerateAttractionPrefab();
            count += GenerateShopPrefabs();
            count += GenerateFacilityPrefabs();
            count += GenerateEnvironmentPrefabs();
            count += GenerateUIPrefabs();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return count;
        }

        // ================================================================
        // Directory setup
        // ================================================================

        private static void EnsureDirectories()
        {
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder(PrefabRoot, "Visitor");
            EnsureFolder(PrefabRoot, "Staff");
            EnsureFolder(PrefabRoot, "Attraction");
            EnsureFolder(PrefabRoot, "Shop");
            EnsureFolder(PrefabRoot, "Facility");
            EnsureFolder(PrefabRoot, "Environment");
            EnsureFolder(PrefabRoot, "UI");
        }

        private static void EnsureFolder(string parent, string folderName)
        {
            string path = $"{parent}/{folderName}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, folderName);
        }

        // ================================================================
        // Visitor Prefabs
        // ================================================================

        private static int GenerateVisitorPrefabs()
        {
            int count = 0;

            // Visitor.prefab
            {
                var go = new GameObject("Visitor");
                go.tag = "Visitor";
                SetLayerByName(go, "Visitor");

                // Capsule visual
                var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.name = "VisitorModel";
                visual.transform.SetParent(go.transform, false);
                visual.transform.localPosition = new Vector3(0f, 1f, 0f);
                Object.DestroyImmediate(visual.GetComponent<CapsuleCollider>());

                // Components
                var col = go.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0f, 1f, 0f);
                col.height = 2f;
                col.radius = 0.3f;

                var agent = go.AddComponent<NavMeshAgent>();
                agent.speed = 3.5f;
                agent.angularSpeed = 120f;
                agent.acceleration = 8f;
                agent.stoppingDistance = 0.5f;
                agent.radius = 0.3f;
                agent.height = 2f;

                go.AddComponent<VisitorAI>();

                // EmotionBubble child
                var bubbleObj = new GameObject("EmotionBubble");
                bubbleObj.transform.SetParent(go.transform, false);
                bubbleObj.transform.localPosition = new Vector3(0f, 2.5f, 0f);
                var sr = bubbleObj.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 10;
                bubbleObj.AddComponent<EmotionBubble>();

                SavePrefab(go, $"{PrefabRoot}/Visitor/Visitor.prefab");
                count++;
            }

            return count;
        }

        // ================================================================
        // Staff Prefabs
        // ================================================================

        private static int GenerateStaffPrefabs()
        {
            int count = 0;

            count += CreateStaffPrefab<MechanicStaff>("Staff_Mechanic",
                new Color(0.2f, 0.4f, 0.8f));
            count += CreateStaffPrefab<CleanerStaff>("Staff_Cleaner",
                new Color(0.3f, 0.8f, 0.3f));
            count += CreateStaffPrefab<EntertainerStaff>("Staff_Entertainer",
                new Color(0.9f, 0.6f, 0.1f));
            count += CreateStaffPrefab<GuardStaff>("Staff_Guard",
                new Color(0.1f, 0.1f, 0.3f));
            count += CreateStaffPrefab<ScientistStaff>("Staff_Scientist",
                new Color(0.9f, 0.9f, 0.9f));

            return count;
        }

        private static int CreateStaffPrefab<T>(string prefabName, Color uniformColor)
            where T : StaffMember
        {
            var go = new GameObject(prefabName);
            go.tag = "Staff";
            SetLayerByName(go, "Staff");

            // Capsule visual
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "StaffModel";
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = new Vector3(0f, 1f, 0f);
            Object.DestroyImmediate(visual.GetComponent<CapsuleCollider>());

            var renderer = visual.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Standard"));
                mat.color = uniformColor;
                renderer.sharedMaterial = mat;
            }

            // Staff marker sphere above head
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "StaffMarker";
            marker.transform.SetParent(go.transform, false);
            marker.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            marker.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
            Object.DestroyImmediate(marker.GetComponent<SphereCollider>());

            var markerRenderer = marker.GetComponent<MeshRenderer>();
            if (markerRenderer != null)
            {
                var mat = new Material(Shader.Find("Standard"));
                mat.color = Color.white;
                markerRenderer.sharedMaterial = mat;
            }

            // Components
            var col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1f, 0f);
            col.height = 2f;
            col.radius = 0.3f;

            var agent = go.AddComponent<NavMeshAgent>();
            agent.speed = 4f;
            agent.angularSpeed = 120f;
            agent.acceleration = 8f;
            agent.stoppingDistance = 0.5f;
            agent.radius = 0.3f;
            agent.height = 2f;

            go.AddComponent<T>();

            SavePrefab(go, $"{PrefabRoot}/Staff/{prefabName}.prefab");
            return 1;
        }

        // ================================================================
        // Attraction Prefab
        // ================================================================

        private static int GenerateAttractionPrefab()
        {
            var go = new GameObject("Attraction_Generic");
            go.tag = "Attraction";
            SetLayerByName(go, "Facility");

            // Placeholder visual (cube)
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "AttractionModel";
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            visual.transform.localScale = new Vector3(3f, 3f, 3f);
            Object.DestroyImmediate(visual.GetComponent<BoxCollider>());

            // Components
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(4f, 4f, 4f);
            col.center = new Vector3(0f, 2f, 0f);

            go.AddComponent<Attraction.Attraction>();
            go.AddComponent<FacilityDirt>();

            // Queue area marker
            var queueArea = new GameObject("QueueArea");
            queueArea.tag = "QueueArea";
            queueArea.transform.SetParent(go.transform, false);
            queueArea.transform.localPosition = new Vector3(0f, 0f, -3f);
            var queueCol = queueArea.AddComponent<BoxCollider>();
            queueCol.size = new Vector3(2f, 1f, 4f);
            queueCol.isTrigger = true;

            // Entrance point
            var entrance = new GameObject("EntrancePoint");
            entrance.transform.SetParent(go.transform, false);
            entrance.transform.localPosition = new Vector3(0f, 0f, -1.5f);

            // Exit point
            var exit = new GameObject("ExitPoint");
            exit.transform.SetParent(go.transform, false);
            exit.transform.localPosition = new Vector3(2f, 0f, 0f);

            SavePrefab(go, $"{PrefabRoot}/Attraction/Attraction_Generic.prefab");
            return 1;
        }

        // ================================================================
        // Shop Prefabs
        // ================================================================

        private static int GenerateShopPrefabs()
        {
            int count = 0;

            count += CreateShopPrefab("Shop_Food", "FoodShop",
                new Color(0.9f, 0.6f, 0.2f));
            count += CreateShopPrefab("Shop_Drink", "DrinkShop",
                new Color(0.2f, 0.7f, 0.9f));
            count += CreateShopPrefab("Shop_Souvenir", "SouvenirShop",
                new Color(0.8f, 0.3f, 0.7f));

            return count;
        }

        private static int CreateShopPrefab(string prefabName, string tag, Color shopColor)
        {
            var go = new GameObject(prefabName);
            go.tag = tag;
            SetLayerByName(go, "Facility");

            // Placeholder visual
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "ShopModel";
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = new Vector3(0f, 1f, 0f);
            visual.transform.localScale = new Vector3(2f, 2f, 2f);
            Object.DestroyImmediate(visual.GetComponent<BoxCollider>());

            var renderer = visual.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Standard"));
                mat.color = shopColor;
                renderer.sharedMaterial = mat;
            }

            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(2.5f, 2.5f, 2.5f);
            col.center = new Vector3(0f, 1.25f, 0f);

            go.AddComponent<Shop>();
            go.AddComponent<FacilityDirt>();

            // Counter point (where visitors interact)
            var counter = new GameObject("CounterPoint");
            counter.transform.SetParent(go.transform, false);
            counter.transform.localPosition = new Vector3(0f, 0f, -1.5f);

            SavePrefab(go, $"{PrefabRoot}/Shop/{prefabName}.prefab");
            return 1;
        }

        // ================================================================
        // Facility Prefabs
        // ================================================================

        private static int GenerateFacilityPrefabs()
        {
            int count = 0;

            // Toilet
            count += CreateFacilityPrefab<ToiletFacility>("Facility_Toilet", "Toilet",
                new Vector3(2f, 2.5f, 2f), new Color(0.85f, 0.85f, 0.95f));

            // Bench
            count += CreateFacilityPrefab<BenchFacility>("Facility_Bench", "Bench",
                new Vector3(2f, 0.8f, 0.6f), new Color(0.55f, 0.35f, 0.15f));

            // TrashCan
            count += CreateGenericFacilityPrefab("Facility_TrashCan", "TrashCan",
                Core.FacilityType.TrashCan, "ゴミ箱",
                new Vector3(0.5f, 1f, 0.5f), new Color(0.4f, 0.6f, 0.4f));

            // InfoBoard
            count += CreateGenericFacilityPrefab("Facility_InfoBoard", "InfoBoard",
                Core.FacilityType.InfoBoard, "案内板",
                new Vector3(1f, 2f, 0.3f), new Color(0.3f, 0.5f, 0.8f));

            // StaffRoom
            count += CreateGenericFacilityPrefab("Facility_StaffRoom", "StaffRoom",
                Core.FacilityType.StaffRoom, "スタッフルーム",
                new Vector3(3f, 2.5f, 3f), new Color(0.7f, 0.7f, 0.5f));

            // ResearchLab
            count += CreateGenericFacilityPrefab("Facility_ResearchLab", "ResearchLab",
                Core.FacilityType.ResearchLab, "研究所",
                new Vector3(3f, 3f, 3f), new Color(0.6f, 0.7f, 0.8f));

            // ParkEntrance
            {
                var go = new GameObject("Facility_ParkEntrance");
                go.tag = "ParkExit";
                SetLayerByName(go, "Facility");

                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = "EntranceModel";
                visual.transform.SetParent(go.transform, false);
                visual.transform.localPosition = new Vector3(0f, 2f, 0f);
                visual.transform.localScale = new Vector3(5f, 4f, 1f);
                Object.DestroyImmediate(visual.GetComponent<BoxCollider>());

                var renderer = visual.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Standard"));
                    mat.color = new Color(0.8f, 0.7f, 0.3f);
                    renderer.sharedMaterial = mat;
                }

                var col = go.AddComponent<BoxCollider>();
                col.size = new Vector3(5f, 4f, 2f);
                col.center = new Vector3(0f, 2f, 0f);
                col.isTrigger = true;

                SavePrefab(go, $"{PrefabRoot}/Facility/Facility_ParkEntrance.prefab");
                count++;
            }

            // Pathway segment
            {
                var go = new GameObject("Facility_Pathway");
                go.tag = "Pathway";
                SetLayerByName(go, "Ground");

                var visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
                visual.name = "PathwayModel";
                visual.transform.SetParent(go.transform, false);
                visual.transform.localPosition = new Vector3(0f, 0.01f, 0f);
                visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                visual.transform.localScale = new Vector3(1f, 1f, 1f);
                Object.DestroyImmediate(visual.GetComponent<MeshCollider>());

                var renderer = visual.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Standard"));
                    mat.color = new Color(0.75f, 0.7f, 0.6f);
                    renderer.sharedMaterial = mat;
                }

                SavePrefab(go, $"{PrefabRoot}/Facility/Facility_Pathway.prefab");
                count++;
            }

            return count;
        }

        private static int CreateFacilityPrefab<T>(string prefabName, string tag,
            Vector3 size, Color color) where T : FacilityBase
        {
            var go = new GameObject(prefabName);
            go.tag = tag;
            SetLayerByName(go, "Facility");

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "FacilityModel";
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
            visual.transform.localScale = size;
            Object.DestroyImmediate(visual.GetComponent<BoxCollider>());

            var renderer = visual.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Standard"));
                mat.color = color;
                renderer.sharedMaterial = mat;
            }

            var col = go.AddComponent<BoxCollider>();
            col.size = size + new Vector3(0.2f, 0.2f, 0.2f);
            col.center = new Vector3(0f, size.y * 0.5f, 0f);

            go.AddComponent<T>();
            go.AddComponent<FacilityDirt>();

            SavePrefab(go, $"{PrefabRoot}/Facility/{prefabName}.prefab");
            return 1;
        }

        private static int CreateGenericFacilityPrefab(string prefabName, string tag,
            Core.FacilityType facilityType, string displayName,
            Vector3 size, Color color)
        {
            var go = new GameObject(prefabName);
            go.tag = tag;
            SetLayerByName(go, "Facility");

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "FacilityModel";
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
            visual.transform.localScale = size;
            Object.DestroyImmediate(visual.GetComponent<BoxCollider>());

            var renderer = visual.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Standard"));
                mat.color = color;
                renderer.sharedMaterial = mat;
            }

            var col = go.AddComponent<BoxCollider>();
            col.size = size + new Vector3(0.2f, 0.2f, 0.2f);
            col.center = new Vector3(0f, size.y * 0.5f, 0f);

            var facility = go.AddComponent<GenericFacility>();
            facility.SetFacilityType(facilityType);
            facility.SetDisplayName(displayName);

            SavePrefab(go, $"{PrefabRoot}/Facility/{prefabName}.prefab");
            return 1;
        }

        // ================================================================
        // Environment Prefabs (Litter, Vomit, Decoration)
        // ================================================================

        private static int GenerateEnvironmentPrefabs()
        {
            int count = 0;

            // Litter prefab (dropped by visitors, cleaned by CleanerStaff)
            {
                var go = new GameObject("Litter");
                go.tag = "Litter";

                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = "LitterModel";
                visual.transform.SetParent(go.transform, false);
                visual.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                visual.transform.localScale = new Vector3(0.3f, 0.1f, 0.2f);
                Object.DestroyImmediate(visual.GetComponent<BoxCollider>());

                var renderer = visual.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Standard"));
                    mat.color = new Color(0.6f, 0.5f, 0.3f);
                    renderer.sharedMaterial = mat;
                }

                var col = go.AddComponent<BoxCollider>();
                col.size = new Vector3(0.4f, 0.2f, 0.3f);
                col.center = new Vector3(0f, 0.1f, 0f);
                col.isTrigger = true;

                SavePrefab(go, $"{PrefabRoot}/Environment/Litter.prefab");
                count++;
            }

            // Vomit prefab (created when visitors vomit, cleaned by CleanerStaff)
            {
                var go = new GameObject("Vomit");
                go.tag = "Vomit";

                var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                visual.name = "VomitModel";
                visual.transform.SetParent(go.transform, false);
                visual.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                visual.transform.localScale = new Vector3(0.5f, 0.02f, 0.5f);
                Object.DestroyImmediate(visual.GetComponent<CapsuleCollider>());

                var renderer = visual.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Standard"));
                    mat.color = new Color(0.7f, 0.65f, 0.2f, 0.9f);
                    renderer.sharedMaterial = mat;
                }

                var col = go.AddComponent<BoxCollider>();
                col.size = new Vector3(0.6f, 0.1f, 0.6f);
                col.center = new Vector3(0f, 0.05f, 0f);
                col.isTrigger = true;

                SavePrefab(go, $"{PrefabRoot}/Environment/Vomit.prefab");
                count++;
            }

            // Decoration prefab (generic placeable decoration)
            {
                var go = new GameObject("Decoration_Generic");
                go.tag = "Decoration";
                SetLayerByName(go, "Facility");

                var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                visual.name = "DecorationModel";
                visual.transform.SetParent(go.transform, false);
                visual.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                visual.transform.localScale = new Vector3(1f, 1f, 1f);
                Object.DestroyImmediate(visual.GetComponent<SphereCollider>());

                var renderer = visual.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Standard"));
                    mat.color = new Color(0.4f, 0.8f, 0.4f);
                    renderer.sharedMaterial = mat;
                }

                var col = go.AddComponent<BoxCollider>();
                col.size = new Vector3(1.2f, 1.2f, 1.2f);
                col.center = new Vector3(0f, 0.6f, 0f);

                SavePrefab(go, $"{PrefabRoot}/Environment/Decoration_Generic.prefab");
                count++;
            }

            return count;
        }

        // ================================================================
        // UI Prefabs
        // ================================================================

        private static int GenerateUIPrefabs()
        {
            int count = 0;

            // Chat Bubble - Player
            count += CreateChatBubblePrefab("UI_ChatBubble_Player",
                TextAlignmentOptions.Right, new Color(0.2f, 0.6f, 0.9f, 0.9f));

            // Chat Bubble - NPC
            count += CreateChatBubblePrefab("UI_ChatBubble_NPC",
                TextAlignmentOptions.Left, new Color(0.9f, 0.9f, 0.9f, 0.9f));

            // Category Tab
            {
                var go = CreateUIGameObject("UI_CategoryTab", new Vector2(120, 40));
                var img = go.AddComponent<Image>();
                img.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
                go.AddComponent<Button>();

                var icon = CreateUIChild(go, "Icon", new Vector2(30, 30));
                icon.AddComponent<Image>();

                var label = CreateUIChild(go, "Label", new Vector2(80, 30));
                var tmp = label.AddComponent<TextMeshProUGUI>();
                tmp.text = "Category";
                tmp.fontSize = 14;
                tmp.alignment = TextAlignmentOptions.Center;

                SavePrefab(go, $"{PrefabRoot}/UI/UI_CategoryTab.prefab");
                count++;
            }

            // Item Card
            {
                var go = CreateUIGameObject("UI_ItemCard", new Vector2(140, 180));
                var img = go.AddComponent<Image>();
                img.color = new Color(0.25f, 0.25f, 0.25f, 0.9f);
                go.AddComponent<Button>();
                var cg = go.AddComponent<CanvasGroup>();
                cg.alpha = 1f;

                var thumbnail = CreateUIChild(go, "Thumbnail", new Vector2(120, 100));
                thumbnail.AddComponent<Image>();

                var nameLabel = CreateUIChild(go, "NameText", new Vector2(120, 25));
                var nameTMP = nameLabel.AddComponent<TextMeshProUGUI>();
                nameTMP.text = "Item Name";
                nameTMP.fontSize = 13;
                nameTMP.alignment = TextAlignmentOptions.Center;

                var costLabel = CreateUIChild(go, "CostText", new Vector2(120, 20));
                var costTMP = costLabel.AddComponent<TextMeshProUGUI>();
                costTMP.text = "$0";
                costTMP.fontSize = 14;
                costTMP.alignment = TextAlignmentOptions.Center;
                costTMP.color = Color.yellow;

                var lockOverlay = CreateUIChild(go, "LockOverlay", new Vector2(140, 180));
                var lockImg = lockOverlay.AddComponent<Image>();
                lockImg.color = new Color(0f, 0f, 0f, 0.6f);
                lockOverlay.SetActive(false);

                SavePrefab(go, $"{PrefabRoot}/UI/UI_ItemCard.prefab");
                count++;
            }

            // Staff List Item
            {
                var go = CreateUIGameObject("UI_StaffListItem", new Vector2(300, 60));
                var img = go.AddComponent<Image>();
                img.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
                go.AddComponent<Button>();

                var nameLabel = CreateUIChild(go, "NameText", new Vector2(100, 25));
                var nameTMP = nameLabel.AddComponent<TextMeshProUGUI>();
                nameTMP.text = "Staff Name";
                nameTMP.fontSize = 14;
                nameTMP.alignment = TextAlignmentOptions.Left;

                var statusLabel = CreateUIChild(go, "StatusText", new Vector2(80, 20));
                var statusTMP = statusLabel.AddComponent<TextMeshProUGUI>();
                statusTMP.text = "Working";
                statusTMP.fontSize = 12;
                statusTMP.alignment = TextAlignmentOptions.Left;

                CreateSliderChild(go, "FatigueSlider", new Vector2(100, 10), Color.red);
                CreateSliderChild(go, "SkillSlider", new Vector2(100, 10), Color.cyan);

                SavePrefab(go, $"{PrefabRoot}/UI/UI_StaffListItem.prefab");
                count++;
            }

            // Quick Reply Button
            {
                var go = CreateUIGameObject("UI_QuickReplyButton", new Vector2(200, 36));
                var img = go.AddComponent<Image>();
                img.color = new Color(0.3f, 0.6f, 0.9f, 0.85f);
                go.AddComponent<Button>();

                var label = CreateUIChild(go, "Label", new Vector2(180, 30));
                var tmp = label.AddComponent<TextMeshProUGUI>();
                tmp.text = "Reply";
                tmp.fontSize = 14;
                tmp.alignment = TextAlignmentOptions.Center;

                SavePrefab(go, $"{PrefabRoot}/UI/UI_QuickReplyButton.prefab");
                count++;
            }

            // Notification Toast
            {
                var go = CreateUIGameObject("UI_NotificationToast", new Vector2(320, 50));
                var img = go.AddComponent<Image>();
                img.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);
                go.AddComponent<CanvasGroup>();

                var icon = CreateUIChild(go, "Icon", new Vector2(30, 30));
                var iconTMP = icon.AddComponent<TextMeshProUGUI>();
                iconTMP.text = "[i]";
                iconTMP.fontSize = 16;
                iconTMP.alignment = TextAlignmentOptions.Center;

                var message = CreateUIChild(go, "Message", new Vector2(270, 40));
                var msgTMP = message.AddComponent<TextMeshProUGUI>();
                msgTMP.text = "Notification";
                msgTMP.fontSize = 13;
                msgTMP.alignment = TextAlignmentOptions.Left;
                msgTMP.enableWordWrapping = true;

                SavePrefab(go, $"{PrefabRoot}/UI/UI_NotificationToast.prefab");
                count++;
            }

            // Achievement Toast
            {
                var go = CreateUIGameObject("UI_AchievementToast", new Vector2(380, 80));
                var img = go.AddComponent<Image>();
                img.color = new Color(0.15f, 0.12f, 0.05f, 0.95f);
                go.AddComponent<CanvasGroup>();

                var title = CreateUIChild(go, "Title", new Vector2(340, 25));
                var titleTMP = title.AddComponent<TextMeshProUGUI>();
                titleTMP.text = "Achievement Unlocked!";
                titleTMP.fontSize = 14;
                titleTMP.color = new Color(1f, 0.85f, 0.3f);
                titleTMP.alignment = TextAlignmentOptions.Center;

                var desc = CreateUIChild(go, "Description", new Vector2(340, 20));
                var descTMP = desc.AddComponent<TextMeshProUGUI>();
                descTMP.text = "Description";
                descTMP.fontSize = 12;
                descTMP.alignment = TextAlignmentOptions.Center;

                SavePrefab(go, $"{PrefabRoot}/UI/UI_AchievementToast.prefab");
                count++;
            }

            return count;
        }

        private static int CreateChatBubblePrefab(string prefabName,
            TextAlignmentOptions alignment, Color bgColor)
        {
            var go = CreateUIGameObject(prefabName, new Vector2(400, 80));
            var img = go.AddComponent<Image>();
            img.color = bgColor;
            go.AddComponent<LayoutElement>();

            var textObj = CreateUIChild(go, "MessageText", new Vector2(380, 60));
            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "Message";
            tmp.fontSize = 16;
            tmp.alignment = alignment;
            tmp.enableWordWrapping = true;

            SavePrefab(go, $"{PrefabRoot}/UI/{prefabName}.prefab");
            return 1;
        }

        // ================================================================
        // Helpers
        // ================================================================

        private static void SavePrefab(GameObject go, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        private static void SetLayerByName(GameObject go, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0)
                SetLayerRecursive(go, layer);
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        private static GameObject CreateUIGameObject(string name, Vector2 size)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = size;
            return go;
        }

        private static GameObject CreateUIChild(GameObject parent, string name, Vector2 size)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = size;
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        private static GameObject CreateSliderChild(GameObject parent, string name,
            Vector2 size, Color fillColor)
        {
            var go = CreateUIChild(parent, name, size);
            var slider = go.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.5f;

            var bg = CreateUIChild(go, "Background", size);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            var fillArea = CreateUIChild(go, "FillArea", size);
            var fill = CreateUIChild(fillArea, "Fill", size);
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = fillColor;

            slider.fillRect = fill.GetComponent<RectTransform>();

            return go;
        }
    }
}
#endif
