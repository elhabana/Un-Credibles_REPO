using System;

namespace UnCredibles.Minigames
{
    // What a minigame needs from the online session, without depending on Netcode.
    // Online the host simulates the minigame and sends three kinds of data to the clients:
    //   - HUD: state, countdown, timer, scores and results (reliable, on change)
    //   - snapshot: positions and anything that moves (unreliable, ~20 times per second)
    //   - events: one-shot moments such as "round 2 starts" (reliable)
    // Clients run the same scene as a replica: they draw what the host sends and simulate nothing.
    public interface IMinigameNetwork
    {
        bool IsHost { get; }

        void SendHud(byte[] data, int length);
        void SendSnapshot(byte[] data, int length);
        void SendEvent(byte[] data, int length);

        event Action<byte[], int> HudReceived;
        event Action<byte[], int> SnapshotReceived;
        event Action<byte[], int> EventReceived;
    }

    // Set by the online session while connected; null offline.
    public static class MinigameNetwork
    {
        public static IMinigameNetwork Current { get; set; }

        public static bool IsOnline => Current != null;
        public static bool IsReplica => Current != null && !Current.IsHost;
    }
}
