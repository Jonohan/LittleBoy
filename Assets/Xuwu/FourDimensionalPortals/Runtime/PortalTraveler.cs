using System.Collections.Generic;
using UnityEngine;

namespace Xuwu.FourDimensionalPortals
{
    [AddComponentMenu("Xuwu/Four Dimensional Portals/Portal Traveler")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class PortalTraveler : MonoBehaviour
    {
        [SerializeField] private Vector3 _transferPivotOffset;
        [SerializeField][LayerField] private int _cloneLayer;
        [SerializeField] private List<MeshRenderer> _meshRenderers;
        [SerializeField] private List<SkinnedMeshRenderer> _skinnedMeshRenderers;

        public Vector3 TransferPivotOffset { get => _transferPivotOffset; set => _transferPivotOffset = value; }

        public int CloneLayer
        {
            get => _cloneLayer;
            set => _cloneLayer = Mathf.Clamp(value, 0, 31);
        }

        public List<MeshRenderer> MeshRenderers => _meshRenderers;

        public List<SkinnedMeshRenderer> SkinnedMeshRenderers => _skinnedMeshRenderers;

        private readonly List<Collider> _attachedColliders = new();
        private readonly List<Material> _materialsBuffer = new();
        private readonly Dictionary<SkinnedMeshRenderer, Mesh> _bakedSkinnedMeshes = new();
        private readonly Dictionary<Material, Material> _cloneMaterials = new();
        private readonly Dictionary<Portal, float> _candidatePortals = new();

        private Vector3? _lastTransferPivot = null;

        private Portal _penetratingPortal = null;
        private Rigidbody _rigidbody = null;

        public Portal PenetratingPortal => _penetratingPortal;
        public Rigidbody Rigidbody => _rigidbody;

        protected virtual void OnEnable()
        {
            PortalSystem.s_activeTravelers.Add(this);
            Validate();
        }

        protected virtual void OnDisable()
        {
            PortalSystem.s_activeTravelers.Remove(this);
            Validate();
        }

        internal void UpdateCandidatePortals()
        {
            transform.SetPositionAndRotation(_rigidbody.position, _rigidbody.rotation);
            var transferPivot = transform.TransformPoint(_transferPivotOffset);

            _candidatePortals.Clear();
            _lastTransferPivot = transferPivot;
            _penetratingPortal = null;

            float distanceToPenetratingPortal = float.MaxValue;

            foreach (var portal in PortalSystem.s_activePortals)
            {
                if (portal._detectionZoneColliders.Count == 0)
                    continue;

                if (Physics.GetIgnoreLayerCollision(gameObject.layer, portal.gameObject.layer))
                    continue;

                if (!portal.GetPlane().GetSide(transferPivot))
                    continue;

                float closestDistanceToPortal = float.MaxValue;
                bool isCandidatePortal = false;
                bool isPenetrating = false;

                foreach (var collider in portal._detectionZoneColliders)
                {
                    if (collider.attachedRigidbody != _rigidbody)
                        continue;

                    isCandidatePortal = true;

                    float colliderToPortalDistance = float.MaxValue;

                    if (collider is MeshCollider meshCollider && !meshCollider.convex)
                        colliderToPortalDistance = Vector3.Distance(portal.transform.position, transferPivot);
                    else
                        colliderToPortalDistance = Vector3.Distance(portal.transform.position, collider.ClosestPoint(portal.transform.position));

                    if (colliderToPortalDistance < closestDistanceToPortal)
                        closestDistanceToPortal = colliderToPortalDistance;

                    if (isPenetrating)
                        continue;

                    if (Physics.ComputePenetration(collider, collider.transform.position, collider.transform.rotation,
                        portal.DetectionMeshCollider, portal.transform.position, portal.transform.rotation, out _, out _))
                    {
                        isPenetrating = true;
                        continue;
                    }
                }

                if (!isCandidatePortal)
                    continue;

                _candidatePortals.Add(portal, closestDistanceToPortal);

                if (isPenetrating && closestDistanceToPortal < distanceToPenetratingPortal)
                {
                    _penetratingPortal = portal;
                    distanceToPenetratingPortal = closestDistanceToPortal;
                }
            }

            if (_penetratingPortal)
            {
                _candidatePortals.Remove(_penetratingPortal);

                var plane = _penetratingPortal.GetPlane();
                var forwardPoint = _penetratingPortal.transform.TransformPoint(Vector3.forward);

                foreach (var collider in _attachedColliders)
                {
                    if (!collider)
                        continue;

                    bool isPenetrating;

                    if (Physics.ComputePenetration(collider, collider.transform.position, collider.transform.rotation,
                        _penetratingPortal.DetectionMeshCollider, _penetratingPortal.transform.position, _penetratingPortal.transform.rotation, out _, out _))
                    {
                        isPenetrating = true;
                    }
                    else if (collider is MeshCollider meshCollider && !meshCollider.convex)
                    {
                        isPenetrating = !plane.GetSide(collider.ClosestPointOnBounds(forwardPoint));
                    }
                    else
                    {
                        isPenetrating = !plane.GetSide(collider.ClosestPoint(forwardPoint));
                    }

                    if (isPenetrating)
                    {
                        collider.hasModifiableContacts = true;
                        _penetratingPortal._detectionZoneColliders.Remove(collider);
                        _penetratingPortal._penetratingColliders.Add(collider);
                    }
                    else
                    {
                        _penetratingPortal._detectionZoneColliders.Add(collider);
                    }
                }
            }
        }

        internal void ResolveCandidatePortals()
        {
            transform.SetPositionAndRotation(_rigidbody.position, _rigidbody.rotation);
            var transferPivot = transform.TransformPoint(_transferPivotOffset);

            Portal targetPortal = null;

            if (_penetratingPortal && _penetratingPortal.IsWorkable())
            {
                if (!_penetratingPortal.GetPlane().GetSide(transferPivot))
                    targetPortal = _penetratingPortal;
            }

            if (!targetPortal && _lastTransferPivot is Vector3 lastTransferPivot && lastTransferPivot != transferPivot)
            {
                var ray = new Ray(lastTransferPivot, transferPivot - lastTransferPivot);

                float lastDistanceToClosestPortal = float.MaxValue;

                foreach (var pair in _candidatePortals)
                {
                    var portal = pair.Key;

                    if (!portal || !portal.IsWorkable())
                        continue;

                    if (portal.GetPlane().GetSide(transferPivot))
                        continue;

                    if (!portal.PlaneMeshCollider.Raycast(ray, out _, Mathf.Infinity))
                        continue;

                    if (pair.Value < lastDistanceToClosestPortal)
                    {
                        targetPortal = portal;
                        lastDistanceToClosestPortal = pair.Value;
                    }
                }
            }

            if (targetPortal)
            {
                var fromPortal = targetPortal;
                var toPortal = targetPortal.LinkedPortal;
                var transferMatrix = fromPortal.GetTransferMatrix();
                var parent = transform.parent;
                var localToWorldMatrix = transferMatrix * transform.localToWorldMatrix;

                transform.parent = null;
                transform.SetPositionAndRotation(localToWorldMatrix.GetPosition(), localToWorldMatrix.rotation);
                transform.localScale = localToWorldMatrix.lossyScale;
                transform.parent = parent;

                _rigidbody.position = transform.position;
                _rigidbody.rotation = transform.rotation;

                fromPortal.OnTravellerTransferToLinkedPortal?.Invoke(this);
                PassThrough(fromPortal, toPortal, transferMatrix);
                toPortal.OnTravellerTransferFromLinkedPortal?.Invoke(this);
            }
        }

        protected virtual void PassThrough(Portal fromPortal, Portal toPortal, Matrix4x4 transferMatrix)
        {
            _rigidbody.mass *= transferMatrix.lossyScale.x * transferMatrix.lossyScale.y * transferMatrix.lossyScale.z;
            _rigidbody.angularVelocity = transferMatrix.rotation * _rigidbody.angularVelocity;
            _rigidbody.velocity = transferMatrix.MultiplyVector(_rigidbody.velocity);
        }

        internal void UpdateRenderers()
        {
            if (!_penetratingPortal || !_penetratingPortal.IsWorkable())
            {
                ResetSlicePlane();
                return;
            }

            Vector4 slicePlane = _penetratingPortal.GetVector4Plane();
            Vector4 cloneSlicePlane = _penetratingPortal.LinkedPortal.GetVector4Plane();

            var transferMatrix = _penetratingPortal.GetTransferMatrix();

            foreach (var renderer in _meshRenderers)
            {
                if (!renderer || !renderer.gameObject.activeInHierarchy || !renderer.enabled)
                    continue;

                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                var matrix = transferMatrix * renderer.localToWorldMatrix;

                renderer.GetMaterials(_materialsBuffer);

                for (int i = 0; i < _materialsBuffer.Count; i++)
                {
                    var material = _materialsBuffer[i];
                    if (!_cloneMaterials.ContainsKey(material))
                        _cloneMaterials.Add(material, new Material(material) { hideFlags = HideFlags.HideAndDontSave });

                    var cloneMaterial = _cloneMaterials[material];
                    cloneMaterial.shader = material.shader;
                    cloneMaterial.CopyPropertiesFromMaterial(material);

                    material.SetVector(PortalSystem.ShaderPropertyID.SlicePlane, slicePlane);
                    cloneMaterial.SetVector(PortalSystem.ShaderPropertyID.SlicePlane, cloneSlicePlane);

                    var subMeshIndex = Mathf.Min(i, mesh.subMeshCount - 1);

                    var rparams = new RenderParams(cloneMaterial)
                    {
                        layer = _cloneLayer,
                        lightProbeUsage = renderer.lightProbeUsage,
                        motionVectorMode = renderer.motionVectorGenerationMode,
                        receiveShadows = renderer.receiveShadows,
                        reflectionProbeUsage = renderer.reflectionProbeUsage,
                        rendererPriority = renderer.rendererPriority,
                        renderingLayerMask = renderer.renderingLayerMask,
                        shadowCastingMode = renderer.shadowCastingMode
                    };

                    Graphics.RenderMesh(in rparams, mesh, subMeshIndex, matrix);
                }
            }

            foreach (var renderer in _skinnedMeshRenderers)
            {
                if (!renderer || !renderer.gameObject.activeInHierarchy || !renderer.enabled)
                    continue;

                if (!_bakedSkinnedMeshes.ContainsKey(renderer))
                    _bakedSkinnedMeshes.Add(renderer, new Mesh() { hideFlags = HideFlags.HideAndDontSave });

                var mesh = _bakedSkinnedMeshes[renderer];
                var matrix = transferMatrix * renderer.localToWorldMatrix;

                renderer.BakeMesh(mesh, true);
                renderer.GetMaterials(_materialsBuffer);

                for (int i = 0; i < _materialsBuffer.Count; i++)
                {
                    var material = _materialsBuffer[i];
                    if (!_cloneMaterials.ContainsKey(material))
                        _cloneMaterials.Add(material, new Material(material) { hideFlags = HideFlags.HideAndDontSave });

                    var cloneMaterial = _cloneMaterials[material];
                    cloneMaterial.shader = material.shader;
                    cloneMaterial.CopyPropertiesFromMaterial(material);

                    material.SetVector(PortalSystem.ShaderPropertyID.SlicePlane, slicePlane);
                    cloneMaterial.SetVector(PortalSystem.ShaderPropertyID.SlicePlane, cloneSlicePlane);

                    var subMeshIndex = Mathf.Min(i, mesh.subMeshCount - 1);

                    var rparams = new RenderParams(cloneMaterial)
                    {
                        layer = _cloneLayer,
                        lightProbeUsage = renderer.lightProbeUsage,
                        motionVectorMode = renderer.motionVectorGenerationMode,
                        receiveShadows = renderer.receiveShadows,
                        reflectionProbeUsage = renderer.reflectionProbeUsage,
                        rendererPriority = renderer.rendererPriority,
                        renderingLayerMask = renderer.renderingLayerMask,
                        shadowCastingMode = renderer.shadowCastingMode
                    };

                    Graphics.RenderMesh(in rparams, mesh, subMeshIndex, matrix);
                }
            }
        }

        public int AttachedColliderCount => _attachedColliders.Count;

        public Collider GetAttachedCollider(int index) => _attachedColliders[index];

        /// <summary>
        /// Call it manually when you add a collider component or add a child transform that contains a collider component at runtime.
        /// </summary>
        public void ValidateAttachedColliders()
        {
            _attachedColliders.Clear();

            foreach (var collider in GetComponentsInChildren<Collider>())
            {
                if (collider.attachedRigidbody == _rigidbody)
                    _attachedColliders.Add(collider);
            }
        }

        public void ResetSlicePlane()
        {
            if (!Application.isPlaying)
                return;

            foreach (var renderer in _meshRenderers)
            {
                if (!renderer || !renderer.gameObject.activeInHierarchy || !renderer.enabled)
                    continue;

                renderer.GetMaterials(_materialsBuffer);

                foreach (var material in _materialsBuffer)
                    material.SetVector(PortalSystem.ShaderPropertyID.SlicePlane, Vector4.zero);
            }

            foreach (var renderer in _skinnedMeshRenderers)
            {
                if (!renderer || !renderer.gameObject.activeInHierarchy || !renderer.enabled)
                    continue;

                renderer.GetMaterials(_materialsBuffer);

                foreach (var material in _materialsBuffer)
                    material.SetVector(PortalSystem.ShaderPropertyID.SlicePlane, Vector4.zero);
            }
        }

        public virtual void Validate()
        {
            TryGetComponent(out _rigidbody);
            ValidateAttachedColliders();

            foreach (var mesh in _bakedSkinnedMeshes.Values)
                PortalSystemUtils.SafeDestroy(mesh);

            foreach (var material in _cloneMaterials.Values)
                PortalSystemUtils.SafeDestroy(material);

            _bakedSkinnedMeshes.Clear();
            _cloneMaterials.Clear();

            _candidatePortals.Clear();
            _lastTransferPivot = null;
            _penetratingPortal = null;

            ResetSlicePlane();
        }

#if UNITY_EDITOR
        protected virtual void OnDrawGizmos()
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.TransformPoint(_transferPivotOffset), transform.rotation, Vector3.one);
            Gizmos.color = Color.red;

            Gizmos.DrawSphere(Vector3.zero, .05f);
        }

        protected virtual void OnValidate()
        {
            _cloneLayer = Mathf.Clamp(_cloneLayer, 0, 31);
        }

        protected virtual void Reset() => Validate();
#endif
    }
}
