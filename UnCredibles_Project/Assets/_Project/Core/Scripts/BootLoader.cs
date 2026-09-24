using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnCredibles.Core
{
    public sealed class BootLoader : MonoBehaviour
    {
        private IEnumerator Start()
        {
            if (!SceneManager.GetSceneByName(GameScenes.Core).isLoaded)
                yield return SceneManager.LoadSceneAsync(GameScenes.Core, LoadSceneMode.Additive);
        }
    }
}
