using UnCredibles.Core;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UnCredibles.UI.MainMenu
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button singleplayerButton;
        [SerializeField] private Button multiplayerButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject multiplayerSetupPanel;
        [SerializeField] private Button createRoomButton;
        [SerializeField] private Button joinRoomButton;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_InputField roomCodeInput;
        private TMP_Text connectionStatus;
        private bool enteringOnline;

        private void Awake()
        {
            // Reuse the existing canvas and font; no separate debug overlay in the menu.
            connectionStatus = Instantiate(createRoomButton.GetComponentInChildren<TMP_Text>(), multiplayerSetupPanel.transform);
            connectionStatus.name = "Connection Status";
            connectionStatus.raycastTarget = false;
            var rect = connectionStatus.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0, -110);
            rect.sizeDelta = new Vector2(600, 80);
            connectionStatus.fontSize = 20;
            connectionStatus.text = "";
            ShowMultiplayerSetup(false);
        }

        private void Update()
        {
            var core = CoreRoot.Instance;
            if (core == null) return;
            bool available = core.Online.CanConnect && !core.Scenes.IsLoading;
            singleplayerButton.interactable = multiplayerButton.interactable = available;
            createRoomButton.interactable = available;
            joinRoomButton.interactable = available && !string.IsNullOrWhiteSpace(roomCodeInput.text);
            roomCodeInput.interactable = available;
            backButton.interactable = available;
            connectionStatus.text = core.Online.Status;
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
        private void OpenSettings() => Debug.Log("Settings menu is not available yet.", this);

        private void ShowMultiplayerSetup(bool show)
        {
            mainPanel.SetActive(!show);
            multiplayerSetupPanel.SetActive(show);
        }
    }
}
