using UnityEngine;

namespace SecretCrush
{
    public interface ICharacterController
    {
        void UpdateState();
        void ClearState();
        Vector3 GetGravity(out Vector3 upAxis);
    }
}