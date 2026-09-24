using System.Collections.Generic;
using NUnit.Framework;
using UnCredibles.Core;
using UnCredibles.Minigames;
using UnCredibles.Players;
using UnCredibles.Players.Inputs;

namespace UnCredibles.Tests
{
    public sealed class RankingAndMatchTests
    {
        [Test]
        public void RankOrdersByScoreAndSharesTies()
        {
            var results = MinigameRanking.Rank(new List<KeyValuePair<int, int>>
            {
                new KeyValuePair<int, int>(0, 400),
                new KeyValuePair<int, int>(1, 1000),
                new KeyValuePair<int, int>(2, 400),
                new KeyValuePair<int, int>(3, 200),
            });

            Assert.AreEqual(1, results[0].PlayerId);
            Assert.AreEqual(1, results[0].Placement);
            Assert.AreEqual(2, results[1].Placement);
            Assert.AreEqual(2, results[2].Placement);
            Assert.AreEqual(0, results[1].PlayerId, "Ties keep slot order.");
            Assert.AreEqual(4, results[3].Placement);
        }

        [Test]
        public void MatchAwardsPointsByPlacementAndFinishes()
        {
            var registry = new PlayerRegistry();
            registry.TryAddPlayer(PlayerType.LocalPlayer, new AIInput(), null, out var human);
            registry.TryAddPlayer(PlayerType.AIPlayer, new AIInput(), null, out var bot);
            var players = new List<PlayerSlot>();
            registry.GetActivePlayers(players);

            var match = new MatchManager(new[] { 4, 3, 2, 1 });
            IReadOnlyList<MinigameResult> standings = null;
            match.MatchFinished += s => standings = s;
            match.StartMatch(players, 2);

            match.RegisterResults(null, new[] { new MinigameResult(bot.PlayerId, 900, 1), new MinigameResult(human.PlayerId, 100, 2) });
            Assert.IsTrue(match.IsRunning);
            match.RegisterResults(null, new[] { new MinigameResult(bot.PlayerId, 50, 1), new MinigameResult(human.PlayerId, 10, 2) });

            Assert.IsFalse(match.IsRunning);
            Assert.AreEqual(8, match.GetPoints(bot.PlayerId));
            Assert.AreEqual(bot.PlayerId, standings[0].PlayerId, "Bots can win the match.");
        }
    }
}
