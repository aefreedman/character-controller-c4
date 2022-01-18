// Based on Jasper Flick's (catlikecoding.com) character controller with a bunch of modifications that probably make it worse
// He uses the MIT-0 No Attribution license but he's a cool cat so credit where credit is due

using System;
using System.Collections;
using System.Collections.Generic;
using SecretCrush.InputModules;
using UnityEditor;
using UnityEngine;

namespace SecretCrush
{
    /// <summary>
    /// This is the part of the character controller code that doesn't know anything about game logic
    /// </summary>
    [RequireComponent(typeof(IPlayerInputModule))]
    public class CharacterController : MonoBehaviour, ICharacterController
    {
        protected IPlayerInputModule PlayerInputModule { get; private set; }

        #region Control Parameters

        [Header("Control Parameters")]
        //These parameters might be overwritten at runtime by game code. Check the game's implementation notes for more details.
        [SerializeField, Tooltip("Player input will be relative to this transform")]
        protected Transform PlayerInputSpace;

        /* Implementation notes:
         *  If max accel is too high, you can 'spiderman' up inclined walls if you touch a wall and then push into it. I haven't found an elegant solution to this.
         *  This might be a combined effect with the ground snapping, and also a result of using MoveTowards to adjust the velocity?
         *  You can avoid the issue in 'slow' games by using less accel and snapping. Snapping should be < max accel, and accel should be <= max speed * 10
         */

        [SerializeField, Range(0, 100f)]
        protected float MaxAccel = 10f;

        [SerializeField, Range(0, 100f)]
        private float MaxAirAccel = 10f;

        [SerializeField, Range(0, 100f)]
        protected float MaxSpeedForward, MaxSpeedBackward, MaxSpeedStrafe = 1f;

        [SerializeField, Range(0, 10f)]
        private float JumpHeight = 1f;

        [SerializeField, Range(0, 90f)]
        private float MaxGroundAngle = 25f, MaxStairsAngle = 50f;

        [SerializeField, Range(0, 100f)]
        private float MaxSnapSpeed = 100f;

        [SerializeField, Min(0f), Tooltip("For checking the distance to the ground for ground snapping.")]
        // Help("Distance from center of transform. Make sure this is large enough to hit the ground, at least.")
        private float ProbeDistance;

        [SerializeField]
        // [ReadOnly]
        protected bool DisableGravity;

        [SerializeField]
         // Help(
         //     "<b>Probe Mask</b>\nThe probe mask should include the stairs layer(s).\nMake sure the Probe and Stairs masks are not set to the same value.<b>Make sure the player collider isn't included in this mask.</b>\nColliders not in this mask will not count toward collision counts that modify behavior."
         // )
        private LayerMask ProbeMask = -1;

        [SerializeField]
        // Help("The stairs mask should be any layer combination that you want to use the Stairs Angle value against.")
        private LayerMask StairsMask = -1;

        [SerializeField, Range(0, 5)]
        private int MaxAirJumps;

        [SerializeField]
        private bool UseJumpBias;

        #endregion

        #region Accessors

        public float GetProbeDistance => ProbeDistance;
        public bool OnGround => _groundContactCount > 0;
        public bool OnSteep => _steepContactCount > 0;

        #endregion

        #region Internal State

        private float _minGroundDotProduct, _minStairsDotProduct;
        private bool _jumpQueued;
        private int _jumpPhase;
        private int _stepsSinceLastGrounded, _stepsSinceLastJump;

        [SerializeField]
        // [ReadOnly]
        private int _groundContactCount, _steepContactCount;

        [SerializeField]
        // [ReadOnly]
        protected Vector3 _velocity, _desiredVelocity, _contactNormal, _adjustedContactNormal, _steepNormal, _upAxis, _rightAxis, _forwardAxis;

        protected bool BlockInput;
        public bool IsInputBlocked() => BlockInput;
        public void SetBlockInput(bool value, string debugMessage = "")
        {
            var desc = value ? "<color=red>ignore</color>" : "<color=green>allow</color>";
            Debug.Log($"Player Controller has been told to {desc} inputs. {debugMessage}");
            BlockInput = value;
            PlayerInputModule.SetBlockInput(value);
        }

        #endregion

        #region References

        private Rigidbody _body;

        [SerializeField]
        protected Renderer _debugRenderer;

        // ReSharper disable once NotAccessedField.Global
        protected Transform _transform;

        // ReSharper disable once NotAccessedField.Global
        protected Collider _collider;

        #endregion

        #region Debug Helpers

        [Header("Debug Helpers")]
        private bool _hasDebugRenderer;

