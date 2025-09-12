using UnityEngine;
using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;

namespace Invector.vCharacterController.AI
{
    /// <summary>
    /// Cast阶段管理器
    /// 管理不同攻击的cast阶段，包括触手攻击的VFX和Feel效果
    /// </summary>
    public class CastManager : MonoBehaviour
    {
        [Header("Boss引用")]
        [Tooltip("Boss黑板引用")]
        public BossBlackboard bossBlackboard;
        
        [Header("触手攻击配置")]
        [Tooltip("上方触手攻击VFX")]
        public GameObject tentacleUpVfx;
        
        [Tooltip("下方触手攻击VFX")]
        public GameObject tentacleDownVfx;
        
        [Tooltip("左方触手攻击VFX")]
        public GameObject tentacleLeftVfx;
        
        [Tooltip("右方触手攻击VFX")]
        public GameObject tentacleRightVfx;
        
        [Header("Feel效果配置")]
        [Tooltip("上方触手攻击Feel效果")]
        public MMF_Player tentacleUpFeel;
        
        [Tooltip("下方触手攻击Feel效果")]
        public MMF_Player tentacleDownFeel;
        
        [Tooltip("左方触手攻击Feel效果")]
        public MMF_Player tentacleLeftFeel;
        
        [Tooltip("右方触手攻击Feel效果")]
        public MMF_Player tentacleRightFeel;
        
        [Header("动画配置")]
        [Tooltip("左右触手旋转动画持续时间")]
        public float horizontalRotationDuration = 1f;
        
        [Tooltip("上下触手弹跳动画持续时间")]
        public float verticalBounceDuration = 0.8f;
        
        [Tooltip("上下触手向上移动距离")]
        public float upwardMoveDistance = 5f;
        
        [Tooltip("上下触手向下弹跳距离")]
        public float downwardBounceDistance = 4f;
        
        [Header("调试信息")]
        [ShowInInspector, ReadOnly]
        private string _lastExecutedAttack = "None";
        
        [ShowInInspector, ReadOnly]
        private Vector3 _lastCastPosition = Vector3.zero;
        
        // 私有变量
        private PortalManager _portalManager;
        private bool _initialized = false;
        
        #region Unity生命周期
        
        private void Awake()
        {
            InitializeManager();
        }
        
        #endregion
        
        #region 初始化
        
        /// <summary>
        /// 初始化管理器
        /// </summary>
        private void InitializeManager()
        {
            if (!bossBlackboard)
            {
                bossBlackboard = GetComponent<BossBlackboard>();
            }
            
            // 直接从同一个Boss对象获取PortalManager
            if (!_portalManager)
            {
                _portalManager = GetComponent<PortalManager>();
            }
            
            _initialized = true;
            Debug.Log("[CastManager] 初始化完成");
        }
        
        #endregion
        
        #region 触手攻击Cast阶段
        
        /// <summary>
        /// 执行上方触手攻击Cast阶段
        /// </summary>
        public void ExecuteTentacleUpCast()
        {
            ExecuteTentacleCast("tentacle_up", tentacleUpVfx, tentacleUpFeel, false);
        }
        
        /// <summary>
        /// 执行下方触手攻击Cast阶段
        /// </summary>
        public void ExecuteTentacleDownCast()
        {
            ExecuteTentacleCast("tentacle_down", tentacleDownVfx, tentacleDownFeel, false);
        }
        
        /// <summary>
        /// 执行左方触手攻击Cast阶段
        /// </summary>
        public void ExecuteTentacleLeftCast()
        {
            ExecuteTentacleCast("tentacle_left", tentacleLeftVfx, tentacleLeftFeel, true);
        }
        
        /// <summary>
        /// 执行右方触手攻击Cast阶段
        /// </summary>
        public void ExecuteTentacleRightCast()
        {
            ExecuteTentacleCast("tentacle_right", tentacleRightVfx, tentacleRightFeel, true);
        }
        
        /// <summary>
        /// 通用触手攻击Cast阶段执行
        /// </summary>
        /// <param name="attackName">攻击名称</param>
        /// <param name="vfxPrefab">VFX预制体</param>
        /// <param name="feelPlayer">Feel播放器</param>
        /// <param name="isHorizontalAttack">是否为水平攻击（左右触手）</param>
        private void ExecuteTentacleCast(string attackName, GameObject vfxPrefab, MMF_Player feelPlayer, bool isHorizontalAttack)
        {
            if (!_initialized)
            {
                Debug.LogWarning("[CastManager] 管理器未初始化");
                return;
            }
            
            // 获取当前传送门位置
            Vector3 castPosition = GetCurrentPortalPosition();
            if (castPosition == Vector3.zero)
            {
                Debug.LogWarning($"[CastManager] 无法获取传送门位置，跳过 {attackName} cast阶段");
                return;
            }
            
            // 播放VFX
            PlayCastVfx(castPosition, vfxPrefab, attackName);
            
            // 播放Feel效果
            PlayCastFeel(castPosition, feelPlayer, attackName);
            
            // 激活BossPart攻击
            ActivateBossPartAttack(isHorizontalAttack);
            
            // 更新调试信息
            _lastExecutedAttack = attackName;
            _lastCastPosition = castPosition;
            
            Debug.Log($"[CastManager] 执行 {attackName} cast阶段，位置: {castPosition}");
        }
        
