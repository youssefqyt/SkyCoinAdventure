using System;
using UnityEngine;

namespace SkyCoin
{
    /// <summary>
    /// The only movement implementation in the project.
    ///
    /// DESIGN DECISION: CharacterController, not Rigidbody.
    ///   - CharacterController never slides, never gets pushed by physics and stops the
    ///     instant input stops, which is what a beginner friendly collect-a-thon needs.
    ///   - Teleporting is 100% reliable: disable the controller, move the transform,
    ///     enable the controller again. A Rigidbody requires moving with Rigidbody.position
    ///     and clearing velocity on the correct API, which is a common source of bugs.
    ///   - Gravity and jumping are written by us, so they are fully predictable.
    /// The trade off is that we must handle moving platforms ourselves - see PlatformCarrier.
    ///
    /// IMPORTANT: there must be NO Rigidbody on the Player root. Mixing Rigidbody and
    /// CharacterController on the same object produces unpredictable movement.
    /// The small kinematic Rigidbody that lives on the child "PickupSensor" is unrelated:
    /// it exists only so that coin trigger events fire reliably.
    ///
    /// Unity 6 note: because we do not use a Rigidbody for the player, the deprecated
    /// Rigidbody.velocity / Rigidbody.drag API (replaced by linearVelocity / linearDamping
    /// in Unity 6) is never involved.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DefaultExecutionOrder(-100)]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Walking speed in metres per second.")]
        [SerializeField] private float moveSpeed = 6f;

        [Tooltip("Sprint multiplies the walking speed. Keep this small so sprinting cannot skip the level.")]
        [SerializeField] private float sprintMultiplier = 1.45f;

        [Tooltip("How fast the character reaches its target velocity (m/s per second) when you press a direction.")]
        [SerializeField] private float acceleration = 45f;

        [Tooltip("How fast the character slows down when you release the keys. Higher = less sliding.")]
        [SerializeField] private float deceleration = 60f;

        [Tooltip("Acceleration and deceleration multiplier while airborne. Below 1 = less control in the air.")]
        [Range(0f, 1f)]
        [SerializeField] private float airControlMultiplier = 0.55f;

        [Tooltip("How fast the model turns to face the movement direction, in degrees per second.")]
        [SerializeField] private float rotationSpeedDegreesPerSecond = 720f;

        [Header("Jump")]
        [SerializeField] private bool canJump = true;

        [Tooltip("Peak height of the jump in metres. The code converts this into an initial velocity.")]
        [SerializeField] private float jumpHeight = 1.4f;

        [Tooltip("Custom gravity in m/s^2. Negative. -22 feels snappier than Earth's -9.81.")]
        [SerializeField] private float gravity = -22f;

        [Tooltip("Maximum falling speed. Prevents tunnelling through thin colliders on long falls.")]
        [SerializeField] private float terminalVelocity = -55f;

        [Tooltip("You may still jump this long after walking off an edge. Makes jumps feel fair.")]
        [SerializeField] private float coyoteTime = 0.12f;

        [Tooltip("A jump pressed this long before landing still triggers on landing. Makes jumps feel responsive.")]
        [SerializeField] private float jumpBufferTime = 0.12f;

        [Tooltip("Small downward velocity applied while grounded so the player sticks to slopes and steps.")]
        [SerializeField] private float groundStickVelocity = -3f;

        [Header("Ground check")]
        [Tooltip("Which layers count as floor. Set this to 'Ground' only (see the setup guide).")]
        [SerializeField] private LayerMask groundMask = ~0;

        [Tooltip("How far below the capsule bottom we still accept a surface as ground.")]
        [SerializeField] private float groundProbeExtraDistance = 0.15f;

        [Header("References")]
        [Tooltip("Assign the Main Camera. Used to make movement camera relative. Auto filled from Camera.main if left empty.")]
        [SerializeField] private Transform cameraTransform;

        [Tooltip("The child that holds the visible robot. Found automatically by name if left empty.")]
        [SerializeField] private Transform visualRoot;

        [Header("Diagnostics")]
        [Tooltip("Draw the ground probe in the Scene view (yellow = grounded, red = airborne).")]
        [SerializeField] private bool drawGroundProbe = false;

        /// <summary>Raised after a teleport so cosmetic scripts can reset their animation state.</summary>
        public event Action Teleported;

        private CharacterController _controller;

        private Vector3 _horizontalVelocity;      // always y = 0
        private float _verticalVelocity;

        private float _lastGroundedTime = -999f;
        private float _jumpBufferedUntil = -999f;

