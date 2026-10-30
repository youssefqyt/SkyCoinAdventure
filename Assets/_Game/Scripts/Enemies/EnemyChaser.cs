using UnityEngine;
using UnityEngine.AI;

namespace SkyCoin
{
    /// <summary>
    /// The Level 2 chaser. Uses NavMeshAgent, so it navigates around obstacles for free,
    /// and it CANNOT cross a gap, because a NavMesh is baked from walkable surfaces and a
    /// gap simply has no navigation data. That is the whole reason the enemy needs no
    /// custom pathfinding.
    ///
    /// FAIRNESS RULES BUILT IN HERE:
    ///  - it does not move until the level's enemy activation delay has passed
    ///    (no spawn camping while the player reads the screen),
    ///  - it moves slower than the player (set on the LevelDefinition / agent speed),
    ///  - capture is a distance test, not a physics push, so it cannot shove the player
    ///    off a platform by accident,
    ///  - it stops completely during pause, victory and game over.
    ///
    /// IMPORTANT / KNOWN LIMITATION:
    /// disabling a floor collider does NOT change an already baked NavMesh. This enemy
    /// therefore stays on permanent navigation routes, and the temporary floors in Level 2
    /// are placed on a separate player route. See the level design section.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyChaser : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Usually the Player root. Auto filled with the first PlayerController if empty.")]
        [SerializeField] private Transform target;

        [Header("Pursuit")]
        [Tooltip("Overridden by the LevelDefinition activation delay when a LevelController exists.")]
        [SerializeField] private float activationDelaySeconds = 1.5f;

        [Tooltip("How far away the player can be before the enemy gives up and stops.")]
        [SerializeField] private float detectionRadius = 45f;

        [SerializeField] private float movementSpeed = 4.6f;
        [SerializeField] private float acceleration = 22f;
        [SerializeField] private float angularSpeed = 420f;
        [SerializeField] private float repathInterval = 0.15f;

        [Header("Capture")]
        [Tooltip("Horizontal distance at which the enemy catches the player.")]
        [SerializeField] private float captureRadius = 0.9f;

        [Tooltip("How much vertical difference is ignored when measuring distance.")]
        [SerializeField] private float captureHeightTolerance = 1.6f;

        [Header("Diagnostics")]
        [SerializeField] private bool logState = false;

        private NavMeshAgent _agent;
        private float _activationTime;
        private float _nextRepathTime;
        private bool _isActive;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();

            _agent.speed = movementSpeed;
            _agent.acceleration = acceleration;
            _agent.angularSpeed = angularSpeed;
            _agent.stoppingDistance = 0f;
            _agent.autoBraking = false;
            _agent.updateRotation = true;
            _agent.isStopped = true;

            if (_agent.radius < 0.35f) _agent.radius = 0.4f;
            if (_agent.height < 1.4f) _agent.height = 1.6f;
        }

        private void Start()
        {
            if (target == null)
            {
                PlayerController player = FindAnyObjectByType<PlayerController>();
                if (player != null) target = player.transform;
            }

            if (target == null)
            {
                Debug.LogError("[EnemyChaser] No player found to chase. Disabling.", this);
                enabled = false;
                return;
            }

            // The level owns the fairness delay, so it is configured in one place.
            LevelDefinition definition = LevelController.Instance != null ? LevelController.Instance.Definition : null;
            if (definition != null && definition.useEnemy)
            {
                activationDelaySeconds = definition.enemyActivationDelaySeconds;
            }

            _activationTime = Time.time + Mathf.Max(0f, activationDelaySeconds);

            if (!_agent.isOnNavMesh)
            {
                Debug.LogError("[EnemyChaser] The agent is not standing on a baked NavMesh. " +
                               "Add a NavMeshSurface to the scene and bake it. The enemy will not move until then.", this);
            }
        }

        private void Update()
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsGameplayRunning)
            {
                StopMoving();
                return;
            }

            if (target == null) return;

            if (!_isActive)
            {
                StopMoving();

                if (Time.time >= _activationTime)
                {
                    _isActive = true;
                    if (_agent.isOnNavMesh) _agent.isStopped = false;
                    if (logState) Debug.Log("[EnemyChaser] Activated and chasing.");
                }

                return;
            }

            // Safe handling if the floor under the agent disappeared or it was teleported off.
            if (!_agent.isOnNavMesh) return;

            float planarDistance = PlanarDistance(transform.position, target.position);

            if (planarDistance <= captureRadius && Mathf.Abs(transform.position.y - target.position.y) <= captureHeightTolerance)
            {
                // The enemy never decides the outcome itself: it reports, and LevelController
                // evaluates every condition in one fixed order.
                if (LevelController.Instance != null) LevelController.Instance.ReportEnemyCapture();
                return;
            }

            if (planarDistance > detectionRadius)
            {
                StopMoving();
                return;
            }

            if (_agent.isStopped) _agent.isStopped = false;

            if (Time.time >= _nextRepathTime)
            {
                _nextRepathTime = Time.time + Mathf.Max(0.05f, repathInterval);
                _agent.SetDestination(target.position);
            }
        }

        private void StopMoving()
        {
            if (_agent == null) return;
            if (!_agent.isOnNavMesh) return;
            if (_agent.isStopped) return;

            _agent.isStopped = true;
            _agent.ResetPath();
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.25f, 0.25f, 1f);
            Gizmos.DrawWireSphere(transform.position, captureRadius);
        }
    }
}
