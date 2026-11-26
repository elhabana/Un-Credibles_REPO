using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnCredibles.Minigames.MowTheLawn;
using UnCredibles.Players;
using UnCredibles.Players.Inputs;
using UnityEngine;

namespace UnCredibles.Tests
{
    // Rules of Mow The Lawn that do not need a running scene: grass, loose bags and the mower's tail.
    public sealed class MowTheLawnTests
    {
        private readonly List<Object> created = new List<Object>();
        private MowTheLawnSettings settings;
        private MowLawn lawn;
        private MowBags bags;

        [SetUp]
        public void SetUp()
        {
            settings = Track(ScriptableObject.CreateInstance<MowTheLawnSettings>());
            lawn = Track(new GameObject("Lawn")).AddComponent<MowLawn>();
            lawn.Initialize(settings);

            var bagPrefab = Track(new GameObject("Bag")).AddComponent<MowBagView>();
            bags = Track(new GameObject("Bags")).AddComponent<MowBags>();
            Set(bags, "bagPrefab", bagPrefab);
            bags.Initialize(settings, lawn);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created)
                if (item != null) Object.DestroyImmediate(item);
            created.Clear();
        }

        // ---------- Lawn ----------

        [Test]
        public void CuttingTallGrassCollectsItAndLeavesNothingToCutTwice()
        {
            float first = lawn.Cut(lawn.Center, 1f);
            Assert.Greater(first, 0f);
            Assert.AreEqual(0f, lawn.GetHeight(lawn.Center));
            Assert.AreEqual(0f, lawn.Cut(lawn.Center, 1f), "just cut grass is too short to cut again");
        }

        [Test]
        public void CutCollectsAboutTheCircleArea()
        {
            const float radius = 2f;
            float cells = Mathf.PI * radius * radius / (settings.CellSize * settings.CellSize);
            Assert.AreEqual(cells, lawn.Cut(lawn.Center, radius), cells * 0.15f);
        }

        [Test]
        public void GrassGrowsBackOverTimeAndAllAtOnceInTheFrenzy()
        {
            lawn.Cut(lawn.Center, 1f);
            lawn.Tick(settings.RegrowSeconds * 0.5f);
            Assert.AreEqual(0.5f, lawn.GetHeight(lawn.Center), 0.01f);
            Assert.Less(settings.MinCutHeight, 0.5f, "default settings: half grown grass can be cut");
            Assert.Greater(lawn.Cut(lawn.Center, 1f), 0f);
            lawn.RegrowAll();
            Assert.AreEqual(1f, lawn.GetHeight(lawn.Center));
        }

        [Test]
        public void ClampInsideKeepsTheMowerOnTheLawn()
        {
            float radius = settings.MowerRadius;
            var outside = lawn.Center + new Vector3(1000f, 0f, -1000f);
            var clamped = lawn.ClampInside(outside, radius);
            Assert.AreEqual(lawn.Center.x + lawn.HalfWidth - radius, clamped.x, 1e-4f);
            Assert.AreEqual(lawn.Center.z - lawn.HalfDepth + radius, clamped.z, 1e-4f);

            var inside = lawn.Center + new Vector3(1f, 0f, 1f);
            Assert.AreEqual(inside, lawn.ClampInside(inside, radius));
        }

        // ---------- Loose bags ----------

        [Test]
        public void DroppedBagsCanOnlyBePickedUpAfterTheDelay()
        {
            bags.Drop(lawn.Center);
            var landed = bags.GetLoosePosition(0);
            Assert.AreEqual(0, bags.PickUp(landed, settings.MowerRadius), "fresh bags cannot be grabbed back at once");

            bags.Tick(settings.PickupDelay + 0.01f, true);
            Assert.AreEqual(1, bags.PickUp(landed, settings.MowerRadius));
            Assert.AreEqual(0, bags.LooseCount);
        }

