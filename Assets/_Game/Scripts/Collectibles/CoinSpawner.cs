using System.Collections.Generic;
using UnityEngine;

namespace SkyCoin
{
    /// <summary>
    /// Owns every coin in a level.
    ///
    /// THE RULE
    ///   "activeCoins" coins are in play at all times, and every "relocateInterval" seconds
    ///   ALL of them jump to brand new positions. Coins are therefore never a fixed set that
    ///   the player sweeps once - they keep re-appearing somewhere else on the map.
    ///   A coin that was already collected comes back to life on the next wave, so the pool
    ///   always refills itself.
    ///
    /// WHY THE POSITIONS ARE SAFE
    ///   A new position is only accepted when a ray cast from above finds real, walkable
    ///   ground on the Ground layer, that ground is not part of the decorations (rocks,
    ///   pillars) or of the hazards (collapsing plates, moving platform), and the spot is not
    ///   right on top of the player or another coin. A rejected spot is simply re-rolled.
    ///   That is what stops a coin from ending up floating over the void, buried inside
    ///   geometry, or parked on top of a 4 m pillar where nobody can reach it.
    ///
    /// EXECUTION ORDER -200
    ///   The starting coins must exist before LevelController.Awake (order 500) runs its one
    ///   scene-wide coin search, otherwise the level would count zero coins.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [AddComponentMenu("Sky Coin Adventure/Coin Spawner")]
    public class CoinSpawner : MonoBehaviour
    {
        [Header("Prefab")]
        [Tooltip("The Coin prefab. Leave empty and no coin can ever appear.")]
        [SerializeField] private GameObject coinPrefab;

        [Header("Coins in play")]
        [Tooltip("How many coins exist at the same time. The objective is a number of COLLECTED " +
                 "coins, and this pool keeps refreshing, so the level can always be finished.")]
        [Min(1)] [SerializeField] private int activeCoins = 12;

        [Header("Relocation")]
        [Tooltip("Seconds between two relocation waves. Every coin jumps to a new spot each wave.")]
        [Min(0.25f)] [SerializeField] private float relocateInterval = 3f;

        [Header("Where coins may appear (world XZ box)")]
        [Tooltip("Centre of the box, in world X and Z.")]
        [SerializeField] private Vector2 areaCenter = Vector2.zero;
        [Tooltip("Width (X) and depth (Z) of the box. It may cover empty space: the ray cast " +
                 "below simply rejects those spots.")]
        [SerializeField] private Vector2 areaSize = new Vector2(20f, 30f);

        [Header("Placement")]
        [Tooltip("Height of the coin centre above the ground it lands on.")]
        [SerializeField] private float heightAboveGround = 1f;
        [Tooltip("The ground probe starts this high above the map.")]
        [SerializeField] private float probeStartHeight = 40f;
        [SerializeField] private float probeDistance = 120f;
        [Tooltip("Only these layers count as ground.")]
        [SerializeField] private LayerMask groundMask;
        [Tooltip("Ground steeper than this is not used.")]
        [SerializeField] private float maxGroundSlope = 45f;
        [Tooltip("Re-rolls per coin before it simply stays where it is for this wave.")]
        [Min(1)] [SerializeField] private int placementAttempts = 24;

        [Header("Fairness")]
        [Tooltip("Never place a coin closer than this to the player (in the XZ plane).")]
        [SerializeField] private float minDistanceFromPlayer = 3.5f;
        [Tooltip("Never place two coins closer than this to each other.")]
        [SerializeField] private float minDistanceBetweenCoins = 2.5f;
        [Tooltip("Ground objects that live under one of these parents are ignored. The builder " +
                 "puts the rocks and pillars under 'Decorations' and the traps under 'Hazards'.")]
        [SerializeField] private string[] excludedParentNames = { "Decorations", "Hazards" };

        private readonly List<Coin> _coins = new List<Coin>();
        private readonly List<Vector3> _taken = new List<Vector3>();

        private Transform _player;
        private float _nextRelocation;

        // ------------------------------------------------------------------
        //  Lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            PlayerController playerController = FindAnyObjectByType<PlayerController>();
            if (playerController != null) _player = playerController.transform;

            // The level's colliders were created before Play started; make sure the physics
            // scene agrees before we start shooting rays at it.
            Physics.SyncTransforms();

            RelocateAll();
        }

        private void Start()
        {
            _nextRelocation = Time.time + relocateInterval;
        }

