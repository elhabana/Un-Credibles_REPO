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
        public event Action<int, CharacterPartSlot, int> PartStepped; // slot index, part slot, -1 / +1

        // Outfit rows (helmet, outfit, boots), built here so the scene does not need them.
        // Compact, so the card covers as little as possible of the figure behind it.
        private const float OutfitRowsHeight = 72f;
        private const float RowHeight = 28f;
        private const float RowSpacing = 31f;
        private const float FirstRowY = 3f;
        private static readonly Color RowIdle = new Color(1f, 1f, 1f, .06f);
        private static readonly Color RowSelected = new Color(1f, .78f, .25f, .35f);
        private static readonly CharacterPartSlot[] PartSlots = { CharacterPartSlot.Helmet, CharacterPartSlot.Outfit, CharacterPartSlot.Boots };
        private static readonly string[] PartSlotKeys = { "lobby.part.helmet", "lobby.part.outfit", "lobby.part.boots" };
        private readonly Image[] rowBacks = new Image[PartSlots.Length];
        private readonly TMP_Text[] partLabels = new TMP_Text[PartSlots.Length];
        private readonly Button[] leftArrows = new Button[PartSlots.Length];
        private readonly Button[] rightArrows = new Button[PartSlots.Length];

        public void Setup(int slotIndex) => SlotIndex = slotIndex;

        private void Awake() => BuildOutfitRows();

        // The card grows upwards: the texts move up with its top, the buttons stay at the bottom
        // and the three rows fit in between.
        private void BuildOutfitRows()
        {
            var card = (RectTransform)transform;
            var buttons = new[] { addAIButton.transform, inviteButton.transform, removeButton.transform };
            foreach (RectTransform child in card)
                child.anchoredPosition += new Vector2(0f, Array.IndexOf(buttons, child) >= 0 ? -OutfitRowsHeight / 2f : OutfitRowsHeight / 2f);
            card.sizeDelta += new Vector2(0f, OutfitRowsHeight);
            card.anchoredPosition += new Vector2(0f, OutfitRowsHeight / 2f);

            for (int i = 0; i < PartSlots.Length; i++)
            {
                int row = i;
                var back = new GameObject("Outfit " + PartSlots[i], typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                back.SetParent(card, false);
                back.anchoredPosition = new Vector2(0f, FirstRowY - i * RowSpacing);
                back.sizeDelta = new Vector2(340f, RowHeight);
                rowBacks[i] = back.GetComponent<Image>();
                rowBacks[i].color = RowIdle;

                var label = Instantiate(nameText, back);
                label.rectTransform.anchoredPosition = Vector2.zero;
                label.rectTransform.sizeDelta = new Vector2(240f, RowHeight);
                label.fontSize = 17f;
                label.enableAutoSizing = true; label.fontSizeMin = 12f; label.fontSizeMax = 17f;
                label.alignment = TextAlignmentOptions.Center;
                partLabels[i] = label;

                leftArrows[i] = Arrow(back, "<", -142f, () => PartStepped?.Invoke(SlotIndex, PartSlots[row], -1));
                rightArrows[i] = Arrow(back, ">", 142f, () => PartStepped?.Invoke(SlotIndex, PartSlots[row], 1));
            }
        }

        // Same look as the card buttons.
        private Button Arrow(RectTransform parent, string text, float x, Action onClick)
        {
            var arrow = Instantiate(removeButton, parent);
            arrow.gameObject.SetActive(true);
            arrow.onClick = new Button.ButtonClickedEvent();
            arrow.onClick.AddListener(() => onClick());
            var rect = (RectTransform)arrow.transform;
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(44f, RowHeight);
            // The copied label is translated as "QUITAR": replace it with a plain one.
            foreach (var old in arrow.GetComponentsInChildren<TMP_Text>(true)) Destroy(old.gameObject);
            var label = Instantiate(nameText, rect);
            label.rectTransform.anchoredPosition = Vector2.zero;
            label.rectTransform.sizeDelta = rect.sizeDelta;
            label.enableAutoSizing = false;
            label.fontSize = 20f;
            label.text = text;
            return arrow;
        }

        // Helmet / outfit / boots of the player; arrows only for whoever may change them.
        public void RenderOutfit(LobbySlotInfo info, CharacterPartCatalog catalog, bool editable, int selectedRow)
        {
            bool show = info.Occupied && catalog != null;
            for (int i = 0; i < PartSlots.Length; i++)
            {
                rowBacks[i].gameObject.SetActive(show);
                if (!show) continue;
                string id = info.Loadout.Get(PartSlots[i]);
                var part = catalog.Find(id);
                string partName = part == null ? L10n.Get(L10n.UI, "lobby.part.none") : PartName(part);
                partLabels[i].text = $"{L10n.Get(L10n.UI, PartSlotKeys[i])}: {partName}";
                leftArrows[i].gameObject.SetActive(editable);
                rightArrows[i].gameObject.SetActive(editable);
                rowBacks[i].color = editable && i == selectedRow ? RowSelected : RowIdle;
            }
        }

        private static string PartName(CharacterPart part)
        {
            string key = "part." + part.Id;
            string text = L10n.Get(L10n.UI, key);
            return text == key ? part.DisplayName : text;
        }

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
