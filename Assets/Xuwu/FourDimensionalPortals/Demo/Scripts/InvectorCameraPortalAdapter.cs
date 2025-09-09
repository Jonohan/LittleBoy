using UnityEngine;
using Invector.vCamera;
using Xuwu.FourDimensionalPortals;

namespace Xuwu.FourDimensionalPortals.Demo
{
    /// <summary>
    /// Invector相机传送门适配器
    /// 让Invector相机系统支持传送门穿透视图
    /// 
    /// 工作原理：
    /// - 传送门系统需要临时控制主相机位置来实现传送门效果
    /// - 此组件协调Invector相机控制和传送门系统之间的交互
    /// - 通过PortalSystemAdditionalCameraData传递传送门状态
    /// - 传送门系统会在渲染时临时修改相机位置，渲染后恢复
    /// </summary>
    [AddComponentMenu("Xuwu/Four Dimensional Portals/Invector Camera Portal Adapter")]
    [RequireComponent(typeof(vThirdPersonCamera))]
    public class InvectorCameraPortalAdapter : MonoBehaviour
    {
        [Header("Portal Integration")]
        [SerializeField] private InvectorPortalAdapter _playerPortalAdapter;
        
        [Header("Camera Settings")]
        [SerializeField] private bool _enablePortalEffects = true;
        [SerializeField] private LayerMask _portalCullingMask = -1;
        [SerializeField] private LayerMask _viewCullingMask = -1;
        
        [Header("Debug")]
        [SerializeField] private bool _showDebugInfo = false;
        
        private vThirdPersonCamera _invectorCamera;
        private Camera _camera;
        private PortalSystemAdditionalCameraData _portalSystemCameraData;
        private bool _isInitialized = false;
        private Portal _lastPenetratingPortal = null;
        private bool _isVirtualCameraActive = false;
        
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
            SetupPortalSystemCallbacks();
        }
        
        private void Update()
        {
            if (!_isInitialized || !_portalSystemCameraData || !_playerPortalAdapter) return;
            
            // 只在传送门状态改变时更新，避免每帧更新
            UpdatePortalState();
            
            // 检测虚拟相机状态
            CheckVirtualCameraStatus();
        }
        
        /// <summary>
        /// 初始化传送门集成
        /// 参考传送门系统的设计模式，简化初始化流程
        /// </summary>
        private void InitializePortalIntegration()
        {
            // 自动查找玩家传送门适配器
            if (!_playerPortalAdapter)
            {
                // 尝试从场景中查找invector角色
                var invectorController = FindObjectOfType<Invector.vCharacterController.vThirdPersonController>();
                if (invectorController)
                {
                    _playerPortalAdapter = invectorController.GetComponent<InvectorPortalAdapter>();
                }
                
                if (!_playerPortalAdapter)
                {
                    Debug.LogWarning($"[InvectorCameraPortalAdapter] 未找到InvectorPortalAdapter组件。请在玩家角色上添加InvectorPortalAdapter组件。");
                }
            }
            
            // 创建传送门相机数据组件
            if (!_portalSystemCameraData)
            {
                _portalSystemCameraData = gameObject.AddComponent<PortalSystemAdditionalCameraData>();
            }
            
            // 设置相机数据
            if (_camera && _portalSystemCameraData)
            {
                _portalSystemCameraData.PenetratingViewCullingMask = _portalCullingMask;
                _portalSystemCameraData.ViewCullingMask = _viewCullingMask;
            }
            
            _isInitialized = true;
            
            // 打印初始化信息
            if (_showDebugInfo)
            {
                Debug.Log($"[InvectorCameraPortalAdapter] 初始化完成！");
                Debug.Log($"[InvectorCameraPortalAdapter] 相机: {(_camera ? _camera.name : "未找到")}");
                Debug.Log($"[InvectorCameraPortalAdapter] 玩家适配器: {(_playerPortalAdapter ? _playerPortalAdapter.name : "未找到")}");
                Debug.Log($"[InvectorCameraPortalAdapter] 传送门相机数据: {(_portalSystemCameraData ? "已创建" : "未创建")}");
                Debug.Log($"[InvectorCameraPortalAdapter] 传送门系统: {(IsPortalSystemActive() ? "已激活" : "未激活")}");
            }
        }
        
        /// <summary>
        /// 设置传送门系统回调
        /// 监听虚拟相机的激活状态
        /// </summary>
        private void SetupPortalSystemCallbacks()
        {
            // 监听相机的渲染事件来检测虚拟相机状态
            if (_camera)
            {
                // 注意：这里我们通过检查PortalSystem的状态来推断虚拟相机是否激活
                // 实际的虚拟相机控制是在PortalSystem内部进行的
                
                // 添加相机渲染前的回调来检测虚拟相机状态
                Camera.onPreRender += OnCameraPreRender;
                Camera.onPostRender += OnCameraPostRender;
            }
        }
        
