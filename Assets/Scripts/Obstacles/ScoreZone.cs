using UnityEngine;

namespace FlappyBird.Obstacles
{
    /// <summary>
    /// Marker for the invisible trigger sitting in a pipe gap. The bird looks for
    /// this component rather than a tag string, so scoring cannot silently break
    /// from a typo or a tag that was never added to the project.
    /// </summary>
    public sealed class ScoreZone : MonoBehaviour
    {
    }
}
