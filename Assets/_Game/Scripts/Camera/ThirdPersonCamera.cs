using UnityEngine;

namespace SkyCoin
{
    /// <summary>
    /// A small, dependency free third person orbit camera.
    ///
    /// WHY NOT CINEMACHINE: Cinemachine is an excellent package, but it adds a second
    /// package to install, a second set of version specific setup steps and a virtual
    /// camera object to keep in sync with pause / victory / game over. This camera does
    /// everything this game needs in one readable file.
    ///
    /// What it does:
    ///   - orbits around the player with mouse or right stick,
    ///   - smoothly follows the player position (never the rotation, so the camera never spins),
    ///   - pulls in when a wall would block the view (SphereCast),
    ///   - stops reading input when the game is paused, so the view never drifts in a menu.
    ///
    /// Runs in LateUpdate so the player has already moved this frame.
    /// </summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Usually the Player root. Auto filled with the first PlayerController if left empty.")]
        [SerializeField] private Transform target;

        [Tooltip("Height above the target's origin that the camera orbits around.")]
        [SerializeField] private float pivotHeight = 1.5f;

        [Header("Distance")]
        [Tooltip("Default distance in metres.")]
        [SerializeField] private float distance = 6f;

        [Tooltip("Closest the player may zoom in.")]
        [SerializeField] private float minDistance = 2.5f;

        [Tooltip("Furthest the player may zoom out.")]
        [SerializeField] private float maxDistance = 11f;

        [Tooltip("How much one scroll notch changes the distance.")]
        [SerializeField] private float zoomStep = 1.2f;

        [SerializeField] private float zoomSmoothTime = 0.07f;

        [Header("Look")]
        [Tooltip("Rotation in degrees per pixel of mouse movement.")]
        [SerializeField] private float lookSensitivity = 0.14f;

        [SerializeField] private bool invertY = false;

        [Tooltip("How far the camera may look down. Negative looks up from below.")]
        [SerializeField] private float minPitch = -25f;

        [Tooltip("How far the camera may look down from above.")]
        [SerializeField] private float maxPitch = 70f;

        [SerializeField] private float startYaw = 0f;
        [SerializeField] private float startPitch = 18f;

        [Header("Smoothing")]
        [Tooltip("How long the camera takes to catch up with the player. Small values feel more responsive.")]
        [SerializeField] private float pivotSmoothTime = 0.06f;

        [Header("Clipping prevention")]
        [Tooltip("Layers the camera may collide with. Exclude the Player and Coin layers so the camera does not push itself off the player.")]
        [SerializeField] private LayerMask collisionMask = ~0;

        [Tooltip("Thickness of the camera probe sphere.")]
        [SerializeField] private float collisionRadius = 0.3f;

        [Tooltip("Extra gap kept between the camera and the wall it hit.")]
        [SerializeField] private float collisionBuffer = 0.15f;

        [Header("Diagnostics")]
        [SerializeField] private bool drawCollisionProbe = false;

        private float _yaw;
        private float _pitch = 18f;
        private float _currentDistance;
        private float _distanceVelocity;
        private Vector3 _smoothedPivot;
        private Vector3 _pivotVelocity;
        private bool _hasSmoothedPivot;

