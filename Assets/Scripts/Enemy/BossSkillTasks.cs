using UnityEngine;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using Sirenix.OdinInspector;
using System.Linq;
using System.Collections.Generic;
using Invector;

namespace Invector.vCharacterController.AI
{
    #region 技能任务基类
    
    /// <summary>
    /// Boss技能任务基类
    /// 提供通用的技能执行流程：生成门 → 前摇 → 施放 → 后摇 → 清理
    /// </summary>
    public abstract class BossSkillTask : Action
    {
        [Header("传送门配置")]
        [UnityEngine.Tooltip("自定义传送门颜色（可选，不设置则使用默认逻辑）")]
        public PortalColor customPortalColor = PortalColor.Blue;
        
        [UnityEngine.Tooltip("是否使用自定义传送门颜色")]
        public bool useCustomPortalColor = false;
        
        // 内部参数，不需要在Inspector中设置
        protected string skillName = "Unknown";
        protected float cooldownTime = 5f;
        protected float spawnPortalTime = 5f;
        protected float telegraphTime = 2f;
        protected float castTime = 6f; // Cast阶段持续时间
        protected float postAttackTime = 1f;
        protected PortalType portalType = PortalType.Ceiling;
        protected PortalColor portalColor = PortalColor.Blue;
        protected bool useDynamicPortalColor = true;
        
        // 标记是否由SmartSkill调用（用于插槽选择逻辑）
        public bool isCalledBySmartSkill = false;
        
        // 组件引用，自动配置
        protected SharedGameObject portalManager = new SharedGameObject();
        protected SharedGameObject bossBlackboard = new SharedGameObject();
        protected SharedGameObject bossAI = new SharedGameObject();
        
        // 私有变量
        protected PortalManager _portalManager;
        protected BossBlackboard _bossBlackboard;
        protected NonHumanoidBossAI _bossAI;
        protected PortalData _currentPortal;
        protected float _skillStartTime;
        protected SkillPhase _currentPhase;
        protected bool _hasExecutedSkill = false;
        
        // BossPart初始transform
        protected Vector3 _initialBossPartPosition;
        protected Quaternion _initialBossPartRotation;
        protected bool _hasStoredInitialTransform = false;
        
        protected enum SkillPhase
        {
            None,
            SpawnPortal,
            Telegraph,
            Cast,
            PostAttack,
            Cleanup
        }
        
        public override void OnStart()
        {
            InitializeComponents();
            
            // 设置传送门颜色
            if (useCustomPortalColor)
            {
                portalColor = customPortalColor;
            }
            else if (useDynamicPortalColor)
            {
                SelectPortalColorByBossPhase();
            }
            
            // 保存BossPart的初始transform（只在第一次时保存）
            if (!_hasStoredInitialTransform && _bossBlackboard && _bossBlackboard.bossPartManager && _bossBlackboard.bossPartManager.bossPart)
            {
                _initialBossPartPosition = _bossBlackboard.bossPartManager.bossPart.transform.position;
                _initialBossPartRotation = _bossBlackboard.bossPartManager.bossPart.transform.rotation;
                _hasStoredInitialTransform = true;
                Debug.Log($"[BossSkillTask] 已保存BossPart初始transform: 位置{_initialBossPartPosition}, 旋转{_initialBossPartRotation.eulerAngles}");
            }
            
            _skillStartTime = Time.time;
            _currentPhase = SkillPhase.SpawnPortal;
            _hasExecutedSkill = false;
            
        }
        
        public override TaskStatus OnUpdate()
        {
            switch (_currentPhase)
            {
                case SkillPhase.SpawnPortal:
                    return HandleSpawnPortal();
                case SkillPhase.Telegraph:
                    return HandleTelegraph();
                case SkillPhase.Cast:
                    return HandleCast();
                case SkillPhase.PostAttack:
                    return HandlePostAttack();
                case SkillPhase.Cleanup:
                    return HandleCleanup();
                default:
                    return TaskStatus.Failure;
            }
        }
        
        public override void OnEnd()
        {
            if (_currentPhase != SkillPhase.Cleanup)
            {
                HandleCleanup();
            }
        }
        
        #region 技能阶段处理
        
        /// <summary>
        /// 处理传送门生成阶段
        /// </summary>
        protected virtual TaskStatus HandleSpawnPortal()
        {
            
            // 检查技能是否需要传送门
            if (_bossBlackboard)
            {
                var availableTypes = _bossBlackboard.GetAvailableSlotTypesForSkill(skillName);
                if (availableTypes.Length == 0)
                {
                    _currentPhase = SkillPhase.Telegraph;
                    return TaskStatus.Running;
                }
            }
            
            if (!_portalManager)
            {
                Debug.LogError($"[{skillName}] 传送门管理器未找到");
                return TaskStatus.Failure;
            }
            
            // 如果还没有生成传送门，先生成传送门
            if (_currentPortal == null)
            {
                // 根据技能名称选择可用的插槽
                PortalSlot selectedSlot = SelectSlotForSkill();
                if (selectedSlot == null)
                {
                    Debug.LogError($"[{skillName}] 没有可用的插槽生成传送门");
                    return TaskStatus.Failure;
                }
                
                // 在选定的插槽上生成传送门
                _currentPortal = _portalManager.GeneratePortal(portalType, portalColor, selectedSlot);
            if (_currentPortal == null)
            {
                Debug.LogError($"[{skillName}] 传送门生成失败");
                return TaskStatus.Failure;
            }
            
            // 更新黑板变量
            if (_bossBlackboard)
            {
                _bossBlackboard.UpdatePortalCount(_portalManager.GetActivePortalCount());
                _bossBlackboard.SetLastPortalType(portalType.ToString());
                    _bossBlackboard.SetLastPortalSlot(selectedSlot.name); // 记录最后使用的插槽
                    _bossBlackboard.SetLastUsedSkill(skillName); // 记录最后使用的技能
            }
            
            }
            
            // 等待传送门生成时间（给传送门一些生成动画时间）
            if (Time.time - _skillStartTime >= spawnPortalTime)
            {
            _currentPhase = SkillPhase.Telegraph;
            }
            
            return TaskStatus.Running;
        }
        
