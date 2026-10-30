using System.Collections.Generic;
using System.Reflection;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SkyCoin.EditorTools
{
    /// <summary>
    /// EDITOR ONLY. Builds the whole playable game from one menu item:
    ///
    ///     Sky Coin Adventure > Setup > 2. Build The Whole Game
    ///
    /// It creates, in this order:
    ///   1. the five gameplay layers (Ground, Player, Enemy, Coin, Hazard),
    ///   2. the material palette (by calling the Milestone 1 material builder),
    ///   3. the two LevelDefinition assets in Assets/_Game/Data,
    ///   4. five prefabs (Player, Coin, Enemy_Chaser, Floor_Hole, MovingPlatform),
    ///   5. three complete scenes (MainMenu, Level_01, Level_02),
    ///   6. the Build Profiles scene list.
    ///
    /// It is IDEMPOTENT and ADDITIVE: running it again rebuilds the generated scenes and
    /// prefabs from scratch, and it never deletes anything else you created. Your own
    /// objects are left alone.
    ///
    /// Coins are no longer authored by hand: each level gets a CoinSpawner that drops its pool
    /// onto validated ground and then moves every coin to a new spot in waves, so the map always
    /// has coins to chase and a coin can never end up inside geometry or floating over a gap.
    /// </summary>
    public static class SkyCoinProjectBuilder
    {
        // ------------------------------------------------------------------
        //  Paths
        // ------------------------------------------------------------------

        private const string MainMenuScenePath = SkyCoinFactory.ScenesFolder + "/MainMenu.unity";
        private const string Level01ScenePath = SkyCoinFactory.ScenesFolder + "/Level_01.unity";
        private const string Level02ScenePath = SkyCoinFactory.ScenesFolder + "/Level_02.unity";

        private const string CoinPrefabPath = SkyCoinFactory.PrefabsFolder + "/Collectibles/Coin.prefab";
        private const string PlayerPrefabPath = SkyCoinFactory.PrefabsFolder + "/Player/Player.prefab";
        private const string EnemyPrefabPath = SkyCoinFactory.PrefabsFolder + "/Enemies/Enemy_Chaser.prefab";
        private const string FloorHolePrefabPath = SkyCoinFactory.PrefabsFolder + "/Hazards/Floor_Hole.prefab";
        private const string MovingPlatformPrefabPath = SkyCoinFactory.PrefabsFolder + "/Hazards/MovingPlatform.prefab";

        private const string Level01DefinitionPath = SkyCoinFactory.DataFolder + "/LevelDefinition_01.asset";
        private const string Level02DefinitionPath = SkyCoinFactory.DataFolder + "/LevelDefinition_02.asset";

        private const float CoinHeightAboveFloor = 1.0f;

        /// <summary>Seconds between two waves of coin relocation. Set on every built CoinSpawner.</summary>
        private const float CoinRelocateInterval = 3f;
        private const float PlayerSpawnHeight = 0.05f;
        private static readonly Vector3 PlayerSpawnPosition = new Vector3(0f, PlayerSpawnHeight, -4f);

        // Layer indices, resolved once per build.
        private static int _groundLayer;
        private static int _playerLayer;
        private static int _enemyLayer;
        private static int _coinLayer;
        private static int _hazardLayer;

        // ------------------------------------------------------------------
        //  Menu items
        // ------------------------------------------------------------------

        // Menu entry disabled: the game is already built into the scenes.
        // [MenuItem("Sky Coin Adventure/Setup/2. Build The Whole Game", false, 20)]
        public static void BuildWholeGame()
        {
            if (!Prepare(out string problem))
            {
                EditorUtility.DisplayDialog("Sky Coin Adventure", problem, "OK");
                Debug.LogError($"[SkyCoin] Build aborted: {problem}");
                return;
            }

            // Never throw away unsaved work silently.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[SkyCoin] Build cancelled by the user (current scene was not saved).");
                return;
            }

            try
            {
                LevelDefinition level01 = CreateLevelDefinition(Level01DefinitionPath, 1);
                LevelDefinition level02 = CreateLevelDefinition(Level02DefinitionPath, 2);

                // A scratch scene to author the prefabs in, so no real scene is touched.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                GameObject coinPrefab = CreateCoinPrefab();
                GameObject playerPrefab = CreatePlayerPrefab();
                GameObject enemyPrefab = CreateEnemyPrefab();
                GameObject floorHolePrefab = CreateFloorHolePrefab();
                GameObject movingPlatformPrefab = CreateMovingPlatformPrefab();

                BuildMainMenuScene();

                int coinsInLevel01 = BuildLevelScene(1, level01, coinPrefab, playerPrefab, null, null, null);
                int coinsInLevel02 = BuildLevelScene(2, level02, coinPrefab, playerPrefab, enemyPrefab, floorHolePrefab, movingPlatformPrefab);

                RegisterScenesInBuildSettings();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorSceneManager.OpenScene(Level01ScenePath, OpenSceneMode.Single);

                Debug.Log($"[SkyCoin] BUILD COMPLETE. Level_01: {coinsInLevel01} coins in play at once (" +
                          $"{level01.requiredCoins} to collect). Level_02: {coinsInLevel02} coins in play at once (" +
                          $"{level02.requiredCoins} to collect).");

                EditorUtility.DisplayDialog(
                    "Sky Coin Adventure",
                    "The game was built successfully.\n\n" +
                    "Scenes created:\n" +
                    "   Assets/_Game/Scenes/MainMenu.unity\n" +
                    "   Assets/_Game/Scenes/Level_01.unity\n" +
                    "   Assets/_Game/Scenes/Level_02.unity\n\n" +
                    $"Level_01: {coinsInLevel01} coins on the map at once, {level01.requiredCoins} to collect.\n" +
                    $"Level_02: {coinsInLevel02} coins on the map at once, {level02.requiredCoins} to collect.\n" +
                    "All coins jump to new random spots every 3 seconds.\n\n" +
                    "Level_01 is now open. Press Play to test.",
                    "Great");
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[SkyCoin] The build threw an exception and stopped:\n{exception}");
                EditorUtility.DisplayDialog("Sky Coin Adventure",
                    "The build failed. See the Console for the full error:\n\n" + exception.Message, "OK");
            }
        }

        // Menu entry disabled.
        // [MenuItem("Sky Coin Adventure/Setup/3. Validate The Built Scenes", false, 30)]
        public static void ValidateLevels()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            int problems = 0;
            problems += ValidateLevelScene(Level01ScenePath, 1);
            problems += ValidateLevelScene(Level02ScenePath, 2);

            EditorUtility.DisplayDialog("Sky Coin Adventure",
                problems == 0
                    ? "Validation passed. Both levels look correct."
                    : $"Validation finished with {problems} problem(s). See the Console.",
                "OK");
        }

        // Menu entry disabled.
        // [MenuItem("Sky Coin Adventure/Setup/4. Reset Saved Progress", false, 50)]
        public static void ResetSavedProgress()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("[SkyCoin] Saved progress and settings were reset. 'Continuer' is now disabled.");
            EditorUtility.DisplayDialog("Sky Coin Adventure", "Saved progress cleared.", "OK");
        }

        // ------------------------------------------------------------------
        //  Preparation
        // ------------------------------------------------------------------

        private static bool Prepare(out string problem)
        {
            problem = null;

            // TextMeshPro needs its essential resources before any text can be created.
            // TMP_Settings.instance is checked FIRST: defaultFontAsset dereferences it and
            // would throw a NullReferenceException when the settings asset does not exist yet.
            if (TMP_Settings.instance == null)
            {
                problem = "TextMeshPro essential resources are missing.\n\n" +
                          "Fix it with: Window > TextMeshPro > Import TMP Essential Resources\n" +
                          "Then run 'Sky Coin Adventure > Setup > 2. Build The Whole Game' again.";
                return false;
            }

            if (TMP_Settings.defaultFontAsset == null)
            {
                problem = "TextMeshPro has no default font asset.\n\n" +
                          "Fix it with: Window > TextMeshPro > Import TMP Essential Resources";
                return false;
            }

            EnsureFolders();

            // Materials first: the prefabs and scenes reference them.
            SkyCoinMaterialBuilder.CreateMaterials();

            if (SkyCoinFactory.LoadMaterial("M_Coin") == null || SkyCoinFactory.LoadMaterial("M_Ground_Grass") == null)
            {
                problem = "The material palette could not be created. Check the Console for details.";
                return false;
            }

            EnsureLayers();

            _groundLayer = SkyCoinFactory.RequireLayer("Ground");
            _playerLayer = SkyCoinFactory.RequireLayer("Player");
            _enemyLayer = SkyCoinFactory.RequireLayer("Enemy");
            _coinLayer = SkyCoinFactory.RequireLayer("Coin");
            _hazardLayer = SkyCoinFactory.RequireLayer("Hazard");

            if (_groundLayer < 0 || _playerLayer < 0 || _enemyLayer < 0 || _coinLayer < 0 || _hazardLayer < 0)
            {
                problem = "The gameplay layers could not be created.\n\n" +
                          "Create them by hand in Edit > Project Settings > Tags and Layers:\n" +
                          "   6 = Ground, 7 = Player, 8 = Enemy, 9 = Coin, 10 = Hazard";
                return false;
            }

            return true;
        }

        private static void EnsureFolders()
        {
            SkyCoinFactory.EnsureFolder(SkyCoinFactory.MaterialsFolder);
            SkyCoinFactory.EnsureFolder(SkyCoinFactory.PrefabsFolder + "/Collectibles");
            SkyCoinFactory.EnsureFolder(SkyCoinFactory.PrefabsFolder + "/Player");
            SkyCoinFactory.EnsureFolder(SkyCoinFactory.PrefabsFolder + "/Enemies");
            SkyCoinFactory.EnsureFolder(SkyCoinFactory.PrefabsFolder + "/Hazards");
            SkyCoinFactory.EnsureFolder(SkyCoinFactory.DataFolder);
            SkyCoinFactory.EnsureFolder(SkyCoinFactory.ScenesFolder);
            SkyCoinFactory.EnsureFolder(SkyCoinFactory.SettingsFolder);
        }

        /// <summary>Creates the missing gameplay layers in ProjectSettings/TagManager.asset.</summary>
        private static void EnsureLayers()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");

            if (assets == null || assets.Length == 0)
            {
                Debug.LogError("[SkyCoin] Could not open ProjectSettings/TagManager.asset. Create the layers by hand.");
                return;
            }

            SerializedObject tagManager = new SerializedObject(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            if (layers == null || !layers.isArray)
            {
                Debug.LogError("[SkyCoin] TagManager.asset has no 'layers' array. Create the layers by hand.");
                return;
            }

            ApplyLayer(layers, 6, "Ground");
            ApplyLayer(layers, 7, "Player");
            ApplyLayer(layers, 8, "Enemy");
            ApplyLayer(layers, 9, "Coin");
            ApplyLayer(layers, 10, "Hazard");

            tagManager.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        private static void ApplyLayer(SerializedProperty layers, int index, string layerName)
        {
            for (int i = 0; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == layerName) return;   // already exists
            }

            if (index >= layers.arraySize)
            {
                Debug.LogError($"[SkyCoin] Layer index {index} is out of range for '{layerName}'.");
                return;
            }

            layers.GetArrayElementAtIndex(index).stringValue = layerName;
            Debug.Log($"[SkyCoin] Created layer {index}: {layerName}");
        }

        // ------------------------------------------------------------------
        //  Level definitions
        // ------------------------------------------------------------------

        private static LevelDefinition CreateLevelDefinition(string assetPath, int levelNumber)
        {
            SkyCoinFactory.EnsureFolder(SkyCoinFactory.DataFolder);

            LevelDefinition definition = AssetDatabase.LoadAssetAtPath<LevelDefinition>(assetPath);

            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<LevelDefinition>();
                AssetDatabase.CreateAsset(definition, assetPath);
            }

            // Shared rules.
            definition.levelNumber = levelNumber;
            definition.deathHeight = -8f;
            definition.practiceMode = false;

            if (levelNumber == 1)
            {
                definition.displayName = "Niveau 1";
                definition.requiredCoins = 30;
                definition.objectiveOverride = "";
                definition.useCountdownTimer = false;
                definition.timeLimitSeconds = 180f;
                definition.useEnemy = false;
                definition.enemyActivationDelaySeconds = 1.5f;
                definition.nextLevelScene = GameScenes.Level02;
            }
            else
            {
                definition.displayName = "Niveau 2";
                definition.requiredCoins = 50;
                definition.objectiveOverride = "";
                definition.useCountdownTimer = true;
                definition.timeLimitSeconds = 180f;
                definition.useEnemy = true;
                definition.enemyActivationDelaySeconds = 1.5f;
                definition.nextLevelScene = "";     // no level 3, so the victory panel hides the button
            }

            EditorUtility.SetDirty(definition);
            return definition;
        }

        // ------------------------------------------------------------------
        //  Prefabs
        // ------------------------------------------------------------------

        private static GameObject SavePrefab(GameObject instance, string assetPath)
        {
            string folder = System.IO.Path.GetDirectoryName(assetPath).Replace('\\', '/');
            SkyCoinFactory.EnsureFolder(folder);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, assetPath);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        private static GameObject CreateCoinPrefab()
        {
            GameObject root = SkyCoinFactory.CreateEmpty("Coin", null, Vector3.zero);
            root.layer = _coinLayer;

            // The trigger lives on the stable root, so the spin animation can never move
            // the collection volume away from where the player aims.
            SphereCollider trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.45f;
            trigger.center = Vector3.zero;

            Coin coin = root.AddComponent<Coin>();

            // Spin pivot: the Coin script rotates this, so the disc rotates around the
            // world up axis like a real coin instead of tumbling.
            GameObject pivot = SkyCoinFactory.CreateEmpty("CoinVisual", root.transform, Vector3.zero);

            SkyCoinFactory.CreatePrimitive(PrimitiveType.Cylinder, "CoinDisc", pivot.transform,
                Vector3.zero, new Vector3(0.75f, 0.05f, 0.75f), SkyCoinFactory.LoadMaterial("M_Coin"),
                _coinLayer, new Vector3(90f, 0f, 0f), keepCollider: false);

            SkyCoinFactory.SetReference(coin, "visualRoot", pivot.transform);

            return SavePrefab(root, CoinPrefabPath);
        }

        private static GameObject CreatePlayerPrefab()
        {
            GameObject root = SkyCoinFactory.CreateEmpty("Player", null, Vector3.zero);
            root.layer = _playerLayer;

            // ------------------------------------------------------------------
            //  CharacterController: the ONLY thing that moves the player.
            //  There is deliberately no Rigidbody on this object.
            // ------------------------------------------------------------------
            CharacterController controller = root.AddComponent<CharacterController>();
            controller.height = 1.7f;
            controller.radius = 0.4f;
            controller.center = new Vector3(0f, 0.85f, 0f);
            controller.slopeLimit = 45f;
            controller.stepOffset = 0.3f;
            controller.skinWidth = 0.03f;
            controller.minMoveDistance = 0f;   // must be 0, or tiny moves are swallowed

            PlayerController playerController = root.AddComponent<PlayerController>();
            PlayerVisuals visuals = root.AddComponent<PlayerVisuals>();

            // ------------------------------------------------------------------
            //  Visuals (children only: they never carry a collider)
            // ------------------------------------------------------------------
            GameObject visualRoot = SkyCoinFactory.CreateEmpty("VisualRoot", root.transform, Vector3.zero);

            Material bodyMaterial = SkyCoinFactory.LoadMaterial("M_Robot_Body");
            Material darkMaterial = SkyCoinFactory.LoadMaterial("M_Robot_Dark");
            Material accentMaterial = SkyCoinFactory.LoadMaterial("M_Robot_Accent");
            Material visorMaterial = SkyCoinFactory.LoadMaterial("M_Visor");

            SkyCoinFactory.CreatePrimitive(PrimitiveType.Capsule, "Body", visualRoot.transform,
                new Vector3(0f, 0.72f, 0f), new Vector3(0.66f, 0.72f, 0.66f), bodyMaterial, _playerLayer,
                default, keepCollider: false);

            SkyCoinFactory.CreatePrimitive(PrimitiveType.Sphere, "Head", visualRoot.transform,
                new Vector3(0f, 1.5f, 0f), new Vector3(0.5f, 0.5f, 0.5f), accentMaterial, _playerLayer,
                default, keepCollider: false);

            GameObject visor = SkyCoinFactory.CreatePrimitive(PrimitiveType.Cube, "Visor", visualRoot.transform,
                new Vector3(0f, 1.52f, 0.24f), new Vector3(0.3f, 0.1f, 0.06f), visorMaterial, _playerLayer,
                default, keepCollider: false);

            Transform armLeft = CreateLimb("ArmLeft", visualRoot.transform, new Vector3(0.43f, 1.22f, 0f), new Vector3(0.15f, 0.45f, 0.15f), bodyMaterial);
            Transform armRight = CreateLimb("ArmRight", visualRoot.transform, new Vector3(-0.43f, 1.22f, 0f), new Vector3(0.15f, 0.45f, 0.15f), bodyMaterial);
            Transform legLeft = CreateLimb("LegLeft", visualRoot.transform, new Vector3(0.17f, 0.78f, 0f), new Vector3(0.17f, 0.7f, 0.17f), darkMaterial);
            Transform legRight = CreateLimb("LegRight", visualRoot.transform, new Vector3(-0.17f, 0.78f, 0f), new Vector3(0.17f, 0.7f, 0.17f), darkMaterial);

            // ------------------------------------------------------------------
            //  Pickup sensor.
            //  A CharacterController never raises trigger callbacks by itself, so the coin
            //  triggers are caught by this small kinematic Rigidbody on a child object.
            // ------------------------------------------------------------------
            GameObject sensor = SkyCoinFactory.CreateEmpty("PickupSensor", root.transform, Vector3.zero);
            sensor.layer = _playerLayer;

            SphereCollider sensorCollider = sensor.AddComponent<SphereCollider>();
            sensorCollider.isTrigger = true;
            sensorCollider.radius = 0.55f;
            sensorCollider.center = new Vector3(0f, 0.85f, 0f);

            Rigidbody sensorBody = sensor.AddComponent<Rigidbody>();
            sensorBody.isKinematic = true;
            sensorBody.useGravity = false;
            sensorBody.interpolation = RigidbodyInterpolation.None;
            sensorBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            // ------------------------------------------------------------------
            //  Wiring
            // ------------------------------------------------------------------
            SkyCoinFactory.SetReference(playerController, "visualRoot", visualRoot.transform);
            SkyCoinFactory.SetLayerMask(playerController, "groundMask", 1 << _groundLayer);

            SkyCoinFactory.SetReference(visuals, "player", playerController);
            SkyCoinFactory.SetReference(visuals, "visualRoot", visualRoot.transform);
            SkyCoinFactory.SetReference(visuals, "armLeft", armLeft);
            SkyCoinFactory.SetReference(visuals, "armRight", armRight);
            SkyCoinFactory.SetReference(visuals, "legLeft", legLeft);
            SkyCoinFactory.SetReference(visuals, "legRight", legRight);
            SkyCoinFactory.SetReference(visuals, "visorRenderer", visor.GetComponent<Renderer>());

            return SavePrefab(root, PlayerPrefabPath);
        }

        /// <summary>Creates a limb as an empty pivot plus a mesh hanging below it.</summary>
        private static Transform CreateLimb(string name, Transform parent, Vector3 pivot, Vector3 meshScale, Material material)
        {
            GameObject limb = SkyCoinFactory.CreateEmpty(name, parent, pivot);

            SkyCoinFactory.CreatePrimitive(PrimitiveType.Cube, name + "Mesh", limb.transform,
                new Vector3(0f, -meshScale.y * 0.5f, 0f), meshScale, material, _playerLayer,
                default, keepCollider: false);

            return limb.transform;
        }

        private static GameObject CreateEnemyPrefab()
        {
            GameObject root = SkyCoinFactory.CreateEmpty("Enemy_Chaser", null, Vector3.zero);
            root.layer = _enemyLayer;

            // No collider on the enemy on purpose: capture is a distance test, so the enemy
            // can never push the player around or shove them off a platform by accident.
            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 1.7f;
            agent.baseOffset = 0f;
            agent.speed = 4.6f;                 // slower than the player's 6
            agent.acceleration = 22f;
            agent.angularSpeed = 420f;
            agent.stoppingDistance = 0f;
            agent.autoBraking = false;
            agent.autoRepath = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

            root.AddComponent<EnemyChaser>();

            GameObject visualRoot = SkyCoinFactory.CreateEmpty("VisualRoot", root.transform, Vector3.zero);

            SkyCoinFactory.CreatePrimitive(PrimitiveType.Capsule, "Body", visualRoot.transform,
                new Vector3(0f, 0.72f, 0f), new Vector3(0.7f, 0.72f, 0.7f), SkyCoinFactory.LoadMaterial("M_Enemy_Body"),
                _enemyLayer, default, keepCollider: false);

            SkyCoinFactory.CreatePrimitive(PrimitiveType.Sphere, "Eye", visualRoot.transform,
                new Vector3(0f, 1.25f, 0.3f), new Vector3(0.3f, 0.3f, 0.3f), SkyCoinFactory.LoadMaterial("M_Enemy_Eye"),
                _enemyLayer, default, keepCollider: false);

            return SavePrefab(root, EnemyPrefabPath);
        }

        private static GameObject CreateFloorHolePrefab()
        {
            // Keeps its BoxCollider: that collider is the surface the player stands on.
            GameObject root = SkyCoinFactory.CreatePrimitive(PrimitiveType.Cube, "Floor_Hole", null,
                Vector3.zero, Vector3.one, SkyCoinFactory.LoadMaterial("M_Hazard_Safe"), _groundLayer);

            DisappearingFloor floor = root.AddComponent<DisappearingFloor>();
            SkyCoinFactory.SetReference(floor, "plateRenderer", root.GetComponent<Renderer>());
            SkyCoinFactory.SetFloat(floor, "openSinkDepth", 8f);

            return SavePrefab(root, FloorHolePrefabPath);
        }

        private static GameObject CreateMovingPlatformPrefab()
        {
            GameObject root = SkyCoinFactory.CreatePrimitive(PrimitiveType.Cube, "MovingPlatform", null,
                Vector3.zero, Vector3.one, SkyCoinFactory.LoadMaterial("M_Bridge_Wood"), _groundLayer);

            MovingPlatform platform = root.AddComponent<MovingPlatform>();
            SkyCoinFactory.SetVector3(platform, "travelOffset", new Vector3(5f, 0f, 0f));
            SkyCoinFactory.SetFloat(platform, "travelDuration", 4f);
            SkyCoinFactory.SetFloat(platform, "pauseAtEnds", 0.9f);

            return SavePrefab(root, MovingPlatformPrefabPath);
        }

        // ------------------------------------------------------------------
        //  Scenes
        // ------------------------------------------------------------------

        private static void BuildMainMenuScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SkyCoinFactory.CreateDirectionalLight(null);
            SkyCoinFactory.CreateGlobalVolume(null);

            Camera camera = SkyCoinFactory.CreateCamera("Main Camera", null, new Vector3(0f, 2.6f, -8f));
            camera.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.06f, 0.11f);

            BuildMenuScenery();

            Canvas canvas = SkyCoinFactory.CreateCanvas("MenuCanvas", null, 0);
            SkyCoinFactory.CreateEventSystem(null);

            SkyCoinFactory.CreatePanel("Backdrop", canvas.transform, new Color(0.02f, 0.03f, 0.07f, 0.45f), false);

            TMP_Text title = SkyCoinFactory.CreateText("Title", canvas.transform, "SKY COIN ADVENTURE",
                84f, TextAlignmentOptions.Center, new Color(1f, 0.86f, 0.35f));
            SkyCoinFactory.PlaceCentered(title.rectTransform, 0f, 320f, 1500f, 120f);

            TMP_Text subtitle = SkyCoinFactory.CreateText("Subtitle", canvas.transform,
                "Un jeu de plateforme 3D  -  Unity 6", 30f, TextAlignmentOptions.Center, new Color(0.8f, 0.87f, 1f));
            SkyCoinFactory.PlaceCentered(subtitle.rectTransform, 0f, 250f, 1200f, 50f);

            Button newGameButton = SkyCoinFactory.CreateButton("Button_NewGame", canvas.transform, "Nouvelle partie",
                0f, 100f, 460f, 72f, SkyCoinFactory.ButtonPrimary, 32f);

            Button continueButton = SkyCoinFactory.CreateButton("Button_Continue", canvas.transform, "Continuer",
                0f, 18f, 460f, 72f, SkyCoinFactory.ButtonNormal, 32f);

            TMP_Text continueLabel = continueButton.GetComponentInChildren<TMP_Text>();

            Button levelSelectButton = SkyCoinFactory.CreateButton("Button_LevelSelect", canvas.transform, "Sélection du niveau",
                0f, -64f, 460f, 72f, SkyCoinFactory.ButtonNeutral, 30f);

            Button optionsButton = SkyCoinFactory.CreateButton("Button_Options", canvas.transform, "Options",
                0f, -146f, 460f, 72f, SkyCoinFactory.ButtonNeutral, 30f);

            Button quitButton = SkyCoinFactory.CreateButton("Button_Quit", canvas.transform, "Quitter",
                0f, -228f, 460f, 72f, SkyCoinFactory.ButtonDanger, 30f);

            TMP_Text footer = SkyCoinFactory.CreateText("Footer", canvas.transform,
                "ZQSD / WASD / Flèches : se déplacer     Souris : regarder     Espace : sauter     Maj : courir     Échap : pause",
                22f, TextAlignmentOptions.Center, new Color(0.65f, 0.72f, 0.85f));
            SkyCoinFactory.PlaceCentered(footer.rectTransform, 0f, -330f, 1700f, 40f);

            // ---- level select ----
            RectTransform levelSelectCard = SkyCoinFactory.CreateCenterCard("LevelSelectPanel", canvas.transform,
                new Vector2(760f, 480f), SkyCoinFactory.PanelSolid);

            TMP_Text levelSelectTitle = SkyCoinFactory.CreateText("Title", levelSelectCard, "Sélection du niveau",
                46f, TextAlignmentOptions.Center, Color.white);
            SkyCoinFactory.PlaceCentered(levelSelectTitle.rectTransform, 0f, 175f, 700f, 60f);

            TMP_Text levelSelectHint = SkyCoinFactory.CreateText("Hint", levelSelectCard,
                "Le niveau 2 se débloque en terminant le niveau 1.", 22f,
                TextAlignmentOptions.Center, new Color(0.72f, 0.78f, 0.9f));
            SkyCoinFactory.PlaceCentered(levelSelectHint.rectTransform, 0f, 122f, 700f, 40f);

            Button level1Button = SkyCoinFactory.CreateButton("Button_Level1", levelSelectCard, "Niveau 1   (30 pièces)",
                0f, 40f, 520f, 68f, SkyCoinFactory.ButtonPrimary, 28f);

            Button level2Button = SkyCoinFactory.CreateButton("Button_Level2", levelSelectCard, "Niveau 2   (50 pièces)",
                0f, -40f, 520f, 68f, SkyCoinFactory.ButtonNormal, 28f);

            Button levelSelectCloseButton = SkyCoinFactory.CreateButton("Button_Close", levelSelectCard, "Fermer",
                0f, -150f, 520f, 68f, SkyCoinFactory.ButtonNeutral, 28f);

            levelSelectCard.gameObject.SetActive(false);

            // ---- confirm overwrite ----
            RectTransform confirmCard = SkyCoinFactory.CreateCenterCard("ConfirmPanel", canvas.transform,
                new Vector2(820f, 360f), SkyCoinFactory.PanelSolid);

            TMP_Text confirmTitle = SkyCoinFactory.CreateText("Title", confirmCard, "Effacer la progression ?",
                44f, TextAlignmentOptions.Center, Color.white);
            SkyCoinFactory.PlaceCentered(confirmTitle.rectTransform, 0f, 105f, 760f, 60f);

            TMP_Text confirmBody = SkyCoinFactory.CreateText("Body", confirmCard,
                "Une nouvelle partie efface le niveau débloqué et la progression sauvegardée.",
                24f, TextAlignmentOptions.Center, new Color(0.8f, 0.84f, 0.92f));
            SkyCoinFactory.PlaceCentered(confirmBody.rectTransform, 0f, 40f, 740f, 50f);

            Button confirmYesButton = SkyCoinFactory.CreateButton("Button_ConfirmYes", confirmCard, "Oui, effacer",
                -160f, -70f, 330f, 66f, SkyCoinFactory.ButtonDanger, 28f);

            Button confirmNoButton = SkyCoinFactory.CreateButton("Button_ConfirmNo", confirmCard, "Annuler",
                160f, -70f, 330f, 66f, SkyCoinFactory.ButtonNeutral, 28f);

            confirmCard.gameObject.SetActive(false);

            OptionsPanelController optionsPanel = BuildOptionsPanel(canvas.transform);

            MainMenuController controller = canvas.gameObject.AddComponent<MainMenuController>();
            SkyCoinFactory.SetReference(controller, "newGameButton", newGameButton);
            SkyCoinFactory.SetReference(controller, "continueButton", continueButton);
            SkyCoinFactory.SetReference(controller, "levelSelectButton", levelSelectButton);
            SkyCoinFactory.SetReference(controller, "optionsButton", optionsButton);
            SkyCoinFactory.SetReference(controller, "quitButton", quitButton);
            SkyCoinFactory.SetReference(controller, "continueLabel", continueLabel);
            SkyCoinFactory.SetReference(controller, "levelSelectPanel", levelSelectCard.gameObject);
            SkyCoinFactory.SetReference(controller, "confirmPanel", confirmCard.gameObject);
            SkyCoinFactory.SetReference(controller, "optionsPanel", optionsPanel);
            SkyCoinFactory.SetReference(controller, "level1Button", level1Button);
            SkyCoinFactory.SetReference(controller, "level2Button", level2Button);
            SkyCoinFactory.SetReference(controller, "levelSelectCloseButton", levelSelectCloseButton);
            SkyCoinFactory.SetReference(controller, "confirmYesButton", confirmYesButton);
            SkyCoinFactory.SetReference(controller, "confirmNoButton", confirmNoButton);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
            Debug.Log($"[SkyCoin] Built {MainMenuScenePath}");
        }

        /// <summary>A little floating island so the menu is not an empty screen.</summary>
        private static void BuildMenuScenery()
        {
            GameObject scenery = SkyCoinFactory.CreateEmpty("MenuScenery", null, Vector3.zero);

            Material grass = SkyCoinFactory.LoadMaterial("M_Ground_Grass");
            Material rock = SkyCoinFactory.LoadMaterial("M_Ground_Rock");
            Material coinMaterial = SkyCoinFactory.LoadMaterial("M_Coin");

            SkyCoinFactory.CreatePrimitive(PrimitiveType.Cube, "MenuGround", scenery.transform,
                new Vector3(0f, -3f, 8f), new Vector3(30f, 1f, 18f), grass, _groundLayer);

            SkyCoinFactory.CreatePrimitive(PrimitiveType.Sphere, "MenuRock", scenery.transform,
                new Vector3(-8f, -1.6f, 10f), new Vector3(3.4f, 2.6f, 3.4f), rock, _groundLayer);

            SkyCoinFactory.CreatePrimitive(PrimitiveType.Sphere, "MenuRock2", scenery.transform,
                new Vector3(9f, -1.9f, 11f), new Vector3(2.6f, 2.2f, 2.6f), rock, _groundLayer);

            // Decorative coins: no scripts, they are pure scenery.
            for (int i = 0; i < 6; i++)
            {
                float x = -7.5f + (i * 3f);
                float y = 0.4f + (Mathf.Sin(i * 1.3f) * 0.35f);

                SkyCoinFactory.CreatePrimitive(PrimitiveType.Cylinder, $"MenuCoin_{i + 1:00}", scenery.transform,
                    new Vector3(x, y, 5.5f), new Vector3(0.75f, 0.05f, 0.75f), coinMaterial, _groundLayer,
                    new Vector3(90f, i * 24f, 0f), keepCollider: false);
            }
        }

        /// <summary>Builds Level 1 or Level 2 and returns how many coins were really placed.</summary>
        private static int BuildLevelScene(int levelNumber, LevelDefinition definition, GameObject coinPrefab,
            GameObject playerPrefab, GameObject enemyPrefab, GameObject floorHolePrefab, GameObject movingPlatformPrefab)
        {
            bool isLevel2 = levelNumber == 2;
            string scenePath = isLevel2 ? Level02ScenePath : Level01ScenePath;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SkyCoinFactory.CreateDirectionalLight(null);
            SkyCoinFactory.CreateGlobalVolume(null);

            GameObject environment = SkyCoinFactory.CreateEmpty("Environment", null, Vector3.zero);
            GameObject islands = SkyCoinFactory.CreateEmpty("Islands", environment.transform, Vector3.zero);
            GameObject decorations = SkyCoinFactory.CreateEmpty("Decorations", environment.transform, Vector3.zero);
            GameObject hazards = SkyCoinFactory.CreateEmpty("Hazards", environment.transform, Vector3.zero);
            GameObject coinsRoot = SkyCoinFactory.CreateEmpty("Coins", null, Vector3.zero);

            Material grass = SkyCoinFactory.LoadMaterial("M_Ground_Grass");
            Material rock = SkyCoinFactory.LoadMaterial("M_Ground_Rock");
            Material wood = SkyCoinFactory.LoadMaterial("M_Bridge_Wood");

            if (isLevel2)
            {
                BuildLevel02Geometry(islands.transform, decorations.transform, hazards.transform, grass, rock, wood,
                    floorHolePrefab, movingPlatformPrefab);
            }
            else
            {
                BuildLevel01Geometry(islands.transform, decorations.transform, grass, rock, wood);
            }

            // ------------------------------------------------------------------
            //  Gameplay objects
            // ------------------------------------------------------------------
            GameObject gameplay = SkyCoinFactory.CreateEmpty("Gameplay", null, Vector3.zero);

            GameObject spawnObject = SkyCoinFactory.CreateEmpty("PlayerSpawn", gameplay.transform, PlayerSpawnPosition);
            PlayerSpawnPoint spawnPoint = spawnObject.AddComponent<PlayerSpawnPoint>();

            GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.SetParent(gameplay.transform, true);
            player.transform.position = PlayerSpawnPosition;
            player.transform.rotation = Quaternion.identity;

            Camera camera = SkyCoinFactory.CreateCamera("Main Camera", null, new Vector3(0f, 3f, -11f));
            ThirdPersonCamera thirdPerson = camera.gameObject.AddComponent<ThirdPersonCamera>();
            SkyCoinFactory.SetReference(thirdPerson, "target", player.transform);
            SkyCoinFactory.SetLayerMask(thirdPerson, "collisionMask",
                SkyCoinFactory.EverythingExcept("Player", "Enemy", "Coin", "Hazard"));
            SkyCoinFactory.SetFloat(thirdPerson, "lookSensitivity", 0.14f);

            SkyCoinFactory.SetReference(player.GetComponent<PlayerController>(), "cameraTransform", camera.transform);

            GameObject levelObject = SkyCoinFactory.CreateEmpty("Level", gameplay.transform, Vector3.zero);
            LevelController level = levelObject.AddComponent<LevelController>();
            SkyCoinFactory.SetReference(level, "player", player.GetComponent<PlayerController>());
            SkyCoinFactory.SetReference(level, "playerSpawn", spawnPoint);

            // The LevelDefinition is the ONE object reference the generic SerializedObject helper
            // cannot be trusted with: a ScriptableObject assigned that way reliably ends up as
            // {fileID: 0} in the saved scene, which leaves the level with no rules at all - no
            // timer (so it shows 00:00) and, because TryCollectCoin bails out on a null
            // definition, no coin pickups either. Write the field directly and verify it.
            AssignLevelDefinition(level, isLevel2 ? Level02DefinitionPath : Level01DefinitionPath, definition);

            if (enemyPrefab != null)
            {
                GameObject enemy = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab);
                enemy.transform.SetParent(gameplay.transform, true);
                enemy.transform.position = new Vector3(5f, 0.1f, 33f);
                enemy.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

                // Bakes the navigation mesh at level start. Only the Ground layer is
                // collected, so coins, hazards and the player never become navmesh.
                GameObject navMeshObject = SkyCoinFactory.CreateEmpty("NavMesh", null, Vector3.zero);
                NavMeshSurface surface = navMeshObject.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.All;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.layerMask = 1 << _groundLayer;
                navMeshObject.AddComponent<RuntimeNavMeshBaker>();
            }

            // ------------------------------------------------------------------
            //  Coins : a CoinSpawner owns them all and moves them to new, validated
            //  positions in waves, so the map always has coins on it.
            // ------------------------------------------------------------------
            int activeCoins = AddCoinSpawner(coinsRoot.transform, coinPrefab, isLevel2);

            // ------------------------------------------------------------------
            //  UI
            // ------------------------------------------------------------------
            BuildLevelUi(definition);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[SkyCoin] Built {scenePath}: {activeCoins} coins in play at once (they relocate every " +
                      $"{CoinRelocateInterval:0.#}s), {definition.requiredCoins} to collect.");

            return activeCoins;
        }

        /// <summary>
        /// Assigns <c>LevelController.definition</c> with a direct field write, then reads it
        /// back and complains loudly if it did not stick.
        ///
        /// Why this is not a plain SkyCoinFactory.SetReference call: assigning a
        /// ScriptableObject through a freshly created SerializedObject has been observed to
        /// serialize as {fileID: 0} in the saved scene. A LevelController with a null
        /// definition has no timer and cannot count coins, which is exactly the failure this
        /// avoids.
        /// </summary>
        private static void AssignLevelDefinition(LevelController level, string assetPath, LevelDefinition fallback)
        {
            // Prefer the on-disk asset: that is the object the scene is able to serialize.
            LevelDefinition toAssign = AssetDatabase.LoadAssetAtPath<LevelDefinition>(assetPath);
            if (toAssign == null) toAssign = fallback;

            if (toAssign == null)
            {
                Debug.LogError($"[SkyCoin] No LevelDefinition found at '{assetPath}'. The level will have no rules.");
                return;
            }

            FieldInfo field = typeof(LevelController).GetField("definition",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (field == null)
            {
                Debug.LogError("[SkyCoin] LevelController.definition was not found. The builder and the script have drifted apart.");
                return;
            }

            field.SetValue(level, toAssign);
            EditorUtility.SetDirty(level);

            if (field.GetValue(level) == null)
            {
                Debug.LogError($"[SkyCoin] The LevelDefinition reference on '{level.name}' did not stick. " +
                               "Assign it by hand in the Inspector before playing.");
            }
        }

        // ------------------------------------------------------------------
        //  Level 1 geometry
        // ------------------------------------------------------------------

        private static void BuildLevel01Geometry(Transform islands, Transform decorations,
            Material grass, Material rock, Material wood)
        {
            // Every platform's walking surface is at y = 0, so the death height of -8 is
            // unambiguous: falling off always leads to the same place.
            CreatePlatform(islands, "Island_Start", 0f, 0f, 0f, 18f, 18f, 1f, grass);
            CreatePlatform(islands, "Bridge_01", 0f, 0f, 14f, 4f, 12f, 0.7f, wood);
            CreatePlatform(islands, "Island_Mid", 0f, 0f, 27f, 14f, 14f, 1f, grass);

            // Two deliberate jumps, to teach that a jump is possible before Level 2 needs it.
            CreatePlatform(islands, "Step_01", 6f, 1.0f, 30f, 3f, 3f, 1f, rock);
            CreatePlatform(islands, "Platform_High", 9f, 1.5f, 34f, 6f, 6f, 1f, rock);

            // Decoration. Rocks keep their colliders, so they also block the camera nicely.
            CreateBlock(decorations, "Rock_01", -6.5f, 0.3f, 5f, 2f, 2f, 2f, rock);
            CreateBlock(decorations, "Rock_02", 6f, 0.2f, 23f, 2.5f, 1.6f, 2f, rock);
            CreateBlock(decorations, "Rock_03", -5f, 0.15f, 26f, 3f, 0.9f, 2.4f, rock);

            CreateBlock(decorations, "Landmark_Pillar", -5.5f, 2f, 30f, 1.5f, 4f, 1.5f, rock);
            SkyCoinFactory.CreatePrimitive(PrimitiveType.Sphere, "Landmark_Orb", decorations,
                new Vector3(-5.5f, 4.4f, 30f), new Vector3(1.2f, 1.2f, 1.2f),
                SkyCoinFactory.LoadMaterial("M_Robot_Accent"), _groundLayer);
        }

        // ------------------------------------------------------------------
        //  Level 2 geometry
        // ------------------------------------------------------------------

        private static void BuildLevel02Geometry(Transform islands, Transform decorations, Transform hazards,
            Material grass, Material rock, Material wood, GameObject floorHolePrefab, GameObject movingPlatformPrefab)
        {
            CreatePlatform(islands, "Island_Start", 0f, 0f, 0f, 18f, 18f, 1f, grass);
            CreatePlatform(islands, "Bridge_01", 0f, 0f, 14f, 4f, 12f, 0.7f, wood);
            CreatePlatform(islands, "Island_Mid", 0f, 0f, 27f, 16f, 16f, 1f, grass);
            CreatePlatform(islands, "Bridge_02", 10f, 0f, 27f, 12f, 4f, 0.7f, wood);
            CreatePlatform(islands, "Island_East", 24f, 0f, 27f, 16f, 16f, 1f, grass);

            // --- the hazard route ------------------------------------------------
            // Island_East ends at x = 32. The ledge is walkable and permanent, so the ENEMY
            // can reach it too. The plates beyond it are separated by a 2.5 m jump, which
            // the enemy cannot cross, so the disappearing floors are never on its route.
            CreatePlatform(islands, "Hazard_Ledge", 33.5f, 0f, 27f, 3f, 6f, 1f, rock);

            // Three plates whose surfaces are at y = 0. Their colliders are on the Ground
            // layer, so while they are closed the player stands on them like real floor.
            CreateHazardPlate(hazards, floorHolePrefab, "Floor_Hole_01", 39f, 27f, 0f);
            CreateHazardPlate(hazards, floorHolePrefab, "Floor_Hole_02", 42f, 27f, 1.3f);
            CreateHazardPlate(hazards, floorHolePrefab, "Floor_Hole_03", 45f, 27f, 2.6f);

            CreatePlatform(islands, "Island_Goal", 50.5f, 0f, 27f, 8f, 8f, 1f, grass);

            // --- the moving platform route ---------------------------------------
            // The platform's start position is a 1.5 m jump from Island_Start, so the patch of
            // navigation mesh baked under it is disconnected from everything else. The enemy
            // can therefore never walk onto the platform, and Island_Bonus is a dead end for it.
            CreateMovingPlatform(hazards, movingPlatformPrefab, "MovingPlatform_01", 12f, -0.2f, 4f);
            CreatePlatform(islands, "Island_Bonus", 22f, 0f, 4f, 8f, 6f, 1f, grass);

            // --- decoration ------------------------------------------------------
            CreateBlock(decorations, "Rock_01", -6.5f, 0.3f, 5f, 2f, 2f, 2f, rock);
            CreateBlock(decorations, "Rock_02", 6f, 0.2f, 23f, 2.5f, 1.6f, 2f, rock);
            CreateBlock(decorations, "Rock_03", -5f, 0.15f, 26f, 3f, 0.9f, 2.4f, rock);
            CreateBlock(decorations, "Rock_04", 21.5f, 0.3f, 29.5f, 1.8f, 1.8f, 1.8f, rock);
            CreateBlock(decorations, "Rock_05", 27f, 0.2f, 24f, 2.2f, 1.4f, 2.2f, rock);
            CreateBlock(decorations, "Rock_06", 22f, 0.2f, 4f, 1.4f, 1.2f, 1.4f, rock);

            CreateBlock(decorations, "Landmark_Pillar", -5.5f, 2f, 31f, 1.5f, 4f, 1.5f, rock);
            SkyCoinFactory.CreatePrimitive(PrimitiveType.Sphere, "Landmark_Orb", decorations,
                new Vector3(-5.5f, 4.4f, 31f), new Vector3(1.2f, 1.2f, 1.2f),
                SkyCoinFactory.LoadMaterial("M_Robot_Accent"), _groundLayer);

            CreateBlock(decorations, "East_Pillar", 30f, 1.75f, 33f, 1.5f, 3.5f, 1.5f, rock);
        }

        // ------------------------------------------------------------------
        //  Scene object helpers
        // ------------------------------------------------------------------

        /// <summary>A slab whose WALKING SURFACE sits at topY. That is the number that matters.</summary>
        private static GameObject CreatePlatform(Transform parent, string name, float centerX, float topY,
            float centerZ, float width, float depth, float thickness, Material material)
        {
            Vector3 center = new Vector3(centerX, topY - (thickness * 0.5f), centerZ);
            Vector3 size = new Vector3(width, thickness, depth);

            return SkyCoinFactory.CreatePrimitive(PrimitiveType.Cube, name, parent, center, size, material, _groundLayer);
        }

        /// <summary>Decoration box, positioned by its centre.</summary>
        private static GameObject CreateBlock(Transform parent, string name, float x, float y, float z,
            float sizeX, float sizeY, float sizeZ, Material material)
        {
            return SkyCoinFactory.CreatePrimitive(PrimitiveType.Cube, name, parent,
                new Vector3(x, y, z), new Vector3(sizeX, sizeY, sizeZ), material, _groundLayer);
        }

        private static void CreateHazardPlate(Transform parent, GameObject prefab, string name,
            float x, float z, float startDelaySeconds)
        {
            GameObject plate = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            plate.name = name;
            plate.transform.SetParent(parent, true);

            // The plate is a 1x1x1 cube in the prefab. Scaling it to 3 x 0.25 x 3 with its
            // centre at y = -0.125 puts its walking surface exactly at y = 0.
            plate.transform.position = new Vector3(x, -0.125f, z);
            plate.transform.localScale = new Vector3(3f, 0.25f, 3f);

            SkyCoinFactory.SetFloat(plate.GetComponent<DisappearingFloor>(), "startDelay", startDelaySeconds);
        }

        private static void CreateMovingPlatform(Transform parent, GameObject prefab, string name, float x, float y, float z)
        {
            GameObject platform = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            platform.name = name;
            platform.transform.SetParent(parent, true);
            platform.transform.position = new Vector3(x, y, z);
            platform.transform.localScale = new Vector3(3f, 0.4f, 3f);
        }

        /// <summary>
        /// Creates the level's coin system. The CoinSpawner owns every coin: at level start it
        /// drops the whole pool onto real ground, then moves ALL of them to new, validated
        /// positions every few seconds. A collected coin simply comes back on the next wave,
        /// so the objective (30 / 50 COLLECTED coins) can always be reached.
        ///
        /// The spawn box is deliberately generous and may cover empty space or the trap route:
        /// the spawner's ray cast rejects anything that is not solid, walkable floor, and it
        /// ignores whatever sits under 'Decorations' or 'Hazards'.
        /// </summary>
        private static int AddCoinSpawner(Transform parent, GameObject coinPrefab, bool isLevel2)
        {
            GameObject spawnerObject = SkyCoinFactory.CreateEmpty("CoinSpawner", parent, Vector3.zero);
            CoinSpawner spawner = spawnerObject.AddComponent<CoinSpawner>();

            // A small pool keeps the scene readable and the HUD honest: what you see is what
            // is currently available to pick up.
            int activeCoins = isLevel2 ? 16 : 12;

            // Level 1 spans x -9..12, z -9..37 (start island, bridge, mid island, high platform).
            // Level 2 spans x -9..55, z -9..35 (both islands, the bonus island, the goal island).
            Vector2 center = isLevel2 ? new Vector2(23f, 13f) : new Vector2(1.5f, 14f);
            Vector2 size = isLevel2 ? new Vector2(64f, 45f) : new Vector2(22f, 47f);

            SkyCoinFactory.SetReference(spawner, "coinPrefab", coinPrefab);
            SkyCoinFactory.SetInt(spawner, "activeCoins", activeCoins);
            SkyCoinFactory.SetFloat(spawner, "relocateInterval", CoinRelocateInterval);
            SkyCoinFactory.SetVector2(spawner, "areaCenter", center);
            SkyCoinFactory.SetVector2(spawner, "areaSize", size);
            SkyCoinFactory.SetFloat(spawner, "heightAboveGround", CoinHeightAboveFloor);
            SkyCoinFactory.SetLayerMask(spawner, "groundMask", 1 << _groundLayer);

            return activeCoins;
        }

        // ------------------------------------------------------------------
        //  Level UI
        // ------------------------------------------------------------------

        private static void BuildLevelUi(LevelDefinition definition)
        {
            Canvas canvas = SkyCoinFactory.CreateCanvas("LevelCanvas", null, 0);
            SkyCoinFactory.CreateEventSystem(null);

            // ---------------- HUD ----------------
            RectTransform hudRoot = SkyCoinFactory.CreateUiGroup("HUD", canvas.transform);

            TMP_Text levelText = SkyCoinFactory.CreateText("HUD_Level", hudRoot, definition.displayName,
                34f, TextAlignmentOptions.TopLeft, new Color(0.85f, 0.9f, 1f));
            SkyCoinFactory.PlaceTopLeft(levelText, 40f, 26f, 800f, 46f);

            TMP_Text coinText = SkyCoinFactory.CreateText("HUD_Coins", hudRoot,
                $"Pièces : 0 / {definition.requiredCoins}", 46f, TextAlignmentOptions.TopLeft, new Color(1f, 0.86f, 0.35f));
            SkyCoinFactory.PlaceTopLeft(coinText, 40f, 74f, 800f, 60f);

            TMP_Text objectiveText = SkyCoinFactory.CreateText("HUD_Objective", hudRoot, definition.ObjectiveText,
                24f, TextAlignmentOptions.TopLeft, new Color(0.78f, 0.85f, 0.95f));
            SkyCoinFactory.PlaceTopLeft(objectiveText, 40f, 142f, 1000f, 40f);

            RectTransform timerGroup = SkyCoinFactory.CreateUiGroup("HUD_TimerGroup", hudRoot);
            TMP_Text timerText = SkyCoinFactory.CreateText("HUD_Timer", timerGroup, "Temps : 03:00",
                48f, TextAlignmentOptions.TopRight, Color.white);
            SkyCoinFactory.PlaceTopRight(timerText, 40f, 30f, 500f, 60f);

            string tutorial = definition.useCountdownTimer
                ? "ZQSD / WASD / Flèches : se déplacer        Souris : regarder        Molette : zoom\n" +
                  "Espace : sauter        Maj : courir        Échap : pause\n\n" +
                  "50 pièces en 3 minutes. Un ennemi te poursuit.\n" +
                  "Les plaques orange s'effondrent : ne t'attarde pas dessus."
                : "ZQSD / WASD / Flèches : se déplacer        Souris : regarder        Molette : zoom\n" +
                  "Espace : sauter        Maj : courir        Échap : pause\n\n" +
                  "Ramasse les 30 pièces. Le vide est mortel : tomber termine la partie.";

            RectTransform tutorialCard = SkyCoinFactory.CreateCenterCard("HUD_Tutorial", hudRoot,
                new Vector2(1180f, 250f), new Color(0.05f, 0.07f, 0.11f, 0.82f), false);
            tutorialCard.anchoredPosition = new Vector2(0f, -300f);

            TMP_Text tutorialText = SkyCoinFactory.CreateText("Text", tutorialCard, tutorial,
                26f, TextAlignmentOptions.Center, new Color(0.9f, 0.94f, 1f));
            SkyCoinFactory.Stretch(tutorialText.rectTransform);
            tutorialText.rectTransform.offsetMin = new Vector2(24f, 24f);
            tutorialText.rectTransform.offsetMax = new Vector2(-24f, -24f);

            HudController hud = canvas.gameObject.AddComponent<HudController>();
            SkyCoinFactory.SetReference(hud, "level", null);          // resolved from LevelController.Instance
            SkyCoinFactory.SetReference(hud, "hudRoot", hudRoot.gameObject);
            SkyCoinFactory.SetReference(hud, "levelText", levelText);
            SkyCoinFactory.SetReference(hud, "coinText", coinText);
            SkyCoinFactory.SetReference(hud, "objectiveText", objectiveText);
            SkyCoinFactory.SetReference(hud, "timerText", timerText);
            SkyCoinFactory.SetReference(hud, "timerGroup", timerGroup.gameObject);
            SkyCoinFactory.SetReference(hud, "tutorialPanel", tutorialCard.gameObject);

            // ---------------- Options (built first so the other panels can reference it) -----
            OptionsPanelController optionsPanel = BuildOptionsPanel(canvas.transform);

            // ---------------- Pause ----------------
            RectTransform pauseGroup = SkyCoinFactory.CreateUiGroup("PauseGroup", canvas.transform);
            SkyCoinFactory.CreatePanel("Backdrop", pauseGroup, new Color(0f, 0f, 0f, 0.6f), true);

            RectTransform pauseCard = SkyCoinFactory.CreateCenterCard("Card", pauseGroup,
                new Vector2(720f, 620f), SkyCoinFactory.PanelSolid);

            TMP_Text pauseTitle = SkyCoinFactory.CreateText("Title", pauseCard, "Pause", 54f,
                TextAlignmentOptions.Center, Color.white);
            SkyCoinFactory.PlaceCentered(pauseTitle.rectTransform, 0f, 235f, 640f, 70f);

            Button resumeButton = SkyCoinFactory.CreateButton("Button_Resume", pauseCard, "Reprendre",
                0f, 115f, 460f, 72f, SkyCoinFactory.ButtonPrimary, 30f);

            Button restartButton = SkyCoinFactory.CreateButton("Button_Restart", pauseCard, "Recommencer",
                0f, 33f, 460f, 72f, SkyCoinFactory.ButtonNormal, 30f);

            Button pauseOptionsButton = SkyCoinFactory.CreateButton("Button_Options", pauseCard, "Options",
                0f, -49f, 460f, 72f, SkyCoinFactory.ButtonNeutral, 30f);

            Button pauseMainMenuButton = SkyCoinFactory.CreateButton("Button_MainMenu", pauseCard, "Menu principal",
                0f, -131f, 460f, 72f, SkyCoinFactory.ButtonNeutral, 30f);

            TMP_Text pauseHint = SkyCoinFactory.CreateText("Hint", pauseCard, "Échap pour reprendre", 22f,
                TextAlignmentOptions.Center, new Color(0.7f, 0.76f, 0.86f));
            SkyCoinFactory.PlaceCentered(pauseHint.rectTransform, 0f, -225f, 640f, 40f);

            PauseMenuController pause = canvas.gameObject.AddComponent<PauseMenuController>();
            SkyCoinFactory.SetReference(pause, "panel", pauseGroup.gameObject);
            SkyCoinFactory.SetReference(pause, "resumeButton", resumeButton);
            SkyCoinFactory.SetReference(pause, "restartButton", restartButton);
            SkyCoinFactory.SetReference(pause, "optionsButton", pauseOptionsButton);
            SkyCoinFactory.SetReference(pause, "mainMenuButton", pauseMainMenuButton);
            SkyCoinFactory.SetReference(pause, "optionsPanel", optionsPanel);

            // ---------------- Result ----------------
            RectTransform resultGroup = SkyCoinFactory.CreateUiGroup("ResultGroup", canvas.transform);
            SkyCoinFactory.CreatePanel("Backdrop", resultGroup, new Color(0f, 0f, 0f, 0.65f), true);

            RectTransform resultCard = SkyCoinFactory.CreateCenterCard("Card", resultGroup,
                new Vector2(900f, 640f), SkyCoinFactory.PanelSolid);

            TMP_Text resultTitle = SkyCoinFactory.CreateText("Title", resultCard, "Victoire !", 62f,
                TextAlignmentOptions.Center, new Color(1f, 0.86f, 0.35f));
            SkyCoinFactory.PlaceCentered(resultTitle.rectTransform, 0f, 240f, 820f, 80f);

            TMP_Text resultReason = SkyCoinFactory.CreateText("Reason", resultCard, "", 30f,
                TextAlignmentOptions.Center, new Color(1f, 0.55f, 0.5f));
            SkyCoinFactory.PlaceCentered(resultReason.rectTransform, 0f, 160f, 820f, 50f);

            TMP_Text resultStats = SkyCoinFactory.CreateText("Stats", resultCard, "", 26f,
                TextAlignmentOptions.Center, new Color(0.85f, 0.9f, 1f));
            SkyCoinFactory.PlaceCentered(resultStats.rectTransform, 0f, 100f, 820f, 46f);

            Button nextLevelButton = SkyCoinFactory.CreateButton("Button_NextLevel", resultCard, "Niveau suivant",
                0f, 5f, 480f, 74f, SkyCoinFactory.ButtonPrimary, 30f);

            Button replayButton = SkyCoinFactory.CreateButton("Button_Replay", resultCard, "Réessayer",
                0f, -80f, 480f, 74f, SkyCoinFactory.ButtonNormal, 30f);

            Button resultMainMenuButton = SkyCoinFactory.CreateButton("Button_MainMenu", resultCard, "Menu principal",
                0f, -165f, 480f, 74f, SkyCoinFactory.ButtonNeutral, 30f);

            ResultPanelController result = canvas.gameObject.AddComponent<ResultPanelController>();
            SkyCoinFactory.SetReference(result, "panel", resultGroup.gameObject);
            SkyCoinFactory.SetReference(result, "titleText", resultTitle);
            SkyCoinFactory.SetReference(result, "reasonText", resultReason);
            SkyCoinFactory.SetReference(result, "statsText", resultStats);
            SkyCoinFactory.SetReference(result, "nextLevelButton", nextLevelButton);
            SkyCoinFactory.SetReference(result, "replayButton", replayButton);
            SkyCoinFactory.SetReference(result, "mainMenuButton", resultMainMenuButton);

            // Draw order: the Options panel is opened ON TOP of the pause menu, and uGUI draws
            // children in hierarchy order (last sibling = front). It was built first purely so
            // the pause menu could reference it, so move it to the end of the canvas here.
            optionsPanel.transform.SetAsLastSibling();
        }

        /// <summary>Options panel, shared by the main menu and the in-game pause menu.</summary>
        private static OptionsPanelController BuildOptionsPanel(Transform canvasTransform)
        {
            RectTransform optionsGroup = SkyCoinFactory.CreateUiGroup("OptionsGroup", canvasTransform);
            SkyCoinFactory.CreatePanel("Backdrop", optionsGroup, new Color(0f, 0f, 0f, 0.78f), true);

            RectTransform card = SkyCoinFactory.CreateCenterCard("Card", optionsGroup,
                new Vector2(880f, 680f), SkyCoinFactory.PanelSolid);

            TMP_Text title = SkyCoinFactory.CreateText("Title", card, "Options", 48f,
                TextAlignmentOptions.Center, Color.white);
            SkyCoinFactory.PlaceCentered(title.rectTransform, 0f, 270f, 800f, 64f);

            Slider masterSlider = CreateOptionsRow(card, "Master", "Volume général", 170f, out TMP_Text masterValue);
            Slider musicSlider = CreateOptionsRow(card, "Music", "Volume musique", 90f, out TMP_Text musicValue);
            Slider sfxSlider = CreateOptionsRow(card, "Sfx", "Volume effets", 10f, out TMP_Text sfxValue);
            Slider sensitivitySlider = CreateOptionsRow(card, "Sensitivity", "Sensibilité souris", -70f, out TMP_Text sensitivityValue);

            Button closeButton = SkyCoinFactory.CreateButton("Button_Close", card, "Fermer",
                0f, -240f, 360f, 70f, SkyCoinFactory.ButtonNormal, 30f);

            OptionsPanelController controller = optionsGroup.gameObject.AddComponent<OptionsPanelController>();
            SkyCoinFactory.SetReference(controller, "panel", optionsGroup.gameObject);
            SkyCoinFactory.SetReference(controller, "masterSlider", masterSlider);
            SkyCoinFactory.SetReference(controller, "musicSlider", musicSlider);
            SkyCoinFactory.SetReference(controller, "sfxSlider", sfxSlider);
            SkyCoinFactory.SetReference(controller, "sensitivitySlider", sensitivitySlider);
            SkyCoinFactory.SetReference(controller, "masterValueText", masterValue);
            SkyCoinFactory.SetReference(controller, "musicValueText", musicValue);
            SkyCoinFactory.SetReference(controller, "sfxValueText", sfxValue);
            SkyCoinFactory.SetReference(controller, "sensitivityValueText", sensitivityValue);
            SkyCoinFactory.SetReference(controller, "closeButton", closeButton);

            optionsGroup.gameObject.SetActive(false);
            return controller;
        }

        private static Slider CreateOptionsRow(RectTransform card, string id, string label, float y, out TMP_Text valueText)
        {
            TMP_Text labelText = SkyCoinFactory.CreateText($"Label_{id}", card, label, 26f,
                TextAlignmentOptions.MidlineLeft, Color.white);
            SkyCoinFactory.PlaceCentered(labelText.rectTransform, -230f, y, 330f, 44f);

            Slider slider = SkyCoinFactory.CreateSlider($"Slider_{id}", card, 70f, y, 340f);

            valueText = SkyCoinFactory.CreateText($"Value_{id}", card, "100 %", 26f,
                TextAlignmentOptions.MidlineRight, new Color(0.82f, 0.88f, 1f));
            SkyCoinFactory.PlaceCentered(valueText.rectTransform, 305f, y, 130f, 44f);

            return slider;
        }

        // ------------------------------------------------------------------
        //  Build settings and validation
        // ------------------------------------------------------------------

        private static void RegisterScenesInBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>
            {
                // MainMenu is index 0: it is the scene a build starts with.
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(Level01ScenePath, true),
                new EditorBuildSettingsScene(Level02ScenePath, true)
            };

            // Keep whatever else was already there, so nothing is silently removed.
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (scenes.Exists(scene => scene.path == existing.path)) continue;
                scenes.Add(existing);
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[SkyCoin] Build Profiles scene list updated: {scenes.Count} scene(s), MainMenu first.");
        }

        private static int ValidateLevelScene(string scenePath, int expectedLevelNumber)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            int problems = 0;

            LevelController[] levels = Object.FindObjectsByType<LevelController>(FindObjectsInactive.Exclude);
            Coin[] coins = Object.FindObjectsByType<Coin>(FindObjectsInactive.Exclude);
            PlayerSpawnPoint[] spawns = Object.FindObjectsByType<PlayerSpawnPoint>(FindObjectsInactive.Exclude);
            PlayerController[] players = Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude);
            ThirdPersonCamera[] cameras = Object.FindObjectsByType<ThirdPersonCamera>(FindObjectsInactive.Exclude);
            HudController[] huds = Object.FindObjectsByType<HudController>(FindObjectsInactive.Exclude);
            CoinSpawner[] spawners = Object.FindObjectsByType<CoinSpawner>(FindObjectsInactive.Exclude);

            // A LevelDefinition asset is not a scene object, so read it from the controller.
            int requiredCoins = 0;
            if (levels.Length == 1 && levels[0].Definition != null)
            {
                requiredCoins = levels[0].Definition.requiredCoins;

                if (levels[0].Definition.levelNumber != expectedLevelNumber)
                {
                    Debug.LogWarning($"[SkyCoin] {scenePath}: the LevelDefinition is for level " +
                                     $"{levels[0].Definition.levelNumber}, expected {expectedLevelNumber}.");
                    problems++;
                }
            }

            problems += Check(scenePath, levels.Length == 1, $"{levels.Length} LevelController(s) found, expected exactly 1");
            problems += Check(scenePath, players.Length == 1, $"{players.Length} PlayerController(s) found, expected exactly 1");
            problems += Check(scenePath, spawns.Length == 1, $"{spawns.Length} PlayerSpawnPoint(s) found, expected exactly 1");
            problems += Check(scenePath, cameras.Length == 1, $"{cameras.Length} ThirdPersonCamera(s) found, expected exactly 1");
            problems += Check(scenePath, huds.Length == 1, $"{huds.Length} HudController(s) found, expected exactly 1");
            problems += Check(scenePath, spawners.Length == 1,
                $"{spawners.Length} CoinSpawner(s) found, expected exactly 1");
            problems += Check(scenePath, coins.Length >= requiredCoins || spawners.Length == 1,
                $"{coins.Length} coins placed but {requiredCoins} are required, and no CoinSpawner refills them");

            // Every coin must still be above real floor.
            int floating = 0;
            Physics.SyncTransforms();

            foreach (Coin coin in coins)
            {
                if (coin == null) continue;

                Vector3 position = coin.transform.position;
                if (Physics.Raycast(position + (Vector3.up * 10f), Vector3.down, out RaycastHit hit, 40f,
                        1 << _groundLayer, QueryTriggerInteraction.Ignore))
                {
                    float gap = position.y - hit.point.y;
                    if (gap < 0.2f || gap > 3f) floating++;
                }
            }

            problems += Check(scenePath, floating == 0, $"{floating} coin(s) are not at a sensible height above the floor");

            Debug.Log($"[SkyCoin] {scenePath}: level {expectedLevelNumber}, {coins.Length} coin(s) in the scene, " +
                      $"{spawners.Length} coin spawner(s), {requiredCoins} to collect, {problems} problem(s).");

            return problems;
        }

        private static int Check(string scenePath, bool condition, string failureMessage)
        {
            if (condition) return 0;

            Debug.LogError($"[SkyCoin] VALIDATION FAILED in {scenePath}: {failureMessage}.");
            return 1;
        }
    }
}
