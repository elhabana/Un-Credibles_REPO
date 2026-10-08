using TMPro;
using UnCredibles.BatPad;
using UnCredibles.Players;
using UnityEngine;
using UnityEngine.UI;

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

        private bool locked;
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
            controller.SlotsChanged += RenderAll;
            controller.CountdownTick += HandleCountdownTick;
            controller.CountdownCancelled += HandleCountdownCancelled;
            controller.Notice += ShowNotice;
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
            if (controller.UsesPhones) controller.BatPad.RoomChanged += RenderBatPad;
            RenderBatPad();
            hintText.text = !controller.IsOnline
                ? "SPACE / A: join & ready     ESC / START: cancel & leave"
                : controller.IsAuthority
                    ? "Comparte el codigo para invitar.   SPACE / A: listo"
                    : "SPACE / A: listo     ESC / B: cancelar";
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

            // Same code for friends online and for the BatPad room of the phones.
            modeText.text = controller.IsOnline
                ? $"MULTIPLAYER {occupied}/4  |  CODIGO: {controller.RoomCode}"
                : "SINGLEPLAYER";
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

        private void HandleCountdownCancelled()
        {
            countdownText.gameObject.SetActive(false);
            if (!locked) return;
            locked = false;
            RenderAll();
        }

        private void ShowNotice(string message) => hintText.text = message;

        private void HandleAddAI(int slotIndex) => controller.AddAI(slotIndex);
        private void HandleInvite(int slotIndex) => controller.InviteFriend(slotIndex);
        private void HandleRemove(int slotIndex) => controller.RemovePlayer(slotIndex);
    }
}
