using System;
using System.Collections.Generic;
using System.IO;
using UnCredibles.Players;
using UnCredibles.Players.Inputs;
using UnityEngine;

namespace UnCredibles.Minigames.MowTheLawn
{
    // Mow the lawn: every mower cuts grass and fills bags that it drags behind like a tail.
    // Bags only score when unloaded into your own bin, in your corner of the garden.
    // Driving into a rival's tail knocks those bags loose; ramming a rival with the turbo makes it
    // drop bags and spin. In the final frenzy all grass grows back and every bag is worth more.
    // The only Update of the minigame: lawn, AI, mowers, bins and bags are ticked from here in order.
    public sealed class MowTheLawnController : MinigameController
    {
        private const byte BagDeliveredEvent = 1;
        private const byte FrenzyStartedEvent = 2;

        [SerializeField] private MowTheLawnSettings settings;
        [SerializeField] private MowLawn lawn;
        [SerializeField] private MowBags bags;
        [SerializeField] private MowerPlayer mowerPrefab;
        [SerializeField] private MowBin binPrefab;
        [SerializeField] private Transform playersParent;

        private readonly List<MowerPlayer> mowers = new List<MowerPlayer>(PlayerRegistry.MaxPlayers);
        private readonly List<MowBin> bins = new List<MowBin>(PlayerRegistry.MaxPlayers); // same order as mowers
        private readonly List<MowerAIBrain> brains = new List<MowerAIBrain>(PlayerRegistry.MaxPlayers);
        private readonly List<Vector3> dropped = new List<Vector3>(32);
        private readonly float[] cutCooldowns = new float[PlayerRegistry.MaxPlayers];
        private readonly float[] unloadTimers = new float[PlayerRegistry.MaxPlayers];

        public bool IsFrenzy { get; private set; }
        public float TimeLeft => Timer != null && Timer.IsRunning ? Timer.Remaining : float.MaxValue;
        public event Action FrenzyStarted;

        protected override void OnInitialize(MinigameContext context)
        {
            lawn.Initialize(settings);
            bags.Initialize(settings, lawn);

            foreach (var player in Players)
            {
                var bin = Instantiate(binPrefab, CornerOf(player.SlotIndex), Quaternion.identity, playersParent);
                bin.name = $"Bin_{player.SlotIndex}";
                bin.Setup(player.SlotIndex, settings.GetPlayerColor(player.SlotIndex));
                bins.Add(bin);

                var mower = Spawns.Spawn(mowerPrefab, player, playersParent);
                // Start facing the middle of the garden.
                var toCenter = lawn.Center - mower.transform.position;
                toCenter.y = 0f;
                if (toCenter.sqrMagnitude > 0.01f) mower.transform.rotation = Quaternion.LookRotation(toCenter);
                mower.Setup(player, settings, bags, settings.GetPlayerColor(player.SlotIndex));
                mowers.Add(mower);
            }

            if (!IsReplica) EnsureBrains();
        }

        protected override void OnGameStarted() { }

        // Slot 0 bottom-left, 1 bottom-right, 2 top-left, 3 top-right, like the spawn points.
        private Vector3 CornerOf(int slotIndex)
        {
            float x = lawn.HalfWidth - settings.BinInset;
            float z = lawn.HalfDepth - settings.BinInset;
            return lawn.Center + new Vector3(slotIndex % 2 == 0 ? -x : x, 0f, slotIndex < 2 ? -z : z);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            lawn.Tick(deltaTime);
            bags.Tick(deltaTime, !IsReplica);
            foreach (var bin in bins) bin.Tick(deltaTime);

            if (IsReplica)
            {
                // Online client: mowers follow the host; the grass is cut locally from their path.
                foreach (var mower in mowers) mower.TickRemote(deltaTime, lawn);
                return;
            }
            if (!IsPlaying) return;
            EnsureBrains(); // an online player who left is now a bot
            if (!IsFrenzy && TimeLeft <= settings.FrenzySeconds) StartFrenzy();

            foreach (var brain in brains) brain.Tick(deltaTime); // before mowers read their input
            foreach (var mower in mowers) mower.Tick(deltaTime, lawn);

            CollideMowers();
            CollideWithBins();
            CutTails(deltaTime);
            PickUpLooseBags();
            UnloadIntoBins(deltaTime);
        }

