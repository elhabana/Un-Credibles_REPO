using System.Collections;
using NUnit.Framework;
using UnCredibles.Core;
using UnCredibles.UI.Garage;
using UnCredibles.UI.PartyLobby;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class GarageFlowTests
{
    private static IEnumerator WaitFor(System.Func<bool> condition)
    {
        float deadline = Time.realtimeSinceStartup + 30f;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.IsTrue(condition(), "Garage flow timed out.");
    }

    [UnityTest]
    public IEnumerator MenuLobbyRepeatedEntryAndMatchReturnKeepSharedScene()
    {
        yield return SceneManager.LoadSceneAsync(GameScenes.MainMenu, LoadSceneMode.Single);
        yield return WaitFor(() => CoreRoot.Instance != null && CoreRoot.Instance.GameFlow.State == GameState.MainMenu);
        var core = CoreRoot.Instance;
        yield return WaitFor(() => !core.Scenes.IsLoading);
        var frontend = Object.FindFirstObjectByType<GarageFrontend>();
        Assert.NotNull(frontend);
        int sceneHandle = frontend.gameObject.scene.handle;
        for (int pass = 0; pass < 2; pass++)
        {
            core.OpenPartyLobby(SessionMode.Local);
            yield return WaitFor(() => frontend.lobbyRoot.activeSelf && frontend.lobbyRoot.GetComponent<PartyLobbyController>().Players != null);
            var lobby = frontend.lobbyRoot.GetComponent<PartyLobbyController>();
            Assert.IsTrue(sceneHandle == frontend.gameObject.scene.handle, "Opening the lobby reloaded the garage.");
            Assert.IsTrue(lobby.AddAI(0));
            Assert.IsTrue(core.Players.OccupiedCount == 1);
            Assert.IsTrue(frontend.lobbyRoot.GetComponent<GaragePlayers>().avatars[0].activeSelf);
            core.ReturnToMainMenu();
            yield return WaitFor(() => !core.Scenes.IsLoading);
            Assert.IsFalse(frontend.lobbyRoot.activeSelf);
            Assert.IsTrue(core.Players.OccupiedCount == 0);
        }
        frontend.ShowSettings();
        yield return new WaitForSecondsRealtime(1f);
        Assert.IsTrue(frontend.cameras.View == GarageView.Settings);
        Assert.IsFalse(frontend.cameras.IsTransitioning);
        float originalVolume = core.Settings.MasterVolume;
        frontend.masterVolume.value = .37f;
        Assert.IsTrue(Mathf.Abs(core.Settings.MasterVolume - .37f) < .001f);
        frontend.masterVolume.value = originalVolume;
        frontend.ShowHome();
        core.OpenPartyLobby(SessionMode.Local);
        yield return WaitFor(() => frontend.lobbyRoot.GetComponent<PartyLobbyController>().Players != null);
        var controller = frontend.lobbyRoot.GetComponent<PartyLobbyController>();
        Assert.IsTrue(controller.AddAI(0));
        Assert.IsTrue(controller.AddAI(1));
        Assert.IsTrue(core.StartMatch(1));
        yield return WaitFor(() => !core.Scenes.IsLoading && core.GameFlow.State == GameState.Minigame);
        core.ReturnToLobby();
        yield return WaitFor(() => !core.Scenes.IsLoading && core.GameFlow.State == GameState.PartyLobby);
        frontend = Object.FindFirstObjectByType<GarageFrontend>();
        yield return WaitFor(() => frontend.lobbyRoot.GetComponent<PartyLobbyController>().Players != null);
        Assert.IsTrue(core.Players.OccupiedCount == 2);
        Assert.IsTrue(frontend.cameras.View == GarageView.Lobby);
        core.ReturnToMainMenu();
        yield return WaitFor(() => !core.Scenes.IsLoading);
    }
}
