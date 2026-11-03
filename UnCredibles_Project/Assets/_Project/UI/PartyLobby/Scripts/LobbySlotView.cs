using System;
using TMPro;
using UnCredibles.Networking;
using UnCredibles.Players;
using UnityEngine;
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

        [Header("Colors")]
        [SerializeField] private Color emptyColor = new Color(0.12f, 0.13f, 0.18f, 0.9f);
        [SerializeField] private Color occupiedColor = new Color(0.18f, 0.28f, 0.52f, 1f);
        [SerializeField] private Color readyColor = new Color(0.18f, 0.55f, 0.3f, 1f);
        [SerializeField] private Color aiColor = new Color(0.36f, 0.27f, 0.5f, 1f);

        public int SlotIndex { get; private set; }

        public event Action<int> AddAIClicked;
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
            addAIButton.onClick.RemoveListener(OnAddAI);
            inviteButton.onClick.RemoveListener(OnInvite);
            removeButton.onClick.RemoveListener(OnRemove);
        }

        // canEdit: this machine owns the lobby (offline or online host). mine: the online client's own slot.
        public void Render(LobbySlotInfo info, bool canEdit, bool locked, bool mine, bool online)
        {
            bool occupied = info.Occupied;
            crown.SetActive(info.Host);
            nameText.text = occupied ? info.Name : online ? L10n.Get(L10n.UI, "lobby.slot_free") : L10n.Get(L10n.UI, "lobby.slot_empty");
            inputText.text = !occupied ? string.Empty : mine ? L10n.Get(L10n.UI, "lobby.you") : InputLabel(info.Source);
            statusText.text = StatusLabel(info, canEdit, mine, online);
            background.color = !occupied ? emptyColor : info.IsAI ? aiColor : info.Ready ? readyColor : occupiedColor;

            bool free = info.State == SlotState.Empty;
            addAIButton.gameObject.SetActive(free && canEdit && !locked);
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

        private void OnAddAI() => AddAIClicked?.Invoke(SlotIndex);
        private void OnInvite() => InviteClicked?.Invoke(SlotIndex);
        private void OnRemove() => RemoveClicked?.Invoke(SlotIndex);
    }
}
