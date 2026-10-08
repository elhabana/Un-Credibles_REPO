namespace UnCredibles.Core
{
    public static class GameScenes
    {
        public const string Boot = "00_Boot";
        public const string Core = "01_Core";
        public const string MainMenu = "02_MainMenu";
        // Menu and lobby are two views of the same garage.
        public const string PartyLobby = MainMenu;
        public const string Results = "04_Results";
    }
}
