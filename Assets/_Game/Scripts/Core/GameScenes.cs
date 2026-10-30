namespace SkyCoin
{
    /// <summary>
    /// Every scene name in one place. These strings must match the scene file names
    /// exactly, including capital letters and underscores.
    ///
    /// Using constants instead of typing strings everywhere stops the classic
    /// "LoadScene did nothing" bug caused by a typo.
    /// </summary>
    public static class GameScenes
    {
        public const string MainMenu = "MainMenu";
        public const string Level01 = "Level_01";
        public const string Level02 = "Level_02";

        /// <summary>Scene name for a 1-based level number.</summary>
        public static string ForLevelNumber(int levelNumber)
        {
            switch (levelNumber)
            {
                case 2: return Level02;
                default: return Level01;
            }
        }
    }
}
