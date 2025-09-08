using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Xuwu.FourDimensionalPortals.Demo
{
    public class PortalGun : MonoBehaviour
    {
        [SerializeField] private LayerMask _layerMask = 1 << 4;
        [SerializeField] private Portal _yellowPortal;
        [SerializeField] private Portal _cyanPortal;

        private const float MaxGrabDistance = 4f;

        private const int HitResultsBudget = 16;
        private static readonly Collider[] s_colliderResultsBuffer = new Collider[HitResultsBudget];
        private static readonly RaycastHit[] s_hitResultsBuffer = new RaycastHit[HitResultsBudget];

        #region InputSystem
        public void OnFireLeft(InputValue value)
        {
            if (!value.isPressed)
                return;

            FirePortal(_yellowPortal);
        }

        public void OnFireRight(InputValue value)
        {
            if (!value.isPressed)
                return;

            FirePortal(_cyanPortal);
        }

        public void OnGrab(InputValue value)
        {
            if (!value.isPressed)
                return;

            Grab();
        }
        #endregion

        private Transform _tempTransform;
        private Rigidbody _grabbedRigidbody;
        private Coroutine _yellowPortalFadeViewSizeCoroutine;
        private Coroutine _cyanPortalFadeViewSizeCoroutine;

        private void OnEnable()
        {
            _tempTransform = new GameObject(nameof(_tempTransform)) { hideFlags = HideFlags.HideAndDontSave }.transform;
            _tempTransform.gameObject.SetActive(false);

            if (_yellowPortal && _yellowPortal.CustomViewMaterial)
                _yellowPortal.CustomViewMaterial.SetFloat("_ViewSize", 1f);

            if (_cyanPortal && _cyanPortal.CustomViewMaterial)
                _cyanPortal.CustomViewMaterial.SetFloat("_ViewSize", 1f);
        }

        private void OnDisable()
        {
            Destroy(_tempTransform.gameObject);

            if (_yellowPortal && _yellowPortal.CustomViewMaterial)
                _yellowPortal.CustomViewMaterial.SetFloat("_ViewSize", 1f);

            if (_cyanPortal && _cyanPortal.CustomViewMaterial)
                _cyanPortal.CustomViewMaterial.SetFloat("_ViewSize", 1f);
        }

        private void FixedUpdate()
        {
            if (!_grabbedRigidbody)
                return;

            Rigidbody targetRigidbody = null;
            float gunScale = Vector3.Dot(Vector3.one, transform.lossyScale) / 3f;

            var origin = transform.position;
            var direction = transform.forward;
            float maxGrabDistance = MaxGrabDistance * gunScale;
            var targetPosition = origin + direction * (maxGrabDistance * .5f);

            int hitCount = Physics.RaycastNonAlloc(origin, direction, s_hitResultsBuffer, maxGrabDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            PhysicsUtils.SortRaycastHits(new Span<RaycastHit>(s_hitResultsBuffer, 0, hitCount));

            for (int i = 0; i < hitCount; i++)
            {
                var hit = s_hitResultsBuffer[i];

                if (!PortalSystem.IsRaycastHitValid(hit))
                    continue;

                if (!hit.rigidbody)
                    break;

                if (hit.rigidbody.TryGetComponent(out Portal hitPortal))
                {
                    if (hit.collider != hitPortal.PlaneMeshCollider)
                        break;

                    if (!hitPortal.IsWorkable())
                        break;

                    var maxHitPoint = hitPortal.TransferPoint(origin + direction * maxGrabDistance);

                    origin = hitPortal.TransferPoint(origin + direction * hit.distance);
                    direction = hitPortal.TransferDirection(direction);
                    maxGrabDistance = Vector3.Distance(origin, maxHitPoint);
                    targetPosition = hitPortal.TransferPoint(targetPosition);

                    hitCount = Physics.RaycastNonAlloc(origin, direction, s_hitResultsBuffer, maxGrabDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    PhysicsUtils.SortRaycastHits(new Span<RaycastHit>(s_hitResultsBuffer, 0, hitCount));

                    for (int j = 0; j < hitCount; j++)
                    {
                        hit = s_hitResultsBuffer[j];

                        if (!PortalSystem.IsRaycastHitValid(hit))
                            continue;

                        if (hit.collider == hitPortal.LinkedPortal.PlaneMeshCollider)
                            continue;

                        targetRigidbody = hit.rigidbody;

                        break;
                    }
                }
                else
                {
                    targetRigidbody = hit.rigidbody;
                }

                break;
            }

            if (targetRigidbody && targetRigidbody.TryGetComponent(out RigidbodyGhost rigidbodyGhost))
            {
                targetPosition = rigidbodyGhost.AttachedPortal.TransferPoint(targetPosition);
                targetRigidbody = rigidbodyGhost.SourceRigidbody;
            }

            if (targetRigidbody == _grabbedRigidbody)
            {
                _grabbedRigidbody.velocity = (targetPosition - _grabbedRigidbody.position) / Time.fixedDeltaTime;
                _grabbedRigidbody.angularVelocity = Vector3.zero;
            }
            else
            {
                _grabbedRigidbody.velocity = Vector3.zero;
                _grabbedRigidbody.angularVelocity = Vector3.zero;
                _grabbedRigidbody = null;
            }
        }

        private void FirePortal(Portal portal)
        {
            portal.PlaneMeshCollider.enabled = false;
            portal.FrameMeshCollider.enabled = false;
            bool isHit = Physics.Raycast(transform.position, transform.forward, out var hit, 100f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            portal.PlaneMeshCollider.enabled = true;
            portal.FrameMeshCollider.enabled = true;

            if (isHit && CheckPlaceable(portal, hit))
            {
                float gunScale = Vector3.Dot(Vector3.one, transform.lossyScale) / 3f;

                portal.transform.parent = null;
                portal.transform.localScale = Vector3.one * gunScale;
                portal.transform.SetPositionAndRotation(_tempTransform.position, _tempTransform.rotation);
                portal.transform.parent = _tempTransform.parent;
                portal.gameObject.SetActive(true);

                if (portal == _yellowPortal)
                {
                    if (_yellowPortalFadeViewSizeCoroutine != null)
                        StopCoroutine(_yellowPortalFadeViewSizeCoroutine);

                    _yellowPortalFadeViewSizeCoroutine = StartCoroutine(FadeViewSize(portal));
                }
                else if (portal == _cyanPortal)
                {
                    if (_cyanPortalFadeViewSizeCoroutine != null)
                        StopCoroutine(_cyanPortalFadeViewSizeCoroutine);

                    _cyanPortalFadeViewSizeCoroutine = StartCoroutine(FadeViewSize(portal));
                }
            }
        }

        private IEnumerator FadeViewSize(Portal portal)
        {
            float fadeDuration = .2f;
            float elapseTime = 0f;

            while (portal.CustomViewMaterial && elapseTime < fadeDuration)
            {
                elapseTime += Time.deltaTime;
                portal.CustomViewMaterial.SetFloat("_ViewSize", Mathf.Clamp01(elapseTime / fadeDuration));
                yield return null;
            }
        }

        private bool CheckPlaceable(Portal portal, RaycastHit hit)
        {
            if (((1 << hit.collider.gameObject.layer) & _layerMask) == 0)
                return false;

            var zOffset = hit.collider.contactOffset + .01f;
            var targetPosition = hit.point + hit.normal * zOffset;

            var targetUp = RigidbodyCharacterController.WorldUp;
            if (Mathf.Abs(Vector3.Dot(targetUp, hit.normal)) > .9f)
            {
                if (Vector3.Dot(targetUp, hit.normal) > 0f)
                    targetUp = transform.up;
                else
                    targetUp = -transform.up;
            }

            var targetRotation = Quaternion.LookRotation(hit.normal, targetUp);

            _tempTransform.parent = null;
            _tempTransform.localScale = transform.lossyScale;
            _tempTransform.SetPositionAndRotation(targetPosition, targetRotation);

            var localBounds = portal.Config.PlaneMesh.bounds;
            localBounds.center = new Vector3(localBounds.center.x, localBounds.center.y, 0f);
            localBounds.extents = new Vector3(localBounds.extents.x, localBounds.extents.y, 0f);

            var center = _tempTransform.TransformPoint(localBounds.center);
            var halfExtents = Vector3.Scale(localBounds.extents, _tempTransform.lossyScale);
            halfExtents.z = .1f;

            int overlapCount = Physics.OverlapBoxNonAlloc(center, halfExtents, s_colliderResultsBuffer, _tempTransform.rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlapCount; i++)
            {
                var collider = s_colliderResultsBuffer[i];

                if (collider.TryGetComponent(out Portal overlapPortal))
                {
                    if (overlapPortal != portal)
                        return false;
                }

                if (((1 << collider.gameObject.layer) & _layerMask) == 0)
                    continue;

                if (collider != hit.collider)
                    return false;
            }

            var leftTop = _tempTransform.TransformPoint(localBounds.center.x - localBounds.extents.x,
                localBounds.center.y + localBounds.extents.y, 0f);
            var rightTop = _tempTransform.TransformPoint(localBounds.center.x + localBounds.extents.x,
                localBounds.center.y + localBounds.extents.y, 0f);
            var leftBottom = _tempTransform.TransformPoint(localBounds.center.x - localBounds.extents.x,
                localBounds.center.y - localBounds.extents.y, 0f);
            var rightBottom = _tempTransform.TransformPoint(localBounds.center.x + localBounds.extents.x,
                localBounds.center.y - localBounds.extents.y, 0f);

            var leftTopRay = new Ray(leftTop, -hit.normal);
            if (!hit.collider.Raycast(leftTopRay, out _, zOffset + .1f))
                return false;

            var rightTopRay = new Ray(rightTop, -hit.normal);
            if (!hit.collider.Raycast(rightTopRay, out _, zOffset + .1f))
                return false;

            var leftBottomRay = new Ray(leftBottom, -hit.normal);
            if (!hit.collider.Raycast(leftBottomRay, out _, zOffset + .1f))
                return false;

            var rightBottomRay = new Ray(rightBottom, -hit.normal);
            if (!hit.collider.Raycast(rightBottomRay, out _, zOffset + .1f))
                return false;

            return true;
        }

        private void Grab()
        {
            if (_grabbedRigidbody)
            {
                _grabbedRigidbody = null;
                return;
            }

            Rigidbody targetRigidbody = null;
            float gunScale = Vector3.Dot(Vector3.one, transform.lossyScale) / 3f;

            var origin = transform.position;
            var direction = transform.forward;
            var maxGrabDistance = MaxGrabDistance * gunScale;

            int hitCount = Physics.RaycastNonAlloc(origin, direction, s_hitResultsBuffer, maxGrabDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            PhysicsUtils.SortRaycastHits(new Span<RaycastHit>(s_hitResultsBuffer, 0, hitCount));

            for (int i = 0; i < hitCount; i++)
            {
                var hit = s_hitResultsBuffer[i];

                if (!PortalSystem.IsRaycastHitValid(hit))
                    continue;

                if (!hit.rigidbody)
                    break;

                if (hit.rigidbody.TryGetComponent(out Portal hitPortal))
                {
                    if (hit.collider != hitPortal.PlaneMeshCollider)
                        break;

                    if (!hitPortal.IsWorkable())
                        break;

                    var maxHitPoint = hitPortal.TransferPoint(origin + direction * maxGrabDistance);

                    origin = hitPortal.TransferPoint(origin + direction * hit.distance);
                    direction = hitPortal.TransferDirection(direction);
                    maxGrabDistance = Vector3.Distance(origin, maxHitPoint);

                    hitCount = Physics.RaycastNonAlloc(origin, direction, s_hitResultsBuffer, maxGrabDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    PhysicsUtils.SortRaycastHits(new Span<RaycastHit>(s_hitResultsBuffer, 0, hitCount));

                    for (int j = 0; j < hitCount; j++)
                    {
                        hit = s_hitResultsBuffer[j];

                        if (!PortalSystem.IsRaycastHitValid(hit))
                            continue;

                        if (hit.collider == hitPortal.LinkedPortal.PlaneMeshCollider)
                            continue;

                        targetRigidbody = hit.rigidbody;

                        break;
                    }
                }
                else
                {
                    targetRigidbody = hit.rigidbody;
                }

                break;
            }

            if (targetRigidbody && !targetRigidbody.TryGetComponent(out Portal _))
            {
                if (targetRigidbody.TryGetComponent(out RigidbodyGhost rigidbodyGhost))
                    _grabbedRigidbody = rigidbodyGhost.SourceRigidbody;
                else
                    _grabbedRigidbody = targetRigidbody;
            }
        }
    }
}
