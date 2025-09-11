using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace Invector.vCharacterController.AI
{
    /// <summary>
    /// Boss部件管理器
    /// 管理唯一的Boss部件，提供移动和攻击控制接口
    /// </summary>
    public class BossPartManager : MonoBehaviour
    {
        [Header("Boss部件管理")]
        [Tooltip("唯一的Boss部件")]
        public BossPart bossPart;
        
        [Tooltip("是否自动查找BossPart")]
        public bool autoFindPart = true;
        
        [Tooltip("BossPart的标签（用于查找）")]
        public string bossPartTag = "BossPart";
        
        [Header("移动配置")]
        [Tooltip("是否瞬间移动到传送门位置")]
        public bool useInstantMovement = true;
        
        [Header("传送门引用")]
        [Tooltip("传送门管理器引用")]
        public PortalManager portalManager;
        
        [Header("批量控制")]
        [Tooltip("默认激活状态")]
        public bool defaultActiveState = true;
        
        [Header("调试信息")]
        [ShowInInspector, ReadOnly]
        private bool _isPartActive;
        
        [ShowInInspector, ReadOnly]
        private bool _isPartAttacking;
        
        [ShowInInspector, ReadOnly]
        private Vector3 _currentPosition;
        
        [ShowInInspector, ReadOnly]
        private Vector3 _targetPosition;
        
        // 私有变量
        private BossBlackboard _bossBlackboard;
        private bool _initialized = false;
        
        #region Unity生命周期
        
        private void Awake()
        {
            InitializeManager();
        }
        
        private void Start()
        {
            SetupBossParts();
        }
        
        private void Update()
        {
            UpdateDebugInfo();
        }
        
        #endregion
        
        #region 初始化
        
        /// <summary>
        /// 初始化管理器
        /// </summary>
        private void InitializeManager()
        {
            _bossBlackboard = GetComponent<BossBlackboard>();
            if (!_bossBlackboard)
            {
                Debug.LogWarning($"[BossPartManager] 未找到BossBlackboard组件在 {gameObject.name}");
            }
            
            // 查找传送门管理器
            if (!portalManager)
            {
                portalManager = GetComponent<PortalManager>();
                if (!portalManager)
                {
                    portalManager = FindObjectOfType<PortalManager>();
                }
            }
        }
        
        /// <summary>
        /// 设置Boss部件
        /// </summary>
        private void SetupBossParts()
        {
            if (autoFindPart && !bossPart)
            {
                FindBossPart();
            }
            
            // 设置部件的初始状态
            if (bossPart)
            {
                bossPart.SetActive(defaultActiveState);
            }
            
            _initialized = true;
            Debug.Log($"[BossPartManager] 初始化完成，管理部件: {(bossPart ? bossPart.name : "无")}");
        }
        
        /// <summary>
        /// 查找Boss部件
        /// </summary>
        private void FindBossPart()
        {
            // 通过标签查找
            if (!string.IsNullOrEmpty(bossPartTag))
            {
                var taggedObject = GameObject.FindGameObjectWithTag(bossPartTag);
                if (taggedObject)
                {
                    bossPart = taggedObject.GetComponent<BossPart>();
                }
            }
            
            // 如果没找到，查找场景中所有的BossPart组件
            if (!bossPart)
            {
                var allParts = FindObjectsOfType<BossPart>();
                if (allParts.Length > 0)
                {
                    bossPart = allParts[0]; // 使用第一个找到的部件
                }
            }
            
            // 设置BossBlackboard引用
            if (bossPart && !bossPart.bossBlackboard && _bossBlackboard)
            {
                bossPart.bossBlackboard = _bossBlackboard;
            }
            
            if (bossPart)
            {
                Debug.Log($"[BossPartManager] 找到Boss部件: {bossPart.name}");
            }
            else
            {
                Debug.LogWarning("[BossPartManager] 未找到Boss部件");
            }
        }
        
        #endregion
        
        #region 部件控制
        
        /// <summary>
        /// 设置部件激活状态
        /// </summary>
        /// <param name="active">是否激活</param>
        public void SetPartActive(bool active)
        {
            if (bossPart)
            {
                bossPart.SetActive(active);
                Debug.Log($"[BossPartManager] 设置部件状态为: {(active ? "激活" : "非激活")}");
            }
        }
        
        /// <summary>
        /// 激活部件
        /// </summary>
        public void ActivatePart()
        {
            SetPartActive(true);
        }
        
        /// <summary>
        /// 非激活部件
        /// </summary>
        public void DeactivatePart()
        {
            SetPartActive(false);
        }
        
        #endregion
        
        #region 移动和攻击控制
        
        /// <summary>
        /// 移动到最新传送门位置并攻击
        /// </summary>
        public void MoveToPortalAndAttack()
        {
            if (!bossPart || !portalManager)
            {
                Debug.LogWarning("[BossPartManager] BossPart或PortalManager未设置");
                return;
            }
            
            // 获取最新传送门位置
            var portalPosition = GetLatestPortalPosition();
            if (portalPosition == Vector3.zero)
            {
                Debug.LogWarning("[BossPartManager] 未找到有效的传送门位置");
                return;
            }
            
            // 移动到传送门位置
            MoveToPosition(portalPosition);
            
            // 激活攻击
            ActivatePartAttack();
            
            Debug.Log($"[BossPartManager] 移动到传送门位置并开始攻击: {portalPosition}");
        }
        
        /// <summary>
        /// 获取最新传送门位置
        /// </summary>
        /// <returns>传送门位置</returns>
        public Vector3 GetLatestPortalPosition()
        {
            if (!portalManager)
                return Vector3.zero;
            
            // 获取最新的传送门位置
            var latestPortal = portalManager.GetLatestPortal();
            if (latestPortal != null && latestPortal.portalObject != null)
            {
                return latestPortal.portalObject.transform.position;
            }
            
            return Vector3.zero;
        }
        
        /// <summary>
        /// 移动到指定位置
        /// </summary>
        /// <param name="targetPosition">目标位置</param>
        public void MoveToPosition(Vector3 targetPosition)
        {
            if (!bossPart)
                return;
            
            _targetPosition = targetPosition;
            
            if (useInstantMovement)
            {
                // 瞬间移动
                bossPart.transform.position = targetPosition;
                Debug.Log($"[BossPartManager] 瞬间移动BossPart到位置: {targetPosition}");
            }
            else
            {
                // 平滑移动（保留选项）
                StartCoroutine(SmoothMoveToPosition(targetPosition));
            }
        }
        
        /// <summary>
        /// 平滑移动到目标位置（备用选项）
        /// </summary>
        /// <param name="targetPosition">目标位置</param>
        private System.Collections.IEnumerator SmoothMoveToPosition(Vector3 targetPosition)
        {
            Vector3 startPosition = bossPart.transform.position;
            float moveTime = 1f; // 固定移动时间
            float elapsedTime = 0f;
            
            while (elapsedTime < moveTime)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / moveTime;
                
                // 使用平滑插值
                bossPart.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
                
                yield return null;
            }
            
            // 确保最终位置准确
            bossPart.transform.position = targetPosition;
        }
        
        /// <summary>
        /// 激活部件攻击
        /// </summary>
        public void ActivatePartAttack()
        {
            if (bossPart)
            {
                bossPart.ActivateAttack();
                Debug.Log("[BossPartManager] 激活部件攻击");
            }
        }
        
        /// <summary>
        /// 关闭部件攻击
        /// </summary>
        public void DeactivatePartAttack()
        {
            if (bossPart)
            {
                bossPart.DeactivateAttack();
                Debug.Log("[BossPartManager] 关闭部件攻击");
            }
        }
        
        
        #endregion
        
        #region 攻击控制
        
        #endregion
        
        #region 伤害设置
        
        /// <summary>
        /// 设置部件的攻击伤害
        /// </summary>
        /// <param name="damage">伤害值</param>
        public void SetPartAttackDamage(float damage)
        {
            if (bossPart != null)
            {
                bossPart.SetAttackDamage(damage);
                Debug.Log($"[BossPartManager] 设置部件攻击伤害为: {damage}");
            }
            else
            {
                Debug.LogWarning($"[BossPartManager] 未找到部件");
            }
        }
        
        #endregion
        
        #region 状态查询
        
        /// <summary>
        /// 检查部件是否处于激活状态
        /// </summary>
        /// <returns>部件是否激活</returns>
        public bool IsPartActive()
        {
            return bossPart != null && bossPart.IsActive();
        }
        
        /// <summary>
        /// 检查部件是否正在攻击
        /// </summary>
        /// <returns>部件是否正在攻击</returns>
        public bool IsPartAttacking()
        {
            return bossPart != null && bossPart.IsAttackActive();
        }
        
        #endregion
        
        #region 调试信息更新
        
        /// <summary>
        /// 更新调试信息
        /// </summary>
        private void UpdateDebugInfo()
        {
            if (!_initialized) return;
            
            if (bossPart)
            {
                _isPartActive = bossPart.IsActive();
                _isPartAttacking = bossPart.IsAttackActive();
                _currentPosition = bossPart.transform.position;
            }
        }
        
        #endregion
        
        #region 调试方法
        
        [Button("激活部件")]
        public void DebugActivatePart()
        {
            ActivatePart();
        }
        
        [Button("非激活部件")]
        public void DebugDeactivatePart()
        {
            DeactivatePart();
        }
        
        [Button("激活攻击")]
        public void DebugActivateAttack()
        {
            ActivatePartAttack();
        }
        
        [Button("关闭攻击")]
        public void DebugDeactivateAttack()
        {
            DeactivatePartAttack();
        }
        
        [Button("移动到传送门并攻击")]
        public void DebugMoveToPortalAndAttack()
        {
            MoveToPortalAndAttack();
        }
        
        [Button("重新查找部件")]
        public void DebugRefindPart()
        {
            FindBossPart();
        }
        
        [Button("显示部件状态")]
        public void DebugShowPartStatus()
        {
            if (bossPart)
            {
                Debug.Log($"[BossPartManager] 部件状态报告:");
                Debug.Log($"部件名称: {bossPart.partName}");
                Debug.Log($"激活状态: {_isPartActive}");
                Debug.Log($"攻击状态: {_isPartAttacking}");
                Debug.Log($"当前位置: {_currentPosition}");
                Debug.Log($"目标位置: {_targetPosition}");
            }
            else
            {
                Debug.Log("[BossPartManager] 未找到Boss部件");
            }
        }
        
        #endregion
    }
}

