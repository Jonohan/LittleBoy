using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
#if USING_UNIVERSAL_RENDER_PIPELINE
using UnityEngine.Rendering.Universal;
#endif

namespace Xuwu.FourDimensionalPortals
{
    [Serializable]
    public sealed class PortalSystemRenderSettings
    {
        [SerializeField][Range(0, 7)] private int _recursionLimit = 2;
        [SerializeField] private RenderTextureFormat _renderTextureFormat = RenderTextureFormat.Default;

        public int RecursionLimit
        {
            get => _recursionLimit;
            set => _recursionLimit = Mathf.Clamp(value, 0, 7);
        }

        public RenderTextureFormat RenderTextureFormat
        {
            get => _renderTextureFormat;
            set => _renderTextureFormat = value;
        }

#if USING_UNIVERSAL_RENDER_PIPELINE
        [SerializeField][UniversalRendererField] private int _rendererIndex = -1;

        public int RendererIndex
        {
            get => _rendererIndex;
            set => _rendererIndex = value;
        }
#endif
    }

    [AddComponentMenu("Xuwu/Four Dimensional Portals/Portal System")]
    [DefaultExecutionOrder(500)] //make sure unity callback is executed last.
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    [RequireComponent(typeof(PortalSystem))]
    public sealed partial class PortalSystem : MonoBehaviour
    {
        public const int PerPortalDetectionBudget = 64;
        public const float GhostObjectsCacheExpirationTime = 10f;
        public const float Tolerance = .001f;

        private static readonly List<PortalSystem> s_portalSystems = new();

        public static PortalSystem ActiveInstance
        {
            get => s_portalSystems.Count > 0 ? s_portalSystems[0] : null;
            set
            {
                int index = s_portalSystems.IndexOf(value);

                if (index > 0)
                {
                    s_portalSystems.RemoveAt(index);
                    s_portalSystems.Insert(0, value);

                    GhostObjectManager.Instance.RootTransform = value.transform;
                }
                else if (index < 0)
                {
                    Debug.LogError($"Failed setting PortalSystem.ActiveInstance to unknown PortalSystem {value}.");
                }
            }
        }

        public bool IsValid => s_portalSystems.Count > 0 && s_portalSystems[0] == this;

        [SerializeField] private CameraType _cameraTypeMask = CameraType.Game | CameraType.SceneView;
        [SerializeField] private PortalSystemRenderSettings _renderSettings;

        public CameraType CameraTypeMask
        {
            get => _cameraTypeMask;
            set => _cameraTypeMask = value;
        }

        public PortalSystemRenderSettings RenderSettings => _renderSettings;

        internal static readonly List<Portal> s_activePortals = new();
        internal static readonly List<PortalTraveler> s_activeTravelers = new();

        private static readonly Collider[] s_colliderResultsBuffer = new Collider[PerPortalDetectionBudget];
        private static readonly Dictionary<Rigidbody, RigidbodyGhost> s_rigidbodyGhostsBuffer = new();

        private static readonly Dictionary<int, Plane> s_portalPlanes = new();
        private static readonly Dictionary<int, int> s_portalFrames = new();
        private static readonly Dictionary<int, int> s_penetratingColliders = new();
        private static readonly Dictionary<int, int> s_obstacleColliders = new();

        private static readonly WaitForFixedUpdate s_waitForFixedUpdate = new();
        private Coroutine _waitForFixedUpdateCoroutine;
        private bool _isWaitForFixedUpdateCoroutineExists;

        private Camera _viewCamera;
        private static CommandBuffer s_commandBuffer;

#if USING_UNIVERSAL_RENDER_PIPELINE
        private UniversalAdditionalCameraData _universalCameraData;
        //private readonly UniversalRenderPipeline.SingleCameraRequest _cameraRequest = new();
        private static ScissorRectPass s_scissorRectPass;
        private static DrawPenetratingViewMeshPass s_drawPenetratingViewMeshPass;
#endif

        internal static Shader WriteStencilShader
        {
            get
            {
#if USING_UNIVERSAL_RENDER_PIPELINE
                if (UniversalRenderPipeline.asset)
                {
                    return Shader.Find("Hidden/Xuwu/Four Dimensional Portals/Write Stencil (Universal)");
                }
                else
#endif
                {
                    return Shader.Find("Hidden/Xuwu/Four Dimensional Portals/Write Stencil (Built-In)");
                }
            }
        }

