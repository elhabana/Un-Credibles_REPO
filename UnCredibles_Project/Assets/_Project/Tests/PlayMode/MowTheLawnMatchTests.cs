using System.Collections;
using NUnit.Framework;
using UnCredibles.Minigames;
using UnCredibles.Minigames.MowTheLawn;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace UnCredibles.Tests.PlayMode
{
    // Smoke test of the real scene: DebugBootstrap runs a whole game (P1 idle on keyboard or a bot,
    // three bots) at high speed and it must reach the frenzy and end with a valid ranking.
    public sealed class MowTheLawnMatchTests
    {
        private const float TimeScale = 8f;
        private const float RealSecondsLimit = 60f;

        [UnityTest]
        public IEnumerator BotsPlayAWholeGameAndItEndsWithResults()
        {
            yield return SceneManager.LoadSceneAsync("SCN_MG_MowTheLawn", LoadSceneMode.Single);
            var controller = Object.FindFirstObjectByType<MowTheLawnController>();
            Assert.NotNull(controller);

            bool frenzy = false;
            controller.FrenzyStarted += () => frenzy = true;
            Time.timeScale = TimeScale;
            try
            {
                float deadline = Time.realtimeSinceStartup + RealSecondsLimit;
                while (controller.State != MinigameState.Results && Time.realtimeSinceStartup < deadline) yield return null;
            }
            finally { Time.timeScale = 1f; }

            Assert.AreEqual(MinigameState.Results, controller.State, "the game never ended");
            Assert.IsTrue(frenzy, "the final frenzy never started");

            var results = controller.GetResults();
            Assert.AreEqual(controller.Players.Count, results.Count);
            int best = 0;
            foreach (var result in results)
            {
                Assert.That(result.Placement, Is.InRange(1, results.Count));
                best = Mathf.Max(best, result.Score);
            }
            Assert.Greater(best, 0, "no bot delivered a single bag");
        }
    }
}
