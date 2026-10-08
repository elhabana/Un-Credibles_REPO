using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnCredibles.Core;
using UnCredibles.UI.Garage;
using UnCredibles.UI.PartyLobby;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class BootAudioTests
{
    private static void AssertSingleListener()
    {
        var active = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None)
            .Where(listener => listener.isActiveAndEnabled).ToArray();
        Assert.IsTrue(active.Length == 1, "Expected exactly one active audio listener, found " + active.Length);
        Assert.NotNull(active[0].GetComponent<PersistentAudioListener>());
    }

    private static IEnumerator WaitFor(System.Func<bool> condition)
    {
        float deadline = Time.realtimeSinceStartup + 30f;
        while (!condition() && Time.realtimeSinceStartup < deadline)
        {
            AssertSingleListener();
            yield return null;
        }
        Assert.IsTrue(condition(), "Audio continuity flow timed out.");
        AssertSingleListener();
    }

    [UnityTest]
    public IEnumerator BootMenuMinigameAndReturnAlwaysHaveOneListener()
    {
        var loading = SceneManager.LoadSceneAsync(GameScenes.Boot, LoadSceneMode.Single);
        yield return WaitFor(() => loading.isDone);
        yield return WaitFor(() => CoreRoot.Instance != null && CoreRoot.Instance.GameFlow.State == GameState.MainMenu && !CoreRoot.Instance.Scenes.IsLoading);
        var core = CoreRoot.Instance;
        core.OpenPartyLobby(SessionMode.Local);
        PartyLobbyController lobby = null;
        yield return WaitFor(() =>
        {
            var frontend = Object.FindFirstObjectByType<GarageFrontend>();
            if (frontend != null && frontend.lobbyRoot.activeSelf) lobby = frontend.lobbyRoot.GetComponent<PartyLobbyController>();
            return lobby != null && lobby.Players != null;
        });
        Assert.IsTrue(lobby.AddAI(0));
        Assert.IsTrue(lobby.AddAI(1));
        Assert.IsTrue(core.StartMatch(1));
        yield return WaitFor(() => !core.Scenes.IsLoading && core.GameFlow.State == GameState.Minigame);
        core.ReturnToLobby();
        yield return WaitFor(() => !core.Scenes.IsLoading && core.GameFlow.State == GameState.PartyLobby);
        core.ReturnToMainMenu();
        yield return WaitFor(() => !core.Scenes.IsLoading && core.GameFlow.State == GameState.MainMenu);
    }
}
