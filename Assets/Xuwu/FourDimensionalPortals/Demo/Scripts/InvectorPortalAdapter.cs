using System.Collections.Generic;
using UnityEngine;
using Invector.vCharacterController;
using Xuwu.FourDimensionalPortals;

namespace Xuwu.FourDimensionalPortals.Demo
{
    /// <summary>
    /// 适配器组件，让invector控制器能够使用传送门系统
    /// 最小化修改，只需要将此组件添加到invector角色上即可
    /// </summary>
    [AddComponentMenu("Xuwu/Four Dimensional Portals/Invector Portal Adapter")]
    [RequireComponent(typeof(vThirdPersonController))]
    [RequireComponent(typeof(Rigidbody))]
    public class InvectorPortalAdapter : PortalTraveler
    {
        [Header("Invector Integration")]
        [SerializeField] private vThirdPersonController _invectorController;
        [SerializeField] private Transform _cameraFollowTarget;
        [SerializeField] private PortalSystemAdditionalCameraData _portalSystemCameraData;
        
        [Header("Portal Settings")]
        [SerializeField] private bool _autoConfigurePortalSettings = true;
        
        private Transform _originalCameraTarget;
        private bool _isInPortal = false;
        
        private void Awake()
        {
            // 获取invector控制器
            if (!_invectorController)
                _invectorController = GetComponent<vThirdPersonController>();
                
            // 自动配置传送门旅行者参数
            if (_autoConfigurePortalSettings)
            {
                TransferPivotOffset = new Vector3(0f, 0.9f, 0f); // 适合invector角色的检测点
                CloneLayer = 8; // 传送门克隆层
            }
            
            // 自动收集渲染器
            CollectRenderers();
        }
        
        private void Start()
        {
            // 保存原始相机目标
            if (_cameraFollowTarget)
                _originalCameraTarget = _cameraFollowTarget;
        }
        
        /// <summary>
        /// 自动收集角色的所有渲染器
        /// </summary>
        private void CollectRenderers()
        {
            // 收集MeshRenderer
            var meshRenderers = GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in meshRenderers)
            {
                if (!MeshRenderers.Contains(renderer))
                    MeshRenderers.Add(renderer);
            }
            
            // 收集SkinnedMeshRenderer
            var skinnedMeshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var renderer in skinnedMeshRenderers)
            {
                if (!SkinnedMeshRenderers.Contains(renderer))
                    SkinnedMeshRenderers.Add(renderer);
            }
        }
        
        protected override void PassThrough(Portal fromPortal, Portal toPortal, Matrix4x4 transferMatrix)
        {
            base.PassThrough(fromPortal, toPortal, transferMatrix);
            
            // 处理invector特有的传送门穿越逻辑
            HandleInvectorPortalTransition(fromPortal, toPortal, transferMatrix);
        }
        
        /// <summary>
        /// 处理invector控制器的传送门穿越
        /// </summary>
        private void HandleInvectorPortalTransition(Portal fromPortal, Portal toPortal, Matrix4x4 transferMatrix)
        {
            if (!_invectorController) return;
            
            // 处理重力方向变化
            HandleGravityChange(fromPortal, toPortal);
            
            // 处理相机系统
            HandleCameraTransition(fromPortal, toPortal, transferMatrix);
            
            // 处理动画状态
            HandleAnimationTransition();
        }
        
        /// <summary>
        /// 处理重力方向变化
        /// </summary>
        private void HandleGravityChange(Portal fromPortal, Portal toPortal)
        {
            // 检查传送门是否改变了重力方向
            var fromUp = fromPortal.transform.up;
            var toUp = toPortal.transform.up;
            
            if (Vector3.Dot(fromUp, toUp) < 0.9f) // 如果重力方向变化较大
            {
                // 给角色一个向上的力，防止掉落
                var upwardForce = toUp * 5f;
                Rigidbody.AddForce(upwardForce, ForceMode.VelocityChange);
            }
        }
        
        /// <summary>
        /// 处理相机过渡
        /// </summary>
        private void HandleCameraTransition(Portal fromPortal, Portal toPortal, Matrix4x4 transferMatrix)
        {
            if (!_cameraFollowTarget || !_portalSystemCameraData) return;
            
            // 更新传送门相机数据
            _portalSystemCameraData.PenetratingPortal = PenetratingPortal;
            
            // 如果相机目标存在，更新其位置和旋转
            if (_cameraFollowTarget)
            {
                var newPosition = toPortal.TransferPoint(_cameraFollowTarget.position);
                var newRotation = toPortal.TransferRotation(_cameraFollowTarget.rotation);
                
                _cameraFollowTarget.SetPositionAndRotation(newPosition, newRotation);
            }
        }
        
        /// <summary>
        /// 处理动画过渡
        /// </summary>
        private void HandleAnimationTransition()
        {
            if (!_invectorController) return;
            
            // 可以在这里添加传送门穿越时的特殊动画处理
            // 例如：播放传送门穿越动画、重置某些动画状态等
            
            // 重置移动状态，防止传送后继续之前的移动
            _invectorController.input = Vector3.zero;
            _invectorController.moveDirection = Vector3.zero;
        }
        
        /// <summary>
        /// 更新传送门状态
        /// </summary>
        private void Update()
        {
            // 检查是否在传送门中
            bool wasInPortal = _isInPortal;
            _isInPortal = PenetratingPortal != null;
            
            // 如果进入或离开传送门，更新相机系统
            if (wasInPortal != _isInPortal && _portalSystemCameraData)
            {
                _portalSystemCameraData.PenetratingPortal = PenetratingPortal;
            }
        }
        
        /// <summary>
        /// 验证组件设置
        /// </summary>
        public override void Validate()
        {
            base.Validate();
            
            // 确保有invector控制器
            if (!_invectorController)
                _invectorController = GetComponent<vThirdPersonController>();
                
            // 自动收集渲染器
            CollectRenderers();
            
            // 自动配置传送门参数
            if (_autoConfigurePortalSettings)
            {
                TransferPivotOffset = new Vector3(0f, 0.9f, 0f);
                CloneLayer = 8;
            }
            
            // 验证传送门相机数据
            if (!_portalSystemCameraData)
            {
                Debug.LogWarning($"[InvectorPortalAdapter] PortalSystemAdditionalCameraData not assigned on {gameObject.name}. " +
                               "Camera portal effects may not work properly.");
            }
        }
        
        /// <summary>
        /// 手动触发传送门穿越（用于测试或特殊需求）
        /// </summary>
        public void ForcePortalTransition(Portal targetPortal)
        {
            if (!targetPortal || !targetPortal.IsWorkable()) return;
            
            var transferMatrix = targetPortal.GetTransferMatrix();
            var newPosition = targetPortal.TransferPoint(transform.position);
            var newRotation = targetPortal.TransferRotation(transform.rotation);
            
            transform.SetPositionAndRotation(newPosition, newRotation);
            Rigidbody.position = newPosition;
            Rigidbody.rotation = newRotation;
            
            // 处理速度转换
            Rigidbody.velocity = transferMatrix.MultiplyVector(Rigidbody.velocity);
            Rigidbody.angularVelocity = transferMatrix.rotation * Rigidbody.angularVelocity;
        }
        
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // 绘制传送门检测点
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.TransformPoint(TransferPivotOffset), 0.1f);
            
            // 绘制传送门连接线
            if (PenetratingPortal)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, PenetratingPortal.transform.position);
            }
        }
#endif
    }
}