        #endregion
        
        #region 辅助方法
        
        /// <summary>
        /// 获取当前传送门位置
        /// </summary>
        /// <returns>传送门世界坐标位置</returns>
        private Vector3 GetCurrentPortalPosition()
        {
            if (!_portalManager)
            {
                Debug.LogWarning("[CastManager] PortalManager未找到");
                return Vector3.zero;
            }
            
            // 获取最后生成的传送门数据
            var portalData = _portalManager.GetLastGeneratedPortalData();
            if (portalData?.portalSlot != null)
            {
                // 获取传送门的世界坐标位置
                return portalData.portalSlot.GetPortalWorldPosition();
            }
            
            Debug.LogWarning("[CastManager] 无法获取传送门数据");
            return Vector3.zero;
        }
        
        /// <summary>
        /// 获取当前传送门旋转
        /// </summary>
        /// <returns>传送门世界坐标旋转</returns>
        private Quaternion GetCurrentPortalRotation()
        {
            if (!_portalManager)
            {
                Debug.LogWarning("[CastManager] PortalManager未找到");
                return Quaternion.identity;
            }
            
            // 获取最后生成的传送门数据
            var portalData = _portalManager.GetLastGeneratedPortalData();
            if (portalData?.portalSlot != null)
            {
                // 获取传送门的世界坐标旋转
                Quaternion rotation = portalData.portalSlot.GetPortalWorldRotation();
                Debug.Log($"[CastManager] 获取传送门旋转: {rotation.eulerAngles}");
                return rotation;
            }
            
            Debug.LogWarning("[CastManager] 无法获取传送门数据");
            return Quaternion.identity;
        }
        
        /// <summary>
        /// 播放Cast阶段VFX
        /// </summary>
        /// <param name="position">播放位置</param>
        /// <param name="vfxPrefab">VFX预制体</param>
        /// <param name="attackName">攻击名称</param>
        private void PlayCastVfx(Vector3 position, GameObject vfxPrefab, string attackName)
        {
            if (vfxPrefab)
            {
                // 获取传送门的旋转
                Quaternion portalRotation = GetCurrentPortalRotation();
                
                GameObject vfxInstance = Instantiate(vfxPrefab, position, portalRotation);
                
                // 5秒后销毁VFX实例
                Destroy(vfxInstance, 5f);
                
                Debug.Log($"[CastManager] 播放 {attackName} VFX，位置: {position}, 旋转: {portalRotation.eulerAngles}");
            }
            else
            {
                Debug.LogWarning($"[CastManager] {attackName} VFX预制体未设置");
            }
        }
        
        /// <summary>
        /// 播放Cast阶段Feel效果
        /// </summary>
        /// <param name="position">播放位置</param>
        /// <param name="feelPlayer">Feel播放器</param>
        /// <param name="attackName">攻击名称</param>
        private void PlayCastFeel(Vector3 position, MMF_Player feelPlayer, string attackName)
        {
            if (feelPlayer)
            {
                // 获取传送门的旋转
                Quaternion portalRotation = GetCurrentPortalRotation();
                
                // 设置Feel播放器位置和旋转并播放
                feelPlayer.transform.position = position;
                feelPlayer.transform.rotation = portalRotation;
                feelPlayer.PlayFeedbacks();
                
                Debug.Log($"[CastManager] 播放 {attackName} Feel效果，位置: {position}, 旋转: {portalRotation.eulerAngles}");
            }
            else
            {
                Debug.LogWarning($"[CastManager] {attackName} Feel播放器未设置");
            }
        }
        
        /// <summary>
        /// 激活BossPart攻击
        /// </summary>
        /// <param name="isHorizontalAttack">是否为水平攻击（左右触手）</param>
        private void ActivateBossPartAttack(bool isHorizontalAttack = false)
        {
            if (bossBlackboard && bossBlackboard.bossPartManager)
            {
                // 获取传送门的完整Transform信息
                var (portalPosition, portalRotation) = bossBlackboard.bossPartManager.GetLatestPortalTransform();
                
                // 如果是水平攻击（左右触手），调整高度为0.5
                Vector3 adjustedPosition = portalPosition;
                if (isHorizontalAttack)
                {
                    adjustedPosition.y = 0.5f;
                }
                
                // 移动BossPart到传送门位置和旋转
                bossBlackboard.bossPartManager.MoveToTransform(adjustedPosition, portalRotation);
                
                // 根据攻击类型播放动画
                if (isHorizontalAttack)
                {
                    // 左右触手：Y轴旋转动画
                    StartCoroutine(PlayHorizontalRotationAnimation());
                }
                else
                {
                    // 上下触手：弹跳动画
                    StartCoroutine(PlayVerticalBounceAnimation(adjustedPosition));
                }
                
                // 激活攻击
                bossBlackboard.bossPartManager.ActivatePartAttack();
                Debug.Log($"[CastManager] 移动BossPart到位置: {adjustedPosition}, 旋转: {portalRotation.eulerAngles}");
            }
            else
            {
                Debug.LogWarning("[CastManager] BossPartManager未找到");
            }
        }
        
