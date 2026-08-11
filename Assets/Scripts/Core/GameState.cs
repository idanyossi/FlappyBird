namespace FlappyBird.Core
{
    /// <summary>
    /// The three phases a run moves through.
    /// </summary>
    public enum GameState
    {
        /// <summary>Waiting for the first flap. The bird hovers and pipes are idle.</summary>
        Ready,

        /// <summary>The run is live: gravity applies, pipes scroll, score accumulates.</summary>
        Playing,

        /// <summary>The bird has hit something. Input restarts the run.</summary>
        GameOver
    }
}
