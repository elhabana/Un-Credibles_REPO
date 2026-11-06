using System;
using System.Collections.Generic;
using UnCredibles.Minigames;
using UnCredibles.Networking;
using UnCredibles.Players;
using UnCredibles.Players.Inputs;
using UnityEngine;

namespace UnCredibles.Core
{
    // Voting of a 3-round match: each ballot picks the next minigame until every round has one.
    // Offline and online host decide; an online client mirrors the host's ballot and sends its votes.
    public sealed class MatchVote : IDisposable
    {
        // The vote screen has three stops and the online vote message carries at most three choices.
        public const int VotedRounds = 3;
        private const float BallotSeconds = 12f;
        private const float LaunchDelay = 2f; // the decided route stays on screen before loading

        private readonly GameFlowManager flow;
        private readonly PlayerRegistry players;
        private readonly MatchManager match;
        private readonly MinigameManager minigames;
        private readonly RelayConnection online;
        private readonly OnlineSession room;
        private readonly Func<bool> startNextRound;

        private readonly List<MinigameData> votedMinigames = new List<MinigameData>(VotedRounds);
        private readonly List<MinigameData> candidates = new List<MinigameData>();
        private readonly Dictionary<int, int> votes = new Dictionary<int, int>();
        private readonly Dictionary<int, int> cursor = new Dictionary<int, int>();
        private readonly Dictionary<int, float> nextMove = new Dictionary<int, float>();
        private float deadline;
        private bool finalizing;
        private bool launchPending; // host / offline: start the first voted round once the deadline passes
        private int nextBallotId;
        private int ballotId;

        public MatchVote(GameFlowManager flow, PlayerRegistry players, MatchManager match, MinigameManager minigames,
            RelayConnection online, OnlineSession room, Func<bool> startNextRound)
        {
            this.flow = flow;
            this.players = players;
            this.match = match;
            this.minigames = minigames;
            this.online = online;
            this.room = room;
            this.startNextRound = startNextRound;
            room.VoteProgressReceived += HandleProgress;
            room.VoteRequested += HandleRequested;
        }

        public IReadOnlyList<MinigameData> Candidates => candidates;
        public IReadOnlyList<MinigameData> VotedMinigames => votedMinigames;
        public IReadOnlyDictionary<int, int> Votes => votes;
        public IReadOnlyDictionary<int, int> Cursor => cursor;
        public float SecondsLeft => Mathf.Max(0f, deadline - Time.unscaledTime);
        public bool CanChange => flow.State == GameState.Voting && !finalizing && SecondsLeft > 0f;
        public event Action Changed;

        private bool IsOnlineHost => flow.Session == SessionMode.Online && online.IsHost;
        private bool IsOnlineClient => flow.Session == SessionMode.Online && !online.IsHost;

        public void Dispose()
        {
            room.VoteProgressReceived -= HandleProgress;
            room.VoteRequested -= HandleRequested;
        }

        public void Clear()
        {
            ballotId = 0;
            candidates.Clear();
            votedMinigames.Clear();
            votes.Clear();
            cursor.Clear();
            nextMove.Clear();
            finalizing = false;
            launchPending = false;
            Changed?.Invoke();
        }

        // Host and clients must build the same list in the same order: votes travel as indices into it.
        public void BuildCandidates()
        {
            foreach (var data in minigames.Minigames)
                if (data != null && data.SupportsPlayerCount(players.OccupiedCount) && !candidates.Contains(data))
                    candidates.Add(data);
        }

        public void Begin()
        {
            ballotId = ++nextBallotId;
            votes.Clear();
            cursor.Clear();
            nextMove.Clear();
            deadline = Time.unscaledTime + BallotSeconds;
            if (IsOnlineHost) SendProgress(BallotSeconds);
            Changed?.Invoke();
        }

        // Offline and online host, every frame while voting.
        public void Update()
        {
            if (finalizing)
            {
                if (launchPending && Time.unscaledTime >= deadline)
                {
                    launchPending = false;
                    if (match.IsRunning) startNextRound();
                }
                return;
            }
            if (Time.unscaledTime >= deadline || EveryoneVoted()) { Finish(); return; }
            foreach (var slot in players.Slots)
            {
                if (!slot.IsOccupied || slot.IsAI || !slot.IsConnected || slot.Input == null) continue;
                if (IsOnlineHost && room.IsRemoteSlot(slot.SlotIndex)) continue;
                StepCursor(slot.PlayerId, slot.Input);
            }
        }

        // Online client, every frame while voting: this machine's player moves its cursor and votes.
        public void UpdateClient(IPlayerInput input)
        {
            if (input == null || candidates.Count == 0 || !CanChange) return;
            foreach (var slot in players.Slots)
            {
                if (!slot.IsOccupied || room.Slots[slot.SlotIndex].Owner != online.LocalClientId) continue;
                StepCursor(slot.PlayerId, input);
                break;
            }
        }

