using UnCredibles.Core;
using UnCredibles.Minigames;
using UnCredibles.UI.Garage;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

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
        // The confirmation sheet replaces the quit button while open.
        [Header("Quit")]
        [SerializeField] private Button quitButton;
        [SerializeField] private Button quitAccept;
        [SerializeField] private Button quitCancel;
        [SerializeField] private GameObject quitSheet;
        [SerializeField] private TMP_Text quitTitle;
        [Header("Title screen")]
        [SerializeField] private TMP_Text pressStart;
        [Tooltip("Slid in from the left after the title screen, in this order: the comic panel, then the options.")]
        [SerializeField] private RectTransform[] introSlide;
        private bool enteringOnline;
        // Title screen: only the first time the menu opens in a session, not when coming back from a party.
        private const float TitleRevealSeconds = .25f;
        private const float RevealEdgePixels = 12f;
        private static bool titleSeen;
        private bool titleActive;
        private bool titlePressed;
        private bool waitingForEdge;
        private System.IDisposable titleInput;
        private float titleReveal = TitleRevealSeconds;
        private readonly System.Collections.Generic.List<GameObject> titleHidden = new System.Collections.Generic.List<GameObject>();
        // Intro after the press: the camera punches in and back, then the buttons slide in from the left.
        private const float ZoomSeconds = .7f;
        private const float ZoomFov = 7f;          // degrees narrower at the peak
        private const float SlideSeconds = .45f;
        private const float SlideStagger = .07f;   // between one button and the next
        private Camera zoomCamera;
        private float zoomBaseFov;
        private float zoomTime = -1f;              // < 0 = not zooming
        private float slideTime = -1f;             // < 0 = not sliding
        private float slideOffset;
        private readonly System.Collections.Generic.List<float> slideTargets = new System.Collections.Generic.List<float>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetTitle() => titleSeen = false; // also with domain reload off in the editor

        private void Awake()
        {
            connectionStatus.text = "";
            ShowQuitConfirm(false);
            ShowMultiplayerSetup(false);
            pressStart.gameObject.SetActive(false);
            foreach (var rect in introSlide)
                if (rect != null && rect.gameObject.activeSelf) titleHidden.Add(rect.gameObject);
            if (!titleSeen) StartTitle();
        }

        private void StartTitle()
        {
            titleActive = true;
            pressStart.text = UnCredibles.Localization.L10n.Get(UnCredibles.Localization.L10n.UI, "menu.press_start");
            pressStart.gameObject.SetActive(true);
            // The logo stays; only the panel and the home options wait (ACEPTAR / CANCELAR stay closed).
            foreach (var hidden in titleHidden) hidden.SetActive(false);
            ListenForTitlePress();
        }

        private void ListenForTitlePress() =>
            titleInput = InputSystem.onAnyButtonPress.CallOnce(_ => titlePressed = true);

        private void UpdateTitle()
        {
            if (titlePressed && titleReveal >= TitleRevealSeconds) StartZoom();
            if (titlePressed) titleReveal -= Time.unscaledDeltaTime;
            float pop = titlePressed ? 1f + (1f - titleReveal / TitleRevealSeconds) * .3f : 1f;
            pressStart.transform.localScale = new Vector3(pop, pop, 1f);
            var color = pressStart.color;
            color.a = titlePressed ? Mathf.Clamp01(titleReveal / TitleRevealSeconds) : .6f + .4f * Mathf.Sin(Time.unscaledTime * 4f);
            pressStart.color = color;
            if (titleReveal > 0f) return;

            titleSeen = true;
            titleActive = false;
            pressStart.gameObject.SetActive(false);
            waitingForEdge = true;
        }

        private bool TryRevealMenu()
        {
            var mouse = Mouse.current;
            if (!Application.isFocused || mouse == null || mouse.delta.ReadValue() == Vector2.zero) return false;
            var pointer = mouse.position.ReadValue();
            if (pointer.x < 0f || pointer.x > RevealEdgePixels || pointer.y < 0f || pointer.y >= Screen.height)
                return false;

            waitingForEdge = false;
            GameAudio.Play(GameSfx.UiMove);
            foreach (var hidden in titleHidden) hidden.SetActive(true);
            ShowMultiplayerSetup(false); // shows the buttons with JUGAR LOCAL selected
            StartSlide();
            return true;
        }

        private void StartZoom()
        {
            var rig = FindFirstObjectByType<GarageCameraRig>();
            zoomCamera = rig != null ? rig.output : Camera.main;
            if (zoomCamera == null || zoomCamera.orthographic) return;
            zoomBaseFov = zoomCamera.fieldOfView;
            zoomTime = 0f;
        }

        private void StartSlide()
        {
            slideOffset = ((RectTransform)mainPanel.transform.parent).rect.width * .6f;
            slideTargets.Clear();
            foreach (var hidden in titleHidden)
            {
                var rect = (RectTransform)hidden.transform;
                slideTargets.Add(rect.anchoredPosition.x);
                rect.anchoredPosition -= new Vector2(slideOffset, 0f);
            }
            slideTime = 0f;
            BlockInput(true);
        }

        // No clicks or pad presses until every button has landed. Not CanvasGroup.interactable:
        // that would paint the buttons with their disabled tint while they slide in.
        private void BlockInput(bool block)
        {
            var group = mainPanel.GetComponent<CanvasGroup>();
            if (group == null) group = mainPanel.AddComponent<CanvasGroup>();
            group.blocksRaycasts = !block;
            if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = !block;
        }

        private void UpdateIntro()
        {
            float dt = Time.unscaledDeltaTime;
            if (zoomTime >= 0f)
            {
                zoomTime += dt;
                float x = Mathf.Clamp01(zoomTime / ZoomSeconds);
                // Quick punch in, softer way back; ends exactly on the original FOV.
                float punch = x < .35f ? Mathf.SmoothStep(0f, 1f, x / .35f) : Mathf.SmoothStep(1f, 0f, (x - .35f) / .65f);
                if (zoomCamera != null) zoomCamera.fieldOfView = zoomBaseFov - ZoomFov * punch;
                if (x >= 1f) zoomTime = -1f;
            }
            if (slideTime < 0f) return;
            slideTime += dt;
            bool done = true;
            for (int i = 0; i < titleHidden.Count; i++)
            {
                if (titleHidden[i] == null) continue;
                float x = Mathf.Clamp01((slideTime - i * SlideStagger) / SlideSeconds);
                if (x < 1f) done = false;
                var rect = (RectTransform)titleHidden[i].transform;
                rect.anchoredPosition = new Vector2(slideTargets[i] - slideOffset * (1f - EaseOutBack(x)), rect.anchoredPosition.y);
            }
            if (!done) return;
            slideTime = -1f;
            BlockInput(false);
        }

        // Overshoots a little before settling: the buttons land with a small bounce.
        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        private void Update()
        {
            UpdateIntro();
            if (titleActive)
            {
                UpdateTitle();
                return;
            }
            if (waitingForEdge && !TryRevealMenu()) return;
            if (mainPanel.activeInHierarchy && slideTime < 0f &&
                ((!quitSheet.activeSelf && BackPressed()) || ClickedOutsideMenu()))
            {
                ShowQuitConfirm(false);
                waitingForEdge = true;
                ShowMultiplayerSetup(false);
                GameAudio.Play(GameSfx.UiBack);
                return;
            }
            if (multiplayerSetupPanel.activeInHierarchy &&
                BackPressed())
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

        private static bool BackPressed() =>
            (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame) ||
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame);

        private bool ClickedOutsideMenu()
        {
            var mouse = Mouse.current;
            var events = EventSystem.current;
            if (mouse == null || events == null || !mouse.leftButton.wasPressedThisFrame) return false;
            var position = mouse.position.ReadValue();
            if (!new Rect(0f, 0f, Screen.width, Screen.height).Contains(position) ||
                RectTransformUtility.RectangleContainsScreenPoint(introSlide[0], position)) return false;

            var hits = new System.Collections.Generic.List<RaycastResult>();
            events.RaycastAll(new PointerEventData(events) { position = position }, hits);
            return !hits.Exists(hit => hit.gameObject.transform.IsChildOf(mainPanel.transform));
        }

        private void OnEnable()
        {
            if (titleActive && !titlePressed && titleInput == null) ListenForTitlePress();
            singleplayerButton.onClick.AddListener(OpenSingleplayer);
            multiplayerButton.onClick.AddListener(OpenMultiplayerSetup);
            settingsButton.onClick.AddListener(OpenSettings);
            createRoomButton.onClick.AddListener(CreateRoom);
            joinRoomButton.onClick.AddListener(JoinRoom);
            backButton.onClick.AddListener(CloseMultiplayerSetup);
            quitButton.onClick.AddListener(OpenQuitConfirm);
            quitAccept.onClick.AddListener(Quit);
            quitCancel.onClick.AddListener(CancelQuit);
        }

        private void OnDisable()
        {
            titleInput?.Dispose();
            titleInput = null;
            if (zoomTime >= 0f && zoomCamera != null) zoomCamera.fieldOfView = zoomBaseFov; // never leave it zoomed
            zoomTime = -1f;
            if (slideTime >= 0f) { slideTime = SlideSeconds + titleHidden.Count * SlideStagger; UpdateIntro(); } // snap into place
            singleplayerButton.onClick.RemoveListener(OpenSingleplayer);
            multiplayerButton.onClick.RemoveListener(OpenMultiplayerSetup);
            settingsButton.onClick.RemoveListener(OpenSettings);
            createRoomButton.onClick.RemoveListener(CreateRoom);
            joinRoomButton.onClick.RemoveListener(JoinRoom);
            backButton.onClick.RemoveListener(CloseMultiplayerSetup);
            quitButton.onClick.RemoveListener(OpenQuitConfirm);
            quitAccept.onClick.RemoveListener(Quit);
            quitCancel.onClick.RemoveListener(CancelQuit);
        }

        private void OpenSingleplayer()
        {
            GameAudio.Play(GameSfx.UiConfirm);
            CoreRoot.Instance.OpenPartyLobby(SessionMode.Local);
        }

        private void OpenMultiplayerSetup()
        {
            GameAudio.Play(GameSfx.UiConfirm);
            ShowMultiplayerSetup(true);
        }

        private void CloseMultiplayerSetup()
        {
            GameAudio.Play(GameSfx.UiBack);
            ShowMultiplayerSetup(false);
        }

        private void CreateRoom()
        {
            bool can = CoreRoot.Instance.Online.CanConnect;
            GameAudio.Play(can ? GameSfx.UiConfirm : GameSfx.UiError);
            if (!can) return;
            enteringOnline = true;
            CoreRoot.Instance.Online.CreateHost();
        }
        private void JoinRoom()
        {
            bool can = CoreRoot.Instance.Online.CanConnect && !string.IsNullOrWhiteSpace(roomCodeInput.text);
            GameAudio.Play(can ? GameSfx.UiConfirm : GameSfx.UiError);
            if (!can) return;
            enteringOnline = true;
            CoreRoot.Instance.Online.JoinHost(roomCodeInput.text);
        }
        private void OpenSettings()
        {
            GameAudio.Play(GameSfx.UiConfirm);
            SettingsRequested?.Invoke();
        }

        public void ResetView()
        {
            ShowQuitConfirm(false);
            ShowMultiplayerSetup(false);
        }

        public void FocusMain()
        {
            if (!titleActive && !waitingForEdge && EventSystem.current != null && singleplayerButton.interactable)
                EventSystem.current.SetSelectedGameObject(singleplayerButton.gameObject);
        }

        private void OpenQuitConfirm()
        {
            GameAudio.Play(GameSfx.UiConfirm);
            ShowQuitConfirm(true);
        }

        private void CancelQuit()
        {
            GameAudio.Play(GameSfx.UiBack);
            ShowQuitConfirm(false);
        }

        private void ShowQuitConfirm(bool show)
        {
            var events = EventSystem.current;
            var selected = events != null ? events.currentSelectedGameObject : null;
            bool wasConfirming = selected == quitAccept.gameObject || selected == quitCancel.gameObject;
            quitButton.gameObject.SetActive(!show && !titleActive && !waitingForEdge);
            quitAccept.gameObject.SetActive(show);
            quitCancel.gameObject.SetActive(show);
            quitSheet.SetActive(show);
            if (show) FitQuitTitle();
            if (events != null && (show || wasConfirming))
                events.SetSelectedGameObject(show ? quitCancel.gameObject : quitButton.gameObject);
        }

        // Back (B / Esc) or moving to another home button closes the confirmation.
        private void UpdateQuitConfirm()
        {
            if (!quitCancel.gameObject.activeSelf) return;
            if (BackPressed())
            {
                CancelQuit();
                return;
            }
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null && selected != quitAccept.gameObject && selected != quitCancel.gameObject)
                ShowQuitConfirm(false);
        }

        // The tab reads like the SALIR sign, in the current language, and fits its text.
        private void FitQuitTitle()
        {
            string text = UnCredibles.Localization.L10n.Get(UnCredibles.Localization.L10n.UI, "menu.quit");
            quitTitle.text = text;
            var tab = (RectTransform)quitTitle.transform.parent;
            tab.sizeDelta = new Vector2(quitTitle.GetPreferredValues(text).x + 40f, tab.sizeDelta.y);
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
            mainPanel.SetActive(!show && !waitingForEdge);
            multiplayerSetupPanel.SetActive(show);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(show ? createRoomButton.gameObject :
                    titleActive || waitingForEdge ? null : singleplayerButton.gameObject);
        }
    }
}