        private Vector3 _groundNormal = Vector3.up;
        private PlatformCarrier _groundCarrier;

        // ------------------------------------------------------------------
        //  Public read only view (used by visuals, camera, level logic)
        // ------------------------------------------------------------------

        /// <summary>True when a walkable surface was found directly under the capsule this frame.</summary>
        public bool IsGrounded { get; private set; }

        /// <summary>The configured walking speed. Cosmetic scripts use it to scale their animation.</summary>
        public float MoveSpeed => moveSpeed;

        /// <summary>Current horizontal speed in m/s. Vertical speed is not included.</summary>
        public float PlanarSpeed => new Vector2(_horizontalVelocity.x, _horizontalVelocity.z).magnitude;

        /// <summary>Current vertical speed in m/s. Positive means going up.</summary>
        public float VerticalVelocity => _verticalVelocity;

        /// <summary>True while the sprint key is held and the player is actually moving.</summary>
        public bool IsSprinting { get; private set; }

        // ------------------------------------------------------------------
        //  Unity lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            if (visualRoot == null)
            {
                visualRoot = transform.Find("VisualRoot");
            }

            // Defensive checks. These warn instead of failing, but they are worth fixing.
            if (cameraTransform == null)
            {
                Debug.LogWarning("[PlayerController] No camera assigned and no Camera.main found. Movement will fall back to world axes.", this);
            }

            int ownLayerBit = 1 << gameObject.layer;
            if ((groundMask.value & ownLayerBit) != 0)
            {
                Debug.LogWarning("[PlayerController] Ground Mask includes the Player's own layer. Set Ground Mask to 'Ground' only, otherwise the ground probe can hit the player.", this);
            }

