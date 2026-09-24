using UnityEngine;

namespace UnCredibles.Minigames.Template
{
    // Starting point for new minigames: duplicate the _Template folder and rename.
    public sealed class TemplateMinigameController : MinigameController
    {
        protected override void OnInitialize(MinigameContext context)
        {
            // Spawn player avatars here with Spawns.Spawn(prefab, player) for each of Players.
            Debug.Log($"Template initialized with {Players.Count} player(s).", this);
        }

        protected override void OnGameStarted()
        {
            Debug.Log("Template started. Use 'Finish With Random Scores' in the component menu to end it.", this);
        }

        [ContextMenu("Finish With Random Scores (Play Mode)")]
        private void FinishWithRandomScores()
        {
            if (!IsPlaying) return;
            foreach (var player in Players) Score.AddScore(player.PlayerId, Random.Range(0, 1000));
            EndGame(MinigameEndReason.CustomCondition);
        }
    }
}
