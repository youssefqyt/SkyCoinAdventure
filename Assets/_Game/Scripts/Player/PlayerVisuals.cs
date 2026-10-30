using UnityEngine;

namespace SkyCoin
{
    /// <summary>
    /// Cosmetic procedural animation for the placeholder robot.
    ///
    /// This component NEVER touches the Player root, its CharacterController or its
    /// colliders. It only animates the child transform "VisualRoot" and the limb pivots.
    /// That separation is what allows the placeholder robot to be replaced by a rigged
    /// character later: delete the visual children, drop the new model in, then either
    /// keep this script (bob and lean still work) or disable it and let an Animator drive
    /// the model. No gameplay code has to change either way.
    ///
    /// Runs after PlayerController thanks to the execution order below.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class PlayerVisuals : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The Player root. Auto filled from the same GameObject if left empty.")]
        [SerializeField] private PlayerController player;

        [Tooltip("Child transform that holds the visible robot.")]
        [SerializeField] private Transform visualRoot;

        [SerializeField] private Transform armLeft;
        [SerializeField] private Transform armRight;
        [SerializeField] private Transform legLeft;
        [SerializeField] private Transform legRight;

        [Tooltip("Renderer of the visor. Used for the coin pickup glow. Optional.")]
        [SerializeField] private Renderer visorRenderer;

        [Header("Walk bob")]
        [SerializeField] private float bobHeight = 0.07f;

        [Tooltip("Full strides per second at full walking speed.")]
        [SerializeField] private float bobFrequency = 1.6f;

        [SerializeField] private float swayAmount = 0.025f;

        [Header("Limb and body motion")]
        [SerializeField] private float legSwingDegrees = 34f;
        [SerializeField] private float armSwingDegrees = 26f;
        [SerializeField] private float forwardLeanDegrees = 8f;
        [SerializeField] private float sideLeanDegrees = 14f;

        [Tooltip("How quickly the animation reacts to speed changes. Higher = snappier.")]
        [SerializeField] private float motionResponse = 12f;

        [Header("Air pose")]
        [SerializeField] private float airArmDegrees = 25f;
        [SerializeField] private float airLegTuckDegrees = 14f;

        [Header("Pickup reaction")]
        [SerializeField] private float pickupPunchScale = 1.16f;
        [SerializeField] private float pickupPunchDuration = 0.22f;
        [SerializeField] private Color visorPickupColor = new Color(1f, 0.85f, 0.25f, 1f);
        [SerializeField] private float visorFlashDuration = 0.25f;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private Vector3 _rootRestPosition;
        private Quaternion _rootRestRotation;
        private Vector3 _rootRestScale;

        private float _phase;
        private float _speed01;
        private float _sideLean;
        private float _previousYaw;

        private float _punchTimer;
        private float _flashTimer;

        private MaterialPropertyBlock _visorBlock;
        private Color _visorBaseEmission = Color.black;
        private bool _visorIsFlashing;

        // ------------------------------------------------------------------
        //  Lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            if (player == null) player = GetComponent<PlayerController>();
            if (visualRoot == null) visualRoot = transform.Find("VisualRoot");

            // Limb references: use what was assigned, otherwise look them up by the names
            // used in the setup guide so a forgotten Inspector slot is not fatal.
            if (armLeft == null) armLeft = FindChildByName("ArmLeft");
            if (armRight == null) armRight = FindChildByName("ArmRight");
            if (legLeft == null) legLeft = FindChildByName("LegLeft");
            if (legRight == null) legRight = FindChildByName("LegRight");

            if (visorRenderer == null)
            {
                Transform visor = FindChildByName("Visor");
                if (visor != null) visorRenderer = visor.GetComponent<Renderer>();
            }

            if (visualRoot == null)
            {
                Debug.LogWarning("[PlayerVisuals] No VisualRoot found. Procedural animation is disabled.", this);
                enabled = false;
                return;
            }

            _rootRestPosition = visualRoot.localPosition;
            _rootRestRotation = visualRoot.localRotation;
            _rootRestScale = visualRoot.localScale;
            _previousYaw = transform.eulerAngles.y;