        // ---------- Rules ----------

        // Mowers bounce off each other. With the turbo on, hitting a rival in front is a ram.
        private void CollideMowers()
        {
            float minDistance = settings.MowerRadius * 2f;
            for (int a = 0; a < mowers.Count; a++)
            {
                for (int b = a + 1; b < mowers.Count; b++)
                {
                    var offset = mowers[b].Position - mowers[a].Position;
                    offset.y = 0f;
                    float distance = offset.magnitude;
                    if (distance >= minDistance) continue;

                    var normal = distance > 0.001f ? offset / distance : Vector3.right;
                    bool rammed = TryRam(mowers[a], mowers[b], normal) | TryRam(mowers[b], mowers[a], -normal);
                    var push = normal * ((minDistance - distance) * 0.5f);
                    var knock = normal * (rammed ? settings.RamKnockForce : settings.BumpForce);
                    mowers[a].Bump(lawn.ClampInside(mowers[a].Position - push, settings.MowerRadius), -knock);
                    mowers[b].Bump(lawn.ClampInside(mowers[b].Position + push, settings.MowerRadius), knock);
                }
            }
        }

        private bool TryRam(MowerPlayer attacker, MowerPlayer victim, Vector3 toVictim)
        {
            if (!attacker.IsBoosting || !victim.CanBeRammed) return false;
            if (Vector3.Angle(attacker.Forward, toVictim) > settings.RamAngle) return false;

            int lost = Mathf.Min(settings.RamBagsLost, victim.BagCount);
            if (lost > 0)
            {
                dropped.Clear();
                victim.CutTailAt(victim.BagCount - lost, dropped);
                foreach (var position in dropped) bags.Drop(position);
            }
            victim.Stun(settings.StunSeconds, settings.RamImmunitySeconds);
            return true;
        }

        // Bins are solid: push mowers out of them.
        private void CollideWithBins()
        {
            float minDistance = settings.BinRadius + settings.MowerRadius;
            foreach (var mower in mowers)
            {
                foreach (var bin in bins)
                {
                    var offset = mower.Position - bin.Position;
                    offset.y = 0f;
                    float distance = offset.magnitude;
                    if (distance >= minDistance) continue;
                    var normal = distance > 0.001f ? offset / distance : Vector3.forward;
                    mower.Bump(lawn.ClampInside(bin.Position + normal * minDistance, settings.MowerRadius), Vector3.zero);
                }
            }
        }

        // A mower touching a bag of another tail cuts it there: the rest of that tail falls loose.
        // Short cooldowns keep it from turning into a chain reaction.
        private void CutTails(float deltaTime)
        {
            float reach = settings.MowerRadius + settings.BagRadius;
            for (int a = 0; a < mowers.Count; a++)
            {
                if (cutCooldowns[a] > 0f)
                {
                    cutCooldowns[a] -= deltaTime;
                    continue;
                }
                var attacker = mowers[a];
                foreach (var victim in mowers)
                {
                    if (victim == attacker || victim.IsTailProtected) continue;
                    var tail = victim.BagPositions;
                    for (int i = 0; i < tail.Count; i++)
                    {
                        var offset = tail[i] - attacker.Position;
                        offset.y = 0f;
                        if (offset.sqrMagnitude > reach * reach) continue;

                        dropped.Clear();
                        victim.CutTailAt(i, dropped);
                        foreach (var position in dropped) bags.Drop(position);
                        cutCooldowns[a] = settings.CutCooldown;
                        break;
                    }
                }
            }
        }

        private void PickUpLooseBags()
        {
            foreach (var mower in mowers)
            {
                if (mower.IsStunned) continue;
                int picked = bags.PickUp(mower.Position, settings.MowerRadius);
                if (picked > 0) mower.AddBags(picked);
            }
        }

