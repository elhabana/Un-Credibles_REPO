using NUnit.Framework;
using UnCredibles.Networking;
using UnCredibles.Players;
using Unity.Collections;
using Unity.Netcode;

namespace UnCredibles.Tests
{
    // Write and Read must cover the same fields in the same order, or every later field shifts on the clients.
    public sealed class NetworkMessageTests
    {
        [Test]
        public void LobbySlotInfoReadsBackWhatWasWritten()
        {
            var sent = new LobbySlotInfo
            {
                Occupied = true,
                PlayerId = 7,
                Name = "Ana",
                Type = PlayerType.OnlinePlayer,
                State = SlotState.Disconnected,
                Source = InputSourceType.Network,
                Ready = true,
                Host = false,
                Owner = 3,
                Loadout = new CharacterLoadout { Helmet = "h1", Outfit = "o2", Boots = "b3" },
            };

            using var writer = new FastBufferWriter(256, Allocator.Temp);
            sent.Write(writer);
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            var got = LobbySlotInfo.Read(reader);

            Assert.AreEqual(sent.Occupied, got.Occupied);
            Assert.AreEqual(sent.PlayerId, got.PlayerId);
            Assert.AreEqual(sent.Name, got.Name);
            Assert.AreEqual(sent.Type, got.Type);
            Assert.AreEqual(sent.State, got.State);
            Assert.AreEqual(sent.Source, got.Source);
            Assert.AreEqual(sent.Ready, got.Ready);
            Assert.AreEqual(sent.Host, got.Host);
            Assert.AreEqual(sent.Owner, got.Owner);
            Assert.AreEqual(sent.Loadout, got.Loadout);
        }

        [Test]
        public void StandingsReadBackWhatWasWritten()
        {
            var sent = new OnlineStandings { Round = 2, TotalRounds = 3, IsFinal = false };
            sent.Rows.Add(new OnlineStandings.Row { PlayerId = 1, Name = "Ana", Placement = 1, Gained = 4, Total = 7 });
            sent.Rows.Add(new OnlineStandings.Row { PlayerId = 4, Name = "BOT", Placement = 2, Gained = 3, Total = 5 });

            using var writer = new FastBufferWriter(256, Allocator.Temp);
            sent.Write(writer);
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            var got = new OnlineStandings();
            got.Read(reader);

            Assert.AreEqual(sent.Round, got.Round);
            Assert.AreEqual(sent.TotalRounds, got.TotalRounds);
            Assert.AreEqual(sent.IsFinal, got.IsFinal);
            CollectionAssert.AreEqual(sent.Rows, got.Rows);
        }
    }
}
