using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnCredibles.Core
{
    // Put it in any content scene (MainMenu, PartyLobby...) so it can be played directly from the editor:
    // if Core is missing it is loaded next to the scene. In the normal Boot flow it does nothing.
    public sealed class CoreSceneLoader : MonoBehaviour
    {
        private void Awake()
        {
            if (CoreRoot.Instance != null || SceneManager.GetSceneByName(GameScenes.Core).isLoaded) return;
            SceneManager.LoadScene(GameScenes.Core, LoadSceneMode.Additive);
        }
    }
}
