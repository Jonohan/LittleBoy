using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Xuwu.FourDimensionalPortals
{
    partial class PortalSystem
    {
        public static class ShaderPropertyID
        {
            public static readonly int SlicePlane = Shader.PropertyToID("_SlicePlane");
            public static readonly int CullMode = Shader.PropertyToID("_CullMode");
            public static readonly int StencilRef = Shader.PropertyToID("_StencilRef");
            public static readonly int StencilComp = Shader.PropertyToID("_StencilComp");
            public static readonly int ZTest = Shader.PropertyToID("_ZTest");
            public static readonly int ZWrite = Shader.PropertyToID("_ZWrite");
        }

        private struct PortalCameraData
        {
            private Matrix4x4 _localToWorldMatrix;
            private Matrix4x4 _worldToCameraMatrix;
            private Rect _viewportRect;

            public RenderTexture RenderTexture;
            public Portal Portal;
            public int Depth;
            public Matrix4x4 LocalToWorldMatrix
            {
                get => _localToWorldMatrix;
                set
                {
                    _localToWorldMatrix = Matrix4x4.TRS(value.GetPosition(), value.rotation, Vector3.one);
                    _worldToCameraMatrix = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * _localToWorldMatrix.inverse;
                }
            }
            public Matrix4x4 WorldToCameraMatrix => _worldToCameraMatrix;
            public Rect ViewportRect
            {
                get => _viewportRect;
                set
                {
                    value.size = Vector2.Max(Vector2.one * .02f, value.size);
                    _viewportRect = value;
                }
            }
            public float NearClipPlane;
            public float FarClipPlane;
            public Matrix4x4 CullingMatrix;
            public Matrix4x4 ProjectionMatrix;
        }

        private sealed class PortalRenderingCore
        {
            private class PortalCameraNode
            {
                private PortalCameraData _data = default;

                public ref PortalCameraData Data => ref _data;
                public PortalCameraNode FirstChild { get; private set; } = null;
                public PortalCameraNode NextSibling { get; private set; } = null;

                public void AddChild(PortalCameraNode node)
                {
                    node.NextSibling = FirstChild;
                    FirstChild = node;
                }

                public void Reset()
                {
                    Data = default;
                    FirstChild = null;
                    NextSibling = null;
                }
            }

            private static PortalRenderingCore s_instance = null;

            public static PortalRenderingCore Instance => s_instance ??= new PortalRenderingCore();

            public static void FreeMemory() => s_instance = null;

            private readonly PortalCameraNode _rootNode = new();
            private readonly ObjectPool<PortalCameraNode> _nodePool = new(() => new PortalCameraNode(), null, x => x.Reset());
            private readonly Stack<PortalCameraNode> _nodeStack = new();
            private readonly Plane[] _frustumPlanes = new Plane[6];

            private bool _isInitialized = false;
            private PortalCameraNode _currentNode = null;

            public int PortalCameraNodeCount => _nodePool.CountActive;
            public ref PortalCameraData Current
            {
                get
                {
                    if (_currentNode is null)
                        return ref _rootNode.Data;
                    else
                        return ref _currentNode.Data;
                }
            }

            private PortalRenderingCore() { }

            public void Initialize(Camera camera, int recursionLimit, Portal penetratingPortal)
            {
                if (_isInitialized || s_portalSystems.Count == 0 || !camera)
                    return;

                _isInitialized = true;

                _rootNode.Data.Depth = 0;
                _rootNode.Data.LocalToWorldMatrix = (Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * camera.worldToCameraMatrix).inverse;
                _rootNode.Data.CullingMatrix = camera.cullingMatrix;
                _rootNode.Data.ProjectionMatrix = camera.projectionMatrix;
                _rootNode.Data.NearClipPlane = camera.nearClipPlane;
                _rootNode.Data.FarClipPlane = camera.farClipPlane;

                _nodeStack.Push(_rootNode);

                s_activePortals.ForEach(portal =>
                {
                    portal.SyncTransform();
                    portal.PlaneMeshCollider.convex = true;
                });

                while (_nodeStack.Count > 0)
                {
                    var currNode = _nodeStack.Pop();

                    if (currNode.Data.Depth >= recursionLimit)
                        continue;

                    var currCameraPos = currNode.Data.LocalToWorldMatrix.GetPosition();
                    var currCameraForward = currNode.Data.LocalToWorldMatrix.rotation * Vector3.forward;

                    GeometryUtility.CalculateFrustumPlanes(currNode.Data.CullingMatrix, _frustumPlanes);

                    foreach (var portal in s_activePortals)
                    {
                        if (!portal.IsWorkable())
                            continue;

                        if (((1 << portal.gameObject.layer) & camera.cullingMask) == 0)
                            continue;

                        if (currNode.Data.Depth != 0)
                        {
                            if (portal == currNode.Data.Portal.LinkedPortal)
                                continue;
                        }

                        var viewportRect = new Rect(0f, 0f, 1f, 1f);
                        if (!(currNode.Data.Depth == 0 && portal == penetratingPortal))
                        {
                            viewportRect = PortalSystemUtils.CalculateBoundsViewportRect(currNode.Data.WorldToCameraMatrix * portal.transform.localToWorldMatrix,
                                    currNode.Data.ProjectionMatrix, currNode.Data.NearClipPlane, portal.Config.PlaneMesh.bounds);

                            if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, portal.PlaneMeshCollider.bounds))
                                continue;

                            if (Vector3.Dot(portal.transform.forward, currCameraPos - portal.transform.position) <= 0)
                                continue;
                        }

                        viewportRect.x = Mathf.Clamp(viewportRect.x - .02f, 0f, .96f);
                        viewportRect.y = Mathf.Clamp(viewportRect.y - .02f, 0f, .96f);
                        viewportRect.width = Mathf.Clamp(viewportRect.width + .04f, .04f, 1f - viewportRect.x);
                        viewportRect.height = Mathf.Clamp(viewportRect.height + .04f, .04f, 1f - viewportRect.y);

                        if (currNode.Data.Depth != 0)
                        {
                            if (!currNode.Data.ViewportRect.Overlaps(viewportRect))
                                continue;
                        }

                        var newNode = _nodePool.Get();
                        newNode.Data.Portal = portal;
                        newNode.Data.Depth = currNode.Data.Depth + 1;

                        var transferMatrix = portal.GetTransferMatrix();
                        newNode.Data.LocalToWorldMatrix = transferMatrix * currNode.Data.LocalToWorldMatrix;

                        newNode.Data.ViewportRect = viewportRect;

                        float transferScale = Vector3.Dot(Vector3.one, transferMatrix.lossyScale) / 3f;
                        newNode.Data.NearClipPlane = Mathf.Max(camera.nearClipPlane, Vector3.Dot(portal.PlaneMeshCollider
                            .ClosestPoint(currCameraPos) - currCameraPos, currCameraForward) * transferScale);
                        newNode.Data.FarClipPlane = currNode.Data.FarClipPlane * transferScale;

                        Matrix4x4 projectionMatrix;
                        if (camera.orthographic)
                        {
                            float left = -camera.orthographicSize;
                            float right = camera.orthographicSize;
                            float bottom = -camera.orthographicSize * camera.aspect;
                            float top = camera.orthographicSize * camera.aspect;

                            projectionMatrix = Matrix4x4.Ortho(left, right, bottom, top, newNode.Data.NearClipPlane, newNode.Data.FarClipPlane);
                        }
                        else if (camera.usePhysicalProperties)
                        {
                            Camera.CalculateProjectionMatrixFromPhysicalProperties(out projectionMatrix, camera.focalLength, camera.sensorSize, camera.lensShift,
                                newNode.Data.NearClipPlane, newNode.Data.FarClipPlane, new Camera.GateFitParameters(camera.gateFit, camera.aspect));
                        }
                        else
                        {
                            projectionMatrix = Matrix4x4.Perspective(camera.fieldOfView, camera.aspect, newNode.Data.NearClipPlane, newNode.Data.FarClipPlane);
                        }

                        var clippingMatrix = Matrix4x4.TRS(new Vector3((1f - viewportRect.x * 2f) / viewportRect.width - 1f, (1f - viewportRect.y * 2f) / viewportRect.height - 1f, 0f),
                            Quaternion.identity, new Vector3(1f / viewportRect.width, 1f / viewportRect.height, 1f));
                        newNode.Data.CullingMatrix = clippingMatrix * projectionMatrix * newNode.Data.WorldToCameraMatrix;

                        var clipPlaneVS = newNode.Data.WorldToCameraMatrix.inverse.transpose
                            * portal.LinkedPortal.GetVector4Plane();

                        if (clipPlaneVS.w < -0.2f && portal.UseObliqueProjectionMatrix)
                            PortalSystemUtils.CalculateObliqueMatrix(ref projectionMatrix, clipPlaneVS);

                        newNode.Data.ProjectionMatrix = projectionMatrix;

                        currNode.AddChild(newNode);
                        _nodeStack.Push(newNode);
                    }
                }

                s_activePortals.ForEach(portal => portal.PlaneMeshCollider.convex = false);
            }

            public bool MoveNext()
            {
                if (!_isInitialized || _currentNode == _rootNode)
                    return false;

                PortalCameraNode prevNode = null;
                PortalCameraNode currNode = null;

                if (_currentNode is null)
                    currNode = _rootNode;
                else
                    prevNode = _currentNode;

                while (_nodeStack.Count != 0 || currNode is not null)
                {
                    while (currNode != null)
                    {
                        _nodeStack.Push(currNode);
                        currNode = currNode.FirstChild;
                    }

                    currNode = _nodeStack.Peek();

                    if (currNode.NextSibling is null || currNode.NextSibling == prevNode)
                    {
                        _nodeStack.Pop();
                        break;
                    }
                    else
                    {
                        currNode = currNode.NextSibling;
                    }
                }

                _currentNode = currNode;
                return _currentNode != _rootNode;
            }

            public void Reset()
            {
                _nodeStack.Clear();
                _currentNode = null;
            }

            public void SetupPortalsBeforeCameraRendering()
            {
                var currNode = _currentNode?.FirstChild;

                while (currNode is not null)
                {
                    var portal = currNode.Data.Portal;
                    var renderTexture = currNode.Data.RenderTexture;

                    portal.SetViewRenderTexture(renderTexture);

                    currNode = currNode.NextSibling;
                }
            }

            public void SetupPortalsAfterCameraRendering()
            {
                var currNode = _currentNode?.FirstChild;

                while (currNode is not null)
                {
                    var portal = currNode.Data.Portal;

                    portal.SetViewRenderTexture(null);
                    RenderTexture.ReleaseTemporary(currNode.Data.RenderTexture);

                    currNode = currNode.NextSibling;
                }
            }

            public void Release()
            {
                if (!_isInitialized)
                    return;

                Reset();
                _isInitialized = false;

                PortalCameraNode prevNode = null;
                PortalCameraNode currNode = _rootNode;

                while (_nodeStack.Count != 0 || currNode is not null)
                {
                    while (currNode != null)
                    {
                        _nodeStack.Push(currNode);
                        currNode = currNode.FirstChild;
                    }

                    currNode = _nodeStack.Peek();

                    if (currNode.NextSibling is null || currNode.NextSibling == prevNode)
                    {
                        _nodeStack.Pop();

                        if (currNode != _rootNode)
                            _nodePool.Release(currNode);

                        prevNode = currNode;
                        currNode = null;
                    }
                    else
                    {
                        currNode = currNode.NextSibling;
                    }
                }

                _rootNode.Reset();
            }
        }
    }
}
