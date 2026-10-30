namespace SkyCoin
{
    /// <summary>
    /// The single list of high level states the game can be in.
    /// Only <see cref="GameManager"/> is allowed to change this value.
    /// </summary>
    public enum GameState
    {
        /// <summary>Nothing is running yet. Used for one frame while a scene loads.</summary>
        Boot,

        /// <summary>Main menu is on screen. No gameplay.</summary>
        MainMenu,

        /// <summary>A level is running and the player can move, collect and be harmed.</summary>
        Playing,

        /// <summary>The player pressed Escape. Gameplay and timers are frozen (timeScale = 0).</summary>
        Paused,

        /// <summary>The level objective was completed. Gameplay is frozen.</summary>
        Victory,

        /// <summary>A loss condition happened (caught, fallen, time expired). Gameplay is frozen.</summary>
        GameOver
    }

    /// <summary>
    /// Why the run ended in <see cref="GameState.GameOver"/>.
    /// Used by the Game Over panel to show one clear reason to the player.
    /// </summary>
    public enum GameOverReason
    {
        None = 0,

        /// <summary>The enemy caught the player (Level 2).</summary>
        Caught = 1,

        /// <summary>The player fell below the level death height.</summary>
        Fallen = 2,

        /// <summary>The Level 2 countdown reached zero.</summary>
        TimeExpired = 3
    }
}
