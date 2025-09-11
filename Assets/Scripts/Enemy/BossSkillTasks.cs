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
        protected float postAttackTime = 1f;
        protected PortalType portalType = PortalType.Ceiling;
        protected PortalColor portalColor = PortalColor.Blue;
        protected bool useDynamicPortalColor = true;
        
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
            
            // 检查施放时间（给技能效果一些执行时间）
            if (Time.time - _skillStartTime >= telegraphTime + 0.1f) // 给0.1秒的执行时间
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
            if (Time.time - _skillStartTime >= telegraphTime + postAttackTime)
            {
                _currentPhase = SkillPhase.Cleanup;
            }
            
            return TaskStatus.Running;
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
            
            // 传送门不需要关闭，它们一直存在
            
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
            
            // 过滤掉与上次技能冲突的插槽
            var validSlots = allAvailableSlots.Where(slot => 
                _bossBlackboard.CanUseSlotForSkill(skillName, slot.name)).ToList();
            
            // 如果没有其他插槽可选，则使用所有插槽
            if (validSlots.Count == 0)
            {
                validSlots = allAvailableSlots;
            }
            
            // 随机选择一个插槽
            int randomIndex = Random.Range(0, validSlots.Count);
            PortalSlot selectedSlot = validSlots[randomIndex];
            
            
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
        [UnityEngine.Tooltip("可用技能列表")]
        public string[] availableSkills = {
            "tentacle_up", "tentacle_down", "tentacle_left", "tentacle_right",
            "wallthrow_left", "wallthrow_right",
            "bombard", "flood", "vortex", "roar"
        };
        
        [UnityEngine.Tooltip("技能权重（影响选择概率）")]
        public float[] skillWeights = {
            1f, 1f, 1f, 1f,  // tentacle技能
            1f, 1f,          // wallthrow技能
            1f, 1f, 1f, 1f   // 其他技能
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
                
                if (!_bossBlackboard)
                {
                    Debug.LogError("[BossSmartSkillSelection] BossBlackboard未找到");
                    return TaskStatus.Failure;
                }
                
                // 获取可用的技能
                var validSkills = GetAvailableSkills();
               
                if (validSkills.Count == 0)
                {
                    return TaskStatus.Failure;
                }
                
                // 根据权重随机选择技能
                string selectedSkillName = SelectSkillByWeight(validSkills);
                selectedSkill.Value = selectedSkillName;
                
                
                // 直接创建并开始执行技能
                _currentSkillTask = CreateSkillTask(selectedSkillName);
                if (_currentSkillTask == null)
                {
                    Debug.LogError($"[BossSmartSkillSelection] 未找到技能类: {selectedSkillName}");
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
            
            Debug.Log($"[BossSmartSkillSelection] 上次使用的技能: {lastUsedSkill}, 插槽: {lastUsedSlot}");
            
            for (int i = 0; i < availableSkills.Length; i++)
            {
                string skillName = availableSkills[i];
                
                // 检查技能冷却
                if (IsSkillOnCooldown(skillName))
                {
                    Debug.Log($"[BossSmartSkillSelection] 技能 {skillName} 在冷却中");
                    continue;
                }
                    
                // 检查技能是否可以使用（避免与上次技能重复）
                if (CanUseSkill(skillName))
                {
                    available.Add(skillName);
                    Debug.Log($"[BossSmartSkillSelection] 技能 {skillName} 可用");
                }
                else
                {
                    Debug.Log($"[BossSmartSkillSelection] 技能 {skillName} 不可用（与上次技能冲突）");
                }
            }
            
            Debug.Log($"[BossSmartSkillSelection] 最终可用技能: {string.Join(", ", available)}");
            return available;
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
                Debug.Log($"[BossSmartSkillSelection] 为技能Task设置Owner: {(task.Owner != null ? task.Owner.name : "null")}");
            }
            
            return task;
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
            postAttackTime = 1f;
            
            base.OnStart();
        }
        
        protected override void PlayTelegraphEffects()
        {
            Debug.Log($"[{skillName}] 播放触手上方前摇特效");
        }
        
        protected override void ExecuteSkillEffect()
        {
            Debug.Log($"[{skillName}] 执行触手上方攻击");
            
            // 激活BossPart攻击
            if (_bossBlackboard && _bossBlackboard.bossPartManager)
            {
                _bossBlackboard.bossPartManager.ActivatePartAttack();
            }
        }
        
        protected override void PlayPostAttackEffects()
        {
            Debug.Log($"[{skillName}] 播放触手上方后摇特效");
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
            postAttackTime = 1f;
            
            base.OnStart();
        }
        
        protected override void PlayTelegraphEffects()
        {
            Debug.Log($"[{skillName}] 播放触手下方前摇特效");
        }
        
        protected override void ExecuteSkillEffect()
        {
            Debug.Log($"[{skillName}] 执行触手下方攻击");
            
            // 激活BossPart攻击
            if (_bossBlackboard && _bossBlackboard.bossPartManager)
            {
                _bossBlackboard.bossPartManager.ActivatePartAttack();
            }
        }
        
        protected override void PlayPostAttackEffects()
        {
            Debug.Log($"[{skillName}] 播放触手下方后摇特效");
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
            postAttackTime = 1f;
            
            base.OnStart();
        }
        
        protected override void PlayTelegraphEffects()
        {
            Debug.Log($"[{skillName}] 播放触手左方前摇特效");
        }
        
        protected override void ExecuteSkillEffect()
        {
            Debug.Log($"[{skillName}] 执行触手左方攻击");
            
            // 激活BossPart攻击
            if (_bossBlackboard && _bossBlackboard.bossPartManager)
            {
                _bossBlackboard.bossPartManager.ActivatePartAttack();
            }
        }
        
        protected override void PlayPostAttackEffects()
        {
            Debug.Log($"[{skillName}] 播放触手左方后摇特效");
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
            postAttackTime = 1f;
            
            base.OnStart();
        }
        
        protected override void PlayTelegraphEffects()
        {
            Debug.Log($"[{skillName}] 播放触手右方前摇特效");
        }
        
        protected override void ExecuteSkillEffect()
        {
            Debug.Log($"[{skillName}] 执行触手右方攻击");
            
            // 激活BossPart攻击
            if (_bossBlackboard && _bossBlackboard.bossPartManager)
            {
                _bossBlackboard.bossPartManager.ActivatePartAttack();
            }
        }
        
        protected override void PlayPostAttackEffects()
        {
            Debug.Log($"[{skillName}] 播放触手右方后摇特效");
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
            Debug.Log($"[{skillName}] 播放侧墙投掷左方前摇特效");
        }
        
        protected override void ExecuteSkillEffect()
        {
            Debug.Log($"[{skillName}] 执行侧墙投掷左方攻击");
            
            // 激活BossPart攻击
            if (_bossBlackboard && _bossBlackboard.bossPartManager)
            {
                _bossBlackboard.bossPartManager.ActivatePartAttack();
            }
        }
        
        protected override void PlayPostAttackEffects()
        {
            Debug.Log($"[{skillName}] 播放侧墙投掷左方后摇特效");
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
            Debug.Log($"[{skillName}] 播放侧墙投掷右方前摇特效");
        }
        
        protected override void ExecuteSkillEffect()
        {
            Debug.Log($"[{skillName}] 执行侧墙投掷右方攻击");
            
            // 激活BossPart攻击
            if (_bossBlackboard && _bossBlackboard.bossPartManager)
            {
                _bossBlackboard.bossPartManager.ActivatePartAttack();
            }
        }
        
        protected override void PlayPostAttackEffects()
        {
            Debug.Log($"[{skillName}] 播放侧墙投掷右方后摇特效");
        }
    }
    
    #endregion
    
}
