using System;
using TMPro;
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

        public void Render(PlayerSlot slot, bool invitesEnabled, bool locked)
        {
            bool occupied = slot.IsOccupied;
            crown.SetActive(slot.IsHost);
            nameText.text = occupied ? slot.PlayerName : "EMPTY";
            inputText.text = occupied ? InputLabel(slot) : string.Empty;
            statusText.text = StatusLabel(slot, invitesEnabled);
            background.color = !occupied ? emptyColor : slot.IsAI ? aiColor : slot.IsReady ? readyColor : occupiedColor;

            bool free = slot.State == SlotState.Empty;
            addAIButton.gameObject.SetActive(free && !locked);
            inviteButton.gameObject.SetActive(free && invitesEnabled && !locked);
            removeButton.gameObject.SetActive(occupied && !locked);
        }

        private static string InputLabel(PlayerSlot slot) => slot.InputSource switch
        {
            InputSourceType.Keyboard => "Keyboard",
            InputSourceType.Gamepad => "Gamepad",
            InputSourceType.PrestoPad => "PrestoPad",
            InputSourceType.Network => "Online",
            InputSourceType.AI => "AI",
            _ => string.Empty,
        };

        private static string StatusLabel(PlayerSlot slot, bool invitesEnabled) => slot.State switch
        {
            SlotState.Empty => invitesEnabled ? string.Empty : "Press SPACE / A to join",
            SlotState.Inviting => "Inviting...",
            SlotState.Connecting => "Connecting...",
            SlotState.Disconnected => "Disconnected",
            SlotState.AI => "READY",
            _ => slot.IsReady ? "READY" : "Press SPACE / A when ready",
        };

        private void OnAddAI() => AddAIClicked?.Invoke(SlotIndex);
        private void OnInvite() => InviteClicked?.Invoke(SlotIndex);
        private void OnRemove() => RemoveClicked?.Invoke(SlotIndex);
    }
}