        // Next to your own bin, bags fly in one by one. Only bags in the bin count as points.
        private void UnloadIntoBins(float deltaTime)
        {
            for (int i = 0; i < mowers.Count; i++)
            {
                var mower = mowers[i];
                var offset = mower.Position - bins[i].Position;
                offset.y = 0f;
                if (mower.BagCount == 0 || offset.sqrMagnitude > settings.DeliverRadius * settings.DeliverRadius)
                {
                    unloadTimers[i] = 0f;
                    continue;
                }

                unloadTimers[i] -= deltaTime;
                if (unloadTimers[i] > 0f) continue;
                unloadTimers[i] = settings.UnloadInterval;

                var from = mower.TakeLastBag();
                bags.Throw(from, bins[i].MouthPosition, mower.Color);
                bins[i].Bounce();
                Score.AddScore(mower.Slot.PlayerId, IsFrenzy ? settings.FrenzyPointsPerBag : 1);

                var message = BeginEvent(BagDeliveredEvent);
                message.Write((byte)i);
                SendEvent();
            }
        }

        private void StartFrenzy()
        {
            IsFrenzy = true;
            ApplyFrenzy();
            BeginEvent(FrenzyStartedEvent);
            SendEvent();
        }

        private void ApplyFrenzy()
        {
            lawn.RegrowAll();
            foreach (var mower in mowers) mower.SpeedMultiplier = settings.FrenzySpeed;
            FrenzyStarted?.Invoke();
        }

        // ---------- AI ----------

        // Every bot gets a brain, including an online player replaced by AI mid-match.
        private void EnsureBrains()
        {
            for (int i = 0; i < mowers.Count; i++)
            {
                var mower = mowers[i];
                if (!(mower.Slot.Input is AIInput aiInput) || HasBrain(mower)) continue;
                brains.Add(new MowerAIBrain(mower, aiInput, lawn, bags, mowers, bins[i], this, settings));
            }
        }

        private bool HasBrain(MowerPlayer mower)
        {
            foreach (var brain in brains)
                if (brain.Player == mower) return true;
            return false;
        }

        // ---------- Online ----------

        // Mowers (position, heading, bags, next bag fill, turbo / protected / stunned) and loose bags.
        protected override void WriteSnapshot(BinaryWriter writer)
        {
            writer.Write((byte)mowers.Count);
            foreach (var mower in mowers)
            {
                var position = mower.Position;
                writer.Write(position.x);
                writer.Write(position.y);
                writer.Write(position.z);
                writer.Write(mower.transform.eulerAngles.y);
                writer.Write((ushort)mower.BagCount);
                writer.Write((byte)Mathf.RoundToInt(Mathf.Clamp01(mower.Fill) * 255f));
                writer.Write((byte)((mower.IsBoosting ? 1 : 0) | (mower.IsTailProtected ? 2 : 0) | (mower.IsStunned ? 4 : 0)));
            }
            bags.WriteState(writer);
        }

        protected override void ReadSnapshot(BinaryReader reader)
        {
            int count = reader.ReadByte();
            for (int i = 0; i < count; i++)
            {
                var position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                float yaw = reader.ReadSingle();
                int bagCount = reader.ReadUInt16();
                float fill = reader.ReadByte() / 255f;
                int flags = reader.ReadByte();
                if (i < mowers.Count)
                    mowers[i].ApplyRemote(position, yaw, bagCount, fill, (flags & 1) != 0, (flags & 2) != 0, (flags & 4) != 0);
            }
            bags.ReadState(reader);
        }

        protected override void OnNetworkEvent(byte eventId, BinaryReader reader)
        {
            if (eventId == BagDeliveredEvent)
            {
                int index = reader.ReadByte();
                if (index >= mowers.Count) return;
                bags.Throw(mowers[index].TailEnd, bins[index].MouthPosition, mowers[index].Color);
                bins[index].Bounce();
            }
            else if (eventId == FrenzyStartedEvent && !IsFrenzy)
            {
                IsFrenzy = true;
                ApplyFrenzy();
            }
        }
    }
}
