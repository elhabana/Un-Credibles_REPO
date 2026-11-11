using System;
using UnityEngine;

namespace UnCredibles.Minigames
{
    // Every sound effect of the game. The clips live on Core's AudioManager.
    public enum GameSfx
    {
        UiMove, UiConfirm, UiBack, UiError, UiReady,
        CountdownBeep, CountdownGo, TimerTick, RoundEnd, Win, Lose, Score,
        Deliver, Jump, Stun, HitSoft, HitRival, RunOver, FailWah,
    }

    // Minigames cannot see Core, so Core plugs its AudioManager in here (like PlayerRegistry's providers).
    public static class GameAudio
    {
        public static Action<GameSfx> Player { get; set; }

        public static void Play(GameSfx sfx) => Player?.Invoke(sfx);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Player = null;
    }
}
