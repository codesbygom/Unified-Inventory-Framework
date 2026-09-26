using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GameCraftLab.UnifiedInventorySystem.Editor
{
    /// <summary>
    /// Tools > Grid Inventory > Import Items from CSV/JSON — creates or updates ItemDefinition assets in bulk.
    /// Columns / fields: itemId, displayName, description, shape ("XX/X."), canRotate, maxStackSize, tags ("a;b"), icon (asset path).
    /// Existing items are matched by id anywhere in the project and updated in place.
    /// </summary>
    public class CsvJsonImporter : EditorWindow
    {
        [Serializable]
        public class ImportedItem
        {
            public string itemId;
            public string displayName;
            public string description;
            public string shape = "X";
            public bool canRotate = true;
            public int maxStackSize = 1;
            public string[] tags = Array.Empty<string>();
            public string icon;
        }

        [Serializable]
        private class ImportedFile
        {
            public ImportedItem[] items = Array.Empty<ImportedItem>();
        }

        public struct ImportSummary
        {
            public int Created;
            public int Updated;
            public List<string> Errors;
        }

        private string _sourcePath = "";
        private string _outputFolder = "Assets/Items";
        private string _log = "";
        private Vector2 _scroll;

        [MenuItem("Tools/Grid Inventory/Import Items from CSV or JSON", priority = 40)]
        public static void Open() => GetWindow<CsvJsonImporter>("Item Importer").minSize = new Vector2(420, 260);

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "CSV header: itemId,displayName,description,shape,canRotate,maxStackSize,tags,icon\n" +
                "JSON: { \"items\": [ { \"itemId\": \"sword\", \"shape\": \"X/X/X\", ... } ] }\n" +
                "shape rows are separated by '/', X = filled. tags are separated by ';'.", MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                _sourcePath = EditorGUILayout.TextField("Source file", _sourcePath);
                if (GUILayout.Button("…", GUILayout.Width(28)))
                {
                    var picked = EditorUtility.OpenFilePanelWithFilters("Items file", "", new[] { "Item tables", "csv,json", "All", "*" });
                    if (!string.IsNullOrEmpty(picked)) _sourcePath = picked;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _outputFolder = EditorGUILayout.TextField("New assets folder", _outputFolder);
                if (GUILayout.Button("…", GUILayout.Width(28)))
                {
                    var picked = EditorUtility.OpenFolderPanel("Output folder", "Assets", "");
                    if (!string.IsNullOrEmpty(picked))
                        _outputFolder = ToProjectPath(picked) ?? _outputFolder;
                }
            }

            using (new EditorGUI.DisabledScope(!File.Exists(_sourcePath)))
            {
                if (GUILayout.Button("Import", GUILayout.Height(26)))
                {
                    try
                    {
                        var summary = ImportFile(_sourcePath, _outputFolder);
                        var sb = new StringBuilder($"Created {summary.Created}, updated {summary.Updated}.");
                        foreach (var e in summary.Errors) sb.Append("\n• ").Append(e);
                        _log = sb.ToString();
                    }
                    catch (Exception ex)
                    {
                        _log = "Import failed: " + ex.Message;
                        Debug.LogException(ex);
                    }
                }
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField(_log, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();
        }

        // ---------- API (usable from your own build scripts) ----------

        public static ImportSummary ImportFile(string path, string outputFolder)
        {
            var text = File.ReadAllText(path);
            var items = path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? ParseJson(text) : ParseCsv(text);
            return Import(items, outputFolder);
        }

        public static List<ImportedItem> ParseJson(string json)
        {
            json = json.Trim();
            if (json.StartsWith("[")) json = "{\"items\":" + json + "}"; // JsonUtility can't read top-level arrays
            var file = JsonUtility.FromJson<ImportedFile>(json);
            return new List<ImportedItem>(file?.items ?? Array.Empty<ImportedItem>());
        }

        public static List<ImportedItem> ParseCsv(string csv)
        {
            var rows = ReadCsvRows(csv);
            var result = new List<ImportedItem>();
            if (rows.Count == 0) return result;

            var header = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < rows[0].Count; i++)
                header[rows[0][i].Trim()] = i;

            string Get(List<string> row, string column)
                => header.TryGetValue(column, out int i) && i < row.Count ? row[i].Trim() : null;

            for (int r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row.Count == 0 || row.TrueForAll(string.IsNullOrWhiteSpace)) continue;

                var item = new ImportedItem
                {
                    itemId = Get(row, "itemId"),
                    displayName = Get(row, "displayName"),
                    description = Get(row, "description"),
                    shape = string.IsNullOrEmpty(Get(row, "shape")) ? "X" : Get(row, "shape"),
                    icon = Get(row, "icon"),
                };
                if (bool.TryParse(Get(row, "canRotate"), out var rot)) item.canRotate = rot;
                if (int.TryParse(Get(row, "maxStackSize"), out var max)) item.maxStackSize = max;
                var tags = Get(row, "tags");
                if (!string.IsNullOrEmpty(tags))
                    item.tags = tags.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                result.Add(item);
            }
            return result;
        }

        public static ImportSummary Import(IEnumerable<ImportedItem> items, string outputFolder)
        {
            var summary = new ImportSummary { Errors = new List<string>() };

            var existing = new Dictionary<string, ItemDefinition>();
            foreach (var def in ItemDatabaseBuilder.FindAssets<ItemDefinition>())
                if (!existing.ContainsKey(def.ItemId)) existing.Add(def.ItemId, def);

            EnsureFolder(outputFolder);

            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.itemId))
                {
                    summary.Errors.Add("Row without itemId skipped.");
                    continue;
                }

                List<Vector2Int> shape;
                try
                {
                    shape = ShapePattern.Parse(item.shape);
                }
                catch (ArgumentException ex)
                {
                    summary.Errors.Add($"{item.itemId}: {ex.Message}");
                    continue;
                }

                if (!existing.TryGetValue(item.itemId, out var def))
                {
                    def = CreateInstance<ItemDefinition>();
                    var path = AssetDatabase.GenerateUniqueAssetPath($"{outputFolder}/{Sanitize(item.itemId)}.asset");
                    AssetDatabase.CreateAsset(def, path);
                    existing.Add(item.itemId, def);
                    summary.Created++;
                }
                else
                {
                    summary.Updated++;
                }

                var so = new SerializedObject(def);
                so.FindProperty("itemId").stringValue = item.itemId;
                so.FindProperty("displayName").stringValue = item.displayName ?? "";
                so.FindProperty("description").stringValue = item.description ?? "";
                so.FindProperty("canRotate").boolValue = item.canRotate;
                so.FindProperty("maxStackSize").intValue = Mathf.Max(1, item.maxStackSize);
                ShapeGridDrawer.WriteCells(so.FindProperty("shape"), shape);

                var tagsProp = so.FindProperty("tags");
                var tags = item.tags ?? Array.Empty<string>();
                tagsProp.arraySize = tags.Length;
                for (int i = 0; i < tags.Length; i++)
                    tagsProp.GetArrayElementAtIndex(i).stringValue = tags[i].Trim();

                if (!string.IsNullOrEmpty(item.icon))
                {
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(item.icon);
                    if (sprite) so.FindProperty("icon").objectReferenceValue = sprite;
                    else summary.Errors.Add($"{item.itemId}: icon '{item.icon}' is not a Sprite asset.");
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(def);
            }

            AssetDatabase.SaveAssets();
            return summary;
        }

        // ---------- helpers ----------

        private static List<List<string>> ReadCsvRows(string csv)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;

            for (int i = 0; i < csv.Length; i++)
            {
                char c = csv[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < csv.Length && csv[i + 1] == '"') { field.Append('"'); i++; }
                    else if (c == '"') quoted = false;
                    else field.Append(c);
                    continue;
                }

                switch (c)
                {
                    case '"': quoted = true; break;
                    case ',': row.Add(field.ToString()); field.Clear(); break;
                    case '\r': break;
                    case '\n':
                        row.Add(field.ToString()); field.Clear();
                        rows.Add(row); row = new List<string>();
                        break;
                    default: field.Append(c); break;
                }
            }

            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                rows.Add(row);
            }
            return rows;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static string Sanitize(string id)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) id = id.Replace(c, '_');
            return id;
        }

        private static string ToProjectPath(string absolute)
        {
            absolute = absolute.Replace('\\', '/');
            var root = Application.dataPath.Replace('\\', '/');
            return absolute.StartsWith(root) ? "Assets" + absolute.Substring(root.Length) : null;
        }
    }
}
