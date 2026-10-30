using UnityEngine;

namespace SkyCoin
{
    /// <summary>
    /// A platform that travels between two authored points and carries the player.
    ///
    /// HOW CARRYING WORKS: the player uses a CharacterController, which Unity never moves
    /// for you when the floor underneath moves. So this platform publishes how much it
    /// moved during the frame (<see cref="DeltaPosition"/>), and PlayerController adds that
    /// to its own movement. DeltaPosition is a PER FRAME DELTA, not a speed.
    ///
    /// EXECUTION ORDER MATTERS: this script runs at -150, before PlayerController at -100,
    /// so the player is carried in the same frame the platform moves instead of one frame
    /// late. That is what removes the usual "sliding backwards on moving platforms" jitter.
    ///
    /// The platform uses scaled time, so it freezes during pause, victory and game over.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public class MovingPlatform : PlatformCarrier
    {
        [Header("Travel")]
        [Tooltip("Offset from the start position to the far end, in world units.")]
        [SerializeField] private Vector3 travelOffset = new Vector3(0f, 0f, 8f);

        [Tooltip("Seconds to travel from one end to the other.")]
        [Min(0.2f)]
        [SerializeField] private float travelDuration = 4f;

        [Tooltip("Seconds to wait at each end before going back.")]
        [Min(0f)]
        [SerializeField] private float pauseAtEnds = 0.6f;

        [Tooltip("Seconds to wait before the first movement.")]
        [Min(0f)]
        [SerializeField] private float startDelay = 0f;

        private Vector3 _startPosition;
        private Vector3 _lastPosition;
        private Vector3 _deltaPosition;

        private float _travelTimer;
        private float _waitTimer;
        private bool _movingForward = true;
        private bool _started;

        /// <summary>World space movement applied during the frame that just ended. Already a delta.</summary>
        public override Vector3 DeltaPosition => _deltaPosition;

        public override bool IsMoving => _deltaPosition.sqrMagnitude > 0.0000001f;

        private void Awake()
        {
            _startPosition = transform.position;
            _lastPosition = _startPosition;
            _waitTimer = startDelay;
            _started = startDelay <= 0f;
        }

        private void Update()
        {
            _deltaPosition = Vector3.zero;

            if (GameManager.Instance != null && !GameManager.Instance.IsGameplayRunning)
            {
                _lastPosition = transform.position;
                return;
            }

            float deltaTime = Time.deltaTime;

            if (!_started)
            {
                _waitTimer -= deltaTime;
                if (_waitTimer > 0f) return;
                _started = true;
            }

            if (_waitTimer > 0f)
            {
                _waitTimer -= deltaTime;
                _lastPosition = transform.position;
                return;
            }

            float duration = Mathf.Max(0.2f, travelDuration);
            _travelTimer += deltaTime * (_movingForward ? 1f : -1f);
            _travelTimer = Mathf.Clamp(_travelTimer, 0f, duration);

            if (_travelTimer >= duration)
            {
                _movingForward = false;
                _waitTimer = pauseAtEnds;
            }
            else if (_travelTimer <= 0f)
            {
                _movingForward = true;
                _waitTimer = pauseAtEnds;
            }

            Vector3 newPosition = _startPosition + (travelOffset * (_travelTimer / duration));
            _deltaPosition = newPosition - _lastPosition;

            transform.position = newPosition;
            _lastPosition = newPosition;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.9f);
            Vector3 from = transform.position;
            Vector3 to = from + travelOffset;

            Gizmos.DrawLine(from, to);
            Gizmos.DrawWireCube(from, new Vector3(0.4f, 0.4f, 0.4f));
            Gizmos.DrawWireCube(to, new Vector3(0.4f, 0.4f, 0.4f));
        }
    }
}