        internal static Shader ViewShader
        {
            get
            {
#if USING_UNIVERSAL_RENDER_PIPELINE
                if (UniversalRenderPipeline.asset)
                {
                    return Shader.Find("Hidden/Xuwu/Four Dimensional Portals/Portal View (Universal)");
                }
                else
#endif
                {
                    return Shader.Find("Hidden/Xuwu/Four Dimensional Portals/Portal View (Built-In)");
                }
            }
        }

        private Material _writeStencilMaterial;
        private Material _stencilViewMaterial;

        private void OnEnable()
        {
            s_portalSystems.Add(this);

            Physics.ContactModifyEvent += ModificationEvent;
            _waitForFixedUpdateCoroutine = StartCoroutine(WaitForFixedUpdateCoroutine());

            RenderPipelineManager.activeRenderPipelineTypeChanged += OnRenderPipelineTypeChanged;
            Camera.onPreCull += OnPreRenderCallback;
            Camera.onPostRender += OnPostRenderCallback;

#if USING_UNIVERSAL_RENDER_PIPELINE
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
#endif

            Validate();
        }

        private void OnDisable()
        {
            s_portalSystems.Remove(this);

            Physics.ContactModifyEvent -= ModificationEvent;
            StopCoroutine(_waitForFixedUpdateCoroutine);

            RenderPipelineManager.activeRenderPipelineTypeChanged -= OnRenderPipelineTypeChanged;
            Camera.onPreCull -= OnPreRenderCallback;
            Camera.onPostRender -= OnPostRenderCallback;

#if USING_UNIVERSAL_RENDER_PIPELINE
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
#endif

            Validate();
        }

        private void OnDestroy()
        {
            if (s_portalSystems.Count == 0)
            {
                GhostObjectManager.FreeMemory();
                PortalRenderingCore.FreeMemory();
            }

            PortalSystemUtils.SafeDestroy(_writeStencilMaterial);
            PortalSystemUtils.SafeDestroy(_stencilViewMaterial);
        }

        private void FixedUpdate()
        {
            if (!IsValid)
                return;

            if (!_isWaitForFixedUpdateCoroutineExists)
            {
                _waitForFixedUpdateCoroutine = StartCoroutine(WaitForFixedUpdateCoroutine());

                Debug.Log($"Found that the WaitForFixedUpdate coroutine stopped unexpectedly, a new coroutine will be started automatically. " +
                    $"Please do not use the PortalSystem instance to call the StopAllCoroutines() method.");
            }

            _isWaitForFixedUpdateCoroutineExists = false;

            UpdateContactModificationPhysicsData();
        }

