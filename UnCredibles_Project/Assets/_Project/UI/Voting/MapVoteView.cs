using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnCredibles.Core;
using UnCredibles.Minigames;
using UnCredibles.Players;
using UnityEngine;
using UnityEngine.UI;
using UnCredibles.Localization;

namespace UnCredibles.UI.Voting
{
    // A screen-space route map over the garage, drawn with the art bible's paper kit (PaperUI): a taped
    // title label, the turquoise timer pill, the 3 stops of the route as small paper cards and every
    // candidate as a tilted polaroid. The Core owns the ballot and its result; this only shows it, so
    // online clients see the same through MatchVote.Changed.
    public sealed class MapVoteView : MonoBehaviour
    {
        private static readonly Color Backdrop = new Color(.114f, .114f, .106f, .85f); // see-through ink
        private static readonly Color FadedPaper = new Color(.957f, .922f, .867f, .55f);
        private static readonly Color FadedInk = new Color(.114f, .114f, .106f, .45f);
        private static readonly Color Dimmed = new Color(.62f, .62f, .6f);
        private static readonly Color EmptyPhoto = new Color(.114f, .114f, .106f, .12f);
        private static readonly Vector2 Middle = new Vector2(.5f, .5f);
        private const int UrgentSeconds = 3;
        private static readonly Vector2 CardTopAnchor = new Vector2(.5f, 1f);
        // Polaroid, top to bottom: inset, photo (16:9), name, status, player discs, wider bottom margin.
        private const float CardTop = 90f;        // top edge of the cards, under the route
        private const float PhotoInset = 14f;
        private const float NameHeight = 52f;
        private const float StatusHeight = 30f;
        private const float BottomMargin = 34f;   // room for the vote stamp under the discs
        private const float StopWidth = 420f;
        private const float StopStep = 480f;
        private const float BadgeSize = 56f;
        private const float BadgeGap = 10f;
        private const float StampSeconds = .15f;

        // Bangers; set by GarageFrontend right after adding this component (falls back to TMP's default).
        public TMP_FontAsset TitleFont { get; set; }

        private CoreRoot core;
        private GameObject root;
        private TMP_Text heading;
        private Image timerPill;
        private TMP_Text timer;
        private TMP_Text hint;
        private readonly Stop[] stops = new Stop[MatchVote.VotedRounds];
        private readonly List<Card> cards = new List<Card>();
        private int renderedCandidateCount = -1;
        private int shownSeconds = -1;
        private int shownRound = -1;

        private sealed class Stop
        {
            public Image panel;
            public Image disc;
            public TMP_Text number;
            public TMP_Text label;
        }

        private sealed class Card
        {
            public Image background;
            public Image thumbnail;
            public TMP_Text title;
            public TMP_Text footer;
            public GameObject inRoute;
            public Button button;
            public float badgeY; // centre of the disc row, from the card's top edge
            public readonly Badge[] badges = new Badge[PlayerRegistry.MaxPlayers];
        }

        private sealed class Badge
        {
            public Image disc;
            public TMP_Text label;
            public RectTransform stamp;
            public bool voted;
            public float stampTime = -1f;
        }

        private readonly bool[] votedBy = new bool[PlayerRegistry.MaxPlayers];
        private readonly bool[] hoveredBy = new bool[PlayerRegistry.MaxPlayers];
        // What the last Render showed per slot (-1 = nothing): a change is a move or a vote to play a sound for.
        private readonly int[] shownCursor = new int[PlayerRegistry.MaxPlayers];
        private readonly int[] shownVote = new int[PlayerRegistry.MaxPlayers];

        private IEnumerator Start()
        {
            while (CoreRoot.Instance == null) yield return null;
            core = CoreRoot.Instance;
            Build();
            core.GameFlow.StateChanged += OnStateChanged;
            core.Vote.Changed += Render;
            L10n.LanguageChanged += HandleLanguageChanged;
            OnStateChanged(core.GameFlow.State, core.GameFlow.State);
        }

        private void OnDestroy()
        {
            if (core != null)
            {
                core.GameFlow.StateChanged -= OnStateChanged;
                core.Vote.Changed -= Render;
            }
            L10n.LanguageChanged -= HandleLanguageChanged;
        }

        // Cards carry their names from when they were built, so they are rebuilt in the new language.
        private void HandleLanguageChanged()
        {
            renderedCandidateCount = -1;
            Render();
        }

