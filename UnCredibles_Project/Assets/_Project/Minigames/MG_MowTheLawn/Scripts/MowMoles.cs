using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UnCredibles.Minigames.MowTheLawn
{
    // Moles: a mound shakes in front of a mower, a mole pops out and stands there as a solid obstacle,
    // leaving bare soil. While out it runs at tail bags nearby; a tail it reaches loses its last bag
    // (MowTheLawnController decides that) and the mole runs away with it, burrowing with the bag unless a mower touches it first. Bonked with the turbo
    // it hides at once. The host spawns and moves them; clients follow the snapshot. Built in code from
    // primitives with the bins' material, ticked by MowTheLawnController.
    public sealed class MowMoles : MonoBehaviour
    {
        public enum MoleState : byte { Hidden, Warning, Up, Fleeing }

        public sealed class Mole
        {
            public MoleState State;
            public Vector3 Position;
            public float Timer;
            public Color BagColor; // owner's colour of the stolen bag while fleeing

            // Only visual.
            public Transform Root;
            public Transform Mound;
            public Transform Body;
            public MowBagView Bag;
            public MoleState Shown = MoleState.Hidden;
            public float Rise;
            public float Pop;
        }

        private const int SpawnSamples = 12;
        private const float MinMowerGap = 1.5f; // never pops out right under a mower
        private const float EdgeMargin = 1.5f;
        private const float MoleSpacing = 3f;
        private const float SoilRadius = 1.1f;
        private const float BodyHeight = .75f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color SoilColor = new Color(.42f, .28f, .16f);
        private static readonly Color FurColor = new Color(.33f, .27f, .25f);
        private static readonly Color NoseColor = new Color(.95f, .55f, .6f);
        private static readonly Color EyeColor = new Color(.05f, .05f, .05f);

        private readonly List<Mole> moles = new List<Mole>();
        private MowTheLawnSettings settings;
        private MowLawn lawn;
        private MowBags bags;
        private Material material;
        private MaterialPropertyBlock block;
        private float spawnTimer;

        public IReadOnlyList<Mole> Moles => moles;

        public void Initialize(MowTheLawnSettings moleSettings, MowLawn mowLawn, MowBags bagPool, Material baseMaterial)
        {
            settings = moleSettings;
            lawn = mowLawn;
            bags = bagPool;
            material = baseMaterial;
            spawnTimer = settings.MoleInterval;
        }

        // Out of the ground: mowers bump into it.
        public static bool IsSolid(Mole mole) => IsOut(mole.State);

        private static bool IsOut(MoleState state) => state == MoleState.Up || state == MoleState.Fleeing;

        public void Steal(Mole mole, Color bagColor)
        {
            mole.State = MoleState.Fleeing;
            mole.Timer = settings.MoleFleeSeconds;
            mole.BagColor = bagColor;
        }

        public void Hide(Mole mole) => mole.State = MoleState.Hidden;

        // host: spawn, time and move the moles (only while playing). Everybody animates them.
        public void Tick(float deltaTime, bool host, IReadOnlyList<MowerPlayer> mowers, IReadOnlyList<MowBin> bins, bool frenzy)
        {
            if (host)
            {
                foreach (var mole in moles) Advance(mole, deltaTime, mowers);
                spawnTimer -= deltaTime;
                if (spawnTimer <= 0f)
                {
                    spawnTimer = settings.MoleInterval;
                    if (ActiveCount() < (frenzy ? settings.FrenzyMaxMoles : settings.MaxMoles)) TrySpawn(mowers, bins);
                }
            }
            foreach (var mole in moles) Animate(mole, deltaTime);
        }

        private void Advance(Mole mole, float deltaTime, IReadOnlyList<MowerPlayer> mowers)
        {
            if (mole.State == MoleState.Hidden) return;
            mole.Timer -= deltaTime;
            if (mole.State == MoleState.Up && TryNearestBag(mole.Position, mowers, out var bag))
            {
                var toBag = Flat(bag - mole.Position);
                float step = settings.MoleLungeSpeed * deltaTime;
                mole.Position = lawn.ClampInside(mole.Position + toBag * Mathf.Min(1f, step / Mathf.Max(toBag.magnitude, .0001f)), settings.MoleRadius);
            }
            if (mole.State == MoleState.Fleeing)
            {
                // Away from the closest mower; burrows with the bag when time is up.
                MowerPlayer chaser = null;
                float closest = float.MaxValue;
                foreach (var mower in mowers)
                {
                    float distance = Flat(mower.Position - mole.Position).sqrMagnitude;
                    if (distance >= closest) continue;
                    closest = distance;
                    chaser = mower;
                }
                var away = chaser != null ? Flat(mole.Position - chaser.Position) : Vector3.zero;
                if (away.sqrMagnitude < .0001f) away = Flat(mole.Position - lawn.Center);
                if (away.sqrMagnitude > .0001f)
                    mole.Position = lawn.ClampInside(mole.Position + away.normalized * (settings.MoleSpeed * deltaTime), settings.MoleRadius);
            }
            if (mole.Timer > 0f) return;
            if (mole.State == MoleState.Warning)
            {
                mole.State = MoleState.Up;
                mole.Timer = settings.MoleUpSeconds;
            }
            else mole.State = MoleState.Hidden;
        }

        // A tail bag (not protected) within lunge range: the closest one.
        private bool TryNearestBag(Vector3 point, IReadOnlyList<MowerPlayer> mowers, out Vector3 best)
        {
            best = default;
            float bestDistance = settings.MoleLungeRange * settings.MoleLungeRange;
            bool found = false;
            foreach (var mower in mowers)
            {
                if (mower.IsTailProtected) continue;
                foreach (var bag in mower.BagPositions)
                {
                    float distance = Flat(bag - point).sqrMagnitude;
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    best = bag;
                    found = true;
                }
            }
            return found;
        }

        // In the way of a mower, a few metres ahead of it: usually the one with the longest tail,
        // sometimes anyone. A random spot when there is no room ahead of anybody.
        private void TrySpawn(IReadOnlyList<MowerPlayer> mowers, IReadOnlyList<MowBin> bins)
        {
            bool found = false;
            var best = Vector3.zero;
            float bestScore = float.MinValue;
            for (int i = 0; i < SpawnSamples; i++)
            {
                Vector3 candidate;
                float score = Random.value;
                if (mowers.Count > 0)
                {
                    var mower = mowers[Random.Range(0, mowers.Count)];
                    var ahead = Quaternion.Euler(0f, Random.Range(-25f, 25f), 0f) * mower.Forward;
                    candidate = mower.Position + Flat(ahead).normalized * Random.Range(settings.MoleAhead.x, settings.MoleAhead.y);
                    if (!lawn.ClampInside(candidate, EdgeMargin).Equals(candidate)) continue;
                    score += mower.BagCount * .5f;
                }
                else candidate = lawn.RandomPoint(EdgeMargin);
                if (!IsClear(candidate, mowers, bins)) continue;
                if (score <= bestScore) continue;
                bestScore = score;
                best = candidate;
                found = true;
            }
            if (!found)
            {
                best = lawn.RandomPoint(EdgeMargin);
                if (!IsClear(best, mowers, bins)) return;
            }

            var mole = Take();
            mole.State = MoleState.Warning;
            mole.Timer = settings.MoleWarnSeconds;
            mole.Position = best;
        }

        private bool IsClear(Vector3 point, IReadOnlyList<MowerPlayer> mowers, IReadOnlyList<MowBin> bins)
        {
            float clearance = settings.MoleClearance * settings.MoleClearance;
            foreach (var mower in mowers)
                if (Flat(mower.Position - point).sqrMagnitude < MinMowerGap * MinMowerGap) return false;
            foreach (var bin in bins)
                if (Flat(bin.Position - point).sqrMagnitude < clearance) return false;
            foreach (var other in moles)
                if (other.State != MoleState.Hidden && Flat(other.Position - point).sqrMagnitude < MoleSpacing * MoleSpacing) return false;
            return true;
        }

        private int ActiveCount()
        {
            int count = 0;
            foreach (var mole in moles)
                if (mole.State != MoleState.Hidden) count++;
            return count;
        }

        private Mole Take()
        {
            foreach (var mole in moles)
                if (mole.State == MoleState.Hidden && mole.Shown == MoleState.Hidden) return mole;
            var created = new Mole();
            moles.Add(created);
            return created;
        }

        // ---------- Look ----------

        private void Animate(Mole mole, float deltaTime)
        {
            if (mole.Root == null) Build(mole);
            bool appeared = mole.Shown == MoleState.Hidden && mole.State != MoleState.Hidden;
            // Popping out leaves bare soil around the hole (also on clients: it is only the look of the grass).
            if (mole.State == MoleState.Up && mole.Shown == MoleState.Warning) lawn.Cut(mole.Position, SoilRadius);
            if (IsOut(mole.State) && !IsOut(mole.Shown)) mole.Pop = 1f;
            mole.Shown = mole.State;

            bool visible = mole.State != MoleState.Hidden || mole.Rise > .01f;
            mole.Root.gameObject.SetActive(visible);
            if (!visible) return;

            var previous = mole.Root.position;
            mole.Root.position = appeared ? mole.Position : Vector3.Lerp(previous, mole.Position, Mathf.Min(1f, 15f * deltaTime));
            var moved = Flat(mole.Root.position - previous);
            if (IsOut(mole.State) && moved.sqrMagnitude > .00001f)
                mole.Root.rotation = Quaternion.Slerp(mole.Root.rotation, Quaternion.LookRotation(moved), Mathf.Min(1f, 12f * deltaTime));

            // Mound: shakes before the mole comes out, stays while it is out.
            bool warning = mole.State == MoleState.Warning;
            mole.Mound.gameObject.SetActive(mole.State != MoleState.Hidden);
            mole.Mound.localPosition = warning ? new Vector3(Random.Range(-.05f, .05f), 0f, Random.Range(-.05f, .05f)) : Vector3.zero;

            // Body rises out of the hole, with a little pop.
            mole.Rise = Mathf.MoveTowards(mole.Rise, IsSolid(mole) ? 1f : 0f, deltaTime * 6f);
            mole.Pop = Mathf.Max(0f, mole.Pop - deltaTime * 4f);
            float hop = mole.State == MoleState.Fleeing ? Mathf.Abs(Mathf.Sin(Time.time * 18f)) * .12f : 0f;
            mole.Body.localPosition = new Vector3(0f, (mole.Rise - 1f) * BodyHeight + hop, 0f);
            mole.Body.localScale = Vector3.one * (1f + mole.Pop * .25f);
            mole.Body.gameObject.SetActive(mole.Rise > .01f);

            // The stolen bag, held up over its head.
            bool carrying = mole.State == MoleState.Fleeing;
            if (carrying && mole.Bag == null)
            {
                mole.Bag = bags.Rent();
                mole.Bag.SetTint(mole.BagColor);
            }
            else if (!carrying && mole.Bag != null)
            {
                bags.Release(mole.Bag);
                mole.Bag = null;
            }
            if (mole.Bag != null)
                mole.Bag.transform.SetPositionAndRotation(mole.Body.position + Vector3.up * (BodyHeight + .35f), mole.Root.rotation);
        }

        private void Build(Mole mole)
        {
            mole.Root = new GameObject("Mole").transform;
            mole.Root.SetParent(transform, false);
            mole.Mound = Part("Mound", PrimitiveType.Sphere, mole.Root, new Vector3(0f, 0f, 0f), new Vector3(1.4f, .35f, 1.4f), SoilColor);
            mole.Body = new GameObject("Body").transform;
            mole.Body.SetParent(mole.Root, false);
            Part("Fur", PrimitiveType.Capsule, mole.Body, new Vector3(0f, BodyHeight * .5f, 0f), new Vector3(.75f, BodyHeight * .5f, .7f), FurColor);
            Part("Nose", PrimitiveType.Sphere, mole.Body, new Vector3(0f, BodyHeight * .65f, .36f), new Vector3(.2f, .16f, .16f), NoseColor);
            Part("Eye_L", PrimitiveType.Sphere, mole.Body, new Vector3(-.13f, BodyHeight * .82f, .3f), Vector3.one * .08f, EyeColor);
            Part("Eye_R", PrimitiveType.Sphere, mole.Body, new Vector3(.13f, BodyHeight * .82f, .3f), Vector3.one * .08f, EyeColor);
        }

        private Transform Part(string partName, PrimitiveType shape, Transform parent, Vector3 position, Vector3 scale, Color color)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = partName;
            DestroyImmediate(part.GetComponent<Collider>()); // also works in edit mode tests
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            var partRenderer = part.GetComponent<Renderer>();
            if (material != null) partRenderer.sharedMaterial = material;
            block ??= new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            partRenderer.SetPropertyBlock(block);
            return part.transform;
        }

        // ---------- Online ----------

        public void WriteState(BinaryWriter writer)
        {
            writer.Write((byte)moles.Count);
            foreach (var mole in moles)
            {
                writer.Write((byte)mole.State);
                writer.Write(mole.Position.x);
                writer.Write(mole.Position.z);
                Color32 bag = mole.BagColor;
                writer.Write(bag.r);
                writer.Write(bag.g);
                writer.Write(bag.b);
            }
        }

        public void ReadState(BinaryReader reader)
        {
            int count = reader.ReadByte();
            while (moles.Count < count) moles.Add(new Mole());
            for (int i = 0; i < count; i++)
            {
                var mole = moles[i];
                mole.State = (MoleState)reader.ReadByte();
                mole.Position = new Vector3(reader.ReadSingle(), lawn.Center.y, reader.ReadSingle());
                mole.BagColor = new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), 255);
            }
        }

        private static Vector3 Flat(Vector3 value) => new Vector3(value.x, 0f, value.z);
    }
}
