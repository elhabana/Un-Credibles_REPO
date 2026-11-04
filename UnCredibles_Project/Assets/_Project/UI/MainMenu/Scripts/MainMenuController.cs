using UnCredibles.Core;
using UnCredibles.UI.Garage;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.Localization.Components;

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
        private Button quitButton;
        private Button quitAccept;
        private Button quitCancel;
        // Title screen: only the first time the menu opens in a session, not when coming back from a party.
        private const float TitleRevealSeconds = .25f;
        private static bool titleSeen;
        private TMP_Text pressStart;
        private bool titlePressed;
        private float titleReveal = TitleRevealSeconds;
        private readonly System.Collections.Generic.List<GameObject> titleHidden = new System.Collections.Generic.List<GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetTitle() => titleSeen = false; // also with domain reload off in the editor

        private void Awake()
        {
            BuildQuit();
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
            if (!titleSeen) BuildTitle();
        }

        private void BuildTitle()
        {
            pressStart = Instantiate(settingsButton.GetComponentInChildren<TMP_Text>(true), mainPanel.transform.parent);
            var localized = pressStart.GetComponent<LocalizeStringEvent>();
            if (localized != null) Destroy(localized);
            pressStart.name = "Press Start";
            pressStart.raycastTarget = false;
            pressStart.text = UnCredibles.Localization.L10n.Get(UnCredibles.Localization.L10n.UI, "menu.press_start");
            pressStart.enableAutoSizing = false;
            pressStart.fontSize = 60;
            pressStart.alignment = TextAlignmentOptions.Center;
            var rect = pressStart.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(0f, -((RectTransform)mainPanel.transform.parent).rect.height * .3f);
            rect.sizeDelta = new Vector2(1000f, 90f);
            // The logo stays; only the home buttons wait (ACEPTAR / CANCELAR stay closed).
            foreach (Transform child in mainPanel.transform)
                if (child.GetComponent<Button>() != null && child.gameObject.activeSelf) titleHidden.Add(child.gameObject);
            InputSystem.onAnyButtonPress.CallOnce(_ => titlePressed = true);
        }

        // Keeps the buttons hidden and the prompt blinking until any key, click or pad button.
        // The buttons appear a moment later so that same press does not also click JUGAR LOCAL.
        private bool UpdateTitle()
        {
            foreach (var hidden in titleHidden) hidden.SetActive(false);
            if (titlePressed) titleReveal -= Time.unscaledDeltaTime;
            float pop = titlePressed ? 1f + (1f - titleReveal / TitleRevealSeconds) * .3f : 1f;
            pressStart.transform.localScale = new Vector3(pop, pop, 1f);
            var color = pressStart.color;
            color.a = titlePressed ? Mathf.Clamp01(titleReveal / TitleRevealSeconds) : .6f + .4f * Mathf.Sin(Time.unscaledTime * 4f);
            pressStart.color = color;
            if (titleReveal > 0f) return true;

            titleSeen = true;
            Destroy(pressStart.gameObject);
            pressStart = null;
            foreach (var hidden in titleHidden) hidden.SetActive(true);
            ShowMultiplayerSetup(false); // shows the buttons with JUGAR LOCAL selected
            return false;
        }

        private void Update()
        {
            if (pressStart != null && UpdateTitle()) return;
            if (multiplayerSetupPanel.activeInHierarchy &&
                ((Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame) ||
                 (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)))
                CloseMultiplayerSetup();
            UpdateQuitConfirm();
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

        // SALIR goes under the last home button, a copy of AJUSTES. Pressing it shows ACEPTAR and CANCELAR
        // right next to it, joined to its right edge and as tall as it.
        private void BuildQuit()
        {
            var template = (RectTransform)settingsButton.transform;
            float lowest = template.anchoredPosition.y;
            float step = 0f;
            foreach (Transform child in mainPanel.transform)
            {
                if (child.GetComponent<Button>() == null) continue;
                float y = ((RectTransform)child).anchoredPosition.y;
                if (y < lowest) lowest = y;
                if (y < template.anchoredPosition.y && (step == 0f || template.anchoredPosition.y - y < step))
                    step = template.anchoredPosition.y - y;
            }
            if (step == 0f) step = template.rect.height + 25f;

            quitButton = CopyButton(template, "SALIR", "menu.quit", new Vector2(template.anchoredPosition.x, lowest - step), template.sizeDelta);
            quitButton.onClick.AddListener(() => ShowQuitConfirm(true));
            var size = new Vector2(template.sizeDelta.x * .5f, template.sizeDelta.y);
            float right = template.anchoredPosition.x + template.sizeDelta.x * .5f;
            float y0 = lowest - step;
            quitAccept = CopyButton(template, "ACEPTAR", "menu.quit_accept", new Vector2(right + size.x * .5f, y0), size);
            quitAccept.onClick.AddListener(Quit);
            quitAccept.gameObject.AddComponent<SelectionHighlight.KeepSize>();
            quitCancel = CopyButton(template, "CANCELAR", "menu.quit_cancel", new Vector2(right + size.x * 1.5f, y0), size);
            quitCancel.onClick.AddListener(() => ShowQuitConfirm(false));
            quitCancel.gameObject.AddComponent<SelectionHighlight.KeepSize>();
            ShowQuitConfirm(false);
        }

        private Button CopyButton(RectTransform template, string objectName, string key, Vector2 position, Vector2 size)
        {
            var rect = Instantiate(template, template.parent);
            rect.name = objectName;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var button = rect.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent(); // drop AJUSTES's scene listeners
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            // The copy keeps AJUSTES's localized label: point it at its own entry.
            rect.GetComponentInChildren<LocalizeStringEvent>(true).StringReference.SetReference("UI", key);
            var label = rect.GetComponentInChildren<TMP_Text>(true);
            label.rectTransform.sizeDelta = new Vector2(size.x - 10f, label.rectTransform.sizeDelta.y);
            label.fontSizeMax = label.fontSize;
            label.enableAutoSizing = true;
            return button;
        }

        private void ShowQuitConfirm(bool show)
        {
            quitAccept.gameObject.SetActive(show);
            quitCancel.gameObject.SetActive(show);
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
                EventSystem.current.SetSelectedGameObject(show ? quitCancel.gameObject : quitButton.gameObject);
        }

        // Back (B / Esc) or moving to another home button closes the confirmation.
        private void UpdateQuitConfirm()
        {
            if (!quitCancel.gameObject.activeSelf) return;
            if ((Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame) ||
                (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame))
            {
                ShowQuitConfirm(false);
                return;
            }
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null && selected != quitButton.gameObject && selected != quitAccept.gameObject &&
                selected != quitCancel.gameObject)
            {
                quitAccept.gameObject.SetActive(false);
                quitCancel.gameObject.SetActive(false);
            }
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
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
