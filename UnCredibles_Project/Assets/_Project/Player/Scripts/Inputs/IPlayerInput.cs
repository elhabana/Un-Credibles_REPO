using System;
using UnityEngine;

namespace UnCredibles.Players.Inputs
{
    public enum PlayerAction { Jump, Interact, Ability, Pause }

    // Gameplay only talks to this interface, never to a concrete device.
    public interface IPlayerInput : IDisposable
    {
        InputSourceType Source { get; }
        bool IsEnabled { get; set; }
        Vector2 Move { get; }
        bool WasPressed(PlayerAction action);
        bool IsHeld(PlayerAction action);
    }
}
