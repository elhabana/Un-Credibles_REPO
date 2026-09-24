using System;
using System.Collections.Generic;
using UnCredibles.Players;
using UnCredibles.Players.Inputs;
using UnityEngine;

namespace UnCredibles.Minigames.Churro
{
    // Players on floats around a kid spinning a churro. Jump to dodge it; if it hits you, you are out
    // for the round. Each round ends when one player is left. Points per round = players that fell
    // before you, so after all rounds whoever lasted longest wins.
    // The only Update of the minigame: spinner, AI and avatars are ticked from here in a fixed order.
    public sealed class ChurroController : MinigameController
    {
        private enum RoundPhase { None, Intro, Spinning, Outro }

        [SerializeField] private ChurroSettings settings;
        [SerializeField] private ChurroSpinner spinner;
        [SerializeField] private ChurroPlayer playerPrefab;
        [SerializeField] private Transform playersParent;

        private readonly List<ChurroPlayer> avatars = new List<ChurroPlayer>(PlayerRegistry.MaxPlayers);
        private readonly List<ChurroAIBrain> brains = new List<ChurroAIBrain>(PlayerRegistry.MaxPlayers);
        private readonly List<ChurroPlayer> hitThisFrame = new List<ChurroPlayer>(PlayerRegistry.MaxPlayers);
        private RoundPhase phase;
        private int roundIndex;
        private int fallenThisRound;
        private float phaseTimer;
        private float hitHalfAngle;

        public int CurrentRound => roundIndex + 1;
        public int TotalRounds => settings.RoundCount;

        public event Action<int, int> RoundStarted;                 // round, total
        public event Action<int, IReadOnlyList<ChurroPlayer>> RoundEnded; // round, survivors

        protected override void OnInitialize(MinigameContext context)
        {
            float floatDistance = 0f;
            foreach (var player in Players)
            {
                var avatar = Spawns.Spawn(playerPrefab, player, playersParent);
                avatar.Setup(player, settings, spinner.Center, settings.GetPlayerColor(player.SlotIndex));
                avatars.Add(avatar);
                floatDistance = Vector3.Distance(Flat(avatar.transform.position), Flat(spinner.Center));
            }

            // Angular half width of a player seen from the centre.
            hitHalfAngle = floatDistance > 0.01f ? Mathf.Atan2(settings.PlayerRadius, floatDistance) * Mathf.Rad2Deg : 10f;

            foreach (var avatar in avatars)
                if (avatar.Slot.Input is AIInput aiInput)
                    brains.Add(new ChurroAIBrain(avatar, aiInput, spinner, settings, hitHalfAngle));

            spinner.ResetForRound(settings.GetRound(0), 45f);
        }

        protected override void OnGameStarted() => BeginRound(0);

        private void Update()
        {
            if (!IsPlaying) return;
            float deltaTime = Time.deltaTime;

            switch (phase)
            {
                case RoundPhase.Intro:
                    TickAvatars(deltaTime, false);
                    if ((phaseTimer -= deltaTime) <= 0f) phase = RoundPhase.Spinning;
                    break;

                case RoundPhase.Spinning:
                    spinner.Tick(deltaTime);
                    foreach (var brain in brains) brain.Tick(); // before avatars read their input
                    TickAvatars(deltaTime, true);
                    CheckHits();
                    break;

                case RoundPhase.Outro:
                    TickAvatars(deltaTime, false);
                    if ((phaseTimer -= deltaTime) <= 0f) NextRoundOrFinish();
                    break;
            }
        }

        private void BeginRound(int index)
        {
            roundIndex = index;
            fallenThisRound = 0;
            foreach (var avatar in avatars) avatar.ResetOnFloat();

            // Start with the churro between two floats so nobody is hit instantly.
            spinner.ResetForRound(settings.GetRound(index), 45f);
            phase = RoundPhase.Intro;
            phaseTimer = settings.RoundIntroSeconds;
            RoundStarted?.Invoke(CurrentRound, TotalRounds);
        }

        private void TickAvatars(float deltaTime, bool canJump)
        {
            foreach (var avatar in avatars) avatar.Tick(deltaTime, canJump);
        }

        private void CheckHits()
        {
            hitThisFrame.Clear();
            foreach (var avatar in avatars)
                if (avatar.IsIn && avatar.FeetHeight < settings.ClearHeight && spinner.SweptThrough(avatar.Angle, hitHalfAngle))
                    hitThisFrame.Add(avatar);

            // Players hit in the same frame share the same result.
            foreach (var avatar in hitThisFrame)
            {
                Score.AddScore(avatar.Slot.PlayerId, fallenThisRound);
                avatar.KnockOut(avatar.transform.position - spinner.Center);
            }
            fallenThisRound += hitThisFrame.Count;

            int remaining = CountIn();
            int stopAt = avatars.Count > 1 ? 1 : 0;
            if (remaining <= stopAt) EndRound();
        }

        private void EndRound()
        {
            var survivors = new List<ChurroPlayer>(1);
            foreach (var avatar in avatars)
            {
                if (!avatar.IsIn) continue;
                Score.AddScore(avatar.Slot.PlayerId, fallenThisRound);
                survivors.Add(avatar);
            }

            phase = RoundPhase.Outro;
            phaseTimer = settings.RoundOutroSeconds;
            RoundEnded?.Invoke(CurrentRound, survivors);
        }

        private void NextRoundOrFinish()
        {
            if (roundIndex + 1 < settings.RoundCount) BeginRound(roundIndex + 1);
            else
            {
                phase = RoundPhase.None;
                EndGame(MinigameEndReason.ObjectiveCompleted);
            }
        }

        private int CountIn()
        {
            int count = 0;
            foreach (var avatar in avatars)
                if (avatar.IsIn) count++;
            return count;
        }

        private static Vector3 Flat(Vector3 value) => new Vector3(value.x, 0f, value.z);
    }
}
