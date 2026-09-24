using System.Collections;
using UnCredibles.Players;
using UnityEngine;

namespace UnCredibles.Core
{
    [RequireComponent(typeof(SceneFlowManager))]
    public sealed class CoreRoot : MonoBehaviour
    {
        public static CoreRoot Instance { get; private set; }
        public PlayerRegistry Players { get; private set; }
        public GameFlowManager GameFlow { get; private set; }
        public SceneFlowManager Scenes { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Players = new PlayerRegistry();
            GameFlow = new GameFlowManager();
            Scenes = GetComponent<SceneFlowManager>();
        }

        private IEnumerator Start()
        {
            if (Instance != this) yield break;
            yield return Scenes.ShowMainMenu();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
