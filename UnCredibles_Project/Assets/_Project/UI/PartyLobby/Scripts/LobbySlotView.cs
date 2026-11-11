using System;
using TMPro;
using UnCredibles.Minigames;
using UnCredibles.Networking;
using UnCredibles.Players;
using UnCredibles.UI.Garage;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
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

        // Second step of adding a bot: "+ BOT" opens one full-width button per difficulty and an X to go back.
        [Header("Bot level")]
        [SerializeField] private Button[] levelButtons; // BotDifficulty order
        [SerializeField] private Button levelCancel;

        // Art bible look (PF_LobbySlot): the card as a paper sheet, the player's disc, READY as a rubber stamp.
        [Header("Paper look")]
        [SerializeField] private Shadow shadow;
        [SerializeField] private GameObject tape;
        [SerializeField] private Image disc;
        [SerializeField] private TMP_Text discTag;
        [SerializeField] private RectTransform stamp;
        [SerializeField] private Sprite paperSprite;   // T_UI_Panel, taken
        [SerializeField] private Sprite dashedSprite;  // T_UI_Dashed, empty (tiled so the dashes repeat)

        public int SlotIndex { get; private set; }

        private static readonly string[] LevelKeys = { "lobby.bot_easy", "lobby.bot_medium", "lobby.bot_hard" }; // BotDifficulty order
        internal static readonly Color FadedPaper = new Color(.957f, .922f, .867f, .55f);
        internal static readonly Color FadedInk = new Color(.114f, .114f, .106f, .45f);
        private const float StampSeconds = .15f;
        private bool choosingLevel;
        private Action redraw;
        private bool wasReady;
        private float stampTime = -1f;

        public event Action<int, BotDifficulty> AddAIClicked;
        public event Action<int> InviteClicked;
        public event Action<int> RemoveClicked;

        public void Setup(int slotIndex) => SlotIndex = slotIndex;

        private void Awake()
        {
            for (int i = 0; i < levelButtons.Length; i++)
            {
                var level = (BotDifficulty)i;
                levelButtons[i].onClick.AddListener(() =>
                {
                    choosingLevel = false;
                    AddAIClicked?.Invoke(SlotIndex, level);
                    redraw();
                });
            }
            levelCancel.onClick.AddListener(() => ShowLevels(false));
        }

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
            redraw = () => Render(info, canEdit, locked, mine, online);
            bool occupied = info.Occupied;
            crown.SetActive(info.Host);
            nameText.text = occupied ? info.Name : online ? L10n.Get(L10n.UI, "lobby.slot_free") : L10n.Get(L10n.UI, "lobby.slot_empty");
            inputText.text = !occupied ? string.Empty : mine ? L10n.Get(L10n.UI, "lobby.you")
                : info.IsAI ? $"{InputLabel(info.Source)} - {L10n.Get(L10n.UI, LevelKeys[(int)info.BotLevel])}" : InputLabel(info.Source);
            statusText.text = StatusLabel(info, canEdit, mine, online);

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
            RenderPaper(info);
        }

        // Empty: faded paper with a dashed border. Taken: paper with tape and shadow. READY slams a stamp.
        private void RenderPaper(LobbySlotInfo info)
        {
            bool occupied = info.Occupied;
            bool ready = !choosingLevel && (info.State == SlotState.AI || info.State == SlotState.Occupied && info.Ready);
            background.sprite = occupied ? paperSprite : dashedSprite;
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
