using System.Collections;
using TMPro;
using UnCredibles.Core;
using UnCredibles.Players;
using UnityEngine;
using UnityEngine.UI;

namespace UnCredibles.UI.PartyLobby
{
    // Renders the lobby. Refreshes only when a slot changes, never every frame.
    public sealed class PartyLobbyView : MonoBehaviour
    {
        [SerializeField] private PartyLobbyController controller;
        [SerializeField] private LobbySlotView[] slotViews = new LobbySlotView[PlayerRegistry.MaxPlayers];
        [SerializeField] private TMP_Text modeText;
        [SerializeField] private TMP_Text countdownText;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private Button backButton;

        private bool locked;

        private void Awake()
        {
            for (int i = 0; i < slotViews.Length; i++) slotViews[i].Setup(i);
            countdownText.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            controller.Initialized += HandleInitialized;
            controller.CountdownTick += HandleCountdownTick;
            controller.CountdownCancelled += HandleCountdownCancelled;
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
            controller.CountdownTick -= HandleCountdownTick;
            controller.CountdownCancelled -= HandleCountdownCancelled;
            backButton.onClick.RemoveListener(controller.BackToMainMenu);
            if (controller.Players != null) controller.Players.SlotChanged -= RenderSlot;
            foreach (var view in slotViews)
            {
                view.AddAIClicked -= HandleAddAI;
                view.InviteClicked -= HandleInvite;
                view.RemoveClicked -= HandleRemove;
            }
        }

        private void HandleInitialized()
        {
            modeText.text = controller.Mode == SessionMode.Online ? "MULTIPLAYER" : "SINGLEPLAYER";
            hintText.text = "SPACE / A: join & ready     ESC / START: cancel & leave";
            controller.Players.SlotChanged += RenderSlot;
            RenderAll();
        }

        private void HandleCountdownTick(int secondsLeft)
        {
            countdownText.gameObject.SetActive(true);
            countdownText.text = secondsLeft > 0 ? secondsLeft.ToString() : "START!";
            if (secondsLeft == 0)
            {
                // Match is starting: hide the edit buttons.
                locked = true;
                RenderAll();
            }
        }

        private void HandleCountdownCancelled() => countdownText.gameObject.SetActive(false);

        private void RenderSlot(PlayerSlot slot) =>
            slotViews[slot.SlotIndex].Render(slot, controller.InvitesEnabled, locked);

        private void RenderAll()
        {
            foreach (var slot in controller.Players.Slots) RenderSlot(slot);
        }

        private void HandleAddAI(int slotIndex) => controller.AddAI(slotIndex);
        private void HandleInvite(int slotIndex) => controller.InviteFriend(slotIndex);
        private void HandleRemove(int slotIndex) => controller.RemovePlayer(slotIndex);
    }
}
