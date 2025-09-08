using UnityEngine;

namespace Xuwu.FourDimensionalPortals
{
    [AddComponentMenu("Xuwu/Four Dimensional Portals/Portal System Additional Camera Data")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class PortalSystemAdditionalCameraData : MonoBehaviour
    {
        [SerializeField] private PortalSystemRenderSettings _overrideRenderSettings;
        [SerializeField] private LayerMask _penetratingViewCullingMask = Physics.AllLayers;
        [SerializeField] private LayerMask _viewCullingMask = Physics.AllLayers;

        public PortalSystemRenderSettings OverrideRenderSettings => _overrideRenderSettings;

        public LayerMask PenetratingViewCullingMask
        {
            get => _penetratingViewCullingMask;
            set => _penetratingViewCullingMask = value;
        }

        public LayerMask ViewCullingMask
        {
            get => _viewCullingMask;
            set => _viewCullingMask = value;
        }

        /// <summary>
        /// Represents the portal which the camera penetrates.
        /// <see cref="PortalSystem"/> will draw <see cref="PortalConfig.PenetratingViewMesh"/> base on this portal.
        /// </summary>
        public Portal PenetratingPortal { get; set; } = null;

        private Camera _camera = null;
        public Camera Camera
        {
            get
            {
                if (!_camera)
                    TryGetComponent(out _camera);

                return _camera;
            }
        }
    }
}