        private void ModificationEvent(PhysicsScene scene, NativeArray<ModifiableContactPair> pairs)
        {
            if (!IsValid)
                return;

            foreach (var pair in pairs)
            {
                Plane plane;
                int planeKey;
                int otherPlaneKey;

                if (s_obstacleColliders.TryGetValue(pair.colliderInstanceID, out planeKey))
                {
                    if (s_portalPlanes.TryGetValue(planeKey, out plane))
                    {
                        s_obstacleColliders.TryGetValue(pair.otherColliderInstanceID, out otherPlaneKey);

                        if (planeKey == otherPlaneKey)
                        {
                            IgnoreContactsOnPositiveSide();
                            continue;
                        }

                        s_penetratingColliders.TryGetValue(pair.otherColliderInstanceID, out otherPlaneKey);

                        if (planeKey == otherPlaneKey)
                        {
                            IgnoreContactsOnPositiveSide();
                            continue;
                        }
                    }

                    IgnoreContacts();
                }
                else if (s_obstacleColliders.TryGetValue(pair.otherColliderInstanceID, out otherPlaneKey))
                {
                    if (s_portalPlanes.TryGetValue(otherPlaneKey, out plane))
                    {
                        s_obstacleColliders.TryGetValue(pair.colliderInstanceID, out planeKey);

                        if (planeKey == otherPlaneKey)
                        {
                            IgnoreContactsOnPositiveSide();
                            continue;
                        }

                        s_penetratingColliders.TryGetValue(pair.colliderInstanceID, out planeKey);

                        if (planeKey == otherPlaneKey)
                        {
                            IgnoreContactsOnPositiveSide();
                            continue;
                        }
                    }

                    IgnoreContacts();
                }
                else if (s_penetratingColliders.TryGetValue(pair.colliderInstanceID, out planeKey)
                    || s_penetratingColliders.TryGetValue(pair.otherColliderInstanceID, out otherPlaneKey))
                {
                    if (planeKey == otherPlaneKey)
                        continue;

                    if (planeKey == pair.otherColliderInstanceID || otherPlaneKey == pair.colliderInstanceID)
                    {
                        IgnoreContacts();
                        continue;
                    }

                    int frameColliderInstanceID;

                    if (s_portalFrames.TryGetValue(planeKey, out frameColliderInstanceID))
                    {
                        if (frameColliderInstanceID == pair.otherColliderInstanceID)
                            continue;
                    }

                    if (s_portalFrames.TryGetValue(otherPlaneKey, out frameColliderInstanceID))
                    {
                        if (frameColliderInstanceID == pair.colliderInstanceID)
                            continue;
                    }

                    if (s_portalPlanes.TryGetValue(planeKey, out plane))
                        IgnoreContactsOnNegativeSide();

                    if (s_portalPlanes.TryGetValue(otherPlaneKey, out plane))
                        IgnoreContactsOnNegativeSide();
                }

                void IgnoreContacts()
                {
                    for (int i = 0; i < pair.contactCount; i++)
                        pair.IgnoreContact(i);
                }

                void IgnoreContactsOnPositiveSide()
                {
                    for (int i = 0; i < pair.contactCount; i++)
                    {
                        if (plane.GetSide(pair.GetPoint(i)))
                            pair.IgnoreContact(i);
                    }
                }

                void IgnoreContactsOnNegativeSide()
                {
                    for (int i = 0; i < pair.contactCount; i++)
                    {
                        if (!plane.GetSide(pair.GetPoint(i)))
                            pair.IgnoreContact(i);
                    }
                }
            }
        }

        private IEnumerator WaitForFixedUpdateCoroutine()
        {
            while (true)
            {
                _isWaitForFixedUpdateCoroutineExists = true;
                yield return s_waitForFixedUpdate;

                if (IsValid)
                    UpdateContactModificationPhysicsData();
            }
        }

