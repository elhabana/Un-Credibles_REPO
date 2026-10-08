using System.Collections.Generic;
using System.IO;
using UnCredibles.Players;
using UnCredibles.Players.Inputs;
using UnityEngine;

namespace UnCredibles.Minigames.MowTheLawn
{
    // Mow the lawn: every mower cuts grass and fills bags that it drags behind like a tail.
    // Driving into a rival's tail knocks those bags loose so anybody can pick them up.
    // Whoever carries the most bags when the time runs out wins (score = bags in the tail).
    // The only Update of the minigame: lawn, AI, mowers and bags are ticked from here in order.
    public sealed class MowTheLawnController : MinigameController
    {
        [SerializeField] private MowTheLawnSettings settings;
        [SerializeField] private MowLawn lawn;
        [SerializeField] private MowBags bags;
        [SerializeField] private MowerPlayer mowerPrefab;
        [SerializeField] private Transform playersParent;

        private readonly List<MowerPlayer> mowers = new List<MowerPlayer>(PlayerRegistry.MaxPlayers);
        private readonly List<MowerAIBrain> brains = new List<MowerAIBrain>(PlayerRegistry.MaxPlayers);
        private readonly List<Vector3> dropped = new List<Vector3>(32);

        protected override void OnInitialize(MinigameContext context)
        {
            lawn.Initialize(settings);
            bags.Initialize(settings, lawn);

            foreach (var player in Players)
            {
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

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            lawn.Tick(deltaTime);
            bags.Tick(deltaTime);

            if (IsReplica)
            {
                // Online client: mowers follow the host; the grass is cut locally from their path.
                foreach (var mower in mowers) mower.TickRemote(deltaTime, lawn);
                return;
            }
            if (!IsPlaying) return;
            EnsureBrains(); // an online player who left is now a bot

            foreach (var brain in brains) brain.Tick(deltaTime); // before mowers read their input
            foreach (var mower in mowers) mower.Tick(deltaTime, lawn);

            SeparateMowers();
            CutTails();
            PickUpLooseBags();
            UpdateScores();
        }

        // Soft push so mowers never drive through each other.
        private void SeparateMowers()
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

                    var push = (distance > 0.001f ? offset / distance : Vector3.right) * ((minDistance - distance) * 0.5f);
                    mowers[a].MoveTo(lawn.ClampInside(mowers[a].Position - push, settings.MowerRadius));
                    mowers[b].MoveTo(lawn.ClampInside(mowers[b].Position + push, settings.MowerRadius));
                }
            }
        }

        // A mower touching a bag of another tail cuts it there: the rest of that tail falls loose.
        private void CutTails()
        {
            float reach = settings.MowerRadius + settings.BagRadius;
            foreach (var attacker in mowers)
            {
                foreach (var victim in mowers)
                {
                    if (victim == attacker) continue;
                    var tail = victim.BagPositions;
                    for (int i = 0; i < tail.Count; i++)
                    {
                        var offset = tail[i] - attacker.Position;
                        offset.y = 0f;
                        if (offset.sqrMagnitude > reach * reach) continue;

                        dropped.Clear();
                        victim.CutTailAt(i, dropped);
                        foreach (var position in dropped) bags.Drop(position);
                        break;
                    }
                }
            }
        }

        private void PickUpLooseBags()
        {
            foreach (var mower in mowers)
            {
                int picked = bags.PickUp(mower.Position, settings.MowerRadius);
                if (picked > 0) mower.AddBags(picked);
            }
        }

        private void UpdateScores()
        {
            foreach (var mower in mowers)
                if (Score.GetScore(mower.Slot.PlayerId) != mower.BagCount)
                    Score.SetScore(mower.Slot.PlayerId, mower.BagCount);
        }

        // Every bot gets a brain, including an online player replaced by AI mid-match.
        private void EnsureBrains()
        {
            foreach (var mower in mowers)
            {
                if (!(mower.Slot.Input is AIInput aiInput) || HasBrain(mower)) continue;
                brains.Add(new MowerAIBrain(mower, aiInput, lawn, bags, mowers, settings));
            }
        }

        private bool HasBrain(MowerPlayer mower)
        {
            foreach (var brain in brains)
                if (brain.Player == mower) return true;
            return false;
        }

        // ---------- Online ----------

        // Mowers (position, heading, bags, how full the next bag is) and the loose bags.
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
                if (i < mowers.Count) mowers[i].ApplyRemote(position, yaw, bagCount, fill);
            }
            bags.ReadState(reader);
        }
    }
}
