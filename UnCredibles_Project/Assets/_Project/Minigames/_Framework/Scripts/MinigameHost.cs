using System;
using UnityEngine;

namespace UnCredibles.Minigames
{
    // Meeting point between a freshly loaded minigame scene and whoever runs it (Core's MinigameManager).
    // Without a host the scene is being played on its own and DebugBootstrap takes over.
    public static class MinigameHost
    {
        private static Action<IMinigame> handler;

        public static bool IsAvailable => handler != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => handler = null;

        public static void Register(Action<IMinigame> minigameHandler)
        {
            if (handler != null && handler != minigameHandler)
                Debug.LogWarning("A minigame host was already registered. Replacing it.");
            handler = minigameHandler;
        }

        public static void Unregister(Action<IMinigame> minigameHandler)
        {
            if (handler == minigameHandler) handler = null;
        }

        public static bool TryHandOff(IMinigame minigame)
        {
            if (handler == null) return false;
            handler(minigame);
            return true;
        }
    }
}
