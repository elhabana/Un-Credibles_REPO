using UnityEngine;

namespace UnCredibles.Minigames
{
    [RequireComponent(typeof(MinigameManager))]
    public sealed class TemplateMinigame : MinigameBase
    {
        [SerializeField, Range(0, 3)] private int testPlayerSlot;
        [SerializeField] private int testScore = 10;

        private void Start() => StartExample();

        [ContextMenu("Start Example (Play Mode)")]
        private void StartExample()
        {
            if (!Application.isPlaying) return;
            GetComponent<MinigameManager>().StartMinigame(this);
        }

        protected override void OnMinigameStarted()
        {
            // Replace this with the minigame's initial setup.
            Debug.Log("Template started. Use Finish Example in this component's menu to submit a result.", this);
        }

        [ContextMenu("Finish Example (Play Mode)")]
        private void FinishExample()
        {
            if (!Application.isPlaying || !IsRunning) return;
            // Replace these test values with the results of the real gameplay.
            FinishMinigame(new MinigameResult(testPlayerSlot, testScore));
        }
    }
}
