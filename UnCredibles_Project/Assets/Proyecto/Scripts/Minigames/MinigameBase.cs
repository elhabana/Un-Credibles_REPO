using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnCredibles.Minigames
{
    public abstract class MinigameBase : MonoBehaviour
    {
        public bool IsRunning { get; private set; }
        public event Action<IReadOnlyList<MinigameResult>> Finished;

        // The manager owns starting; each minigame decides when it finishes.
        internal void Begin()
        {
            if (IsRunning) return;
            IsRunning = true;
            try { OnMinigameStarted(); }
            catch
            {
                IsRunning = false;
                throw;
            }
        }

        protected abstract void OnMinigameStarted();

        protected void FinishMinigame(params MinigameResult[] results)
        {
            if (!IsRunning) return;
            if (results == null) throw new ArgumentNullException(nameof(results));

            var slots = new HashSet<int>();
            foreach (var result in results)
                if (!slots.Add(result.PlayerSlot))
                    throw new ArgumentException("Only one result per player slot is allowed.", nameof(results));

            // Keep a snapshot so the caller cannot alter the submitted results.
            var snapshot = Array.AsReadOnly((MinigameResult[])results.Clone());
            IsRunning = false;
            Finished?.Invoke(snapshot);
        }
    }
}