        /// <summary>
        /// 相机渲染前回调
        /// </summary>
        private void OnCameraPreRender(Camera camera)
        {
            if (camera != _camera || !_showDebugInfo) return;
            
            // 检查是否有传送门正在被穿透
            var penetratingPortal = GetPenetratingPortal();
            if (penetratingPortal != null)
            {
                // 每60帧打印一次，避免刷屏
                if (Time.frameCount % 60 == 0)
                {
                    Debug.Log($"[InvectorCameraPortalAdapter] 🎬 相机渲染前 - 虚拟相机应该激活，传送门: {penetratingPortal.name}");
                }
            }
        }
        
        /// <summary>
        /// 相机渲染后回调
        /// </summary>
        private void OnCameraPostRender(Camera camera)
        {
            if (camera != _camera || !_showDebugInfo) return;
            
            // 检查是否有传送门正在被穿透
            var penetratingPortal = GetPenetratingPortal();
            if (penetratingPortal != null)
            {
                // 每60帧打印一次，避免刷屏
                if (Time.frameCount % 60 == 0)
                {
                    Debug.Log($"[InvectorCameraPortalAdapter] 🎬 相机渲染后 - 虚拟相机渲染完成，传送门: {penetratingPortal.name}");
                }
            }
        }
        
        /// <summary>
        /// 更新传送门状态
        /// 基于相机位置检测传送门穿透，参考原代码逻辑
        /// </summary>
        private void UpdatePortalState()
        {
            if (!_portalSystemCameraData) return;
            
            // 基于相机位置检测传送门穿透
            var currentPenetratingPortal = DetectPortalPenetrationByCameraView();
            
            // 检测传送门状态变化
            if (currentPenetratingPortal != _lastPenetratingPortal)
            {
                _lastPenetratingPortal = currentPenetratingPortal;
                
                // 总是打印传送门状态变化，不管是否启用调试
                if (currentPenetratingPortal != null)
                {
                    Debug.Log($"[InvectorCameraPortalAdapter] 相机在传送门后方，需要虚拟相机: {currentPenetratingPortal.name}");
                    // 打印虚拟相机创建信息
                    PrintVirtualCameraCreationInfo(currentPenetratingPortal);
                }
                else
                {
                    Debug.Log("[InvectorCameraPortalAdapter] 相机不在传送门后方，关闭虚拟相机");
                }
            }
            
            // 更新穿透传送门状态
            _portalSystemCameraData.PenetratingPortal = currentPenetratingPortal;
        }
        
        /// <summary>
        /// 基于相机位置检测传送门穿透
        /// 参考原代码逻辑：检测相机是否在传送门后方（需要虚拟相机）
        /// </summary>
        private Portal DetectPortalPenetrationByCameraView()
        {
            if (!_camera || !IsPortalSystemActive()) return null;
            
            var cameraPos = _camera.transform.position;
            
            // 获取所有活跃的传送门
            var activePortals = FindObjectsOfType<Portal>();
            
            // 遍历所有活跃的传送门
            foreach (var portal in activePortals)
            {
                if (!portal || !portal.IsWorkable() || !portal.gameObject.activeInHierarchy) continue;
                
                var portalPos = portal.transform.position;
                var portalForward = portal.transform.forward;
                
                // 原代码逻辑：检查相机是否在传送门后方
                // Vector3.Dot(portal.transform.forward, currCameraPos - portal.transform.position) <= 0 时跳过
                // 这意味着当 dot > 0 时（相机在传送门前方）跳过，只有当 dot <= 0 时（相机在传送门后方）才处理
                var cameraToPortal = cameraPos - portalPos;
                var dot = Vector3.Dot(portalForward, cameraToPortal);
                
                if (dot > 0) continue; // 相机在传送门前方，不需要虚拟相机
                
                // 检查传送门是否在相机的视野范围内（可选，用于优化）
                var viewportPoint = _camera.WorldToViewportPoint(portalPos);
                if (viewportPoint.x < 0 || viewportPoint.x > 1 || 
                    viewportPoint.y < 0 || viewportPoint.y > 1 || 
                    viewportPoint.z <= 0) continue; // 传送门不在视野内
                
                // 检查距离是否合理（避免过远的传送门）
                var distance = cameraToPortal.magnitude;
                if (distance > 100f) continue; // 距离过远
                
                // 找到了需要虚拟相机的传送门（相机在传送门后方）
                if (_showDebugInfo && Time.frameCount % 60 == 0)
                {
                    Debug.Log($"[InvectorCameraPortalAdapter] 检测到传送门穿透: {portal.name}, 距离: {distance:F2}, 点积: {dot:F2}, 视口点: {viewportPoint}");
                }
                
                return portal;
            }
            
            return null;
        }
        
