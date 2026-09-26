using System.Collections.Generic;
using System.IO;
using GameCraftLab.UnifiedInventorySystem.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if GCL_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace GameCraftLab.UnifiedInventorySystem.Samples
{
    /// <summary>
    /// One engine, three inventory styles side by side — everything built from code, no assets needed.
    /// Drop this on an empty GameObject in an empty scene and press Play.
    ///
    /// Mouse: drag to move, right click / R to rotate while dragging, Esc to cancel.
    /// Gamepad: d-pad moves, A picks up / drops, Y rotates, B cancels, shoulders switch grid.
    /// Keys: D damages a random hold cell (Dredge style), F repairs all, S saves, L loads.
    /// </summary>
    public class BasicDemo : MonoBehaviour
    {
        private enum HoldCell { Normal, Damaged }

        [SerializeField] private Vector2 cellSize = new Vector2(52f, 52f);

        private readonly List<ItemDefinition> _definitions = new List<ItemDefinition>();
        private InventorySystem _system;
        private CellStateManager<HoldCell> _holdStates;
        private InventoryGrid _hold;
        private Text _status;
        private Font _font;

        private string SavePath => Path.Combine(Application.persistentDataPath, "grid-inventory-demo.json");

        private void Start()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem();
            var canvas = CreateCanvas();
            var interaction = canvas.gameObject.AddComponent<InventoryInteraction>();
            interaction.Dropped += (item, view, result) =>
                SetStatus(result == PlacementResult.Success ? $"Moved {item.Definition.DisplayName}." : $"Can't drop there: {result}.");

            // --- Items -------------------------------------------------------------------------
            var potion = Def("potion", "X", "Potion", 10);
            var coin = Def("coin", "X", "Coins", 99);
            var herb = Def("herb", "X", "Herb", 5);
            var gem = Def("gem", "X", "Gem");
            var sword = Def("sword", "X/X/X", "Sword", tags: "weapon");
            var rifle = Def("rifle", "XXXX/.X..", "Rifle", tags: "weapon");
            var pistol = Def("pistol", "XX/X.", "Pistol", tags: "weapon");
            var shield = Def("shield", "XX/XX", "Shield");
            var ammo = Def("ammo", "X", "Ammo", 30);
            var cod = Def("cod", "XX", "Cod", tags: "fish");
            var eel = Def("eel", "XXXX", "Eel", tags: "fish");
            var crab = Def("crab", ".X./XXX", "Crab", tags: "fish");
            var snapper = Def("snapper", "X./XX", "Snapper", tags: "fish");

            // --- Containers ----------------------------------------------------------------------
            _system = new InventorySystem();

            // Skyrim-ish: every item is one cell.
            var basic = _system.AddContainer("basic", new InventoryGrid(6, 3));

            // RE4-ish: rectangle, multi-cell items, rotation.
            var re4 = _system.AddContainer("attache", new InventoryGrid(8, 5));

            // Dredge-ish: irregular hull, cells can get damaged.
            _holdStates = new CellStateManager<HoldCell>();
            _holdStates.Register(HoldCell.Damaged, (grid, cell, item) => false);
            _hold = _system.AddContainer("hold", new InventoryGrid(ShapePattern.Parse(
                ".XXXX./" +
                "XXXXXX/" +
                "XXXXXX/" +
                ".XX.XX"), _holdStates));

            _system.AddItem(potion, 14);
            _system.AddItem(coin, 120);
            _system.AddItem(herb, 3);
            _system.AddItem(gem, 2);

            re4.TryAutoPlace(new ItemInstance(sword));
            re4.TryAutoPlace(new ItemInstance(rifle));
            re4.TryAutoPlace(new ItemInstance(pistol));
            re4.TryAutoPlace(new ItemInstance(shield));
            re4.TryAutoPlace(new ItemInstance(ammo, 30));
            re4.TryAutoPlace(new ItemInstance(ammo, 12));

            _hold.TryAutoPlace(new ItemInstance(eel));
            _hold.TryAutoPlace(new ItemInstance(crab));
            _hold.TryAutoPlace(new ItemInstance(cod));
            _hold.TryAutoPlace(new ItemInstance(snapper));

            // --- Views ---------------------------------------------------------------------------
            float x = 40f;
            x = AddView(canvas.transform, "Basic (1 cell each)", basic, interaction, x, null);
            x = AddView(canvas.transform, "Attaché case (RE4)", re4, interaction, x, null);
            AddView(canvas.transform, "Hold (Dredge)", _hold, interaction, x,
                new CellStateColorProvider<HoldCell>(_holdStates).Map(HoldCell.Damaged, new Color(0.45f, 0.08f, 0.08f, 0.9f)));

            var pad = canvas.gameObject.AddComponent<GamepadCursorInputProvider>();
            pad.Interaction = interaction;

            var help = CreateText(canvas.transform, 16, TextAnchor.LowerLeft);
            help.rectTransform.anchorMin = new Vector2(0f, 0f);
            help.rectTransform.anchorMax = new Vector2(1f, 0f);
            help.rectTransform.pivot = new Vector2(0f, 0f);
            help.rectTransform.anchoredPosition = new Vector2(40f, 20f);
            help.rectTransform.sizeDelta = new Vector2(-80f, 60f);
            help.text = "Mouse: drag · right click / R rotate · Esc cancel      Gamepad: d-pad · A pick/drop · Y rotate · B cancel · LB/RB grid\n" +
                        "D damage a hold cell · F repair · S save · L load";

            _status = CreateText(canvas.transform, 18, TextAnchor.UpperLeft);
            _status.rectTransform.anchorMin = _status.rectTransform.anchorMax = _status.rectTransform.pivot = new Vector2(0f, 1f);
            _status.rectTransform.anchoredPosition = new Vector2(40f, -20f);
            _status.rectTransform.sizeDelta = new Vector2(1200f, 30f);
            SetStatus("Ready.");
        }

        private void Update()
        {
            if (Pressed("d")) DamageRandomHoldCell();
            if (Pressed("f")) { _holdStates.ClearStates(); SetStatus("Hold repaired."); }
            if (Pressed("s")) Save();
            if (Pressed("l")) Load();
        }

        private void DamageRandomHoldCell()
        {
            var candidates = new List<Vector2Int>();
            foreach (var cell in _hold.Cells)
                if (_holdStates.GetState(cell) == HoldCell.Normal)
                    candidates.Add(cell);
            if (candidates.Count == 0) return;

            var target = candidates[Random.Range(0, candidates.Count)];
            _holdStates.SetState(target, HoldCell.Damaged);

            // The package never decides what happens to the item — the game does. Here fish are lost.
            var victim = _hold.GetItemAt(target);
            if (victim != null && victim.Definition.HasTag("fish"))
            {
                _hold.Remove(victim);
                SetStatus($"Hull damaged at {target}: the {victim.Definition.DisplayName} was lost!");
            }
            else
            {
                SetStatus($"Hull damaged at {target}.");
            }
        }

        private void Save()
        {
            InventorySerializer.SaveToFile(_system, SavePath);
            SetStatus($"Saved to {SavePath}");
        }

        private void Load()
        {
            if (!File.Exists(SavePath)) { SetStatus("Nothing saved yet — press S first."); return; }
            var db = ItemDatabase.Create(_definitions);
            var report = InventorySerializer.LoadFromFile(_system, SavePath, db);
            Destroy(db);
            SetStatus(report.HasWarnings ? report.ToString().Replace('\n', ' ') : $"Loaded {report.ItemsRestored} item(s).");
        }

        // ---------- scene building ----------

        private float AddView(Transform canvas, string title, InventoryGrid grid, InventoryInteraction interaction, float x,
            ICellVisualProvider visuals)
        {
            var label = CreateText(canvas, 20, TextAnchor.LowerLeft);
            label.text = title;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(0f, 1f);
            label.rectTransform.anchoredPosition = new Vector2(x, -70f);
            label.rectTransform.sizeDelta = new Vector2(400f, 28f);

            var go = new GameObject(title, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -105f);

            var view = go.AddComponent<InventoryGridView>();
            view.SetLayout(cellSize, new Vector2(3f, 3f));
            go.AddComponent<MouseInputProvider>();    // add before Bind so the interaction picks it up
            view.CellVisuals = visuals;
            view.Bind(grid, interaction);

            return x + rt.rect.width + 60f;
        }

        private Canvas CreateCanvas()
        {
            var go = new GameObject("Inventory Canvas", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            var bg = new GameObject("Background", typeof(RectTransform)).AddComponent<Image>();
            bg.transform.SetParent(go.transform, false);
            bg.rectTransform.anchorMin = Vector2.zero;
            bg.rectTransform.anchorMax = Vector2.one;
            bg.rectTransform.offsetMin = bg.rectTransform.offsetMax = Vector2.zero;
            bg.color = new Color(0.09f, 0.1f, 0.12f);
            bg.raycastTarget = false;
            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>()) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if GCL_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        private Text CreateText(Transform parent, int size, TextAnchor anchor)
        {
            var text = new GameObject("Text", typeof(RectTransform)).AddComponent<Text>();
            text.transform.SetParent(parent, false);
            text.font = _font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = new Color(0.9f, 0.9f, 0.9f);
            text.raycastTarget = false;
            return text;
        }

        private ItemDefinition Def(string id, string pattern, string displayName, int maxStack = 1, params string[] tags)
        {
            var def = ItemDefinition.Create(id, ShapePattern.Parse(pattern), maxStack, true, displayName, null, tags);
            _definitions.Add(def);
            return def;
        }

        private void SetStatus(string message)
        {
            if (_status) _status.text = message;
        }

        private static bool Pressed(string key)
        {
#if GCL_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            switch (key)
            {
                case "d": return keyboard.dKey.wasPressedThisFrame;
                case "f": return keyboard.fKey.wasPressedThisFrame;
                case "s": return keyboard.sKey.wasPressedThisFrame;
                case "l": return keyboard.lKey.wasPressedThisFrame;
                default: return false;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(key);
#else
            return false;
#endif
        }

        private void OnDestroy()
        {
            foreach (var def in _definitions)
                if (def) Destroy(def);
        }
    }
}
