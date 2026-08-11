using UnityEngine.InputSystem;

namespace FlappyBird.InputHandling
{
    /// <summary>
    /// Single place that answers "did the player ask to flap this frame?".
    ///
    /// The project runs the Input System package exclusively (activeInputHandler = 1),
    /// so the legacy UnityEngine.Input API is unavailable and would throw at runtime.
    /// Each device is null-checked because a device that is not present on the current
    /// platform reports null rather than an inert object.
    /// </summary>
    public static class FlapInput
    {
        /// <summary>
        /// True on the single frame the player pressed space, clicked, or tapped.
        /// </summary>
        public static bool FlapPressedThisFrame()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                return true;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                return true;
            }

            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
            {
                return true;
            }

            return false;
        }
    }
}
