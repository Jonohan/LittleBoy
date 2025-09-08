using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

namespace Xuwu.FourDimensionalPortals
{
    [AddComponentMenu("Xuwu/Four Dimensional Portals/Portal")]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(Portal))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Portal : MonoBehaviour
    {
        [SerializeField] private PortalConfig _config;
        [SerializeField] private Portal _linkedPortal;
        [SerializeField] private Material _overrideEndMaterial;
        [SerializeField] private Material _customViewMaterial;
        [SerializeField] private Vector3 _detectionZoneScale = Vector3.one;
        [SerializeField] private bool _useObliqueProjectionMatrix = true;
        [SerializeField] private UnityEvent<PortalTraveler> _onTravellerTransferFromLinkedPortal;
        [SerializeField] private UnityEvent<PortalTraveler> _onTravellerTransferToLinkedPortal;

        public PortalConfig Config
        {
            get => _config;
            set
            {
                if (_config != value)
                {
                    if (_linkedPortal)
                        _linkedPortal._linkedPortal = null;
                    _linkedPortal = null;
                }

                _config = value;
                Validate();
            }
        }

        public Portal LinkedPortal => _linkedPortal;

        public Material OverrideEndMaterial
        {
            get => _overrideEndMaterial;
            set => _overrideEndMaterial = value;
        }

        public Material CustomViewMaterial
        {
            get => _customViewMaterial;
            set => _customViewMaterial = value;
        }

        public Vector3 DetectionZoneScale
        {
            get => _detectionZoneScale;
            set => _detectionZoneScale = Vector3.Max(Vector3.one, value);
        }

        public bool UseObliqueProjectionMatrix
        {
            get => _useObliqueProjectionMatrix;
            set => _useObliqueProjectionMatrix = value;
        }

        public UnityEvent<PortalTraveler> OnTravellerTransferFromLinkedPortal => _onTravellerTransferFromLinkedPortal;

        public UnityEvent<PortalTraveler> OnTravellerTransferToLinkedPortal => _onTravellerTransferToLinkedPortal;

        internal readonly HashSet<Collider> _detectionZoneColliders = new();
        internal readonly HashSet<Collider> _penetratingColliders = new();

        private MeshCollider _planeMeshCollider;
        private MeshCollider _detectionMeshCollider;
        private MeshCollider _frameMeshCollider;

        public MeshCollider PlaneMeshCollider => _planeMeshCollider;
        public MeshCollider DetectionMeshCollider => _detectionMeshCollider;
        public MeshCollider FrameMeshCollider => _frameMeshCollider;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Rigidbody _planeRigidbody;

        public int PlaneRigidbodyInstanceID => _planeRigidbody.GetInstanceID();

        private Material _defaultViewMaterial;

        private void OnEnable()
        {
            PortalSystem.s_activePortals.Add(this);
            Validate();
        }

        private void OnDisable()
        {
            PortalSystem.s_activePortals.Remove(this);
            Validate();
        }

        private void OnDestroy()
        {
            PortalSystemUtils.SafeDestroy(_defaultViewMaterial);
        }

        public bool IsWorkable() => _config && _config.PlaneMesh && _linkedPortal && _linkedPortal != this
            && isActiveAndEnabled && _linkedPortal.isActiveAndEnabled
            && _linkedPortal._config == _config && _linkedPortal._linkedPortal == this
            && PortalSystemUtils.IsScalingUniform(transform.lossyScale)
            && PortalSystemUtils.IsScalingUniform(_linkedPortal.transform.lossyScale)
            && Vector3.Dot(Vector3.one, transform.lossyScale) / 3f > 0f
            && Vector3.Dot(Vector3.one, _linkedPortal.transform.lossyScale) / 3f > 0f;

        public Plane GetPlane(float zOffset = 0f) => new(transform.forward, transform.position + transform.forward * zOffset);

        public Vector4 GetVector4Plane(float zOffset = 0f) => new(transform.forward.x, transform.forward.y, transform.forward.z,
            0f - Vector3.Dot(transform.forward, transform.position + transform.forward * zOffset));

        public Matrix4x4 GetTransferMatrix() => _linkedPortal ? _linkedPortal.transform.localToWorldMatrix
            * Matrix4x4.Rotate(Quaternion.Euler(0f, 180f, 0f)) * transform.worldToLocalMatrix : Matrix4x4.identity;

        public Vector3 TransferDirection(Vector3 direction) => _linkedPortal ? _linkedPortal.transform.TransformDirection(
            Quaternion.Euler(0f, 180f, 0f) * transform.InverseTransformDirection(direction)) : direction;

        public Vector3 TransferPoint(Vector3 position) => _linkedPortal ? _linkedPortal.transform.TransformPoint(
            Quaternion.Euler(0f, 180f, 0f) * transform.InverseTransformPoint(position)) : position;

        public Vector3 TransferVector(Vector3 vector) => _linkedPortal ? _linkedPortal.transform.TransformVector(
            Quaternion.Euler(0f, 180f, 0f) * transform.InverseTransformVector(vector)) : vector;

        public Quaternion TransferRotation(Quaternion rotation) => _linkedPortal ? _linkedPortal.transform.rotation
            * Quaternion.Euler(0f, 180f, 0f) * Quaternion.Inverse(transform.rotation) * rotation : rotation;

        public bool LinkPortal(Portal portal)
        {
            if (!_config)
            {
                Debug.LogWarning($"Config of ({name}) is missing, unable to link portal.");
                return false;
            }

            if (portal is null)
            {
                if (_linkedPortal && _linkedPortal._linkedPortal == this)
                    _linkedPortal._linkedPortal = null;

                _linkedPortal = null;
                return true;
            }

            if (!portal)
            {
                Debug.LogWarning($"({name}) cannot link to ({portal.name}), because ({portal.name}) has been destroyed.");
                return false;
            }

            if (portal == this)
            {
                Debug.LogWarning($"({name}) cannot link to itself.");
                return false;
            }

            if (!portal._config)
            {
                Debug.LogWarning($"({name}) cannot link to ({portal.name}), because config of ({portal.name}) is missing.");
                return false;
            }

            if (_config != portal._config)
            {
                Debug.LogWarning($"({name}) cannot link to ({portal.name}), because their config are different.");
                return false;
            }

            if (_linkedPortal && _linkedPortal._linkedPortal == this)
                _linkedPortal._linkedPortal = null;

            if (portal._linkedPortal && portal._linkedPortal._linkedPortal == portal)
                portal._linkedPortal._linkedPortal = null;

            _linkedPortal = portal;
            _linkedPortal._linkedPortal = this;

            return true;
        }

        public void Validate()
        {
            var meshColliders = GetComponents<MeshCollider>();
            while (meshColliders.Length < 3)
            {
                gameObject.AddComponent<MeshCollider>();
                meshColliders = GetComponents<MeshCollider>();
            }

            _planeMeshCollider = meshColliders[0];
            _planeMeshCollider.enabled = enabled;
            _planeMeshCollider.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector | HideFlags.NotEditable;
            _planeMeshCollider.convex = false;

            _detectionMeshCollider = meshColliders[1];
            _detectionMeshCollider.enabled = enabled;
            _detectionMeshCollider.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector | HideFlags.NotEditable;
            _detectionMeshCollider.convex = true;
            _detectionMeshCollider.isTrigger = true;

            _frameMeshCollider = meshColliders[2];
            _frameMeshCollider.enabled = enabled;
            _frameMeshCollider.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector | HideFlags.NotEditable;
            _frameMeshCollider.convex = false;

            TryGetComponent(out _meshFilter);
            TryGetComponent(out _meshRenderer);
            TryGetComponent(out _planeRigidbody);

            _meshFilter.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;

            _meshRenderer.enabled = enabled;
            _meshRenderer.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
            _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _meshRenderer.allowOcclusionWhenDynamic = false;
            _meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            _meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            _planeRigidbody.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector | HideFlags.NotEditable;
            _planeRigidbody.useGravity = false;
            _planeRigidbody.isKinematic = true;
            _planeRigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;

            if (_config)
            {
                _planeMeshCollider.sharedMesh = _config.PlaneMesh;
                _detectionMeshCollider.sharedMesh = _config.DetectionMesh;
                _frameMeshCollider.sharedMesh = _config.FrameMesh;

                _meshFilter.sharedMesh = _config.PlaneMesh;
                _meshRenderer.sharedMaterial = _overrideEndMaterial ? _overrideEndMaterial : _config.EndMaterial;
            }
            else
            {
                _planeMeshCollider.sharedMesh = null;
                _detectionMeshCollider.sharedMesh = null;
                _frameMeshCollider.sharedMesh = null;

                _meshFilter.sharedMesh = null;
                _meshRenderer.sharedMaterial = null;
            }

            if (_defaultViewMaterial)
                _defaultViewMaterial.shader = PortalSystem.ViewShader;
            else
                _defaultViewMaterial = new Material(PortalSystem.ViewShader) { hideFlags = HideFlags.HideAndDontSave };

            _defaultViewMaterial.SetFloat(PortalSystem.ShaderPropertyID.CullMode, (float)CullMode.Back);
            _defaultViewMaterial.SetFloat(PortalSystem.ShaderPropertyID.StencilRef, 1f);
            _defaultViewMaterial.SetFloat(PortalSystem.ShaderPropertyID.StencilComp, 0f);
            _defaultViewMaterial.SetFloat(PortalSystem.ShaderPropertyID.ZTest, 4f);
            _defaultViewMaterial.SetFloat(PortalSystem.ShaderPropertyID.ZWrite, 1f);

            _detectionZoneScale = Vector3.Max(Vector3.one, _detectionZoneScale);

            if (!_linkedPortal)
                return;

            if (_linkedPortal._linkedPortal != this)
            {
                _linkedPortal = null;
                return;
            }

            _linkedPortal._linkedPortal = this;

            if (!_config || _linkedPortal._config != _config)
            {
                _linkedPortal._linkedPortal = null;
                _linkedPortal = null;
            }
        }

        internal void SyncTransform()
        {
            _planeRigidbody.position = transform.position;
            _planeRigidbody.rotation = transform.rotation;
        }

        internal void SetViewRenderTexture(RenderTexture renderTexture)
        {
            if (renderTexture)
            {
                _meshRenderer.sharedMaterial = _customViewMaterial ? _customViewMaterial : _defaultViewMaterial;
                _meshRenderer.sharedMaterial.mainTexture = renderTexture;
            }
            else
            {
                _meshRenderer.sharedMaterial = _overrideEndMaterial ? _overrideEndMaterial : _config.EndMaterial;

                if (_customViewMaterial)
                    _customViewMaterial.mainTexture = null;

                _defaultViewMaterial.mainTexture = null;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!IsWorkable())
                return;

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 1f, 1f, .5f);

            var localBounds = _config.PlaneMesh.bounds;
            localBounds.center = new Vector3(localBounds.center.x, localBounds.center.y, .5f * _detectionZoneScale.z);
            localBounds.extents = Vector3.Scale(new Vector3(localBounds.extents.x + .05f, localBounds.extents.y + .05f, .5f), _detectionZoneScale);

            Gizmos.DrawWireCube(localBounds.center, localBounds.size);
        }

        private void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this)
                    Validate();
            };

            _detectionZoneScale = Vector3.Max(Vector3.one, _detectionZoneScale);
        }

        private void Reset() => Validate();
#endif
    }
}
