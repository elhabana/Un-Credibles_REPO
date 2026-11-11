using System;
using TMPro;
using UnCredibles.Minigames;
using UnCredibles.Networking;
using UnCredibles.Players;
using UnCredibles.UI.Garage;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Localization.Components;
using UnCredibles.Localization;

namespace UnCredibles.UI.PartyLobby
{
    // One lobby card. Only renders a PlayerSlot and reports button clicks.
    public sealed class LobbySlotView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private GameObject crown;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text inputText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button addAIButton;
        [SerializeField] private Button inviteButton;
        [SerializeField] private Button removeButton;

        [Header("Colors")]
        [SerializeField] private Color emptyColor = new Color(0.12f, 0.13f, 0.18f, 0.9f);
        [SerializeField] private Color occupiedColor = new Color(0.18f, 0.28f, 0.52f, 1f);
        [SerializeField] private Color readyColor = new Color(0.18f, 0.55f, 0.3f, 1f);
        [SerializeField] private Color aiColor = new Color(0.36f, 0.27f, 0.5f, 1f);

        public int SlotIndex { get; private set; }

        private static readonly string[] LevelKeys = { "lobby.bot_easy", "lobby.bot_medium", "lobby.bot_hard" }; // BotDifficulty order
        private const float LevelButtonHeight = 48f;
        private const float LevelButtonStep = 55f;
        private const float LevelCancelSize = 40f;

        // Second step of adding a bot: "+ BOT" opens one full-width button per difficulty and an X to go back.
        private Button[] levelButtons;
        private Button levelCancel;
        private bool choosingLevel;
        private Action redraw;

        // Art bible look (ApplyArt): the card as a paper sheet, the player's disc, READY as a rubber stamp.
        private static readonly Color FadedPaper = new Color(.957f, .922f, .867f, .55f);
        private static readonly Color FadedInk = new Color(.114f, .114f, .106f, .45f);
        private static readonly Vector2 Middle = new Vector2(.5f, .5f);
        private const float StampSeconds = .15f;
        private TMP_FontAsset titleFont;
        private bool styled;
        private Shadow shadow;
        private GameObject tape;
        private Image disc;
        private TMP_Text discTag;
        private RectTransform stamp;
        private bool wasReady;
        private float stampTime = -1f;

        public event Action<int, BotDifficulty> AddAIClicked;
        public event Action<int> InviteClicked;
        public event Action<int> RemoveClicked;

        public void Setup(int slotIndex) => SlotIndex = slotIndex;

        private void OnEnable()
        {
            addAIButton.onClick.AddListener(OnAddAI);
            inviteButton.onClick.AddListener(OnInvite);
            removeButton.onClick.AddListener(OnRemove);
        }

        private void OnDisable()
        {
            choosingLevel = false;
            addAIButton.onClick.RemoveListener(OnAddAI);
            inviteButton.onClick.RemoveListener(OnInvite);
            removeButton.onClick.RemoveListener(OnRemove);
        }

        // canEdit: this machine owns the lobby (offline or online host). mine: the online client's own slot.
        public void Render(LobbySlotInfo info, bool canEdit, bool locked, bool mine, bool online)
        {
            BuildLevelButtons();
            redraw = () => Render(info, canEdit, locked, mine, online);
            bool occupied = info.Occupied;
            crown.SetActive(info.Host);
            nameText.text = occupied ? info.Name : online ? L10n.Get(L10n.UI, "lobby.slot_free") : L10n.Get(L10n.UI, "lobby.slot_empty");
            inputText.text = !occupied ? string.Empty : mine ? L10n.Get(L10n.UI, "lobby.you")
                : info.IsAI ? $"{InputLabel(info.Source)} - {L10n.Get(L10n.UI, LevelKeys[(int)info.BotLevel])}" : InputLabel(info.Source);
            statusText.text = StatusLabel(info, canEdit, mine, online);
            if (!styled) background.color = !occupied ? emptyColor : info.IsAI ? aiColor : info.Ready ? readyColor : occupiedColor;

            bool canAdd = info.State == SlotState.Empty && canEdit && !locked;
            choosingLevel &= canAdd;
            if (choosingLevel)
            {
                nameText.text = L10n.Get(L10n.UI, "lobby.bot_choose");
                inputText.text = statusText.text = string.Empty;
            }
            addAIButton.gameObject.SetActive(canAdd && !choosingLevel);
            levelCancel.gameObject.SetActive(choosingLevel);
            for (int i = 0; i < levelButtons.Length; i++)
            {
                levelButtons[i].gameObject.SetActive(choosingLevel);
                levelButtons[i].GetComponentInChildren<TMP_Text>(true).text = L10n.Get(L10n.UI, LevelKeys[i]); // follows the language
            }
            inviteButton.gameObject.SetActive(false); // invitations go through the room code for now
            removeButton.gameObject.SetActive(occupied && canEdit && !locked);
            if (styled) RenderPaper(info);
        }