        private static void UpdateContactModificationPhysicsData()
        {
            GhostObjectManager.Instance.CollectAndResetGhostObjectInstances();

            Physics.SyncTransforms();

            s_activeTravelers.ForEach(static traveler => traveler.ResolveCandidatePortals());

            Physics.SyncTransforms();

            foreach (var portal in s_activePortals)
            {
                portal._detectionZoneColliders.Clear();
                portal._penetratingColliders.Clear();

                if (!portal.IsWorkable())
                    continue;

                var localBounds = portal.Config.PlaneMesh.bounds;
                localBounds.center = new Vector3(localBounds.center.x, localBounds.center.y, .5f * portal.DetectionZoneScale.z + Tolerance);
                localBounds.extents = Vector3.Scale(new Vector3(localBounds.extents.x + .05f, localBounds.extents.y + .05f, .5f), portal.DetectionZoneScale);

                var center = portal.transform.TransformPoint(localBounds.center);
                var halfExtents = Vector3.Scale(localBounds.extents, portal.transform.lossyScale);

                portal.PlaneMeshCollider.enabled = false;
                portal.DetectionMeshCollider.enabled = false;
                portal.FrameMeshCollider.enabled = false;

                int overlapCount = Physics.OverlapBoxNonAlloc(center, halfExtents, s_colliderResultsBuffer, portal.transform.rotation, Physics.AllLayers, QueryTriggerInteraction.Ignore);

                portal.PlaneMeshCollider.enabled = true;
                portal.DetectionMeshCollider.enabled = true;
                portal.FrameMeshCollider.enabled = true;

                for (int i = 0; i < overlapCount; i++)
                    portal._detectionZoneColliders.Add(s_colliderResultsBuffer[i]);
            }

            s_activeTravelers.ForEach(static traveler => traveler.UpdateCandidatePortals());

            s_portalPlanes.Clear();
            s_portalFrames.Clear();
            s_penetratingColliders.Clear();
            s_obstacleColliders.Clear();

            foreach (var portal in s_activePortals)
            {
                s_portalPlanes.Add(portal.PlaneMeshCollider.GetInstanceID(), portal.GetPlane());
                s_portalFrames.Add(portal.PlaneMeshCollider.GetInstanceID(), portal.FrameMeshCollider.GetInstanceID());

                if (portal._detectionZoneColliders.Count == 0 && portal._penetratingColliders.Count == 0)
                    continue;

                var transferMatrix = portal.GetTransferMatrix();

                s_rigidbodyGhostsBuffer.Clear();

                RigidbodyGhost CreateRigidbodyGhost(Rigidbody rigidbody)
                {
                    RigidbodyGhost rigidbodyGhost = null;

                    if (rigidbody && !s_rigidbodyGhostsBuffer.TryGetValue(rigidbody, out rigidbodyGhost))
                    {
                        rigidbodyGhost = GhostObjectManager.Instance.CreateRigidbodyGhost(rigidbody);

                        if (rigidbodyGhost)
                        {
                            rigidbodyGhost.gameObject.layer = rigidbody.gameObject.layer;
                            rigidbodyGhost.AttachedPortal = portal.LinkedPortal;

                            var localToWorldMatrix = transferMatrix * rigidbody.transform.localToWorldMatrix;

                            rigidbodyGhost.transform.parent = null;
                            rigidbodyGhost.transform.SetPositionAndRotation(localToWorldMatrix.GetPosition(), localToWorldMatrix.rotation);
                            rigidbodyGhost.transform.localScale = localToWorldMatrix.lossyScale;
                            rigidbodyGhost.transform.parent = portal.LinkedPortal.transform;

                            rigidbodyGhost.Rigidbody.position = rigidbodyGhost.transform.position;
                            rigidbodyGhost.Rigidbody.rotation = rigidbodyGhost.transform.rotation;
                            rigidbodyGhost.Rigidbody.mass *= transferMatrix.lossyScale.x * transferMatrix.lossyScale.y * transferMatrix.lossyScale.z;

                            if (!rigidbodyGhost.Rigidbody.isKinematic)
                            {
                                rigidbodyGhost.Rigidbody.angularVelocity = transferMatrix.MultiplyVector(rigidbody.angularVelocity);
                                rigidbodyGhost.Rigidbody.velocity = transferMatrix.MultiplyVector(rigidbody.velocity);
                            }

                            s_rigidbodyGhostsBuffer.Add(rigidbody, rigidbodyGhost);
                        }
                    }

                    return rigidbodyGhost;
                }

                ColliderGhost CreateColliderGhost(Collider collider)
                {
                    var colliderGhost = GhostObjectManager.Instance.CreateColliderGhost(collider);

                    if (colliderGhost)
                    {
                        colliderGhost.gameObject.layer = collider.gameObject.layer;
                        colliderGhost.AttachedPortal = portal.LinkedPortal;

                        var localToWorldMatrix = transferMatrix * collider.transform.localToWorldMatrix;
                        colliderGhost.transform.parent = null;
                        colliderGhost.transform.SetPositionAndRotation(localToWorldMatrix.GetPosition(), localToWorldMatrix.rotation);
                        colliderGhost.transform.localScale = localToWorldMatrix.lossyScale;
                    }

                    return colliderGhost;
                }

                foreach (var collider in portal._penetratingColliders)
                {
                    if (s_penetratingColliders.ContainsKey(collider.GetInstanceID()))
                        continue;

                    s_penetratingColliders.Add(collider.GetInstanceID(), portal.PlaneMeshCollider.GetInstanceID());

                    var colliderGhost = CreateColliderGhost(collider);
                    if (!colliderGhost)
                        continue;

                    colliderGhost.transform.parent = portal.LinkedPortal.transform;
                    s_penetratingColliders.Add(colliderGhost.Collider.GetInstanceID(), portal.LinkedPortal.PlaneMeshCollider.GetInstanceID());

                    var rigidbodyGhost = CreateRigidbodyGhost(collider.attachedRigidbody);
                    if (!rigidbodyGhost)
                        continue;

                    colliderGhost.transform.parent = rigidbodyGhost.transform;
                }

                if (portal.LinkedPortal._penetratingColliders.Count == 0)
                    continue;

                foreach (var collider in portal._detectionZoneColliders)
                {
                    var rigidbody = collider.attachedRigidbody;
                    if (rigidbody)
                        rigidbody.transform.SetPositionAndRotation(rigidbody.position, rigidbody.rotation);

                    var colliderGhost = CreateColliderGhost(collider);
                    if (!colliderGhost)
                        continue;

                    colliderGhost.transform.parent = portal.LinkedPortal.transform;
                    s_obstacleColliders.Add(colliderGhost.Collider.GetInstanceID(), portal.LinkedPortal.PlaneMeshCollider.GetInstanceID());

                    var rigidbodyGhost = CreateRigidbodyGhost(rigidbody);
                    if (!rigidbodyGhost)
                        continue;

                    colliderGhost.transform.parent = rigidbodyGhost.transform;
                }
            }

            foreach (var portal in s_activePortals)
            {
                portal._detectionZoneColliders.Clear();
                portal._penetratingColliders.Clear();
            }

            GhostObjectManager.Instance.DestroyInactiveGhostObjectInstances(GhostObjectsCacheExpirationTime);
        }

