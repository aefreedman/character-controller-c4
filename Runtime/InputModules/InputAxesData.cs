using System;
using UnityEngine;

namespace SecretCrush.InputModules
{
    [Serializable]
    public struct InputAxesData
    {
        /// <summary>
        /// This should be the move vector as reported by an input device
        /// </summary>
        public Vector2 InputMoveAxis;
        
        /// <summary>
        /// This should be the move vector after any modification by an input manager
        /// </summary>
        public Vector2 InputMoveAdjusted;
        
        /// <summary>
        /// This should be the aim vector as reported by the input device
        /// </summary>
        public Vector2 InputAimAxis;
        
        /// <summary>
        /// This should be the aim vector after any modification by an input manager
        /// </summary>
        public Vector2 InputAimAdjusted;
    }
}