        /// <summary>
        /// 处理前摇阶段
        /// </summary>
        protected virtual TaskStatus HandleTelegraph()
        {
            // 开始传送门前摇阶段（自动选择已完成生成阶段的传送门）
            if (_currentPortal?.portalSlot != null)
            {
                int selectedPortal = _portalManager.StartPortalTelegraphing(telegraphTime);
                if (selectedPortal == 0)
                {
                }
            }
            
            // 播放前摇动画和特效
            PlayTelegraphEffects();
            
            // 检查前摇时间
            if (Time.time - _skillStartTime >= telegraphTime)
            {
                _currentPhase = SkillPhase.Cast;
            }
            
            return TaskStatus.Running;
        }
        
        /// <summary>
        /// 处理施放阶段
        /// </summary>
        protected virtual TaskStatus HandleCast()
        {
            // 执行技能效果（只在第一次进入时执行）
            if (!_hasExecutedSkill)
            {
                ExecuteSkillEffect();
                _hasExecutedSkill = true;
            }
            
            // 检查Cast阶段时间
            if (Time.time - _skillStartTime >= telegraphTime + castTime)
            {
                _currentPhase = SkillPhase.PostAttack;
            }
            
            return TaskStatus.Running;
        }
        
        /// <summary>
        /// 处理后摇阶段
        /// </summary>
        protected virtual TaskStatus HandlePostAttack()
        {
            // 播放后摇动画
            PlayPostAttackEffects();
            
            // 检查后摇时间
            if (Time.time - _skillStartTime >= telegraphTime + castTime + postAttackTime)
            {
                _currentPhase = SkillPhase.Cleanup;
            }
            
            return TaskStatus.Running;
        }
        
        /// <summary>
        /// 重置BossPart到初始位置
        /// </summary>
        protected virtual void ResetBossPartToInitialPosition()
        {
            if (_bossBlackboard && _bossBlackboard.bossPartManager && _bossBlackboard.bossPartManager.bossPart)
            {
                // 重置到游戏开始时的transform
                _bossBlackboard.bossPartManager.bossPart.transform.position = _initialBossPartPosition;
                _bossBlackboard.bossPartManager.bossPart.transform.rotation = _initialBossPartRotation;
                
                // 停用攻击
                _bossBlackboard.bossPartManager.DeactivatePart();
                
                Debug.Log($"[BossSkillTask] BossPart已重置到初始transform: 位置{_initialBossPartPosition}, 旋转{_initialBossPartRotation.eulerAngles}");
            }
            else
            {
                Debug.LogWarning("[BossSkillTask] 无法重置BossPart，组件未找到");
            }
        }
        
        /// <summary>
        /// 处理清理阶段
        /// </summary>
        protected virtual TaskStatus HandleCleanup()
        {
            // 设置技能冷却
            if (_bossBlackboard)
            {
                _bossBlackboard.SetCooldown(skillName.ToLower(), cooldownTime);
            }
            
            // 通用清理：确保被打断或正常结束时资源全部回收、状态复原
            try
            {
                // 1) 仅在 Spawn 阶段被中止时回收VFX并复原插槽数据
                //    其余阶段视为"传送门已成功创建且流程完成"，不复位传送门
                if (_currentPhase == SkillPhase.SpawnPortal)
                {
                    if (_currentPortal != null && _currentPortal.portalSlot != null)
                    {
                        _currentPortal.portalSlot.ResetSlot();
                    }
                }
                
                // 2) 重置BossPart到初始位置
                ResetBossPartToInitialPosition();
                
                // 3) 关闭可能仍在激活的Boss部件与攻击（与传送门是否复位无关）
                if (_bossBlackboard && _bossBlackboard.bossPartManager)
                {
                    _bossBlackboard.bossPartManager.DeactivatePartAttack();
                    _bossBlackboard.bossPartManager.DeactivatePart();
                }
            }
            finally
            {
                // 4) 复位本地状态机标志
                _currentPortal = null;
                _hasExecutedSkill = false;
                _currentPhase = SkillPhase.None;
            }
            
            return TaskStatus.Success;
        }
        
        #endregion
        
        #region 抽象方法
        
        /// <summary>
        /// 播放前摇特效
        /// </summary>
        protected abstract void PlayTelegraphEffects();
        
        /// <summary>
        /// 执行技能效果
        /// </summary>
        protected abstract void ExecuteSkillEffect();
        
        /// <summary>
        /// 播放后摇特效
        /// </summary>
        protected abstract void PlayPostAttackEffects();
        
        // 传送门不需要关闭，它们一直存在
        
        #endregion
        
        #region 辅助方法
        
        /// <summary>
        /// 从外部设置自定义传送门颜色（会禁用动态颜色选择）
        /// </summary>
        /// <param name="color">要使用的颜色</param>
        public void SetCustomPortalColor(PortalColor color)
        {
            customPortalColor = color;
            useCustomPortalColor = true;
            useDynamicPortalColor = false;
        }

        /// <summary>
        /// 初始化组件引用
        /// </summary>
        private void InitializeComponents()
        {
            // 自动配置所有组件引用（避免重复配置）
            AutoConfigureComponents();
        }
        
        /// <summary>
        /// 自动配置所有组件引用
        /// </summary>
        private void AutoConfigureComponents()
        {
            if (!Owner) return;
            
            // 自动配置BossBlackboard
            if (!_bossBlackboard)
            {
                _bossBlackboard = Owner.GetComponent<BossBlackboard>();
            }
            
            // 自动配置PortalManager
            if (!_portalManager)
            {
                _portalManager = UnityEngine.Object.FindObjectOfType<PortalManager>();
            }
            
            // 自动配置BossAI
            if (!_bossAI)
            {
                _bossAI = Owner.GetComponent<NonHumanoidBossAI>();
            }
            
            // 自动配置SharedGameObject引用（避免在Inspector中手动配置）
            if (portalManager.Value == null && _portalManager)
            {
                portalManager.Value = _portalManager.gameObject;
            }
            
            if (bossBlackboard.Value == null && _bossBlackboard)
            {
                bossBlackboard.Value = _bossBlackboard.gameObject;
            }
            
            if (bossAI.Value == null && _bossAI)
            {
                bossAI.Value = _bossAI.gameObject;
            }
        }
        
        /// <summary>
        /// 根据Boss阶段动态选择传送门颜色
        /// </summary>
        private void SelectPortalColorByBossPhase()
        {
            if (!_bossBlackboard) return;
            
            // 检查是否处于恐惧阶段
            if (_bossBlackboard.fearOn.Value)
            {
                // 恐惧阶段：使用巨大传送门
                portalColor = PortalColor.GiantOrange;
            }
            else
            {
                // 其他阶段：从Blue和Orange随机选择
                PortalColor[] normalColors = { PortalColor.Blue, PortalColor.Orange };
                portalColor = normalColors[Random.Range(0, normalColors.Length)];
            }
        }
        