        private void Update()
        {
            // Coins must not move while the game is paused, on the result screen or in a menu.
            if (GameManager.Instance != null && !GameManager.Instance.IsGameplayRunning) return;

            if (Time.time < _nextRelocation) return;

            _nextRelocation = Time.time + relocateInterval;
            RelocateAll();
        }

        // ------------------------------------------------------------------
        //  The wave
        // ------------------------------------------------------------------

        /// <summary>
        /// Creates the coins that do not exist yet, then moves EVERY coin to a fresh position.
        /// Collected coins are re-activated, which is what refills the level.
        /// </summary>
        public void RelocateAll()
        {
            // Cleared first: the coins created just below must already avoid each other.
            _taken.Clear();

            EnsurePoolSize();

            for (int i = 0; i < _coins.Count; i++)
            {
                Coin coin = _coins[i];
                if (coin == null) continue;

                if (!TryFindValidPosition(out Vector3 position))
                {
                    // No acceptable spot this attempt round. The coin stays where it is: parked
                    // far below the world if it is brand new, or at its previous spot if not.
                    continue;
                }

                coin.PlaceAt(position);
                _taken.Add(position);
            }
        }

        private void EnsurePoolSize()
        {
            while (_coins.Count < activeCoins)
            {
                Coin coin = CreateCoin();
                if (coin == null) return;   // the prefab is missing; nothing more we can do
                _coins.Add(coin);
            }
        }

        private Coin CreateCoin()
        {
            if (coinPrefab == null)
            {
                Debug.LogError("[CoinSpawner] No coin prefab assigned, so no coin can be created.", this);
                return null;
            }

            GameObject instance = Instantiate(coinPrefab, transform);
            instance.name = "Coin";

            Coin coin = instance.GetComponent<Coin>();
            if (coin == null)
            {
                Debug.LogError("[CoinSpawner] The assigned prefab has no Coin component on its root.", this);
                Destroy(instance);
                return null;
            }

            // Park it far below the world so it can never be seen or collected before
            // RelocateAll() gives it a real, validated spot in the very same pass.
            coin.PlaceAt(Vector3.down * 1000f);
            return coin;
        }

        // ------------------------------------------------------------------
        //  Position search
        // ------------------------------------------------------------------

        private bool TryFindValidPosition(out Vector3 position)
        {
            float halfWidth = areaSize.x * 0.5f;
            float halfDepth = areaSize.y * 0.5f;

            for (int attempt = 0; attempt < placementAttempts; attempt++)
            {
                float x = areaCenter.x + Random.Range(-halfWidth, halfWidth);
                float z = areaCenter.y + Random.Range(-halfDepth, halfDepth);

                // Straight down from high above: if there is no floor here, this spot is void.
                if (!Physics.Raycast(new Vector3(x, probeStartHeight, z), Vector3.down,
                        out RaycastHit hit, probeDistance, groundMask, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                if (Vector3.Angle(hit.normal, Vector3.up) > maxGroundSlope) continue;
                if (IsExcludedGround(hit.collider.transform)) continue;

                Vector3 candidate = hit.point + (Vector3.up * heightAboveGround);

                if (IsTooCloseToPlayer(candidate)) continue;
                if (IsTooCloseToOtherCoin(candidate)) continue;

                position = candidate;
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        private bool IsExcludedGround(Transform hitTransform)
        {
            if (excludedParentNames == null || excludedParentNames.Length == 0) return false;

            Transform current = hitTransform;

            while (current != null)
            {
                for (int i = 0; i < excludedParentNames.Length; i++)
                {
                    string name = excludedParentNames[i];
                    if (!string.IsNullOrEmpty(name) && current.name == name) return true;
                }

                current = current.parent;
            }

            return false;
        }

        private bool IsTooCloseToPlayer(Vector3 candidate)
        {
            if (_player == null) return false;

            Vector2 candidateXz = new Vector2(candidate.x, candidate.z);
            Vector2 playerXz = new Vector2(_player.position.x, _player.position.z);

            return Vector2.Distance(candidateXz, playerXz) < minDistanceFromPlayer;
        }

        private bool IsTooCloseToOtherCoin(Vector3 candidate)
        {
            float sqrMinimum = minDistanceBetweenCoins * minDistanceBetweenCoins;

            for (int i = 0; i < _taken.Count; i++)
            {
                if ((_taken[i] - candidate).sqrMagnitude < sqrMinimum) return true;
            }

            return false;
        }
    }
}
