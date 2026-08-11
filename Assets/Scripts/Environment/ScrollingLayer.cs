using FlappyBird.Core;
using UnityEngine;

namespace FlappyBird.Environment
{
    /// <summary>
    /// Slides a repeating strip (ground or backdrop) leftward and wraps it, so a
    /// fixed-size sprite reads as endless terrain.
    ///
    /// The wrap uses a modulo of the tile width rather than a hard reset, which
    /// keeps the seam exact even if a long frame overshoots the boundary.
    /// </summary>
    public sealed class ScrollingLayer : MonoBehaviour
    {
        [Tooltip("Leftward speed in units per second. Use a smaller value than the " +
                 "pipes for a parallax backdrop.")]
        [SerializeField] private float scrollSpeed = 3.5f;

        [Tooltip("Width of one tile. The strip must be at least two tiles wide so " +
                 "the wrap is never visible.")]
        [SerializeField] private float tileWidth = 10f;

        [Tooltip("Keep scrolling after the bird dies. The original game freezes " +
                 "the ground on impact.")]
        [SerializeField] private bool scrollWhileGameOver;

        private Vector3 startPosition;
        private float distanceScrolled;

        private void Awake()
        {
            startPosition = transform.position;
        }

        // See the note in BirdController: OnEnable can run before the manager's
        // Awake, in which case the subscription was silently skipped.
        private void Start()
        {
            GameManager game = GameManager.Instance;
            if (game == null)
            {
                return;
            }

            game.StateChanged += HandleStateChanged;
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
            if (!ShouldScroll())
            {
                return;
            }

            distanceScrolled = Mathf.Repeat(
                distanceScrolled + scrollSpeed * Time.deltaTime,
                tileWidth);

            transform.position = startPosition + Vector3.left * distanceScrolled;
        }

        private bool ShouldScroll()
        {
            GameManager game = GameManager.Instance;
            if (game == null)
            {
                return false;
            }

            return game.State switch
            {
                // Scrolling on the title screen sells the idea that the world is
                // already moving and the bird is simply hovering in place.
                GameState.Ready => true,
                GameState.Playing => true,
                GameState.GameOver => scrollWhileGameOver,
                _ => false
            };
        }

        private void HandleStateChanged(GameState state)
        {
            if (state == GameState.Ready)
            {
                distanceScrolled = 0f;
                transform.position = startPosition;
            }
        }
    }
}
