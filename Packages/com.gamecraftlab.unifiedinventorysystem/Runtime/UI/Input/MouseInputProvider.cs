using System;
using UnityEngine;
using UnityEngine.EventSystems;
#if GCL_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace GameCraftLab.UnifiedInventorySystem.UI
{
    /// <summary>
    /// Mouse / touch drag &amp; drop through the EventSystem: press on an item to pick it up, drag, release to drop.
    /// Right click (or the rotate key) rotates while dragging, Escape cancels.
    /// Add it next to each <see cref="InventoryGridView"/>; the view registers it with its interaction.
    /// </summary>
    [RequireComponent(typeof(InventoryGridView))]
    public class MouseInputProvider : MonoBehaviour, IInventoryInputProvider,
        IPointerDownHandler, IPointerUpHandler, IDragHandler, IPointerMoveHandler, IPointerExitHandler
    {
        [SerializeField] private bool rightClickRotates = true;
        [SerializeField, Tooltip("Rotate / cancel keys. Only one provider per frame reacts, however many grids there are.")]
        private bool keyboardShortcuts = true;
#if GCL_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
        [SerializeField] private Key rotateKey = Key.R;
        [SerializeField] private Key cancelKey = Key.Escape;
#elif ENABLE_LEGACY_INPUT_MANAGER
        [SerializeField] private KeyCode rotateKey = KeyCode.R;
        [SerializeField] private KeyCode cancelKey = KeyCode.Escape;
#endif

        private static int s_lastKeyboardFrame = -1;
        private InventoryGridView _view;
        private bool _pressed;

        public bool DropOnRelease => true;
        public bool IsPointerBased => true;

        public event Action<IInventoryInputProvider, InventoryGridView, Vector2Int> CellHovered;
        public event Action<IInventoryInputProvider, Vector2> PointerMoved;
        public event Action<IInventoryInputProvider> PrimaryPressed;
        public event Action<IInventoryInputProvider> PrimaryReleased;
        public event Action<IInventoryInputProvider> RotatePressed;
        public event Action<IInventoryInputProvider> CancelPressed;

        private void Awake() => _view = GetComponent<InventoryGridView>();

        private void OnEnable()
        {
            if (_view && _view.Interaction) _view.Interaction.RegisterProvider(this);
        }

        private void OnDisable()
        {
            if (_view && _view.Interaction) _view.Interaction.UnregisterProvider(this);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                _pressed = true;
                ReportHover(eventData);
                PointerMoved?.Invoke(this, eventData.position);
                PrimaryPressed?.Invoke(this);
            }
            else if (eventData.button == PointerEventData.InputButton.Right && rightClickRotates)
            {
                RotatePressed?.Invoke(this);
            }
        }

        // Sent to the grid where the drag started, even when the pointer is over another grid.
        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            ReportHover(eventData);
            PointerMoved?.Invoke(this, eventData.position);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            _pressed = false;
            ReportHover(eventData);
            PrimaryReleased?.Invoke(this);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (_pressed || eventData.dragging) return;
            ReportHover(eventData);
            PointerMoved?.Invoke(this, eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_pressed || eventData.dragging) return;
            CellHovered?.Invoke(this, null, default);
        }

        private void Update()
        {
            if (!keyboardShortcuts || s_lastKeyboardFrame == Time.frameCount) return;

            bool rotate = false, cancel = false;
#if GCL_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                rotate = keyboard[rotateKey].wasPressedThisFrame;
                cancel = keyboard[cancelKey].wasPressedThisFrame;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            rotate = Input.GetKeyDown(rotateKey);
            cancel = Input.GetKeyDown(cancelKey);
#endif
            if (!rotate && !cancel) return;

            s_lastKeyboardFrame = Time.frameCount;
            if (rotate) RotatePressed?.Invoke(this);
            if (cancel) CancelPressed?.Invoke(this);
        }

        private void ReportHover(PointerEventData eventData)
        {
            var hit = eventData.pointerCurrentRaycast.gameObject;
            var view = hit ? hit.GetComponentInParent<InventoryGridView>() : null;

            if (view && view.ScreenToCell(eventData.position, out var cell))
                CellHovered?.Invoke(this, view, cell);
            else
                CellHovered?.Invoke(this, null, default);
        }
    }
}
