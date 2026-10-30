using UnityEngine;

namespace SkyCoin
{
    /// <summary>
    /// Per level configuration. One asset per level, in Assets/_Game/Data/.
    ///
    /// Keeping the rules in a ScriptableObject instead of hard coded constants means the
    /// LevelController script never has to change when you rebalance a level: you edit
    /// numbers in the Inspector while the game is running.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelDefinition_", menuName = "Sky Coin Adventure/Level Definition", order = 0)]
    public class LevelDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Niveau 1";

        [Tooltip("1-based level number. Used for saving progress.")]
        [Min(1)] public int levelNumber = 1;

        [Header("Objective")]
        [Tooltip("How many coins must be collected to win. Level 1 = 30, Level 2 = 50.")]
        [Min(1)] public int requiredCoins = 30;

        [Tooltip("Leave empty to auto generate 'Récupère N pièces !'.")]
        public string objectiveOverride = "";

        [Header("Failure")]
        [Tooltip("Falling below this world Y ends the run. All islands sit at y = 0, so -8 is unambiguous.")]
        public float deathHeight = -8f;

        [Header("Countdown")]
        public bool useCountdownTimer = false;

        [Tooltip("Seconds allowed. Level 2 starts at 180.")]
        [Min(5f)] public float timeLimitSeconds = 180f;

        [Header("Enemy")]
        public bool useEnemy = false;

        [Tooltip("Seconds before the enemy starts chasing. Gives the player a safe start.")]
        [Min(0f)] public float enemyActivationDelaySeconds = 1.5f;

        [Header("Progression")]
        [Tooltip("Scene name of the next level. Empty means the victory panel hides its 'Niveau suivant' button.")]
        public string nextLevelScene = "";

        [Header("Practice mode (optional, off by default)")]
        [Tooltip("When true, falling sends the player back to the authored checkpoint instead of ending the run.")]
        public bool practiceMode = false;

        /// <summary>The line shown in the HUD under the coin counter.</summary>
        public string ObjectiveText =>
            string.IsNullOrWhiteSpace(objectiveOverride)
                ? $"Récupère {requiredCoins} pièces !"
                : objectiveOverride;

        public bool HasNextLevel => !string.IsNullOrWhiteSpace(nextLevelScene);

        private void OnValidate()
        {
            if (requiredCoins < 1) requiredCoins = 1;
            if (timeLimitSeconds < 5f) timeLimitSeconds = 5f;
        }
    }
}
