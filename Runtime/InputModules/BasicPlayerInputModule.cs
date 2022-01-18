using UnityEngine;

namespace SecretCrush.InputModules
{
    /// <summary>
    /// Basic implementation of an input module for the C4 using default Unity Input system values.
    /// </summary>
    public class BasicPlayerInputModule : MonoBehaviour, IPlayerInputModule
    {
        private InputAxesData _data;
        public void ManagedUpdate()
        {
            _data = new InputAxesData();
            _data.InputMoveAxis.x = Input.GetAxis("Horizontal");
            _data.InputMoveAxis.y = Input.GetAxis("Vertical");

            // Can do whatever adjustment you want to the input values here
            // This is intended for things like deadzones and curves; velocity adjustment should be in the character controller
            _data.InputMoveAdjusted.x = _data.InputMoveAxis.x;
            _data.InputMoveAdjusted.y = _data.InputMoveAxis.y;
        }

        public InputAxesData GetInputAxesData() => _data;

        public void ClearInputAxes()
        {
            _data = new InputAxesData();
        }

        public AxisMode.Value GetAxisMode() => AxisMode.Value.Relative;

        public bool IsSprinting() => false;

        public bool IsCrouching() => false;

        public bool IsJumpQueued() => Input.GetButton("Jump");

        public bool IsReady() => true;

        public void SetBlockInput(bool value) { }
    }
}