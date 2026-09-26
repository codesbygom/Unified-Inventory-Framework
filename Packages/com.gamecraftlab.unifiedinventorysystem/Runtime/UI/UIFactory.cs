using UnityEngine;
using UnityEngine.UI;

namespace GameCraftLab.UnifiedInventorySystem.UI
{
    /// <summary>Small helpers to build uGUI objects from code, so the package works without prefabs.</summary>
    internal static class UIFactory
    {
        private static Font s_font;

        public static Font DefaultFont => s_font ? s_font : s_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent ? parent.gameObject.layer : go.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static void AnchorTopLeft(RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public static Image CreateImage(string name, Transform parent, Color color, bool raycastTarget = false)
        {
            var img = CreateRect(name, parent).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycastTarget;
            return img;
        }

        public static Text CreateText(string name, Transform parent, int fontSize, TextAnchor alignment)
        {
            var text = CreateRect(name, parent).gameObject.AddComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        /// <summary>Stable, pleasant color per id — used when an item has no icon.</summary>
        public static Color ColorFromId(string id)
        {
            unchecked
            {
                int hash = 17;
                foreach (char c in id ?? string.Empty)
                    hash = hash * 31 + c;
                float hue = (hash & 0x7FFFFFFF) % 360 / 360f;
                return Color.HSVToRGB(hue, 0.55f, 0.85f);
            }
        }

        public static Camera CanvasCamera(Transform anyChild)
        {
            var canvas = anyChild ? anyChild.GetComponentInParent<Canvas>() : null;
            if (!canvas) return null;
            canvas = canvas.rootCanvas;
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }
    }
}