        private static readonly int BaseColor = Shader.PropertyToID("_Color"); // if using HDRender Pipeline this is 'BaseColor'

        public enum ColorSetting
        {
            None,
            ContactCount,
            Airborne
        }

        public ColorSetting ColorMode;
        private RaycastHit _hit;
        public List<ContactPoint> ContactPoints;
        public List<string> GroundContactNamesList;
        public List<Vector3> GroundContactPoint;
        public List<Vector3> GroundContactNormal;

        #endregion


        private void OnValidate()
        {
            _minGroundDotProduct = Mathf.Cos(MaxGroundAngle * Mathf.Deg2Rad);
            _minStairsDotProduct = Mathf.Cos(MaxStairsAngle * Mathf.Deg2Rad);

            if (StairsMask == ProbeMask)
            {
                Debug.LogWarning("The probe and stairs masks are set to the same value! This can lead to unexpected behavior!");
            }
        }

        protected virtual void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _transform = transform;
            _body.useGravity = false;
            _hasDebugRenderer = _debugRenderer != null;
            _collider = GetComponent<Collider>();
            GroundContactNamesList = new List<string>();
            GroundContactPoint = new List<Vector3>();
            GroundContactNormal = new List<Vector3>();
            ContactPoints = new List<ContactPoint>();
#if !UNITY_EDITOR
            // Make sure we don't leave the debug renderer on in builds
            if (_hasDebugRenderer)
            {
                _debugRenderer = null;
                _hasDebugRenderer = false;
            }
#endif
            OnValidate();
        }

        protected virtual IEnumerator Start()
        {
            PlayerInputModule = GetComponent<IPlayerInputModule>();
            if (PlayerInputModule == null)
                Debug.LogError("The Character Controller is missing an input module component!");
            yield return null;
        }

        protected virtual void OnDisable()
        {
            _body.velocity = Vector3.zero;
            _velocity = Vector3.zero;
            PlayerInputModule.ClearInputAxes();
        }

        protected virtual void Update()
        {
            if (!PlayerInputModule.IsReady())
                return;

            var inputAxesData = new InputAxesData();
            if (!BlockInput)
            {
                PlayerInputModule.ManagedUpdate();
                inputAxesData = PlayerInputModule.GetInputAxesData();
            }

            if (PlayerInputSpace)
            {
                _rightAxis = ProjectDirectionOnPlane(PlayerInputSpace.right, _upAxis);
                _forwardAxis = ProjectDirectionOnPlane(PlayerInputSpace.forward, _upAxis);
            }
            else
            {
                _rightAxis = ProjectDirectionOnPlane(Vector3.right, _upAxis);
                _forwardAxis = ProjectDirectionOnPlane(Vector3.forward, _upAxis);
            }

            // Calculate the actual desired speed from input values adjusted for differential speed ranges
            _desiredVelocity = new Vector3(
                inputAxesData.InputMoveAdjusted.x * MaxSpeedStrafe,
                0f,
                inputAxesData.InputMoveAdjusted.y * (_desiredVelocity.y >= 0 ? MaxSpeedForward : MaxSpeedBackward)
            );

            _jumpQueued |= PlayerInputModule.IsJumpQueued();

            if (_hasDebugRenderer)
                RunDebugHelpers();
        }

