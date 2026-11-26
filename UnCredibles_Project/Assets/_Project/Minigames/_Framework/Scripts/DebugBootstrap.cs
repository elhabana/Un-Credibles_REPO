using System;
using System.Collections.Generic;
using UnCredibles.Players;
using UnCredibles.Players.Inputs;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnCredibles.Minigames
{
    // Lets a minigame scene run on its own (Open scene -> Play) with temporary players.
    // Does nothing when Core is loaded and hosting the minigame.
    public sealed class DebugBootstrap : MonoBehaviour
    {
        [Serializable]
        private struct DebugPlayer
        {
            public InputSourceType inputSource;
            public string playerName;
        }

        [SerializeField] private MinigameController minigame;
        [SerializeField] private InputActionAsset playerControls;
        [SerializeField] private DebugPlayer[] players =
        {
            new DebugPlayer { inputSource = InputSourceType.Keyboard },
            new DebugPlayer { inputSource = InputSourceType.AI },
            new DebugPlayer { inputSource = InputSourceType.AI },
            new DebugPlayer { inputSource = InputSourceType.AI },
        };
        [SerializeField] private bool skipCountdown;
        [SerializeField, Tooltip("Show the rules card first, like a real match (humans press Jump to start).")]
        private bool showRulesCard;
        [SerializeField, Tooltip("Everybody wears its default suit, like the lobby gives. Empty in the editor = the project's catalog.")]
        private CharacterPartCatalog partCatalog;

        private PlayerRegistry registry;

        private void Start()
        {
            if (MinigameHost.IsAvailable || minigame == null)
            {
                if (minigame == null) Debug.LogError("DebugBootstrap needs a MinigameController.", this);
                enabled = false;
                return;
            }

            registry = new PlayerRegistry();
            var catalog = FindCatalog();
            int gamepadIndex = 0;
            int count = Mathf.Min(players.Length, PlayerRegistry.MaxPlayers);
            for (int i = 0; i < count; i++)
            {
                var input = CreateInput(players[i].inputSource, ref gamepadIndex);
                var type = input is AIInput ? PlayerType.AIPlayer : PlayerType.LocalPlayer;
                if (registry.TryAddPlayer(type, input, players[i].playerName, out var slot) && catalog != null)
                    registry.SetLoadout(slot.SlotIndex, catalog.DefaultLoadout());
            }

            var active = new List<PlayerSlot>(PlayerRegistry.MaxPlayers);
            registry.GetActivePlayers(active);
            Debug.Log($"[DebugBootstrap] Running {minigame.name} with {active.Count} temporary player(s).", this);

            minigame.Finished += LogResults;
            // Not a debug session when the card is wanted: offline that only brings the card (no network here).
            minigame.Initialize(new MinigameContext(minigame.Data, active, !showRulesCard));
            if (skipCountdown && !showRulesCard) minigame.StartGame();
            else minigame.StartCountdown();
        }

        private void OnDestroy()
        {
            if (minigame != null) minigame.Finished -= LogResults;
            registry?.Clear();
        }

        private CharacterPartCatalog FindCatalog()
        {
            if (partCatalog != null) return partCatalog;
#if UNITY_EDITOR
            // Debug runs only happen in the editor: no need to wire the catalog in every minigame scene.
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:" + nameof(CharacterPartCatalog)))
                return UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterPartCatalog>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
#endif
            return null;
        }

        private IPlayerInput CreateInput(InputSourceType source, ref int gamepadIndex)
        {
            if (playerControls != null)
            {
                if (source == InputSourceType.Keyboard && Keyboard.current != null)
                    return new KeyboardInput(playerControls, Keyboard.current);
                if (source == InputSourceType.Gamepad && gamepadIndex < Gamepad.all.Count)
                    return new GamepadInput(playerControls, Gamepad.all[gamepadIndex++]);
            }

            if (source != InputSourceType.AI)
                Debug.LogWarning($"[DebugBootstrap] No {source} available (or no PlayerControls asset). Using AI instead.", this);
            return new AIInput();
        }

        private void LogResults(IReadOnlyList<MinigameResult> results)
        {
            foreach (var result in results)
                Debug.Log($"[DebugBootstrap] #{result.Placement} Player {result.PlayerId}: {result.Score}", this);
        }
    }
}
