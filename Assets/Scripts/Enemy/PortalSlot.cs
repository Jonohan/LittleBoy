using UnityEngine;
using System.Collections;
using Sirenix.OdinInspector;

namespace Invector.vCharacterController.AI
{
    /// <summary>
    /// 传送门状态（每个传送门独立的状态）
    /// </summary>
    public enum PortalState
    {
        Idle,           // 空闲
        Generating,     // 生成中 (VFX播放)
        Telegraphing,   // 前摇阶段 (传送门移动过来)
        Active,         // 激活状态 (传送门就位)
        Closing         // 关闭中
    }
    
    /// <summary>
    /// VFX实例数据
    /// </summary>
    [System.Serializable]
    public class PortalVfxInstance
    {
        public int portalId;                    // 传送门ID
        public GameObject vfxObject;            // VFX对象
        public PortalState state;               // 当前状态
        public Vector3 finalPosition;           // 最终位置
        public float startTime;                 // 开始时间
        public PortalColor color;               // 传送门颜色
    }
    
    /// <summary>
    /// 传送门插槽类型
    /// </summary>
    public enum PortalSlotType
    {
        Ceiling,        // 天花板
        WallLeft,       // 左墙
        WallRight,      // 右墙
        Ground          // 地面
    }
    
    /// <summary>
    /// 传送门插槽 - 为多个传送门提供VFX播放和位置追踪服务
    /// </summary>
    public class PortalSlot : MonoBehaviour
    {
        [Header("插槽配置")]
        [Tooltip("插槽类型")]
        public PortalSlotType slotType = PortalSlotType.Ceiling;
        
        [Tooltip("插槽平面 (用于VFX播放)")]
        public Transform slotPlane;
        
        [Tooltip("VFX生成点")]
        public Transform vfxSpawnPoint;
        
        [Header("VFX配置")]
        [Tooltip("生成阶段VFX预制体（从PortalManager获取）")]
        [HideInInspector]
        public GameObject generatingVfxPrefab;
        
        [Tooltip("前摇阶段VFX预制体（从PortalManager获取）")]
        [HideInInspector]
        public GameObject telegraphingVfxPrefab;
        
        [Tooltip("VFX移动速度")]
        public float vfxMoveSpeed = 1.5f;
        
        [Tooltip("VFX追踪玩家的强度")]
        [Range(0f, 1f)]
        public float playerTrackingStrength = 0.2f;
        
        [Tooltip("VFX位置偏移（沿Quad Z方向调整）")]
        public float vfxZOffset = 0.5f;
        
        [Header("传送门配置")]
        [Tooltip("场景中的传送门对象（从PortalManager获取）")]
        [HideInInspector]
        public GameObject scenePortal;
        
        [Tooltip("传送门移动速度")]
        public float portalMoveSpeed = 5f;
        
        [Tooltip("传送门移动曲线")]
        public AnimationCurve portalMoveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        [Header("目标追踪")]
        [Tooltip("玩家引用")]
        public Transform playerTarget;
        
        [Tooltip("追踪范围")]
        public float trackingRange = 25f;
        
        [Tooltip("传送门激活后解除插槽占用的延迟时间")]
        public float slotReleaseDelay = 1f;
        
        [Header("调试")]
        [ShowInInspector, ReadOnly]
        private int _activePortalCount = 0;
        
        [ShowInInspector, ReadOnly]
        private System.Collections.Generic.List<PortalVfxInstance> _activeVfxInstances = new System.Collections.Generic.List<PortalVfxInstance>();
        
        // 私有变量
        private System.Collections.Generic.Dictionary<int, Coroutine> _vfxTrackingCoroutines = new System.Collections.Generic.Dictionary<int, Coroutine>();
        private System.Collections.Generic.Dictionary<int, Coroutine> _portalMoveCoroutines = new System.Collections.Generic.Dictionary<int, Coroutine>();
        
        #region Unity生命周期
        
        private void Start()
        {
            InitializeSlot();
        }
        
        private void Update()
        {
            // 插槽本身不需要状态更新，每个VFX实例独立管理
        }
        
        #endregion
        
        #region 初始化
        
