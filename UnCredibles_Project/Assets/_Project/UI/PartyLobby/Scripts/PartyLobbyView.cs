using TMPro;
using UnCredibles.BatPad;
using UnCredibles.Players;
using UnityEngine;
using UnityEngine.UI;
using UnCredibles.Localization;

namespace UnCredibles.UI.PartyLobby
{
    // Renders the lobby. Refreshes only when the slots change, never every frame.
    // Draws controller.Slots, which is the same data offline, on the host and on online clients.
    public sealed class PartyLobbyView : MonoBehaviour
    {
        [SerializeField] private PartyLobbyController controller;
        [SerializeField] private LobbySlotView[] slotViews = new LobbySlotView[PlayerRegistry.MaxPlayers];
        [SerializeField] private TMP_Text modeText;
        [SerializeField] private TMP_Text countdownText;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private Button backButton;
        [Header("BatPad")]
        [SerializeField] private RawImage batPadQr;
        [SerializeField] private TMP_Text batPadText;

        private const float CalloutBlinkSpeed = 2.5f;

        private bool locked;
        private Texture2D batPadQrTexture;
        private TMP_Text addPlayerCallout; // blinking "another player? gamepad or QR" above the QR
        // The small QR is replaced by a "connect with your phone" button that opens it big.
        private Button phoneButton;
        private GameObject phoneOverlay;
        private RawImage bigQr;
        private TMP_Text phoneButtonLabel, phoneScanText, phoneCloseLabel;

        private void Awake()
        {
            for (int i = 0; i < slotViews.Length; i++) slotViews[i].Setup(i);
            countdownText.gameObject.SetActive(false);
            CreateAddPlayerCallout();
            CreatePhonePanel();
            RenderBatPad();
        }

        private void Update()
        {
            if (!addPlayerCallout.gameObject.activeSelf) return;
            addPlayerCallout.alpha = Mathf.Lerp(0.35f, 1f, Mathf.PingPong(Time.unscaledTime * CalloutBlinkSpeed, 1f));
        }

        // Built from the QR caption at runtime so the shared lobby scene does not need a new object.
        private void CreateAddPlayerCallout()
        {
            addPlayerCallout = Instantiate(batPadText, batPadText.transform.parent);
            addPlayerCallout.name = "Add Player Callout";
            var qrRect = (RectTransform)batPadQr.transform;
            var rect = addPlayerCallout.rectTransform;
            rect.anchorMin = rect.anchorMax = qrRect.anchorMin;
            // Three short lines (question, gamepad, QR) read the same in every language and stay
            // narrow enough not to leave the screen next to the QR.
            rect.sizeDelta = new Vector2(340f, 120f);
            rect.anchoredPosition = qrRect.anchoredPosition + new Vector2(0f, (qrRect.sizeDelta.y + rect.sizeDelta.y) * 0.5f + 8f);
            addPlayerCallout.fontSize = batPadText.fontSize * 1.2f;
            addPlayerCallout.lineSpacing = -10f;
            addPlayerCallout.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            addPlayerCallout.alignment = TextAlignmentOptions.Center;
            addPlayerCallout.text = L10n.Get(L10n.UI, "lobby.add_player");
            addPlayerCallout.gameObject.SetActive(false);
        }

        // Only while someone can still join here: the QR is up, a slot is free and the match is not starting.
        private void RenderAddPlayerCallout(int occupied)
        {
            bool show = batPadQrTexture != null && !locked && occupied < PlayerRegistry.MaxPlayers;
            addPlayerCallout.gameObject.SetActive(show);
            phoneButton.gameObject.SetActive(show);
            if (!show) phoneOverlay.SetActive(false);
        }

