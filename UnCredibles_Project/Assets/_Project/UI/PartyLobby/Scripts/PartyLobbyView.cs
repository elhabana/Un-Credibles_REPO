using System.Collections;
using TMPro;
using UnCredibles.BatPad;
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
        [Header("BatPad")]
        [SerializeField] private RawImage batPadQr;
        [SerializeField] private TMP_Text batPadText;

        private bool locked;
        private string onlineSignature;
        private readonly System.Collections.Generic.List<PlayerSlot> phoneSlots = new System.Collections.Generic.List<PlayerSlot>();
        private Texture2D batPadQrTexture;

        private void Awake()
        {
            for (int i = 0; i < slotViews.Length; i++) slotViews[i].Setup(i);
            countdownText.gameObject.SetActive(false);
            RenderBatPad();
        }

        private void OnDestroy()
        {
            if (batPadQrTexture != null) Destroy(batPadQrTexture);
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
            modeText.text = controller.Mode == SessionMode.Online ? "MULTIPLAYER" : "SINGLEPLAYER";
            if (controller.UsesPhones) controller.BatPad.RoomChanged += RenderBatPad;
            RenderBatPad();

            if (controller.Mode == SessionMode.Online)
            {
                hintText.text = "Comparte el codigo para invitar. Las partidas online estaran disponibles proximamente.";
                RenderOnline();
                return;
            }
            hintText.text = "SPACE / A: join & ready     ESC / START: cancel & leave";
            controller.Players.SlotChanged += RenderSlot;
            RenderAll();
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
            batPadQr.gameObject.SetActive(batPadQrTexture != null);
            batPadText.gameObject.SetActive(usesPhones);
            batPadText.text = url != null ? "Scan to play with your phone" : "Connecting phone controller...";
        }

        private void Update()
        {
            if (controller.Players != null && controller.Mode == SessionMode.Online) RenderOnline();
        }

        private void RenderOnline()
        {
            var connection = CoreRoot.Instance.Online;
            if (!connection.IsConnected) return;
            var ids = new System.Collections.Generic.List<ulong>(connection.Connections);
            ids.Sort();

            // The host's phones fill the cards after the network connections.
            phoneSlots.Clear();
            foreach (var slot in controller.Players.Slots)
                if (slot.Input is BatPadInput) phoneSlots.Add(slot);

            string signature = connection.JoinCode + ":" + string.Join(",", ids) + ":" + PhoneSignature();
            if (signature == onlineSignature) return;
            onlineSignature = signature;
            // Same code for friends online and for the BatPad room of the phones.
            modeText.text = $"MULTIPLAYER {ids.Count + phoneSlots.Count}/4  |  CODIGO: {connection.JoinCode}";
            countdownText.gameObject.SetActive(false);
            for (int i = 0; i < slotViews.Length; i++)
            {
                int phoneIndex = i - ids.Count;
                if (i < ids.Count) slotViews[i].RenderConnection(ids[i], connection.LocalClientId);
                else if (phoneIndex < phoneSlots.Count) slotViews[i].RenderPhone(phoneSlots[phoneIndex].SlotIndex + 1);
                else slotViews[i].RenderConnection(null, connection.LocalClientId);
            }
        }

        private string PhoneSignature()
        {
            var text = new System.Text.StringBuilder();
            foreach (var slot in phoneSlots) text.Append(slot.SlotIndex).Append(',');
            return text.ToString();
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
