using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameCraftLab.UnifiedInventorySystem.Editor
{
    /// <summary>Container inspector: paint which cells exist.</summary>
    [CustomEditor(typeof(ContainerDefinition))]
    public class ContainerDefinitionEditor : UnityEditor.Editor
    {
        private SerializedProperty _cells;
        private readonly HashSet<Vector2Int> _set = new HashSet<Vector2Int>();
        private Vector2Int _canvas;

        private void OnEnable()
        {
            _cells = serializedObject.FindProperty("cells");
            ShapeGridDrawer.ReadCells(_cells, _set);
            _canvas = ShapeGridDrawer.SuggestCanvas(_set, 6);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "cells");

            EditorGUILayout.Space();
            ShapeGridDrawer.ReadCells(_cells, _set);
            if (ContainerPainterGUI.Draw(_set, ref _canvas))
                ShapeGridDrawer.WriteCells(_cells, _set);

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            if (GUILayout.Button("Open in Container Shape Painter"))
                ContainerShapePainterWindow.Open((ContainerDefinition)target);
        }
    }

    /// <summary>Shared painter UI for the inspector and the painter window.</summary>
    internal static class ContainerPainterGUI
    {
        public const int MaxCanvas = 24;

        public static bool Draw(HashSet<Vector2Int> cells, ref Vector2Int canvas, float cellSize = 22f)
        {
            bool changed = false;

            EditorGUILayout.LabelField($"Cells ({cells.Count})", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Canvas", GUILayout.Width(50));
                canvas.x = EditorGUILayout.IntSlider(Mathf.Max(1, canvas.x), 1, MaxCanvas);
                canvas.y = EditorGUILayout.IntSlider(Mathf.Max(1, canvas.y), 1, MaxCanvas);
            }

            changed |= ShapeGridDrawer.Draw(cells, canvas.x, canvas.y, cellSize);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Fill canvas"))
                {
                    cells.Clear();
                    for (int y = 0; y < canvas.y; y++)
                        for (int x = 0; x < canvas.x; x++)
                            cells.Add(new Vector2Int(x, y));
                    changed = true;
                }
                if (GUILayout.Button("Clear"))
                {
                    cells.Clear();
                    changed = true;
                }
                if (GUILayout.Button("Invert"))
                {
                    for (int y = 0; y < canvas.y; y++)
                        for (int x = 0; x < canvas.x; x++)
                        {
                            var c = new Vector2Int(x, y);
                            if (!cells.Remove(c)) cells.Add(c);
                        }
                    changed = true;
                }
            }

            if (cells.Count == 0)
                EditorGUILayout.HelpBox("A container needs at least one cell.", MessageType.Warning);

            return changed;
        }
    }
}
