using System.Collections;
using TMPro;
using UnCredibles.Core;
using UnCredibles.UI.MainMenu;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

        private IEnumerator Start()
        {
            PlaceSettingsOnTv();
            while (CoreRoot.Instance == null) yield return null;
            core = CoreRoot.Instance;
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
            StateChanged(core.GameFlow.State, core.GameFlow.State);
        }

        private void OnDestroy()
        {
            if (core != null) core.GameFlow.StateChanged -= StateChanged;
            if (menu != null) menu.SettingsRequested -= ShowSettings;
        }

        private void StateChanged(GameState previous, GameState current)
        {
            if (current == GameState.PartyLobby) Show(GarageView.Lobby);
            else if (current == GameState.MainMenu || current == GameState.Boot)
            {
                Show(GarageView.Home);
                menu.ResetView();
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

            PlaceVolume(masterVolume, "GENERAL", 21);
            PlaceVolume(musicVolume, "MUSICA", -2);
            PlaceVolume(sfxVolume, "EFECTOS", -25);
            AddInstruction(settings.transform);
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
            text.text = "CLIC O ARRASTRA PARA CAMBIAR EL VOLUMEN";
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
            var text = detailPanels[0].transform.Find("Text - " + label).GetComponent<TextMeshProUGUI>();
            void ShowValue(float value) => text.text = label + "  " + Mathf.RoundToInt(value * 100f) + "%";
            slider.onValueChanged.AddListener(ShowValue);
            ShowValue(slider.value);
        }

        private void PlaceVolume(Slider slider, string label, float y)
        {
            var text = detailPanels[0].transform.Find("Text - " + label).GetComponent<TextMeshProUGUI>();
            Place(text.rectTransform, new Vector2(0, y), new Vector2(120, 10));
            text.fontSize = 6;
            Place((RectTransform)slider.transform, new Vector2(0, y - 10), new Vector2(125, 5));
            Place(slider.handleRect, Vector2.zero, new Vector2(6, 8));
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