        public static bool IsRaycastHitValid(RaycastHit raycastHit)
        {
            if (s_obstacleColliders.TryGetValue(raycastHit.colliderInstanceID, out _))
                return false;

            if (s_penetratingColliders.TryGetValue(raycastHit.colliderInstanceID, out int planeKey))
            {
                if (s_portalPlanes.TryGetValue(planeKey, out var plane))
                    return plane.GetSide(raycastHit.point);
            }

            return true;
        }

        public static bool IsCollisionValid(Collider collider, Collider otherCollider, Vector3 point)
        {
            if (!collider || !otherCollider)
                return false;

            if (Physics.GetIgnoreLayerCollision(collider.gameObject.layer, otherCollider.gameObject.layer))
                return false;

            if (Physics.GetIgnoreCollision(collider, otherCollider))
                return false;

            int colliderInstanceID = collider.GetInstanceID();
            int otherColliderInstanceID = otherCollider.GetInstanceID();

            Plane plane;
            int planeKey;
            int otherPlaneKey;

            if (s_obstacleColliders.TryGetValue(colliderInstanceID, out planeKey))
            {
                if (s_portalPlanes.TryGetValue(planeKey, out plane))
                {
                    s_obstacleColliders.TryGetValue(otherColliderInstanceID, out otherPlaneKey);

                    if (planeKey == otherPlaneKey)
                        return !plane.GetSide(point);

                    s_penetratingColliders.TryGetValue(otherColliderInstanceID, out otherPlaneKey);

                    if (planeKey == otherPlaneKey)
                        return !plane.GetSide(point);
                }

                return false;
            }
            else if (s_obstacleColliders.TryGetValue(otherColliderInstanceID, out otherPlaneKey))
            {
                if (s_portalPlanes.TryGetValue(otherPlaneKey, out plane))
                {
                    s_obstacleColliders.TryGetValue(colliderInstanceID, out planeKey);

                    if (planeKey == otherPlaneKey)
                        return !plane.GetSide(point);

                    s_penetratingColliders.TryGetValue(colliderInstanceID, out planeKey);

                    if (planeKey == otherPlaneKey)
                        return !plane.GetSide(point);
                }

                return false;
            }
            else if (s_penetratingColliders.TryGetValue(colliderInstanceID, out planeKey)
                || s_penetratingColliders.TryGetValue(otherColliderInstanceID, out otherPlaneKey))
            {
                if (planeKey == otherPlaneKey)
                    return true;

                if (planeKey == otherColliderInstanceID || otherPlaneKey == colliderInstanceID)
                    return false;

                int frameColliderInstanceID;

                if (s_portalFrames.TryGetValue(planeKey, out frameColliderInstanceID))
                {
                    if (frameColliderInstanceID == otherColliderInstanceID)
                        return true;
                }

                if (s_portalFrames.TryGetValue(otherPlaneKey, out frameColliderInstanceID))
                {
                    if (frameColliderInstanceID == colliderInstanceID)
                        return true;
                }

                if (s_portalPlanes.TryGetValue(planeKey, out plane))
                    return plane.GetSide(point);

                if (s_portalPlanes.TryGetValue(otherPlaneKey, out plane))
                    return plane.GetSide(point);
            }

            return true;
        }

