namespace UnCredibles.Players
{
    public enum PlayerType { Empty, LocalPlayer, PrestoPadPlayer, OnlinePlayer, AIPlayer }

    public enum SlotState { Empty, Inviting, Connecting, Occupied, AI, Disconnected }

    public enum InputSourceType { None, Keyboard, Gamepad, PrestoPad, AI, Network }
}
