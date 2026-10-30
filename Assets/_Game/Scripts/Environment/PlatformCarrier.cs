using UnityEngine;

namespace SkyCoin
{
    /// <summary>
    /// Base class for any surface the player can stand on that moves by itself.
    ///
    /// WHY THIS EXISTS
    /// The player uses a CharacterController, which is moved manually with
    /// CharacterController.Move(). Unity does not automatically carry such a player when
    /// the floor under it moves, so the player script adds the platform's per frame
    /// movement to its own movement. This class is the contract between the two.
    ///
    /// <see cref="DeltaPosition"/> is a PER FRAME DELTA in world units, not a velocity.
    /// Do not multiply it by Time.deltaTime.
    ///
    /// The concrete implementation (MovingPlatform) is added in Milestone 5.
    /// </summary>
    public abstract class PlatformCarrier : MonoBehaviour
    {
        /// <summary>World space movement applied during the frame that just ended.</summary>
        public abstract Vector3 DeltaPosition { get; }

        /// <summary>True while the platform is actually moving.</summary>
        public abstract bool IsMoving { get; }
    }
}
