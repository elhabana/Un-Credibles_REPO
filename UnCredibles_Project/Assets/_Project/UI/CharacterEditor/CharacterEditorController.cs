using System;
using System.Collections.Generic;
using TMPro;
using UnCredibles.Localization;
using UnCredibles.Players;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UnCredibles.UI.CharacterEditor
{
    // Character editor ("probador"): a small studio with the character on a turntable and a
    // panel to try on helmets, outfits and boots. It builds its own stage, camera and UI, so it
    // can live in its own scene or be opened on top of another one (Open / Closed).
    public sealed class CharacterEditorController : MonoBehaviour
    {
        [SerializeField, Tooltip("PF_Shared_PJ (1 unit tall, with PlayerCharacterOutfit).")]
        private GameObject characterPrefab;
        [SerializeField, Tooltip("Player colours to preview (red, blue, green, yellow).")]
        private Material[] playerColors = new Material[0];
        [SerializeField] private float characterHeight = 1.7f;
        [SerializeField, Tooltip("Far from everything else, so it can be opened inside another scene.")]
        private Vector3 stagePosition = new Vector3(0f, -500f, 0f);
        [SerializeField] private bool openOnStart = true;
        [SerializeField] private Color backgroundTop = new Color(.16f, .19f, .26f);
        [SerializeField] private Color accent = new Color(1f, .78f, .25f);

        private static readonly Color PanelColor = new Color(.06f, .08f, .12f, .9f);
        private static readonly Color CardColor = new Color(.15f, .18f, .25f, 1f);
        private static readonly Color CardSelected = new Color(.33f, .29f, .14f, 1f);
        private static readonly CharacterPartSlot[] Slots = { CharacterPartSlot.Helmet, CharacterPartSlot.Outfit, CharacterPartSlot.Boots };

        // Camera framing per slot: point looked at (character 1.7 tall) and distance.
        private static readonly (Vector3 target, float distance)[] Framing =
        {
            (new Vector3(0f, 1.42f, 0f), 2.9f),   // helmet: the head
            (new Vector3(0f, .88f, 0f), 5.2f),    // outfit: the whole body
            (new Vector3(0f, .3f, 0f), 2.6f),     // boots: the feet
        };

        public event Action<CharacterLoadout> Closed;
        public CharacterLoadout Loadout => loadout;
        public bool IsOpen { get; private set; }

        private Transform stage;
        private Transform turntable;
        private Camera viewCamera;
        private GameObject character;
        private PlayerCharacterOutfit outfit;
        private PlayerCharacterAnimator animatorDriver;
        private Canvas canvas;
        private RectTransform grid;
        private TextMeshProUGUI runLabel;
        private readonly List<Button> tabs = new List<Button>();
        private readonly List<(Button button, string partId)> cards = new List<(Button, string)>();
        private readonly List<Button> swatches = new List<Button>();

        private CharacterLoadout loadout;
        private CharacterLoadout original;
        private int slotIndex = 1;
        private int colorIndex;
        private bool running;
        private float yaw;
        private float zoom = 1f;
        private Vector3 cameraTarget;
        private float cameraDistance = 3.5f;

        private void Start()
        {
            if (openOnStart) Open(CharacterLoadoutStore.Load(), 0);
        }

        public void Open(CharacterLoadout start, int playerColor)
        {
            if (stage == null) Build();
            original = loadout = start;
            colorIndex = Mathf.Clamp(playerColor, 0, Mathf.Max(0, playerColors.Length - 1));
            stage.gameObject.SetActive(true);
            canvas.gameObject.SetActive(true);
            IsOpen = true;
            yaw = 0f; zoom = 1f;
            PaintCharacter();
            SelectSlot(1, true);
        }

        public void Close(bool save)
        {
            if (!save) loadout = original;
            else CharacterLoadoutStore.Save(loadout);
            stage.gameObject.SetActive(false);
            canvas.gameObject.SetActive(false);
            IsOpen = false;
            Closed?.Invoke(loadout);
        }

        // ---------- Stage ----------

        private void Build()
        {
            stage = new GameObject("CharacterEditorStage").transform;
            stage.SetParent(transform, false);
            stage.position = stagePosition;

            var platform = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(platform.GetComponent<Collider>());
            platform.name = "Platform";
            platform.transform.SetParent(stage, false);
            platform.transform.localPosition = new Vector3(0f, -.04f, 0f);
            platform.transform.localScale = new Vector3(1.7f, .04f, 1.7f);
            platform.GetComponent<Renderer>().material = Tinted(new Color(.22f, .25f, .32f));
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(ring.GetComponent<Collider>());
            ring.name = "Ring";
            ring.transform.SetParent(stage, false);
            ring.transform.localPosition = new Vector3(0f, -.05f, 0f);
            ring.transform.localScale = new Vector3(1.85f, .03f, 1.85f);
            ring.GetComponent<Renderer>().material = Tinted(accent);

            // Studio lights: key from the front-left, a cool rim from behind, a soft fill.
            Light("Key", new Vector3(-1.6f, 2.8f, 2.2f), new Color(1f, .96f, .9f), 9f, 8f);
            Light("Rim", new Vector3(1.4f, 2.4f, -1.8f), new Color(.6f, .75f, 1f), 7f, 6f);
            Light("Fill", new Vector3(1.8f, 1f, 2.6f), new Color(.9f, .9f, 1f), 4f, 7f);

            turntable = new GameObject("Turntable").transform;
            turntable.SetParent(stage, false);
            character = Instantiate(characterPrefab, turntable);
            character.transform.localScale = Vector3.one * characterHeight;
            outfit = character.GetComponent<PlayerCharacterOutfit>();
            animatorDriver = character.GetComponent<PlayerCharacterAnimator>();

            var cameraObject = new GameObject("CharacterEditorCamera");
            cameraObject.transform.SetParent(stage, false);
            viewCamera = cameraObject.AddComponent<Camera>();
            viewCamera.clearFlags = CameraClearFlags.SolidColor;
            viewCamera.backgroundColor = backgroundTop;
            viewCamera.nearClipPlane = .05f;
            viewCamera.farClipPlane = 25f;
            viewCamera.fieldOfView = 30f;
            viewCamera.depth = 50f; // above any other camera when opened inside another scene

            BuildUI();
        }

        private void Light(string name, Vector3 position, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(stage, false);
            go.transform.localPosition = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
        }

        private static Material Tinted(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader);
            material.SetColor("_BaseColor", color);
            material.color = color;
            return material;
        }

        // The player colour goes on the skin; parts that use it take it from there.
        private void PaintCharacter()
        {
            if (playerColors.Length > 0)
            {
                var skin = playerColors[colorIndex];
                foreach (var renderer in character.GetComponentsInChildren<Renderer>(true))
                    if (renderer.name.StartsWith("Cuerpo_") || renderer.name.StartsWith("Parpado_"))
                        renderer.sharedMaterials = Fill(skin, renderer.sharedMaterials.Length);
            }
            outfit.Apply(loadout);
            for (int i = 0; i < swatches.Count; i++) Highlight(swatches[i], i == colorIndex);
        }

        private static Material[] Fill(Material material, int count)
        {
            var result = new Material[Mathf.Max(1, count)];
            for (int i = 0; i < result.Length; i++) result[i] = material;
            return result;
        }

        // ---------- UI ----------

        private void BuildUI()
        {
            var canvasObject = new GameObject("CharacterEditorUI", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

            // Whole screen: drag to turn the character, wheel to zoom (behind the panels).
            var dragArea = Rect("DragArea", canvasObject.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            dragArea.gameObject.AddComponent<Image>().color = Color.clear;
            var drag = dragArea.gameObject.AddComponent<DragSurface>();
            drag.Dragged += delta => yaw += delta.x * .35f;
            drag.Scrolled += amount => zoom = Mathf.Clamp(zoom - amount * .08f, .55f, 1.6f);

            // Left panel: title, slot tabs and the part grid.
            var panel = Rect("Panel", canvasObject.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(40f, 40f), new Vector2(620f, -40f));
            panel.gameObject.AddComponent<Image>().color = PanelColor;
            var title = Text(panel, T("probador.title", "Personaje"), 58, FontStyles.Bold);
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(32f, -100f), new Vector2(-32f, -24f));
            title.color = accent;

            var tabRow = Rect("Tabs", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(32f, -180f), new Vector2(-32f, -112f));
            var tabLayout = tabRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 10f; tabLayout.childForceExpandWidth = true; tabLayout.childControlWidth = true; tabLayout.childControlHeight = true;
            string[] tabKeys = { "probador.tab.helmet", "probador.tab.outfit", "probador.tab.boots" };
            string[] tabNames = { "Casco", "Ropa", "Botas" };
            for (int i = 0; i < Slots.Length; i++)
            {
                int index = i;
                tabs.Add(Button(tabRow, T(tabKeys[i], tabNames[i]), 32, () => SelectSlot(index, false)));
            }

            var scroll = Rect("Scroll", panel, Vector2.zero, Vector2.one, new Vector2(32f, 32f), new Vector2(-32f, -200f));
            var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
            scroll.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, .15f);
            scroll.gameObject.AddComponent<RectMask2D>();
            grid = Rect("Grid", scroll, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            grid.pivot = new Vector2(.5f, 1f);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(170f, 200f);
            layout.spacing = new Vector2(14f, 14f);
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;
            grid.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = grid;
            scrollRect.horizontal = false;
            scrollRect.scrollSensitivity = 30f;

            // Bottom right: colours, run preview, back and save.
            var bar = Rect("Bar", canvasObject.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-1160f, 40f), new Vector2(-40f, 130f));
            var barLayout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            barLayout.spacing = 14f; barLayout.childAlignment = TextAnchor.MiddleRight;
            barLayout.childControlWidth = false; barLayout.childControlHeight = true; barLayout.childForceExpandWidth = false;
            for (int i = 0; i < playerColors.Length; i++)
            {
                int index = i;
                var swatch = Button(bar, string.Empty, 20, () => { colorIndex = index; PaintCharacter(); });
                swatch.GetComponent<Image>().color = Color.Lerp(BaseColor(playerColors[i]), Color.white, .05f);
                ((RectTransform)swatch.transform).sizeDelta = new Vector2(90f, 90f);
                swatches.Add(swatch);
            }
            var run = Button(bar, T("probador.run", "Correr"), 30, ToggleRun);
            ((RectTransform)run.transform).sizeDelta = new Vector2(200f, 90f);
            runLabel = run.GetComponentInChildren<TextMeshProUGUI>();
            var back = Button(bar, T("probador.back", "Volver"), 30, () => Close(false));
            ((RectTransform)back.transform).sizeDelta = new Vector2(200f, 90f);
            var save = Button(bar, T("probador.save", "Guardar"), 30, () => Close(true));
            ((RectTransform)save.transform).sizeDelta = new Vector2(220f, 90f);
            save.GetComponent<Image>().color = accent;
            save.GetComponentInChildren<TextMeshProUGUI>().color = new Color(.1f, .08f, .02f);

            var hint = Text(canvasObject.transform, T("probador.hint", "Arrastra para girar  ·  Rueda para acercar  ·  Q / E cambia de pestaña"), 26, FontStyles.Normal);
            Place(hint.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-1100f, -90f), new Vector2(-40f, -40f));
            hint.alignment = TextAlignmentOptions.Right;
            hint.color = new Color(1f, 1f, 1f, .7f);
        }

        private void SelectSlot(int index, bool immediate)
        {
            slotIndex = index;
            for (int i = 0; i < tabs.Count; i++) Highlight(tabs[i], i == index);
            var (target, distance) = Framing[index];
            if (immediate)
            {
                cameraTarget = target;
                cameraDistance = distance;
            }
            FillGrid();
        }

        private void FillGrid()
        {
            foreach (Transform child in grid) Destroy(child.gameObject);
            cards.Clear();
            var slot = Slots[slotIndex];
            AddCard(null, slot);
            if (outfit.Catalog != null)
                foreach (var part in outfit.Catalog.InSlot(slot)) AddCard(part, slot);
            RefreshCards();
            var selected = cards.Find(c => c.partId == (loadout.Get(slot) ?? string.Empty)).button ?? cards[0].button;
            EventSystem.current?.SetSelectedGameObject(selected.gameObject);
        }

        private void AddCard(CharacterPart part, CharacterPartSlot slot)
        {
            string id = part != null ? part.Id : string.Empty;
            var card = Button(grid, string.Empty, 22, () =>
            {
                loadout = loadout.With(slot, id);
                outfit.Apply(loadout);
                RefreshCards();
            });
            var icon = Rect("Icon", card.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(10f, 46f), new Vector2(-10f, -10f));
            if (part != null && part.Icon != null)
            {
                var image = icon.gameObject.AddComponent<Image>();
                image.sprite = part.Icon;
                image.preserveAspect = true;
                image.raycastTarget = false;
            }
            else
            {
                var dash = Text(icon, part == null ? "—" : "?", 64, FontStyles.Bold);
                Place(dash.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                dash.color = new Color(1f, 1f, 1f, .4f);
            }
            var label = Text(card.transform, part == null ? T("probador.none", "Nada") : T("part." + part.Id, part.DisplayName), 22, FontStyles.Bold);
            Place(label.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(6f, 6f), new Vector2(-6f, 44f));
            label.enableAutoSizing = true; label.fontSizeMin = 14f; label.fontSizeMax = 22f;
            cards.Add((card, id));
        }

        private void RefreshCards()
        {
            string current = loadout.Get(Slots[slotIndex]) ?? string.Empty;
            foreach (var (button, partId) in cards) Highlight(button, partId == current);
        }

        private void ToggleRun()
        {
            running = !running;
            animatorDriver.SetRunning(running);
            runLabel.text = running ? T("probador.stop", "Parar") : T("probador.run", "Correr");
        }

        // ---------- Every frame: controls and camera ----------

        private void Update()
        {
            if (!IsOpen) return;
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            if (keyboard != null)
            {
                if (keyboard.qKey.wasPressedThisFrame) SelectSlot((slotIndex + Slots.Length - 1) % Slots.Length, false);
                if (keyboard.eKey.wasPressedThisFrame) SelectSlot((slotIndex + 1) % Slots.Length, false);
                if (keyboard.escapeKey.wasPressedThisFrame) Close(false);
            }
            if (gamepad != null)
            {
                if (gamepad.leftShoulder.wasPressedThisFrame) SelectSlot((slotIndex + Slots.Length - 1) % Slots.Length, false);
                if (gamepad.rightShoulder.wasPressedThisFrame) SelectSlot((slotIndex + 1) % Slots.Length, false);
                yaw += gamepad.rightStick.ReadValue().x * 180f * Time.unscaledDeltaTime;
                zoom = Mathf.Clamp(zoom + (gamepad.leftTrigger.ReadValue() - gamepad.rightTrigger.ReadValue()) * Time.unscaledDeltaTime, .55f, 1.6f);
                if (gamepad.buttonEast.wasPressedThisFrame) Close(false);
            }

            turntable.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var (target, distance) = Framing[slotIndex];
            float blend = 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime);
            cameraTarget = Vector3.Lerp(cameraTarget, target, blend);
            cameraDistance = Mathf.Lerp(cameraDistance, distance * zoom, blend);
            // The panel covers the left of the screen: the camera looks a bit to its left (world +X,
            // it faces -Z) so the character sits right of centre.
            var side = Vector3.right * cameraDistance * .17f;
            var focus = stage.TransformPoint(cameraTarget + side);
            var eye = stage.TransformPoint(cameraTarget + new Vector3(0f, .12f, 1f).normalized * cameraDistance + side);
            viewCamera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(focus - eye));
        }

        // ---------- UI helpers ----------

        private static string T(string key, string fallback)
        {
            string text = L10n.Get(L10n.UI, key);
            return string.IsNullOrEmpty(text) || text == key ? fallback : text;
        }

        private static Color BaseColor(Material material) =>
            material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color;

        private void Highlight(Button button, bool on)
        {
            var image = button.GetComponent<Image>();
            if (swatches.Contains(button))
            {
                var outline = OutlineOf(button);
                outline.effectColor = accent;
                outline.effectDistance = on ? new Vector2(6f, -6f) : Vector2.zero;
                return;
            }
            if (image.color != accent) image.color = on ? CardSelected : CardColor;
            var outlineCard = OutlineOf(button);
            outlineCard.effectColor = accent;
            outlineCard.effectDistance = on ? new Vector2(3f, -3f) : Vector2.zero;
        }

        private static Outline OutlineOf(Button button)
        {
            var outline = button.GetComponent<Outline>();
            return outline != null ? outline : button.gameObject.AddComponent<Outline>();
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Place(rect, anchorMin, anchorMax, offsetMin, offsetMax);
            return rect;
        }

        private static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
        }

        private static TextMeshProUGUI Text(Transform parent, string text, float size, FontStyles style)
        {
            var label = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(parent, false);
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        private static Button Button(Transform parent, string text, float size, Action onClick)
        {
            var rect = new GameObject("Button", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = CardColor;
            var button = rect.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
            colors.selectedColor = new Color(1.2f, 1.2f, 1.2f);
            colors.colorMultiplier = 1.4f;
            button.colors = colors;
            button.onClick.AddListener(() => onClick());
            if (!string.IsNullOrEmpty(text))
            {
                var label = Text(rect, text, size, FontStyles.Bold);
                Place(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));
            }
            return button;
        }
    }
}
