using UnityEngine;

namespace SkyCoin
{
    /// <summary>
    /// A floor tile that cycles through five explicit phases so the player always gets a
    /// warning before it drops:
    ///
    ///   Safe -> Warning (flashes orange) -> Open (collider DISABLED, plate sinks away)
    ///        -> Returning (collider back, plate rises) -> Cooldown -> Safe -> ...
    ///
    /// KEY REQUIREMENT: when the hazard is open the COLLIDER IS DISABLED, not just hidden.
    /// Hiding a mesh while leaving the collider on produces the classic bug where the
    /// player walks on thin air and the level becomes impossible to read.
    ///
    /// The whole cycle uses scaled time, so it freezes automatically in pause, victory and
    /// game over (Time.timeScale = 0). Nothing here ever destroys or moves the player.
    ///
    /// This plate belongs on the GROUND layer: while it is safe, the player's ground probe
    /// must treat it as real floor, exactly like an island.
    /// </summary>
    public class DisappearingFloor : MonoBehaviour
    {
        private enum Phase
        {
            Safe,
            Warning,
            Open,
            Returning,
            Cooldown
        }

        [Header("Timings (seconds)")]
        [Tooltip("How long the plate is solid and normal.")]
        [SerializeField] private float safeDuration = 2.5f;

        [Tooltip("Flashing warning before the plate drops. Never go below about 0.8s.")]
        [SerializeField] private float warningDuration = 1.2f;

        [Tooltip("How long the plate stays gone.")]
        [SerializeField] private float openDuration = 2.0f;

        [Tooltip("How long the plate takes to rise back into place.")]
        [SerializeField] private float returningDuration = 0.3f;

        [Tooltip("Extra rest after returning, so the cycle is not metronomic.")]
        [SerializeField] private float cooldownDuration = 1.5f;

        [Header("Desynchronisation")]
        [Tooltip("Seconds to skip at level start. Give neighbouring plates different values.")]
        [SerializeField] private float startDelay = 0f;

        [Header("Visuals")]
        [Tooltip("Auto resolved from this GameObject if empty. The plate is tinted for the warning phase.")]
        [SerializeField] private Renderer plateRenderer;

        [Tooltip("Flash speed while warning. Uses MaterialPropertyBlock, so no material is instanced.")]
        [SerializeField] private float warningFlashSpeed = 7f;

        [Tooltip("How far the plate sinks while open. Large enough to read as 'gone'.")]
        [SerializeField] private float openSinkDepth = 8f;

        [SerializeField] private float sinkSpeed = 16f;

        [Header("Collision")]
        [Tooltip("Auto resolved from this GameObject if empty. This is the collider that gets disabled.")]
        [SerializeField] private Collider plateCollider;

        private Phase _phase = Phase.Safe;
        private float _phaseTimer;

        private Renderer[] _childRenderers;
        private MaterialPropertyBlock _block;
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        private Color _baseColor = Color.grey;
        private Color _warningColor = new Color(1f, 0.35f, 0.1f);

        private Vector3 _shownPosition;
        private Vector3 _hiddenPosition;

        private void Awake()
        {
            if (plateCollider == null) plateCollider = GetComponent<Collider>();
            if (plateRenderer == null) plateRenderer = GetComponent<Renderer>();
            if (plateRenderer == null) plateRenderer = GetComponentInChildren<Renderer>();

            if (plateCollider == null)
            {
                Debug.LogError("[DisappearingFloor] No collider found. The plate can never block the player, so it would be a permanent hole.", this);
                enabled = false;
                return;
            }

            _shownPosition = transform.position;
            _hiddenPosition = _shownPosition + (Vector3.down * openSinkDepth);

            _block = new MaterialPropertyBlock();
            _childRenderers = GetComponentsInChildren<Renderer>();

            if (plateRenderer != null && plateRenderer.sharedMaterial != null &&
                plateRenderer.sharedMaterial.HasProperty(ColorId))
            {
                _baseColor = plateRenderer.sharedMaterial.GetColor(ColorId);
            }

            _phase = Phase.Safe;
            _phaseTimer = -startDelay;
            SetColliderEnabled(true);
            SetWarningTint(false);
        }

        private void Update()
        {
            // Frozen during pause, victory and game over.
            if (GameManager.Instance != null && !GameManager.Instance.IsGameplayRunning) return;

            float deltaTime = Time.deltaTime;

            if (_phaseTimer < 0f)
            {
                _phaseTimer += deltaTime;
                return;
            }

            _phaseTimer += deltaTime;

            switch (_phase)
            {
                case Phase.Safe:
                    if (_phaseTimer >= safeDuration) Advance(Phase.Warning);
                    break;

                case Phase.Warning:
                    UpdateWarningFlash();
                    if (_phaseTimer >= warningDuration) Advance(Phase.Open);
                    break;

                case Phase.Open:
                    if (_phaseTimer >= openDuration) Advance(Phase.Returning);
                    break;

                case Phase.Returning:
                    if (_phaseTimer >= returningDuration) Advance(Phase.Cooldown);
                    break;

                case Phase.Cooldown:
                    if (_phaseTimer >= cooldownDuration) Advance(Phase.Safe);
                    break;
            }

            UpdateVisualPosition(deltaTime);
        }

        private void Advance(Phase next)
        {
            _phase = next;
            _phaseTimer = 0f;

            switch (next)
            {
                case Phase.Safe:
                    SetColliderEnabled(true);
                    SetWarningTint(false);
                    break;

                case Phase.Warning:
                    // Still solid while warning: the player must have time to step off.
                    SetColliderEnabled(true);
                    break;

                case Phase.Open:
                    // THE important line: the collision surface really goes away.
                    SetColliderEnabled(false);
                    SetWarningTint(false);
                    break;

                case Phase.Returning:
                    // Back on before the plate is visually back, so the player can land on it.
                    SetColliderEnabled(true);
                    SetWarningTint(false);
                    break;

                case Phase.Cooldown:
                    SetColliderEnabled(true);
                    SetWarningTint(false);
                    break;
            }
        }

        private void UpdateVisualPosition(float deltaTime)
        {
            Vector3 targetPosition = _phase == Phase.Open ? _hiddenPosition : _shownPosition;
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, sinkSpeed * deltaTime);
        }

        private void SetColliderEnabled(bool value)
        {
            if (plateCollider == null) return;
            plateCollider.enabled = value;
        }

        private void UpdateWarningFlash()
        {
            float pulse = (Mathf.Sin(Time.time * warningFlashSpeed) * 0.5f) + 0.5f;
            SetWarningTint(true, pulse);
        }

        /// <summary>
        /// Tints the plate without creating a material instance: MaterialPropertyBlock only
        /// overrides the value for this renderer, so the shared material is never modified.
        /// </summary>
        private void SetWarningTint(bool warning, float pulse = 1f)
        {
            if (_block == null || _childRenderers == null) return;

            Color color = warning ? Color.Lerp(_baseColor, _warningColor, pulse) : _baseColor;

            for (int i = 0; i < _childRenderers.Length; i++)
            {
                Renderer renderer = _childRenderers[i];
                if (renderer == null) continue;

                renderer.GetPropertyBlock(_block);
                _block.SetColor(ColorId, color);
                renderer.SetPropertyBlock(_block);
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Shows the sink distance in the Scene view.
            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.8f);
            Gizmos.DrawWireCube(transform.position + (Vector3.down * openSinkDepth * 0.5f),
                new Vector3(1f, Mathf.Abs(openSinkDepth), 1f));
        }
    }
}
