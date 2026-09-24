using UnCredibles.Players;
using UnityEngine;

namespace UnCredibles.Minigames
{
    [CreateAssetMenu(fileName = "MG_NewMinigame_Data", menuName = "UnCredibles/Minigame Data")]
    public sealed class MinigameData : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string sceneName;
        [Tooltip("Seconds of gameplay. 0 = no time limit, the minigame ends itself.")]
        [SerializeField, Min(0f)] private float duration = 60f;
        [SerializeField, Min(0)] private int countdownSeconds = 3;
        [SerializeField, Range(1, PlayerRegistry.MaxPlayers)] private int minPlayers = 1;
        [SerializeField, Range(1, PlayerRegistry.MaxPlayers)] private int maxPlayers = PlayerRegistry.MaxPlayers;
        [SerializeField] private Sprite thumbnail;
        [SerializeField] private AudioClip music;
        [SerializeField, TextArea(3, 8)] private string rules;

        public string Id => id;
        public string DisplayName => displayName;
        public string SceneName => sceneName;
        public float Duration => duration;
        public int CountdownSeconds => countdownSeconds;
        public int MinPlayers => minPlayers;
        public int MaxPlayers => maxPlayers;
        public Sprite Thumbnail => thumbnail;
        public AudioClip Music => music;
        public string Rules => rules;

        public bool SupportsPlayerCount(int count) => count >= minPlayers && count <= maxPlayers;

        private void OnValidate()
        {
            if (maxPlayers < minPlayers) maxPlayers = minPlayers;
        }
    }
}