        #endregion
        
        #region 动画协程
        
        /// <summary>
        /// 左右触手Y轴旋转动画
        /// </summary>
        private System.Collections.IEnumerator PlayHorizontalRotationAnimation()
        {
            if (!bossBlackboard?.bossPartManager?.bossPart)
                yield break;
                
            Transform bossPartTransform = bossBlackboard.bossPartManager.bossPart.transform;
            Quaternion baseRotation = bossPartTransform.rotation;
            
            // 判断是左触手还是右触手（通过攻击名称判断）
            bool isLeftAttack = _lastExecutedAttack.Contains("left");
            float rotationDirection = -1f; // 左右触手都使用逆时针方向
            
            // 第一阶段：瞬移到-60度位置
            Vector3 startEuler = baseRotation.eulerAngles;
            Vector3 minus60Euler = new Vector3(startEuler.x, startEuler.y + (-60f * rotationDirection), startEuler.z);
            Quaternion minus60Rotation = Quaternion.Euler(minus60Euler);
            bossPartTransform.rotation = minus60Rotation;
            Debug.Log($"[CastManager] 瞬移到-60度位置: {minus60Euler}");
            
            // 第二阶段：从-60度旋转到+60度
            Vector3 plus60Euler = new Vector3(startEuler.x, startEuler.y + (60f * rotationDirection), startEuler.z);
            Quaternion plus60Rotation = Quaternion.Euler(plus60Euler);
            
            float elapsedTime = 0f;
            
            while (elapsedTime < horizontalRotationDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / horizontalRotationDuration;
                
                // 使用EaseInOut曲线
                t = EaseInOut(t);
                
                bossPartTransform.rotation = Quaternion.Lerp(minus60Rotation, plus60Rotation, t);
                yield return null;
            }
            
            // 确保最终旋转准确
            bossPartTransform.rotation = plus60Rotation;
            Debug.Log($"[CastManager] 左右触手旋转动画完成，最终角度: {plus60Euler}");
        }
        
        /// <summary>
        /// 上下触手弹跳动画
        /// </summary>
        private System.Collections.IEnumerator PlayVerticalBounceAnimation(Vector3 startPosition)
        {
            if (!bossBlackboard?.bossPartManager?.bossPart)
                yield break;
                
            Transform bossPartTransform = bossBlackboard.bossPartManager.bossPart.transform;
            
            // 第一阶段：瞬移向上5
            Vector3 upwardPosition = startPosition + Vector3.up * upwardMoveDistance;
            bossPartTransform.position = upwardPosition;
            Debug.Log($"[CastManager] 瞬移向上到: {upwardPosition}");
            
            // 第二阶段：弹跳向下4
            Vector3 finalPosition = upwardPosition - Vector3.up * downwardBounceDistance;
            Vector3 currentPosition = upwardPosition;
            
            float elapsedTime = 0f;
            
            while (elapsedTime < verticalBounceDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / verticalBounceDuration;
                
                // 使用EaseInOut曲线
                t = EaseInOut(t);
                
                bossPartTransform.position = Vector3.Lerp(currentPosition, finalPosition, t);
                yield return null;
            }
            
            // 确保最终位置准确
            bossPartTransform.position = finalPosition;
            Debug.Log($"[CastManager] 上下触手弹跳动画完成，最终位置: {finalPosition}");
        }
        
        /// <summary>
        /// EaseInOut缓动函数
        /// </summary>
        /// <param name="t">时间参数 (0-1)</param>
        /// <returns>缓动后的参数</returns>
        private float EaseInOut(float t)
        {
            return t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t;
        }
        
        #endregion
        
        #region 调试方法
        
        [Button("测试上方触手攻击")]
        public void DebugTestTentacleUp()
        {
            ExecuteTentacleUpCast();
        }
        
        [Button("测试下方触手攻击")]
        public void DebugTestTentacleDown()
        {
            ExecuteTentacleDownCast();
        }
        
        [Button("测试左方触手攻击")]
        public void DebugTestTentacleLeft()
        {
            ExecuteTentacleLeftCast();
        }
        
        [Button("测试右方触手攻击")]
        public void DebugTestTentacleRight()
        {
            ExecuteTentacleRightCast();
        }
        
        [Button("显示当前状态")]
        public void DebugShowStatus()
        {
            Debug.Log($"[CastManager] 状态报告:");
            Debug.Log($"最后执行的攻击: {_lastExecutedAttack}");
            Debug.Log($"最后Cast位置: {_lastCastPosition}");
            Debug.Log($"PortalManager状态: {(_portalManager ? "已连接" : "未连接")}");
            Debug.Log($"BossBlackboard状态: {(bossBlackboard ? "已连接" : "未连接")}");
        }
        
        #endregion
    }
}
