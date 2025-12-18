/*
 * author: mark joshwel
 * date: 18/12/2025
 * description: unified grounding system for AR entities - handles anchor position storage,
 *              timer-based Y-only stabilisation, and horizontal plane filtering
 */

using UnityEngine;

namespace PokkatCore
{
    /// <summary>
    ///     unified grounding behaviour for AR entities (nekos, bowls).
    ///     locks XZ to anchor position while allowing Y stabilisation against AR plane drift.
    ///     requires PlaneHandling reference via CoreGameplay.instance.planes
    /// </summary>
    public sealed class GroundingBehaviour : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Stabilisation Settings")]
        [Tooltip("enable periodic Y stabilisation against plane drift")]
        [SerializeField]
        private bool enableStabilisation = true;

        [Tooltip("seconds between stabilisation checks")] [SerializeField]
        private float stabilisationInterval = 0.1f;

        [Tooltip("minimum Y drift (metres) to trigger stabilisation")] [SerializeField]
        private float stabilisationThreshold = 0.02f;

        #endregion

        #region Private Fields

        /// <summary>
        ///     anchor position - XZ is locked, Y is adjusted by stabilisation
        /// </summary>
        private Vector3 _anchorPosition;

        /// <summary>
        ///     whether the entity has been grounded (enables stabilisation)
        /// </summary>
        private bool _isGrounded;

        /// <summary>
        ///     timer for stabilisation interval
        /// </summary>
        private float _stabilisationTimer;

        #endregion

        #region Public Properties

        /// <summary>
        ///     whether this entity is currently grounded
        /// </summary>
        public bool isGrounded => _isGrounded;

        /// <summary>
        ///     current anchor position (read-only)
        /// </summary>
        public Vector3 anchorPosition => _anchorPosition;

        #endregion

        #region Public Methods

        /// <summary>
        ///     grounds the entity at the specified position.
        ///     sets anchor and enables stabilisation
        /// </summary>
        /// <param name="position">the grounded position (will be used as XZ anchor)</param>
        public void Ground(Vector3 position)
        {
            _anchorPosition = position;
            _isGrounded = true;
            transform.position = position;
            Logkat.Dev($"GroundingBehaviour: grounded at {position}");
        }

        /// <summary>
        ///     updates anchor position for intentional movement (walking, navmesh snap).
        ///     call this after each walk step or when entity intentionally moves
        /// </summary>
        /// <param name="newPosition">the new anchor position</param>
        public void UpdateAnchor(Vector3 newPosition)
        {
            if (!_isGrounded) return;
            _anchorPosition = newPosition;
        }

        /// <summary>
        ///     performs stabilisation check - call from Update().
        ///     projects anchor XZ to nearest horizontal plane to get Y height only.
        ///     keeps XZ locked to anchor, only adjusts Y for plane drift correction
        /// </summary>
        public void Stabilise()
        {
            if (!enableStabilisation || !_isGrounded) return;

            // timer-based to avoid running every frame
            _stabilisationTimer -= Time.deltaTime;
            if (_stabilisationTimer > 0f) return;
            _stabilisationTimer = stabilisationInterval;

            var planes = CoreGameplay.instance?.planes;
            if (!planes) return;

            // get Y height at anchor XZ position (not at current position which may have drifted)
            // this prevents chasing planes that have shifted in XZ
            if (!planes.TryGetPlaneHeightAt(_anchorPosition, out var planeHeight))
            {
                Logkat.Dev($"GroundingBehaviour: no horizontal plane for stabilisation at anchor {_anchorPosition}");
                return;
            }

            // calculate Y drift (how much the entity has drifted from the plane)
            var yDrift = Mathf.Abs(transform.position.y - planeHeight);

            // check XZ drift from anchor (should be zero unless something moved us)
            var xzDrift = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z),
                new Vector2(_anchorPosition.x, _anchorPosition.z)
            );

            // only stabilise if drift exceeds threshold
            if (yDrift < stabilisationThreshold && xzDrift < stabilisationThreshold) return;

            // lock XZ to anchor, use plane height for Y
            var stablePos = new Vector3(_anchorPosition.x, planeHeight, _anchorPosition.z);
            Logkat.Dev(
                $"GroundingBehaviour: stabilising from {transform.position} to {stablePos} (yDrift={yDrift:F3}, xzDrift={xzDrift:F3})");
            transform.position = stablePos;

            // update anchor Y to match current plane height
            _anchorPosition.y = planeHeight;
        }

        /// <summary>
        ///     immediately snaps to ground (horizontal plane height) while preserving XZ anchor.
        ///     use for one-shot grounding after jumps or state transitions
        /// </summary>
        public void SnapToGround()
        {
            if (!_isGrounded) return;

            var planes = CoreGameplay.instance?.planes;
            if (!planes) return;

            // get Y height at anchor XZ position
            if (!planes.TryGetPlaneHeightAt(_anchorPosition, out var planeHeight)) return;

            // lock XZ to anchor, use plane height for Y
            var snappedPos = new Vector3(_anchorPosition.x, planeHeight, _anchorPosition.z);
            transform.position = snappedPos;
            _anchorPosition.y = planeHeight;
            Logkat.Dev($"GroundingBehaviour: snapped to ground at {snappedPos}");
        }

        /// <summary>
        ///     resets grounding state (for reuse or respawn scenarios)
        /// </summary>
        public void Reset()
        {
            _isGrounded = false;
            _anchorPosition = Vector3.zero;
            _stabilisationTimer = 0f;
        }

        #endregion
    }
}