namespace SecretCrush.InputModules
{
    /// <summary>
    /// AxisMode describes whether an input axis outputs Relative values (0, 1) or absolute values (0, infinity)
    /// </summary>
    public static class AxisMode
    {
        public enum Value
        {
            Relative,
            Absolute
        }
    }

    /// <summary>
    /// This interface describes the required input data and queries for a basic player character controller for a generic first or third person game.
    /// Input actions related to locomotion (sprint, crouch, jump) have required methods to allow for implementing classes to implement locomotion logic without
    /// dependency on the source of the input (Unity Engine, ReWired, InControl, etc.)
    ///
    /// Other input values related to non-locomotive abilities ("pick up", "interact") should be the responsibility of some other state machine or controller.
    /// </summary>
    public interface IPlayerInputModule
    {
        /// <summary>
        /// This method should query the required input device manager and conform the data as required. This should be called only once per frame by the controller
        /// instance it is for. However, calling it multiple times per frame should be safe.
        /// </summary>
        void ManagedUpdate();
        
        /// <summary>
        /// Returns the input axis data struct so that any other class can perform logic based on the same input that the player is using on that frame.
        /// </summary>
        /// <returns></returns>
        InputAxesData GetInputAxesData();

        /// <summary>
        /// Clears the state of the input axes data.
        /// </summary>
        void ClearInputAxes();

        /// <summary>
        /// Returns the current axis mode. This should be Relative for joystick-like devices, and absolute for mouse-like devices.
        /// </summary>
        /// <returns></returns>
        AxisMode.Value GetAxisMode();

        bool IsSprinting();
        bool IsCrouching();

        bool IsJumpQueued();

        bool IsReady();

        void SetBlockInput(bool value);
    }
}