        // Built at runtime, like the callout: a button where the small QR was, and a dark panel
        // with the QR big enough to scan from the sofa.
        private void CreatePhonePanel()
        {
            var parent = batPadQr.transform.parent;
            var qrRect = (RectTransform)batPadQr.transform;

            phoneButton = PlainButton(parent, out phoneButtonLabel, 24f);
            var buttonRect = (RectTransform)phoneButton.transform;
            buttonRect.anchorMin = buttonRect.anchorMax = qrRect.anchorMin;
            buttonRect.sizeDelta = new Vector2(330f, 76f);
            buttonRect.anchoredPosition = qrRect.anchoredPosition + new Vector2(-50f, 0f); // its text fits on screen
            phoneButton.onClick.AddListener(() =>
            {
                phoneOverlay.transform.SetAsLastSibling(); // above the cards and the outfit arrows
                phoneOverlay.SetActive(true);
            });

            phoneOverlay = new GameObject("Phone QR Panel", typeof(RectTransform), typeof(Image));
            var overlay = (RectTransform)phoneOverlay.transform;
            overlay.SetParent(parent, false);
            overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            phoneOverlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, .9f); // also blocks clicks behind

            bigQr = new GameObject("Big QR", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            bigQr.rectTransform.SetParent(overlay, false);
            bigQr.rectTransform.sizeDelta = new Vector2(560f, 560f);
            bigQr.rectTransform.anchoredPosition = new Vector2(0f, 40f);

            phoneScanText = Instantiate(batPadText, overlay);
            phoneScanText.gameObject.SetActive(true);
            phoneScanText.rectTransform.anchorMin = phoneScanText.rectTransform.anchorMax = new Vector2(.5f, .5f);
            phoneScanText.rectTransform.sizeDelta = new Vector2(1500f, 60f);
            phoneScanText.rectTransform.anchoredPosition = new Vector2(0f, 370f);
            phoneScanText.fontSize = 32f;
            phoneScanText.textWrappingMode = TextWrappingModes.NoWrap;
            phoneScanText.alignment = TextAlignmentOptions.Center;

            var close = PlainButton(overlay, out phoneCloseLabel, 28f);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(.5f, .5f);
            closeRect.sizeDelta = new Vector2(280f, 70f);
            closeRect.anchoredPosition = new Vector2(0f, -300f);
            close.onClick.AddListener(() => phoneOverlay.SetActive(false));

            batPadQr.gameObject.SetActive(false);
            phoneButton.gameObject.SetActive(false);
            phoneOverlay.SetActive(false);
            SetPhoneTexts();
        }

