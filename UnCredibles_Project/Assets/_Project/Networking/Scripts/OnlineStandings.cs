using System.Collections.Generic;
using Unity.Netcode;

namespace UnCredibles.Networking
{
    // What the Results screen shows, sent by the host to the clients after every minigame.
    public sealed class OnlineStandings
    {
        public struct Row
        {
            public string Name;
            public int Placement;
            public int Gained;
            public int Total;
        }

        public int Round;
        public int TotalRounds;
        public bool IsFinal;
        public readonly List<Row> Rows = new List<Row>(4);

        internal void Write(FastBufferWriter writer)
        {
            writer.WriteValueSafe(Round);
            writer.WriteValueSafe(TotalRounds);
            writer.WriteValueSafe(IsFinal);
            writer.WriteValueSafe((byte)Rows.Count);
            foreach (var row in Rows)
            {
                writer.WriteValueSafe(row.Name ?? string.Empty);
                writer.WriteValueSafe(row.Placement);
                writer.WriteValueSafe(row.Gained);
                writer.WriteValueSafe(row.Total);
            }
        }

        internal void Read(FastBufferReader reader)
        {
            reader.ReadValueSafe(out Round);
            reader.ReadValueSafe(out TotalRounds);
            reader.ReadValueSafe(out IsFinal);
            reader.ReadValueSafe(out byte count);
            Rows.Clear();
            for (int i = 0; i < count; i++)
            {
                var row = new Row();
                reader.ReadValueSafe(out row.Name);
                reader.ReadValueSafe(out row.Placement);
                reader.ReadValueSafe(out row.Gained);
                reader.ReadValueSafe(out row.Total);
                Rows.Add(row);
            }
        }
    }
}
