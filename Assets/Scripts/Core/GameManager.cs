using System;
using UnityEngine;

namespace FlappyBird.Core
{
    /// <summary>
    /// Owns the run's state machine and score. Everything else in the game
    /// reacts to the events raised here rather than polling or reaching into
    /// each other, which keeps the bird, the pipes and the HUD independent.
    ///
    /// A restart resets objects in place instead of reloading the scene. That
    /// avoids a reload stall and the whole class of "stale reference after
    /// scene load" bugs.
    /// </summary>
    /// <remarks>
    /// Runs early so <see cref="Instance"/> is assigned before anything else
    /// starts looking for it.
    /// </remarks>
    [DefaultExecutionOrder(-100)]
    public sealed class GameManager : MonoBehaviour
    {
        private const string BestScoreKey = "FlappyBird.BestScore";

        public static GameManager Instance { get; private set; }

        /// <summary>Raised whenever the run moves between phases.</summary>
        public event Action<GameState> StateChanged;

        /// <summary>Raised when the score changes, including the reset to zero.</summary>
        public event Action<int> ScoreChanged;

        public GameState State { get; private set; } = GameState.Ready;

        public int Score { get; private set; }

        public int BestScore { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"A second {nameof(GameManager)} was found and destroyed.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            BestScore = PlayerPrefs.GetInt(BestScoreKey, 0);
        }

        private void Start()
        {
            // Broadcast once on the first frame so listeners that subscribed in
            // OnEnable receive the opening state instead of missing it.
            EnterState(GameState.Ready);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Called by the bird on its first flap. Ignored unless the run is waiting to begin.
        /// </summary>
        public void BeginRun()
        {
            if (State != GameState.Ready)
            {
                return;
            }

            EnterState(GameState.Playing);
        }

        /// <summary>
        /// Called by the bird when it collides with a pipe or the ground.
        /// </summary>
        public void EndRun()
        {
            if (State != GameState.Playing)
            {
                return;
            }

            if (Score > BestScore)
            {
                BestScore = Score;
                PlayerPrefs.SetInt(BestScoreKey, BestScore);
                PlayerPrefs.Save();
            }

            EnterState(GameState.GameOver);
        }

        /// <summary>
        /// Clears the score and returns to the waiting state. Listeners restore
        /// their own starting condition in response to the state change.
        /// </summary>
        public void RestartRun()
        {
            if (State != GameState.GameOver)
            {
                return;
            }

            SetScore(0);
            EnterState(GameState.Ready);
        }

        /// <summary>
        /// Called once per pipe cleared. Only counts while the run is live so a
        /// score zone brushed during the death animation cannot inflate the total.
        /// </summary>
        public void AddPoint()
        {
            if (State != GameState.Playing)
            {
                return;
            }

            SetScore(Score + 1);
        }

        private void SetScore(int value)
        {
            Score = value;
            ScoreChanged?.Invoke(Score);
        }

        private void EnterState(GameState next)
        {
            State = next;
            StateChanged?.Invoke(next);
        }
    }
}
