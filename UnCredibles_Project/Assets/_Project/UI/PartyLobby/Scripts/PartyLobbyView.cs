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
        [SerializeField] private TMP_Text addPlayerCallout; // blinking "another player? gamepad or QR" above the QR
        // The small QR is replaced by a "connect with your phone" button that opens it big.
        [Header("Phone QR")]
        [SerializeField] private Button phoneButton;
        [SerializeField] private TMP_Text phoneButtonLabel;
        [SerializeField] private GameObject phoneOverlay;
        [SerializeField] private RawImage bigQr;
        [SerializeField] private TMP_Text phoneScanText;
        [SerializeField] private Button phoneClose;
        [SerializeField] private TMP_Text phoneCloseLabel;
        // Art bible look: title on a taped label, the room code in a turquoise pill.
        [Header("Title")]
        [SerializeField] private RectTransform titleLabel;
        [SerializeField] private Image codePill;
        [SerializeField] private TMP_Text codeText;

        private const float CalloutBlinkSpeed = 2.5f;

        private bool locked;
        private Texture2D batPadQrTexture;
        private Vector2 titleCenter;

        private void Awake()
        {
            for (int i = 0; i < slotViews.Length; i++) slotViews[i].Setup(i);
            countdownText.gameObject.SetActive(false);
            titleCenter = titleLabel.anchoredPosition; // baked where the title sits; LayoutTitle moves it
            addPlayerCallout.text = L10n.Get(L10n.UI, "lobby.add_player");
            addPlayerCallout.gameObject.SetActive(false);
            phoneButton.onClick.AddListener(() =>
            {
                phoneOverlay.transform.SetAsLastSibling(); // above the cards and the outfit arrows
                phoneOverlay.SetActive(true);
            });
            phoneClose.onClick.AddListener(() => phoneOverlay.SetActive(false));
            batPadQr.gameObject.SetActive(false);
            phoneButton.gameObject.SetActive(false);
            phoneOverlay.SetActive(false);
            SetPhoneTexts();
            RenderBatPad();
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
            if (!withCode) return;
            codePill.rectTransform.anchoredPosition = titleCenter + new Vector2((total - pillWidth) * .5f, 0f);
            codePill.rectTransform.sizeDelta = new Vector2(pillWidth, 68f);
        }

        private void Update()
        {
            if (!addPlayerCallout.gameObject.activeSelf) return;
            addPlayerCallout.alpha = Mathf.Lerp(0.35f, 1f, Mathf.PingPong(Time.unscaledTime * CalloutBlinkSpeed, 1f));
        }

        // Only while someone can still join here: the QR is up, a slot is free and the match is not starting.
        private void RenderAddPlayerCallout(int occupied)
        {
            bool show = batPadQrTexture != null && !locked && occupied < PlayerRegistry.MaxPlayers;
            addPlayerCallout.gameObject.SetActive(show);
            phoneButton.gameObject.SetActive(show);
            if (!show) phoneOverlay.SetActive(false);
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
