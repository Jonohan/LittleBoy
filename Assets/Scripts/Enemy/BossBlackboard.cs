using UnityEngine;
using BehaviorDesigner.Runtime;
using Sirenix.OdinInspector;
using System.Linq;
using Xuwu.Character;

namespace Invector.vCharacterController.AI
{
    /// <summary>
    /// Boss黑板变量系统
    /// 管理Boss的所有状态变量，供行为树使用
    /// </summary>
    [System.Serializable]
    public class BossBlackboard : MonoBehaviour
    {
        [Header("血量与阶段")]
        [UnityEngine.Tooltip("Boss当前血量百分比 (0-1)")]
        public SharedFloat hpPct = new SharedFloat();
        
        [UnityEngine.Tooltip("当前阶段")]
        public SharedString phase = new SharedString();
        
        [Header("传送门管理")]
        [UnityEngine.Tooltip("当前场上传送门数量")]
        public SharedInt numPortals = new SharedInt();
        
        [UnityEngine.Tooltip("上一个生成的传送门类型")]
        public SharedString lastPortalType = new SharedString();
        
        [UnityEngine.Tooltip("上一个使用的传送门插槽名称")]
        public SharedString lastPortalSlot = new SharedString();
        
        [UnityEngine.Tooltip("上一个使用的技能名称")]
        public SharedString lastUsedSkill = new SharedString();
        
        [Header("技能冷却")]
        [UnityEngine.Tooltip("异能轰炸冷却时间")]
        public SharedFloat cooldown_bombard = new SharedFloat();
        
        [UnityEngine.Tooltip("异能洪水冷却时间")]
        public SharedFloat cooldown_flood = new SharedFloat();
        
        [UnityEngine.Tooltip("触手横扫冷却时间")]
        public SharedFloat cooldown_tentacle = new SharedFloat();
        
        [UnityEngine.Tooltip("漩涡发射冷却时间")]
        public SharedFloat cooldown_vortex = new SharedFloat();
        
        [UnityEngine.Tooltip("侧墙直线投掷冷却时间")]
        public SharedFloat cooldown_wallThrow = new SharedFloat();
        
        [UnityEngine.Tooltip("吼叫冷却时间")]
        public SharedFloat cooldown_roar = new SharedFloat();
        
        [Header("目标与状态")]
        [UnityEngine.Tooltip("玩家引用")]
        public SharedGameObject target = new SharedGameObject();
        
        [UnityEngine.Tooltip("愤怒态开关 (70%以下)")]
        public SharedBool angerOn = new SharedBool();
        
        [UnityEngine.Tooltip("恐惧态开关 (40%以下)")]
        public SharedBool fearOn = new SharedBool();
        
        [UnityEngine.Tooltip("失能态开关 (玩家4.5体型落地触发)")]
        public SharedBool disabledOn = new SharedBool();
        
        [Header("传送门插槽")]
        [UnityEngine.Tooltip("可放置传送门的预设插槽")]
        public Transform[] arenaSlots;
        
        [Header("Boss部件管理")]
        [UnityEngine.Tooltip("Boss部件管理器")]
        public BossPartManager bossPartManager;
        
        [Header("阶段阈值")]
        [UnityEngine.Tooltip("愤怒阶段血量阈值")]
        [Range(0f, 1f)]
        public float angerThreshold = 0.7f;
        
        [UnityEngine.Tooltip("恐惧阶段血量阈值")]
        [Range(0f, 1f)]
        public float fearThreshold = 0.4f;
        
        [Header("传送门限制")]
        [UnityEngine.Tooltip("最大传送门数量")]
        public int maxPortals = 2;
        
        [UnityEngine.Tooltip("传送门生成间隔")]
        public float portalSpawnInterval = 3f;
        
        [Header("调试信息")]
        [ShowInInspector, ReadOnly]
        private string _currentPhaseDisplay;
        
        [ShowInInspector, ReadOnly]
        private float _currentHpDisplay;
        
        [ShowInInspector, ReadOnly]
        private int _currentPortalCount;
        