        /// <summary>
        /// 根据技能名称选择可用的插槽，避免与上次技能重复
        /// </summary>
        /// <returns>选中的插槽，如果没有可用插槽则返回null</returns>
        private PortalSlot SelectSlotForSkill()
        {
            if (!_portalManager || !_bossBlackboard) 
            {
                Debug.LogError($"[{skillName}] PortalManager或BossBlackboard为空 - PortalManager: {(_portalManager != null ? "✓" : "✗")}, BossBlackboard: {(_bossBlackboard != null ? "✓" : "✗")}");
                return null;
            }
            
            // 获取当前技能可用的插槽类型
            var availableTypes = _bossBlackboard.GetAvailableSlotTypesForSkill(skillName);
            if (availableTypes.Length == 0)
            {
                return null;
            }
            
            // 收集所有可用的插槽
            var allAvailableSlots = new List<PortalSlot>();
            foreach (var type in availableTypes)
            {
                var slots = _portalManager.GetSlotsByType(type);
                if (slots != null)
                {
                    allAvailableSlots.AddRange(slots);
                }
            }
            
            
            if (allAvailableSlots.Count == 0)
            {
                Debug.LogError($"[{skillName}] 没有找到可用的插槽");
                return null;
            }
            
            // 根据调用方式决定插槽选择逻辑
            List<PortalSlot> validSlots;
            
            if (isCalledBySmartSkill)
            {
                // SmartSkill调用：保留避免重复的逻辑
                validSlots = allAvailableSlots.Where(slot => 
                    _bossBlackboard.CanUseSlotForSkill(skillName, slot.name)).ToList();
                
                // 如果没有其他插槽可选，则使用所有插槽
                if (validSlots.Count == 0)
                {
                    validSlots = allAvailableSlots;
                    Debug.LogWarning($"[{skillName}] SmartSkill: 没有可用插槽，使用所有插槽");
                }
            }
            else
            {
                // 独立Task调用：允许重复使用插槽
                validSlots = allAvailableSlots;
                Debug.Log($"[{skillName}] 独立Task: 允许重复使用插槽，使用所有插槽");
            }
            
            Debug.Log($"[{skillName}] 可用插槽数量: {validSlots.Count}, 总插槽数量: {allAvailableSlots.Count}");
            
            // 随机选择一个插槽
            int randomIndex = Random.Range(0, validSlots.Count);
            PortalSlot selectedSlot = validSlots[randomIndex];
            
            Debug.Log($"[{skillName}] 选择插槽: {selectedSlot.name}, 上次使用插槽: {_bossBlackboard.GetLastPortalSlot()}, 上次使用技能: {_bossBlackboard.GetLastUsedSkill()}");
            
            return selectedSlot;
        }
        
        #endregion
    }
    
    #endregion
    
    #region 具体技能实现
    
    /// <summary>
    /// 天花板异能轰炸技能
    /// </summary>
    [TaskDescription("天花板异能轰炸 - 全场散点雨攻击")]
    [TaskIcon("{SkinColor}ActionIcon.png")]
    public class CeilingEnergyBombard : BossSkillTask
    {
        [Header("轰炸配置")]
        [UnityEngine.Tooltip("投掷物预制体")]
        public GameObject projectilePrefab;
        
        [UnityEngine.Tooltip("投掷物数量")]
        public int projectileCount = 20;
        
        [UnityEngine.Tooltip("轰炸范围")]
        public float bombardRadius = 10f;
        
        [UnityEngine.Tooltip("投掷物速度")]
        public float projectileSpeed = 15f;
        
        [UnityEngine.Tooltip("伤害值")]
        public float damage = 50f;
        
        public override void OnStart()
        {
            // 自动设置技能参数
            skillName = "bombard";
            portalType = PortalType.Ceiling;
            cooldownTime = 8f;
            spawnPortalTime = 5f;
            telegraphTime = 3f;
            postAttackTime = 2f;
            
            base.OnStart();
        }
        
        protected override void PlayTelegraphEffects()
        {
        }
        
        protected override void ExecuteSkillEffect()
        {
        }
        
        protected override void PlayPostAttackEffects()
        {
        }
        
        // 传送门不需要关闭，它们一直存在
    }
    
    /// <summary>
    /// 侧墙直线投掷技能
    /// </summary>
    [TaskDescription("侧墙直线投掷 - 高速直线攻击")]
    [TaskIcon("{SkinColor}ActionIcon.png")]
    public class WallLineThrow : BossSkillTask
    {
        [Header("投掷配置")]
        [UnityEngine.Tooltip("投掷物预制体")]
        public GameObject projectilePrefab;
        
        [UnityEngine.Tooltip("投掷物速度")]
        public float projectileSpeed = 25f;
        
        [UnityEngine.Tooltip("伤害值")]
        public float damage = 40f;
        
        [UnityEngine.Tooltip("投掷方向")]
        public Vector3 throwDirection = Vector3.forward;
        
        public override void OnStart()
        {
            // 自动设置技能参数
            skillName = "wallthrow";
            portalType = PortalType.WallLeft; // 默认左墙
            cooldownTime = 6f;
            spawnPortalTime = 5f;
            telegraphTime = 2f;
            postAttackTime = 1f;
            
            base.OnStart();
        }
        
        protected override void PlayTelegraphEffects()
        {
        }
        
        protected override void ExecuteSkillEffect()
        {
        }
        
        protected override void PlayPostAttackEffects()
        {
        }
        
        // 传送门不需要关闭，它们一直存在
    }
    
    /// <summary>
    /// 触手横扫技能
    /// </summary>
    [TaskDescription("触手横扫 - 物理扇形攻击")]
    [TaskIcon("{SkinColor}ActionIcon.png")]
    public class TentacleSwipe : BossSkillTask
    {
        [Header("横扫配置")]
        [UnityEngine.Tooltip("横扫角度")]
        public float swipeAngle = 120f;
        
        [UnityEngine.Tooltip("横扫半径")]
        public float swipeRadius = 8f;
        
        [UnityEngine.Tooltip("伤害值")]
        public float damage = 60f;
        
        [UnityEngine.Tooltip("击退力度")]
        public float knockbackForce = 10f;
        
