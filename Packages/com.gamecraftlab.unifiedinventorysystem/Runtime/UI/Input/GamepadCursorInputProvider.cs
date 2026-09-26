using System;
using System.Collections.Generic;
using UnityEngine;
#if GCL_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#else
#pragma warning disable CS0067, CS0414 // only used when the Input System is available
#endif

namespace GameCraftLab.UnifiedInventorySystem.UI
{
    /// <summary>
    /// Discrete grid cursor for gamepads (Input System package).
    /// D-pad / left stick moves, South (A / Cross) picks up and drops, North (Y / Triangle) rotates,
    /// East (B / Circle) cancels, shoulders switch between grids.
    /// Put one in the scene and point it at the <see cref="InventoryInteraction"/>.
    /// </summary>
    public class GamepadCursorInputProvider : MonoBehaviour, IInventoryInputProvider
    {
        [SerializeField] private InventoryInteraction interaction;
        [SerializeField, Tooltip("Grids the shoulders cycle through. Empty = every view registered with the interaction.")]
        private List<InventoryGridView> views = new List<InventoryGridView>();
        [SerializeField] private float repeatDelay = 0.35f;
        [SerializeField] private float repeatInterval = 0.08f;
        [SerializeField, Range(0.1f, 0.95f)] private float stickThreshold = 0.5f;

        private int _viewIndex;
        private Vector2Int _cell;
        private Vector2Int _heldDirection;
        private float _nextRepeat;
        private InventoryGridView _cursorView;
        private bool _active;

        public bool DropOnRelease => false;
        public bool IsPointerBased => false;

        public event Action<IInventoryInputProvider, InventoryGridView, Vector2Int> CellHovered;
        public event Action<IInventoryInputProvider, Vector2> PointerMoved { add { } remove { } }
        public event Action<IInventoryInputProvider> PrimaryPressed;
        public event Action<IInventoryInputProvider> PrimaryReleased { add { } remove { } }
        public event Action<IInventoryInputProvider> RotatePressed;
        public event Action<IInventoryInputProvider> CancelPressed;

        public InventoryInteraction Interaction
        {
            get => interaction;
            set
            {
                if (interaction && isActiveAndEnabled) interaction.UnregisterProvider(this);
                interaction = value;
                if (interaction && isActiveAndEnabled) interaction.RegisterProvider(this);
            }
        }

        private IReadOnlyList<InventoryGridView> Views
            => views.Count > 0 ? views : interaction ? interaction.Views : (IReadOnlyList<InventoryGridView>)Array.Empty<InventoryGridView>();

        private void OnEnable()
        {
            if (interaction) interaction.RegisterProvider(this);
        }

        private void OnDisable()
        {
            if (interaction) interaction.UnregisterProvider(this);
            HideCursor();
        }

        /// <summary>Moves the cursor from code (also shows it).</summary>
        public void SetCursor(InventoryGridView view, Vector2Int cell)
        {
            var list = Views;
            for (int i = 0; i < list.Count; i++)
                if (list[i] == view) _viewIndex = i;
            _cell = cell;
            _active = true;
            Report();
        }

#if GCL_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
        private void Update()
        {
            var pad = Gamepad.current;
            if (pad == null || Views.Count == 0) return;

            var direction = ReadDirection(pad);
            bool anyButton = pad.buttonSouth.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame
                             || pad.buttonEast.wasPressedThisFrame || pad.leftShoulder.wasPressedThisFrame
                             || pad.rightShoulder.wasPressedThisFrame;

            if (!_active)
            {
                if (direction == Vector2Int.zero && !anyButton) return;
                _active = true;           // first touch just wakes the cursor up
                _heldDirection = direction;
                _nextRepeat = Time.unscaledTime + repeatDelay;
                ClampCell();
                Report();
                return;
            }

            HandleMovement(direction);

            if (pad.leftShoulder.wasPressedThisFrame) SwitchView(-1);
            if (pad.rightShoulder.wasPressedThisFrame) SwitchView(+1);
            if (pad.buttonSouth.wasPressedThisFrame) PrimaryPressed?.Invoke(this);
            if (pad.buttonNorth.wasPressedThisFrame) RotatePressed?.Invoke(this);
            if (pad.buttonEast.wasPressedThisFrame) CancelPressed?.Invoke(this);
        }

        private Vector2Int ReadDirection(Gamepad pad)
        {
            var v = pad.dpad.ReadValue();
            if (v.sqrMagnitude < 0.01f) v = pad.leftStick.ReadValue();

            // Screen up is -y on the grid.
            if (Mathf.Abs(v.x) >= Mathf.Abs(v.y))
                return Mathf.Abs(v.x) >= stickThreshold ? new Vector2Int(v.x > 0 ? 1 : -1, 0) : Vector2Int.zero;
            return Mathf.Abs(v.y) >= stickThreshold ? new Vector2Int(0, v.y > 0 ? -1 : 1) : Vector2Int.zero;
        }
#endif

        private void HandleMovement(Vector2Int direction)
        {
            if (direction == Vector2Int.zero)
            {
                _heldDirection = Vector2Int.zero;
                return;
            }

            bool fresh = direction != _heldDirection;
            if (!fresh && Time.unscaledTime < _nextRepeat) return;

            _nextRepeat = Time.unscaledTime + (fresh ? repeatDelay : repeatInterval);
            _heldDirection = direction;

            _cell += direction;
            ClampCell();
            Report();
        }

        private void SwitchView(int step)
        {
            var list = Views;
            if (list.Count == 0) return;
            _viewIndex = ((_viewIndex + step) % list.Count + list.Count) % list.Count;
            ClampCell();
            Report();
        }

        private void ClampCell()
        {
            var list = Views;
            if (list.Count == 0) return;
            _viewIndex = Mathf.Clamp(_viewIndex, 0, list.Count - 1);
            var grid = list[_viewIndex].Grid;
            if (grid == null) return;

            var b = grid.Bounds;
            _cell = new Vector2Int(Mathf.Clamp(_cell.x, b.xMin, b.xMax - 1), Mathf.Clamp(_cell.y, b.yMin, b.yMax - 1));
        }

        private void Report()
        {
            var list = Views;
            if (list.Count == 0) return;
            var view = list[Mathf.Clamp(_viewIndex, 0, list.Count - 1)];

            if (_cursorView && _cursorView != view) _cursorView.SetCursor(null);
            _cursorView = view;
            view.SetCursor(_cell);
            CellHovered?.Invoke(this, view, _cell);
        }

        private void HideCursor()
        {
            if (_cursorView) _cursorView.SetCursor(null);
            _cursorView = null;
            _active = false;
        }

#if !(GCL_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM)
        private void Awake()
        {
            Debug.LogWarning("[GamepadCursorInputProvider] Needs the Input System package with the new input backend enabled. " +
                             "You can still drive it from code via SetCursor / your own provider.", this);
        }
#endif
    }
}