        private void RunDebugHelpers()
        {
            switch (ColorMode)
            {
                case ColorSetting.ContactCount:
                    _debugRenderer.material.SetColor(BaseColor, Color.cyan * _groundContactCount * 0.25f);
                    break;
                case ColorSetting.Airborne:
                    _debugRenderer.material.SetColor(BaseColor, OnGround ? Color.green : Color.cyan);
                    break;
                case ColorSetting.None:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void FixedUpdate()
        {
            var gravity = GetGravity(out _upAxis);
            UpdateState();

            AdjustVelocity();

            if (_jumpQueued)
            {
                _jumpQueued = false;
                Jump(gravity);
            }

            // If on the ground and not moving, remove gravity component along the contact normal to stop sliding due to gravity
            if (OnGround && _velocity.sqrMagnitude < 0.01f)
            {
                _velocity += _contactNormal * (Vector3.Dot(gravity, _contactNormal) * Time.deltaTime);
            }
            else
            {
                _velocity += gravity * Time.deltaTime;
            }


            _body.velocity = _velocity;

            ClearState();
        }


        public void ClearState()
        {
            _groundContactCount = _steepContactCount = 0;
            GroundContactNamesList.Clear();
            GroundContactNormal.Clear();
            GroundContactPoint.Clear();
            ContactPoints.Clear();
            _contactNormal = _adjustedContactNormal = _steepNormal = Vector3.zero;
        }

        public void UpdateState()
        {
            _stepsSinceLastGrounded++;
            _stepsSinceLastJump++;
            _velocity = _body.velocity;
            if (OnGround || SnapToGround() || CheckSteepContacts())
            {
                _stepsSinceLastGrounded = 0;
                if (_stepsSinceLastJump > 1)
                {
                    _jumpPhase = 0;
                }

                // Use fixed normal value if multiple ground contacts
                if (_groundContactCount <= 1)
                    return;
                //Normalize because we add contact normals together
                _adjustedContactNormal.Normalize();
                _contactNormal = _adjustedContactNormal;
            }
            else
            {
                _contactNormal = _upAxis;
            }
        }

        private bool SnapToGround()
        {
            if (_stepsSinceLastGrounded > 1 || _stepsSinceLastJump <= 2)
                return false;
            var speed = _velocity.magnitude;
            if (speed > MaxSnapSpeed)
                return false;
            if (!Physics.Raycast(_body.position, -_upAxis, out _hit, ProbeDistance, ProbeMask))
                return false;
            var updot = Vector3.Dot(_upAxis, _hit.normal);
            if (updot < GetMinDot(_hit.collider.gameObject.layer))
                return false;

            _groundContactCount = 1;
            _contactNormal = _hit.normal;

            var dot = Vector3.Dot(_velocity, _hit.normal);
            if (dot > 0f)
                _velocity = (_velocity - _hit.normal * dot).normalized * speed;

            return true;
        }

        private void OnCollisionEnter(Collision other)
        {
            EvaluateCollision(other);
        }

        private void OnCollisionStay(Collision other)
        {
            EvaluateCollision(other);
        }

        private void EvaluateCollision(Collision collision)
        {
            var minDot = GetMinDot(collision.gameObject.layer);
            for (var i = 0; i < collision.contactCount; i++)
            {
                var normal = collision.GetContact(i).normal;
                var updot = Vector3.Dot(_upAxis, normal);
                if (updot >= minDot)
                {
                    var contact = collision.GetContact(0);
                    _groundContactCount++;
                    _contactNormal += normal;

                    // Debug Data
                    GroundContactNamesList.Add(collision.gameObject.name);
                    ContactPoints.Add(contact);
                    GroundContactPoint.Add(contact.point);
                    GroundContactNormal.Add(contact.normal);

                    // check if second contact world y position is higher than the collider would have moved this fixed update
                    // store as adjusted normal value and use it if we have more than one ground contact


                    var minBound = _collider.bounds.min;
                    var diff = Mathf.Abs(minBound.y - contact.point.y);
                    // velocity incorporates deltatime so we need to remove it before converting to fixed dt
                    var deltaPosLastLoop = Mathf.Abs((_velocity.y / Time.deltaTime) * Time.fixedDeltaTime);
                    if (diff < deltaPosLastLoop)
                    {
                        // this will ruin movement on slopes? yes we only want to do this for multiple contacts & flat surfaces
                        normal = Vector3.up;
                    }

                    _adjustedContactNormal += normal;
                }
                else if (updot > -0.01f)
                {
                    _steepContactCount++;
                    _steepNormal += normal;
                }
            }
        }

        private bool CheckSteepContacts()
        {
            if (_steepContactCount <= 1)
                return false;
            _steepNormal.Normalize();
            var updot = Vector3.Dot(_upAxis, _steepNormal);
            if (updot < _minGroundDotProduct)
                return false;
            _steepContactCount = 0;
            _groundContactCount = 1;
            _contactNormal = _steepNormal;
            return true;
        }

        public Vector3 GetGravity(out Vector3 upAxis)
        {
            var gravity = Physics.gravity;
            upAxis = -gravity.normalized;
            return DisableGravity ? Vector3.zero : gravity;
        }

        public void SetGravity(bool toState, bool freezePosition = false)
        {
            DisableGravity = !toState;
            
            // LAZY HACK -- resetting velocity when we toggle gravity off so you don't keep drifting off
            // If we need actual zero-g behavior this needs to be a lot smarter
            if (DisableGravity)
                _velocity = _body.velocity = Vector3.zero;

            if (freezePosition && DisableGravity)
                _body.constraints = RigidbodyConstraints.FreezeAll;
            else
                _body.constraints = RigidbodyConstraints.FreezeRotation;
        }

        private void Jump(Vector3 gravity)
        {
            Vector3 jumpDirection;

            if (OnGround)
            {
                jumpDirection = _contactNormal;
            }
            else if (OnSteep)
            {
                jumpDirection = _steepNormal;
                _jumpPhase = 0;
            }
            else if (MaxAirJumps > 0 && _jumpPhase <= MaxAirJumps)
            {
                if (_jumpPhase == 0)
                {
                    _jumpPhase = 1;
                }

                jumpDirection = _contactNormal;
            }
            else
            {
                return;
            }

            _stepsSinceLastJump = 0;
            _jumpPhase++;
            var jumpSpeed = Mathf.Sqrt(2f * gravity.magnitude * JumpHeight);
            if (UseJumpBias)
                jumpDirection = (jumpDirection + _upAxis).normalized;
            var alignedSpeed = Vector3.Dot(_velocity, jumpDirection);
            if (alignedSpeed > Mathf.Epsilon)
            {
                jumpSpeed = Mathf.Max(jumpSpeed - alignedSpeed, 0f);
            }

            _velocity += jumpDirection * jumpSpeed;
        }

        /// <summary>
        ///  remove component of contact normal along the velocity to prevent sliding up the slope. Not sure if really working.
        /// </summary>
        private void PreventSlideUpSlope()
        {
            var comp = _contactNormal * Vector3.Dot(_velocity, _contactNormal);
            // var projection = ProjectDirectionOnPlane(_velocity, _steepNormal);
            // var dot = Vector3.Dot(projection, _velocity);
            _velocity -= comp;
        }

        private static Vector3 ProjectDirectionOnPlane(Vector3 direction, Vector3 normal) => (direction - normal * Vector3.Dot(direction, normal)).normalized;

        private void AdjustVelocity()
        {
            var xAxis = ProjectDirectionOnPlane(_rightAxis, _contactNormal);
            var zAxis = ProjectDirectionOnPlane(_forwardAxis, _contactNormal);
            var currentX = Vector3.Dot(_velocity, xAxis);
            var currentZ = Vector3.Dot(_velocity, zAxis);
            var accel = OnGround ? MaxAccel : MaxAirAccel;
            var newX = Mathf.MoveTowards(currentX, _desiredVelocity.x, accel * Time.deltaTime);
            var newZ = Mathf.MoveTowards(currentZ, _desiredVelocity.z, accel * Time.deltaTime);
            _velocity += xAxis * (newX - currentX) + zAxis * (newZ - currentZ);
        }

        private float GetMinDot(int layer) => (StairsMask & (1 << layer)) == 0 ? _minGroundDotProduct : _minStairsDotProduct;

        protected InputAxesData InputAxesData => PlayerInputModule.GetInputAxesData();
        protected AxisMode.Value AxisMode => PlayerInputModule.GetAxisMode();

        public Vector3 GetVelocity() => _velocity;

        // private static bool IsInLayerMask(int layer, LayerMask layerMask) => layerMask == (layerMask | (1 << layer));

        #region Debug Helpers

#if UNITY_EDITOR

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying)
                return;
            DrawContacts();
            DrawMoveDirection();
            DrawGroundRaycast();
        }

