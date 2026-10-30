using UnityEngine;

namespace SkyCoin
{
    /// <summary>
    /// Marks the authored starting position of a level.
    ///
    /// WHY A COMPONENT INSTEAD OF A HARD CODED POSITION:
    /// a spawn is a design decision, not a number in code. It also lets LevelController
    /// place the player with PlayerController.Teleport(), which disables the
    /// CharacterController for the move - the only reliable way to place it.
    /// The gizmo below makes it obvious in the Scene view whether the spawn sits
    /// above real ground or inside a rock.
    /// </summary>
    public class PlayerSpawnPoint : MonoBehaviour
    {
        public Vector3 SpawnPosition => transform.position;
        public Quaternion SpawnRotation => transform.rotation;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.35f, 1f);
            Gizmos.DrawWireSphere(transform.position + (Vector3.up * 0.9f), 0.9f);
            Gizmos.DrawRay(transform.position, transform.forward * 2f);
        }
    }
}
