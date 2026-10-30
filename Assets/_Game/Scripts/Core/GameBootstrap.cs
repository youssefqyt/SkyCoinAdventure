using UnityEngine;

namespace SkyCoin
{
    /// <summary>
    /// Creates the persistent manager layer automatically, in code.
    ///
    /// WHY THIS EXISTS
    /// Without it, every scene would need a "GameRoot" object holding GameManager,
    /// GameInput, AudioManager, SaveService and SceneLoader. Forgetting one produces the
    /// classic chain of NullReferenceExceptions, and duplicating it produces two game
    /// states fighting each other.
    ///
    /// With this bootstrap:
    ///   - pressing Play on ANY scene works (no "you must start from Bootstrap" rule),
    ///   - there can never be duplicates,
    ///   - scenes stay focused on level content.
    ///
    /// The object is created before the first scene loads and is marked DontDestroyOnLoad,
    /// so it survives every scene change and is never recreated.
    ///
    /// If a scene already contains a GameManager (for example a hand made one), this
    /// bootstrap stays out of the way.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreatePersistentLayer()
        {
            if (GameManager.Instance != null) return;

            GameObject root = new GameObject("GameRoot (auto)");

            root.AddComponent<GameManager>();
            root.AddComponent<GameInput>();
            root.AddComponent<AudioManager>();
            root.AddComponent<SaveService>();
            root.AddComponent<SceneLoader>();

            Object.DontDestroyOnLoad(root);
        }
    }
}
