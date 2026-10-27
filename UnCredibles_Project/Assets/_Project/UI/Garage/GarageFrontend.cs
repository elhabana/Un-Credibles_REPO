using System.Collections;
using TMPro;
using UnCredibles.Core;
using UnCredibles.UI.MainMenu;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnCredibles.Localization;

namespace UnCredibles.UI.Garage
{
    public sealed class GarageFrontend : MonoBehaviour
    {
        public GarageCameraRig cameras;
        public MainMenuController menu;
        public GameObject menuRoot;
        public GameObject lobbyRoot;
        public GameObject[] detailPanels;
        public Slider masterVolume;
        public Slider musicVolume;
        public Slider sfxVolume;
        [Header("Settings on the TV")]
        [Tooltip("Background of the old screen-space settings panel; hidden once the panel is on the TV.")]
        public GameObject settingsScreenPanel;
        public TextMeshProUGUI settingsTitle;
        public RectTransform settingsBack;
        public TextMeshProUGUI masterLabel;
        public TextMeshProUGUI musicLabel;
        public TextMeshProUGUI sfxLabel;
        public TextMeshProUGUI tvInstruction;
        [Tooltip("TV screen mesh (faces its local +Z). The settings canvas and the TV camera are fitted to it.")]
        public MeshFilter tvScreen;
        [Tooltip("CRT material of the TV screen mesh (UnCredibles/Garage/CRTScreen); it shows the settings texture.")]
        public Material tvScreenMaterial;
        [Tooltip("Layer seen only by the camera that renders the settings into the TV screen.")]
        public LayerMask tvScreenLayer;
        [Tooltip("Sprites and title font of the settings menu on the TV.")]
        public TvMenuSkin tvMenuSkin;
        // Settings layout size in canvas units; the canvas grows to the screen's aspect around it.
        private static readonly Vector2 SettingsLayout = new Vector2(170f, 100f);
        private const int TvTextureWidth = 1024;
        // Part of the glass left uncovered by the TV frame and its rounded corners; the content stays inside it.
        private const float TvSafeArea = .82f;
        // The TV starts warming up while the camera is still flying to it.
        private const float TvPowerOnDelay = .35f;
        private const float TvPowerOnSeconds = 1.1f;
        private const float TvPowerOffSeconds = .4f;
        private static readonly int TvPowerId = Shader.PropertyToID("_Power");
        private CoreRoot core;
        private Material tvMaterial;
        private Mesh tvMesh;
        private Camera tvCamera;
        private Coroutine tvSwitch;
        private float tvPower;
        private TvSettingsOsd tvOsd;
        private TextMeshProUGUI languageLabel;
        private TextMeshProUGUI languageValue;
        private Button previousLanguage;
        private Button nextLanguage;

        private IEnumerator Start()
        {
            PlaceSettingsOnTv();
            while (CoreRoot.Instance == null) yield return null;
            core = CoreRoot.Instance;
            gameObject.AddComponent<UnCredibles.UI.Voting.MapVoteView>();
            core.GameFlow.StateChanged += StateChanged;
            menu.SettingsRequested += ShowSettings;
            masterVolume.SetValueWithoutNotify(core.Settings.MasterVolume);
            musicVolume.SetValueWithoutNotify(core.Settings.MusicVolume);
            sfxVolume.SetValueWithoutNotify(core.Settings.SfxVolume);
            ConfigureVolume(masterVolume, masterLabel, "settings.master");
            ConfigureVolume(musicVolume, musicLabel, "settings.music");
            ConfigureVolume(sfxVolume, sfxLabel, "settings.sfx");
            masterVolume.onValueChanged.AddListener(SetVolume);
            musicVolume.onValueChanged.AddListener(SetVolume);
            sfxVolume.onValueChanged.AddListener(SetVolume);
            previousLanguage.onClick.AddListener(() => core.Settings.SetLanguage(L10n.NextLanguage(-1)));
            nextLanguage.onClick.AddListener(() => core.Settings.SetLanguage(L10n.NextLanguage(1)));
            ShowLanguage();
            L10n.LanguageChanged += HandleLanguageChanged;
            StateChanged(core.GameFlow.State, core.GameFlow.State);
        }

