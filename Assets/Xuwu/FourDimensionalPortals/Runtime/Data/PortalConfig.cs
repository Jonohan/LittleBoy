using UnityEngine;

namespace Xuwu.FourDimensionalPortals
{
    [CreateAssetMenu(menuName = "Four Dimensional Portals/Portal Config")]
    public sealed class PortalConfig : ScriptableObject
    {
        [SerializeField] private Mesh _planeMesh;
        [SerializeField] private Mesh _detectionMesh;
        [SerializeField] private Mesh _frameMesh;
        [SerializeField] private Material _endMaterial;
        [SerializeField] private Mesh _penetratingViewMesh;

        public Mesh PlaneMesh
        {
            get => _planeMesh;
            set
            {
                if (_planeMesh == value)
                    return;

                _planeMesh = value;
                ValidatePortals();
            }
        }

        public Mesh DetectionMesh
        {
            get => _detectionMesh ? _detectionMesh : _planeMesh;
            set
            {
                if (_detectionMesh == value)
                    return;

                _detectionMesh = value;
                ValidatePortals();
            }
        }

        public Mesh FrameMesh
        {
            get => _frameMesh;
            set
            {
                if (_frameMesh == value)
                    return;

                _frameMesh = value;
                ValidatePortals();
            }
        }

        public Material EndMaterial
        {
            get => _endMaterial;
            set
            {
                if (_endMaterial == value)
                    return;

                _endMaterial = value;
                ValidatePortals();
            }
        }

        public Mesh PenetratingViewMesh
        {
            get => _penetratingViewMesh;
            set
            {
                if (_penetratingViewMesh == value)
                    return;

                _penetratingViewMesh = value;
                ValidatePortals();
            }
        }

        private void ValidatePortals()
        {
            foreach (var portal in FindObjectsOfType<Portal>(true))
            {
                if (portal.Config == this)
                    portal.Validate();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this)
                    ValidatePortals();
            };
        }
#endif
    }
}