        // 私有变量
        private NonHumanoidBossAI _bossAI;
        private CharacterSizeController _playerSizeController;
        
        #region Unity生命周期
        
        private void Awake()
        {
            _bossAI = GetComponent<NonHumanoidBossAI>();
            if (!_bossAI)
            {
                Debug.LogError($"[BossBlackboard] 未找到NonHumanoidBossAI组件在 {gameObject.name}");
                enabled = false;
                return;
            }
            
            // 查找玩家
            FindPlayer();
            
            // 查找Boss部件管理器
            FindBossPartManager();
        }
        
        private void Start()
        {
            InitializeBlackboard();
        }
        
        private void Update()
        {
            UpdateBlackboardValues();
            UpdateDebugDisplay();
        }
        
        #endregion
        
        #region 初始化
        
        /// <summary>
        /// 初始化黑板变量
        /// </summary>
        private void InitializeBlackboard()
        {
            // 初始化血量
            if (_bossAI)
            {
                hpPct.Value = _bossAI.currentHealth / _bossAI.maxHealth;
            }
            
            // 初始化阶段
            phase.Value = "P1_Normal";
            
            // 初始化传送门数量
            numPortals.Value = 0;
            lastPortalType.Value = "None";
            
            // 初始化所有冷却
            ResetAllCooldowns();
            
            // 初始化状态
            angerOn.Value = false;
            fearOn.Value = false;
            disabledOn.Value = false;
            
            Debug.Log("[BossBlackboard] 黑板变量初始化完成");
        }
        
