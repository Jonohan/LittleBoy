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
        
        [Header("其他攻击配置")]
        [Tooltip("轰炸攻击VFX（场景内多个对象）")]
        public GameObject[] bombardVfxObjects;
        
        [Tooltip("洪水攻击VFX")]
        public GameObject floodVfx;
        
        [Tooltip("左墙投掷攻击VFX")]
        public GameObject wallThrowLeftVfx;
        
        [Tooltip("右墙投掷攻击VFX")]
        public GameObject wallThrowRightVfx;
        
        [Header("Feel效果配置")]
        [Tooltip("上方触手攻击Feel效果")]
        public MMF_Player tentacleUpFeel;
        
        [Tooltip("下方触手攻击Feel效果")]
        public MMF_Player tentacleDownFeel;
        
        [Tooltip("左方触手攻击Feel效果")]
        public MMF_Player tentacleLeftFeel;
        
        [Tooltip("右方触手攻击Feel效果")]
        public MMF_Player tentacleRightFeel;
        
        [Tooltip("轰炸攻击Feel效果")]
        public MMF_Player bombardFeel;
        
        [Tooltip("洪水攻击Feel效果")]
        public MMF_Player floodFeel;
        
        [Tooltip("左墙投掷攻击Feel效果")]
        public MMF_Player wallThrowLeftFeel;
        
        [Tooltip("右墙投掷攻击Feel效果")]
        public MMF_Player wallThrowRightFeel;
        
        [Header("伤害对象配置")]
        [Tooltip("轰炸伤害对象")]
        public GameObject bombardDamageObject;
        
        [Tooltip("洪水伤害对象")]
        public GameObject floodDamageObject;
        
        [Tooltip("洪水水面对象")]
        public GameObject floodWaterSurfaceObject;
        
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
        
        // 轰炸VFX无需实例化，使用场景对象数组启停
        private bool _isBombardCasting = false;
        private bool _isFloodCasting = false;
        
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
            
            // 订阅玩家体型变化事件
            if (bossBlackboard)
            {
                bossBlackboard.OnPlayerSizeLevelChanged += OnPlayerSizeLevelChanged;
            }
            
            _initialized = true;
            
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
        /// 执行轰炸攻击Cast阶段
        /// </summary>
        public void ExecuteBombardCast()
        {
            // 激活场景中的轰炸VFX（不改变位置与旋转）
            ActivateBombardVfx();
            
            // 触发Feel
            var castPosition = GetCurrentPortalPosition();
            PlayCastFeel(castPosition, bombardFeel, "bombard");
            
            // 伤害对象启停（按体型规则）
            ActivateDamageObject(bombardDamageObject, true);
            
            _isBombardCasting = true;
            _lastExecutedAttack = "bombard";
            _lastCastPosition = castPosition;
            
        }
        
        /// <summary>
        /// 执行洪水攻击Cast阶段
        /// </summary>
        public void ExecuteFloodCast()
        {
            ExecuteOtherAttackCast("flood", floodVfx, floodFeel, floodDamageObject, false);
            
            // 处理洪水水面对象
            if (floodWaterSurfaceObject)
            {
                StartCoroutine(HandleFloodWaterSurface());
            }
            _isFloodCasting = true;
        }
        
        /// <summary>
        /// 执行左墙投掷攻击Cast阶段
        /// </summary>
        public void ExecuteWallThrowLeftCast()
        {
            ExecuteOtherAttackCast("wallthrow_left", wallThrowLeftVfx, wallThrowLeftFeel, null, false);
            _lastExecutedAttack = "wallthrow_left";
            _lastCastPosition = GetCurrentPortalPosition();
        }
        
        /// <summary>
        /// 执行右墙投掷攻击Cast阶段
        /// </summary>
        public void ExecuteWallThrowRightCast()
        {
            ExecuteOtherAttackCast("wallthrow_right", wallThrowRightVfx, wallThrowRightFeel, null, false);
            _lastExecutedAttack = "wallthrow_right";
            _lastCastPosition = GetCurrentPortalPosition();
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
            
            
        }
        
        // 删除专用实例化式的轰炸执行逻辑（改为启停场景对象）
        
        /// <summary>
        /// 通用其他攻击Cast阶段执行
        /// </summary>
        /// <param name="attackName">攻击名称</param>
        /// <param name="vfxPrefab">VFX预制体</param>
        /// <param name="feelPlayer">Feel播放器</param>
        /// <param name="damageObject">伤害对象</param>
        /// <param name="isBombard">是否为轰炸攻击</param>
        private void ExecuteOtherAttackCast(string attackName, GameObject vfxPrefab, MMF_Player feelPlayer, GameObject damageObject, bool isBombard)
        {
            if (!_initialized)
            {
                Debug.LogWarning("[CastManager] 管理器未初始化");
                return;
            }
            
            // 获取传送门位置
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
            
            // 激活伤害对象（根据玩家体型条件）
            ActivateDamageObject(damageObject, isBombard);
            
            // 更新调试信息
            _lastExecutedAttack = attackName;
            _lastCastPosition = castPosition;
            
            
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
        /// 批量激活轰炸VFX（场景对象，不改变位置与旋转）
        /// </summary>
        private void ActivateBombardVfx()
        {
            if (bombardVfxObjects == null || bombardVfxObjects.Length == 0) return;
            for (int i = 0; i < bombardVfxObjects.Length; i++)
            {
                var go = bombardVfxObjects[i];
                if (go) go.SetActive(true);
            }
            
        }

        /// <summary>
        /// 批量关闭轰炸VFX（场景对象）
        /// </summary>
        private void DeactivateBombardVfx()
        {
            if (bombardVfxObjects == null || bombardVfxObjects.Length == 0) return;
            for (int i = 0; i < bombardVfxObjects.Length; i++)
            {
                var go = bombardVfxObjects[i];
                if (go) go.SetActive(false);
            }
            
        }

        /// <summary>
        /// 玩家体型等级变化回调
        /// </summary>
        /// <param name="newLevel">新的体型等级</param>
        private void OnPlayerSizeLevelChanged(int newLevel)
        {
            if (!_initialized) return;
            if (_isBombardCasting)
            {
                ActivateDamageObjectWithLevel(bombardDamageObject, true, newLevel);
            }
            if (_isFloodCasting)
            {
                ActivateDamageObjectWithLevel(floodDamageObject, false, newLevel);
            }
        }

        private void OnDestroy()
        {
            if (bossBlackboard)
            {
                bossBlackboard.OnPlayerSizeLevelChanged -= OnPlayerSizeLevelChanged;
            }
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
                
                // 计算VFX旋转：让VFX的Y轴对齐传送门的Z轴
                Vector3 portalZDirection = portalRotation * Vector3.forward; // 传送门的Z轴方向
                Quaternion vfxRotation = Quaternion.FromToRotation(Vector3.up, portalZDirection);
                
                GameObject vfxInstance = Instantiate(vfxPrefab, position, vfxRotation);
                
                // 5秒后销毁VFX实例
                Destroy(vfxInstance, 5f);
                
                
            }
            else
            {
                Debug.LogWarning($"[CastManager] {attackName} VFX预制体未设置");
            }
        }
        
        // 删除实例化式的轰炸VFX播放，改为启停场景对象
        
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
                
                // 计算Feel旋转：让Feel的Y轴对齐传送门的Z轴
                Vector3 portalZDirection = portalRotation * Vector3.forward; // 传送门的Z轴方向
                Quaternion feelRotation = Quaternion.FromToRotation(Vector3.up, portalZDirection);
                
                // 设置Feel播放器位置和旋转并播放
                feelPlayer.transform.position = position;
                feelPlayer.transform.rotation = feelRotation;
                feelPlayer.PlayFeedbacks();
                
                
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
                
            }
            else
            {
                Debug.LogWarning("[CastManager] BossPartManager未找到");
            }
        }
        
        /// <summary>
        /// 激活伤害对象（根据玩家体型条件）
        /// </summary>
        /// <param name="damageObject">伤害对象</param>
        /// <param name="isBombard">是否为轰炸攻击</param>
        private void ActivateDamageObject(GameObject damageObject, bool isBombard)
        {
            if (!damageObject)
            {
                Debug.LogWarning("[CastManager] 伤害对象未设置");
                return;
            }
            
            // 获取玩家体型等级
            int playerSizeLevel = GetPlayerSizeLevel();
            
            // 根据攻击类型和玩家体型决定是否激活
            bool shouldActivate = true;
            if (isBombard)
            {
                // 轰炸：玩家体型为1级时禁用
                shouldActivate = (playerSizeLevel != 1);
            }
            else
            {
                // 洪水：玩家体型>=3级时禁用
                shouldActivate = (playerSizeLevel < 3);
            }
            
            if (shouldActivate)
            {
                damageObject.SetActive(true);
                
            }
            else
            {
                damageObject.SetActive(false);
                
            }
        }

        /// <summary>
        /// 根据给定体型等级执行启停（供事件回调使用）
        /// </summary>
        private void ActivateDamageObjectWithLevel(GameObject damageObject, bool isBombard, int playerSizeLevel)
        {
            if (!damageObject) return;
            bool shouldActivate = isBombard ? (playerSizeLevel != 1) : (playerSizeLevel < 3);
            damageObject.SetActive(shouldActivate);
        }
        
        /// <summary>
        /// 获取玩家体型等级
        /// </summary>
        /// <returns>玩家体型等级</returns>
        private int GetPlayerSizeLevel()
        {
            // 通过BossBlackboard获取玩家体型等级
            if (bossBlackboard)
            {
                return bossBlackboard.GetPlayerSizeLevel();
            }
            
            // 备用方案：返回默认等级
            Debug.LogWarning("[CastManager] 无法获取玩家体型等级，使用默认值");
            return 2; // 默认中等体型
        }
        
        /// <summary>
        /// 处理洪水水面对象动画
        /// </summary>
        private System.Collections.IEnumerator HandleFloodWaterSurface()
        {
            if (!floodWaterSurfaceObject) yield break;
            
            // 激活水面对象
            floodWaterSurfaceObject.SetActive(true);
            
            // 获取初始位置
            Vector3 initialPosition = floodWaterSurfaceObject.transform.position;
            Vector3 targetPosition = new Vector3(initialPosition.x, 1.5f, initialPosition.z);
            
            float elapsedTime = 0f;
            float riseDuration = 1f; // 1秒上升时间
            
            // 水面上升动画
            while (elapsedTime < riseDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / riseDuration;
                
                // 使用平滑插值
                floodWaterSurfaceObject.transform.position = Vector3.Lerp(initialPosition, targetPosition, t);
                
                yield return null;
            }
            
            // 确保最终位置准确
            floodWaterSurfaceObject.transform.position = targetPosition;
            
        }
        
        /// <summary>
        /// 重置洪水水面对象
        /// </summary>
        public void ResetFloodWaterSurface()
        {
            if (floodWaterSurfaceObject)
            {
                // 重置到初始位置 (0,0,0)
                floodWaterSurfaceObject.transform.position = Vector3.zero;
                floodWaterSurfaceObject.SetActive(false);
                
            }
        }
        
        /// <summary>
        /// 平滑将洪水水面对象移回原点并禁用
        /// </summary>
        public void SmoothResetFloodWaterSurface()
        {
            if (!floodWaterSurfaceObject) return;
            StartCoroutine(SmoothResetFloodWaterSurfaceCoroutine());
        }
        
        private System.Collections.IEnumerator SmoothResetFloodWaterSurfaceCoroutine()
        {
            Vector3 start = floodWaterSurfaceObject.transform.position;
            Vector3 target = Vector3.zero;
            float duration = 1f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = t / duration;
                floodWaterSurfaceObject.transform.position = Vector3.Lerp(start, target, k);
                yield return null;
            }
            floodWaterSurfaceObject.transform.position = target;
            floodWaterSurfaceObject.SetActive(false);
            
        }
        
        /// <summary>
        /// 重置所有伤害对象
        /// </summary>
        public void ResetAllDamageObjects()
        {
            // 关闭轰炸VFX（场景对象数组）
            DeactivateBombardVfx();
            
            // 重置轰炸伤害对象
            if (bombardDamageObject)
            {
                bombardDamageObject.SetActive(false);
                
            }
            
            // 重置洪水伤害对象
            if (floodDamageObject)
            {
                floodDamageObject.SetActive(false);
                
            }
            _isBombardCasting = false;
            _isFloodCasting = false;
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
            
        }
        
        #endregion
    }
}