        private void Update()
        {
            if (root == null || !root.activeSelf || core == null) return;
            AnimateStamps(Time.unscaledDeltaTime);
            int seconds = Mathf.CeilToInt(core.Vote.SecondsLeft);
            if (seconds == shownSeconds) return;
            shownSeconds = seconds;
            timer.SetText("{0} s", seconds);
            timerPill.color = seconds <= UrgentSeconds ? PaperUI.Alarm : PaperUI.TimerTeal;
        }

        private void OnStateChanged(GameState previous, GameState current)
        {
            if (root == null) return;
            shownSeconds = -1; // a new ballot rewrites the timer on its first frame
            shownRound = -1;   // and its first render makes no sound
            root.SetActive(current == GameState.Voting);
            if (current == GameState.Voting) Render();
        }

        private void Build()
        {
            for (int i = 0; i < shownCursor.Length; i++) shownCursor[i] = shownVote[i] = -1;
            var canvas = PaperUI.Canvas("Mapa de votacion", transform, 200);
            root = canvas.gameObject;
            root.AddComponent<GraphicRaycaster>(); // the cards take mouse clicks

            var backdrop = new GameObject("Fondo", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            backdrop.transform.SetParent(root.transform, false);
            backdrop.color = Backdrop;
            backdrop.raycastTarget = false;
            PaperUI.Stretch(backdrop.rectTransform);

            var label = PaperUI.Panel("Titulo", root.transform, PaperUI.Paper, -1.5f, true);
            Place(label.rectTransform, 0f, 440f, 1150f, 110f);
            heading = Label("Texto", label.transform, TitleFont, 76f, 40f, PaperUI.Ink);
            PaperUI.Stretch(heading.rectTransform, 14f);

            timerPill = PaperUI.Pill("Tiempo", root.transform, PaperUI.TimerTeal, 72f);
            Place(timerPill.rectTransform, 0f, 330f, 210f, 72f);
            timer = Label("Texto", timerPill.transform, TitleFont, 56f, 30f, PaperUI.Paper);
            PaperUI.Stretch(timer.rectTransform, 6f);

            // The road joins the 3 stops; drawn first so the cards sit on it.
            var road = new GameObject("Camino", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            road.transform.SetParent(root.transform, false);
            road.color = PaperUI.Ink;
            road.raycastTarget = false;
            Place(road.rectTransform, 0f, 222f, StopStep * 2f, 8f);
            for (int i = 0; i < stops.Length; i++) stops[i] = CreateStop(i);

            var footer = PaperUI.Pill("Instrucciones", root.transform, PaperUI.Paper, 60f);
            Place(footer.rectTransform, 0f, -478f, 1560f, 60f);
            hint = Label("Texto", footer.transform, TitleFont, 30f, 16f, PaperUI.Ink);
            PaperUI.Stretch(hint.rectTransform, 10f);
            hint.rectTransform.offsetMin = new Vector2(30f, 6f);
            hint.rectTransform.offsetMax = new Vector2(-30f, -6f);
            hint.text = L10n.Get(L10n.UI, "vote.hint");
            root.SetActive(false);
        }

        private Stop CreateStop(int index)
        {
            var stop = new Stop();
            stop.panel = PaperUI.Panel("Parada " + (index + 1), root.transform, PaperUI.Paper, index % 2 == 0 ? -1.5f : 1.5f);
            Place(stop.panel.rectTransform, (index - 1) * StopStep, 222f, StopWidth, 90f);
            stop.disc = PaperUI.Disc("Numero", stop.panel.transform, PaperUI.Ink);
            PaperUI.Place(stop.disc.rectTransform, new Vector2(0f, .5f), new Vector2(14f, 0f), new Vector2(62f, 62f));
            stop.number = Label("Texto", stop.disc.transform, TitleFont, 38f, 20f, PaperUI.Paper);
            PaperUI.Stretch(stop.number.rectTransform);
            stop.number.text = (index + 1).ToString();
            stop.label = Label("Nombre", stop.panel.transform, TitleFont, 34f, 18f, PaperUI.Ink);
            PaperUI.Stretch(stop.label.rectTransform, 10f);
            stop.label.rectTransform.offsetMin = new Vector2(88f, 10f);
            return stop;
        }

        private void Render()
        {
            if (root == null || core == null || core.GameFlow.State != GameState.Voting) return;
            var candidates = core.Vote.Candidates;
            if (renderedCandidateCount != candidates.Count)
            {
                foreach (var card in cards) Destroy(card.background.gameObject);
                cards.Clear();
                renderedCandidateCount = candidates.Count;
                for (int i = 0; i < candidates.Count; i++) cards.Add(CreateCard(i, candidates[i], candidates.Count));
            }

            int round = core.Vote.VotedMinigames.Count;
            heading.text = round >= MatchVote.VotedRounds ? L10n.Get(L10n.UI, "vote.route_decided") : L10n.Format(L10n.UI, "vote.round", round + 1, MatchVote.VotedRounds);
            for (int i = 0; i < stops.Length; i++) RenderStop(stops[i], i, round);
            PlayFeedback(round);

            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                bool picked = Contains(core.Vote.VotedMinigames, candidates[i]);
                int votes = 0;
                int selectors = 0;
                for (int slot = 0; slot < votedBy.Length; slot++)
                {
                    votedBy[slot] = !picked && shownVote[slot] == i;
                    hoveredBy[slot] = !picked && shownCursor[slot] == i;
                    if (votedBy[slot]) votes++;
                    if (hoveredBy[slot]) selectors++;
                }
                LayoutBadges(card);
                // Pointed at by a local player: the photo lifts a little off the table.
                float lift = selectors > 0 ? 1.06f : 1f;
                card.background.rectTransform.localScale = new Vector3(lift, lift, 1f);
                card.background.color = picked ? Dimmed : Color.white;
                card.thumbnail.color = card.thumbnail.sprite == null ? EmptyPhoto : picked ? Dimmed : Color.white;
                card.title.color = picked ? FadedInk : PaperUI.Ink;
                card.inRoute.SetActive(picked);
                card.footer.gameObject.SetActive(!picked);
                card.footer.text = votes == 1 ? L10n.Get(L10n.UI, "vote.one_vote") : votes > 1 ? L10n.Format(L10n.UI, "vote.votes", votes) : L10n.Get(L10n.UI, "vote.available");
                card.button.interactable = !picked && core.Vote.CanChange;
            }
            hint.text = round >= MatchVote.VotedRounds ? L10n.Get(L10n.UI, "vote.route_decided") :
                L10n.Get(L10n.UI, "vote.hint_full");
        }

        // Done: paper with a turquoise number and the chosen name. Current: white, ink number, a bit bigger.
        // Pending: faded paper.
        private void RenderStop(Stop stop, int index, int round)
        {
            bool done = index < round;
            bool current = index == round;
            stop.label.text = done ? MinigameName(core.Vote.VotedMinigames[index]) :
                current ? L10n.Get(L10n.UI, "vote.voting") : L10n.Get(L10n.UI, "vote.undecided");
            stop.panel.color = done ? PaperUI.Paper : current ? Color.white : FadedPaper;
            stop.disc.color = done ? PaperUI.TimerTeal : current ? PaperUI.Ink : FadedPaper;
            stop.number.color = done || current ? PaperUI.Paper : FadedInk;
            stop.label.color = done || current ? PaperUI.Ink : FadedInk;
            float scale = current ? 1.08f : 1f;
            stop.panel.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        // Works out every slot's cursor and confirmed vote, and sounds the change against the last render.
        // Deduced from what is shown, so an online client hears it too. A new ballot starts silent.
        private void PlayFeedback(int round)
        {
            bool sameBallot = round == shownRound;
            shownRound = round;
            bool moved = false;
            bool confirmed = false;
            int first = FirstAvailable();
            foreach (var slot in core.Players.Slots)
            {
                int index = slot.SlotIndex;
                int vote = -1;
                int cursor = -1;
                if (slot.IsOccupied && !slot.IsAI)
                {
                    if (core.Vote.Votes.TryGetValue(slot.PlayerId, out int choice)) vote = choice;
                    if (IsLocalVoter(slot)) cursor = core.Vote.Cursor.TryGetValue(slot.PlayerId, out int at) ? at : first;
                }
                if (sameBallot)
                {
                    // A cursor that appears or disappears (voter joined, left or became a bot) is not a move.
                    moved |= cursor >= 0 && shownCursor[index] >= 0 && cursor != shownCursor[index];
                    confirmed |= vote >= 0 && vote != shownVote[index];
                }
                shownCursor[index] = cursor;
                shownVote[index] = vote;
            }
            if (confirmed) GameAudio.Play(GameSfx.UiConfirm);
            else if (moved) GameAudio.Play(GameSfx.UiMove);
        }

        private bool IsLocalVoter(UnCredibles.Players.PlayerSlot slot)
        {
            if (!slot.IsOccupied || slot.IsAI || !slot.IsConnected) return false;
            if (core.GameFlow.Session == SessionMode.Local) return true;
            return core.Online.IsHost ? !core.Room.IsRemoteSlot(slot.SlotIndex) :
                core.Room.Slots[slot.SlotIndex].Owner == core.Online.LocalClientId;
        }

        private int FirstAvailable()
        {
            for (int i = 0; i < core.Vote.Candidates.Count; i++)
                if (!Contains(core.Vote.VotedMinigames, core.Vote.Candidates[i])) return i;
            return 0;
        }

        // Polaroid: white frame, the thumbnail as the photo, the name written on the bottom margin.
        private Card CreateCard(int index, MinigameData data, int count)
        {
            var card = new Card();
            float width = Mathf.Min(365f, 1540f / Mathf.Max(1, count) - 14f);
            float photoWidth = width - PhotoInset * 2f;
            float photoHeight = photoWidth * 9f / 16f;
            // Height fits the content (y = distance from the card's top edge to each row's centre).
            float nameY = PhotoInset + photoHeight + NameHeight * .5f;
            float statusY = nameY + (NameHeight + StatusHeight) * .5f;
            card.badgeY = statusY + StatusHeight * .5f + 4f + BadgeSize * .5f;
            float height = card.badgeY + BadgeSize * .5f + BottomMargin;

            float tilt = index % 2 == 0 ? -2.5f : 2f;
            card.background = PaperUI.Panel("Destino " + data.DisplayName, root.transform, Color.white, tilt, true);
            card.background.raycastTarget = true;
            Place(card.background.rectTransform, (index - (count - 1) * .5f) * (width + 22f), CardTop - height * .5f, width, height);
            card.button = card.background.gameObject.AddComponent<Button>();
            int selectedIndex = index;
            card.button.onClick.AddListener(() =>
            {
                UnCredibles.Players.PlayerSlot voter = null;
                foreach (var slot in core.Players.Slots)
                {
                    if (!IsLocalVoter(slot)) continue;
                    if (voter == null) voter = slot;
                    if (!core.Vote.Votes.ContainsKey(slot.PlayerId)) { voter = slot; break; }
                }
                if (voter != null) core.Vote.Cast(voter.PlayerId, selectedIndex);
            });

            card.thumbnail = new GameObject("Miniatura", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            card.thumbnail.transform.SetParent(card.background.transform, false);
            card.thumbnail.raycastTarget = false;
            card.thumbnail.preserveAspect = true;
            card.thumbnail.sprite = data.Thumbnail;
            PlaceFromTop(card.thumbnail.rectTransform, 0f, PhotoInset + photoHeight * .5f, photoWidth, photoHeight);

            card.title = Label("Nombre", card.background.transform, TitleFont, 46f, 24f, PaperUI.Ink);
            card.title.text = MinigameName(data);
            PlaceFromTop(card.title.rectTransform, 0f, nameY, width - 30f, NameHeight);
            card.footer = Label("Votos", card.background.transform, TitleFont, 28f, 16f, PaperUI.TimerTeal);
            PlaceFromTop(card.footer.rectTransform, 0f, statusY, width - 30f, StatusHeight);

            for (int slot = 0; slot < card.badges.Length; slot++)
                card.badges[slot] = CreateBadge(card.background.transform, slot, card.badgeY);
            card.inRoute = CreateInRouteStamp(card.background.transform, width, height);
            return card;
        }

        // "EN LA RUTA": an ink line crossing the photo out and a red rubber stamp over it.
        private GameObject CreateInRouteStamp(Transform parent, float width, float height)
        {
            var group = new GameObject("En la ruta", typeof(RectTransform));
            group.transform.SetParent(parent, false);
            PaperUI.Stretch((RectTransform)group.transform);
            var strike = new GameObject("Tachon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            strike.transform.SetParent(group.transform, false);
            strike.color = PaperUI.Ink;
            strike.raycastTarget = false;
            Place(strike.rectTransform, 0f, 0f, Mathf.Sqrt(width * width + height * height) - 20f, 10f);
            strike.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(height, width) * Mathf.Rad2Deg);
            var stamp = PaperUI.Pill("Sello", group.transform, PaperUI.Alarm, 72f);
            Place(stamp.rectTransform, 0f, 30f, width - 20f, 72f);
            stamp.rectTransform.localEulerAngles = new Vector3(0f, 0f, -16f);
            var label = Label("Texto", stamp.transform, TitleFont, 48f, 24f, PaperUI.Paper);
            PaperUI.Stretch(label.rectTransform, 8f);
            label.text = L10n.Get(L10n.UI, "vote.in_route");
            group.SetActive(false);
            return group;
        }

        // Player disc with the tag ("J1"); a confirmed vote also gets a small red stamp.
        private Badge CreateBadge(Transform parent, int slotIndex, float y)
        {
            var badge = new Badge();
            badge.disc = PaperUI.Disc("Ficha P" + (slotIndex + 1), parent, PlayerIdentity.ColorFor(slotIndex));
            PlaceFromTop(badge.disc.rectTransform, 0f, y, BadgeSize, BadgeSize);
            badge.label = Label("Texto", badge.disc.transform, TitleFont, 30f, 16f, PaperUI.Ink);
            PaperUI.Stretch(badge.label.rectTransform, 4f);
            badge.label.text = PlayerIdentity.LabelFor(slotIndex);
            var stamp = PaperUI.Pill("Sello", badge.disc.transform, PaperUI.Alarm, 26f);
            badge.stamp = stamp.rectTransform;
            Place(badge.stamp, 0f, -30f, 96f, 26f);
            badge.stamp.localEulerAngles = new Vector3(0f, 0f, -12f);
            var stampLabel = Label("Texto", badge.stamp, TitleFont, 20f, 10f, PaperUI.Paper);
            PaperUI.Stretch(stampLabel.rectTransform, 2f);
            stampLabel.text = L10n.Get(L10n.UI, "vote.voted");
            badge.stamp.gameObject.SetActive(false);
            badge.disc.gameObject.SetActive(false);
            return badge;
        }

        // Confirmed votes are solid discs with a stamp; a local player only pointing at the card gets a faded disc.
        private void LayoutBadges(Card card)
        {
            int visible = 0;
            for (int i = 0; i < card.badges.Length; i++)
                if (votedBy[i] || hoveredBy[i]) visible++;
            float x = -(visible - 1) * (BadgeSize + BadgeGap) * .5f;
            for (int i = 0; i < card.badges.Length; i++)
            {
                var badge = card.badges[i];
                bool shown = votedBy[i] || hoveredBy[i];
                badge.disc.gameObject.SetActive(shown);
                bool voted = shown && votedBy[i];
                badge.stamp.gameObject.SetActive(voted);
                if (voted && !badge.voted)
                {
                    badge.stampTime = 0f;
                    badge.stamp.localScale = new Vector3(2.2f, 2.2f, 1f);
                }
                badge.voted = voted;
                if (!shown) continue;
                Color color = PlayerIdentity.ColorFor(i);
                if (!voted) color.a = .45f;
                badge.disc.color = color;
                badge.label.color = voted ? PaperUI.Ink : FadedInk;
                badge.disc.rectTransform.anchoredPosition = new Vector2(x, -card.badgeY);
                x += BadgeSize + BadgeGap;
            }
        }

        // The vote stamp slams down: from big to its size in a moment.
        private void AnimateStamps(float dt)
        {
            for (int c = 0; c < cards.Count; c++)
            {
                var badges = cards[c].badges;
                for (int i = 0; i < badges.Length; i++)
                {
                    var badge = badges[i];
                    if (badge.stampTime < 0f) continue;
                    badge.stampTime += dt;
                    float t = Mathf.Clamp01(badge.stampTime / StampSeconds);
                    float scale = Mathf.Lerp(2.2f, 1f, t);
                    badge.stamp.localScale = new Vector3(scale, scale, 1f);
                    if (t >= 1f) badge.stampTime = -1f;
                }
            }
        }

        // Name from the Minigames table by id; the asset's DisplayName if the table has no entry.
        private static string MinigameName(MinigameData data)
        {
            string key = "name." + data.Id;
            string text = L10n.Get(L10n.Minigames, key);
            return text == key ? data.DisplayName : text;
        }

        // Centred, auto-sized label of the paper kit.
        private static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, float size, float min, Color color)
        {
            var label = PaperUI.Text(name, parent, font, size, color, TextAlignmentOptions.Center);
            label.enableAutoSizing = true;
            label.fontSizeMin = min;
            label.fontSizeMax = size;
            return label;
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height) =>
            PaperUI.Place(rect, Middle, new Vector2(x, y), new Vector2(width, height));

        // Centred on a point y below the parent's top edge (cards grow to fit, so their rows hang from the top).
        private static void PlaceFromTop(RectTransform rect, float x, float y, float width, float height)
        {
            PaperUI.Place(rect, CardTopAnchor, new Vector2(x, -y), new Vector2(width, height));
            rect.pivot = Middle;
        }

        private static bool Contains(IReadOnlyList<MinigameData> list, MinigameData data)
        {
            foreach (var item in list) if (item == data) return true;
            return false;
        }
    }
}
