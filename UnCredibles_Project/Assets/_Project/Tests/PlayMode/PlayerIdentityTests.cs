using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnCredibles.Minigames;
using UnCredibles.Players;
using UnCredibles.Players.Inputs;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class PlayerIdentityTests
{
    [UnityTest]
    public IEnumerator AllMinigamesKeepSlotColorsAndOutlinedLabels()
    {
        var registry = new PlayerRegistry();
        for (int i = 0; i < 4; i++)
            Assert.IsTrue(registry.TryAddPlayer(PlayerType.LocalPlayer, new AIInput(), null, out _));
        var players = registry.Slots;
        IMinigame pending = null;
        Action<IMinigame> initialize = game => pending = game;
        MinigameHost.Register(initialize);
        try
        {
            foreach (string scene in new [] { "MG_Churro", "MG_CrossyRoad", "MG_MowTheLawn" })
            {
                yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
                // Reproduce the host waiting for remote clients before initialization.
                for (int frame = 0; frame < 10; frame++) yield return null;
                Assert.NotNull(pending);
                pending.Initialize(new MinigameContext(pending.Data, players, true));
                pending = null;
                yield return null;
                yield return null;
                var views = UnityEngine.Object.FindObjectsByType<PlayerPresentation>(FindObjectsSortMode.None);
                Assert.IsTrue(views.Length == 4, scene + " must display all four slots.");
                foreach (var view in views)
                {
                    var text = view.GetComponentInChildren<TextMeshPro>();
                    int index = int.Parse(text.text.Substring(1)) - 1;
                    Assert.IsTrue(text.text == PlayerIdentity.LabelFor(index));
                    Assert.IsTrue(text.color == PlayerIdentity.ColorFor(index), scene + " changed the slot color.");
                    Assert.IsTrue((Color)text.outlineColor == Color.white && text.outlineWidth > 0f);
                    Assert.IsTrue(HasBodyColor(view, text.color), scene + " body and label must match.");
                    Assert.IsTrue(Quaternion.Angle(text.transform.rotation, Camera.main.transform.rotation) < .1f);
                }
                Capture(scene);
            }
            // Online disconnects can replace a human with AI without respawning the avatar.
            var bot = UnityEngine.Object.FindObjectsByType<PlayerPresentation>(FindObjectsSortMode.None)
                .Single(v => v.GetComponentInChildren<TextMeshPro>().text == "P3");
            Assert.IsTrue(registry.ReplaceWithAI(2, new AIInput()));
            yield return null;
            yield return null;
            Assert.IsTrue(bot.GetComponentInChildren<TextMeshPro>().text == "IA");
            Assert.IsTrue(bot.GetComponentInChildren<TextMeshPro>().color == PlayerIdentity.ColorFor(2, true));
            Assert.IsTrue(HasBodyColor(bot, PlayerIdentity.ColorFor(2, true)));
        }
        finally
        {
            MinigameHost.Unregister(initialize);
            registry.Clear();
        }
    }

    private static bool HasBodyColor(PlayerPresentation view, Color expected)
    {
        var block = new MaterialPropertyBlock();
        return view.GetComponentsInChildren<Renderer>().Any(r =>
        {
            if (r.GetComponent<TMP_Text>() != null) return false;
            r.GetPropertyBlock(block);
            return block.GetColor("_BaseColor") == expected;
        });
    }

    private static void Capture(string scene)
    {
        var camera = Camera.main;
        var target = new RenderTexture(1920, 1080, 24);
        var previous = RenderTexture.active;
        var previousTarget = camera.targetTexture;
        var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        image.Apply();
        File.WriteAllBytes("Temp/identity-" + scene + ".png", image.EncodeToPNG());
        camera.targetTexture = previousTarget;
        RenderTexture.active = previous;
        UnityEngine.Object.Destroy(image);
        UnityEngine.Object.Destroy(target);
    }
}