        private void LateUpdate()
        {
            if (!IsValid)
                return;

#if UNITY_EDITOR
            if (s_portalSystems.Count > 1)
                Debug.LogWarning($"There are {s_portalSystems.Count} PortalSystem in the scene. Please make sure there is always only 1 PortalSystem.");
#endif

            if (Application.isPlaying)
                s_activeTravelers.ForEach(static traveler => traveler.UpdateRenderers());
        }

        private void InitializeCamera(Camera camera, out PortalSystemRenderSettings renderSettings,
            out int viewCullingMask, out int penetratingViewCullingMask, out Portal penetratingPortal)
        {
            renderSettings = _renderSettings;
            viewCullingMask = camera.cullingMask;
            penetratingViewCullingMask = camera.cullingMask;
            penetratingPortal = null;

            if (camera.TryGetComponent(out PortalSystemAdditionalCameraData cameraData))
            {
                renderSettings = cameraData.OverrideRenderSettings;
                viewCullingMask = cameraData.ViewCullingMask;
                penetratingViewCullingMask = cameraData.PenetratingViewCullingMask;

                if (cameraData.PenetratingPortal && cameraData.PenetratingPortal.IsWorkable())
                    penetratingPortal = cameraData.PenetratingPortal.Config.PenetratingViewMesh ? cameraData.PenetratingPortal : null;
            }

            _viewCamera.CopyFrom(camera);
            _viewCamera.allowMSAA = false;
            _viewCamera.rect = new Rect(0f, 0f, 1f, 1f);
            _viewCamera.ResetAspect();
            _viewCamera.ResetWorldToCameraMatrix();
        }

        private void OnRenderPipelineTypeChanged()
        {
            if (!IsValid)
                return;

            Validate();

            foreach (var portal in FindObjectsOfType<Portal>(true))
                portal.Validate();
        }

        private void OnPreRenderCallback(Camera camera)
        {
            if (!IsValid || camera == _viewCamera || (camera.cameraType & _cameraTypeMask) == 0)
                return;

            InitializeCamera(camera, out var renderSettings, out int viewCullingMask,
                out int penetratingViewCullingMask, out var penetratingPortal);

            var desc = new RenderTextureDescriptor(camera.pixelWidth, camera.pixelHeight, renderSettings.RenderTextureFormat, 32);

            PortalRenderingCore.Instance.Initialize(camera, renderSettings.RecursionLimit, penetratingPortal);

            while (PortalRenderingCore.Instance.MoveNext())
            {
                ref var currentData = ref PortalRenderingCore.Instance.Current;
                currentData.RenderTexture = RenderTexture.GetTemporary(desc);

                if (currentData.Depth == 1 && currentData.Portal == penetratingPortal)
                {
                    _stencilViewMaterial.mainTexture = currentData.RenderTexture;
                    _viewCamera.cullingMask = penetratingViewCullingMask;
                }
                else
                {
                    _viewCamera.cullingMask = viewCullingMask;
                }

                _viewCamera.transform.SetPositionAndRotation(currentData.LocalToWorldMatrix.GetPosition(), currentData.LocalToWorldMatrix.rotation);
                _viewCamera.targetTexture = currentData.RenderTexture;
                _viewCamera.nearClipPlane = currentData.NearClipPlane;
                _viewCamera.farClipPlane = currentData.FarClipPlane;
                _viewCamera.cullingMatrix = currentData.CullingMatrix;
                _viewCamera.projectionMatrix = currentData.ProjectionMatrix;

                PortalRenderingCore.Instance.SetupPortalsBeforeCameraRendering();

                _viewCamera.Render();
                _viewCamera.targetTexture = null;

                PortalRenderingCore.Instance.SetupPortalsAfterCameraRendering();
            }

            PortalRenderingCore.Instance.SetupPortalsBeforeCameraRendering();

            _viewCamera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            _viewCamera.transform.localScale = Vector3.one;

            s_commandBuffer.Clear();

            if (penetratingPortal)
            {
                var planeMesh = penetratingPortal.Config.PlaneMesh;
                var viewMesh = penetratingPortal.Config.PenetratingViewMesh;
                var localToWorldMatrix = penetratingPortal.transform.localToWorldMatrix;

                s_commandBuffer.ClearRenderTarget(RTClearFlags.Stencil, Color.clear, 1f, 0);
                s_commandBuffer.DrawMesh(planeMesh, localToWorldMatrix, _writeStencilMaterial, 0, 0);
                s_commandBuffer.DrawMesh(viewMesh, localToWorldMatrix, _writeStencilMaterial, 0, 0);
                s_commandBuffer.DrawMesh(viewMesh, localToWorldMatrix, _stencilViewMaterial, 0, 0);
                s_commandBuffer.ClearRenderTarget(RTClearFlags.Stencil, Color.clear, 1f, 0);
            }

            camera.AddCommandBuffer(CameraEvent.AfterForwardAlpha, s_commandBuffer);
        }

