using UnityEngine;
using BehaviorDesigner.Runtime;
using Sirenix.OdinInspector;
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
        [Tooltip("Boss当前血量百分比 (0-1)")]
        public SharedFloat hpPct = new SharedFloat();
        
        [Tooltip("当前阶段")]
        public SharedString phase = new SharedString();
        
        [Header("传送门管理")]
        [Tooltip("当前场上传送门数量")]
        public SharedInt numPortals = new SharedInt();
        
        [Tooltip("上一个生成的传送门类型")]
        public SharedString lastPortalType = new SharedString();
        
        [Header("技能冷却")]
        [Tooltip("异能轰炸冷却时间")]
        public SharedFloat cooldown_bombard = new SharedFloat();
        
        [Tooltip("异能洪水冷却时间")]
        public SharedFloat cooldown_flood = new SharedFloat();
        
        [Tooltip("触手横扫冷却时间")]
        public SharedFloat cooldown_tentacle = new SharedFloat();
        
        [Tooltip("漩涡发射冷却时间")]
        public SharedFloat cooldown_vortex = new SharedFloat();
        
        [Tooltip("侧墙直线投掷冷却时间")]
        public SharedFloat cooldown_wallThrow = new SharedFloat();
        
        [Tooltip("吼叫冷却时间")]
        public SharedFloat cooldown_roar = new SharedFloat();
        
        [Header("目标与状态")]
        [Tooltip("玩家引用")]
        public SharedGameObject target = new SharedGameObject();
        
        [Tooltip("愤怒态开关 (70%以下)")]
        public SharedBool angerOn = new SharedBool();
        
        [Tooltip("恐惧态开关 (40%以下)")]
        public SharedBool fearOn = new SharedBool();
        
        [Tooltip("失能态开关 (玩家4.5体型落地触发)")]
        public SharedBool disabledOn = new SharedBool();
        
        [Header("传送门插槽")]
        [Tooltip("可放置传送门的预设插槽")]
        public Transform[] arenaSlots;
        
        [Header("阶段阈值")]
        [Tooltip("愤怒阶段血量阈值")]
        [Range(0f, 1f)]
        public float angerThreshold = 0.7f;
        
        [Tooltip("恐惧阶段血量阈值")]
        [Range(0f, 1f)]
        public float fearThreshold = 0.4f;
        
        [Header("传送门限制")]
        [Tooltip("最大传送门数量")]
        public int maxPortals = 2;
        
        [Tooltip("传送门生成间隔")]
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
                    cooldown_tentacle.Value = cooldownTime;
                    break;
                case "vortex":
                    cooldown_vortex.Value = cooldownTime;
                    break;
                case "wallthrow":
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
                    return cooldown_tentacle.Value <= 0f;
                case "vortex":
                    return cooldown_vortex.Value <= 0f;
                case "wallthrow":
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
