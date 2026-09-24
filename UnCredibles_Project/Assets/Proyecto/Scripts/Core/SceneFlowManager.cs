using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnCredibles.Core
{
    public sealed class SceneFlowManager : MonoBehaviour
    {
        public bool IsLoading { get; private set; }

        public IEnumerator ShowMainMenu()
        {
            if (IsLoading) yield break;
            IsLoading = true;
            try
            {
                if (!SceneManager.GetSceneByName(GameScenes.MainMenu).isLoaded)
                    yield return SceneManager.LoadSceneAsync(GameScenes.MainMenu, LoadSceneMode.Additive);

                var menu = SceneManager.GetSceneByName(GameScenes.MainMenu);
                if (!menu.isLoaded)
                {
                    Debug.LogError("MainMenu could not load. Check the Build Profiles scene list.");
                    yield break;
                }
                SceneManager.SetActiveScene(menu);
                var boot = SceneManager.GetSceneByName(GameScenes.Boot);
                if (boot.isLoaded) yield return SceneManager.UnloadSceneAsync(boot);
                CoreRoot.Instance.GameFlow.EnterMainMenu();
            }
            finally { IsLoading = false; }
        }
    }
}