        private void OnPostRenderCallback(Camera camera)
        {
            if (!IsValid || camera == _viewCamera || (camera.cameraType & _cameraTypeMask) == 0)
                return;

            camera.RemoveCommandBuffer(CameraEvent.AfterForwardAlpha, s_commandBuffer);
            _stencilViewMaterial.mainTexture = null;

            PortalRenderingCore.Instance.SetupPortalsAfterCameraRendering();
            PortalRenderingCore.Instance.Release();
        }

#if USING_UNIVERSAL_RENDER_PIPELINE
        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (!IsValid || camera == _viewCamera || (camera.cameraType & _cameraTypeMask) == 0)
                return;

            InitializeCamera(camera, out var renderSettings, out int viewCullingMask,
                out int penetratingViewCullingMask, out var penetratingPortal);

            var universalCameraData = camera.GetUniversalAdditionalCameraData();
            var scriptableRenderer = universalCameraData.scriptableRenderer;

            _universalCameraData.SetRenderer(renderSettings.RendererIndex);
            _universalCameraData.renderShadows = universalCameraData.renderShadows;
            _universalCameraData.requiresColorOption = universalCameraData.requiresColorOption;
            _universalCameraData.requiresDepthOption = universalCameraData.requiresDepthOption;
            _universalCameraData.renderType = CameraRenderType.Base;
            _universalCameraData.renderPostProcessing = false;
            _universalCameraData.antialiasing = AntialiasingMode.None;
            _universalCameraData.antialiasingQuality = AntialiasingQuality.Low;
            _universalCameraData.stopNaN = universalCameraData.stopNaN;
            _universalCameraData.dithering = universalCameraData.dithering;
            _universalCameraData.allowXRRendering = universalCameraData.allowXRRendering;

            var desc = new RenderTextureDescriptor(camera.pixelWidth, camera.pixelHeight, renderSettings.RenderTextureFormat, 32);

            PortalRenderingCore.Instance.Initialize(camera, renderSettings.RecursionLimit, penetratingPortal);

            while (PortalRenderingCore.Instance.MoveNext())
            {
                ref var currentData = ref PortalRenderingCore.Instance.Current;
                currentData.RenderTexture = RenderTexture.GetTemporary(desc);

                if (currentData.Depth == 1 && currentData.Portal == penetratingPortal)
                {
                    _stencilViewMaterial.mainTexture = currentData.RenderTexture;
                    _viewCamera.cullingMask = penetratingViewCullingMask;
                }
                else
                {
                    _viewCamera.cullingMask = viewCullingMask;
                }

                _viewCamera.transform.SetPositionAndRotation(currentData.LocalToWorldMatrix.GetPosition(), currentData.LocalToWorldMatrix.rotation);
                _viewCamera.nearClipPlane = currentData.NearClipPlane;
                _viewCamera.farClipPlane = currentData.FarClipPlane;
                _viewCamera.cullingMatrix = currentData.CullingMatrix;
                _viewCamera.projectionMatrix = currentData.ProjectionMatrix;

                s_scissorRectPass.ViewportRect = currentData.ViewportRect;
                scriptableRenderer.EnqueuePass(s_scissorRectPass);

                PortalRenderingCore.Instance.SetupPortalsBeforeCameraRendering();

#if UNITY_2022_2_OR_NEWER
#pragma warning disable 0618
                _viewCamera.targetTexture = currentData.RenderTexture;
                UniversalRenderPipeline.RenderSingleCamera(context, _viewCamera);
                _viewCamera.targetTexture = null;
#pragma warning restore 0618

                //_cameraRequest.destination = currentData.RenderTexture;
                //RenderPipeline.SubmitRenderRequest(_viewCamera, _cameraRequest);
#else
                _viewCamera.targetTexture = currentData.RenderTexture;
                UniversalRenderPipeline.RenderSingleCamera(context, _viewCamera);
                _viewCamera.targetTexture = null;
#endif

                PortalRenderingCore.Instance.SetupPortalsAfterCameraRendering();
            }

