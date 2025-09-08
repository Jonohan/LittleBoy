using System.Collections.Generic;
using UnityEngine;

namespace Xuwu.FourDimensionalPortals
{
    partial class PortalSystem
    {
        private sealed class GhostObjectManager
        {
            private static GhostObjectManager s_instance = null;

            public static GhostObjectManager Instance => s_instance ??= new GhostObjectManager();

            public static void FreeMemory()
            {
                if (s_instance is null)
                    return;

                s_instance.Clear();
                s_instance = null;
            }

            private readonly GhostObjectPool<RigidbodyGhost> _rigidbodyGhostPool = new();
            private readonly GhostObjectPool<BoxColliderGhost> _boxColliderGhostPool = new();
            private readonly GhostObjectPool<CapsuleColliderGhost> _capsuleColliderGhostPool = new();
            private readonly GhostObjectPool<MeshColliderGhost> _meshColliderGhostPool = new();
            private readonly GhostObjectPool<SphereColliderGhost> _sphereColliderGhostPool = new();

            private Transform _rootTransform;
            public Transform RootTransform
            {
                get => _rootTransform;
                set
                {
                    _rootTransform = value;

                    if (_rootTransform != null)
                    {
                        _rigidbodyGhostPool.RootTransform = _rootTransform;
                        _boxColliderGhostPool.RootTransform = _rootTransform;
                        _capsuleColliderGhostPool.RootTransform = _rootTransform;
                        _meshColliderGhostPool.RootTransform = _rootTransform;
                        _sphereColliderGhostPool.RootTransform = _rootTransform;
                    }
                    else
                    {
                        Clear();
                    }
                }
            }

            private GhostObjectManager() { }

            public RigidbodyGhost CreateRigidbodyGhost(Rigidbody sourceRigidbody)
            {
                if (!sourceRigidbody)
                    return null;

                var rigidbodyGhost = _rigidbodyGhostPool.Get();
                rigidbodyGhost.Setup(sourceRigidbody);

                return rigidbodyGhost;
            }

            public ColliderGhost CreateColliderGhost(Collider sourceCollider)
            {
                if (!sourceCollider)
                    return null;

                ColliderGhost colliderGhost = null;

                switch (sourceCollider)
                {
                    case BoxCollider boxCollider:
                        var boxColliderGhost = _boxColliderGhostPool.Get();
                        boxColliderGhost.Setup(boxCollider);
                        colliderGhost = boxColliderGhost;
                        break;

                    case CapsuleCollider capsuleCollider:
                        var capsuleColliderGhost = _capsuleColliderGhostPool.Get();
                        capsuleColliderGhost.Setup(capsuleCollider);
                        colliderGhost = capsuleColliderGhost;
                        break;

                    case MeshCollider meshCollider:
                        var meshColliderGhost = _meshColliderGhostPool.Get();
                        meshColliderGhost.Setup(meshCollider);
                        colliderGhost = meshColliderGhost;
                        break;

                    case SphereCollider sphereCollider:
                        var sphereColliderGhost = _sphereColliderGhostPool.Get();
                        sphereColliderGhost.Setup(sphereCollider);
                        colliderGhost = sphereColliderGhost;
                        break;

                    default:
                        break;
                }

                return colliderGhost;
            }

            public void CollectAndResetGhostObjectInstances()
            {
                _rigidbodyGhostPool.CollectAndResetColliderGhostInstances();
                _boxColliderGhostPool.CollectAndResetColliderGhostInstances();
                _capsuleColliderGhostPool.CollectAndResetColliderGhostInstances();
                _meshColliderGhostPool.CollectAndResetColliderGhostInstances();
                _sphereColliderGhostPool.CollectAndResetColliderGhostInstances();
            }

            public void DestroyInactiveGhostObjectInstances(float t = 0f)
            {
                _rigidbodyGhostPool.DestroyInactiveColliderGhostInstances(t);
                _boxColliderGhostPool.DestroyInactiveColliderGhostInstances(t);
                _capsuleColliderGhostPool.DestroyInactiveColliderGhostInstances(t);
                _meshColliderGhostPool.DestroyInactiveColliderGhostInstances(t);
                _sphereColliderGhostPool.DestroyInactiveColliderGhostInstances(t);
            }

            public void Clear()
            {
                _rigidbodyGhostPool.Clear();
                _boxColliderGhostPool.Clear();
                _capsuleColliderGhostPool.Clear();
                _meshColliderGhostPool.Clear();
                _sphereColliderGhostPool.Clear();
            }
        }

        private class GhostObjectPool<T> where T : GhostObject, new()
        {
            private readonly List<T> _list = new();
            private int _getIndex = -1;

            private Transform _rootTransform;
            public Transform RootTransform
            {
                get => _rootTransform;
                set
                {
                    if (_rootTransform == value)
                        return;

                    _rootTransform = value;

                    for (int i = _list.Count - 1; i >= 0; i--)
                    {
                        var colliderGhost = _list[i];

                        if (!colliderGhost || i > _getIndex)
                            continue;

                        colliderGhost.transform.SetParent(RootTransform);
                    }
                }
            }

            public T Get()
            {
                T colliderGhost = _getIndex >= 0 ? _list[_getIndex--] : null;

                if (!colliderGhost)
                {
                    colliderGhost = new GameObject(typeof(T).Name) { hideFlags = HideFlags.NotEditable | HideFlags.DontSave }.AddComponent<T>();
                    _list.Add(colliderGhost);
                }

                colliderGhost._lastTimeOfUse = float.PositiveInfinity;
                colliderGhost.gameObject.SetActive(true);

                return colliderGhost;
            }

            public void CollectAndResetColliderGhostInstances()
            {
                for (int i = _list.Count - 1; i >= 0; i--)
                {
                    var colliderGhost = _list[i];

                    if (colliderGhost)
                    {
                        if (i > _getIndex)
                            colliderGhost._lastTimeOfUse = Time.unscaledTime;

                        colliderGhost.gameObject.SetActive(false);
                        colliderGhost.gameObject.hideFlags = HideFlags.NotEditable | HideFlags.DontSave;
                        colliderGhost.gameObject.layer = 0;
                        colliderGhost.transform.SetParent(RootTransform);
                        colliderGhost.transform.localPosition = Vector3.zero;
                        colliderGhost.transform.localRotation = Quaternion.identity;
                        colliderGhost.transform.localScale = Vector3.one;
                        colliderGhost.Reset();
                    }
                    else
                    {
                        _list.RemoveAt(i);
                    }
                }

                _getIndex = _list.Count - 1;
            }

            public void DestroyInactiveColliderGhostInstances(float t = 0f)
            {
                for (int i = _list.Count - 1; i >= 0; i--)
                {
                    var colliderGhost = _list[i];

                    if (i > _getIndex)
                        continue;

                    if (Time.unscaledTime - colliderGhost._lastTimeOfUse < t)
                        continue;

                    if (colliderGhost)
                        PortalSystemUtils.SafeDestroy(colliderGhost.gameObject);

                    _list.RemoveAt(i);
                    _getIndex--;
                }
            }

            public void Clear()
            {
                foreach (var colliderGhost in _list)
                {
                    if (colliderGhost)
                        PortalSystemUtils.SafeDestroy(colliderGhost.gameObject);
                }

                _list.Clear();
                _getIndex = -1;
            }
        }
    }
}
