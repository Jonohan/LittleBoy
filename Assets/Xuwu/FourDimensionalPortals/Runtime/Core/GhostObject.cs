using UnityEngine;

namespace Xuwu.FourDimensionalPortals
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GhostObject))]
    public abstract class GhostObject : MonoBehaviour
    {
        internal float _lastTimeOfUse;

        public Portal AttachedPortal { get; internal set; } = null;

        private void Awake() => Reset();

        protected internal virtual void Reset() => AttachedPortal = null;
    }

    [RequireComponent(typeof(Rigidbody))]
    public sealed class RigidbodyGhost : GhostObject
    {
        public Rigidbody SourceRigidbody { get; private set; } = null;

        private Rigidbody _rigidbody;
        public Rigidbody Rigidbody => _rigidbody;

        protected internal override void Reset()
        {
            base.Reset();
            SourceRigidbody = null;
            TryGetComponent(out _rigidbody);

            _rigidbody.isKinematic = false;

            _rigidbody.angularDrag = .05f;
            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.centerOfMass = Vector3.zero;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
            _rigidbody.constraints = RigidbodyConstraints.None;
            _rigidbody.detectCollisions = true;
            _rigidbody.drag = 0f;
            _rigidbody.freezeRotation = false;
            _rigidbody.inertiaTensor = Vector3.one;
            _rigidbody.inertiaTensorRotation = Quaternion.identity;
            _rigidbody.interpolation = RigidbodyInterpolation.None;
            //_rigidbody.isKinematic = false;
            _rigidbody.mass = 1f;
            _rigidbody.maxAngularVelocity = Physics.defaultMaxAngularSpeed;
            _rigidbody.maxDepenetrationVelocity = Physics.defaultMaxDepenetrationVelocity;
            _rigidbody.position = Vector3.zero;
            _rigidbody.rotation = Quaternion.identity;
            _rigidbody.sleepThreshold = Physics.sleepThreshold;
            _rigidbody.solverIterations = Physics.defaultSolverIterations;
            _rigidbody.solverVelocityIterations = Physics.defaultSolverVelocityIterations;
            _rigidbody.useGravity = true;
            _rigidbody.velocity = Vector3.zero;

#if UNITY_2022_2_OR_NEWER
            _rigidbody.automaticCenterOfMass = true;
            _rigidbody.automaticInertiaTensor = true;
            _rigidbody.excludeLayers = 0;
            _rigidbody.includeLayers = 0;
#endif
        }

        internal void Setup(Rigidbody sourceRigidbody)
        {
            SourceRigidbody = sourceRigidbody;

            _rigidbody.angularDrag = sourceRigidbody.angularDrag;
            _rigidbody.angularVelocity = sourceRigidbody.angularVelocity;
            _rigidbody.centerOfMass = sourceRigidbody.centerOfMass;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
            _rigidbody.constraints = sourceRigidbody.constraints;
            _rigidbody.detectCollisions = sourceRigidbody.detectCollisions;
            _rigidbody.drag = sourceRigidbody.drag;
            _rigidbody.freezeRotation = sourceRigidbody.freezeRotation;
            _rigidbody.inertiaTensor = sourceRigidbody.inertiaTensor;
            _rigidbody.inertiaTensorRotation = sourceRigidbody.inertiaTensorRotation;
            _rigidbody.interpolation = sourceRigidbody.interpolation;
            //_rigidbody.isKinematic = sourceRigidbody.isKinematic;
            _rigidbody.mass = sourceRigidbody.mass;
            _rigidbody.maxAngularVelocity = sourceRigidbody.maxAngularVelocity;
            _rigidbody.maxDepenetrationVelocity = sourceRigidbody.maxDepenetrationVelocity;
            _rigidbody.position = sourceRigidbody.position;
            _rigidbody.rotation = sourceRigidbody.rotation;
            _rigidbody.sleepThreshold = sourceRigidbody.sleepThreshold;
            _rigidbody.solverIterations = sourceRigidbody.solverIterations;
            _rigidbody.solverVelocityIterations = sourceRigidbody.solverVelocityIterations;
            _rigidbody.useGravity = sourceRigidbody.useGravity;
            _rigidbody.velocity = sourceRigidbody.velocity;

            _rigidbody.isKinematic = sourceRigidbody.isKinematic;

#if UNITY_2022_2_OR_NEWER
            _rigidbody.automaticCenterOfMass = sourceRigidbody.automaticCenterOfMass;
            _rigidbody.automaticInertiaTensor = sourceRigidbody.automaticInertiaTensor;
            _rigidbody.excludeLayers = sourceRigidbody.excludeLayers;
            _rigidbody.includeLayers = sourceRigidbody.includeLayers;
#endif
        }
    }

    public abstract class ColliderGhost : GhostObject
    {
        public Collider SourceCollider { get; private set; } = null;

        private Collider _collider;
        public Collider Collider => _collider;

        protected internal override void Reset()
        {
            base.Reset();
            SourceCollider = null;
            TryGetComponent(out _collider);

            _collider.contactOffset = Physics.defaultContactOffset;
            _collider.enabled = false;
            _collider.hasModifiableContacts = false;
            _collider.isTrigger = false;
            _collider.sharedMaterial = null;

#if UNITY_2022_2_OR_NEWER
            _collider.excludeLayers = 0;
            _collider.includeLayers = 0;
            _collider.layerOverridePriority = 0;
            _collider.providesContacts = false;
#endif
        }

        protected void SetSourceCollider(Collider sourceCollider)
        {
            SourceCollider = sourceCollider;

            _collider.contactOffset = sourceCollider.contactOffset;
            _collider.enabled = sourceCollider.enabled;
            _collider.hasModifiableContacts = true;
            _collider.isTrigger = sourceCollider.isTrigger;
            _collider.sharedMaterial = sourceCollider.sharedMaterial;

#if UNITY_2022_2_OR_NEWER
            _collider.excludeLayers = sourceCollider.excludeLayers;
            _collider.includeLayers = sourceCollider.includeLayers;
            _collider.layerOverridePriority = sourceCollider.layerOverridePriority;
            _collider.providesContacts = sourceCollider.providesContacts;
#endif
        }
    }

    [RequireComponent(typeof(BoxCollider))]
    public sealed class BoxColliderGhost : ColliderGhost
    {
        public new BoxCollider SourceCollider => base.SourceCollider as BoxCollider;
        public new BoxCollider Collider => base.Collider as BoxCollider;

        protected internal override void Reset()
        {
            base.Reset();
            Collider.center = Vector3.zero;
            Collider.size = Vector3.one;
        }

        internal void Setup(BoxCollider sourceCollider)
        {
            SetSourceCollider(sourceCollider);
            Collider.center = sourceCollider.center;
            Collider.size = sourceCollider.size;
        }
    }

    [RequireComponent(typeof(CapsuleCollider))]
    public sealed class CapsuleColliderGhost : ColliderGhost
    {
        public new CapsuleCollider SourceCollider => base.SourceCollider as CapsuleCollider;
        public new CapsuleCollider Collider => base.Collider as CapsuleCollider;

        protected internal override void Reset()
        {
            base.Reset();
            Collider.center = Vector3.zero;
            Collider.direction = 1;
            Collider.height = 2f;
            Collider.radius = .5f;
        }

        internal void Setup(CapsuleCollider sourceCollider)
        {
            SetSourceCollider(sourceCollider);
            Collider.center = sourceCollider.center;
            Collider.direction = sourceCollider.direction;
            Collider.height = sourceCollider.height;
            Collider.radius = sourceCollider.radius;
        }
    }

    [RequireComponent(typeof(MeshCollider))]
    public sealed class MeshColliderGhost : ColliderGhost
    {
        public new MeshCollider SourceCollider => base.SourceCollider as MeshCollider;
        public new MeshCollider Collider => base.Collider as MeshCollider;

        protected internal override void Reset()
        {
            base.Reset();
            Collider.convex = false;
            Collider.cookingOptions = MeshColliderCookingOptions.None;
            Collider.sharedMesh = null;
        }

        internal void Setup(MeshCollider sourceCollider)
        {
            SetSourceCollider(sourceCollider);
            Collider.convex = sourceCollider.convex;
            Collider.sharedMesh = sourceCollider.sharedMesh;
        }
    }

    [RequireComponent(typeof(SphereCollider))]
    public sealed class SphereColliderGhost : ColliderGhost
    {
        public new SphereCollider SourceCollider => base.SourceCollider as SphereCollider;
        public new SphereCollider Collider => base.Collider as SphereCollider;

        protected internal override void Reset()
        {
            base.Reset();
            Collider.center = Vector3.zero;
            Collider.radius = .5f;
        }

        internal void Setup(SphereCollider sourceCollider)
        {
            SetSourceCollider(sourceCollider);
            Collider.center = sourceCollider.center;
            Collider.radius = sourceCollider.radius;
        }
    }
}