        /// <summary>
        /// 打印虚拟相机创建信息
        /// </summary>
        private void PrintVirtualCameraCreationInfo(Portal portal)
        {
            if (!portal || !portal.LinkedPortal) return;
            
            var cameraPos = _camera.transform.position;
            var cameraRotation = _camera.transform.rotation;
            var portalPos = portal.transform.position;
            var linkedPortalPos = portal.LinkedPortal.transform.position;
            
            // 计算虚拟相机应该在的位置（连接传送门的前方）
            var transferMatrix = portal.GetTransferMatrix();
            var virtualCameraPos = transferMatrix.MultiplyPoint(cameraPos);
            var virtualCameraRotation = transferMatrix.rotation * cameraRotation;
            
            Debug.LogWarning($"=== 虚拟相机创建信息 ===");
            Debug.LogWarning($"传送门: {portal.name} -> {portal.LinkedPortal.name}");
            Debug.LogWarning($"主相机位置: {cameraPos}");
            Debug.LogWarning($"主相机旋转: {cameraRotation.eulerAngles}");
            Debug.LogWarning($"传送门位置: {portalPos}");
            Debug.LogWarning($"连接传送门位置: {linkedPortalPos}");
            Debug.LogWarning($"虚拟相机位置: {virtualCameraPos}");
            Debug.LogWarning($"虚拟相机旋转: {virtualCameraRotation.eulerAngles}");
            Debug.LogWarning($"传送矩阵: {transferMatrix}");
            Debug.LogWarning($"========================");
        }
        