        // Same look as the lobby buttons, with a plain label (the copied one is translated
        // to its own text).
        private Button PlainButton(Transform parent, out TMP_Text label, float fontSize)
        {
            var button = Instantiate(backButton, parent);
            button.onClick = new Button.ButtonClickedEvent();
            foreach (var old in button.GetComponentsInChildren<TMP_Text>(true)) Destroy(old.gameObject);
            label = Instantiate(batPadText, button.transform);
            label.gameObject.SetActive(true);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private void SetPhoneTexts()
        {
            phoneButtonLabel.text = L10n.Get(L10n.UI, "lobby.phone_connect");
            phoneScanText.text = L10n.Get(L10n.UI, "lobby.phone_scan");
            phoneCloseLabel.text = L10n.Get(L10n.UI, "lobby.phone_close");
        }

        private void OnDestroy()
        {
            if (batPadQrTexture != null) Destroy(batPadQrTexture);
        }

        private void OnEnable()
        {
            controller.Initialized += HandleInitialized;
            controller.SlotsChanged += RenderAll;
            controller.CountdownTick += HandleCountdownTick;
            controller.CountdownCancelled += HandleCountdownCancelled;
            controller.Notice += ShowNotice;
            L10n.LanguageChanged += HandleLanguageChanged;
            backButton.onClick.AddListener(controller.BackToMainMenu);
            foreach (var view in slotViews)
            {
                view.AddAIClicked += HandleAddAI;
                view.InviteClicked += HandleInvite;
                view.RemoveClicked += HandleRemove;
            }
        }

        private void OnDisable()
        {
            controller.Initialized -= HandleInitialized;
            controller.SlotsChanged -= RenderAll;
            controller.CountdownTick -= HandleCountdownTick;
            controller.CountdownCancelled -= HandleCountdownCancelled;
            controller.Notice -= ShowNotice;
            L10n.LanguageChanged -= HandleLanguageChanged;
            backButton.onClick.RemoveListener(controller.BackToMainMenu);
            if (controller.BatPad != null) controller.BatPad.RoomChanged -= RenderBatPad;
            foreach (var view in slotViews)
            {
                view.AddAIClicked -= HandleAddAI;
                view.InviteClicked -= HandleInvite;
                view.RemoveClicked -= HandleRemove;
            }
        }

        private void HandleInitialized()
        {
            locked = false;
            countdownText.gameObject.SetActive(false);
            if (controller.UsesPhones) controller.BatPad.RoomChanged += RenderBatPad;
            RenderBatPad();
            ShowHint();
            RenderAll();
        }

        private void ShowHint()
        {
            hintText.text = !controller.IsOnline
                ? L10n.Get(L10n.UI, "lobby.hint_local")
                : controller.IsAuthority
                    ? L10n.Get(L10n.UI, "lobby.hint_host")
                    : L10n.Get(L10n.UI, "lobby.hint_client");
            // Second line: how to change helmet, outfit and boots.
            hintText.text += System.Environment.NewLine + L10n.Get(L10n.UI, "lobby.hint_outfit");
        }

        // Slots, QR caption and callout are redrawn by RenderBatPad; the bottom hint is set again.
        private void HandleLanguageChanged()
        {
            addPlayerCallout.text = L10n.Get(L10n.UI, "lobby.add_player");
            SetPhoneTexts();
            RenderBatPad();
            if (controller.Players != null) ShowHint();
        }

        // QR of the phone controller once the room is open; a status text meanwhile.
        // Hidden where phones are not used (online clients).
        private void RenderBatPad()
        {
            bool initialized = controller.Players != null;
            bool usesPhones = !initialized || controller.UsesPhones;
            string url = initialized && usesPhones ? controller.BatPad.ControllerUrl : null;

            if (batPadQrTexture != null) Destroy(batPadQrTexture);
            batPadQrTexture = url != null ? QrTexture.Create(url) : null;

            batPadQr.texture = batPadQrTexture;
            bigQr.texture = batPadQrTexture;
            if (batPadQrTexture == null) phoneOverlay.SetActive(false);
            // Once the QR is up the callout above it explains it, so the caption only says "connecting".
            batPadText.gameObject.SetActive(usesPhones && url == null);
            batPadText.text = L10n.Get(L10n.UI, "lobby.batpad_connecting");
            if (initialized) RenderAll();
        }

        private void RenderAll()
        {
            if (controller.Players == null) return;
            var slots = controller.Slots;
            int occupied = 0;
            for (int i = 0; i < slotViews.Length; i++)
            {
                var info = slots[i];
                if (info.Occupied) occupied++;
                bool mine = controller.IsOnline && !controller.IsAuthority && info.Owner == controller.LocalClientId;
                slotViews[i].Render(info, controller.CanEditSlots, locked, mine, controller.IsOnline);
            }
            RenderAddPlayerCallout(occupied);

            // Same code for friends online and for the BatPad room of the phones.
            modeText.text = controller.IsOnline
                ? L10n.Format(L10n.UI, "lobby.mode_multi", occupied, controller.RoomCode)
                : L10n.Get(L10n.UI, "lobby.mode_single");
        }

        private void HandleCountdownTick(int secondsLeft)
        {
            countdownText.gameObject.SetActive(true);
            countdownText.text = secondsLeft > 0 ? secondsLeft.ToString() : L10n.Get(L10n.UI, "lobby.start");
            if (secondsLeft == 0)
            {
                // Match is starting: hide the edit buttons.
                locked = true;
                RenderAll();
            }
        }

        private void HandleCountdownCancelled()
        {
            countdownText.gameObject.SetActive(false);
            if (!locked) return;
            locked = false;
            RenderAll();
        }

        private void ShowNotice(string message) => hintText.text = message;

        private void HandleAddAI(int slotIndex) => controller.AddAI(slotIndex);
        private void HandleInvite(int slotIndex) => controller.InviteFriend(slotIndex);
        private void HandleRemove(int slotIndex) => controller.RemovePlayer(slotIndex);
    }
}