        public override void OnStart()
        {
            // 自动设置技能参数
            skillName = "tentacle";
            portalType = PortalType.Ground; // 触手从地面伸出
            cooldownTime = 7f;
            spawnPortalTime = 5f;
            telegraphTime = 2.5f;
            postAttackTime = 1.5f;
            
            base.OnStart();
        }
        
        protected override void PlayTelegraphEffects()
        {
        }
        
        protected override void ExecuteSkillEffect()
        {
        }
        
        protected override void PlayPostAttackEffects()
        {
        }
        
        // 传送门不需要关闭，它们一直存在
    }
    
    /// <summary>
    /// 地面洪水技能
    /// </summary>
    [TaskDescription("地面洪水 - 水位上升攻击")]
    [TaskIcon("{SkinColor}ActionIcon.png")]
    public class GroundFlood : BossSkillTask
    {
        [Header("洪水配置")]
        [UnityEngine.Tooltip("洪水预制体")]
        public GameObject floodPrefab;
        
        [UnityEngine.Tooltip("水位上升速度")]
        public float waterRiseSpeed = 2f;
        
        [UnityEngine.Tooltip("最大水位高度")]
        public float maxWaterHeight = 5f;
        
        [UnityEngine.Tooltip("洪水持续时间")]
        public float floodDuration = 8f;
        
        [UnityEngine.Tooltip("伤害值")]
        public float damage = 30f;
        
        private GameObject _floodObject;
        private float _floodStartTime;
        
        public override void OnStart()
        {
            // 自动设置技能参数
            skillName = "flood";
            portalType = PortalType.Ground;
            cooldownTime = 10f;
            spawnPortalTime = 5f;
            telegraphTime = 3f;
            postAttackTime = 1f;
            
            base.OnStart();
        }
        
        protected override void PlayTelegraphEffects()
        {
        }
        
        protected override void ExecuteSkillEffect()
        {
        }
        
        protected override void PlayPostAttackEffects()
        {
        }
    }
    
    /// <summary>
    /// 漩涡发射技能
    /// </summary>
    [TaskDescription("漩涡发射 - 地面吸入到天花板抛出")]
    [TaskIcon("{SkinColor}ActionIcon.png")]
    public class VortexLaunch : BossSkillTask
    {
        [Header("漩涡配置")]
        [UnityEngine.Tooltip("漩涡预制体")]
        public GameObject vortexPrefab;
        
        [UnityEngine.Tooltip("吸入力度")]
        public float suckForce = 15f;
        
        [UnityEngine.Tooltip("吸入范围")]
        public float suckRadius = 6f;
        
        [UnityEngine.Tooltip("抛出力度")]
        public float launchForce = 20f;
        
        [UnityEngine.Tooltip("伤害值")]
        public float damage = 45f;
        
        private GameObject _vortexObject;
        private PortalData _ceilingPortal;
        
        public override void OnStart()
        {
            // 自动设置技能参数
            skillName = "vortex";
            portalType = PortalType.Ground;
            cooldownTime = 12f;
            spawnPortalTime = 5f;
            telegraphTime = 2f;
            postAttackTime = 3f;
            
            base.OnStart();
        }
        
        protected override void PlayTelegraphEffects()
        {
        }
        
        protected override void ExecuteSkillEffect()
        {
        }
        
        protected override void PlayPostAttackEffects()
        {
        }
        
        // 传送门不需要关闭，它们一直存在
    }
    
    /// <summary>
    /// 吼叫技能
    /// </summary>
    [TaskDescription("Boss吼叫 - 嘲讽和喘息")]
    [TaskIcon("{SkinColor}ActionIcon.png")]
    public class BossRoar : BossSkillTask
    {
        [Header("吼叫配置")]
        [UnityEngine.Tooltip("吼叫音效")]
        public AudioClip roarSound;
        
        [UnityEngine.Tooltip("吼叫动画触发参数")]
        public string roarTrigger = "Roar";
        
        [UnityEngine.Tooltip("击退力度")]
        public float knockbackForce = 5f;
        
        [UnityEngine.Tooltip("击退范围")]
        public float knockbackRadius = 8f;
        
        public override void OnStart()
        {
            // 自动设置技能参数
            skillName = "roar";
            portalType = PortalType.None; // 吼叫不需要传送门
            cooldownTime = 15f;
            spawnPortalTime = 5f;
            telegraphTime = 1f;
            postAttackTime = 2f;
            
            base.OnStart();
        }
        
        protected override void PlayTelegraphEffects()
        {
        }
        
        protected override void ExecuteSkillEffect()
        {
        }
        
        protected override void PlayPostAttackEffects()
        {
        }
        
        // 传送门不需要关闭，它们一直存在
    }
    
    #endregion
    
    #region 智能技能选择
    
    /// <summary>
    /// 技能颜色配置类
    /// </summary>
    [System.Serializable]
    public class SkillColorConfig
    {
        [UnityEngine.Tooltip("技能名称")]
        public string skillName;
        
        [UnityEngine.Tooltip("首选颜色")]
        public PortalColor preferredColor;
        
        [UnityEngine.Tooltip("是否在Blue和Orange之间随机选择")]
        public bool useRandomColor;
        
        // 无参构造用于序列化/反序列化
        public SkillColorConfig() {}

        public SkillColorConfig(string name, PortalColor color, bool random)
        {
            skillName = name;
            preferredColor = color;
            useRandomColor = random;
        }
    }
        
        /// <summary>
    /// 智能技能选择任务
    /// 根据当前状态和冷却时间选择可用的技能
        /// </summary>
    [TaskDescription("智能技能选择 - 根据状态和冷却选择可用技能")]
    [TaskIcon("{SkinColor}ActionIcon.png")]
    public class BossSmartSkillSelection : Action
    {
        [Header("组件引用")]
        [UnityEngine.Tooltip("Boss黑板引用")]
        public SharedGameObject bossBlackboard;
        
        [Header("技能配置")]
        [UnityEngine.Tooltip("可用技能列表（不含触发型技能）")]
        public string[] availableSkills = {
            "tentacle_up", "tentacle_down", "tentacle_left", "tentacle_right",
            "wallthrow_left", "wallthrow_right",
            "roar"
        };
        
        [UnityEngine.Tooltip("技能权重（影响选择概率，对应 availableSkills 顺序，不含触发型技能）")]
        public float[] skillWeights = {
            1f, 1f, 1f, 1f,  // tentacle技能
            1f, 1f,          // wallthrow技能
            0f               // roar
        };

