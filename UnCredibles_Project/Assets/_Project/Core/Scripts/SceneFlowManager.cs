using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnCredibles.Core
{
    // Core stays loaded; one "content" scene (menu, lobby, minigame...) is swapped next to it.
    // Extra additive scenes can be loaded on top when needed.
    public sealed class SceneFlowManager : MonoBehaviour
    {
        public bool IsLoading { get; private set; }
        public string CurrentContentScene { get; private set; }

        public event Action<string> ContentSceneLoaded;

        public void LoadContent(string sceneName, Action onLoaded = null)
        {
            if (IsLoading)
            {
                Debug.LogWarning($"Cannot load {sceneName} while another scene is loading.", this);
                return;
            }
            StartCoroutine(LoadContentRoutine(sceneName, onLoaded));
        }

        public IEnumerator LoadContentRoutine(string sceneName, Action onLoaded = null)
        {
            if (IsLoading) yield break;
            IsLoading = true;
            try
            {
                // Boot is only needed once; drop it together with the previous content.
                yield return Unload(GameScenes.Boot);
                if (!string.IsNullOrEmpty(CurrentContentScene) && CurrentContentScene != sceneName)
                    yield return Unload(CurrentContentScene);

                yield return LoadAdditive(sceneName, true);
                if (!SceneManager.GetSceneByName(sceneName).isLoaded)
                {
                    Debug.LogError($"{sceneName} could not load. Check the Build Profiles scene list.", this);
                    yield break;
                }
                CurrentContentScene = sceneName;
            }
            finally { IsLoading = false; }

            ContentSceneLoaded?.Invoke(sceneName);
            onLoaded?.Invoke();
        }

        // Editor shortcut: when a content scene is played directly, Core adopts it instead of loading the menu.
        public bool TryAdoptLoadedContentScene(out string sceneName)
        {
            sceneName = null;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || scene.name == GameScenes.Core || scene.name == GameScenes.Boot) continue;
                sceneName = scene.name;
                CurrentContentScene = sceneName;
                SceneManager.SetActiveScene(scene);
                return true;
            }
            return false;
        }

        public IEnumerator LoadAdditive(string sceneName, bool setActive)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.isLoaded)
            {
                var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                if (operation == null) yield break;
                yield return operation;
                scene = SceneManager.GetSceneByName(sceneName);
            }
            if (setActive && scene.isLoaded) SceneManager.SetActiveScene(scene);
        }

        public IEnumerator Unload(string sceneName)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.isLoaded) yield break;
            var operation = SceneManager.UnloadSceneAsync(scene);
            if (operation != null) yield return operation;
        }
    }
}
