using NUnit.Framework;
using UnCredibles.Players;
using UnCredibles.Players.Inputs;

namespace UnCredibles.Tests
{
    public sealed class PlayerRegistryTests
    {
        [Test]
        public void AddsPlayersUpToFourSlots()
        {
            var registry = new PlayerRegistry();
            for (int i = 0; i < PlayerRegistry.MaxPlayers; i++)
                Assert.IsTrue(registry.TryAddPlayer(PlayerType.LocalPlayer, new AIInput(), null, out _));

            Assert.IsFalse(registry.TryAddPlayer(PlayerType.LocalPlayer, new AIInput(), null, out _));
            Assert.AreEqual(PlayerRegistry.MaxPlayers, registry.OccupiedCount);
        }

        [Test]
        public void AIPlayersAreReadyAutomatically()
        {
            var registry = new PlayerRegistry();
            registry.TryAddPlayer(PlayerType.AIPlayer, new AIInput(), null, out var bot);
            registry.TryAddPlayer(PlayerType.LocalPlayer, new AIInput(), null, out var human);

            Assert.IsTrue(bot.IsReady);
            Assert.IsFalse(registry.AllPlayersReady);
            registry.SetReady(human.SlotIndex, true);
            Assert.IsTrue(registry.AllPlayersReady);
        }

        [Test]
        public void ReplaceWithAIKeepsPlayerId()
        {
            var registry = new PlayerRegistry();
            registry.TryAddPlayer(PlayerType.OnlinePlayer, new AIInput(), "Habana", out var slot);
            registry.SetHost(slot.SlotIndex);
            int id = slot.PlayerId;

            Assert.IsTrue(registry.ReplaceWithAI(slot.SlotIndex, new AIInput()));
            Assert.AreEqual(id, slot.PlayerId);
            Assert.AreEqual(PlayerType.AIPlayer, slot.PlayerType);
            Assert.AreEqual(SlotState.AI, slot.State);
            Assert.IsTrue(slot.IsReady);
            Assert.IsFalse(slot.IsHost);
        }

        [Test]
        public void InvitingSlotIsSkippedWhenAutoAssigning()
        {
            var registry = new PlayerRegistry();
            registry.SetSlotState(0, SlotState.Inviting);
            registry.TryAddPlayer(PlayerType.AIPlayer, new AIInput(), null, out var slot);
            Assert.AreEqual(1, slot.SlotIndex);
        }

        [Test]
        public void RemovingClearsTheSlot()
        {
            var registry = new PlayerRegistry();
            registry.TryAddPlayer(PlayerType.LocalPlayer, new AIInput(), null, out var slot);
            Assert.IsTrue(registry.RemovePlayer(slot.SlotIndex));
            Assert.AreEqual(PlayerType.Empty, slot.PlayerType);
            Assert.AreEqual(SlotState.Empty, slot.State);
            Assert.IsNull(slot.Input);
        }
    }
}
