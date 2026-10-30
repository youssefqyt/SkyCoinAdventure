using UnityEngine;

namespace SkyCoin
{
    /// <summary>
    /// A collectible energy coin.
    ///
    /// STRUCTURE (see the Coin prefab):
    ///   Coin            <- this script, SphereCollider (isTrigger)
    ///     CoinVisual    <- the mesh that spins and bobs
    /// The trigger lives on the stable root, so the bobbing animation can never move the
    /// collection volume away from where the player expects it.
    ///
    /// COLLECTION RULES:
    ///  - collected at most once (guarded by _collected, and by the collider being disabled),
    ///  - the count is owned by LevelController, never by the coin,
    ///  - nothing is collected while the game is not Playing (paused / victory / game over).
    /// </summary>
    public class Coin : MonoBehaviour
    {
        [Header("Visual motion")]
        [Tooltip("Child object that spins and bobs. Auto resolved to the first child if empty.")]
        [SerializeField] private Transform visualRoot;

        [SerializeField] private float spinDegreesPerSecond = 130f;

        [Tooltip("Total bob travel in metres.")]
        [SerializeField] private float bobHeight = 0.22f;

        [SerializeField] private float bobSpeed = 2.2f;

        [Header("Pickup")]
        [Tooltip("Hide the coin the instant it is collected. Particles are added in Milestone 6.")]
        [SerializeField] private bool hideOnCollect = true;

        private Vector3 _visualRestPosition;
        private float _bobPhase;
        private bool _collected;

        /// <summary>True once this coin has been counted by the level.</summary>
        public bool IsCollected => _collected;

        /// <summary>
        /// Moves an existing coin to a new spot and makes it collectable again.
        /// This is what CoinSpawner calls for every coin on each relocation wave: a coin that
        /// was collected earlier simply comes back to life somewhere else. The count already
        /// banked by LevelController is never touched, so a coin can be picked up again
        /// without ever counting twice.
        /// </summary>
        public void PlaceAt(Vector3 worldPosition)
        {
            _collected = false;

            transform.position = worldPosition;
            transform.rotation = Quaternion.identity;

            if (!gameObject.activeSelf) gameObject.SetActive(true);

            // New spot, new bob rhythm, so a wave of coins does not pulse in lockstep.
            _bobPhase = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Awake()
        {
            if (visualRoot == null)
            {
                visualRoot = transform.childCount > 0 ? transform.GetChild(0) : transform;
            }

            _visualRestPosition = visualRoot.localPosition;

            // Desynchronise the bob so a row of coins does not pulse in lockstep.
            _bobPhase = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            if (_collected) return;

            // Cosmetic motion only. Freezing it in pause / victory / game over is intentional.
            if (GameManager.Instance != null && !GameManager.Instance.IsGameplayRunning) return;

            float deltaTime = Time.deltaTime;

            if (visualRoot != transform)
            {
                visualRoot.Rotate(0f, spinDegreesPerSecond * deltaTime, 0f, Space.Self);
            }
            else
            {
                transform.Rotate(0f, spinDegreesPerSecond * deltaTime, 0f, Space.World);
            }

            _bobPhase += deltaTime * bobSpeed;
            float offset = (Mathf.Sin(_bobPhase) * 0.5f) + 0.5f;   // 0 .. 1
            visualRoot.localPosition = _visualRestPosition + (Vector3.up * offset * bobHeight);
        }

        private void OnTriggerEnter(Collider other)
        {
            TryCollect(other);
        }

        private void TryCollect(Collider other)
        {
            if (_collected) return;

            if (GameManager.Instance != null && !GameManager.Instance.IsGameplayRunning) return;

            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;   // something else touched the coin

            LevelController level = LevelController.Instance;
            if (level == null) return;

            // Take the coin locally first so a second trigger event in the same frame is ignored.
            _collected = true;

            if (!level.TryCollectCoin(this))
            {
                // The level refused (the run already ended). Put the coin back.
                _collected = false;
                return;
            }

            PlayerVisuals visuals = player.GetComponent<PlayerVisuals>();
            if (visuals != null) visuals.PlayPickupReaction();

            if (AudioManager.Instance != null) AudioManager.Instance.PlayCoin();

            if (hideOnCollect)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