        public void Cast(int playerId, int candidateIndex)
        {
            if (!CanChange ||
                candidateIndex < 0 || candidateIndex >= candidates.Count || votedMinigames.Contains(candidates[candidateIndex])) return;
            if (!players.TryGetByPlayerId(playerId, out var slot) || slot.IsAI || !slot.IsConnected) return;
            if (IsOnlineClient)
            {
                if (room.Slots[slot.SlotIndex].Owner != online.LocalClientId) return;
                cursor[playerId] = candidateIndex;
                room.SendVote(ballotId, candidateIndex);
                Changed?.Invoke();
                return;
            }
            cursor[playerId] = candidateIndex;
            if (votes.TryGetValue(playerId, out int current) && current == candidateIndex)
            {
                Changed?.Invoke();
                return;
            }
            votes[playerId] = candidateIndex;
            if (IsOnlineHost) SendProgress(SecondsLeft);
            Changed?.Invoke();
        }

        // Left/right moves the player's cursor (with repeat delay); Jump votes for it.
        private void StepCursor(int playerId, IPlayerInput controller)
        {
            int selected = cursor.TryGetValue(playerId, out int current) ? current : FirstAvailable();
            float axis = controller.Move.x;
            if (Mathf.Abs(axis) > .55f && (!nextMove.TryGetValue(playerId, out float repeat) || Time.unscaledTime >= repeat))
            {
                selected = NextAvailable(selected, axis > 0 ? 1 : -1);
                cursor[playerId] = selected;
                nextMove[playerId] = Time.unscaledTime + .25f;
                Changed?.Invoke();
            }
            if (controller.WasPressed(PlayerAction.Jump)) Cast(playerId, selected);
        }

        // The ballot closes as soon as every connected human has voted; bots never vote.
        private bool EveryoneVoted()
        {
            int voters = 0;
            foreach (var slot in players.Slots)
            {
                if (!slot.IsOccupied || slot.IsAI || !slot.IsConnected) continue;
                if (!votes.ContainsKey(slot.PlayerId)) return false;
                voters++;
            }
            return voters > 0;
        }

        private int FirstAvailable() => NextAvailable(candidates.Count - 1, 1);

        private int NextAvailable(int current, int direction)
        {
            for (int step = 1; step <= candidates.Count; step++)
            {
                int index = (current + direction * step + candidates.Count * 2) % candidates.Count;
                if (!votedMinigames.Contains(candidates[index])) return index;
            }
            return 0;
        }

        // Most votes wins; a tie is broken at random among the tied candidates.
        private void Finish()
        {
            int best = -1;
            int bestCount = -1;
            int tied = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (votedMinigames.Contains(candidates[i])) continue;
                int count = 0;
                foreach (var choice in votes.Values) if (choice == i) count++;
                if (count > bestCount) { best = i; bestCount = count; tied = 1; }
                else if (count == bestCount && UnityEngine.Random.Range(0, ++tied) == 0) best = i;
            }
            if (best < 0) { startNextRound(); return; }
            votedMinigames.Add(candidates[best]);
            if (votedMinigames.Count >= match.TotalRounds)
            {
                finalizing = true;
                launchPending = true;
                deadline = Time.unscaledTime + LaunchDelay;
                if (IsOnlineHost) SendProgress(LaunchDelay);
                Changed?.Invoke();
            }
            else Begin();
        }

        private void HandleRequested(int requestBallotId, int playerId, int candidateIndex)
        {
            if (IsOnlineHost && requestBallotId == ballotId) Cast(playerId, candidateIndex);
        }

        // Online client: the host's ballot, mirrored.
        private void HandleProgress(int selectedCount, float seconds)
        {
            if (!IsOnlineClient || (flow.State != GameState.PartyLobby && flow.State != GameState.Voting)) return;
            bool newBallot = ballotId != room.VoteBallotId;
            if (newBallot && selectedCount == 0) candidates.Clear();
            if (candidates.Count == 0) BuildCandidates();
            ballotId = room.VoteBallotId;
            finalizing = !room.VoteIsOpen;
            deadline = Time.unscaledTime + seconds;
            // The host sends chosen indices in the same registered candidate order.
            votedMinigames.Clear();
            votes.Clear();
            if (newBallot)
            {
                cursor.Clear();
                nextMove.Clear();
            }
            foreach (int index in room.VoteChoices)
                if (index >= 0 && index < candidates.Count) votedMinigames.Add(candidates[index]);
            foreach (var vote in room.ConfirmedVotes) votes[vote.Key] = vote.Value;
            flow.ChangeState(GameState.Voting);
            Changed?.Invoke();
        }

        private void SendProgress(float seconds)
        {
            var indices = new List<int>(votedMinigames.Count);
            foreach (var data in votedMinigames) indices.Add(candidates.IndexOf(data));
            room.SendVoteProgress(ballotId, !finalizing, indices, votes, seconds);
        }
    }
}
