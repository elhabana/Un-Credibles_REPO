using System;
using TMPro;
using UnCredibles.Networking;
using UnCredibles.Players;
using UnityEngine;
using UnityEngine.UI;

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
            nameText.text = occupied ? info.Name : online ? "LIBRE" : "EMPTY";
            inputText.text = !occupied ? string.Empty : mine ? "Tu" : InputLabel(info.Source);
            statusText.text = StatusLabel(info, canEdit, mine, online);
            background.color = !occupied ? emptyColor : info.IsAI ? aiColor : info.Ready ? readyColor : occupiedColor;

            bool free = info.State == SlotState.Empty;
            addAIButton.gameObject.SetActive(free && canEdit && !locked);
            inviteButton.gameObject.SetActive(false); // invitations go through the room code for now
            removeButton.gameObject.SetActive(occupied && canEdit && !locked);
        }

        private static string InputLabel(InputSourceType source) => source switch
        {
            InputSourceType.Keyboard => "Keyboard",
            InputSourceType.Gamepad => "Gamepad",
            InputSourceType.PrestoPad => "BatPad",
            InputSourceType.Network => "Online",
            InputSourceType.AI => "AI",
            _ => string.Empty,
        };

        private static string StatusLabel(LobbySlotInfo info, bool canEdit, bool mine, bool online) => info.State switch
        {
            SlotState.Empty => !online ? "Press SPACE / A to join" : canEdit ? "Esperando jugador" : "Libre",
            SlotState.Inviting => "Inviting...",
            SlotState.Connecting => "Connecting...",
            SlotState.Disconnected => "Disconnected",
            SlotState.AI => "READY",
            _ => info.Ready ? "READY"
                : !online || canEdit && info.Source != InputSourceType.Network || mine ? "Press SPACE / A when ready"
                : "Not ready",
        };

        private void OnAddAI() => AddAIClicked?.Invoke(SlotIndex);
        private void OnInvite() => InviteClicked?.Invoke(SlotIndex);
        private void OnRemove() => RemoveClicked?.Invoke(SlotIndex);
    }
}