        /// <summary>
        /// 查找玩家引用
        /// </summary>
        private void FindPlayer()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player)
            {
                target.Value = player;
                _playerSizeController = player.GetComponent<CharacterSizeController>();
                Debug.Log($"[BossBlackboard] 找到玩家: {player.name}");
            }
            else
            {
                Debug.LogWarning("[BossBlackboard] 未找到玩家对象");
            }
        }
        
        /// <summary>
        /// 查找Boss部件管理器
        /// </summary>
        private void FindBossPartManager()
        {
            if (!bossPartManager)
            {
                bossPartManager = GetComponent<BossPartManager>();
                if (!bossPartManager)
                {
                    bossPartManager = GetComponentInChildren<BossPartManager>();
                }
            }
            
            if (bossPartManager)
            {
                Debug.Log($"[BossBlackboard] 找到Boss部件管理器: {bossPartManager.name}");
            }
            else
            {
                Debug.LogWarning("[BossBlackboard] 未找到Boss部件管理器");
            }
        }
        
        #endregion
        
        #region 更新方法
        
        /// <summary>
        /// 更新黑板变量值
        /// </summary>
        private void UpdateBlackboardValues()
        {
            if (!_bossAI) return;
            
            // 更新血量百分比
            hpPct.Value = _bossAI.currentHealth / _bossAI.maxHealth;
            
            // 更新阶段状态
            UpdatePhaseStatus();
            
            // 更新失能状态
            UpdateDisabledStatus();
            
            // 更新冷却时间
            UpdateCooldowns();
        }
        
        /// <summary>
        /// 更新阶段状态
        /// </summary>
        private void UpdatePhaseStatus()
        {
            string newPhase = "P1_Normal";
            bool newAngerOn = false;
            bool newFearOn = false;
            
            if (hpPct.Value <= 0f)
            {
                newPhase = "Dead";
            }
            else if (hpPct.Value <= fearThreshold)
            {
                newPhase = "P3_Fear";
                newFearOn = true;
            }
            else if (hpPct.Value <= angerThreshold)
            {
                newPhase = "P2_Anger";
                newAngerOn = true;
            }
            
            // 更新阶段变量
            if (phase.Value != newPhase)
            {
                phase.Value = newPhase;
                Debug.Log($"[BossBlackboard] 阶段切换: {newPhase}");
            }
            
            angerOn.Value = newAngerOn;
            fearOn.Value = newFearOn;
        }
        
        /// <summary>
        /// 更新失能状态
        /// </summary>
        private void UpdateDisabledStatus()
        {
            if (!_playerSizeController) return;
            
            // 检查玩家是否达到4.5倍体型且落地
            float playerSize = _playerSizeController.GetCurrentSize();
            bool isGrounded = _playerSizeController.GetComponent<vThirdPersonMotor>()?.isGrounded ?? false;
            
            if (playerSize >= 4.5f && isGrounded && !disabledOn.Value)
            {
                disabledOn.Value = true;
                Debug.Log("[BossBlackboard] Boss进入失能状态 - 玩家巨大化落地");
            }
        }
        
        /// <summary>
        /// 更新冷却时间
        /// </summary>
        private void UpdateCooldowns()
        {
            float deltaTime = Time.deltaTime;
            
            if (cooldown_bombard.Value > 0f)
                cooldown_bombard.Value = Mathf.Max(0f, cooldown_bombard.Value - deltaTime);
            
            if (cooldown_flood.Value > 0f)
                cooldown_flood.Value = Mathf.Max(0f, cooldown_flood.Value - deltaTime);
            
            if (cooldown_tentacle.Value > 0f)
                cooldown_tentacle.Value = Mathf.Max(0f, cooldown_tentacle.Value - deltaTime);
            
            if (cooldown_vortex.Value > 0f)
                cooldown_vortex.Value = Mathf.Max(0f, cooldown_vortex.Value - deltaTime);
            
            if (cooldown_wallThrow.Value > 0f)
                cooldown_wallThrow.Value = Mathf.Max(0f, cooldown_wallThrow.Value - deltaTime);
            
            if (cooldown_roar.Value > 0f)
                cooldown_roar.Value = Mathf.Max(0f, cooldown_roar.Value - deltaTime);
        }
        
        /// <summary>
        /// 更新调试显示
        /// </summary>
        private void UpdateDebugDisplay()
        {
            _currentPhaseDisplay = phase.Value;
            _currentHpDisplay = hpPct.Value;
            _currentPortalCount = numPortals.Value;
        }
        
        #endregion
        
        #region 公共方法
        
        /// <summary>
        /// 重置所有冷却时间
        /// </summary>
        [Button("重置所有冷却")]
        public void ResetAllCooldowns()
        {
            cooldown_bombard.Value = 0f;
            cooldown_flood.Value = 0f;
            cooldown_tentacle.Value = 0f;
            cooldown_vortex.Value = 0f;
            cooldown_wallThrow.Value = 0f;
            cooldown_roar.Value = 0f;
        }
        
        /// <summary>
        /// 设置技能冷却
        /// </summary>
        /// <param name="skillName">技能名称</param>
        /// <param name="cooldownTime">冷却时间</param>
        public void SetCooldown(string skillName, float cooldownTime)
        {
            switch (skillName.ToLower())
            {
                case "bombard":
                    cooldown_bombard.Value = cooldownTime;
                    break;
                case "flood":
                    cooldown_flood.Value = cooldownTime;
                    break;
                case "tentacle":
                case "tentacle_up":
                case "tentacle_down":
                case "tentacle_left":
                case "tentacle_right":
                    cooldown_tentacle.Value = cooldownTime;
                    break;
                case "vortex":
                    cooldown_vortex.Value = cooldownTime;
                    break;
                case "wallthrow":
                case "wallthrow_left":
                case "wallthrow_right":
                    cooldown_wallThrow.Value = cooldownTime;
                    break;
                case "roar":
                    cooldown_roar.Value = cooldownTime;
                    break;
                default:
                    Debug.LogWarning($"[BossBlackboard] 未知技能名称: {skillName}");
                    break;
            }
        }
        
        /// <summary>
        /// 检查技能是否可用
        /// </summary>
        /// <param name="skillName">技能名称</param>
        /// <returns>是否可用</returns>
        public bool IsSkillAvailable(string skillName)
        {
            switch (skillName.ToLower())
            {
                case "bombard":
                    return cooldown_bombard.Value <= 0f;
                case "flood":
                    return cooldown_flood.Value <= 0f;
                case "tentacle":
                case "tentacle_up":
                case "tentacle_down":
                case "tentacle_left":
                case "tentacle_right":
                    return cooldown_tentacle.Value <= 0f;
                case "vortex":
                    return cooldown_vortex.Value <= 0f;
                case "wallthrow":
                case "wallthrow_left":
                case "wallthrow_right":
                    return cooldown_wallThrow.Value <= 0f;
                case "roar":
                    return cooldown_roar.Value <= 0f;
                default:
                    Debug.LogWarning($"[BossBlackboard] 未知技能名称: {skillName}");
                    return false;
            }
        }
        
        /// <summary>
        /// 更新传送门数量
        /// </summary>
        /// <param name="count">新的数量</param>
        public void UpdatePortalCount(int count)
        {
            numPortals.Value = Mathf.Clamp(count, 0, maxPortals);
        }
        
        /// <summary>
        /// 设置最后传送门类型
        /// </summary>
        /// <param name="portalType">传送门类型</param>
        public void SetLastPortalType(string portalType)
        {
            lastPortalType.Value = portalType;
        }
        
        /// <summary>
        /// 设置最后使用的传送门插槽
        /// </summary>
        /// <param name="slotName">插槽名称</param>
        public void SetLastPortalSlot(string slotName)
        {
            lastPortalSlot.Value = slotName;
        }
        
        /// <summary>
        /// 获取最后使用的传送门插槽
        /// </summary>
        /// <returns>插槽名称</returns>
        public string GetLastPortalSlot()
        {
            return lastPortalSlot.Value;
        }
        
        /// <summary>
        /// 根据技能名称获取可用的插槽类型
        /// </summary>
        /// <param name="skillName">技能名称</param>
        /// <returns>可用的插槽类型数组</returns>
        public PortalType[] GetAvailableSlotTypesForSkill(string skillName)
        {
            switch (skillName)
            {
                case "tentacle_up":
                    return new PortalType[] { PortalType.Ceiling };
                case "tentacle_down":
                    return new PortalType[] { PortalType.Ground };
                case "tentacle_left":
                    return new PortalType[] { PortalType.WallLeft };
                case "tentacle_right":
                    return new PortalType[] { PortalType.WallRight };
                case "wallthrow_left":
                    return new PortalType[] { PortalType.WallLeft };
                case "wallthrow_right":
                    return new PortalType[] { PortalType.WallRight };
                case "bombard":
                    return new PortalType[] { PortalType.Ceiling };
                case "flood":
                    return new PortalType[] { PortalType.Ground };
                case "vortex":
                    return new PortalType[] { PortalType.Ceiling, PortalType.Ground };
                case "roar":
                    return new PortalType[] { }; // 吼叫不需要传送门
                default:
                    return new PortalType[] { };
            }
        }
        
        /// <summary>
        /// 检查技能是否可以使用指定插槽（避免与上次技能重复）
        /// </summary>
        /// <param name="skillName">技能名称</param>
        /// <param name="slotName">插槽名称</param>
        /// <returns>是否可以使用</returns>
        public bool CanUseSlotForSkill(string skillName, string slotName)
        {
            // 如果插槽名称为空，表示没有上次使用的插槽，可以使用
            if (string.IsNullOrEmpty(lastPortalSlot.Value))
                return true;
                
            // 如果插槽名称与上次不同，可以使用
            if (slotName != lastPortalSlot.Value)
                return true;
                
            // 如果插槽名称相同，检查是否是不同类型的技能
            var lastSkillTypes = GetAvailableSlotTypesForSkill(GetLastUsedSkill());
            var currentSkillTypes = GetAvailableSlotTypesForSkill(skillName);
            
            // 如果技能类型完全不同，可以使用
            return !lastSkillTypes.Any(type => currentSkillTypes.Contains(type));
        }
        
        /// <summary>
        /// 获取上次使用的技能名称
        /// </summary>
        /// <returns>技能名称</returns>
        public string GetLastUsedSkill()
        {
            return lastUsedSkill.Value;
        }
        
        /// <summary>
        /// 设置上次使用的技能名称
        /// </summary>
        /// <param name="skillName">技能名称</param>
        public void SetLastUsedSkill(string skillName)
        {
            lastUsedSkill.Value = skillName;
        }
        
        /// <summary>
        /// 获取可用的传送门插槽
        /// </summary>
        /// <param name="preferredType">首选类型</param>
        /// <returns>可用插槽</returns>
        public Transform GetAvailablePortalSlot(string preferredType = "")
        {
            if (arenaSlots == null || arenaSlots.Length == 0)
            {
                Debug.LogWarning("[BossBlackboard] 没有配置传送门插槽");
                return null;
            }
            
            // 优先选择指定类型的插槽
            if (!string.IsNullOrEmpty(preferredType))
            {
                foreach (var slot in arenaSlots)
                {
                    if (slot && slot.name.Contains(preferredType))
                    {
                        return slot;
                    }
                }
            }
            
            // 随机选择一个可用插槽
            var availableSlots = new System.Collections.Generic.List<Transform>();
            foreach (var slot in arenaSlots)
            {
                if (slot != null)
                {
                    availableSlots.Add(slot);
                }
            }
            
            if (availableSlots.Count > 0)
            {
                return availableSlots[Random.Range(0, availableSlots.Count)];
            }
            
            return null;
        }
        
        #endregion
        
        #region Boss部件控制方法
        
        /// <summary>
        /// 激活Boss部件
        /// </summary>
        public void ActivateBossPart()
        {
            if (bossPartManager)
            {
                bossPartManager.ActivatePart();
            }
        }
        
        /// <summary>
        /// 非激活Boss部件
        /// </summary>
        public void DeactivateBossPart()
        {
            if (bossPartManager)
            {
                bossPartManager.DeactivatePart();
            }
        }
        
        /// <summary>
        /// 激活Boss部件攻击
        /// </summary>
        public void ActivateBossPartAttack()
        {
            if (bossPartManager)
            {
                bossPartManager.ActivatePartAttack();
            }
        }
        
        /// <summary>
        /// 关闭Boss部件攻击
        /// </summary>
        public void DeactivateBossPartAttack()
        {
            if (bossPartManager)
            {
                bossPartManager.DeactivatePartAttack();
            }
        }
        
        /// <summary>
        /// 移动到传送门位置并攻击
        /// </summary>
        public void MoveBossPartToPortalAndAttack()
        {
            if (bossPartManager)
            {
                bossPartManager.MoveToPortalAndAttack();
            }
        }
        
        /// <summary>
        /// 获取Boss部件是否激活
        /// </summary>
        /// <returns>是否激活</returns>
        public bool IsBossPartActive()
        {
            if (bossPartManager && bossPartManager.bossPart)
            {
                return bossPartManager.bossPart.IsActive();
            }
            return false;
        }
        
        /// <summary>
        /// 获取Boss部件是否正在攻击
        /// </summary>
        /// <returns>是否正在攻击</returns>
        public bool IsBossPartAttacking()
        {
            if (bossPartManager && bossPartManager.bossPart)
            {
                return bossPartManager.bossPart.IsAttackActive();
            }
            return false;
        }
        
        #endregion
        
        #region 调试方法
        
        [Button("强制进入愤怒状态")]
        public void ForceAngerState()
        {
            hpPct.Value = angerThreshold - 0.1f;
            Debug.Log("[BossBlackboard] 强制进入愤怒状态");
        }
        
        [Button("强制进入恐惧状态")]
        public void ForceFearState()
        {
            hpPct.Value = fearThreshold - 0.1f;
            Debug.Log("[BossBlackboard] 强制进入恐惧状态");
        }
        
        [Button("强制失能状态")]
        public void ForceDisabledState()
        {
            disabledOn.Value = true;
            Debug.Log("[BossBlackboard] 强制进入失能状态");
        }
        
        [Button("重置到正常状态")]
        public void ResetToNormalState()
        {
            hpPct.Value = 1.0f;
            disabledOn.Value = false;
            Debug.Log("[BossBlackboard] 重置到正常状态");
        }
        
        #endregion
    }
}
