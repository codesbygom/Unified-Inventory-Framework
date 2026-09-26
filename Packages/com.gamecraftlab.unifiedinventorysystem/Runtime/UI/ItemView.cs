using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GameCraftLab.UnifiedInventorySystem.UI
{
    /// <summary>
    /// Draws one item: a tile per shape cell, the icon (rotated with the item) and the stack count.
    /// Works without a prefab; if you assign a prefab to the grid view, any of these parts you already
    /// set up are reused and only the missing ones are created.
    /// Purely visual — input is handled by the input providers.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ItemView : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private Text countLabel;
        [SerializeField, Tooltip("Shown instead of the icon when the item has none.")]
        private Text nameLabel;
        [SerializeField, Range(0f, 1f)] private float tileAlpha = 0.6f;

        private readonly List<Image> _tiles = new List<Image>();
        private RectTransform _tilesRoot;
        private CanvasGroup _group;
        private RectTransform _rt;

        public ItemInstance Item { get; private set; }
        public int Rotation { get; private set; }
        public RectTransform RectTransform => _rt ? _rt : _rt = (RectTransform)transform;

        public void Setup(ItemInstance item)
        {
            if (Item != null) Item.StackCountChanged -= OnStackCountChanged;
            Item = item;
            if (Item == null) return;

            Item.StackCountChanged += OnStackCountChanged;
            EnsureParts();

            var def = Item.Definition;
            icon.sprite = def.Icon;
            icon.enabled = def.Icon != null;
            icon.preserveAspect = true;
            nameLabel.enabled = def.Icon == null;
            nameLabel.text = def.DisplayName;
            UpdateCount();
        }

        /// <summary>Positions the view on the grid and lays it out for the placement's rotation.</summary>
        public void PlaceAt(GridCoordinateConverter converter, Placement placement)
        {
            UIFactory.AnchorTopLeft(RectTransform);
            RectTransform.anchoredPosition = converter.CellToLocal(placement.Origin);
            Layout(converter, placement.Rotation);
        }

        /// <summary>Sizes tiles and icon for a rotation without moving the view (used by the dragged ghost).</summary>
        public void Layout(GridCoordinateConverter converter, int rotation)
        {
            if (Item == null) return;
            Rotation = rotation;

            var shape = Item.GetShape(rotation);
            RectTransform.pivot = new Vector2(0f, 1f);
            RectTransform.sizeDelta = converter.SizeFor(shape.Width, shape.Height);

            // One tile per shape cell, so irregular shapes read correctly.
            var color = UIFactory.ColorFromId(Item.Definition.ItemId);
            color.a = tileAlpha;
            int i = 0;
            foreach (var offset in shape)
            {
                if (i == _tiles.Count)
                    _tiles.Add(UIFactory.CreateImage("Tile", _tilesRoot, color));
                var tile = _tiles[i++];
                tile.gameObject.SetActive(true);
                tile.color = color;
                var rt = tile.rectTransform;
                UIFactory.AnchorTopLeft(rt);
                rt.anchoredPosition = new Vector2(offset.x * converter.Pitch.x, -offset.y * converter.Pitch.y);
                rt.sizeDelta = converter.CellSize;
            }
            for (; i < _tiles.Count; i++)
                _tiles[i].gameObject.SetActive(false);

            // The icon is drawn for the unrotated shape and turned with the item.
            var baseShape = Item.Definition.BaseShape;
            var iconRt = icon.rectTransform;
            iconRt.anchorMin = iconRt.anchorMax = iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = Vector2.zero;
            iconRt.sizeDelta = converter.SizeFor(baseShape.Width, baseShape.Height);
            iconRt.localEulerAngles = new Vector3(0f, 0f, -90f * rotation);
        }

        public void SetAlpha(float alpha)
        {
            EnsureGroup();
            _group.alpha = alpha;
        }

        public void SetBlocksRaycasts(bool blocks)
        {
            EnsureGroup();
            _group.blocksRaycasts = blocks;
        }

        private void OnDestroy()
        {
            if (Item != null) Item.StackCountChanged -= OnStackCountChanged;
        }

        private void OnStackCountChanged(ItemInstance item) => UpdateCount();

        private void UpdateCount()
        {
            bool show = Item != null && Item.Definition.IsStackable && Item.StackCount > 1;
            countLabel.enabled = show;
            if (show) countLabel.text = Item.StackCount.ToString();
        }

        private void EnsureGroup()
        {
            if (!_group) _group = GetComponent<CanvasGroup>();
            if (!_group) _group = gameObject.AddComponent<CanvasGroup>();
        }

        private void EnsureParts()
        {
            if (!_tilesRoot)
            {
                _tilesRoot = UIFactory.CreateRect("Tiles", transform);
                UIFactory.Stretch(_tilesRoot);
                _tilesRoot.SetAsFirstSibling();
            }

            if (!icon)
            {
                icon = UIFactory.CreateImage("Icon", transform, Color.white);
            }
            icon.raycastTarget = false;

            if (!nameLabel)
            {
                nameLabel = UIFactory.CreateText("Name", transform, 13, TextAnchor.MiddleCenter);
                UIFactory.Stretch(nameLabel.rectTransform);
                nameLabel.rectTransform.offsetMin = new Vector2(3f, 3f);
                nameLabel.rectTransform.offsetMax = new Vector2(-3f, -3f);
                nameLabel.resizeTextForBestFit = true;
                nameLabel.resizeTextMinSize = 8;
                nameLabel.resizeTextMaxSize = 14;
            }

            if (!countLabel)
            {
                countLabel = UIFactory.CreateText("Count", transform, 14, TextAnchor.LowerRight);
                UIFactory.Stretch(countLabel.rectTransform);
                countLabel.rectTransform.offsetMin = new Vector2(2f, 1f);
                countLabel.rectTransform.offsetMax = new Vector2(-4f, -2f);
                countLabel.fontStyle = FontStyle.Bold;
            }
            countLabel.transform.SetAsLastSibling();
        }
    }
}
