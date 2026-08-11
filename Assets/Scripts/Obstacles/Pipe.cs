using UnityEngine;

namespace FlappyBird.Obstacles
{
    /// <summary>
    /// One pipe pair and the scoring gap between them.
    ///
    /// A pipe knows how to position and move itself, but not when to exist —
    /// the spawner owns its lifetime and recycles it through a pool.
    /// </summary>
    public sealed class Pipe : MonoBehaviour
    {
        [Tooltip("Upper pipe, moved to sit above the gap.")]
        [SerializeField] private Transform topPipe;

        [Tooltip("Lower pipe, moved to sit below the gap.")]
        [SerializeField] private Transform bottomPipe;

        private float scrollSpeed;
        private float despawnX;

        /// <summary>Raised when the pipe has scrolled off-screen and wants recycling.</summary>
        public event System.Action<Pipe> ExitedPlayfield;

        /// <summary>
        /// Places the pair around a gap and starts it moving.
        /// </summary>
        /// <param name="spawnPosition">World position of the pair's centre.</param>
        /// <param name="gapHeight">Vertical clearance between the two pipes.</param>
        /// <param name="speed">Leftward speed in units per second.</param>
        /// <param name="despawnThresholdX">World X at which the pipe is recycled.</param>
        public void Launch(Vector2 spawnPosition, float gapHeight, float speed, float despawnThresholdX)
        {
            transform.position = spawnPosition;
            scrollSpeed = speed;
            despawnX = despawnThresholdX;

            // Each pipe sits half a gap away from the centre, so the opening is
            // always exactly gapHeight tall no matter where the pair is placed.
            float halfGap = gapHeight * 0.5f;
            topPipe.localPosition = new Vector3(0f, halfGap, 0f);
            bottomPipe.localPosition = new Vector3(0f, -halfGap, 0f);
        }

        private void Update()
        {
            transform.position += Vector3.left * (scrollSpeed * Time.deltaTime);

            if (transform.position.x <= despawnX)
            {
                ExitedPlayfield?.Invoke(this);
            }
        }

        /// <summary>
        /// Freezes the pair in place, used while the game is not running.
        /// </summary>
        public void Halt()
        {
            scrollSpeed = 0f;
        }
    }
}
