using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnCredibles.Minigames
{
    // One coordinator per test scene. Core integration is deliberately deferred.
    public sealed class MinigameManager : MonoBehaviour
    {
        public MinigameBase ActiveMinigame { get; private set; }
        public IReadOnlyList<MinigameResult> LastResults { get; private set; }
            = Array.Empty<MinigameResult>();

        public bool StartMinigame(MinigameBase minigame)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || minigame == null ||
                !minigame.isActiveAndEnabled || ActiveMinigame != null || minigame.IsRunning)
                return false;

            LastResults = Array.Empty<MinigameResult>();
            ActiveMinigame = minigame;
            minigame.Finished += ReceiveResults;
            try { minigame.Begin(); }
            catch
            {
                minigame.Finished -= ReceiveResults;
                ActiveMinigame = null;
                throw;
            }
            return true;
        }

        private void ReceiveResults(IReadOnlyList<MinigameResult> results)
        {
            ActiveMinigame.Finished -= ReceiveResults;
            ActiveMinigame = null;
            LastResults = results;
            Debug.Log($"Minigame finished: {results.Count} player result(s).", this);
            foreach (var result in results)
                Debug.Log($"Slot {result.PlayerSlot}: {result.Score} points.", this);
        }

        private void OnDestroy()
        {
            if (ActiveMinigame != null)
                ActiveMinigame.Finished -= ReceiveResults;
        }
    }
}
