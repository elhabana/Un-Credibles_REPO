using System;
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
    // A screen-space route map over the garage (PF_MapVote, in SCN_MainMenu), drawn with the art bible's paper
    // kit: a taped title label, the turquoise timer pill, the 3 stops of the route as small paper cards and every
    // candidate as a tilted polaroid (PF_VoteCard). The Core owns the ballot and its result; this only shows it,
    // so online clients see the same through MatchVote.Changed.
    public sealed class MapVoteView : MonoBehaviour
    {
        private static readonly Color FadedPaper = new Color(.957f, .922f, .867f, .55f);
        private static readonly Color FadedInk = new Color(.114f, .114f, .106f, .45f);
        private static readonly Color Dimmed = new Color(.62f, .62f, .6f);
        private static readonly Color EmptyPhoto = new Color(.114f, .114f, .106f, .12f);
        private const int UrgentSeconds = 3;
        private const float CardTop = 90f;        // top edge of the cards, under the route
        private const float BadgeGap = 10f;
        private const float StampSeconds = .15f;

        [Serializable]
        internal sealed class Stop
        {
            public Image panel;
            public Image disc;
            public TMP_Text number;
            public TMP_Text label;
        }

        [Tooltip("The vote canvas; this object stays active to follow the game state and shows it only while voting.")]
        [SerializeField] internal GameObject root;
        [SerializeField] internal TMP_Text heading;
        [SerializeField] internal Image timerPill;
        [SerializeField] internal TMP_Text timer;
        [SerializeField] internal TMP_Text hint;
        [SerializeField] internal Stop[] stops = new Stop[MatchVote.VotedRounds];
        [Tooltip("Parent of the polaroids, stretched over the canvas: they are placed from its centre.")]
        [SerializeField] internal RectTransform cardContainer;
        [SerializeField] internal VoteCard cardPrefab;

        private CoreRoot core;
        private readonly List<VoteCard> cards = new List<VoteCard>();
        private int renderedCandidateCount = -1;
        private int shownSeconds = -1;
        private int shownRound = -1;

        private readonly bool[] votedBy = new bool[PlayerRegistry.MaxPlayers];
        private readonly bool[] hoveredBy = new bool[PlayerRegistry.MaxPlayers];
        // What the last Render showed per slot (-1 = nothing): a change is a move or a vote to play a sound for.
        private readonly int[] shownCursor = new int[PlayerRegistry.MaxPlayers];
        private readonly int[] shownVote = new int[PlayerRegistry.MaxPlayers];

        private IEnumerator Start()
        {
            while (CoreRoot.Instance == null) yield return null;
            core = CoreRoot.Instance;
            for (int i = 0; i < shownCursor.Length; i++) shownCursor[i] = shownVote[i] = -1;
            hint.text = L10n.Get(L10n.UI, "vote.hint");
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

        private void Render()
        {
            if (root == null || core == null || core.GameFlow.State != GameState.Voting) return;
            var candidates = core.Vote.Candidates;
            if (renderedCandidateCount != candidates.Count)
            {
                foreach (var card in cards) Destroy(card.gameObject);
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

        // One PF_VoteCard per candidate, sized to share the row and tilted alternately.
        private VoteCard CreateCard(int index, MinigameData data, int count)
        {
            var card = Instantiate(cardPrefab, cardContainer);
            card.name = "Destino " + data.DisplayName;
            float width = Mathf.Min(365f, 1540f / Mathf.Max(1, count) - 14f);
            float height = card.Layout(width);
            var rect = card.background.rectTransform;
            rect.anchoredPosition = new Vector2((index - (count - 1) * .5f) * (width + 22f), CardTop - height * .5f);
            rect.localEulerAngles = new Vector3(0f, 0f, index % 2 == 0 ? -2.5f : 2f);

            card.thumbnail.sprite = data.Thumbnail;
            card.title.text = MinigameName(data);
            card.routeLabel.text = L10n.Get(L10n.UI, "vote.in_route");
            for (int slot = 0; slot < card.badges.Length; slot++)
            {
                var badge = card.badges[slot];
                badge.label.text = PlayerIdentity.LabelFor(slot);
                badge.stampLabel.text = L10n.Get(L10n.UI, "vote.voted");
                badge.voted = false;
                badge.stampTime = -1f;
            }

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
            return card;
        }

        // Confirmed votes are solid discs with a stamp; a local player only pointing at the card gets a faded disc.
        private void LayoutBadges(VoteCard card)
        {
            const float BadgeSize = VoteCard.BadgeSize;
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
                badge.disc.rectTransform.anchoredPosition = new Vector2(x, -card.BadgeY);
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

        private static bool Contains(IReadOnlyList<MinigameData> list, MinigameData data)
        {
            foreach (var item in list) if (item == data) return true;
            return false;
        }
    }
}