        /// <summary>
        /// 检测虚拟相机状态
        /// 基于相机视线检测传送门穿透状态
        /// </summary>
        private void CheckVirtualCameraStatus()
        {
            // 总是检查状态，不管是否启用调试
            if (!IsPortalSystemActive()) 
            {
                if (_showDebugInfo && Time.frameCount % 60 == 0)
                {
                    Debug.LogWarning("[InvectorCameraPortalAdapter] 传送门系统未激活！");
                }
                return;
            }
            
            var portalSystem = PortalSystem.ActiveInstance;
            var penetratingPortal = GetPenetratingPortal();
            
            // 检查是否有传送门正在被相机视线穿透
            bool shouldBeActive = penetratingPortal != null;
            
            // 检测状态变化
            if (shouldBeActive != _isVirtualCameraActive)
            {
                _isVirtualCameraActive = shouldBeActive;
                
                if (_isVirtualCameraActive)
                {
                    Debug.Log($"[InvectorCameraPortalAdapter] 虚拟相机激活！相机视线穿透传送门: {penetratingPortal.name}");
                    Debug.Log($"[InvectorCameraPortalAdapter] 主相机位置: {_camera.transform.position}");
                    Debug.Log($"[InvectorCameraPortalAdapter] 主相机朝向: {_camera.transform.forward}");
                    Debug.Log($"[InvectorCameraPortalAdapter] 传送门位置: {penetratingPortal.transform.position}");
                    Debug.Log($"[InvectorCameraPortalAdapter] 连接传送门位置: {penetratingPortal.LinkedPortal.transform.position}");
                }
                else
                {
                    Debug.Log("[InvectorCameraPortalAdapter] 虚拟相机关闭！相机视线离开传送门");
                }
            }
            
            // 如果虚拟相机激活，打印详细信息
            if (_isVirtualCameraActive && _showDebugInfo)
            {
                // 每30帧打印一次详细信息，避免刷屏
                if (Time.frameCount % 30 == 0)
                {
                    var viewportPoint = _camera.WorldToViewportPoint(penetratingPortal.transform.position);
                    Debug.Log($"[InvectorCameraPortalAdapter] 虚拟相机状态 - 主相机: {_camera.transform.position}, 传送门: {penetratingPortal.name}, 视口点: {viewportPoint}");
                }
            }
            
            // 每60帧打印一次基础状态，帮助调试
            if (_showDebugInfo && Time.frameCount % 60 == 0)
            {
                Debug.Log($"[InvectorCameraPortalAdapter] 状态检查 - 传送门系统: {IsPortalSystemActive()}, 相机视线穿透传送门: {(penetratingPortal ? penetratingPortal.name : "无")}, 虚拟相机: {_isVirtualCameraActive}");
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
        /// 设置传送门穿透视图剔除遮罩
        /// </summary>
        public void SetPortalCullingMask(LayerMask cullingMask)
        {
            _portalCullingMask = cullingMask;
            
            if (_portalSystemCameraData)
            {
                _portalSystemCameraData.PenetratingViewCullingMask = cullingMask;
            }
        }
        
        /// <summary>
        /// 设置普通视图剔除遮罩
        /// </summary>
        public void SetViewCullingMask(LayerMask cullingMask)
        {
            _viewCullingMask = cullingMask;
            
            if (_portalSystemCameraData)
            {
                _portalSystemCameraData.ViewCullingMask = cullingMask;
            }
        }
        
        /// <summary>
        /// 获取当前穿透的传送门
        /// </summary>
        public Portal GetPenetratingPortal()
        {
            return _portalSystemCameraData ? _portalSystemCameraData.PenetratingPortal : null;
        }
        
        /// <summary>
        /// 检查传送门系统是否正在工作
        /// </summary>
        public bool IsPortalSystemActive()
        {
            return PortalSystem.ActiveInstance != null && PortalSystem.ActiveInstance.IsValid;
        }
        
        /// <summary>
        /// 获取传送门系统状态信息（用于调试）
        /// </summary>
        public string GetPortalSystemStatus()
        {
            if (!IsPortalSystemActive())
                return "传送门系统未激活";
                
            var activeInstance = PortalSystem.ActiveInstance;
            var penetratingPortal = GetPenetratingPortal();
            
            return $"传送门系统已激活\n" +
                   $"相机视线穿透传送门: {(penetratingPortal ? penetratingPortal.name : "无")}\n" +
                   $"虚拟相机状态: {(_isVirtualCameraActive ? "激活" : "关闭")}\n" +
                   $"相机数据: {(_portalSystemCameraData ? "已配置" : "未配置")}";
        }
        
        /// <summary>
        /// 强制打印当前状态（用于调试）
        /// </summary>
        [ContextMenu("打印当前状态")]
        public void PrintCurrentStatus()
        {
            Debug.Log("=== InvectorCameraPortalAdapter 状态报告 ===");
            Debug.Log($"初始化状态: {_isInitialized}");
            Debug.Log($"相机: {(_camera ? _camera.name : "未找到")}");
            Debug.Log($"相机位置: {(_camera ? _camera.transform.position.ToString() : "无")}");
            Debug.Log($"相机朝向: {(_camera ? _camera.transform.forward.ToString() : "无")}");
            Debug.Log($"传送门相机数据: {(_portalSystemCameraData ? "已创建" : "未创建")}");
            Debug.Log($"传送门系统激活: {IsPortalSystemActive()}");
            
            // 基于相机视线检测的传送门穿透
            var penetratingPortal = DetectPortalPenetrationByCameraView();
            Debug.Log($"相机视线穿透传送门: {(penetratingPortal ? penetratingPortal.name : "无")}");
            
            if (_portalSystemCameraData)
            {
                Debug.Log($"相机数据穿透传送门: {(_portalSystemCameraData.PenetratingPortal ? _portalSystemCameraData.PenetratingPortal.name : "无")}");
            }
            
            Debug.Log($"虚拟相机状态: {(_isVirtualCameraActive ? "激活" : "关闭")}");
            
            // 打印所有活跃传送门的信息
            if (IsPortalSystemActive())
            {
                var activePortals = FindObjectsOfType<Portal>();
                Debug.Log($"活跃传送门数量: {activePortals.Length}");
                foreach (var portal in activePortals)
                {
                    if (portal && portal.IsWorkable() && portal.gameObject.activeInHierarchy)
                    {
                        var viewportPoint = _camera.WorldToViewportPoint(portal.transform.position);
                        Debug.Log($"传送门: {portal.name}, 位置: {portal.transform.position}, 视口点: {viewportPoint}");
                    }
                }
            }
            
            Debug.Log("=== 状态报告结束 ===");
        }
        
        private void OnDestroy()
        {
            // 清理相机回调
            if (_camera)
            {
                Camera.onPreRender -= OnCameraPreRender;
                Camera.onPostRender -= OnCameraPostRender;
            }
        }
        
        private void OnValidate()
        {
            if (Application.isPlaying && _isInitialized)
            {
                SetPortalEffectsEnabled(_enablePortalEffects);
                SetPortalCullingMask(_portalCullingMask);
                SetViewCullingMask(_viewCullingMask);
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
            
            // 如果有穿透传送门，绘制连接线
            if (_portalSystemCameraData && _portalSystemCameraData.PenetratingPortal)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(_camera.transform.position, _portalSystemCameraData.PenetratingPortal.transform.position);
                
                // 绘制传送门位置
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(_portalSystemCameraData.PenetratingPortal.transform.position, Vector3.one * 0.5f);
            }
            
            // 绘制传送门系统状态
            if (_showDebugInfo && Application.isPlaying)
            {
                var status = GetPortalSystemStatus();
                UnityEditor.Handles.Label(_camera.transform.position + Vector3.up * 2f, status);
            }
        }
#endif
    }
}
