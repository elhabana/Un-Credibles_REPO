using System;
using UnityEngine;

namespace UnCredibles.Minigames
{
    // Only ticks while running (the component disables itself otherwise) and
    // notifies once per whole second so the UI does not refresh every frame.
    public sealed class RoundTimer : MonoBehaviour
    {
        private int lastWholeSecond;

        public float Remaining { get; private set; }
        public bool IsRunning => enabled;

        public event Action<int> SecondChanged;
        public event Action Expired;

        private void Awake() => enabled = false;

        public void StartTimer(float seconds)
        {
            Remaining = Mathf.Max(0f, seconds);
            lastWholeSecond = Mathf.CeilToInt(Remaining);
            enabled = true;
            SecondChanged?.Invoke(lastWholeSecond);
        }

        public void Stop() => enabled = false;

        private void Update()
        {
            Remaining -= Time.deltaTime;
            if (Remaining <= 0f)
            {
                Remaining = 0f;
                enabled = false;
                SecondChanged?.Invoke(0);
                Expired?.Invoke();
                return;
            }

            int whole = Mathf.CeilToInt(Remaining);
            if (whole == lastWholeSecond) return;
            lastWholeSecond = whole;
            SecondChanged?.Invoke(whole);
        }
    }
}
