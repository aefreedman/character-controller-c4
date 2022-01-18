using UnityEditor;
using UnityEngine;

namespace SecretCrush
{
    [CustomEditor(typeof(CharacterController))]
    public class CharacterControllerEditor : Editor
    {
        private CharacterController _controller;
        private Vector3 _pos;

        private void OnSceneGUI()
        {
            _controller = target as CharacterController;
            if (_controller == null)
                return;
        
            _pos = _controller.transform.position;
        
            if (Event.current.type != EventType.Repaint)
                return;
        
            DrawMoveDirection();
            DrawGroundRaycast();
        }
        
        private void DrawMoveDirection()
        {
            var vel = _controller.GetVelocity();
            if (vel.magnitude <= Mathf.Epsilon)
                return;
            Handles.color = Color.gray;
            Handles.ArrowHandleCap(
                0,
                _controller.transform.position,
                Quaternion.LookRotation(vel),
                HandleUtility.GetHandleSize(_pos),
                EventType.Repaint
            );
        }

        private void DrawGroundRaycast()
        {
            Handles.color = _controller.OnGround ? Color.red : Color.green;
        
            // raw raycast debug
            Handles.DrawLine(_pos + Vector3.up * 0.05f, _pos + Vector3.down * _controller.GetProbeDistance);
            // ground hit position
            // Handles.SphereHandleCap(0, _pos + Vector3.down * _controller.GroundDistance, Quaternion.identity, 0.05f, EventType.Repaint);
        }

        // private void DrawSpherecast()
        // {
        //     Handles.color = new Color(1f, 0, 0, 0.5f);
        //     // Handles.SphereHandleCap(0, pos + Vector3.down * (_tankControl.CurrentGroundCheckDistance - _tankControl.GroundCheckOffsetDistance - _tankControl.ColliderRadius),
        //     // Quaternion.identity, _tankControl.ColliderRadius * 2f, EventType.Repaint);
        //     // Handles.DrawLine(pos, pos + Vector3.down);
        // }
    }
}