            if (_controller.stepOffset <= 0f)
            {
                Debug.LogWarning("[PlayerController] CharacterController step offset is 0. Small steps and ramps will block the player.", this);
            }
        }

        private void Update()
        {
            // Anything that is not "Playing" (pause, victory, game over) freezes the player.
            if (GameManager.Instance != null && !GameManager.Instance.IsGameplayRunning)
            {
                return;
            }

            // Clamp the step so a frame hitch or a long stall cannot teleport the player
            // through a thin collider.
            float deltaTime = Mathf.Min(Time.deltaTime, 0.05f);

            ProbeGround();
            ReadJumpRequests();
            ApplyMovement(deltaTime);
        }

        // ------------------------------------------------------------------
        //  Ground detection
        // ------------------------------------------------------------------

        /// <summary>
        /// A downward SphereCast that matches the capsule's bottom half.
        /// This is more reliable than CharacterController.isGrounded, which reports true
        /// for a frame or two right after a jump.
        /// </summary>
        private void ProbeGround()
        {
            Vector3 worldCenter = transform.TransformPoint(_controller.center);
            float probeRadius = _controller.radius * 0.9f;
            float castDistance =
                Mathf.Max(0.01f, (_controller.height * 0.5f) - _controller.radius) + groundProbeExtraDistance;

            IsGrounded = false;
            _groundNormal = Vector3.up;
            _groundCarrier = null;

            if (Physics.SphereCast(worldCenter, probeRadius, Vector3.down, out RaycastHit hit,
                    castDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                // Surfaces steeper than the controller's slope limit are not "ground":
                // the CharacterController would slide the player down them anyway.
                float minimumNormalY = Mathf.Cos((_controller.slopeLimit + 5f) * Mathf.Deg2Rad);

                if (hit.normal.y >= minimumNormalY)
                {
                    IsGrounded = true;
                    _groundNormal = hit.normal;
                    _groundCarrier = hit.collider.GetComponentInParent<PlatformCarrier>();
                }
            }

            if (IsGrounded)
            {
                _lastGroundedTime = Time.time;
            }

            if (drawGroundProbe)
            {
                Debug.DrawRay(worldCenter, Vector3.down * castDistance, IsGrounded ? Color.yellow : Color.red);
            }
        }

        private void ReadJumpRequests()
        {
            // Consume (not peek): a single tap - keyboard or touch button - must register once.
            if (GameInput.Instance != null && GameInput.Instance.ConsumeJumpPressedThisFrame())
            {
                // Remember the press. It is consumed on this frame, or a few frames later
                // if the player is just about to land.
                _jumpBufferedUntil = Time.time + jumpBufferTime;
            }
        }

        // ------------------------------------------------------------------
        //  Movement
        // ------------------------------------------------------------------

        private void ApplyMovement(float deltaTime)
        {
            Vector2 rawInput = GameInput.Instance != null ? GameInput.Instance.Move : Vector2.zero;

            Vector3 wishDirection = ToCameraRelativeDirection(rawInput);

            // Walking up a ramp must follow the ramp, not push into it.
            if (IsGrounded && wishDirection.sqrMagnitude > 0.0001f)
            {
                wishDirection = Vector3.ProjectOnPlane(wishDirection, _groundNormal).normalized;
            }

            IsSprinting = GameInput.Instance != null && GameInput.Instance.SprintHeld && wishDirection.sqrMagnitude > 0.0001f;
            float targetSpeed = moveSpeed * (IsSprinting ? sprintMultiplier : 1f);

            Vector3 targetHorizontalVelocity = wishDirection * targetSpeed;

            float rate = wishDirection.sqrMagnitude > 0.0001f ? acceleration : deceleration;
            if (!IsGrounded)
            {
                rate *= airControlMultiplier;
            }

            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, targetHorizontalVelocity, rate * deltaTime);

            ApplyVerticalMovement(deltaTime);

            // --- Assemble the final movement for this frame ---
            Vector3 frameMotion = _horizontalVelocity * deltaTime;
            frameMotion.y = _verticalVelocity * deltaTime;

            // Moving platform carry. DeltaPosition is a per frame delta, so it is NOT
            // multiplied by deltaTime.
            if (IsGrounded && _groundCarrier != null)
            {
                frameMotion += _groundCarrier.DeltaPosition;
            }

            _controller.Move(frameMotion);

            ApplyRotation(wishDirection, deltaTime);
        }

        private void ApplyVerticalMovement(float deltaTime)
        {
            bool jumpAllowed =
                canJump &&
                Time.time <= _jumpBufferedUntil &&
                Time.time - _lastGroundedTime <= coyoteTime;

            if (jumpAllowed)
            {
                // Exact initial velocity needed to reach jumpHeight under our custom gravity.
                _verticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);

                // Consume both timers so one press can never produce two jumps.
                _jumpBufferedUntil = -999f;
                _lastGroundedTime = -999f;
                IsGrounded = false;
                _groundCarrier = null;
                return;
            }

            if (IsGrounded && _verticalVelocity <= 0f)
            {
                _verticalVelocity = groundStickVelocity;
            }
            else
            {
                _verticalVelocity = Mathf.Max(_verticalVelocity + (gravity * deltaTime), terminalVelocity);
            }
        }

        private void ApplyRotation(Vector3 wishDirection, float deltaTime)
        {
            if (wishDirection.sqrMagnitude < 0.0001f) return;

            Quaternion targetRotation = Quaternion.LookRotation(wishDirection, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeedDegreesPerSecond * deltaTime);
        }

        /// <summary>
        /// Turns a raw stick / WASD input into a world direction based on where the camera looks.
        /// The vertical part of the camera's forward is removed so looking down does not slow the player.
        /// </summary>
        private Vector3 ToCameraRelativeDirection(Vector2 input)
        {
            if (input.sqrMagnitude < 0.0001f) return Vector3.zero;

            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;

            if (cameraTransform != null)
            {
                forward = cameraTransform.forward;
                right = cameraTransform.right;
            }

            forward.y = 0f;
            right.y = 0f;

            // If the camera points straight down, forward collapses. Fall back to world axes.
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            if (right.sqrMagnitude < 0.01f) right = Vector3.right;

            forward.Normalize();
            right.Normalize();

            return (forward * input.y + right * input.x).normalized;
        }

        // ------------------------------------------------------------------
        //  Public API used by level logic and checkpoints
        // ------------------------------------------------------------------

        /// <summary>
        /// Moves the player to a new position safely.
        /// The CharacterController is disabled for the single frame of the move, which is
        /// what makes this reliable - writing to transform.position while the controller is
        /// enabled can be silently ignored or produce a large depenetration push.
        /// </summary>
        public void Teleport(Vector3 worldPosition, Quaternion worldRotation)
        {
            bool controllerWasEnabled = _controller.enabled;
            _controller.enabled = false;

            transform.SetPositionAndRotation(worldPosition, worldRotation);

            _controller.enabled = controllerWasEnabled;

            _horizontalVelocity = Vector3.zero;
            _verticalVelocity = 0f;
            _jumpBufferedUntil = -999f;
            _lastGroundedTime = -999f;
            _groundNormal = Vector3.up;
            _groundCarrier = null;
            IsGrounded = false;
            IsSprinting = false;

            Teleported?.Invoke();
        }
    }
}
