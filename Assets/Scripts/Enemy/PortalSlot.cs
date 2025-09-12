using UnityEngine;
using System.Collections;
using Sirenix.OdinInspector;

namespace Invector.vCharacterController.AI
{
    // 移除状态机 - 插槽现在可以自由重复使用
    
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
    /// 传送门插槽 - 管理传送门的生成、VFX播放和传送门移动
    /// 插槽可以重复使用，不限制传送门数量
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
        public float vfxZOffset = 0f;
        
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
        
        [Header("调试")]
        [ShowInInspector, ReadOnly]
        public bool _isInUse = false;
        
        [ShowInInspector, ReadOnly]
        private GameObject _currentVfx;
        
        [ShowInInspector, ReadOnly]
        private bool _isTrackingPlayer = false;
        
        [ShowInInspector, ReadOnly]
        private Vector3 _originalPortalPosition;
        
        [ShowInInspector, ReadOnly]
        private Quaternion _originalPortalRotation;
        
        [ShowInInspector, ReadOnly]
        private Vector3 _vfxFinalPosition;
        
        [ShowInInspector, ReadOnly]
        public Vector3 portalWorldPosition;
        
        [ShowInInspector, ReadOnly]
        public Quaternion portalWorldRotation;
        
        // 私有变量
        private Coroutine _vfxTrackingCoroutine;
        private Coroutine _portalMoveCoroutine;
        
        #region Unity生命周期
        
        private void Start()
        {
            InitializeSlot();
        }
        
        private void Update()
        {
            // 如果有玩家追踪且正在追踪，更新VFX位置
            if (_isTrackingPlayer && playerTarget && _currentVfx)
            {
                TrackPlayer();
            }
            

        }
        
        #endregion
        
        #region 初始化
        
        /// <summary>
        /// 初始化插槽
        /// </summary>
        private void InitializeSlot()
        {
            // 保存传送门原始位置
            if (scenePortal)
            {
                _originalPortalPosition = scenePortal.transform.position;
                _originalPortalRotation = scenePortal.transform.rotation;
            }
            
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
            
        }
        
        #endregion
        
        // 移除状态管理 - 插槽现在可以自由重复使用
        
        #region 公共方法
        
