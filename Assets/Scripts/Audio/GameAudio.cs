using FlappyBird.Core;
using FlappyBird.Player;
using UnityEngine;

namespace FlappyBird.Audio
{
    /// <summary>
    /// Plays the game's sound effects in response to events raised elsewhere.
    ///
    /// Audio listens; it never drives. Nothing in the gameplay code knows this
    /// component exists, so sound can be muted or removed entirely without
    /// touching the rules.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class GameAudio : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("The bird to listen to for flaps.")]
        [SerializeField] private BirdController bird;

        [Header("Clips")]
        [SerializeField] private AudioClip flapClip;
        [SerializeField] private AudioClip scoreClip;
        [SerializeField] private AudioClip hitClip;
        [SerializeField] private AudioClip dieClip;

        [Header("Timing")]
        [Tooltip("Delay between the impact sound and the falling sound, in seconds.")]
        [SerializeField] private float dieDelay = 0.28f;

        private AudioSource source;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
        }

        // Start rather than OnEnable, for the same ordering reason as every
        // other subscriber: the manager may not exist yet during OnEnable.
        private void Start()
        {
            GameManager game = GameManager.Instance;
            if (game != null)
            {
                game.ScoreChanged += HandleScoreChanged;
                game.StateChanged += HandleStateChanged;
            }

            if (bird != null)
            {
                bird.Flapped += PlayFlap;
            }
        }

        private void OnDestroy()
        {
            GameManager game = GameManager.Instance;
            if (game != null)
            {
                game.ScoreChanged -= HandleScoreChanged;
                game.StateChanged -= HandleStateChanged;
            }

            if (bird != null)
            {
                bird.Flapped -= PlayFlap;
            }
        }

        private void PlayFlap()
        {
            Play(flapClip);
        }

        private void HandleScoreChanged(int score)
        {
            // ScoreChanged also fires on the reset to zero at the start of a
            // run, which should be silent.
            if (score > 0)
            {
                Play(scoreClip);
            }
        }

        private void HandleStateChanged(GameState state)
        {
            if (state != GameState.GameOver)
            {
                return;
            }

            Play(hitClip);

            // The falling sound follows the impact rather than overlapping it.
            if (dieClip != null)
            {
                Invoke(nameof(PlayDie), dieDelay);
            }
        }

        private void PlayDie()
        {
            Play(dieClip);
        }

        private void Play(AudioClip clip)
        {
            if (clip != null)
            {
                // PlayOneShot so overlapping effects do not cut each other off.
                source.PlayOneShot(clip);
            }
        }
    }
}
