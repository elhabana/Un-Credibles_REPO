using TMPro;
using UnCredibles.BatPad;
using UnCredibles.Minigames;
using UnCredibles.Players;
using UnCredibles.Players.Inputs;
using UnCredibles.UI.Garage;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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
        private bool menuWasOpen;
        private bool cursorMode;
        private int dressSlot = -1; // bot card being dressed from the cursor
        private Vector2 dressLast;
        private GameObject pendingFocus;
        private int hintMode; // 0 lobby, 1 cursor and its menus, 2 dressing a bot

        private void Awake()
        {
            for (int i = 0; i < slotViews.Length; i++) slotViews[i].Setup(i);
            countdownText.gameObject.SetActive(false);
            titleCenter = titleLabel.anchoredPosition; // baked where the title sits; LayoutTitle moves it
            addPlayerCallout.text = L10n.Get(L10n.UI, "lobby.add_player");
            addPlayerCallout.gameObject.SetActive(false);
            phoneButton.onClick.AddListener(OpenPhone);
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

        private void OpenPhone()
        {
            phoneOverlay.transform.SetAsLastSibling(); // above the cards and the outfit arrows
            phoneOverlay.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(phoneClose.gameObject);
        }

        // Pads and keys have no pointer: X / E opens the bot level choice on the first free card, Y / Shift the
        // phone QR. While one is open the EventSystem drives it (stick + A) and B closes it.
        private void HandlePadShortcuts()
        {
            if (controller.Players == null) return;
            LobbySlotView choosing = null;
            foreach (var view in slotViews)
                if (view.ChoosingLevel) choosing = view;
            bool open = cursorMode || choosing != null || phoneOverlay.activeSelf;
            if (open)
            {
                controller.MenuFrame = Time.frameCount;
                bool menu = AnyMenuPressed();
                if (dressSlot >= 0) DressBot();
                else if (menu || AnyDevicePressed(PlayerAction.Crouch) || AnyDevicePressed(PlayerAction.Pause) ||
                    phoneOverlay.activeSelf && AnyDevicePressed(PlayerAction.Ability))
                {
                    // B steps back one level (level choice / QR, then the cursor); Select / Tab closes it all.
                    GameAudio.Play(GameSfx.UiBack);
                    if (choosing != null) choosing.CloseLevels();
                    else if (phoneOverlay.activeSelf)
                    {
                        phoneOverlay.SetActive(false);
                        if (cursorMode && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(phoneButton.gameObject);
                    }
                    else cursorMode = false;
                    if (menu)
                    {
                        phoneOverlay.SetActive(false);
                        cursorMode = false;
                    }
                }
                else if (cursorMode && choosing == null && !phoneOverlay.activeSelf && AnyDevicePressed(PlayerAction.Interact))
                    StartDressing();
                else if (cursorMode && pendingFocus != null)
                {
                    if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(pendingFocus);
                    pendingFocus = null;
                }
                else if (cursorMode) KeepCursorFocus();
            }
            else if (AnyMenuPressed())
            {
                GameAudio.Play(GameSfx.UiConfirm);
                cursorMode = true;
                KeepCursorFocus();
            }
            else if (AnyDevicePressed(PlayerAction.Interact))
            {
                LobbySlotView free = null;
                foreach (var view in slotViews)
                    if (free == null && view.CanAddBot) free = view;
                if (free == null) GameAudio.Play(GameSfx.UiError);
                else
                {
                    GameAudio.Play(GameSfx.UiConfirm);
                    free.OpenLevels();
                }
            }
            else if (phoneButton.gameObject.activeSelf && AnyDevicePressed(PlayerAction.Ability))
            {
                GameAudio.Play(GameSfx.UiConfirm);
                OpenPhone();
            }

            if (locked) // the match is starting: the edit buttons are gone
            {
                cursorMode = false;
                dressSlot = -1;
            }
            bool nowOpen = cursorMode || phoneOverlay.activeSelf;
            foreach (var view in slotViews) nowOpen |= view.ChoosingLevel;
            // Closed: no button keeps the focus, or the next A would press it as well as toggle Ready.
            if (menuWasOpen && !nowOpen && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            menuWasOpen = nowOpen;
            int mode = dressSlot >= 0 ? 2 : nowOpen ? 1 : 0;
            if (mode == hintMode) return;
            hintMode = mode;
            ShowHint();
        }

        // X / E on a bot's card (cursor mode): the stick dresses that bot like a player dresses itself.
        private void StartDressing()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            int card = -1;
            for (int i = 0; i < slotViews.Length; i++)
                if (selected != null && selected.transform.IsChildOf(slotViews[i].transform)) card = i;
            if (card < 0 || !controller.Slots[card].IsAI || !controller.CanCustomize(card))
            {
                GameAudio.Play(GameSfx.UiError);
                return;
            }
            GameAudio.Play(GameSfx.UiConfirm);
            dressSlot = card;
            dressLast = StrongestMove(); // a stick already pushed does not count as a step
            EventSystem.current.SetSelectedGameObject(null); // the stick is for the outfit now
        }

        private void DressBot()
        {
            if (!controller.Slots[dressSlot].IsAI || AnyDevicePressed(PlayerAction.Jump) || AnyDevicePressed(PlayerAction.Crouch) ||
                AnyDevicePressed(PlayerAction.Pause) || AnyDevicePressed(PlayerAction.Interact) || AnyMenuPressed())
            {
                GameAudio.Play(GameSfx.UiBack);
                // Next frame: the A that finished must not also press the button it lands on.
                var remove = slotViews[dressSlot].RemoveButton;
                pendingFocus = remove.activeInHierarchy ? remove : null;
                dressSlot = -1;
                return;
            }
            const float push = .5f;
            var move = StrongestMove();
            if (move.y > push && dressLast.y <= push) controller.MovePartRow(dressSlot, -1);
            else if (move.y < -push && dressLast.y >= -push) controller.MovePartRow(dressSlot, 1);
            else if (move.x > push && dressLast.x <= push) controller.StepPart(dressSlot, (CharacterPartSlot)controller.SelectedPartRow(dressSlot), 1);
            else if (move.x < -push && dressLast.x >= -push) controller.StepPart(dressSlot, (CharacterPartSlot)controller.SelectedPartRow(dressSlot), -1);
            dressLast = move;
        }

        private Vector2 StrongestMove()
        {
            var move = Vector2.zero;
            foreach (var slot in controller.Players.Slots)
                if (slot.IsOccupied && !slot.IsAI && slot.Input is DeviceInput input && input.Move.sqrMagnitude > move.sqrMagnitude)
                    move = input.Move;
            return move;
        }

        // Keyboard and gamepads of this machine only: phones and online players cannot drive these menus.
        private bool AnyDevicePressed(PlayerAction action)
        {
            foreach (var slot in controller.Players.Slots)
                if (slot.IsOccupied && !slot.IsAI && slot.Input is DeviceInput input && input.WasPressed(action)) return true;
            return false;
        }

        // Select on a pad, Tab on the keyboard: the cursor over the cards (add / remove bot, QR, leave).
        private bool AnyMenuPressed()
        {
            foreach (var slot in controller.Players.Slots)
                if (slot.IsOccupied && !slot.IsAI && slot.Input is DeviceInput input &&
                    (input.Device is Gamepad pad && pad.selectButton.wasPressedThisFrame ||
                     input.Device is Keyboard keys && keys.tabKey.wasPressedThisFrame)) return true;
            return false;
        }

        // The pressed button may have just hidden (bot added, player removed): move to the first one still there.
        private void KeepCursorFocus()
        {
            var events = EventSystem.current;
            if (events == null) return;
            var current = events.currentSelectedGameObject;
            if (current != null && current.activeInHierarchy) return;
            foreach (var button in GetComponentsInChildren<Button>())
                if (button.interactable && button.navigation.mode != Navigation.Mode.None) // outfit arrows opt out
                {
                    events.SetSelectedGameObject(button.gameObject);
                    return;
                }
        }

        private void Update()
        {
            HandlePadShortcuts();
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
            // Inside the cursor or dressing a bot the bottom line lists only the controls that work there.
            if (hintMode > 0)
            {
                hintText.text = L10n.Get(L10n.UI, hintMode == 2 ? "lobby.hint_dress" : "lobby.hint_cursor");
                return;
            }
            hintText.text = !controller.IsOnline
                ? L10n.Get(L10n.UI, "lobby.hint_local")
                : controller.IsAuthority
                    ? L10n.Get(L10n.UI, "lobby.hint_host")
                    : L10n.Get(L10n.UI, "lobby.hint_client");
            // Second line: how to change helmet, outfit and boots.
            hintText.text += System.Environment.NewLine + L10n.Get(L10n.UI, "lobby.hint_outfit");
            // Third line, only for who can edit the cards: the menu, bots and phone QR.
            if (controller.CanEditSlots) hintText.text += System.Environment.NewLine + L10n.Get(L10n.UI, "lobby.hint_menu");
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