        private void DrawContacts()
        {
            for (var i = 0; i < ContactPoints.Count; i++)
            {
                var contact = ContactPoints[i];
                // var name = contact.otherCollider.name;
                Gizmos.color = new Color(0, 0.7f + 0.3f / ContactPoints.Count * (i + 1), 1, 1);
                Handles.Label(contact.point + contact.normal, contact.otherCollider.name);
                Gizmos.DrawLine(contact.point, contact.point + contact.normal);
            }

            Gizmos.color = new Color(1, 0.3f, 0.3f, 0.6f);
            var bounds = GetComponent<Collider>().bounds;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
            // Gizmos.color = Color.cyan;
            Gizmos.color = OnGround ? Color.red : Color.green;
            var minpos = new Vector3(bounds.min.x + bounds.size.x / 2, bounds.min.y, bounds.min.z + bounds.size.z / 2);
            Gizmos.DrawSphere(minpos, 0.025f);
            // Gizmos.DrawLine(bounds.min, bounds.max);
        }

        private void DrawMoveDirection()
        {
            Handles.color = Color.gray;
            var pos = _transform.position;
            Handles.ArrowHandleCap(0, pos, Quaternion.LookRotation(GetVelocity()), HandleUtility.GetHandleSize(pos), EventType.Repaint);
        }

        private void DrawGroundRaycast()
        {
            Handles.color = OnGround ? Color.red : Color.green;
            var pos = _transform.position;

            // raw raycast debug
            Handles.DrawLine(pos + Vector3.up * 0.05f, pos + Vector3.down * GetProbeDistance);
            // ground hit position
            // Handles.SphereHandleCap(0, _pos + Vector3.down * _controller.GroundDistance, Quaternion.identity, 0.05f, EventType.Repaint);
        }

#endif

        #endregion
    }
}