            PortalRenderingCore.Instance.SetupPortalsBeforeCameraRendering();

            _viewCamera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            _viewCamera.transform.localScale = Vector3.one;

            s_drawPenetratingViewMeshPass.PenetratingPortal = penetratingPortal;
            s_drawPenetratingViewMeshPass.WriteStencilMaterial = _writeStencilMaterial;
            s_drawPenetratingViewMeshPass.StencilViewMaterial = _stencilViewMaterial;

            scriptableRenderer.EnqueuePass(s_drawPenetratingViewMeshPass);
        }

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (!IsValid || camera == _viewCamera || (camera.cameraType & _cameraTypeMask) == 0)
                return;

            _stencilViewMaterial.mainTexture = null;

            PortalRenderingCore.Instance.SetupPortalsAfterCameraRendering();
            PortalRenderingCore.Instance.Release();
        }
#endif

        public void Validate()
        {
            TryGetComponent(out _viewCamera);

            _viewCamera.enabled = false;
            _viewCamera.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector | HideFlags.NotEditable;

            s_commandBuffer ??= new CommandBuffer() { name = "DrawPenetratingViewMesh" };

#if USING_UNIVERSAL_RENDER_PIPELINE
            if (UniversalRenderPipeline.asset)
            {
                _universalCameraData = _viewCamera.GetUniversalAdditionalCameraData();
                _universalCameraData.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector | HideFlags.NotEditable;
            }

            s_scissorRectPass ??= new ScissorRectPass();
            s_drawPenetratingViewMeshPass ??= new DrawPenetratingViewMeshPass();
#endif
            if (_writeStencilMaterial)
                _writeStencilMaterial.shader = WriteStencilShader;
            else
                _writeStencilMaterial = new Material(WriteStencilShader) { hideFlags = HideFlags.HideAndDontSave };

            _writeStencilMaterial.SetFloat(ShaderPropertyID.StencilRef, 1f);

            if (_stencilViewMaterial)
                _stencilViewMaterial.shader = ViewShader;
            else
                _stencilViewMaterial = new Material(ViewShader) { hideFlags = HideFlags.HideAndDontSave };

            _stencilViewMaterial.SetFloat(ShaderPropertyID.CullMode, (float)CullMode.Front);
            _stencilViewMaterial.SetFloat(ShaderPropertyID.StencilRef, 1f);
            _stencilViewMaterial.SetFloat(ShaderPropertyID.StencilComp, 6f);
            _stencilViewMaterial.SetFloat(ShaderPropertyID.ZTest, 8f);
            _stencilViewMaterial.SetFloat(ShaderPropertyID.ZWrite, 0f);

            transform.hideFlags = HideFlags.NotEditable;
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = Vector3.one;

            if (s_portalSystems.Count != 0)
            {
                GhostObjectManager.Instance.RootTransform = s_portalSystems[0].transform;
            }
            else
            {
                s_activeTravelers.ForEach(static traveler => traveler.Validate());

                s_portalPlanes.Clear();
                s_portalFrames.Clear();
                s_penetratingColliders.Clear();
                s_obstacleColliders.Clear();
            }
        }

#if UNITY_EDITOR
        private void Reset() => Validate();
#endif
    }
}
