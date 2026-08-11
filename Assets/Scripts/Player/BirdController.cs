using FlappyBird.Core;
using FlappyBird.InputHandling;
using FlappyBird.Obstacles;
using UnityEngine;

namespace FlappyBird.Player
{
    /// <summary>
    /// Drives the bird: flapping, gravity, nose-tilt, death and reset.
    ///
    /// Input is sampled in Update (so a press between physics steps is never
    /// dropped) and applied in FixedUpdate (so the resulting motion is
    /// frame-rate independent).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class BirdController : MonoBehaviour
    {
        [Header("Flight")]
        [Tooltip("Upward speed applied instantly on each flap, in units per second.")]
        [SerializeField] private float flapSpeed = 7f;

        [Tooltip("Multiplier on world gravity while the run is live.")]
        [SerializeField] private float gravityScale = 2.6f;

        [Tooltip("Bird cannot rise past this world Y, so it can never leave the playfield.")]
        [SerializeField] private float ceilingY = 5.5f;

        [Header("Tilt")]
        [Tooltip("Nose-up angle when rising, in degrees.")]
        [SerializeField] private float maxUpAngle = 25f;

        [Tooltip("Nose-down angle when falling, in degrees.")]
        [SerializeField] private float maxDownAngle = -85f;

        [Tooltip("Vertical speed at which the bird reaches its full nose-down angle.")]
        [SerializeField] private float tiltReferenceSpeed = 8f;

        [Tooltip("How quickly the sprite rotates toward its target angle.")]
        [SerializeField] private float tiltSharpness = 10f;

        [Header("Idle bob")]
        [Tooltip("Height of the hover animation shown before the run starts.")]
        [SerializeField] private float bobAmplitude = 0.25f;

        [Tooltip("Speed of the hover animation shown before the run starts.")]
        [SerializeField] private float bobFrequency = 2.5f;

        /// <summary>Raised the moment a flap is actually applied, for audio and effects.</summary>
        public event System.Action Flapped;

        private Rigidbody2D body;
        private Vector2 startPosition;
        private Quaternion startRotation;

        /// <summary>Set in Update, consumed by the next FixedUpdate.</summary>
        private bool flapQueued;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            startPosition = body.position;
            startRotation = transform.rotation;
        }

        // Subscribing in Start rather than OnEnable: Unity guarantees every
        // Awake has run before any Start, but it does NOT guarantee the
        // manager's Awake beats another object's OnEnable. Wiring up in
        // OnEnable silently did nothing whenever the ordering went the other
        // way, leaving the bird frozen and the pipes never spawning.
        private void Start()
        {
            GameManager game = GameManager.Instance;
            if (game == null)
            {
                Debug.LogError($"No {nameof(GameManager)} in the scene; the bird cannot run.", this);
                return;
            }

            game.StateChanged += HandleStateChanged;
            HandleStateChanged(game.State);
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.StateChanged -= HandleStateChanged;
            }
        }

        private void Update()
        {
            GameManager game = GameManager.Instance;
            if (game == null)
            {
                return;
            }

            bool pressed = FlapInput.FlapPressedThisFrame();

            switch (game.State)
            {
                case GameState.Ready:
                    AnimateIdleBob();
                    if (pressed)
                    {
                        // The press that starts the run is also the first flap,
                        // so the bird never stalls for a frame at the whistle.
                        game.BeginRun();
                        flapQueued = true;
                    }
                    break;

                case GameState.Playing:
                    if (pressed)
                    {
                        flapQueued = true;
                    }
                    break;

                case GameState.GameOver:
                    if (pressed)
                    {
                        game.RestartRun();
                    }
                    break;
            }
        }

        private void FixedUpdate()
        {
            GameManager game = GameManager.Instance;
            if (game == null || game.State != GameState.Playing)
            {
                return;
            }

            if (flapQueued)
            {
                // Assigning velocity rather than adding force makes every flap
                // feel identical regardless of how fast the bird was falling.
                body.linearVelocity = new Vector2(body.linearVelocity.x, flapSpeed);
                flapQueued = false;
                Flapped?.Invoke();
            }

            ClampToCeiling();
            ApplyTilt();
        }

        /// <summary>
        /// Stops the bird leaving the top of the screen without killing the player,
        /// matching the original game's forgiving ceiling.
        /// </summary>
        private void ClampToCeiling()
        {
            if (body.position.y <= ceilingY)
            {
                return;
            }

            body.position = new Vector2(body.position.x, ceilingY);

            if (body.linearVelocity.y > 0f)
            {
                body.linearVelocity = new Vector2(body.linearVelocity.x, 0f);
            }
        }

        /// <summary>
        /// Points the bird along its travel: nose up while climbing, tipping
        /// steadily downward the faster it falls.
        /// </summary>
        private void ApplyTilt()
        {
            float fallRatio = Mathf.Clamp(body.linearVelocity.y / tiltReferenceSpeed, -1f, 1f);
            float targetAngle = fallRatio >= 0f
                ? Mathf.Lerp(0f, maxUpAngle, fallRatio)
                : Mathf.Lerp(0f, maxDownAngle, -fallRatio);

            float smoothed = Mathf.LerpAngle(
                body.rotation,
                targetAngle,
                1f - Mathf.Exp(-tiltSharpness * Time.fixedDeltaTime));

            body.MoveRotation(smoothed);
        }

        /// <summary>
        /// Gentle hover while waiting for the first press, so the title screen is not static.
        /// </summary>
        private void AnimateIdleBob()
        {
            float offset = Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
            body.position = new Vector2(startPosition.x, startPosition.y + offset);
        }

        private void HandleStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.Ready:
                    ResetToStart();
                    break;

                case GameState.Playing:
                    body.bodyType = RigidbodyType2D.Dynamic;
                    body.gravityScale = gravityScale;
                    break;

                case GameState.GameOver:
                    // Keep gravity on so the bird visibly drops after dying,
                    // but stop responding to input.
                    flapQueued = false;
                    break;
            }
        }

        private void ResetToStart()
        {
            flapQueued = false;

            // Kinematic while waiting keeps the bird exactly where the bob puts it
            // and stops gravity accumulating before the run begins.
            body.bodyType = RigidbodyType2D.Kinematic;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.gravityScale = 0f;
            body.position = startPosition;
            transform.rotation = startRotation;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.EndRun();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent(out ScoreZone _) && GameManager.Instance != null)
            {
                GameManager.Instance.AddPoint();
            }
        }
    }
}
