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
        private CoreRoot core;
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
            ConfigureVolume(masterVolume, "GENERAL");
            ConfigureVolume(musicVolume, "MUSICA");
            ConfigureVolume(sfxVolume, "EFECTOS");
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
        }

        private void HandleLanguageChanged()
        {
            ShowVolume(masterVolume, "GENERAL", masterVolume.value);
            ShowVolume(musicVolume, "MUSICA", musicVolume.value);
            ShowVolume(sfxVolume, "EFECTOS", sfxVolume.value);
            ShowLanguage();
            AddInstruction(detailPanels[0].transform);
        }

        private void ShowLanguage()
        {
            languageLabel.text = L10n.Get(L10n.UI, "settings.language");
            languageValue.text = L10n.LanguageName(L10n.CurrentLanguage).ToUpperInvariant();
        }

        private void Update()
        {
            if (cameras.View < GarageView.Settings) return;
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
            var tvView = cameras.viewpoints[(int)GarageView.Settings];
            var cameraPosition = new Vector3(3f, 1.9f, 3.45f);
            tvView.SetPositionAndRotation(cameraPosition,
                Quaternion.LookRotation(new Vector3(3f, 1.9f, 4.75f) - cameraPosition));
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
                settings.transform.Find("Panel").gameObject.SetActive(false);
            }

            Place(root, Vector2.zero, new Vector2(170, 100));
            root.SetPositionAndRotation(new Vector3(3f, 1.9f, 4.72f), Quaternion.identity);
            root.localScale = Vector3.one * .01f;
            settings.GetComponent<Canvas>().worldCamera = cameras.output;
            var title = settings.transform.Find("Text - AJUSTES").GetComponent<TextMeshProUGUI>();
            Place(title.rectTransform, new Vector2(-15, 38), new Vector2(115, 14));
            title.fontSize = 9;

            var back = (RectTransform)settings.transform.Find("VOLVER");
            Place(back, new Vector2(62, 38), new Vector2(38, 15));
            var backLabel = back.GetComponentInChildren<TextMeshProUGUI>(true);
            Place(backLabel.rectTransform, Vector2.zero, new Vector2(36, 14));
            backLabel.fontSize = 5;

            // Rows are kept short so the language selector fits under the three volumes.
            PlaceVolume(masterVolume, "GENERAL", 26);
            PlaceVolume(musicVolume, "MUSICA", 11);
            PlaceVolume(sfxVolume, "EFECTOS", -4);
            AddLanguageSelector(settings.transform, back);
            AddInstruction(settings.transform);
        }

        // "<  ESPAÑOL  >" built from the scene's own label and VOLVER button, so it looks like the rest of the TV.
        private void AddLanguageSelector(Transform parent, RectTransform backButton)
        {
            var source = detailPanels[0].transform.Find("Text - GENERAL").GetComponent<TextMeshProUGUI>();
            languageLabel = Instantiate(source, parent);
            languageLabel.name = "Text - LANGUAGE";
            Place(languageLabel.rectTransform, new Vector2(0, -19), new Vector2(120, 8));
            languageValue = Instantiate(source, parent);
            languageValue.name = "Language value";
            languageValue.alignment = TextAlignmentOptions.Center;
            languageValue.fontStyle = FontStyles.Bold;
            Place(languageValue.rectTransform, new Vector2(0, -28), new Vector2(70, 9));
            previousLanguage = ArrowButton(backButton, parent, "<", -45);
            nextLanguage = ArrowButton(backButton, parent, ">", 45);
        }

        private static Button ArrowButton(RectTransform template, Transform parent, string arrow, float x)
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
            label.font = copied.font;
            label.color = copied.color;
            label.raycastTarget = false;
            Destroy(copied.gameObject);
            label.text = arrow;
            label.fontSize = 7;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            Place(rect, new Vector2(x, -28), new Vector2(16, 10));
            Place(label.rectTransform, Vector2.zero, new Vector2(16, 10));
            return button;
        }

        private static void AddInstruction(Transform parent)
        {
            var instruction = parent.Find("TV instruction");
            if (instruction == null)
            {
                var created = new GameObject("TV instruction", typeof(RectTransform), typeof(TextMeshProUGUI));
                created.transform.SetParent(parent, false);
                instruction = created.transform;
            }
            var text = instruction.GetComponent<TextMeshProUGUI>();
            Place(text.rectTransform, new Vector2(0, -46), new Vector2(160, 8));
            text.text = L10n.Get(L10n.UI, "settings.volume_hint");
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = 4;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
        }

        private void ConfigureVolume(Slider slider, string label)
        {
            slider.GetComponent<Image>().color = new Color(.28f, .37f, .45f);
            slider.handleRect.GetComponent<Image>().color = new Color(.98f, .62f, .14f);
            slider.onValueChanged.AddListener(value => ShowVolume(slider, label, value));
            ShowVolume(slider, label, slider.value);
        }

        private void ShowVolume(Slider slider, string label, float value)
        {
            var text = detailPanels[0].transform.Find("Text - " + label).GetComponent<TextMeshProUGUI>();
            text.text = VolumeLabel(label) + "  " + Mathf.RoundToInt(value * 100f) + "%";
        }

        // The label is also part of the scene object name ("Text - GENERAL"), so only the shown text is translated.
        private static string VolumeLabel(string label) => label switch
        {
            "GENERAL" => L10n.Get(L10n.UI, "settings.master"),
            "MUSICA" => L10n.Get(L10n.UI, "settings.music"),
            "EFECTOS" => L10n.Get(L10n.UI, "settings.sfx"),
            _ => label,
        };

        private void PlaceVolume(Slider slider, string label, float y)
        {
            var text = detailPanels[0].transform.Find("Text - " + label).GetComponent<TextMeshProUGUI>();
            Place(text.rectTransform, new Vector2(0, y), new Vector2(120, 8));
            text.fontSize = 5;
            Place((RectTransform)slider.transform, new Vector2(0, y - 7), new Vector2(125, 3.5f));
            Place(slider.handleRect, Vector2.zero, new Vector2(5, 6));
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