        // ------------------------------------------------------------------
        //  Lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            _yaw = startYaw;
            _pitch = startPitch;
            _currentDistance = distance;
        }

        private void Start()
        {
            if (target == null)
            {
                // One time lookup at level start. Never do this in Update.
                PlayerController player = FindAnyObjectByType<PlayerController>();
                if (player != null) target = player.transform;
            }

            if (target == null)
            {
                Debug.LogWarning("[ThirdPersonCamera] No target assigned and no PlayerController found in the scene.", this);
            }

            _smoothedPivot = target != null ? target.position + (Vector3.up * pivotHeight) : transform.position;
            _hasSmoothedPivot = true;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            float deltaTime = Time.deltaTime;

            ReadInput(deltaTime);

            Vector3 pivot = target.position + (Vector3.up * pivotHeight);

            // Smooth the follow so the camera never snaps when the player lands or teleports.
            if (!_hasSmoothedPivot)
            {
                _smoothedPivot = pivot;
                _hasSmoothedPivot = true;
            }
            else
            {
                _smoothedPivot = Vector3.SmoothDamp(_smoothedPivot, pivot, ref _pivotVelocity, pivotSmoothTime, Mathf.Infinity, deltaTime);
            }

            Quaternion orbitRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 desiredOffset = orbitRotation * new Vector3(0f, 0f, -_currentDistance);

            Vector3 finalPosition = ApplyClippingPrevention(_smoothedPivot, desiredOffset);

            transform.SetPositionAndRotation(finalPosition, orbitRotation);
        }

        // ------------------------------------------------------------------
        //  Internal steps
        // ------------------------------------------------------------------

        private void ReadInput(float deltaTime)
        {
            // Paused, victory and game over must not move the camera.
            bool gameplayRunning = GameManager.Instance == null || GameManager.Instance.IsGameplayRunning;

            // Zoom is always smoothed, even when no input is being read.
            float zoomValue = 0f;

            if (gameplayRunning && GameInput.Instance != null)
            {
                Vector2 look = GameInput.Instance.Look;
                float yawDelta = look.x * lookSensitivity;
                float pitchDelta = look.y * lookSensitivity * (invertY ? 1f : -1f);

                _yaw += yawDelta;
                _pitch = Mathf.Clamp(_pitch + pitchDelta, minPitch, maxPitch);

                zoomValue = GameInput.Instance.Zoom;

                // Some platforms report the scroll wheel in raw units (120 per notch) instead of
                // the normalised -1 / +1. Collapse anything large to a single step so zoom
                // behaves the same everywhere.
                if (Mathf.Abs(zoomValue) > 3f)
                {
                    zoomValue = Mathf.Sign(zoomValue);
                }
            }

            float wantedDistance = _currentDistance - (zoomValue * zoomStep);
            wantedDistance = Mathf.Clamp(wantedDistance, minDistance, maxDistance);

            _currentDistance = Mathf.SmoothDamp(_currentDistance, wantedDistance, ref _distanceVelocity, zoomSmoothTime, Mathf.Infinity, deltaTime);
        }

        private Vector3 ApplyClippingPrevention(Vector3 pivot, Vector3 desiredOffset)
        {
            float wantedLength = desiredOffset.magnitude;
            if (wantedLength < 0.001f)
            {
                return pivot;
            }

            Vector3 direction = desiredOffset / wantedLength;

            if (Physics.SphereCast(pivot, collisionRadius, direction, out RaycastHit hit,
                    wantedLength + collisionBuffer, collisionMask, QueryTriggerInteraction.Ignore))
            {
                float allowedLength = Mathf.Clamp(hit.distance - collisionBuffer, 0.4f, wantedLength);

                if (drawCollisionProbe)
                {
                    Debug.DrawLine(pivot, pivot + (direction * hit.distance), Color.cyan);
                }

                return pivot + (direction * allowedLength);
            }

            return pivot + desiredOffset;
        }

        // ------------------------------------------------------------------
        //  Small public API
        // ------------------------------------------------------------------

        /// <summary>Lets a level bootstrap or a respawn system re-point the camera at runtime.</summary>
        public void SetTarget(Transform newTarget, bool snapImmediately = true)
        {
            target = newTarget;

            if (snapImmediately && target != null)
            {
                _smoothedPivot = target.position + (Vector3.up * pivotHeight);
                _pivotVelocity = Vector3.zero;
                _hasSmoothedPivot = true;
            }
        }

        /// <summary>Lets a settings menu drive mouse sensitivity.</summary>
        public void SetLookSensitivity(float degreesPerPixel)
        {
            lookSensitivity = Mathf.Max(0.01f, degreesPerPixel);
        }

        /// <summary>Called when the player is teleported so the camera does not sweep across the level.</summary>
        public void SnapToTarget()
        {
            if (target == null) return;
            _smoothedPivot = target.position + (Vector3.up * pivotHeight);
            _pivotVelocity = Vector3.zero;
        }
    }
}
