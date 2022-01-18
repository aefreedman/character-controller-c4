namespace SecretCrush.Modules
{
    public interface ICrouchModule
    {
        bool IsCrouching();

        /// <summary>
        /// Sets crouch state to the provided state and moves the camera target position
        /// </summary>
        /// <param name="toState"></param>
        void SetCrouch(bool toState);
    }
}