        // 扁平化触发配置：避免嵌套数组在 BT Inspector 中显示异常
        [System.Serializable]
        public class TriggerProbConfig
        {
            [Range(0f,1f)] public float bombard; // 下一次触发"bombard"的绝对概率
            [Range(0f,1f)] public float flood;   // 下一次触发"flood"的绝对概率
            [Range(0f,1f)] public float vortex;  // 下一次触发"vortex"的绝对概率
        }

        [Header("触发型技能概率（根据上一次技能，在下一次中按绝对概率触发）")]
        public TriggerProbConfig tentacle_upTrigger = new TriggerProbConfig { flood = 0.5f };
        public TriggerProbConfig tentacle_downTrigger = new TriggerProbConfig { bombard = 0.5f };
        public TriggerProbConfig tentacle_leftTrigger = new TriggerProbConfig();
        public TriggerProbConfig tentacle_rightTrigger = new TriggerProbConfig();
        public TriggerProbConfig wallthrow_leftTrigger = new TriggerProbConfig();
        public TriggerProbConfig wallthrow_rightTrigger = new TriggerProbConfig();
        public TriggerProbConfig roarTrigger = new TriggerProbConfig();
        
        [Header("技能颜色配置")]
        [UnityEngine.Tooltip("技能特定颜色配置")]
        public SkillColorConfig[] skillColorConfigs = {
            new SkillColorConfig("tentacle_up", PortalColor.Blue, true), //在这之后flood有1/2的概率发生，1/2是vortex
            new SkillColorConfig("tentacle_down", PortalColor.Blue, false), // 只有在这之后bombard才有1/2的发生概率会发生
            new SkillColorConfig("tentacle_left", PortalColor.Blue, true), // 随机选择
            new SkillColorConfig("tentacle_right", PortalColor.Orange, true), // 随机选择
            new SkillColorConfig("wallthrow_left", PortalColor.Blue, true),
            new SkillColorConfig("wallthrow_right", PortalColor.Orange, true),
            new SkillColorConfig("bombard", PortalColor.Blue, false),
            new SkillColorConfig("flood", PortalColor.Orange, false),
            new SkillColorConfig("vortex", PortalColor.Blue, true), // 随机选择
            new SkillColorConfig("roar", PortalColor.Blue, false) // roar不需要传送门，但保留配置
        };
        
        [Header("输出")]
        [UnityEngine.Tooltip("选中的技能名称")]
        public SharedString selectedSkill;
        
        private BossBlackboard _bossBlackboard;
        
        public override void OnStart()
        {
            // 自动配置组件引用
            AutoConfigureComponents();
        }
        
        /// <summary>
        /// 自动配置所有组件引用
        /// </summary>
        private void AutoConfigureComponents()
        {
            if (!Owner) return;
            
            // 自动配置BossBlackboard
            if (!_bossBlackboard)
            {
                _bossBlackboard = Owner.GetComponent<BossBlackboard>();
            }
            
            // 自动配置SharedGameObject引用
            if (bossBlackboard.Value == null && _bossBlackboard)
            {
                bossBlackboard.Value = _bossBlackboard.gameObject;
            }
        }
        
        private BossSkillTask _currentSkillTask;
        private bool _isExecutingSkill = false;
        
        public override TaskStatus OnUpdate()
        {
            // 如果还没有选择技能，先选择技能
            if (_currentSkillTask == null && !_isExecutingSkill)
            {
                Debug.Log("------");
                
                if (!_bossBlackboard)
                {
                    Debug.LogError("[BossSmartSkillSelection] BossBlackboard未找到");
                    LogBTFailure("BossBlackboard is null");
                    return TaskStatus.Failure;
                }
                
                // 获取可用的技能
                var validSkills = GetAvailableSkills();
               
                if (validSkills.Count == 0)
                {
                    LogBTFailure("No available skills after cooldown/slot checks");
                    return TaskStatus.Failure;
                }
                
                // 先尝试触发型技能（基于上一次技能，在下一次选择中按概率触发）
                string selectedSkillName = TrySelectTriggeredSkill();
                
                // 如果没有触发，则按权重在可用技能中选择（已不包含触发型技能）
                if (string.IsNullOrEmpty(selectedSkillName))
                {
                    selectedSkillName = SelectSkillByWeight(validSkills);
                }
                selectedSkill.Value = selectedSkillName;
                
                
                // 直接创建并开始执行技能
                _currentSkillTask = CreateSkillTask(selectedSkillName);
                if (_currentSkillTask == null)
                {
                    Debug.LogError($"[BossSmartSkillSelection] 未找到技能类: {selectedSkillName}");
                    LogBTFailure($"CreateSkillTask returned null for {selectedSkillName}");
                    return TaskStatus.Failure;
                }
                
                _currentSkillTask.OnStart();
                _isExecutingSkill = true;
            }
            
            // 执行当前技能Task
            if (_currentSkillTask != null)
            {
                TaskStatus status = _currentSkillTask.OnUpdate();
                
                // 如果技能执行完成，清理并返回成功
                if (status == TaskStatus.Success)
                {
                    _currentSkillTask.OnEnd();
                    _currentSkillTask = null;
                    _isExecutingSkill = false;
                    return TaskStatus.Success;
                }
                
                return status;
            }
            
            return TaskStatus.Failure;
        }

        // 打印行为树失败原因与黑板快照，辅助定位“状态切换导致失败”的问题
        private void LogBTFailure(string reason)
        {
            if (_bossBlackboard)
            {
                Debug.LogWarning(
                    $"[BossSmartSkillSelection] Failure: {reason} | phase={_bossBlackboard.phase.Value}, hpPct={_bossBlackboard.hpPct.Value:F2}, angerOn={_bossBlackboard.angerOn.Value}, fearOn={_bossBlackboard.fearOn.Value}, lastSkill={_bossBlackboard.GetLastUsedSkill()}, portals={_bossBlackboard.numPortals.Value}"
                );
            }
            else
            {
                Debug.LogWarning($"[BossSmartSkillSelection] Failure: {reason} | bossBlackboard=null");
            }
        }
        
        public override void OnEnd()
        {
            if (_currentSkillTask != null)
            {
                _currentSkillTask.OnEnd();
                _currentSkillTask = null;
            }
            _isExecutingSkill = false;
    }
    