        /// <summary>
        /// 初始化插槽
        /// </summary>
        private void InitializeSlot()
        {
            // 查找玩家目标
            if (!playerTarget)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player)
                {
                    playerTarget = player.transform;
                }
            }
            
            // 设置插槽平面
            if (!slotPlane)
            {
                slotPlane = transform;
            }
            
            // 设置VFX生成点
            if (!vfxSpawnPoint)
            {
                vfxSpawnPoint = transform;
            }
            
            Debug.Log($"[PortalSlot] 插槽初始化完成: {slotType}");
        }
        
        #endregion
        
        #region 状态管理
        
        /// <summary>
        /// 更新插槽状态
        /// </summary>
        private void UpdateSlotState()
        {
            switch (_currentState)
            {
                case PortalSlotState.Generating:
                    UpdateGeneratingState();
                    break;
                case PortalSlotState.Telegraphing:
                    UpdateTelegraphingState();
                    break;
                case PortalSlotState.Active:
                    UpdateActiveState();
                    break;
                case PortalSlotState.Closing:
                    UpdateClosingState();
                    break;
            }
        }
        
        /// <summary>
        /// 更新生成状态
        /// </summary>
        private void UpdateGeneratingState()
        {
            // VFX持续播放，追踪玩家
            if (_isTrackingPlayer && playerTarget)
            {
                TrackPlayer();
            }
        }
        
        /// <summary>
        /// 更新前摇状态
        /// </summary>
        private void UpdateTelegraphingState()
        {
            // 传送门正在移动过来
            // 状态由协程管理
        }
        
        /// <summary>
        /// 更新激活状态
        /// </summary>
        private void UpdateActiveState()
        {
            // 传送门已就位，可以正常使用
        }
        
        /// <summary>
        /// 更新关闭状态
        /// </summary>
        private void UpdateClosingState()
        {
            // 传送门正在关闭
        }
        
        #endregion
        
        #region 公共方法
        
        /// <summary>
        /// 开始生成传送门
        /// </summary>
        /// <param name="portalColor">传送门颜色</param>
        /// <param name="generatingVfxPrefab">生成阶段VFX预制体（从PortalManager传入）</param>
        public void StartGenerating(PortalColor portalColor, GameObject generatingVfxPrefab)
        {
            if (_currentState != PortalSlotState.Idle)
            {
                Debug.LogWarning($"[PortalSlot] 插槽正在使用中，无法开始生成");
                return;
            }
            
            if (generatingVfxPrefab == null)
            {
                Debug.LogError($"[PortalSlot] 生成VFX预制体为空！无法开始生成传送门");
                return;
            }
            
            _currentState = PortalSlotState.Generating;
            
            // 设置VFX预制体
            this.generatingVfxPrefab = generatingVfxPrefab;
            
            // 播放生成VFX
            PlayGeneratingVfx(portalColor);
            
            // 开始追踪玩家
            StartPlayerTracking();
            
            Debug.Log($"[PortalSlot] 开始生成传送门: {slotType} {portalColor}");
        }
        
        /// <summary>
        /// 开始前摇阶段
        /// </summary>
        /// <param name="telegraphDuration">前摇持续时间</param>
        /// <param name="telegraphingVfxPrefab">前摇阶段VFX预制体（从PortalManager传入）</param>
        /// <param name="portalObject">传送门对象（从PortalManager传入）</param>
        public void StartTelegraphing(float telegraphDuration, GameObject telegraphingVfxPrefab, GameObject portalObject)
        {
            if (_currentState != PortalSlotState.Generating)
            {
                Debug.LogWarning($"[PortalSlot] 插槽状态错误，无法开始前摇");
                return;
            }
            
            if (telegraphingVfxPrefab == null)
            {
                Debug.LogError($"[PortalSlot] 前摇VFX预制体为空！无法开始前摇阶段");
                return;
            }
            
            if (portalObject == null)
            {
                Debug.LogError($"[PortalSlot] 传送门对象为空！无法开始前摇阶段");
                return;
            }
            
            _currentState = PortalSlotState.Telegraphing;
            
            // 停止玩家追踪（VFX停在当前位置）
            StopPlayerTracking();
            
            // 保存VFX的最终位置
            if (_currentVfx)
            {
                _vfxFinalPosition = _currentVfx.transform.position;
            }
            else
            {
                _vfxFinalPosition = vfxSpawnPoint.position;
            }
            
            // 停止循环播放的生成VFX
            StopGeneratingVfx();
            
            // 设置前摇VFX预制体
            this.telegraphingVfxPrefab = telegraphingVfxPrefab;
            
            // 保存传送门原始位置（当第一次设置传送门对象时）
            if (portalObject && _originalPortalPosition == Vector3.zero)
            {
                _originalPortalPosition = portalObject.transform.position;
                _originalPortalRotation = portalObject.transform.rotation;
            }
            
            // 播放前摇VFX（与传送门移动同时进行）
            PlayTelegraphingVfx();
            
            // 开始移动传送门到VFX的最终位置
            StartPortalMovement(telegraphDuration, portalObject);
            
            Debug.Log($"[PortalSlot] 开始前摇阶段: {slotType}");
        }
        
        /// <summary>
        /// 激活传送门
        /// </summary>
        public void ActivatePortal()
        {
            if (_currentState != PortalSlotState.Telegraphing)
            {
                Debug.LogWarning($"[PortalSlot] 插槽状态错误，无法激活传送门");
                return;
            }
            
            _currentState = PortalSlotState.Active;
            
            // 停止前摇VFX
            StopTelegraphingVfx();
            
            // 传送门已就位（保持激活状态，不需要改变SetActive）
            
            Debug.Log($"[PortalSlot] 传送门激活: {slotType}");
            
            // 延迟一段时间后解除插槽占用，允许生成下一个传送门
            StartCoroutine(ReleaseSlotAfterDelay(slotReleaseDelay));
        }
        
        /// <summary>
        /// 延迟解除插槽占用
        /// </summary>
        /// <param name="delay">延迟时间</param>
        private IEnumerator ReleaseSlotAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            
            // 解除插槽占用，但保持传送门激活状态
            _currentState = PortalSlotState.Idle;
            
            // 清理VFX相关状态
            _currentVfx = null;
            _isTrackingPlayer = false;
            
            Debug.Log($"[PortalSlot] 插槽已解除占用，可继续生成下一个传送门: {slotType}");
        }
        
        // 传送门不需要关闭，它们一直存在
        
        /// <summary>
        /// 重置插槽到空闲状态
        /// </summary>
        public void ResetSlot()
        {
            _currentState = PortalSlotState.Idle;
            
            // 停止所有VFX
            StopAllVfx();
            
            // 停止追踪
            StopPlayerTracking();
            
            // 重置传送门位置
            if (scenePortal)
            {
                scenePortal.transform.position = _originalPortalPosition;
                scenePortal.transform.rotation = _originalPortalRotation;
                scenePortal.SetActive(false);
            }
            
            Debug.Log($"[PortalSlot] 插槽重置: {slotType}");
        }
        
        #endregion
        
        #region VFX管理
        
        /// <summary>
        /// 播放生成VFX
        /// </summary>
        /// <param name="portalColor">传送门颜色</param>
        private void PlayGeneratingVfx(PortalColor portalColor)
        {
            if (!generatingVfxPrefab) return;
            
            // 计算VFX位置：沿Quad Z方向偏移
            Vector3 vfxPosition = CalculateVfxPosition();
            
            // 计算VFX朝向：Y轴朝向平面Z轴正方向
            Quaternion vfxRotation = CalculateVfxRotation();
            
            _currentVfx = UnityEngine.Object.Instantiate(generatingVfxPrefab, vfxPosition, vfxRotation);
            
            // 根据颜色调整VFX
            AdjustVfxForColor(_currentVfx, portalColor);
        }
        
        /// <summary>
        /// 播放前摇VFX
        /// </summary>
        private void PlayTelegraphingVfx()
        {
            if (!telegraphingVfxPrefab) return;
            
            if (_currentVfx)
            {
                UnityEngine.Object.Destroy(_currentVfx);
            }
            
            // 计算VFX位置：沿Quad Z方向偏移
            Vector3 vfxPosition = CalculateVfxPosition();
            
            // 计算VFX朝向：Y轴朝向平面Z轴正方向
            Quaternion vfxRotation = CalculateVfxRotation();
            
            _currentVfx = UnityEngine.Object.Instantiate(telegraphingVfxPrefab, vfxPosition, vfxRotation);
        }
        
        /// <summary>
        /// 停止生成VFX
        /// </summary>
        private void StopGeneratingVfx()
        {
            if (_currentVfx)
            {
                UnityEngine.Object.Destroy(_currentVfx);
                _currentVfx = null;
            }
        }
        
        /// <summary>
        /// 停止前摇VFX
        /// </summary>
        private void StopTelegraphingVfx()
        {
            if (_currentVfx)
            {
                UnityEngine.Object.Destroy(_currentVfx);
                _currentVfx = null;
            }
        }
        
        /// <summary>
        /// 停止所有VFX
        /// </summary>
        private void StopAllVfx()
        {
            StopGeneratingVfx();
            StopTelegraphingVfx();
        }
        
        /// <summary>
        /// 计算VFX位置
        /// </summary>
        /// <returns>VFX的位置</returns>
        private Vector3 CalculateVfxPosition()
        {
            if (!slotPlane || !vfxSpawnPoint) return Vector3.zero;
            
            // 获取Quad的Z轴方向（forward）
            Vector3 quadZDirection = slotPlane.forward;
            
            // 计算VFX位置：生成点位置 + 沿Z方向的偏移
            Vector3 vfxPosition = vfxSpawnPoint.position + quadZDirection * vfxZOffset;
            
            return vfxPosition;
        }
        
        /// <summary>
        /// 计算VFX朝向
        /// </summary>
        /// <returns>VFX的旋转</returns>
        private Quaternion CalculateVfxRotation()
        {
            if (!slotPlane) return Quaternion.identity;
            
            // 获取平面的Z轴正方向（forward）
            Vector3 planeZForward = slotPlane.forward;
            
            // 计算VFX的旋转：让VFX的Y轴朝向Quad的Z轴正方向
            // 使用Quaternion.FromToRotation从世界Y轴旋转到Quad的Z轴正方向
            Quaternion baseRotation = Quaternion.FromToRotation(Vector3.up, planeZForward);
            
            // 调试打印
            Vector3 vfxYDirection = baseRotation * Vector3.up;
            Debug.Log($"[PortalSlot] Quad正方向: {planeZForward}, VFX Y方向: {vfxYDirection}");
            
            return baseRotation;
        }
        
        /// <summary>
        /// 根据颜色调整VFX
        /// </summary>
        /// <param name="vfx">VFX对象</param>
        /// <param name="color">传送门颜色</param>
        private void AdjustVfxForColor(GameObject vfx, PortalColor color)
        {
            // 这里可以根据颜色调整VFX的外观
            // 例如改变颜色、强度等
            var renderer = vfx.GetComponent<Renderer>();
            if (renderer)
            {
                switch (color)
                {
                    case PortalColor.Blue:
                        renderer.material.color = Color.blue;
                        break;
                    case PortalColor.Orange:
                        renderer.material.color = new Color(1f, 0.5f, 0f); // 橙色
                        break;
                    case PortalColor.GiantOrange:
                        renderer.material.color = Color.red;
                        // 巨型传送门可以调整大小
                        vfx.transform.localScale *= 1.5f;
                        break;
                }
            }
        }
        
        #endregion
        
        #region 玩家追踪
        
        /// <summary>
        /// 开始追踪玩家
        /// </summary>
        private void StartPlayerTracking()
        {
            if (!playerTarget) return;
            
            _isTrackingPlayer = true;
            _vfxTrackingCoroutine = StartCoroutine(TrackPlayerCoroutine());
        }
        
        /// <summary>
        /// 停止追踪玩家
        /// </summary>
        private void StopPlayerTracking()
        {
            _isTrackingPlayer = false;
            
            if (_vfxTrackingCoroutine != null)
            {
                StopCoroutine(_vfxTrackingCoroutine);
                _vfxTrackingCoroutine = null;
            }
        }
        
        /// <summary>
        /// 追踪玩家协程
        /// </summary>
        private IEnumerator TrackPlayerCoroutine()
        {
            while (_isTrackingPlayer && _currentVfx)
            {
                TrackPlayer();
                yield return null;
            }
        }
        
        /// <summary>
        /// 追踪玩家
        /// </summary>
        private void TrackPlayer()
        {
            if (!playerTarget || !_currentVfx) return;
            
            // 计算玩家在插槽平面上的投影
            Vector3 playerPos = playerTarget.position;
            Vector3 slotPos = slotPlane.position;
            Vector3 slotNormal = slotPlane.forward;
            
            // 将玩家位置投影到插槽平面上
            Vector3 projectedPos = playerPos - Vector3.Project(playerPos - slotPos, slotNormal);
            
            // 限制在插槽范围内
            Vector3 localPos = slotPlane.InverseTransformPoint(projectedPos);
            
            // 获取Quad的实际大小
            Vector3 quadSize = GetQuadSize(slotPlane);
            
            // 限制在Quad的矩形边界内
            localPos.x = Mathf.Clamp(localPos.x, -quadSize.x * 0.5f, quadSize.x * 0.5f);
            localPos.y = Mathf.Clamp(localPos.y, -quadSize.y * 0.5f, quadSize.y * 0.5f);
            localPos.z = 0f; // 保持在平面上
            
            Vector3 targetPos = slotPlane.TransformPoint(localPos);
            
            // 应用Z轴偏移
            Vector3 quadZDirection = slotPlane.forward;
            targetPos += quadZDirection * vfxZOffset;
            
            // 平滑移动VFX
            Vector3 currentPos = _currentVfx.transform.position;
            Vector3 newPos = Vector3.Lerp(currentPos, targetPos, playerTrackingStrength * Time.deltaTime * vfxMoveSpeed);
            
            _currentVfx.transform.position = newPos;
        }
        
        /// <summary>
        /// 获取Quad的实际大小（不受父节点缩放影响）
        /// </summary>
        /// <param name="quadTransform">Quad的Transform</param>
        /// <returns>Quad的大小</returns>
        private Vector3 GetQuadSize(Transform quadTransform)
        {
            // 检查是否有MeshRenderer来获取实际大小
            var renderer = quadTransform.GetComponent<MeshRenderer>();
            if (renderer)
            {
                // 使用MeshRenderer的bounds来获取实际大小
                // 注意：bounds.size是世界坐标大小，需要转换为本地坐标
                Vector3 worldSize = renderer.bounds.size;
                
                // 将世界坐标大小转换为本地坐标大小
                // 使用lossyScale来去除父节点缩放的影响
                Vector3 localSize = new Vector3(
                    worldSize.x / quadTransform.lossyScale.x,
                    worldSize.y / quadTransform.lossyScale.y,
                    worldSize.z / quadTransform.lossyScale.z
                );
                
                return localSize;
            }
            
            // 如果没有MeshRenderer，使用Transform的localScale
            // Unity的默认Quad是1x1，所以实际大小 = localScale
            Vector3 scale = quadTransform.localScale;
            return scale;
        }
        
        #endregion
        
        #region 传送门移动
        
        /// <summary>
        /// 开始传送门移动
        /// </summary>
        /// <param name="duration">移动持续时间</param>
        /// <param name="portalObject">传送门对象</param>
        private void StartPortalMovement(float duration, GameObject portalObject)
        {
            if (!portalObject) return;
            
            _portalMoveCoroutine = StartCoroutine(MovePortalCoroutine(duration, portalObject));
        }
        
        /// <summary>
        /// 传送门移动协程
        /// </summary>
        /// <param name="duration">移动持续时间</param>
        /// <param name="portalObject">传送门对象</param>
        private IEnumerator MovePortalCoroutine(float duration, GameObject portalObject)
        {
            Vector3 startPos = _originalPortalPosition;
            // 传送门移动到VFX的最终位置
            Vector3 endPos = _vfxFinalPosition;
            Quaternion startRot = _originalPortalRotation;
            Quaternion endRot = vfxSpawnPoint.rotation;
            
            // 传送门瞬移旋转和位置到最终位置
            portalObject.transform.rotation = endRot;
            portalObject.transform.position = endPos;
            
            // 等待指定时间（用于前摇效果）
            yield return new WaitForSeconds(duration);
            
            // 激活传送门
            ActivatePortal();
        }
        
        // 传送门不需要返回，它们一直存在
        
        #endregion
        
        #region 调试方法
        
        [Button("测试生成传送门")]
        public void TestStartGenerating()
        {
            Debug.LogWarning("[PortalSlot] 测试生成传送门：请使用PortalManager的测试方法！PortalSlot不管理VFX资源。");
        }
        
        [Button("测试前摇阶段")]
        public void TestStartTelegraphing()
        {
            Debug.LogWarning("[PortalSlot] 测试前摇阶段：请使用PortalManager的测试方法！PortalSlot不管理VFX和传送门资源。");
        }
        
        // 传送门不需要关闭，它们一直存在
        
        [Button("重置插槽")]
        public void TestResetSlot()
        {
            ResetSlot();
        }
        
        [Button("测试插槽释放")]
        public void TestReleaseSlot()
        {
            if (_currentState == PortalSlotState.Active)
            {
                StartCoroutine(ReleaseSlotAfterDelay(0.1f));
                Debug.Log("[PortalSlot] 手动触发插槽释放");
            }
            else
            {
                Debug.LogWarning($"[PortalSlot] 当前状态 {_currentState} 无法释放插槽，需要先激活传送门");
            }
        }
        
        #endregion
        
        #region 调试显示
        
        private void OnDrawGizmosSelected()
        {
            // 绘制追踪范围
            if (slotPlane)
            {
                Gizmos.color = Color.yellow;
                Gizmos.matrix = Matrix4x4.identity;
                Gizmos.DrawWireSphere(slotPlane.position, trackingRange);
            }
            
            // 绘制青色范围
            
            if (slotPlane)
            {
                Gizmos.color = Color.cyan;
                Gizmos.matrix = slotPlane.localToWorldMatrix;
                Vector3 quadSize = GetQuadSize(slotPlane);
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(quadSize.x, quadSize.y, 0.1f));
                Gizmos.matrix = Matrix4x4.identity; // 重置矩阵，避免影响后续绘制
            }
            
            
            // 绘制VFX生成点（固定位置）
            if (vfxSpawnPoint)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(vfxSpawnPoint.position, 0.2f);
            }
            
            // 绘制追踪目标位置（跟随玩家）
            if (slotPlane && playerTarget)
            {
                Vector3 targetPos = CalculateTrackingTargetPosition();
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(targetPos, 0.3f);
            }
        }
        
        /// <summary>
        /// 计算追踪目标位置（用于调试显示）
        /// </summary>
        /// <returns>追踪目标位置</returns>
        private Vector3 CalculateTrackingTargetPosition()
        {
            if (!playerTarget || !slotPlane) return Vector3.zero;
            
            // 计算玩家在插槽平面上的投影
            Vector3 playerPos = playerTarget.position;
            Vector3 slotPos = slotPlane.position;
            Vector3 slotNormal = slotPlane.forward;
            
            // 将玩家位置投影到插槽平面上
            Vector3 projectedPos = playerPos - Vector3.Project(playerPos - slotPos, slotNormal);
            
            // 限制在插槽范围内
            Vector3 localPos = slotPlane.InverseTransformPoint(projectedPos);
            
            // 获取Quad的实际大小
            Vector3 quadSize = GetQuadSize(slotPlane);
            
            // 限制在Quad的矩形边界内
            localPos.x = Mathf.Clamp(localPos.x, -quadSize.x * 0.5f, quadSize.x * 0.5f);
            localPos.y = Mathf.Clamp(localPos.y, -quadSize.y * 0.5f, quadSize.y * 0.5f);
            localPos.z = 0f; // 保持在平面上
            
            return slotPlane.TransformPoint(localPos);
        }
        
        #endregion
    }
}
