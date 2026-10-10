using UnCredibles.Core;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace UnCredibles.UI.MainMenu
{
    public sealed class MainMenuController : MonoBehaviour
    {
        public event System.Action SettingsRequested;
        [SerializeField] private Button singleplayerButton;
        [SerializeField] private Button multiplayerButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject multiplayerSetupPanel;
        [SerializeField] private Button createRoomButton;
        [SerializeField] private Button joinRoomButton;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_InputField roomCodeInput;
        [SerializeField] private TMP_Text connectionStatus;
        private bool enteringOnline;

        private void Awake()
        {
            // Reuse the existing canvas and font; no separate debug overlay in the menu.
            if (connectionStatus == null)
            {
                connectionStatus = Instantiate(createRoomButton.GetComponentInChildren<TMP_Text>(), multiplayerSetupPanel.transform);
                connectionStatus.name = "Connection Status";
                connectionStatus.raycastTarget = false;
                var rect = connectionStatus.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0, -110);
                rect.sizeDelta = new Vector2(600, 80);
                connectionStatus.fontSize = 20;
            }
            connectionStatus.text = "";
            ShowMultiplayerSetup(false);
        }

        private void Update()
        {
            if (multiplayerSetupPanel.activeInHierarchy &&
                ((Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame) ||
                 (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)))
                CloseMultiplayerSetup();
            var core = CoreRoot.Instance;
            if (core == null) return;
            bool available = core.Online.CanConnect && !core.Scenes.IsLoading;
            singleplayerButton.interactable = multiplayerButton.interactable = available;
            createRoomButton.interactable = available;
            joinRoomButton.interactable = available && !string.IsNullOrWhiteSpace(roomCodeInput.text);
            roomCodeInput.interactable = available;
            backButton.interactable = available;
            connectionStatus.text = core.Online.Status;
            if (available && mainPanel.activeInHierarchy && EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == null) FocusMain();
            if (enteringOnline && core.Online.IsConnected && !core.Scenes.IsLoading)
            {
                enteringOnline = false;
                core.OpenPartyLobby(SessionMode.Online);
            }
            else if (enteringOnline && core.Online.CanConnect) enteringOnline = false;
        }

        private void OnEnable()
        {
            singleplayerButton.onClick.AddListener(OpenSingleplayer);
            multiplayerButton.onClick.AddListener(OpenMultiplayerSetup);
            settingsButton.onClick.AddListener(OpenSettings);
            createRoomButton.onClick.AddListener(CreateRoom);
            joinRoomButton.onClick.AddListener(JoinRoom);
            backButton.onClick.AddListener(CloseMultiplayerSetup);
        }

        private void OnDisable()
        {
            singleplayerButton.onClick.RemoveListener(OpenSingleplayer);
            multiplayerButton.onClick.RemoveListener(OpenMultiplayerSetup);
            settingsButton.onClick.RemoveListener(OpenSettings);
            createRoomButton.onClick.RemoveListener(CreateRoom);
            joinRoomButton.onClick.RemoveListener(JoinRoom);
            backButton.onClick.RemoveListener(CloseMultiplayerSetup);
        }

        private void OpenSingleplayer() => CoreRoot.Instance.OpenPartyLobby(SessionMode.Local);

        private void OpenMultiplayerSetup() => ShowMultiplayerSetup(true);
        private void CloseMultiplayerSetup() => ShowMultiplayerSetup(false);

        private void CreateRoom()
        {
            if (!CoreRoot.Instance.Online.CanConnect) return;
            enteringOnline = true;
            CoreRoot.Instance.Online.CreateHost();
        }
        private void JoinRoom()
        {
            if (!CoreRoot.Instance.Online.CanConnect || string.IsNullOrWhiteSpace(roomCodeInput.text)) return;
            enteringOnline = true;
            CoreRoot.Instance.Online.JoinHost(roomCodeInput.text);
        }
        private void OpenSettings() => SettingsRequested?.Invoke();

        public void ResetView() => ShowMultiplayerSetup(false);

        public void FocusMain()
        {
            if (EventSystem.current != null && singleplayerButton.interactable)
                EventSystem.current.SetSelectedGameObject(singleplayerButton.gameObject);
        }

        private void ShowMultiplayerSetup(bool show)
        {
            mainPanel.SetActive(!show);
            multiplayerSetupPanel.SetActive(show);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(show ? createRoomButton.gameObject : singleplayerButton.gameObject);
        }
    }
}
