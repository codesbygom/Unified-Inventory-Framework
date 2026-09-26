using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameCraftLab.UnifiedInventorySystem.UI
{
    /// <summary>
    /// Pick up / rotate / drop logic shared by every input device and every grid view on screen.
    /// Input providers only report "what the player did"; this class decides what it means.
    /// While an item is held it stays in its grid (the source view just dims it), so cancelling
    /// or an invalid drop needs no rollback.
    /// </summary>
    [DisallowMultipleComponent]
    public class InventoryInteraction : MonoBehaviour
    {
        [SerializeField, Tooltip("Parent of the dragged item. Defaults to the root canvas of the source view.")]
        private RectTransform dragLayer;
        [SerializeField, Range(0f, 1f)] private float heldSourceAlpha = 0.35f;
        [SerializeField, Range(0f, 1f)] private float ghostAlpha = 0.85f;
        [SerializeField, Tooltip("Dropping onto a stack of the same item merges into it.")]
        private bool mergeStacksOnDrop = true;

        private readonly List<InventoryGridView> _views = new List<InventoryGridView>();
        private readonly List<IInventoryInputProvider> _providers = new List<IInventoryInputProvider>();
        private readonly List<CellPreview> _preview = new List<CellPreview>();

        private InventoryGridView _hoverView;
        private Vector2Int _hoverCell;
        private ItemView _ghost;
        private Vector2Int _grabOffset;   // cell of the held item under the cursor, in held-rotation shape space
        private bool _followPointer;
        private Vector2 _lastPointer;

        public IReadOnlyList<InventoryGridView> Views => _views;
        public ItemInstance HeldItem { get; private set; }
        public InventoryGridView SourceView { get; private set; }
        public int HeldRotation { get; private set; }
        public bool IsHolding => HeldItem != null;
        public InventoryGridView HoverView => _hoverView;
        public Vector2Int HoverCell => _hoverCell;

        public event Action<ItemInstance, InventoryGridView> PickedUp;
        /// <summary>Item, target view (null when dropped outside every grid), result.</summary>
        public event Action<ItemInstance, InventoryGridView, PlacementResult> Dropped;
        public event Action<ItemInstance> HoldCancelled;
        public event Action<ItemInstance, int> HeldRotated;

        // ---------- registration ----------

        /// <summary>Called by <see cref="InventoryGridView.Bind"/>. Also registers input providers found on the view.</summary>
        public void RegisterView(InventoryGridView view)
        {
            if (!view || _views.Contains(view)) return;
            _views.Add(view);
            foreach (var provider in view.GetComponents<IInventoryInputProvider>())
                RegisterProvider(provider);
        }

        public void UnregisterView(InventoryGridView view)
        {
            if (!_views.Remove(view)) return;
            foreach (var provider in view.GetComponents<IInventoryInputProvider>())
                UnregisterProvider(provider);
            if (SourceView == view) CancelHold();
            if (_hoverView == view) _hoverView = null;
        }

        public void RegisterProvider(IInventoryInputProvider provider)
        {
            if (provider == null || _providers.Contains(provider)) return;
            _providers.Add(provider);
            provider.CellHovered += OnCellHovered;
            provider.PointerMoved += OnPointerMoved;
            provider.PrimaryPressed += OnPrimaryPressed;
            provider.PrimaryReleased += OnPrimaryReleased;
            provider.RotatePressed += OnRotatePressed;
            provider.CancelPressed += OnCancelPressed;
        }

        public void UnregisterProvider(IInventoryInputProvider provider)
        {
            if (provider == null || !_providers.Remove(provider)) return;
            provider.CellHovered -= OnCellHovered;
            provider.PointerMoved -= OnPointerMoved;
            provider.PrimaryPressed -= OnPrimaryPressed;
            provider.PrimaryReleased -= OnPrimaryReleased;
            provider.RotatePressed -= OnRotatePressed;
            provider.CancelPressed -= OnCancelPressed;
        }

        // ---------- actions (callable from code too) ----------

        public void SetHover(InventoryGridView view, Vector2Int cell)
        {
            _hoverView = view && view.Grid != null ? view : null;
            _hoverCell = cell;
            if (IsHolding)
            {
                RefreshPreview();
                UpdateGhostPosition();
            }
        }

        public bool TryPickUp(InventoryGridView view, Vector2Int cell)
        {
            if (IsHolding || !view || view.Grid == null) return false;

            var item = view.Grid.GetItemAt(cell);
            if (item == null || !view.Grid.TryGetPlacement(item, out var placement)) return false;

            HeldItem = item;
            SourceView = view;
            HeldRotation = placement.Rotation;
            _grabOffset = cell - placement.Origin;
            _hoverView = view;
            _hoverCell = cell;

            var sourceItemView = view.GetItemView(item);
            if (sourceItemView) sourceItemView.SetAlpha(heldSourceAlpha);

            CreateGhost();
            RefreshPreview();
            UpdateGhostPosition();
            PickedUp?.Invoke(item, view);
            return true;
        }

        /// <summary>Turns the held item 90° clockwise, keeping the grabbed cell under the cursor.</summary>
        public bool RotateHeld()
        {
            if (!IsHolding || HeldItem.Definition.RotationCount < 2) return false;

            var oldShape = HeldItem.GetShape(HeldRotation);
            _grabOffset = CellShape.RotateCellCW(_grabOffset, oldShape.Height);
            HeldRotation = (HeldRotation + 1) % HeldItem.Definition.RotationCount;

            // Wrapping around on a symmetric shape can leave the offset outside the new bounds.
            var shape = HeldItem.GetShape(HeldRotation);
            _grabOffset = new Vector2Int(Mathf.Clamp(_grabOffset.x, 0, shape.Width - 1),
                                         Mathf.Clamp(_grabOffset.y, 0, shape.Height - 1));

            if (_ghost) _ghost.Layout(SourceView.Converter, HeldRotation);
            RefreshPreview();
            UpdateGhostPosition();
            HeldRotated?.Invoke(HeldItem, HeldRotation);
            return true;
        }

        /// <summary>Drops the held item on the hovered cell (move, transfer between grids, or stack merge).</summary>
        public PlacementResult TryDrop()
        {
            if (!IsHolding) return PlacementResult.NotInGrid;

            var item = HeldItem;
            var source = SourceView;
            var target = _hoverView;

            PlacementResult result;
            if (!target)
            {
                result = PlacementResult.OutOfContainer;
            }
            else if (mergeStacksOnDrop && TryGetMergeTarget(target, out var stack))
            {
                ItemStacking.Merge(item, stack, source.Grid, out _);
                result = PlacementResult.Success;
            }
            else
            {
                result = InventorySystem.TryTransfer(item, source.Grid, target.Grid, CurrentPlacement());
            }

            EndHold();
            Dropped?.Invoke(item, target, result);
            return result;
        }

        /// <summary>Puts the held item back where it was (it never left).</summary>
        public void CancelHold()
        {
            if (!IsHolding) return;
            var item = HeldItem;
            EndHold();
            HoldCancelled?.Invoke(item);
        }

        // ---------- provider callbacks ----------

        private void OnCellHovered(IInventoryInputProvider provider, InventoryGridView view, Vector2Int cell)
        {
            if (!provider.IsPointerBased) _followPointer = false;
            SetHover(view, cell);
        }

        private void OnPointerMoved(IInventoryInputProvider provider, Vector2 screenPosition)
        {
            _followPointer = true;
            _lastPointer = screenPosition;
            UpdateGhostPosition();
        }

        private void OnPrimaryPressed(IInventoryInputProvider provider)
        {
            if (IsHolding) TryDrop();
            else if (_hoverView) TryPickUp(_hoverView, _hoverCell);
        }

        private void OnPrimaryReleased(IInventoryInputProvider provider)
        {
            if (IsHolding && provider.DropOnRelease) TryDrop();
        }

        private void OnRotatePressed(IInventoryInputProvider provider) => RotateHeld();
        private void OnCancelPressed(IInventoryInputProvider provider) => CancelHold();

        // ---------- internals ----------

        private Placement CurrentPlacement() => new Placement(_hoverCell - _grabOffset, HeldRotation);

        private bool TryGetMergeTarget(InventoryGridView view, out ItemInstance stack)
        {
            stack = view.Grid.GetItemAt(_hoverCell);
            return stack != null && HeldItem.CanStackWith(stack) && stack.SpaceInStack > 0;
        }

        private void RefreshPreview()
        {
            foreach (var view in _views)
                if (view && view != _hoverView)
                    view.ClearPreview();

            if (!IsHolding || !_hoverView) return;

            _preview.Clear();
            if (mergeStacksOnDrop && TryGetMergeTarget(_hoverView, out var stack))
            {
                foreach (var cell in _hoverView.Grid.GetOccupiedCells(stack))
                    _preview.Add(new CellPreview(cell, CellPreviewStatus.Valid));
            }
            else
            {
                var ignore = _hoverView.Grid == SourceView.Grid ? HeldItem : null;
                _hoverView.Grid.GetPlacementPreview(HeldItem, CurrentPlacement(), _preview, ignore);
            }
            _hoverView.ShowPreview(_preview);
        }

        private void CreateGhost()
        {
            var parent = dragLayer;
            if (!parent)
            {
                var canvas = SourceView.GetComponentInParent<Canvas>();
                parent = canvas ? (RectTransform)canvas.rootCanvas.transform : (RectTransform)SourceView.transform.parent;
            }

            _ghost = SourceView.CreateItemView(HeldItem, parent);
            _ghost.name = "Held " + _ghost.name;
            _ghost.SetAlpha(ghostAlpha);
            _ghost.transform.SetAsLastSibling();

            // Match the source grid's on-screen scale even if the view is nested in scaled parents.
            var sourceScale = SourceView.transform.lossyScale;
            var parentScale = parent.lossyScale;
            _ghost.transform.localScale = new Vector3(
                parentScale.x != 0f ? sourceScale.x / parentScale.x : 1f,
                parentScale.y != 0f ? sourceScale.y / parentScale.y : 1f,
                1f);

            _ghost.RectTransform.anchorMin = _ghost.RectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _ghost.Layout(SourceView.Converter, HeldRotation);
        }

        private void UpdateGhostPosition()
        {
            if (!_ghost) return;
            var rt = _ghost.RectTransform;

            if (_followPointer)
            {
                // Smooth, pixel-exact follow: the grabbed cell's center sits under the pointer.
                var parent = (RectTransform)rt.parent;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, _lastPointer, UIFactory.CanvasCamera(parent), out var local))
                {
                    var grab = SourceView.Converter.ShapeCellCenterOffset(_grabOffset);
                    rt.localPosition = local - Vector2.Scale(grab, rt.localScale);
                }
            }
            else if (_hoverView)
            {
                // Discrete cursor (gamepad): snap to the target cell.
                rt.position = _hoverView.CellToWorld(CurrentPlacement().Origin);
            }
        }

        private void EndHold()
        {
            if (SourceView)
            {
                var sourceItemView = SourceView.GetItemView(HeldItem);
                if (sourceItemView) sourceItemView.SetAlpha(1f);
            }
            if (_ghost) Destroy(_ghost.gameObject);
            _ghost = null;

            foreach (var view in _views)
                if (view) view.ClearPreview();

            HeldItem = null;
            SourceView = null;
        }

        private void OnDisable() => CancelHold();

        private void OnDestroy()
        {
            foreach (var provider in _providers.ToArray())
                UnregisterProvider(provider);
        }
    }
}