            if (visorRenderer != null)
            {
                _visorBlock = new MaterialPropertyBlock();
                if (visorRenderer.sharedMaterial != null &&
                    visorRenderer.sharedMaterial.HasProperty(EmissionColorId))
                {
                    _visorBaseEmission = visorRenderer.sharedMaterial.GetColor(EmissionColorId);
                }
            }
        }

        private void OnEnable()
        {
            if (player != null) player.Teleported += HandleTeleported;
        }

        private void OnDisable()
        {
            // Always unsubscribe. Unbounded subscriptions are one of the classic Unity leaks.
            if (player != null) player.Teleported -= HandleTeleported;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return;   // frozen (paused / victory / game over)

            UpdateSpeedAndPhase(deltaTime);
            UpdateBody(deltaTime);
            UpdateLimbs();
            UpdatePickupFeedback(deltaTime);
        }

        // ------------------------------------------------------------------
        //  Animation steps
        // ------------------------------------------------------------------

        private void UpdateSpeedAndPhase(float deltaTime)
        {
            float referenceSpeed = player != null ? Mathf.Max(0.01f, player.MoveSpeed) : 1f;
            float targetSpeed01 = player != null ? Mathf.Clamp01(player.PlanarSpeed / referenceSpeed) : 0f;

            _speed01 = Mathf.Lerp(_speed01, targetSpeed01, motionResponse * deltaTime);

            // The stride phase only advances while the robot is actually moving.
            _phase += deltaTime * bobFrequency * _speed01 * Mathf.PI * 2f;
        }

        private void UpdateBody(float deltaTime)
        {
            // Vertical bob: two little steps per full phase cycle.
            float bob = Mathf.Abs(Mathf.Sin(_phase)) * bobHeight * _speed01;

            // Slight lateral sway.
            float sway = Mathf.Cos(_phase) * swayAmount * _speed01;

            visualRoot.localPosition = _rootRestPosition + new Vector3(sway, bob, 0f);

            // Side lean follows how fast the robot is turning.
            float yaw = transform.eulerAngles.y;
            float yawDelta = Mathf.DeltaAngle(_previousYaw, yaw);
            _previousYaw = yaw;

            float targetSideLean = 0f;
            if (deltaTime > 0f)
            {
                targetSideLean = Mathf.Clamp(-(yawDelta / deltaTime) * 0.05f, -sideLeanDegrees, sideLeanDegrees);
            }
            _sideLean = Mathf.Lerp(_sideLean, targetSideLean, motionResponse * deltaTime);

            float forwardLean = forwardLeanDegrees * _speed01;
            if (player != null && !player.IsGrounded)
            {
                // A little forward tilt in the air reads as "jumping".
                forwardLean += 4f;
            }

            visualRoot.localRotation = _rootRestRotation * Quaternion.Euler(forwardLean, 0f, _sideLean);

            // Pickup punch is applied as a uniform scale pulse.
            float punch = 1f;
            if (_punchTimer > 0f)
            {
                float k = _punchTimer / pickupPunchDuration;          // 1 -> 0
                punch = 1f + ((pickupPunchScale - 1f) * Mathf.Sin(k * Mathf.PI));
            }
            visualRoot.localScale = _rootRestScale * punch;
        }

        private void UpdateLimbs()
        {
            bool grounded = player == null || player.IsGrounded;

            float stride = Mathf.Sin(_phase) * _speed01;

            float legSwing = stride * legSwingDegrees;
            float armSwing = stride * armSwingDegrees;

            if (!grounded)
            {
                // In the air: arms lift, legs tuck, and the stride freezes.
                legSwing = 0f;
                armSwing = 0f;
            }

            SetLocalRotation(legLeft, legSwing - (grounded ? 0f : airLegTuckDegrees));
            SetLocalRotation(legRight, -legSwing - (grounded ? 0f : airLegTuckDegrees));

            SetLocalRotation(armLeft, -armSwing - (grounded ? 0f : airArmDegrees));
            SetLocalRotation(armRight, armSwing - (grounded ? 0f : airArmDegrees));
        }

        private void UpdatePickupFeedback(float deltaTime)
        {
            if (_punchTimer > 0f) _punchTimer = Mathf.Max(0f, _punchTimer - deltaTime);

            if (visorRenderer == null || _visorBlock == null) return;

            if (_flashTimer > 0f)
            {
                _flashTimer = Mathf.Max(0f, _flashTimer - deltaTime);
                float k = _flashTimer / visorFlashDuration;
                Color color = Color.Lerp(_visorBaseEmission, visorPickupColor * 4f, k);

                _visorBlock.SetColor(EmissionColorId, color);
                visorRenderer.SetPropertyBlock(_visorBlock);
                _visorIsFlashing = true;
                return;
            }

            // One final write restores the original emission, then we stop touching the renderer.
            if (_visorIsFlashing)
            {
                _visorIsFlashing = false;
                _visorBlock.SetColor(EmissionColorId, _visorBaseEmission);
                visorRenderer.SetPropertyBlock(_visorBlock);
            }
        }

        // ------------------------------------------------------------------
        //  Public API
        // ------------------------------------------------------------------

        /// <summary>Called by the coin when it is collected.</summary>
        public void PlayPickupReaction()
        {
            _punchTimer = pickupPunchDuration;
            _flashTimer = visorFlashDuration;
        }

        // ------------------------------------------------------------------
        //  Helpers
        // ------------------------------------------------------------------

        private Transform FindChildByName(string childName)
        {
            // Limbs are optional. A replacement model without a part named "LegLeft"
            // simply does not animate that part - no warning, this is a normal situation.
            Transform root = visualRoot != null ? visualRoot : transform;
            return FindRecursive(root, childName);
        }

        private static Transform FindRecursive(Transform parent, string childName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName) return child;

                Transform deeper = FindRecursive(child, childName);
                if (deeper != null) return deeper;
            }
            return null;
        }

        private static void SetLocalRotation(Transform target, float xDegrees)
        {
            if (target == null) return;
            target.localRotation = Quaternion.Euler(xDegrees, 0f, 0f);
        }

        private void HandleTeleported()
        {
            _phase = 0f;
            _speed01 = 0f;
            _sideLean = 0f;
            _punchTimer = 0f;
            _flashTimer = 0f;
            _previousYaw = transform.eulerAngles.y;

            if (visualRoot != null)
            {
                visualRoot.localPosition = _rootRestPosition;
                visualRoot.localRotation = _rootRestRotation;
                visualRoot.localScale = _rootRestScale;
            }
        }
    }
}
