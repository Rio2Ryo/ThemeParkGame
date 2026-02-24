// ============================================================
// ThemeParkGame - Tag & Layer Setup Editor Utility
// カスタムタグ・レイヤーをUnityプロジェクトに自動登録する
// メニュー: ThemeParkGame > Setup Tags & Layers
// ============================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace ThemeParkGame.Editor
{
    public static class TagLayerSetup
    {
        // ---- 登録するカスタムタグ ----
        private static readonly string[] CustomTags = new string[]
        {
            "Attraction",
            "FoodShop",
            "DrinkShop",
            "SouvenirShop",
            "Toilet",
            "Bench",
            "TrashCan",
            "InfoBoard",
            "ParkExit",
            "Staff",
            "Visitor",
            "StaffRoom",
            "ResearchLab",
            "Pathway",
            "Litter",
            "Vomit",
            "Hooligan",
            "QueueArea",
            "Decoration"
        };

        // ---- 登録するカスタムレイヤー (Layer 8〜11) ----
        private static readonly (string name, int index)[] CustomLayers = new (string, int)[]
        {
            ("Ground",   8),
            ("Facility", 9),
            ("Visitor",  10),
            ("Staff",    11)
        };

        [MenuItem("ThemeParkGame/Setup Tags && Layers", false, 10)]
        public static void SetupTagsAndLayers()
        {
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]
            );

            int tagsAdded = SetupTags(tagManager);
            int layersAdded = SetupLayers(tagManager);

            tagManager.ApplyModifiedProperties();

            Debug.Log($"[TagLayerSetup] 完了: タグ {tagsAdded} 件追加, レイヤー {layersAdded} 件追加");
            EditorUtility.DisplayDialog(
                "Tags & Layers Setup",
                $"セットアップ完了\n\nタグ追加: {tagsAdded} 件\nレイヤー追加: {layersAdded} 件",
                "OK"
            );
        }

        private static int SetupTags(SerializedObject tagManager)
        {
            SerializedProperty tagsProp = tagManager.FindProperty("tags");
            int addedCount = 0;

            foreach (string tag in CustomTags)
            {
                if (TagExists(tagsProp, tag))
                    continue;

                int index = tagsProp.arraySize;
                tagsProp.InsertArrayElementAtIndex(index);
                SerializedProperty newTag = tagsProp.GetArrayElementAtIndex(index);
                newTag.stringValue = tag;
                addedCount++;
            }

            return addedCount;
        }

        private static bool TagExists(SerializedProperty tagsProp, string tag)
        {
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag)
                    return true;
            }

            // Unity組み込みタグもチェック
            foreach (string builtinTag in UnityEditorInternal.InternalEditorUtility.tags)
            {
                if (builtinTag == tag) return true;
            }

            return false;
        }

        private static int SetupLayers(SerializedObject tagManager)
        {
            SerializedProperty layersProp = tagManager.FindProperty("layers");
            int addedCount = 0;

            foreach (var (name, index) in CustomLayers)
            {
                SerializedProperty layerProp = layersProp.GetArrayElementAtIndex(index);
                if (!string.IsNullOrEmpty(layerProp.stringValue) && layerProp.stringValue == name)
                    continue;

                if (!string.IsNullOrEmpty(layerProp.stringValue) && layerProp.stringValue != name)
                {
                    Debug.LogWarning(
                        $"[TagLayerSetup] Layer {index} は既に '{layerProp.stringValue}' に設定されています。" +
                        $"'{name}' への変更をスキップします。"
                    );
                    continue;
                }

                layerProp.stringValue = name;
                addedCount++;
            }

            return addedCount;
        }

        /// <summary>
        /// タグが登録済みかどうかを確認するユーティリティ
        /// </summary>
        public static bool IsTagRegistered(string tag)
        {
            foreach (string t in UnityEditorInternal.InternalEditorUtility.tags)
            {
                if (t == tag) return true;
            }
            return false;
        }

        /// <summary>
        /// レイヤーが登録済みかどうかを確認するユーティリティ
        /// </summary>
        public static bool IsLayerRegistered(string layerName)
        {
            return LayerMask.NameToLayer(layerName) >= 0;
        }
    }
}
#endif