    /// <summary>
        /// 获取当前可用的技能列表
    /// </summary>
        /// <returns>可用技能列表</returns>
        private List<string> GetAvailableSkills()
        {
            var available = new List<string>();
            var lastUsedSkill = _bossBlackboard.GetLastUsedSkill();
            var lastUsedSlot = _bossBlackboard.GetLastPortalSlot();
            

            for (int i = 0; i < availableSkills.Length; i++)
            {
                string skillName = availableSkills[i];
                
                // 检查技能冷却
                if (IsSkillOnCooldown(skillName))
                {
                    continue;
                }
                    
                // 检查技能是否可以使用（避免与上次技能重复）
                if (CanUseSkill(skillName))
                {
                    available.Add(skillName);
                }

            }
            
            return available;
    }

        /// <summary>
        /// 根据上一次技能，尝试选择触发型技能（只在下一次选择中评估）
        /// </summary>
        /// <returns>若触发则返回技能名，否则返回空字符串</returns>
        private string TrySelectTriggeredSkill()
        {
            if (_bossBlackboard == null) return string.Empty;
            string last = _bossBlackboard.GetLastUsedSkill();
            if (string.IsNullOrEmpty(last)) return string.Empty;

            TriggerProbConfig cfg = GetTriggerConfig(last);
            if (cfg == null) return string.Empty;

            var candidates = new List<(string skill, float p)>();
            if (cfg.bombard > 0f) candidates.Add(("bombard", Mathf.Clamp01(cfg.bombard)));
            if (cfg.flood > 0f) candidates.Add(("flood", Mathf.Clamp01(cfg.flood)));
            if (cfg.vortex > 0f) candidates.Add(("vortex", Mathf.Clamp01(cfg.vortex)));
            if (candidates.Count == 0) return string.Empty;

            var valid = new List<(string skill, float p)>();
            foreach (var c in candidates)
            {
                if (IsSkillOnCooldown(c.skill)) continue;
                if (!CanUseSkill(c.skill)) continue;
                valid.Add(c);
            }
            if (valid.Count == 0) return string.Empty;

            float total = 0f; foreach (var v in valid) total += v.p;
            float roll = Random.value;
            if (total > 1f)
            {
                float accN = 0f;
                foreach (var v in valid)
                {
                    accN += v.p / total;
                    if (roll <= accN) return v.skill;
                }
                return string.Empty;
            }
            else
            {
                float acc = 0f;
                foreach (var v in valid)
                {
                    acc += v.p;
                    if (roll <= acc) return v.skill;
                }
                return string.Empty;
            }
        }

        private TriggerProbConfig GetTriggerConfig(string lastSkill)
        {
            switch (lastSkill)
            {
                case "tentacle_up": return tentacle_upTrigger;
                case "tentacle_down": return tentacle_downTrigger;
                case "tentacle_left": return tentacle_leftTrigger;
                case "tentacle_right": return tentacle_rightTrigger;
                case "wallthrow_left": return wallthrow_leftTrigger;
                case "wallthrow_right": return wallthrow_rightTrigger;
                case "roar": return roarTrigger;
                default: return null;
            }
        }
    
    /// <summary>
        /// 检查技能是否在冷却中
    /// </summary>
        /// <param name="skillName">技能名称</param>
        /// <returns>是否在冷却中</returns>
        private bool IsSkillOnCooldown(string skillName)
        {
            switch (skillName)
            {
                case "tentacle_up":
                case "tentacle_down":
                case "tentacle_left":
                case "tentacle_right":
                    return _bossBlackboard.cooldown_tentacle.Value > 0;
                case "wallthrow_left":
                case "wallthrow_right":
                    return _bossBlackboard.cooldown_wallThrow.Value > 0;
                case "bombard":
                    return _bossBlackboard.cooldown_bombard.Value > 0;
                case "flood":
                    return _bossBlackboard.cooldown_flood.Value > 0;
                case "vortex":
                    return _bossBlackboard.cooldown_vortex.Value > 0;
                case "roar":
                    return _bossBlackboard.cooldown_roar.Value > 0;
                default:
                    return false;
            }
        }
        
        /// <summary>
        /// 检查技能是否可以使用（避免与上次技能重复）
        /// </summary>
        /// <param name="skillName">技能名称</param>
        /// <returns>是否可以使用</returns>
        private bool CanUseSkill(string skillName)
        {
            // 获取技能可用的插槽类型
            var availableTypes = _bossBlackboard.GetAvailableSlotTypesForSkill(skillName);
            if (availableTypes.Length == 0)
                return true; // 不需要传送门的技能（如roar）
                
            // 检查是否有可用的插槽，避免与上次技能重复
            var portalManager = UnityEngine.Object.FindObjectOfType<PortalManager>();
            if (portalManager == null)
            {
                Debug.LogError("[BossSmartSkillSelection] 未找到PortalManager");
                return false;
            }
            
            var allAvailableSlots = new List<PortalSlot>();
            foreach (var type in availableTypes)
            {
                var slots = portalManager.GetSlotsByType(type);
                if (slots != null)
                {
                    allAvailableSlots.AddRange(slots);
                }
            }
            
            // 检查是否有可用的插槽（避免与上次技能冲突）
            foreach (var slot in allAvailableSlots)
            {
                if (_bossBlackboard.CanUseSlotForSkill(skillName, slot.name))
                {
                    return true; // 至少有一个可用插槽
                }
            }
            
            return false; // 没有可用插槽
        }
        
        /// <summary>
        /// 根据权重随机选择技能
        /// </summary>
        /// <param name="availableSkills">可用技能列表</param>
        /// <returns>选中的技能名称</returns>
        private string SelectSkillByWeight(List<string> availableSkills)
        {
            if (availableSkills.Count == 1)
                return availableSkills[0];
                
            // 计算总权重
            float totalWeight = 0f;
            foreach (string skill in availableSkills)
            {
                int index = System.Array.IndexOf(this.availableSkills, skill);
                if (index >= 0 && index < skillWeights.Length)
                {
                    totalWeight += skillWeights[index];
                }
            }
            
            // 随机选择
            float randomValue = Random.Range(0f, totalWeight);
            float currentWeight = 0f;
            
            foreach (string skill in availableSkills)
            {
                int index = System.Array.IndexOf(this.availableSkills, skill);
                if (index >= 0 && index < skillWeights.Length)
                {
                    currentWeight += skillWeights[index];
                    if (randomValue <= currentWeight)
                    {
                        return skill;
                    }
                }
            }
            
            // 兜底返回第一个技能
            return availableSkills[0];
        }
        
