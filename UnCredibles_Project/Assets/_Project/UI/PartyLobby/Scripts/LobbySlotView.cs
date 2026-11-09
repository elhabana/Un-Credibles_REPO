using System;
using TMPro;
using UnCredibles.Networking;
using UnCredibles.Players;
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
            background.color = !occupied ? emptyColor : info.IsAI ? aiColor : info.Ready ? readyColor : occupiedColor;

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