        private void OnDestroy()
        {
            if (core != null) core.GameFlow.StateChanged -= StateChanged;
            if (menu != null) menu.SettingsRequested -= ShowSettings;
            L10n.LanguageChanged -= HandleLanguageChanged;
            if (tvCamera != null)
            {
                tvCamera.targetTexture.Release();
                Destroy(tvCamera.targetTexture);
            }
            Destroy(tvMaterial);
            Destroy(tvMesh);
        }

        private void HandleLanguageChanged()
        {
            ShowVolume(masterLabel, "settings.master", masterVolume.value);
            ShowVolume(musicLabel, "settings.music", musicVolume.value);
            ShowVolume(sfxLabel, "settings.sfx", sfxVolume.value);
            ShowLanguage();
            ShowInstruction();
        }

        private void ShowLanguage()
        {
            languageLabel.text = L10n.Get(L10n.UI, "settings.language");
            languageValue.text = L10n.LanguageName(L10n.CurrentLanguage).ToUpperInvariant();
        }

        private void Update()
        {
            if (cameras.View < GarageView.Settings) return;
            if (cameras.View == GarageView.Settings && tvOsd != null)
                tvOsd.Tick(EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null);
            if ((Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame) ||
                (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)) ShowHome();
        }

        private void StateChanged(GameState previous, GameState current)
        {
            if (current == GameState.PartyLobby) Show(GarageView.Lobby);
            else if (current == GameState.MainMenu || current == GameState.Boot)
            {
                Show(GarageView.Home);
                menu.ResetView();
            }
            else if (current == GameState.Voting)
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                menuRoot.SetActive(false);
                lobbyRoot.SetActive(false);
                for (int i = 0; i < detailPanels.Length; i++) detailPanels[i].SetActive(false);
            }
            else lobbyRoot.SetActive(false);
        }

        public void ShowHome()
        {
            if (core != null) core.Settings.Save();
            Show(GarageView.Home);
            menu.ResetView();
        }
        public void ShowSettings() => Show(GarageView.Settings);
        public void ShowCredits() => Show(GarageView.Credits);
        public void ShowGallery() => Show(GarageView.Gallery);