        /// <summary>
        /// 根据技能名称直接创建对应的技能类
        /// </summary>
        /// <param name="skillName">技能名称</param>
        /// <returns>技能Task实例</returns>
        private BossSkillTask CreateSkillTask(string skillName)
        {
            BossSkillTask task = null;
            
            switch (skillName)
            {
                case "tentacle_up":
                    task = new TentacleUpAttack();
                    break;
                case "tentacle_down":
                    task = new TentacleDownAttack();
                    break;
                case "tentacle_left":
                    task = new TentacleLeftAttack();
                    break;
                case "tentacle_right":
                    task = new TentacleRightAttack();
                    break;
                case "wallthrow_left":
                    task = new WallThrowLeftAttack();
                    break;
                case "wallthrow_right":
                    task = new WallThrowRightAttack();
                    break;
                case "bombard":
                    task = new CeilingEnergyBombard();
                    break;
                case "flood":
                    task = new GroundFlood();
                    break;
                case "vortex":
                    task = new VortexLaunch();
                    break;
                case "roar":
                    task = new BossRoar();
                    break;
                default:
                    Debug.LogError($"[BossSmartSkillSelection] 未知的技能名称: {skillName}");
                    return null;
            }
            
            // 设置Owner，这样技能Task就能正确获取BossBlackboard
            if (task != null)
            {
                task.Owner = this.Owner;
                task.isCalledBySmartSkill = true; // 标记为SmartSkill调用
                
                // 根据技能配置设置颜色
                ConfigureSkillColor(task, skillName);
                
                Debug.Log($"[BossSmartSkillSelection] 为技能Task设置Owner: {(task.Owner != null ? task.Owner.name : "null")}");
            }
            
            return task;
        }
        
        /// <summary>
        /// 根据技能配置设置技能颜色
        /// </summary>
        /// <param name="task">技能Task</param>
        /// <param name="skillName">技能名称</param>
        private void ConfigureSkillColor(BossSkillTask task, string skillName)
        {
            // 查找对应的颜色配置
            SkillColorConfig config = null;
            foreach (var skillConfig in skillColorConfigs)
            {
                if (skillConfig.skillName == skillName)
                {
                    config = skillConfig;
                    break;
                }
            }
            
            if (config == null)
            {
                Debug.LogWarning($"[BossSmartSkillSelection] 未找到技能 {skillName} 的颜色配置，使用默认颜色");
                return;
            }
            
            // 设置颜色配置
            if (config.useRandomColor)
            {
                // 随机选择Blue或Orange
                PortalColor[] randomColors = { PortalColor.Blue, PortalColor.Orange };
                var chosen = randomColors[Random.Range(0, randomColors.Length)];
                task.SetCustomPortalColor(chosen);
                Debug.Log($"[BossSmartSkillSelection] 技能 {skillName} 使用随机颜色: {chosen}");
            }
            else
            {
                // 使用指定的首选颜色
                task.SetCustomPortalColor(config.preferredColor);
                Debug.Log($"[BossSmartSkillSelection] 技能 {skillName} 使用指定颜色: {config.preferredColor}");
            }
        }
    }
    
    #endregion
    
    #region 方向化技能实现
    
    /// <summary>
    /// 触手上方攻击
    /// </summary>
    [TaskDescription("触手上方攻击 - 从天花板伸出触手攻击")]
    [TaskIcon("{SkinColor}ActionIcon.png")]
    public class TentacleUpAttack : BossSkillTask
    {
        [Header("触手配置")]
        [UnityEngine.Tooltip("触手长度")]
        public float tentacleLength = 10f;
        
        [UnityEngine.Tooltip("伤害值")]
        public float damage = 60f;
        
        [UnityEngine.Tooltip("击退力度")]
        public float knockbackForce = 10f;
        
        
        public override void OnStart()
        {
            // 自动设置技能参数
            skillName = "tentacle_up";
            portalType = PortalType.Ceiling;
            cooldownTime = 5f;
            spawnPortalTime = 5f;
            telegraphTime = 2f;
            castTime = 6f; // Cast阶段持续6秒
            postAttackTime = 1f;
            
            base.OnStart();
        }
        
        protected override void PlayTelegraphEffects()
        {

        }
        
        protected override void ExecuteSkillEffect()
        {
            // 调用CastManager执行上方触手攻击
            if (_bossBlackboard && _bossBlackboard.castManager)
            {
                _bossBlackboard.castManager.ExecuteTentacleUpCast();
            }
            else
            {
                Debug.LogWarning("[TentacleUpAttack] CastManager未找到，使用备用方案");
                // 备用方案：直接激活BossPart攻击
                if (_bossBlackboard && _bossBlackboard.bossPartManager)
                {
                    _bossBlackboard.bossPartManager.ActivatePartAttack();
                }
            }
        }
        
        protected override void PlayPostAttackEffects()
        {
        }
    }
    
    /// <summary>
    /// 触手下方攻击
    /// </summary>
    [TaskDescription("触手下方攻击 - 从地面伸出触手攻击")]
    [TaskIcon("{SkinColor}ActionIcon.png")]
    public class TentacleDownAttack : BossSkillTask
    {
        [Header("触手配置")]
        [UnityEngine.Tooltip("触手长度")]
        public float tentacleLength = 10f;
        
        [UnityEngine.Tooltip("伤害值")]
        public float damage = 60f;
        
        [UnityEngine.Tooltip("击退力度")]
        public float knockbackForce = 10f;
        
        
        public override void OnStart()
        {
            // 自动设置技能参数
            skillName = "tentacle_down";
            portalType = PortalType.Ground;
            cooldownTime = 5f;
            spawnPortalTime = 5f;
            telegraphTime = 2f;
            castTime = 6f; // Cast阶段持续6秒
            postAttackTime = 1f;
            
            base.OnStart();
        }
        
        protected override void PlayTelegraphEffects()
        {
        }
        
        protected override void ExecuteSkillEffect()
        {
            // 调用CastManager执行下方触手攻击
            if (_bossBlackboard && _bossBlackboard.castManager)
            {
                _bossBlackboard.castManager.ExecuteTentacleDownCast();
            }
            else
            {
                Debug.LogWarning("[TentacleDownAttack] CastManager未找到，使用备用方案");
                // 备用方案：直接激活BossPart攻击
                if (_bossBlackboard && _bossBlackboard.bossPartManager)
                {
                    _bossBlackboard.bossPartManager.ActivatePartAttack();
                }
            }
        }
        
