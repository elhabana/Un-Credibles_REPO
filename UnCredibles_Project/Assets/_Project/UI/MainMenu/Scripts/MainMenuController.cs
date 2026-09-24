using UnCredibles.Core;
using UnityEngine;
using UnityEngine.UI;

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

        private void Awake() => ShowMultiplayerSetup(false);

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

        // Online rooms come after the local flow works (architecture guide, steps 13-14).
        private void CreateRoom() => Debug.Log("Create Room is not available yet.", this);
        private void JoinRoom() => Debug.Log("Join Room is not available yet.", this);
        private void OpenSettings() => Debug.Log("Settings menu is not available yet.", this);

        private void ShowMultiplayerSetup(bool show)
        {
            mainPanel.SetActive(!show);
            multiplayerSetupPanel.SetActive(show);
        }
    }
}
