/*
 * author: mark joshwel
 * date: 11/12/2025
 * description: neko character with texture management, blinking, and procedural animations
 */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace PokkatCore
{
    /// <summary>
    ///     neko entity with texture loading, periodic blinking, and procedural movement
    /// </summary>
    public class AREntityNeko : MonoBehaviour
    {
        [Header("Texture Settings")] [Tooltip("texture id to load (0-44)")] [SerializeField]
        private int textureId;

        [Tooltip("periodic blinking")] [SerializeField]
        private bool enableBlinking = true;

        [Tooltip("seconds between blinks")] [SerializeField]
        private float blinkInterval = 3f;

        [Tooltip("blink duration in seconds")] [SerializeField]
        private float blinkDuration = 0.15f;

        [Header("Movement Settings")] [Tooltip("metres per second")] [SerializeField]
        private float walkSpeed = 1f;

        [Tooltip("seconds per step")] [SerializeField]
        private float walkStepDuration = 0.15f;

        [Tooltip("tilt angle in degrees")] [SerializeField]
        private float walkTiltAngle = 25f;

        [Tooltip("jump height in metres")] [SerializeField]
        private float jumpHeight = 0.15f;

        [Tooltip("jump duration in seconds")] [SerializeField]
        private float jumpDuration = 0.3f;

        [Tooltip("fall speed in metres per second")] [SerializeField]
        private float fallSpeed = 2f;

        [Header("Ground Stabilisation")]
        [Tooltip("project to nearest plane when grounded")]
        [SerializeField]
        private bool enableGroundStabilisation = true;

        [Tooltip("seconds between checks")] [SerializeField]
        private float stabilisationInterval = 0.5f;

        [Tooltip("minimum drift to stabilise")] [SerializeField]
        private float stabilisationThreshold = 0.02f;

        /// <summary>
        ///     cached renderers for texture application
        /// </summary>
        private readonly List<Renderer> _renderers = new();

        /// <summary>
        ///     original y rotation to restore after animations
        /// </summary>
        private float _baseYRotation;

        /// <summary>
        ///     coroutine handle for blinking
        /// </summary>
        private Coroutine _blinkCoroutine;

        /// <summary>
        ///     eyes closed texture for this neko
        /// </summary>
        private Texture _eyesClosedTexture;

        /// <summary>
        ///     eyes open texture for this neko
        /// </summary>
        private Texture _eyesOpenTexture;

        /// <summary>
        ///     whether this neko is currently following a tracked image
        /// </summary>
        private bool _isFollowing;

        /// <summary>
        ///     whether this neko has landed on a plane
        /// </summary>
        private bool _isGrounded;

        /// <summary>
        ///     coroutine handle for current movement animation
        /// </summary>
        private Coroutine _movementCoroutine;

        /// <summary>
        ///     timer for ground stabilisation interval
        /// </summary>
        private float _stabilisationTimer;

        private void Awake()
        {
            CacheRenderers();

            if (CompareTag("NekoFriend"))
            {
                Logkat.Out("AREntityNeko: i am a friend! (randomising texture)");
                RandomizeTextureId();
            }
            else
            {
                if (textureId is < 0 or > 44)
                {
                    Logkat.Warn("AREntityNeko: textureId out of range (0-44), defaulting to 0");
                    textureId = 0;
                }
            }

            LoadTextures(textureId);
            Logkat.Out("AREntityNeko: Awake/Setup OK");
        }

        private void Start()
        {
            ApplyTexture(_eyesOpenTexture);
            _baseYRotation = transform.eulerAngles.y;

            if (enableBlinking)
                _blinkCoroutine = StartCoroutine(BlinkRoutine());

            Logkat.Out("AREntityNeko: Start/Configure OK");
        }

        private void Update()
        {
            // skip ground stabilisation if disabled, not grounded, or following
            if (!enableGroundStabilisation || !_isGrounded || _isFollowing) return;

            // timer-based stabilisation to avoid running every frame
            _stabilisationTimer -= Time.deltaTime;
            if (_stabilisationTimer > 0f) return;
            _stabilisationTimer = stabilisationInterval;

            // get CoreGameplay and planes reference
            var gameplay = CoreGameplay.instance;
            if (!gameplay || !gameplay.planes) return;

            // project current position onto nearest plane
            if (!gameplay.planes.TryProjectToPlane(transform.position, out var projectedPos)) return;

            // only move if drift exceeds threshold (avoids micro-jitter)
            var drift = Vector3.Distance(transform.position, projectedPos);
            if (drift < stabilisationThreshold) return;

            // snap to projected position
            transform.position = projectedPos;
        }

        private void OnDisable()
        {
            if (_blinkCoroutine != null)
            {
                StopCoroutine(_blinkCoroutine);
                _blinkCoroutine = null;
            }

            if (_movementCoroutine != null)
            {
                StopCoroutine(_movementCoroutine);
                _movementCoroutine = null;
            }
        }

        /// <summary>
        ///     caches all renderer components in children
        /// </summary>
        private void CacheRenderers()
        {
            _renderers.Clear();
            _renderers.AddRange(GetComponentsInChildren<Renderer>(true));
        }

        /// <summary>
        ///     loads texture pair from Resources/NekoTextures for the given id
        /// </summary>
        private void LoadTextures(int id)
        {
            var idString = id.ToString("D2");
            var eyesOpenPath = $"NekoTextures/Tex_Neko_Body_{idString}";
            var eyesClosedPath = $"NekoTextures/Tex_Neko_Body_{idString}_eyeclose";

            _eyesOpenTexture = Resources.Load<Texture>(eyesOpenPath);
            _eyesClosedTexture = Resources.Load<Texture>(eyesClosedPath);

            if (!_eyesOpenTexture || !_eyesClosedTexture)
                Logkat.Warn($"AREntityNeko: failed to load textures for id {id}");
        }

        /// <summary>
        ///     applies a texture to all cached renderers
        /// </summary>
        private void ApplyTexture(Texture texture)
        {
            if (!texture) return;
            foreach (var rdr in _renderers)
                rdr.material.mainTexture = texture;
        }

        /// <summary>
        ///     changes the neko's texture to the specified id
        /// </summary>
        // ReSharper disable once MemberCanBePrivate.Global
        public void SetTextureId(int id)
        {
            if (id is < 0 or > 44) Logkat.Panic("AREntityNeko: textureId out of range (0-44)");
            textureId = id;
            LoadTextures(id);
            ApplyTexture(_eyesOpenTexture);
            Logkat.Out($"AREntityNeko: set texture to ID {id}");
        }

        /// <summary>
        ///     changes the neko's texture to a random id (0-44)
        /// </summary>
        private void RandomizeTextureId()
        {
            SetTextureId(Random.Range(0, 45));
        }

        /// <summary>
        ///     performs a single blink (eyes close then open)
        /// </summary>
        public void Blink()
        {
            if (_eyesClosedTexture && _eyesOpenTexture)
                StartCoroutine(BlinkOnce());
        }

        /// <summary>
        ///     coroutine for a single blink
        /// </summary>
        private IEnumerator BlinkOnce()
        {
            ApplyTexture(_eyesClosedTexture);
            yield return new WaitForSeconds(blinkDuration);
            ApplyTexture(_eyesOpenTexture);
        }

        /// <summary>
        ///     coroutine that blinks periodically while enabled
        /// </summary>
        private IEnumerator BlinkRoutine()
        {
            while (enableBlinking)
            {
                yield return new WaitForSeconds(blinkInterval);
                yield return BlinkOnce();
            }
        }

        /// <summary>
        ///     starts following mode (called by CoreGameplay when spawned on tracked image)
        /// </summary>
        public void StartFollowing()
        {
            _isFollowing = true;
            _isGrounded = false;
            // unparent so CoreGameplay can move us freely
            transform.SetParent(null);
            Logkat.Out("AREntityNeko: started following tracked image");
        }

        /// <summary>
        ///     stops following mode and triggers fall to nearest plane
        /// </summary>
        public void StopFollowing()
        {
            _isFollowing = false;
            Logkat.Out("AREntityNeko: stopped following");
        }

        /// <summary>
        ///     falls to the nearest detected plane with completion callback
        /// </summary>
        /// <param name="onComplete">callback invoked when fall completes</param>
        public void Fall(Action onComplete = null)
        {
            if (_movementCoroutine != null) StopCoroutine(_movementCoroutine);
            _movementCoroutine = StartCoroutine(FallRoutine(onComplete));
        }

        /// <summary>
        ///     walks toward a target position with choppy stop-motion style animation
        /// </summary>
        public void WalkTo(Vector3 targetPosition)
        {
            if (_movementCoroutine != null) StopCoroutine(_movementCoroutine);
            _movementCoroutine = StartCoroutine(WalkRoutine(targetPosition));
        }

        /// <summary>
        ///     performs a single jump in place
        /// </summary>
        public void Jump()
        {
            if (_movementCoroutine != null) StopCoroutine(_movementCoroutine);
            _movementCoroutine = StartCoroutine(JumpRoutine());
        }

        /// <summary>
        ///     coroutine that falls to the nearest plane
        /// </summary>
        /// <param name="onComplete">optional callback invoked when fall completes</param>
        private IEnumerator FallRoutine(Action onComplete)
        {
            // Logkat.Out($"AREntityNeko: [Debug] FallRoutine started, currentPos={transform.position}");

            var gameplay = CoreGameplay.instance;
            if (!gameplay || !gameplay.planes)
            {
                Logkat.Warn("AREntityNeko: no CoreGameplay instance, cannot fall properly");
                onComplete?.Invoke();
                yield break;
            }

            // project current position onto nearest plane surface
            Vector3 targetPosition;
            if (gameplay.planes.TryProjectToPlane(transform.position, out var projectedPos))
            {
                targetPosition = projectedPos;
                // Logkat.Out($"AREntityNeko: [Debug] projected to plane, targetPos={targetPosition}");
            }
            else
            {
                // fallback: no plane available, snap to y=0
                // Logkat.Warn("AREntityNeko: [Debug] no plane found, falling to y=0");
                targetPosition = transform.position;
                targetPosition.y = 0f;
            }

            // fall toward target position
            // Logkat.Out($"AREntityNeko: [Debug] falling from {transform.position} to {targetPosition}");
            while (Vector3.Distance(transform.position, targetPosition) > 0.01f)
            {
                var fallStep = fallSpeed * Time.deltaTime;
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, fallStep);
                yield return null;
            }

            // snap to exact target and mark as grounded
            transform.position = targetPosition;
            _isGrounded = true;

            // Logkat.Out($"AREntityNeko: [Debug] landed at {transform.position}");
            _movementCoroutine = null;
            onComplete?.Invoke();
        }

        /// <summary>
        ///     coroutine for choppy stop-motion walk animation
        /// </summary>
        private IEnumerator WalkRoutine(Vector3 targetPosition)
        {
            Logkat.Out("AREntityNeko: walking");

            var tiltLeft = true;

            while (Vector3.Distance(transform.position, targetPosition) > 0.1f)
            {
                // face the target
                var direction = (targetPosition - transform.position).normalized;
                direction.y = 0;

                if (direction.sqrMagnitude > 0.001f)
                {
                    var targetRotation = Quaternion.LookRotation(direction);

                    // add choppy tilt like a kid moving a lego figure
                    var tiltAngle = tiltLeft ? walkTiltAngle : -walkTiltAngle;
                    var tiltRotation = Quaternion.Euler(0, 0, tiltAngle);
                    transform.rotation = targetRotation * tiltRotation;
                }

                // move forward in discrete steps (choppy/stop-motion)
                var stepDistance = walkSpeed * walkStepDuration;
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, stepDistance);

                tiltLeft = !tiltLeft;
                yield return new WaitForSeconds(walkStepDuration);
            }

            // reset rotation to upright
            var finalRotation = transform.eulerAngles;
            finalRotation.z = 0;
            transform.eulerAngles = finalRotation;

            Logkat.Out("AREntityNeko: finished walking");
            _movementCoroutine = null;
        }

        /// <summary>
        ///     coroutine for a single jump with sine curve
        /// </summary>
        private IEnumerator JumpRoutine()
        {
            Logkat.Out("AREntityNeko: jumping");

            var startPos = transform.position;
            var elapsed = 0f;

            while (elapsed < jumpDuration)
            {
                elapsed += Time.deltaTime;
                var t = elapsed / jumpDuration;

                // sine curve for smooth up-down arc
                var heightOffset = Mathf.Sin(t * Mathf.PI) * jumpHeight;
                transform.position = startPos + Vector3.up * heightOffset;

                yield return null;
            }

            // ensure we land exactly where we started
            transform.position = startPos;

            Logkat.Out("AREntityNeko: finished jumping");
            _movementCoroutine = null;
        }
    }
}