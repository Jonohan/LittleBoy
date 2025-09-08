using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Xuwu.FourDimensionalPortals.Demo
{
    public static class PhysicsUtils
    {
        public static void SortRaycastHits(Span<RaycastHit> raycastHits)
        {
            for (int i = 1; i <= raycastHits.Length; i++)
            {
                for (int j = 0; j < raycastHits.Length - i; j++)
                {
                    var hit = raycastHits[j];

                    if (hit.distance > raycastHits[j + 1].distance)
                    {
                        raycastHits[j] = raycastHits[j + 1];
                        raycastHits[j + 1] = hit;
                    }
                }
            }
        }
    }

    [RequireComponent(typeof(CapsuleCollider))]
    public class RigidbodyCharacterController : PortalTraveler
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private Transform _rotationHandle;
        [SerializeField] private Transform _cameraFollowTarget;
        [SerializeField] private PortalSystemAdditionalCameraData _portalSystemCameraData;

        private const float GravitationalAcceleration = -9.81f * 2f;
        private const float JumpHeight = 1.5f;
        public static readonly Vector3 WorldUp = Vector3.up;

        private const int GroundCheckBudget = 16;
        private static readonly RaycastHit[] s_hitResultsBuffer = new RaycastHit[GroundCheckBudget];

        #region InputSystem
        private Vector2 _moveInput;
        private Vector2 _lookInput;
        private bool _isJump;
        private bool _isSprinting;
        private bool _isFirstPersonView;

        public void OnMove(InputValue value) => _moveInput = value.Get<Vector2>();

        public void OnLook(InputValue value) => _lookInput = value.Get<Vector2>() * new Vector2(.1f, .075f);

        public void OnJump(InputValue value) => _isJump = value.isPressed;

        public void OnSprint(InputValue value) => _isSprinting = value.isPressed;

        public void OnSwitchView(InputValue value)
        {
            if (!value.isPressed)
                return;

            _isFirstPersonView = !_isFirstPersonView;

            if (_isFirstPersonView)
            {
                _portalSystemCameraData.Camera.cullingMask = LayerMask.GetMask("Default", "TransparentFX", "Water", "UI"); ;
                _portalSystemCameraData.PenetratingViewCullingMask = LayerMask.GetMask("Default", "Ignore Raycast", "Water", "UI");
            }
            else
            {
                _portalSystemCameraData.Camera.cullingMask = Physics.AllLayers;
                _portalSystemCameraData.PenetratingViewCullingMask = Physics.AllLayers;
            }
        }

        public void OnIncreaseRecursionLimit(InputValue value)
        {
            if (!value.isPressed)
                return;

            _portalSystemCameraData.OverrideRenderSettings.RecursionLimit++;
        }

        public void OnDecreaseRecursionLimit(InputValue value)
        {
            if (!value.isPressed)
                return;

            _portalSystemCameraData.OverrideRenderSettings.RecursionLimit--;
        }
        #endregion

        private Vector2 _smoothMoveInput;
        private Vector2 _smoothMoveInputVelocity;
        private float _currGravitationalAcceleration;
        private float _currJumpHeight;
        private float _xRotation;

        private CapsuleCollider _capsuleCollider;
        private PhysicMaterial _physicsMaterial;

        private void FixedUpdate()
        {
            _smoothMoveInput = Vector2.SmoothDamp(_smoothMoveInput, _moveInput * (_isSprinting ? 6f : 2.3f), ref _smoothMoveInputVelocity, .1f);

            //maintain relative velocity between different scale, it is not physical
            _currGravitationalAcceleration = GravitationalAcceleration * transform.lossyScale.y;
            _currJumpHeight = JumpHeight * transform.lossyScale.y;

            //slerp rotation to world up
            var targetForward = Mathf.Abs(Vector3.Dot(WorldUp, transform.forward)) > .9f
                ? Vector3.Cross(transform.right, WorldUp).normalized
                : Vector3.Cross(Vector3.Cross(WorldUp, transform.forward).normalized, WorldUp).normalized;

            var currToTargetRotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(targetForward, WorldUp), .1f) * Quaternion.Inverse(transform.rotation);
            currToTargetRotation.ToAngleAxis(out float angle, out var axis);
            transform.RotateAround(transform.TransformPoint(TransferPivotOffset), axis, angle);

            //horizontal rotation
            if (_isFirstPersonView)
            {
                transform.rotation *= Quaternion.AngleAxis(_lookInput.x, transform.up);
                _rotationHandle.localRotation = Quaternion.identity;
            }
            else
            {
                _rotationHandle.localRotation *= Quaternion.AngleAxis(_lookInput.x, Vector3.up);

                if (_smoothMoveInput.magnitude > .1f)
                {
                    var tempRotation = _rotationHandle.rotation;
                    var lookForward = _rotationHandle.TransformDirection(new Vector3(_smoothMoveInput.x, 0f, _smoothMoveInput.y).normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookForward, transform.up), .2f);
                    _rotationHandle.rotation = tempRotation;
                }
            }

            var velocityLocal = _rotationHandle.InverseTransformVector(Rigidbody.velocity);
            bool isGrounded = CheckGrounded();

            if (isGrounded)
            {
                if (_isJump)
                {
                    var jumpInitialVelocity = Mathf.Sqrt(-(2f * _currGravitationalAcceleration * _currJumpHeight)) * WorldUp;

                    if (_isFirstPersonView)
                    {
                        Rigidbody.AddForce(jumpInitialVelocity, ForceMode.VelocityChange);
                        _animator.SetTrigger("Jump");
                    }
                    else
                    {
                        var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
                        bool canJump = stateInfo.IsName("Idle Walk Run Blend");

                        if (stateInfo.IsName("JumpLand"))
                            canJump = stateInfo.normalizedTime > .25f;

                        if (canJump)
                        {
                            Rigidbody.AddForce(jumpInitialVelocity, ForceMode.VelocityChange);
                            _animator.SetTrigger("Jump");
                        }
                    }
                }

                _physicsMaterial.dynamicFriction = 0f;
                _physicsMaterial.staticFriction = 0f;

                velocityLocal.x = _smoothMoveInput.x;
                velocityLocal.z = _smoothMoveInput.y;

                Rigidbody.velocity = _rotationHandle.TransformVector(velocityLocal);
            }
            else
            {
                if (_smoothMoveInput == Vector2.zero)
                {
                    _physicsMaterial.dynamicFriction = 1f;
                    _physicsMaterial.staticFriction = 1f;
                }
                else
                {
                    _physicsMaterial.dynamicFriction = .1f;
                    _physicsMaterial.staticFriction = .1f;
                }

                var velocityChangeX = _smoothMoveInput.x - velocityLocal.x;
                var velocityChangeZ = _smoothMoveInput.y - velocityLocal.z;

                velocityChangeX = _smoothMoveInput.x >= 0f ? Mathf.Clamp(velocityChangeX, 0f, _smoothMoveInput.x)
                    : Mathf.Clamp(velocityChangeX, _smoothMoveInput.x, 0f);
                velocityChangeZ = _smoothMoveInput.y >= 0f ? Mathf.Clamp(velocityChangeZ, 0f, _smoothMoveInput.y)
                    : Mathf.Clamp(velocityChangeZ, _smoothMoveInput.y, 0f);

                Rigidbody.AddForce(_rotationHandle.TransformVector(velocityChangeX, 0f, velocityChangeZ) * .5f, ForceMode.VelocityChange);
            }

            Rigidbody.AddForce(_currGravitationalAcceleration * WorldUp, ForceMode.Acceleration);

            _animator.SetBool("Grounded", isGrounded);
            _animator.SetFloat("MoveMag", _smoothMoveInput.magnitude);
            _isJump = false;
        }

        private void LateUpdate()
        {
            _xRotation += _lookInput.y;
            _xRotation = Mathf.Clamp(_xRotation, -90f, 90f);

            _cameraFollowTarget.localRotation = Quaternion.AngleAxis(_xRotation, Vector3.right);

            if (_isFirstPersonView)
            {
                _portalSystemCameraData.PenetratingPortal = PenetratingPortal;
                _portalSystemCameraData.transform.SetPositionAndRotation(_cameraFollowTarget.position, _cameraFollowTarget.rotation);
                return;
            }

            var origin = _cameraFollowTarget.position;
            var direction = -(_cameraFollowTarget.rotation * Vector3.forward);
            float maxDistance = 4f * (Vector3.Dot(Vector3.one, transform.lossyScale) / 3f);

            var cameraRotation = _cameraFollowTarget.rotation;
            var cameraPosition = origin + direction * maxDistance;

            int hitCount = Physics.RaycastNonAlloc(origin, direction, s_hitResultsBuffer, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            PhysicsUtils.SortRaycastHits(new Span<RaycastHit>(s_hitResultsBuffer, 0, hitCount));

            for (int i = 0; i < hitCount; i++)
            {
                var hit = s_hitResultsBuffer[i];

                if (!PortalSystem.IsRaycastHitValid(hit))
                    continue;

                if (hit.collider.TryGetComponent(out Portal portal) && portal.IsWorkable() && hit.collider == portal.PlaneMeshCollider)
                {
                    _portalSystemCameraData.PenetratingPortal = portal.LinkedPortal;

                    cameraPosition = portal.TransferPoint(cameraPosition);
                    cameraRotation = portal.TransferRotation(cameraRotation);

                    origin = portal.TransferPoint(origin + direction * hit.distance);
                    direction = portal.TransferDirection(direction);
                    maxDistance = Vector3.Distance(origin, cameraPosition);

                    hitCount = Physics.RaycastNonAlloc(origin, direction, s_hitResultsBuffer, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    PhysicsUtils.SortRaycastHits(new Span<RaycastHit>(s_hitResultsBuffer, 0, hitCount));

                    for (int j = 0; j < hitCount; j++)
                    {
                        hit = s_hitResultsBuffer[j];

                        if (!PortalSystem.IsRaycastHitValid(hit))
                            continue;

                        cameraPosition = origin + direction * (hit.distance - .2f);

                        break;
                    }
                }
                else
                {
                    cameraPosition = origin + direction * (hit.distance - .2f);
                }

                break;
            }

            _portalSystemCameraData.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
        }

        private bool CheckGrounded()
        {
            var origin = transform.TransformPoint(_capsuleCollider.center);
            float radius = _capsuleCollider.radius * .9f;
            float maxDistance = (_capsuleCollider.height * .5f - radius + .05f);

            radius *= transform.lossyScale.y;
            maxDistance *= transform.lossyScale.y;

            _capsuleCollider.enabled = false;
            int hitCount = Physics.SphereCastNonAlloc(origin, radius, -transform.up, s_hitResultsBuffer, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            _capsuleCollider.enabled = true;

            bool isGrounded = false;

            for (int i = 0; i < hitCount; i++)
            {
                var hit = s_hitResultsBuffer[i];
                isGrounded |= PortalSystem.IsCollisionValid(_capsuleCollider, hit.collider, hit.point);
            }

            return isGrounded;
        }

        protected override void PassThrough(Portal fromPortal, Portal toPortal, Matrix4x4 transferMatrix)
        {
            base.PassThrough(fromPortal, toPortal, transferMatrix);

            if (Mathf.Abs(Vector3.Dot(fromPortal.transform.forward, WorldUp)) > .1f
                || Mathf.Abs(Vector3.Dot(toPortal.transform.forward, WorldUp)) > .1f)
            {
                var velocityLocal = toPortal.transform.InverseTransformDirection(Rigidbody.velocity);

                float popUpHeight = 2f * transform.lossyScale.y;
                float extVelocityChangeLocalZ = Mathf.Sqrt(-(2f * _currGravitationalAcceleration * popUpHeight));

                var extVelocityChange = toPortal.transform.forward * Mathf.Clamp(extVelocityChangeLocalZ
                    - velocityLocal.z, 0f, extVelocityChangeLocalZ);

                transform.position += toPortal.transform.forward * (_capsuleCollider.radius * transform.lossyScale.y);
                Rigidbody.AddForce(extVelocityChange, ForceMode.VelocityChange);
            }

            if (!_isFirstPersonView)
                return;

            var cameraForward = _cameraFollowTarget.forward;
            var cameraRight = _cameraFollowTarget.right;
            var targetForward = Mathf.Abs(Vector3.Dot(WorldUp, cameraForward)) > .9f
                ? Vector3.Cross(cameraRight, WorldUp).normalized
                : Vector3.Cross(Vector3.Cross(WorldUp, cameraForward).normalized, WorldUp).normalized;

            var currToTargetRotation = Quaternion.LookRotation(targetForward, Vector3.Cross(targetForward, cameraRight)) * Quaternion.Inverse(transform.rotation);
            currToTargetRotation.ToAngleAxis(out float angle, out var axis);
            transform.RotateAround(transform.TransformPoint(TransferPivotOffset), axis, angle);

            _xRotation = Vector3.SignedAngle(transform.forward, cameraForward, transform.right);
        }

        public override void Validate()
        {
            base.Validate();

            Rigidbody.drag = .5f;
            Rigidbody.hideFlags = HideFlags.NotEditable;
            Rigidbody.mass = 50f * transform.lossyScale.x * transform.lossyScale.y * transform.lossyScale.z;
            Rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
            Rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            Rigidbody.interpolation = RigidbodyInterpolation.None;
            Rigidbody.isKinematic = false;
            Rigidbody.useGravity = false;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            TryGetComponent(out _capsuleCollider);

            _capsuleCollider.hideFlags = HideFlags.NotEditable;
            _capsuleCollider.direction = 1;
            _capsuleCollider.enabled = true;
            _capsuleCollider.isTrigger = false;
            _capsuleCollider.sharedMaterial = null;
            _capsuleCollider.center = new Vector3(0f, .9f, 0f);
            _capsuleCollider.height = 1.8f;
            _capsuleCollider.radius = .25f;

            if (!_capsuleCollider.sharedMaterial)
            {
                _capsuleCollider.sharedMaterial = new PhysicMaterial();
                _physicsMaterial = _capsuleCollider.sharedMaterial;
            }

            _physicsMaterial.frictionCombine = PhysicMaterialCombine.Multiply;
            _physicsMaterial.bounciness = 1f;
            _physicsMaterial.bounceCombine = PhysicMaterialCombine.Multiply;

            _portalSystemCameraData.PenetratingPortal = null;
        }
    }
}
