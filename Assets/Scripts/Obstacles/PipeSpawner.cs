using System.Collections.Generic;
using FlappyBird.Core;
using UnityEngine;

namespace FlappyBird.Obstacles
{
    /// <summary>
    /// Produces the endless stream of pipes.
    ///
    /// Pipes are pooled rather than created and destroyed, so a long run does not
    /// generate a steady trickle of garbage and the resulting collection hitches.
    /// </summary>
    public sealed class PipeSpawner : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField] private Pipe pipePrefab;

        [Header("Timing")]
        [Tooltip("Seconds between pipe pairs.")]
        [SerializeField] private float spawnInterval = 1.5f;

        [Tooltip("Leftward speed of every pipe, in units per second.")]
        [SerializeField] private float scrollSpeed = 3.5f;

        [Header("Placement")]
        [Tooltip("World X where pipes appear, just beyond the right edge.")]
        [SerializeField] private float spawnX = 11f;

        [Tooltip("World X where pipes are recycled, just beyond the left edge.")]
        [SerializeField] private float despawnX = -13f;

        [Tooltip("Vertical clearance the bird must fly through.")]
        [SerializeField] private float gapHeight = 3.4f;

        [Tooltip("Lowest world Y the gap centre may take.")]
        [SerializeField] private float minGapCentreY = -1.8f;

        [Tooltip("Highest world Y the gap centre may take.")]
        [SerializeField] private float maxGapCentreY = 2.2f;

        private readonly Queue<Pipe> pool = new Queue<Pipe>();
        private readonly List<Pipe> active = new List<Pipe>();

        private float timeUntilNextSpawn;
        private bool spawning;

        // See the note in BirdController: subscribing in OnEnable races with
        // the manager's Awake, and losing that race meant no pipes ever spawned.
        private void Start()
        {
            GameManager game = GameManager.Instance;
            if (game == null)
            {
                Debug.LogError($"No {nameof(GameManager)} in the scene; pipes will not spawn.", this);
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
            if (!spawning)
            {
                return;
            }

            timeUntilNextSpawn -= Time.deltaTime;
            if (timeUntilNextSpawn > 0f)
            {
                return;
            }

            timeUntilNextSpawn = spawnInterval;
            SpawnPipe();
        }

        private void SpawnPipe()
        {
            Pipe pipe = Rent();
            float gapCentreY = Random.Range(minGapCentreY, maxGapCentreY);

            pipe.Launch(new Vector2(spawnX, gapCentreY), gapHeight, scrollSpeed, despawnX);
            active.Add(pipe);
        }

        private Pipe Rent()
        {
            Pipe pipe = pool.Count > 0 ? pool.Dequeue() : Instantiate(pipePrefab, transform);

            pipe.ExitedPlayfield += Recycle;
            pipe.gameObject.SetActive(true);
            return pipe;
        }

        private void Recycle(Pipe pipe)
        {
            // Unsubscribing here — rather than in the pipe — keeps the handler
            // from being attached twice when the same instance is rented again.
            pipe.ExitedPlayfield -= Recycle;
            pipe.gameObject.SetActive(false);

            active.Remove(pipe);
            pool.Enqueue(pipe);
        }

        /// <summary>
        /// Returns every live pipe to the pool, used when a run restarts.
        /// </summary>
        private void ClearPlayfield()
        {
            // Iterate a copy: Recycle mutates the active list as it goes.
            foreach (Pipe pipe in new List<Pipe>(active))
            {
                Recycle(pipe);
            }
        }

        private void HaltAll()
        {
            foreach (Pipe pipe in active)
            {
                pipe.Halt();
            }
        }

        private void HandleStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.Ready:
                    spawning = false;
                    ClearPlayfield();
                    break;

                case GameState.Playing:
                    spawning = true;
                    // Give the player a moment of clear sky before the first pipe.
                    timeUntilNextSpawn = spawnInterval;
                    break;

                case GameState.GameOver:
                    spawning = false;
                    HaltAll();
                    break;
            }
        }
    }
}
