using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GameCraftLab.UnifiedInventorySystem.UI
{
    /// <summary>
    /// uGUI view of one <see cref="InventoryGrid"/>: draws the cells that exist (so irregular containers
    /// look irregular), keeps one <see cref="ItemView"/> per item in sync with grid events, and shows
    /// placement previews. Call <see cref="Bind"/> from code.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class InventoryGridView : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private Vector2 cellSize = new Vector2(64f, 64f);
        [SerializeField] private Vector2 spacing = new Vector2(2f, 2f);
        [SerializeField, Tooltip("Resize this RectTransform to exactly fit the grid when bound.")]
        private bool resizeToFit = true;

        [Header("Look")]
        [SerializeField] private Sprite cellSprite;
        [SerializeField] private Color cellColor = new Color(1f, 1f, 1f, 0.12f);
        [SerializeField] private Color validColor = new Color(0.3f, 0.9f, 0.4f, 0.55f);
        [SerializeField] private Color invalidColor = new Color(0.95f, 0.3f, 0.3f, 0.55f);
        [SerializeField] private Color cursorColor = new Color(1f, 0.85f, 0.2f, 0.6f);
        [SerializeField, Tooltip("Optional. Must have an ItemView component on its root.")]
        private ItemView itemViewPrefab;

        private readonly Dictionary<Vector2Int, Image> _cellImages = new Dictionary<Vector2Int, Image>();
        private readonly Dictionary<ItemInstance, ItemView> _itemViews = new Dictionary<ItemInstance, ItemView>();
        private readonly Dictionary<Vector2Int, CellPreviewStatus> _preview = new Dictionary<Vector2Int, CellPreviewStatus>();
        private RectTransform _cellLayer;
        private RectTransform _itemLayer;
        private Vector2Int? _cursor;
        private ICellVisualProvider _cellVisuals;
        private RectTransform _rt;

        public InventoryGrid Grid { get; private set; }
        public InventoryInteraction Interaction { get; private set; }
        public GridCoordinateConverter Converter { get; private set; }
        public RectTransform RectTransform => _rt ? _rt : _rt = (RectTransform)transform;
        public Camera EventCamera => UIFactory.CanvasCamera(transform);

        /// <summary>Optional per-cell coloring (damaged, locked...). Views refresh when it raises Changed.</summary>
        public ICellVisualProvider CellVisuals
        {
            get => _cellVisuals;
            set
            {
                if (_cellVisuals != null) _cellVisuals.Changed -= RefreshCells;
                _cellVisuals = value;
                if (_cellVisuals != null) _cellVisuals.Changed += RefreshCells;
                RefreshCells();
            }
        }

        public event Action<InventoryGridView> Bound;

        public void SetLayout(Vector2 newCellSize, Vector2 newSpacing)
        {
            cellSize = newCellSize;
            spacing = newSpacing;
            if (Grid != null) Bind(Grid, Interaction);
        }

        /// <summary>Shows <paramref name="grid"/> and (optionally) lets <paramref name="interaction"/> drive drag &amp; drop on it.</summary>
        public void Bind(InventoryGrid grid, InventoryInteraction interaction = null)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            Unbind();

            Grid = grid;
            Converter = new GridCoordinateConverter(cellSize, spacing, grid.Bounds.min);
            EnsureRaycastSurface();
            EnsureLayers();

            if (resizeToFit)
            {
                var size = Converter.SizeFor(grid.Bounds.width, grid.Bounds.height);
                RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
                RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
            }

            foreach (var cell in grid.Cells)
            {
                var img = UIFactory.CreateImage($"Cell {cell.x},{cell.y}", _cellLayer, cellColor);
                img.sprite = cellSprite;
                img.type = cellSprite && cellSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
                UIFactory.AnchorTopLeft(img.rectTransform);
                img.rectTransform.anchoredPosition = Converter.CellToLocal(cell);
                img.rectTransform.sizeDelta = Converter.CellSize;
                _cellImages.Add(cell, img);
            }

            foreach (var item in grid.Items)
                AddItemView(item);

            grid.ItemAdded += AddItemView;
            grid.ItemRemoved += RemoveItemView;
            grid.ItemMoved += OnItemMoved;

            RefreshCells();

            Interaction = interaction;
            if (Interaction) Interaction.RegisterView(this);
            Bound?.Invoke(this);
        }

        public void Unbind()
        {
            if (Interaction) Interaction.UnregisterView(this);
            Interaction = null;

            if (Grid != null)
            {
                Grid.ItemAdded -= AddItemView;
                Grid.ItemRemoved -= RemoveItemView;
                Grid.ItemMoved -= OnItemMoved;
            }
            Grid = null;

            foreach (var view in _itemViews.Values)
                if (view) Destroy(view.gameObject);
            foreach (var img in _cellImages.Values)
                if (img) Destroy(img.gameObject);
            _itemViews.Clear();
            _cellImages.Clear();
            _preview.Clear();
            _cursor = null;
        }

        // ---------- coordinates ----------

        /// <summary>Cell under a screen point; false when outside the grid's bounds.</summary>
        public bool ScreenToCell(Vector2 screenPosition, Camera eventCamera, out Vector2Int cell)
        {
            cell = default;
            return Grid != null
                   && Converter.ScreenToGridCell(RectTransform, screenPosition, eventCamera, out cell)
                   && Grid.Bounds.Contains(cell);
        }

        public bool ScreenToCell(Vector2 screenPosition, out Vector2Int cell)
            => ScreenToCell(screenPosition, EventCamera, out cell);

        /// <summary>World position of a cell's top-left corner.</summary>
        public Vector3 CellToWorld(Vector2Int cell) => Converter.CellToWorld(RectTransform, cell);

        // ---------- items ----------

        public ItemView GetItemView(ItemInstance item)
            => item != null && _itemViews.TryGetValue(item, out var view) ? view : null;

        /// <summary>Creates a view for an item that isn't laid out on this grid (e.g. the dragged ghost).</summary>
        public ItemView CreateItemView(ItemInstance item, Transform parent)
        {
            ItemView view;
            if (itemViewPrefab)
            {
                view = Instantiate(itemViewPrefab, parent, false);
            }
            else
            {
                view = UIFactory.CreateRect("Item", parent).gameObject.AddComponent<ItemView>();
            }
            view.name = $"Item {item.Definition.ItemId}";
            view.Setup(item);
            view.SetBlocksRaycasts(false);
            return view;
        }

        // ---------- highlighting ----------

        public void ShowPreview(IReadOnlyList<CellPreview> cells)
        {
            _preview.Clear();
            for (int i = 0; i < cells.Count; i++)
                _preview[cells[i].Cell] = cells[i].Status;
            RefreshCells();
        }

        public void ClearPreview()
        {
            if (_preview.Count == 0) return;
            _preview.Clear();
            RefreshCells();
        }

        /// <summary>Highlights one cell (gamepad cursor). Null hides it.</summary>
        public void SetCursor(Vector2Int? cell)
        {
            if (_cursor == cell) return;
            _cursor = cell;
            RefreshCells();
        }

        /// <summary>Recomputes every cell color (base → visual provider → preview → cursor).</summary>
        public void RefreshCells()
        {
            foreach (var kv in _cellImages)
            {
                var cell = kv.Key;
                var color = cellColor;

                if (_cellVisuals != null && Grid != null && _cellVisuals.TryGetCellColor(Grid, cell, out var custom))
                    color = custom;

                if (_preview.TryGetValue(cell, out var status))
                    color = status == CellPreviewStatus.Valid ? validColor : invalidColor;

                if (_cursor == cell)
                    color = Color.Lerp(color, cursorColor, 0.75f);

                kv.Value.color = color;
            }
        }

        // ---------- internals ----------

        private void OnDestroy()
        {
            Unbind();
            if (_cellVisuals != null) _cellVisuals.Changed -= RefreshCells;
        }

        private void AddItemView(ItemInstance item)
        {
            if (_itemViews.ContainsKey(item)) return;
            var view = CreateItemView(item, _itemLayer);
            Grid.TryGetPlacement(item, out var placement);
            view.PlaceAt(Converter, placement);
            _itemViews.Add(item, view);
        }

        private void RemoveItemView(ItemInstance item)
        {
            if (!_itemViews.TryGetValue(item, out var view)) return;
            _itemViews.Remove(item);
            if (view) Destroy(view.gameObject);
        }

        private void OnItemMoved(ItemInstance item, Placement from, Placement to)
        {
            var view = GetItemView(item);
            if (view) view.PlaceAt(Converter, to);
        }

        // A transparent image over the whole rect so pointer events hit the grid even between cells.
        private void EnsureRaycastSurface()
        {
            var graphic = GetComponent<Graphic>();
            if (!graphic)
            {
                var img = gameObject.AddComponent<Image>();
                img.color = new Color(0f, 0f, 0f, 0f);
                graphic = img;
            }
            graphic.raycastTarget = true;
        }

        private void EnsureLayers()
        {
            if (!_cellLayer)
            {
                _cellLayer = UIFactory.CreateRect("Cells", transform);
                UIFactory.Stretch(_cellLayer);
            }
            if (!_itemLayer)
            {
                _itemLayer = UIFactory.CreateRect("Items", transform);
                UIFactory.Stretch(_itemLayer);
            }
            _cellLayer.SetSiblingIndex(0);
            _itemLayer.SetSiblingIndex(1);
        }
    }
}
