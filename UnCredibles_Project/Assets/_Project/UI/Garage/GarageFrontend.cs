using System.Collections;
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
            while (CoreRoot.Instance == null) yield return null;
            core = CoreRoot.Instance;
            core.GameFlow.StateChanged += StateChanged;
            menu.SettingsRequested += ShowSettings;
            masterVolume.SetValueWithoutNotify(core.Settings.MasterVolume);
            musicVolume.SetValueWithoutNotify(core.Settings.MusicVolume);
            sfxVolume.SetValueWithoutNotify(core.Settings.SfxVolume);
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
    }
}