        private void Show(GarageView view)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            menuRoot.SetActive(view == GarageView.Home);
            lobbyRoot.SetActive(view == GarageView.Lobby);
            for (int i = 0; i < detailPanels.Length; i++)
                detailPanels[i].SetActive((int)view == i + 2);
            ShowOnTv(view == GarageView.Settings);
            cameras.Show(view);
            if (view == GarageView.Home) menu.FocusMain();
            else if ((int)view >= 2)
            {
                var buttons = detailPanels[(int)view - 2].GetComponentsInChildren<Button>(true);
                foreach (var button in buttons)
                    if (button.gameObject.activeInHierarchy && button.interactable)
                    {
                        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(button.gameObject);
                        break;
                    }
            }
        }

        private void SetVolume(float _) => core.Settings.SetVolumes(masterVolume.value, musicVolume.value, sfxVolume.value);

        // Existing scenes contain the original screen-space settings panel. Move it onto the TV at runtime.
        private void PlaceSettingsOnTv()
        {
            var settings = detailPanels[0];
            var root = (RectTransform)settings.transform;
            if (settings.GetComponent<Canvas>() == null)
            {
                root.SetParent(null, false);
                var canvas = settings.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = cameras.output;
                settings.AddComponent<GraphicRaycaster>();
                var background = settings.AddComponent<Image>();
                background.color = new Color(.05f, .08f, .12f);
                background.raycastTarget = false;
                if (settingsScreenPanel != null) settingsScreenPanel.SetActive(false);
            }

            FitSettingsToScreen(root);
            settings.GetComponent<Canvas>().worldCamera = cameras.output;
            AddLanguageSelector(settings.transform, settingsBack);
            tvOsd = new TvSettingsOsd(tvMenuSkin, new[] { masterVolume, musicVolume, sfxVolume },
                new[] { masterLabel, musicLabel, sfxLabel }, settingsBack, languageLabel, new[] { previousLanguage, nextLanguage });
            tvOsd.Build(root, settingsTitle, languageValue, tvInstruction);
            ShowInstruction();
            // After the language selector: its copies must land on the TV layer too.
            int layer = 0;
            while (layer < 31 && (tvScreenLayer.value & (1 << layer)) == 0) layer++;
            foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
            cameras.output.cullingMask &= ~(1 << layer);
            tvCamera.cullingMask = 1 << layer;
        }

        // The player never sees the flat canvas: it only takes the clicks. A camera renders it into a
        // texture mapped on the curved screen mesh, so the image follows the glass and the TV frame crops it.
        // The canvas is sized to the screen's edges as seen from the TV camera and the mesh UVs use that
        // same projection, so a click lands on what is drawn under it. One uniform scale keeps the content undistorted.
        private void FitSettingsToScreen(RectTransform root)
        {
            var tvView = cameras.viewpoints[(int)GarageView.Settings];
            var screen = tvScreen.transform;
            var mesh = tvScreen.sharedMesh;
            var bounds = mesh.bounds;
            var normal = screen.forward;
            var rotation = Quaternion.LookRotation(-normal, screen.up);
            float width = bounds.size.x * screen.lossyScale.x;
            float height = bounds.size.y * screen.lossyScale.y;

            var camera = cameras.output;
            float tan = Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad);
            // The screen takes most of the view, leaving a little of the TV frame around it.
            float distance = Mathf.Max(height * .5f / tan, width * .5f / (tan * camera.aspect)) / .88f;
            var front = screen.TransformPoint(new Vector3(bounds.center.x, bounds.center.y, bounds.max.z)) + normal * .005f;
            var cameraPosition = front + normal * distance;
            tvView.SetPositionAndRotation(cameraPosition, rotation);

            // Each vertex projected onto the canvas plane, in canvas axes. The vertices along the middle
            // of each edge give the visible edge of the glass, not the bounding box of its curved back.
            var vertices = mesh.vertices;
            var projected = new Vector2[vertices.Length];
            var right = rotation * Vector3.right;
            var up = rotation * Vector3.up;
            var half = Vector2.zero;
            for (int i = 0; i < vertices.Length; i++)
            {
                var ray = screen.TransformPoint(vertices[i]) - cameraPosition;
                var offset = cameraPosition + ray * (Vector3.Dot(front - cameraPosition, normal) / Vector3.Dot(ray, normal)) - front;
                projected[i] = new Vector2(Vector3.Dot(offset, right), Vector3.Dot(offset, up));
                if (Mathf.Abs(vertices[i].y - bounds.center.y) <= bounds.size.y * .15f)
                    half.x = Mathf.Max(half.x, Mathf.Abs(projected[i].x));
                if (Mathf.Abs(vertices[i].x - bounds.center.x) <= bounds.size.x * .15f)
                    half.y = Mathf.Max(half.y, Mathf.Abs(projected[i].y));
            }
            var size = half * 2f;
            float unit = TvSafeArea * Mathf.Min(size.x / SettingsLayout.x, size.y / SettingsLayout.y);
            Place(root, Vector2.zero, size / unit);
            root.SetPositionAndRotation(front, rotation);
            root.localScale = Vector3.one * unit;

            var uvs = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
                uvs[i] = new Vector2(.5f + projected[i].x / size.x, .5f + projected[i].y / size.y);
            tvMesh = Instantiate(mesh);
            tvMesh.uv = uvs;
            tvScreen.sharedMesh = tvMesh;

            var texture = new RenderTexture(TvTextureWidth, Mathf.RoundToInt(TvTextureWidth * size.y / size.x), 24)
            {
                name = "TV settings",
                antiAliasing = 4,
                wrapMode = TextureWrapMode.Clamp
            };
            tvCamera = new GameObject("TV settings camera").AddComponent<Camera>();
            tvCamera.transform.SetPositionAndRotation(front + normal * .1f, rotation);
            tvCamera.orthographic = true;
            tvCamera.orthographicSize = size.y * .5f;
            tvCamera.nearClipPlane = .01f;
            tvCamera.farClipPlane = .2f;
            tvCamera.clearFlags = CameraClearFlags.SolidColor;
            tvCamera.backgroundColor = TvSettingsOsd.Background;
            tvCamera.targetTexture = texture;
            tvCamera.enabled = false;

            tvMaterial = new Material(tvScreenMaterial) { mainTexture = texture };
            tvMaterial.SetFloat(TvPowerId, 0f);
            tvScreen.GetComponent<Renderer>().sharedMaterial = tvMaterial;
        }

        private void ShowOnTv(bool on)
        {
            if (tvCamera == null) return;
            if (tvSwitch != null) StopCoroutine(tvSwitch);
            // Switching off, the texture keeps the last picture for the tube to collapse.
            tvCamera.enabled = on;
            tvSwitch = StartCoroutine(SwitchTv(on));
        }

        private IEnumerator SwitchTv(bool on)
        {
            if (on && tvPower == 0f) yield return new WaitForSeconds(TvPowerOnDelay);
            float target = on ? 1f : 0f;
            float speed = 1f / (on ? TvPowerOnSeconds : TvPowerOffSeconds);
            while (tvPower != target)
            {
                tvPower = Mathf.MoveTowards(tvPower, target, Time.deltaTime * speed);
                tvMaterial.SetFloat(TvPowerId, tvPower);
                yield return null;
            }
            tvSwitch = null;
        }

        // "IDIOMA  < ESPAÑOL >" built from the scene's own label and VOLVER button; TvSettingsOsd lays it out.
        private void AddLanguageSelector(Transform parent, RectTransform backButton)
        {
            languageLabel = Instantiate(masterLabel, parent);
            languageLabel.name = "Text - LANGUAGE";
            languageValue = Instantiate(masterLabel, parent);
            languageValue.name = "Language value";
            previousLanguage = ArrowButton(backButton, parent, "<");
            nextLanguage = ArrowButton(backButton, parent, ">");
        }

        private static Button ArrowButton(RectTransform template, Transform parent, string arrow)
        {
            var rect = Instantiate(template, parent);
            rect.name = arrow == "<" ? "Language previous" : "Language next";
            var button = rect.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent(); // drop VOLVER's scene listener
            // VOLVER's label is localized by the scene and would write "VOLVER" back over the arrow,
            // so the copy gets a plain label of its own instead.
            var copied = rect.GetComponentInChildren<TextMeshProUGUI>(true);
            var label = new GameObject("Arrow", typeof(RectTransform), typeof(TextMeshProUGUI))
                .GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(rect, false);
            // Immediate, so the TV menu styling finds the arrow and not VOLVER's label in this same frame.
            DestroyImmediate(copied.gameObject);
            label.text = arrow;
            return button;
        }

        private void ShowInstruction() => tvInstruction.text = L10n.Get(L10n.UI, "settings.volume_hint");

        private void ConfigureVolume(Slider slider, TextMeshProUGUI label, string key)
        {
            slider.onValueChanged.AddListener(value => ShowVolume(label, key, value));
            ShowVolume(label, key, slider.value);
        }

        private static void ShowVolume(TextMeshProUGUI label, string key, float value) =>
            label.text = TvSettingsOsd.VolumeText(L10n.Get(L10n.UI, key), value);

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
