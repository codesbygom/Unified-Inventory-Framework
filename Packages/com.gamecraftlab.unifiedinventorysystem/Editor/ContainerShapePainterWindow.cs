using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameCraftLab.UnifiedInventorySystem.Editor
{
    /// <summary>Tools > Grid Inventory > Container Shape Painter — design container shapes without code.</summary>
    public class ContainerShapePainterWindow : EditorWindow
    {
        private ContainerDefinition _target;
        private readonly HashSet<Vector2Int> _cells = new HashSet<Vector2Int>();
        private Vector2Int _canvas = new Vector2Int(8, 6);
        private float _cellSize = 26f;
        private Vector2 _scroll;
        private bool _dirty;

        [MenuItem("Tools/Grid Inventory/Container Shape Painter", priority = 0)]
        public static void OpenEmpty() => Open(null);

        public static void Open(ContainerDefinition container)
        {
            var window = GetWindow<ContainerShapePainterWindow>("Container Painter");
            window.minSize = new Vector2(320, 300);
            window.Load(container);
        }

        private void Load(ContainerDefinition container)
        {
            _target = container;
            _cells.Clear();
            if (_target)
            {
                foreach (var c in _target.Cells) _cells.Add(c);
                _canvas = ShapeGridDrawer.SuggestCanvas(_cells, 6);
            }
            else
            {
                for (int y = 0; y < _canvas.y; y++)
                    for (int x = 0; x < _canvas.x; x++)
                        _cells.Add(new Vector2Int(x, y));
            }
            _dirty = false;
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                var picked = (ContainerDefinition)EditorGUILayout.ObjectField(_target, typeof(ContainerDefinition), false, GUILayout.Width(220));
                if (EditorGUI.EndChangeCheck() && (!_dirty || EditorUtility.DisplayDialog("Discard changes?", "The current shape has unsaved changes.", "Discard", "Keep")))
                    Load(picked);

                GUILayout.FlexibleSpace();
                GUILayout.Label("Zoom", EditorStyles.miniLabel);
                _cellSize = GUILayout.HorizontalSlider(_cellSize, 12f, 40f, GUILayout.Width(80));
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (ContainerPainterGUI.Draw(_cells, ref _canvas, _cellSize))
                _dirty = true;
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!_target || !_dirty || _cells.Count == 0))
                {
                    if (GUILayout.Button("Save"))
                        Save(_target);
                }
                using (new EditorGUI.DisabledScope(_cells.Count == 0))
                {
                    if (GUILayout.Button("Save As New Asset…"))
                        SaveAsNew();
                }
                using (new EditorGUI.DisabledScope(!_target || !_dirty))
                {
                    if (GUILayout.Button("Revert"))
                        Load(_target);
                }
            }
        }

        private void Save(ContainerDefinition container)
        {
            var so = new SerializedObject(container);
            ShapeGridDrawer.WriteCells(so.FindProperty("cells"), _cells);
            so.ApplyModifiedProperties(); // records Undo and marks dirty
            AssetDatabase.SaveAssetIfDirty(container);
            _dirty = false;
        }

        private void SaveAsNew()
        {
            var path = EditorUtility.SaveFilePanelInProject("Save Container", "NewContainer", "asset", "Where to save the container definition?");
            if (string.IsNullOrEmpty(path)) return;

            var container = CreateInstance<ContainerDefinition>();
            AssetDatabase.CreateAsset(container, path);
            Save(container);
            _target = container;
            EditorGUIUtility.PingObject(container);
        }
    }
}