        [Test]
        public void LooseBagsVanishAfterTheirLifetimeOnlyOnTheHost()
        {
            bags.Drop(lawn.Center);
            bags.Tick(settings.LooseBagLifetime + 0.1f, false);
            Assert.AreEqual(1, bags.LooseCount, "clients wait for the host's snapshot");
            bags.Tick(0.01f, true);
            Assert.AreEqual(0, bags.LooseCount);
        }

        [Test]
        public void TooManyLooseBagsDropTheOldest()
        {
            for (int i = 0; i < settings.MaxLooseBags + 5; i++) bags.Drop(lawn.Center);
            Assert.AreEqual(settings.MaxLooseBags, bags.LooseCount);
        }

        // ---------- Mower ----------

        [Test]
        public void CuttingATailDropsThatBagAndEverythingBehindAndProtectsTheRest()
        {
            var mower = CreateMower(out _);
            mower.AddBags(5);

            var dropped = new List<Vector3>();
            Assert.AreEqual(3, mower.CutTailAt(2, dropped));
            Assert.AreEqual(2, mower.BagCount);
            Assert.AreEqual(3, dropped.Count);
            Assert.IsTrue(mower.IsTailProtected);
        }

        [Test]
        public void UnloadingTakesBagsOneByOne()
        {
            var mower = CreateMower(out _);
            mower.AddBags(2);
            mower.TakeLastBag();
            Assert.AreEqual(1, mower.BagCount);
            Assert.IsFalse(mower.IsTailProtected, "unloading is not a cut");
        }

        [Test]
        public void DrivingOverGrassFillsBags()
        {
            Set(settings, "cellsPerBag", 10f);
            var mower = CreateMower(out var input);
            input.SetMove(Vector2.right);
            for (int i = 0; i < 60; i++) mower.Tick(1f / 30f, lawn);

            Assert.Greater(mower.Position.x, lawn.Center.x + 1f, "the mower drives where the stick points");
            Assert.Greater(mower.BagCount, 0);
        }

        [Test]
        public void StunnedMowerCannotDriveOrBoost()
        {
            var mower = CreateMower(out var input);
            mower.Stun(1f, 2f);
            input.SetMove(Vector2.right);
            input.Press(PlayerAction.Jump);
            var start = mower.Position;
            mower.Tick(0.1f, lawn);

            Assert.IsFalse(mower.IsBoosting);
            Assert.IsFalse(mower.CanBeRammed);
            Assert.AreEqual(start.x, mower.Position.x, 1e-3f);
        }

        [Test]
        public void TurboGoesOnCooldown()
        {
            var mower = CreateMower(out _);
            mower.Boost();
            Assert.IsTrue(mower.IsBoosting);
            Assert.IsFalse(mower.CanBoost);
            mower.Tick(settings.BoostCooldown + 0.01f, lawn);
            Assert.IsFalse(mower.IsBoosting);
            Assert.IsTrue(mower.CanBoost);
        }

        [Test]
        public void CarryingBagsSlowsDownButNeverBelowTheMinimum()
        {
            Assert.AreEqual(1f, settings.SpeedFactor(0));
            Assert.Less(settings.SpeedFactor(5), 1f);
            Assert.AreEqual(settings.SpeedFactor(1000), settings.SpeedFactor(10000));
            Assert.Greater(settings.SpeedFactor(10000), 0f);
        }

        private MowerPlayer CreateMower(out AIInput input)
        {
            input = new AIInput();
            var registry = new PlayerRegistry();
            registry.TryAddPlayer(PlayerType.AIPlayer, input, null, out var slot);
            var mower = Track(new GameObject("Mower")).AddComponent<MowerPlayer>();
            mower.transform.position = lawn.Center;
            mower.transform.rotation = Quaternion.LookRotation(Vector3.right);
            mower.Setup(slot, settings, bags);
            var presentation = mower.GetComponent<UnCredibles.Minigames.PlayerPresentation>();
            if (presentation != null && presentation.Label != null) Track(presentation.Label.gameObject); // the name tag lives beside the mower
            return mower;
        }

        private T Track<T>(T item) where T : Object
        {
            created.Add(item);
            return item;
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }
}
