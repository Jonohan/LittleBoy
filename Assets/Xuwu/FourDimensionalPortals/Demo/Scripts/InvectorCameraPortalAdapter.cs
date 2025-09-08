using System.Reflection;
using UnityEngine;
using Invector.vCamera;
using Xuwu.FourDimensionalPortals;

namespace Xuwu.FourDimensionalPortals.Demo
{
    /// <summary>
    /// invector相机传送门适配器
    /// 让invector的相机系统支持传送门穿透视图
    /// </summary>
    [AddComponentMenu("Xuwu/Four Dimensional Portals/Invector Camera Portal Adapter")]
    [RequireComponent(typeof(vThirdPersonCamera))]
    public class InvectorCameraPortalAdapter : MonoBehaviour
    {
        [Header("Portal Integration")]
        [SerializeField] private PortalSystemAdditionalCameraData _portalSystemCameraData;
        [SerializeField] private InvectorPortalAdapter _portalAdapter;
        [SerializeField] private Transform _manualPlayerTarget;
        
        [Header("Camera Settings")]
        [SerializeField] private bool _enablePortalEffects = true;
        [SerializeField] private LayerMask _portalCullingMask = -1;
        
        private vThirdPersonCamera _invectorCamera;
        private Camera _camera;
        private bool _isInitialized = false;
        
        private void Awake()
        {
            _invectorCamera = GetComponent<vThirdPersonCamera>();
            _camera = GetComponent<Camera>();
            
            if (!_camera)
                _camera = GetComponentInChildren<Camera>();
        }
        
        private void Start()
        {
            InitializePortalIntegration();
        }
        
        private void Update()
        {
            if (!_isInitialized) return;
            
            UpdatePortalCameraData();
        }
        
        /// <summary>
        /// 初始化传送门集成
        /// </summary>
        private void InitializePortalIntegration()
        {
            // 自动查找传送门适配器
            if (!_portalAdapter)
            {
                Transform player = null;
                
                // 首先尝试手动指定的目标
                if (_manualPlayerTarget)
                {
                    player = _manualPlayerTarget;
                }
                else
                {
                    // 尝试通过反射获取invector相机目标
                    try
                    {
                        var targetField = _invectorCamera.GetType().GetField("target");
                        if (targetField != null)
                            player = targetField.GetValue(_invectorCamera) as Transform;
                        
                        // 如果反射失败，尝试其他可能的属性名
                        if (!player)
                        {
                            var targetProperty = _invectorCamera.GetType().GetProperty("target");
                            if (targetProperty != null)
                                player = targetProperty.GetValue(_invectorCamera) as Transform;
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[InvectorCameraPortalAdapter] Failed to get camera target via reflection: {e.Message}");
                    }
                    
                    // 如果还是找不到，尝试查找场景中的invector角色
                    if (!player)
                    {
                        var invectorController = FindObjectOfType<Invector.vCharacterController.vThirdPersonController>();
                        if (invectorController)
                            player = invectorController.transform;
                    }
                }
                
                if (player)
                    _portalAdapter = player.GetComponent<InvectorPortalAdapter>();
            }
            
            // 创建传送门相机数据
            if (!_portalSystemCameraData)
            {
                _portalSystemCameraData = gameObject.AddComponent<PortalSystemAdditionalCameraData>();
            }
            
            // 设置相机数据（Camera属性是只读的，会自动设置为当前相机）
            if (_camera)
            {
                _portalSystemCameraData.PenetratingViewCullingMask = _portalCullingMask;
            }
            
            _isInitialized = true;
        }
        
        /// <summary>
        /// 更新传送门相机数据
        /// </summary>
        private void UpdatePortalCameraData()
        {
            if (!_portalSystemCameraData || !_portalAdapter) return;
            
            // 更新穿透传送门
            _portalSystemCameraData.PenetratingPortal = _portalAdapter.PenetratingPortal;
            
            // 更新相机位置和旋转
            if (_camera)
            {
                _portalSystemCameraData.transform.SetPositionAndRotation(_camera.transform.position, _camera.transform.rotation);
            }
        }
        
        /// <summary>
        /// 启用/禁用传送门效果
        /// </summary>
        public void SetPortalEffectsEnabled(bool enabled)
        {
            _enablePortalEffects = enabled;
            
            if (_portalSystemCameraData)
            {
                _portalSystemCameraData.enabled = enabled;
            }
        }
        
        /// <summary>
        /// 设置传送门剔除遮罩
        /// </summary>
        public void SetPortalCullingMask(LayerMask cullingMask)
        {
            _portalCullingMask = cullingMask;
            
            if (_portalSystemCameraData)
            {
                _portalSystemCameraData.PenetratingViewCullingMask = cullingMask;
            }
        }
        
        private void OnValidate()
        {
            if (Application.isPlaying && _isInitialized)
            {
                SetPortalEffectsEnabled(_enablePortalEffects);
                SetPortalCullingMask(_portalCullingMask);
            }
        }
        
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!_camera) return;
            
            // 绘制相机视野
            Gizmos.color = Color.blue;
            Gizmos.matrix = _camera.transform.localToWorldMatrix;
            Gizmos.DrawFrustum(Vector3.zero, _camera.fieldOfView, _camera.farClipPlane, _camera.nearClipPlane, _camera.aspect);
        }
#endif
    }
}
