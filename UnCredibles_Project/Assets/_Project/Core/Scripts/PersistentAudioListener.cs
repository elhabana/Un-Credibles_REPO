using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnCredibles.Core
{
    // Exists before Boot and survives the gaps while content scenes are unloaded/loaded.
    [RequireComponent(typeof(AudioListener))]
    public sealed class PersistentAudioListener : MonoBehaviour
    {
        private static PersistentAudioListener instance;
        private Camera viewCamera;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            if (instance == null) new GameObject("Persistent Audio Listener").AddComponent<PersistentAudioListener>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                GetComponent<AudioListener>().enabled = false;
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += SceneLoaded;
            UsePersistentListener();
        }

        private void SceneLoaded(Scene scene, LoadSceneMode mode) => UsePersistentListener();

        private void UsePersistentListener()
        {
            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                listener.enabled = listener.gameObject == gameObject;
            viewCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (viewCamera == null || !viewCamera.isActiveAndEnabled) viewCamera = Camera.main;
            if (viewCamera != null)
                transform.SetPositionAndRotation(viewCamera.transform.position, viewCamera.transform.rotation);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            if (instance == this) instance = null;
        }
    }
}
