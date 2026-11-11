using TMPro;
using UnCredibles.BatPad;
using UnCredibles.Minigames;
using UnCredibles.Players;
using UnCredibles.UI.Garage;
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
        private Button phoneClose;
        // Art bible look (ApplyArt): title on a taped label, the room code in a turquoise pill.
        private TMP_FontAsset titleFont;
        private bool artRequested;
        private RectTransform titleLabel;
        private Image codePill;
        private TMP_Text codeText;
        private Vector2 titleCenter;

        private void Awake()
        {
            for (int i = 0; i < slotViews.Length; i++) slotViews[i].Setup(i);
            countdownText.gameObject.SetActive(false);
            CreateAddPlayerCallout();
            CreatePhonePanel();
            if (artRequested) Style();
            RenderBatPad();
        }

        // Called by GarageFrontend with Bangers; styles now, or at Awake if the lobby has not woken up yet.
        public void ApplyArt(TMP_FontAsset font)
        {
            titleFont = font;
            artRequested = true;
            if (addPlayerCallout != null) Style();
        }

#if UNITY_EDITOR
        // Edit-mode preview only, on GarageMenuPreview's hidden throwaway copy of the canvas: the runtime look
        // with four empty local slots (no controller, BatPad or players outside Play).
        public void BuildPreview(TMP_FontAsset font)
        {
            Awake();
            ApplyArt(font);
            for (int i = 0; i < slotViews.Length; i++) slotViews[i].Render(default, true, false, false, false);
        }
#endif

        private void Style()
        {
            artRequested = false;
            foreach (var view in slotViews) view.ApplyArt(titleFont);
            var parent = modeText.transform.parent;

            titleCenter = modeText.rectTransform.anchoredPosition;
            var label = PaperUI.Panel("Title label", parent, Color.white, -1.5f, true);
            label.transform.SetSiblingIndex(modeText.transform.GetSiblingIndex());
            titleLabel = label.rectTransform;
            PaperUI.Place(titleLabel, modeText.rectTransform.anchorMin, titleCenter, new Vector2(500f, 84f));
            SignButton.Text(modeText, titleFont, PaperUI.Ink, false);
            modeText.enableAutoSizing = true;
            modeText.fontSizeMin = 24f;
            modeText.fontSizeMax = 52f;
            modeText.rectTransform.localEulerAngles = new Vector3(0f, 0f, -1.5f);

            codePill = PaperUI.Pill("Room code", parent, PaperUI.TimerTeal, 68f);
            codePill.rectTransform.localEulerAngles = new Vector3(0f, 0f, 2f);
            codeText = PaperUI.Text("Code", codePill.transform, titleFont, 44f, PaperUI.Paper, TextAlignmentOptions.Center);
            PaperUI.Stretch(codeText.rectTransform, 6f);
            codePill.gameObject.SetActive(false);

            // Controls footer on a paper pill, one line per hint.
            var hintRect = hintText.rectTransform;
            var footer = PaperUI.Pill("Controls", parent, PaperUI.Paper, 84f);
            footer.transform.SetSiblingIndex(hintText.transform.GetSiblingIndex());
            PaperUI.Place(footer.rectTransform, hintRect.anchorMin, hintRect.anchoredPosition, new Vector2(hintRect.sizeDelta.x + 40f, 84f));
            SignButton.Text(hintText, titleFont, PaperUI.Ink, false);
            hintText.fontSizeMax = hintText.fontSize;
            hintText.fontSizeMin = 14f;
            hintText.enableAutoSizing = true;
            hintRect.sizeDelta = new Vector2(hintRect.sizeDelta.x - 40f, 74f);

            SignButton.Text(countdownText, titleFont, PaperUI.Paper, true);
            SignButton.Text(addPlayerCallout, titleFont, SignButton.Mustard, true);
            SignButton.Text(batPadText, titleFont, PaperUI.Paper, true);
            SignButton.Text(phoneScanText, titleFont, PaperUI.Paper, false);
            SignButton.Pill(backButton, titleFont, SignButton.Brick, PaperUI.Paper, -1.5f);
            SignButton.Pill(phoneButton, titleFont, SignButton.SofaBlue, PaperUI.Paper, 1.5f);
            SignButton.Pill(phoneClose, titleFont, PaperUI.Ink, PaperUI.Paper);
            if (controller.Players != null) RenderAll();
        }

        // Label sized to the title, the code pill right after it; the pair centred where the title was.
        private void LayoutTitle(bool withCode)
        {
            float labelWidth = Mathf.Min(modeText.GetPreferredValues(modeText.text).x + 80f, 540f);
            float pillWidth = withCode ? Mathf.Min(codeText.GetPreferredValues(codeText.text).x + 60f, 380f) : 0f;
            float gap = withCode ? 24f : 0f;
            float total = labelWidth + gap + pillWidth;
            var labelCenter = titleCenter + new Vector2((labelWidth - total) * .5f, 0f);
            titleLabel.anchoredPosition = labelCenter;
            titleLabel.sizeDelta = new Vector2(labelWidth, titleLabel.sizeDelta.y);
            modeText.rectTransform.anchoredPosition = labelCenter;
            modeText.rectTransform.sizeDelta = new Vector2(labelWidth - 40f, 64f);
            codePill.gameObject.SetActive(withCode);
            if (withCode)
                PaperUI.Place(codePill.rectTransform, titleLabel.anchorMin,
                    titleCenter + new Vector2((total - pillWidth) * .5f, 0f), new Vector2(pillWidth, 68f));
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

            var close = phoneClose = PlainButton(overlay, out phoneCloseLabel, 28f);
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
            // Immediate: Style may run in this same frame and must find the new label, not the old one (and edit mode has no Destroy).
            foreach (var old in button.GetComponentsInChildren<TMP_Text>(true)) DestroyImmediate(old.gameObject);
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
            string mode = controller.IsOnline
                ? L10n.Format(L10n.UI, "lobby.mode_multi", occupied, controller.RoomCode)
                : L10n.Get(L10n.UI, "lobby.mode_single");
            if (codePill == null)
            {
                modeText.text = mode;
                return;
            }
            // shortcut: the code pill splits "MULTIJUGADOR n/4 | CODIGO: X" at the bar; a translation without
            // a bar stays whole on the label. Give the code its own key if that ever happens.
            int bar = controller.IsOnline ? mode.IndexOf('|') : -1;
            modeText.text = bar < 0 ? mode : mode.Substring(0, bar).Trim();
            if (bar >= 0) codeText.text = mode.Substring(bar + 1).Trim();
            LayoutTitle(bar >= 0);
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

        private void HandleAddAI(int slotIndex, BotDifficulty difficulty) =>
            GameAudio.Play(controller.AddAI(slotIndex, difficulty) ? GameSfx.UiConfirm : GameSfx.UiError);
        private void HandleInvite(int slotIndex) => controller.InviteFriend(slotIndex);
        private void HandleRemove(int slotIndex) =>
            GameAudio.Play(controller.RemovePlayer(slotIndex) ? GameSfx.UiBack : GameSfx.UiError);
    }
}
