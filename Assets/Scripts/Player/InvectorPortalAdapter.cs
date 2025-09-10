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
        
        // 跳跃触发时间记录，用于限制触发频率
        private float _lastJumpTriggerTime = 0f;
        
        // 平滑旋转相关
        private bool _isSmoothingRotation = false;
        private Quaternion _targetRotation;
        private float _smoothRotationSpeed = 0.1f;
        
        private void Awake()
        {
            // 获取invector控制器
            if (!_invectorController)
                _invectorController = GetComponent<vThirdPersonController>();
                
            // 自动配置传送门旅行者参数
            if (_autoConfigurePortalSettings)
            {
                TransferPivotOffset = new Vector3(0f, 0.1f, 0f); // 适合invector角色的检测点
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
        
        private void OnTriggerEnter(Collider other)
        {
            // 检查是否与传送门碰撞
            var portal = other.GetComponent<Portal>();
            if (portal)
            {
                
                // 检查是否为地面传送门，如果是则触发真正的跳跃输入
                HandleGroundPortalJump(portal);
            }
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
            
            Debug.Log($"[InvectorPortalAdapter] 玩家 {gameObject.name} 完成传送门穿越");
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
            
            // 处理角色方向变化
            HandleCharacterOrientation(fromPortal, toPortal);
            
            // 处理动画状态
            HandleAnimationTransition();
        }
        
        /// <summary>
        /// 处理重力方向变化（完全按照原版RigidbodyCharacterController的思路）
        /// </summary>
        private void HandleGravityChange(Portal fromPortal, Portal toPortal)
        {
            // 检查是否是垂直传送门
            if (Mathf.Abs(Vector3.Dot(fromPortal.transform.forward, Vector3.up)) > .1f
                || Mathf.Abs(Vector3.Dot(toPortal.transform.forward, Vector3.up)) > .1f)
            {
                // 获取本地速度
                var velocityLocal = toPortal.transform.InverseTransformDirection(Rigidbody.velocity);
                
                // 计算弹出高度
                float popUpHeight = transform.lossyScale.y;
                
                // 获取重力加速度（原版使用_currGravitationalAcceleration）
                float gravitationalAcceleration = GetGravitationalAcceleration();
                
                // 计算所需的垂直速度变化
                float extVelocityChangeLocalZ = Mathf.Sqrt(-(2f * gravitationalAcceleration * popUpHeight));
                
                // 计算需要添加的力
                var extVelocityChange = toPortal.transform.forward * Mathf.Clamp(extVelocityChangeLocalZ
                    - velocityLocal.z, 0f, extVelocityChangeLocalZ);
                
                // 调整位置避免碰撞
                transform.position += toPortal.transform.forward * (GetCapsuleRadius() * transform.lossyScale.y);
                
                // 应用垂直力（原版第315行）
                Rigidbody.AddForce(extVelocityChange, ForceMode.VelocityChange);
            }
        }
        
        /// <summary>
        /// 获取重力加速度（完全按照原版RigidbodyCharacterController的计算方式）
        /// </summary>
        private float GetGravitationalAcceleration()
        {
            const float gravitationalAcceleration = -9.81f *0.2f;
            
            return gravitationalAcceleration * transform.lossyScale.y;
        }
        
        /// <summary>
        /// 获取胶囊碰撞器半径
        /// </summary>
        private float GetCapsuleRadius()
        {
            var capsuleCollider = GetComponent<CapsuleCollider>();
            if (capsuleCollider)
                return capsuleCollider.radius;
            return 0.5f; // 默认半径
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
        /// 处理角色方向变化（启动平滑旋转到水平状态）
        /// </summary>
        private void HandleCharacterOrientation(Portal fromPortal, Portal toPortal)
        {
            // 获取当前旋转的Y轴角度（水平方向）
            float currentYRotation = transform.eulerAngles.y;
            
            // 创建目标旋转：只保留Y轴旋转，X和Z轴设为0
            _targetRotation = Quaternion.Euler(0f, currentYRotation, 0f);
            
            // 启动平滑旋转
            _isSmoothingRotation = true;
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
        /// 处理地面传送门跳跃 - 触发玩家一次真正的跳跃输入
        /// </summary>
        private void HandleGroundPortalJump(Portal portal)
        {
            if (!portal) return;
            
            // 获取传送门的前向向量（法向量）
            Vector3 portalForward = portal.transform.forward;
            
            // 检查传送门是否几乎垂直向上（Y分量接近1）
            float upwardThreshold = 0.8f;
            if (portalForward.y > upwardThreshold)
            {
                
                // 触发真正的跳跃输入
                TriggerRealJump();
            }
        }
        
        /// <summary>
        /// 触发真正的跳跃输入 - 模拟玩家按下跳跃键，每2秒最多触发一次，跳跃高度为真实高度的30%
        /// </summary>
        private void TriggerRealJump()
        {
            // 检查时间限制：每2秒最多触发一次
            float currentTime = Time.time;
            if (currentTime - _lastJumpTriggerTime < 2.0f)
            {
                return;
            }
            
            // 检查是否在地面且可以跳跃
            if (_invectorController.isGrounded && !_invectorController.isJumping && !_invectorController.isRolling)
            {
                // 保存原始跳跃高度
                float originalJumpHeight = (_invectorController as vThirdPersonMotor).jumpHeight;
                
                // 设置跳跃高度为真实高度的30%
                (_invectorController as vThirdPersonMotor).jumpHeight = originalJumpHeight * 0.3f;
                
                // 直接调用Invector控制器的Jump方法，模拟玩家按下跳跃键
                _invectorController.Jump(false); // false表示不消耗体力
                
                // 延迟恢复原始跳跃高度
                StartCoroutine(RestoreJumpHeightAfterDelay(originalJumpHeight, 0.5f));
                
                // 记录触发时间
                _lastJumpTriggerTime = currentTime;
            }
        }
        
        /// <summary>
        /// 延迟恢复跳跃高度
        /// </summary>
        private System.Collections.IEnumerator RestoreJumpHeightAfterDelay(float originalJumpHeight, float delay)
        {
            yield return new WaitForSeconds(delay);
            (_invectorController as vThirdPersonMotor).jumpHeight = originalJumpHeight;
        }
        
        /// <summary>
        /// 更新传送门状态和平滑旋转
        /// </summary>
        private void Update()
        {
            // 打印当前角色判定点坐标
            var transferPivot = transform.TransformPoint(TransferPivotOffset);
            //Debug.Log($"[InvectorPortalAdapter] 角色 {gameObject.name} 判定点坐标: {transferPivot} (偏移: {TransferPivotOffset})");
            
            // 检查是否在传送门中
            bool wasInPortal = _isInPortal;
            _isInPortal = PenetratingPortal != null;
            
            // 如果进入或离开传送门，更新相机系统并打印日志
            if (wasInPortal != _isInPortal && _portalSystemCameraData)
            {
                _portalSystemCameraData.PenetratingPortal = PenetratingPortal;
            }
            
            // 处理平滑旋转
            if (_isSmoothingRotation)
            {
                // 获取当前旋转的Y轴角度（保持Y轴不变）
                float currentYRotation = transform.eulerAngles.y;
                
                // 获取目标旋转的X和Z轴角度
                Vector3 targetEuler = _targetRotation.eulerAngles;
                
                // 创建只影响X和Z轴的平滑旋转
                var smoothRotation = Quaternion.Euler(
                    Mathf.LerpAngle(transform.eulerAngles.x, targetEuler.x, _smoothRotationSpeed),
                    currentYRotation, // 保持Y轴不变
                    Mathf.LerpAngle(transform.eulerAngles.z, targetEuler.z, _smoothRotationSpeed)
                );
                
                transform.rotation = smoothRotation;
                
                // 检查X和Z轴是否已经接近目标
                float xDiff = Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.x, targetEuler.x));
                float zDiff = Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.z, targetEuler.z));
                
                if (xDiff < 0.1f && zDiff < 0.1f)
                {
                    // 旋转完成，停止平滑旋转
                    transform.rotation = _targetRotation;
                    _isSmoothingRotation = false;
                }
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
                TransferPivotOffset = new Vector3(0f, 0.1f, 0f);
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
