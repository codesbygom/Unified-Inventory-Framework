using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameCraftLab.UnifiedInventorySystem.Editor
{
    /// <summary>Item inspector with a shape painter and a preview of the unique rotations.</summary>
    [CustomEditor(typeof(ItemDefinition), true)]
    [CanEditMultipleObjects]
    public class ItemDefinitionEditor : UnityEditor.Editor
    {
        private const int MinCanvas = 4;
        private const int MaxCanvas = 16;

        private SerializedProperty _shape;
        private readonly HashSet<Vector2Int> _cells = new HashSet<Vector2Int>();
        private Vector2Int _canvas = new Vector2Int(MinCanvas, MinCanvas);

        private void OnEnable()
        {
            _shape = serializedObject.FindProperty("shape");
            ShapeGridDrawer.ReadCells(_shape, _cells);
            _canvas = ShapeGridDrawer.SuggestCanvas(_cells, MinCanvas);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "shape");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Shape", EditorStyles.boldLabel);

            if (serializedObject.isEditingMultipleObjects)
            {
                EditorGUILayout.HelpBox("Select a single item to paint its shape.", MessageType.Info);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            ShapeGridDrawer.ReadCells(_shape, _cells);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Canvas", GUILayout.Width(50));
                _canvas.x = EditorGUILayout.IntSlider(_canvas.x, 1, MaxCanvas);
                _canvas.y = EditorGUILayout.IntSlider(_canvas.y, 1, MaxCanvas);
            }

            if (ShapeGridDrawer.Draw(_cells, _canvas.x, _canvas.y))
            {
                if (_cells.Count == 0) _cells.Add(Vector2Int.zero); // an item always covers at least one cell
                ShapeGridDrawer.WriteCells(_shape, _cells);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Single cell"))
                {
                    _cells.Clear();
                    _cells.Add(Vector2Int.zero);
                    ShapeGridDrawer.WriteCells(_shape, _cells);
                }
                if (GUILayout.Button("Fill canvas"))
                {
                    _cells.Clear();
                    for (int y = 0; y < _canvas.y; y++)
                        for (int x = 0; x < _canvas.x; x++)
                            _cells.Add(new Vector2Int(x, y));
                    ShapeGridDrawer.WriteCells(_shape, _cells);
                }
                if (GUILayout.Button("Move to top-left"))
                {
                    ShapeGridDrawer.WriteCells(_shape, new CellShape(_cells));
                }
            }

            serializedObject.ApplyModifiedProperties();
            DrawRotations();
        }

        private void DrawRotations()
        {
            var def = (ItemDefinition)target;
            IReadOnlyList<CellShape> rotations;
            try
            {
                rotations = def.Rotations;
            }
            catch (System.ArgumentException)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Rotations: {rotations.Count} unique  ·  {def.BaseShape.Count} cell(s)  ·  pattern \"{def.BaseShape}\"",
                EditorStyles.miniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (var shape in rotations)
                {
                    ShapeGridDrawer.DrawPreview(shape);
                    GUILayout.Space(12);
                }
                GUILayout.FlexibleSpace();
            }
        }
    }
}
