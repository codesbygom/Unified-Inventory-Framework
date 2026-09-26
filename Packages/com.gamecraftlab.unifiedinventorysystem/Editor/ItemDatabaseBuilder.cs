using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameCraftLab.UnifiedInventorySystem.Editor
{
    /// <summary>Tools > Grid Inventory > Rebuild Item Database — scans the project for every ItemDefinition.</summary>
    public static class ItemDatabaseBuilder
    {
        [MenuItem("Tools/Grid Inventory/Rebuild Item Database", priority = 20)]
        public static void RebuildAll()
        {
            var databases = FindAssets<ItemDatabase>();
            if (databases.Count == 0)
            {
                var path = EditorUtility.SaveFilePanelInProject("Create Item Database", "ItemDatabase", "asset",
                    "No ItemDatabase found. Where should it be created?");
                if (string.IsNullOrEmpty(path)) return;

                var created = ScriptableObject.CreateInstance<ItemDatabase>();
                AssetDatabase.CreateAsset(created, path);
                databases.Add(created);
            }

            foreach (var db in databases)
            {
                int count = Rebuild(db, out var warnings);
                foreach (var w in warnings) Debug.LogWarning($"[Item Database] {w}", db);
                Debug.Log($"[Item Database] {AssetDatabase.GetAssetPath(db)}: {count} item(s).", db);
            }
        }

        /// <summary>Replaces the database content with every ItemDefinition in the project, sorted by id.</summary>
        public static int Rebuild(ItemDatabase database, out List<string> warnings)
        {
            warnings = new List<string>();
            var defs = FindAssets<ItemDefinition>();
            defs.Sort((a, b) => string.CompareOrdinal(a.ItemId, b.ItemId));

            var seen = new Dictionary<string, ItemDefinition>();
            foreach (var def in defs)
            {
                if (seen.TryGetValue(def.ItemId, out var other))
                    warnings.Add($"Duplicate id '{def.ItemId}': {AssetDatabase.GetAssetPath(other)} and {AssetDatabase.GetAssetPath(def)}.");
                else
                    seen.Add(def.ItemId, def);
            }

            var so = new SerializedObject(database);
            var list = so.FindProperty("items");
            list.arraySize = defs.Count;
            for (int i = 0; i < defs.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = defs[i];
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssetIfDirty(database);
            return defs.Count;
        }

        internal static List<T> FindAssets<T>() where T : Object
        {
            var result = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (obj is T typed) result.Add(typed);
            }
            return result;
        }
    }

    [CustomEditor(typeof(ItemDatabase))]
    public class ItemDatabaseEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Rebuild From Project"))
            {
                int count = ItemDatabaseBuilder.Rebuild((ItemDatabase)target, out var warnings);
                foreach (var w in warnings) Debug.LogWarning($"[Item Database] {w}", target);
                Debug.Log($"[Item Database] {count} item(s).", target);
            }
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }
}
