using FlappyBird.Core;
using TMPro;
using UnityEngine;

namespace FlappyBird.UI
{
    /// <summary>
    /// Binds the on-screen text and panels to the game's events.
    ///
    /// The HUD only reads from <see cref="GameManager"/>; it never drives the
    /// game, so the rules stay in one place and the UI can be restyled freely.
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        [Header("Score")]
        [SerializeField] private TMP_Text scoreLabel;

        [Header("Panels")]
        [Tooltip("Shown before the first flap.")]
        [SerializeField] private GameObject readyPanel;

        [Tooltip("Shown after the bird dies.")]
        [SerializeField] private GameObject gameOverPanel;

        [Header("Game over details")]
        [SerializeField] private TMP_Text finalScoreLabel;
        [SerializeField] private TMP_Text bestScoreLabel;

        private void OnEnable()
        {
            GameManager game = GameManager.Instance;
            if (game == null)
            {
                return;
            }

            game.StateChanged += HandleStateChanged;
            game.ScoreChanged += HandleScoreChanged;

            // Adopt the current values immediately, in case this component was
            // enabled after the manager had already broadcast.
            HandleStateChanged(game.State);
            HandleScoreChanged(game.Score);
        }

        private void OnDisable()
        {
            GameManager game = GameManager.Instance;
            if (game == null)
            {
                return;
            }

            game.StateChanged -= HandleStateChanged;
            game.ScoreChanged -= HandleScoreChanged;
        }

        private void HandleScoreChanged(int score)
        {
            if (scoreLabel != null)
            {
                scoreLabel.text = score.ToString();
            }
        }

        private void HandleStateChanged(GameState state)
        {
            SetActiveSafely(readyPanel, state == GameState.Ready);
            SetActiveSafely(gameOverPanel, state == GameState.GameOver);

            // The running score is redundant once the game-over panel reports it.
            SetActiveSafely(scoreLabel != null ? scoreLabel.gameObject : null,
                            state != GameState.GameOver);

            if (state == GameState.GameOver)
            {
                ShowFinalScores();
            }
        }

        private void ShowFinalScores()
        {
            GameManager game = GameManager.Instance;
            if (game == null)
            {
                return;
            }

            if (finalScoreLabel != null)
            {
                finalScoreLabel.text = game.Score.ToString();
            }

            if (bestScoreLabel != null)
            {
                bestScoreLabel.text = game.BestScore.ToString();
            }
        }

        private static void SetActiveSafely(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }
    }
}