        protected override void PlayPostAttackEffects()
        {
        }
    }
    
    /// <summary>
    /// 触手左方攻击
    /// </summary>
    [TaskDescription("触手左方攻击 - 从左墙伸出触手攻击")]
    [TaskIcon("{SkinColor}ActionIcon.png")]
    public class TentacleLeftAttack : BossSkillTask
    {
        [Header("触手配置")]
        [UnityEngine.Tooltip("触手长度")]
        public float tentacleLength = 10f;
        
        [UnityEngine.Tooltip("伤害值")]
        public float damage = 60f;
        
        [UnityEngine.Tooltip("击退力度")]
        public float knockbackForce = 10f;
        
        
        public override void OnStart()
        {
            // 自动设置技能参数
            skillName = "tentacle_left";
            portalType = PortalType.WallLeft;
            cooldownTime = 5f;
            spawnPortalTime = 5f;
            telegraphTime = 2f;
            castTime = 6f; // Cast阶段持续6秒
            postAttackTime = 1f;
            
            base.OnStart();
        }
        
        protected override void PlayTelegraphEffects()
        {
        }
        
        protected override void ExecuteSkillEffect()
        {
            // 调用CastManager执行左方触手攻击
            if (_bossBlackboard && _bossBlackboard.castManager)
            {
                _bossBlackboard.castManager.ExecuteTentacleLeftCast();
            }
            else
            {
                Debug.LogWarning("[TentacleLeftAttack] CastManager未找到，使用备用方案");
                // 备用方案：直接激活BossPart攻击
                if (_bossBlackboard && _bossBlackboard.bossPartManager)
                {
                    _bossBlackboard.bossPartManager.ActivatePartAttack();
                }
            }
        }
        
        protected override void PlayPostAttackEffects()
        {
        }
    }
    
    /// <summary>
    /// 触手右方攻击
    /// </summary>
    [TaskDescription("触手右方攻击 - 从右墙伸出触手攻击")]
    [TaskIcon("{SkinColor}ActionIcon.png")]
    public class TentacleRightAttack : BossSkillTask
    {
        [Header("触手配置")]
        [UnityEngine.Tooltip("触手长度")]
        public float tentacleLength = 10f;
        
        [UnityEngine.Tooltip("伤害值")]
        public float damage = 60f;
        
        [UnityEngine.Tooltip("击退力度")]
        public float knockbackForce = 10f;
        
        
        public override void OnStart()
        {
            // 自动设置技能参数
            skillName = "tentacle_right";
            portalType = PortalType.WallRight;
            cooldownTime = 5f;
            spawnPortalTime = 5f;
            telegraphTime = 2f;
            castTime = 6f; // Cast阶段持续6秒
            postAttackTime = 1f;
            
            base.OnStart();
        }
        
        protected override void PlayTelegraphEffects()
        {
        }
        
        protected override void ExecuteSkillEffect()
        {
            // 调用CastManager执行右方触手攻击
            if (_bossBlackboard && _bossBlackboard.castManager)
            {
                _bossBlackboard.castManager.ExecuteTentacleRightCast();
            }
            else
            {
                Debug.LogWarning("[TentacleRightAttack] CastManager未找到，使用备用方案");
                // 备用方案：直接激活BossPart攻击
                if (_bossBlackboard && _bossBlackboard.bossPartManager)
                {
                    _bossBlackboard.bossPartManager.ActivatePartAttack();
                }
            }
        }
        
        protected override void PlayPostAttackEffects()
        {
        }
    }
    
    /// <summary>
    /// 侧墙投掷左方攻击
    /// </summary>
    [TaskDescription("侧墙投掷左方攻击 - 从左墙投掷直线攻击")]
    [TaskIcon("{SkinColor}ActionIcon.png")]
    public class WallThrowLeftAttack : BossSkillTask
    {
        [Header("投掷配置")]
        [UnityEngine.Tooltip("投掷物预制体")]
        public GameObject projectilePrefab;
        
        [UnityEngine.Tooltip("投掷物速度")]
        public float projectileSpeed = 25f;
        
        [UnityEngine.Tooltip("伤害值")]
        public float damage = 40f;
        
        
        public override void OnStart()
        {
            // 自动设置技能参数
            skillName = "wallthrow_left";
            portalType = PortalType.WallLeft;
            cooldownTime = 4f;
            spawnPortalTime = 5f;
            telegraphTime = 1.5f;
            postAttackTime = 0.5f;
            
            base.OnStart();
        }
        
        protected override void PlayTelegraphEffects()
        {
        }
        
        protected override void ExecuteSkillEffect()
        {
            
            // 激活BossPart攻击
            if (_bossBlackboard && _bossBlackboard.bossPartManager)
            {
                _bossBlackboard.bossPartManager.ActivatePartAttack();
            }
        }
        
        protected override void PlayPostAttackEffects()
        {
        }
    }
    
    /// <summary>
    /// 侧墙投掷右方攻击
    /// </summary>
    [TaskDescription("侧墙投掷右方攻击 - 从右墙投掷直线攻击")]
    [TaskIcon("{SkinColor}ActionIcon.png")]
    public class WallThrowRightAttack : BossSkillTask
    {
        [Header("投掷配置")]
        [UnityEngine.Tooltip("投掷物预制体")]
        public GameObject projectilePrefab;
        
        [UnityEngine.Tooltip("投掷物速度")]
        public float projectileSpeed = 25f;
        
        [UnityEngine.Tooltip("伤害值")]
        public float damage = 40f;
        
        
        public override void OnStart()
        {
            // 自动设置技能参数
            skillName = "wallthrow_right";
            portalType = PortalType.WallRight;
            cooldownTime = 4f;
            spawnPortalTime = 5f;
            telegraphTime = 1.5f;
            postAttackTime = 0.5f;
            
            base.OnStart();
        }
        
        protected override void PlayTelegraphEffects()
        {
        }
        
        protected override void ExecuteSkillEffect()
        {
            
            // 激活BossPart攻击
            if (_bossBlackboard && _bossBlackboard.bossPartManager)
            {
                _bossBlackboard.bossPartManager.ActivatePartAttack();
            }
        }
        
        protected override void PlayPostAttackEffects()
        {
        }
    }
    
    #endregion
    
}