        // Called by PartyLobbyView with Bangers, after Setup: restyles the scene card and its buttons.
        public void ApplyArt(TMP_FontAsset font)
        {
            titleFont = font;
            styled = true;
            var card = background.rectTransform;
            card.localEulerAngles = new Vector3(0f, 0f, SlotIndex % 2 == 0 ? -1.5f : 1.5f);
            background.pixelsPerUnitMultiplier = 1f;
            SignButton.AddShadow(background);
            shadow = background.GetComponent<Shadow>();

            var strip = new GameObject("Tape", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            strip.transform.SetParent(card, false);
            strip.color = new Color(1f, .97f, .85f, .55f); // PaperUI's tape
            strip.raycastTarget = false;
            Place(strip.rectTransform, new Vector2(-160f, 118f), new Vector2(110f, 32f));
            strip.rectTransform.localEulerAngles = new Vector3(0f, 0f, 40f);
            tape = strip.gameObject;

            disc = PaperUI.Disc("Disc", card, PlayerIdentity.ColorFor(SlotIndex));
            Place(disc.rectTransform, new Vector2(-132f, 78f), new Vector2(68f, 68f));
            discTag = PaperUI.Text("Tag", disc.transform, font, 30f, PaperUI.Ink, TextAlignmentOptions.Center);
            PaperUI.Stretch(discTag.rectTransform);

            // Behind the status text, which then reads in paper over the red ink.
            stamp = PaperUI.Pill("Ready stamp", card, PaperUI.Alarm, 56f).rectTransform;
            stamp.SetSiblingIndex(statusText.transform.GetSiblingIndex());
            Place(stamp, new Vector2(0f, 2f), new Vector2(210f, 56f));
            stamp.localEulerAngles = new Vector3(0f, 0f, -8f);
            stamp.gameObject.SetActive(false);

            Label(nameText, new Vector2(6f, 88f), new Vector2(192f, 44f), 38f, TextAlignmentOptions.Left);
            Label(inputText, new Vector2(40f, 50f), new Vector2(264f, 30f), 24f, TextAlignmentOptions.Left);
            Label(statusText, new Vector2(0f, 2f), new Vector2(330f, 44f), 30f, TextAlignmentOptions.Center);
            inputText.color = SignButton.SoftInk;
            if (crown.TryGetComponent(out TMP_Text crownText))
            {
                SignButton.Text(crownText, font, PaperUI.Alarm, false);
                Place(crownText.rectTransform, new Vector2(128f, 116f), new Vector2(100f, 28f));
                crownText.rectTransform.localEulerAngles = new Vector3(0f, 0f, 8f);
            }

            var remove = (RectTransform)removeButton.transform;
            Place(remove, new Vector2(0f, -92f), new Vector2(170f, 44f));
            SignButton.Pill(removeButton, font, SignButton.Brick, PaperUI.Paper);
            SignButton.Pill(addAIButton, font, PaperUI.Ink, PaperUI.Paper);
            if (levelButtons != null) // already drawn once: its copies were made from the plain "+ BOT"
            {
                foreach (var level in levelButtons) SignButton.Pill(level, font, PaperUI.Ink, PaperUI.Paper);
                SignButton.Pill(levelCancel, font, SignButton.Brick, PaperUI.Paper);
            }
            redraw?.Invoke();
        }

        // Empty: faded paper with a dashed border. Taken: paper with tape and shadow. READY slams a stamp.
        private void RenderPaper(LobbySlotInfo info)
        {
            bool occupied = info.Occupied;
            bool ready = !choosingLevel && (info.State == SlotState.AI || info.State == SlotState.Occupied && info.Ready);
            background.sprite = occupied ? PaperUI.PanelSprite : SignButton.DashedSprite;
            background.type = occupied ? Image.Type.Sliced : Image.Type.Tiled;
            background.color = occupied ? PaperUI.Paper : FadedPaper;
            shadow.enabled = occupied;
            tape.SetActive(occupied);
            var color = PlayerIdentity.ColorFor(SlotIndex, info.IsAI);
            if (!occupied) color.a = .35f;
            disc.color = color;
            discTag.text = PlayerIdentity.LabelFor(SlotIndex, info.IsAI);
            discTag.color = occupied ? PaperUI.Ink : FadedInk;
            nameText.color = occupied || choosingLevel ? PaperUI.Ink : FadedInk;
            statusText.color = ready ? PaperUI.Paper : SignButton.SoftInk;
            statusText.rectTransform.localEulerAngles = new Vector3(0f, 0f, ready ? -8f : 0f);
            stamp.gameObject.SetActive(ready);
            if (ready && !wasReady)
            {
                stampTime = 0f;
                stamp.localScale = new Vector3(2.2f, 2.2f, 1f);
            }
            wasReady = ready;
        }

        // The stamp slams down: from big to its size in a moment (as on the rules card).
        private void Update()
        {
            if (stampTime < 0f) return;
            stampTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(stampTime / StampSeconds);
            float scale = Mathf.Lerp(2.2f, 1f, t);
            stamp.localScale = new Vector3(scale, scale, 1f);
            if (t >= 1f) stampTime = -1f;
        }

        private void Label(TMP_Text label, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            SignButton.Text(label, titleFont, PaperUI.Ink, false);
            Place(label.rectTransform, position, size);
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.enableAutoSizing = true;
            label.fontSizeMin = 16f;
            label.fontSizeMax = fontSize;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size) => PaperUI.Place(rect, Middle, position, size);

        private static string InputLabel(InputSourceType source) => source switch
        {
            InputSourceType.Keyboard => L10n.Get(L10n.UI, "lobby.input_keyboard"),
            InputSourceType.Gamepad => L10n.Get(L10n.UI, "lobby.input_gamepad"),
            InputSourceType.PrestoPad => L10n.Get(L10n.UI, "lobby.input_batpad"),
            InputSourceType.Network => L10n.Get(L10n.UI, "lobby.input_online"),
            InputSourceType.AI => L10n.Get(L10n.UI, "lobby.input_bot"),
            _ => string.Empty,
        };

        private static string StatusLabel(LobbySlotInfo info, bool canEdit, bool mine, bool online) => info.State switch
        {
            SlotState.Empty => !online ? L10n.Get(L10n.UI, "lobby.press_join") : canEdit ? L10n.Get(L10n.UI, "lobby.waiting_player") : L10n.Get(L10n.UI, "lobby.free"),
            SlotState.Inviting => L10n.Get(L10n.UI, "lobby.inviting"),
            SlotState.Connecting => L10n.Get(L10n.UI, "lobby.connecting"),
            SlotState.Disconnected => L10n.Get(L10n.UI, "lobby.disconnected"),
            SlotState.AI => L10n.Get(L10n.UI, "lobby.ready"),
            _ => info.Ready ? L10n.Get(L10n.UI, "lobby.ready")
                : !online || canEdit && info.Source != InputSourceType.Network || mine ? L10n.Get(L10n.UI, "lobby.press_ready")
                : L10n.Get(L10n.UI, "lobby.not_ready"),
        };

        // Built on the first Render: the card may be drawn before its own Awake while the lobby activates.
        private void BuildLevelButtons()
        {
            if (levelButtons != null) return;
            var template = (RectTransform)addAIButton.transform;
            float bottom = template.anchoredPosition.y - (template.sizeDelta.y - LevelButtonHeight) * .5f; // lines up with "+ BOT"
            levelButtons = new Button[LevelKeys.Length];
            for (int i = 0; i < levelButtons.Length; i++)
            {
                float y = bottom + (levelButtons.Length - 1 - i) * LevelButtonStep; // Easy on top
                var button = CopyAddButton(new Vector2(template.anchoredPosition.x, y), new Vector2(template.sizeDelta.x, LevelButtonHeight));
                var level = (BotDifficulty)i;
                button.onClick.AddListener(() =>
                {
                    choosingLevel = false;
                    AddAIClicked?.Invoke(SlotIndex, level);
                    redraw();
                });
                levelButtons[i] = button;
            }

            var corner = new Vector2(template.anchoredPosition.x + (template.sizeDelta.x - LevelCancelSize) * .5f, nameText.rectTransform.anchoredPosition.y);
            levelCancel = CopyAddButton(corner, Vector2.one * LevelCancelSize);
            levelCancel.GetComponentInChildren<TMP_Text>(true).text = "X";
            levelCancel.onClick.AddListener(() => ShowLevels(false));
        }

        private Button CopyAddButton(Vector2 position, Vector2 size)
        {
            var rect = Instantiate((RectTransform)addAIButton.transform, addAIButton.transform.parent);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var localized = rect.GetComponentInChildren<LocalizeStringEvent>(true);
            if (localized != null) DestroyImmediate(localized); // would overwrite the copy's own text
            return rect.GetComponent<Button>();
        }

        private void ShowLevels(bool show)
        {
            choosingLevel = show;
            redraw();
            if (EventSystem.current != null) // the pressed button just hid: keep a focus for pad and keys
                EventSystem.current.SetSelectedGameObject((show ? levelButtons[(int)BotDifficulty.Medium] : addAIButton).gameObject);
        }

        private void OnAddAI() => ShowLevels(true);
        private void OnInvite() => InviteClicked?.Invoke(SlotIndex);
        private void OnRemove() => RemoveClicked?.Invoke(SlotIndex);
    }
}