        /// <summary>
        /// 开始生成传送门（插槽可重复使用）
        /// </summary>
        /// <param name="portalColor">传送门颜色</param>
        /// <param name="generatingVfxPrefab">生成VFX预制体</param>
        public void StartGenerating(PortalColor portalColor, GameObject generatingVfxPrefab)
        {
            if (generatingVfxPrefab == null)
            {
                Debug.LogError($"[PortalSlot] 生成VFX预制体为空！无法开始生成传送门");
                return;
            }
            
            _isInUse = true;
            
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
        /// <param name="telegraphingVfxPrefab">前摇VFX预制体</param>
        /// <param name="portalObject">传送门对象</param>
        public void StartTelegraphing(float telegraphDuration, GameObject telegraphingVfxPrefab, GameObject portalObject)
        {
            if (telegraphingVfxPrefab == null)
            {
                Debug.LogError($"[PortalSlot] 前摇VFX预制体为空！无法开始前摇");
                return;
            }
            
            if (portalObject == null)
            {
                Debug.LogError($"[PortalSlot] 传送门对象为空！无法开始前摇");
                return;
            }
            
            _isInUse = true;
            
            // 设置VFX预制体和传送门对象
            this.telegraphingVfxPrefab = telegraphingVfxPrefab;
            this.scenePortal = portalObject;
            
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
            
            // 先开始移动传送门到VFX的最终位置
            StartPortalMovement(telegraphDuration);
        }
        
        /// <summary>
        /// 激活传送门
        /// </summary>
        public void ActivatePortal()
        {
            _isInUse = true;
            
            // 停止前摇VFX
            StopTelegraphingVfx();
            
            // 传送门已就位
            if (scenePortal)
            {
                scenePortal.SetActive(true);
            }
            
        }
        
        /// <summary>
        /// 关闭传送门
        /// </summary>
        public void ClosePortal()
        {
            _isInUse = false;
            
            // 停止所有VFX
            StopAllVfx();
            
            // 停止追踪
            StopPlayerTracking();
            
            // 移动传送门回原位
            StartPortalReturn();
            
        }
        
        /// <summary>
        /// 重置插槽到空闲状态
        /// </summary>
        public void ResetSlot()
        {
            _isInUse = false;
            
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

        }
        
        /// <summary>
        /// 检查插槽是否正在使用中
        /// </summary>
        /// <returns>是否正在使用</returns>
        public bool IsInUse()
        {
            return _isInUse;
        }
        
        /// <summary>
        /// 获取传送门世界坐标位置
        /// </summary>
        /// <returns>传送门世界坐标位置</returns>
        public Vector3 GetPortalWorldPosition()
        {
            return portalWorldPosition;
        }
        
        /// <summary>
        /// 获取传送门世界坐标旋转
        /// </summary>
        /// <returns>传送门世界坐标旋转</returns>
        public Quaternion GetPortalWorldRotation()
        {
            return portalWorldRotation;
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
            
            // 在传送门的世界坐标位置生成VFX
            Vector3 vfxPosition = portalWorldPosition;
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
        /// 获取Quad的本地大小（用于VFX移动范围计算，与Gizmos显示同步）
        /// </summary>
        /// <param name="quadTransform">Quad的Transform</param>
        /// <returns>Quad的本地大小</returns>
        private Vector3 GetQuadSize(Transform quadTransform)
        {
            // 获取Mesh的本地边界（与Gizmos显示使用相同的逻辑）
            var meshFilter = quadTransform.GetComponent<MeshFilter>();
            if (meshFilter && meshFilter.mesh)
            {
                // 使用Mesh的本地边界，这样不受任何变换影响
                Vector3 meshSize = meshFilter.mesh.bounds.size;
                return meshSize; // 返回原始Mesh大小，不应用localScale
            }
            
            // 如果没有MeshFilter，使用默认Quad大小
            return Vector3.one; // Unity的默认Quad是1x1
        }
        
        #endregion
        
        #region 传送门移动
        
        /// <summary>
        /// 开始传送门移动
        /// </summary>
        /// <param name="duration">移动持续时间</param>
        private void StartPortalMovement(float duration)
        {
            if (!scenePortal) return;
            
            _portalMoveCoroutine = StartCoroutine(MovePortalCoroutine(duration));
        }
        
        /// <summary>
        /// 传送门移动协程
        /// </summary>
        /// <param name="duration">移动持续时间</param>
        private IEnumerator MovePortalCoroutine(float duration)
        {
            // 使用传送门的当前位置作为起始位置，而不是_originalPortalPosition
            Vector3 startPos = scenePortal.transform.position;
            // 传送门移动到VFX的最终位置
            Vector3 endPos = _vfxFinalPosition;
            Quaternion startRot = scenePortal.transform.rotation;
            Quaternion endRot = vfxSpawnPoint.rotation;
            
            // 传送门瞬移旋转和位置到最终位置
            scenePortal.transform.rotation = endRot;
            scenePortal.transform.position = endPos;
            
            // 获取传送门瞬移后的世界坐标和旋转（考虑一级父对象）
            if (scenePortal.transform.parent)
            {
                // 如果有父对象，计算世界坐标和旋转
                portalWorldPosition = scenePortal.transform.parent.TransformPoint(scenePortal.transform.position);
                portalWorldRotation = scenePortal.transform.parent.rotation * scenePortal.transform.rotation;
            }
            else
            {
                // 如果没有父对象，直接使用本地坐标和旋转
                portalWorldPosition = scenePortal.transform.position;
                portalWorldRotation = scenePortal.transform.rotation;
            }
            
            Debug.Log($"[PortalSlot] {gameObject.name} - 设置portalWorldRotation: {portalWorldRotation.eulerAngles}");
            
            
            // 传送门移动完成后，播放前摇VFX
            PlayTelegraphingVfx();
            
            // 等待指定时间（用于前摇效果）
            yield return new WaitForSeconds(duration);
            
            // 激活传送门
            ActivatePortal();
        }
        
        /// <summary>
        /// 开始传送门返回
        /// </summary>
        private void StartPortalReturn()
        {
            if (!scenePortal) return;
            
            _portalMoveCoroutine = StartCoroutine(ReturnPortalCoroutine());
        }
        
        /// <summary>
        /// 传送门返回协程
        /// </summary>
        private IEnumerator ReturnPortalCoroutine()
        {
            Vector3 startPos = scenePortal.transform.position;
            Vector3 endPos = _originalPortalPosition;
            Quaternion startRot = scenePortal.transform.rotation;
            Quaternion endRot = _originalPortalRotation;
            
            float duration = 1f; // 返回时间
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float curveT = portalMoveCurve.Evaluate(t);
                
                scenePortal.transform.position = Vector3.Lerp(startPos, endPos, curveT);
                scenePortal.transform.rotation = Quaternion.Lerp(startRot, endRot, curveT);
                
                yield return null;
            }
            
            // 确保最终位置正确
            scenePortal.transform.position = endPos;
            scenePortal.transform.rotation = endRot;
            scenePortal.SetActive(false);
            
            // 重置到空闲状态
            _isInUse = false;
        }
        
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
        
        [Button("测试关闭传送门")]
        public void TestClosePortal()
        {
            ClosePortal();
        }
        
        [Button("重置插槽")]
        public void TestResetSlot()
        {
            ResetSlot();
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
            
            // 绘制插槽平面范围（青色）
            if (slotPlane)
            {
                Gizmos.color = Color.cyan;
                Gizmos.matrix = Matrix4x4.identity; // 不使用localToWorldMatrix，避免双重变换
                
                // 获取Quad的本地大小（不包含缩放）
                var meshFilter = slotPlane.GetComponent<MeshFilter>();
                Vector3 localSize = Vector3.one; // 默认Quad大小
                if (meshFilter && meshFilter.mesh)
                {
                    localSize = meshFilter.mesh.bounds.size;
                }
                
                // 绘制本地大小的立方体，让Gizmos自动应用变换
                Gizmos.matrix = slotPlane.localToWorldMatrix;
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(localSize.x, localSize.y, 0.1f));
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
