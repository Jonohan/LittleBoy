using UnityEngine;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using Sirenix.OdinInspector;

namespace Invector.vCharacterController.AI
{
    /// <summary>
    /// Boss行为树主控制器
    /// 管理Boss的顶层行为逻辑和阶段切换
    /// </summary>
    [AddComponentMenu("Behavior Designer/Boss Behavior Tree")]
    public class BossBehaviorTree : BehaviorTree
    {
        [Header("Boss配置")]
        [UnityEngine.Tooltip("Boss黑板变量引用")]
        public BossBlackboard bossBlackboard;
        
        [UnityEngine.Tooltip("Boss AI控制器引用")]
        public NonHumanoidBossAI bossAI;
        
        [Header("阶段配置")]
        [UnityEngine.Tooltip("P1教学期子树")]
        public ExternalBehaviorTree p1NormalSubtree;
        
        [UnityEngine.Tooltip("P2愤怒期子树")]
        public ExternalBehaviorTree p2AngerSubtree;
        
        [UnityEngine.Tooltip("P3恐惧期子树")]
        public ExternalBehaviorTree p3FearSubtree;
        
        [Header("特殊状态")]
        [UnityEngine.Tooltip("失能状态子树")]
        public ExternalBehaviorTree disabledSubtree;
        
        [UnityEngine.Tooltip("死亡状态子树")]
        public ExternalBehaviorTree deathSubtree;
        
        [Header("调试")]
        [ShowInInspector, ReadOnly]
        private string _currentActiveSubtree;
        
        [ShowInInspector, ReadOnly]
        private string _lastPhaseChange;
        
        private float _lastPhaseChangeTime;
        
        #region Unity生命周期
        
        private void Awake()
        {
            InitializeComponents();
        }
        
        private void Start()
        {
            InitializeBehaviorTree();
        }
        
        #endregion
        
        #region 初始化
        
        /// <summary>
        /// 初始化组件引用
        /// </summary>
        private void InitializeComponents()
        {
            if (!bossBlackboard)
                bossBlackboard = GetComponent<BossBlackboard>();
            
            if (!bossAI)
                bossAI = GetComponent<NonHumanoidBossAI>();
            
            if (!bossBlackboard)
            {
                Debug.LogError($"[BossBehaviorTree] 未找到BossBlackboard组件在 {gameObject.name}");
                enabled = false;
                return;
            }
            
            if (!bossAI)
            {
                Debug.LogError($"[BossBehaviorTree] 未找到NonHumanoidBossAI组件在 {gameObject.name}");
                enabled = false;
                return;
            }
        }
        
        /// <summary>
        /// 初始化行为树
        /// </summary>
        private void InitializeBehaviorTree()
        {
            // 设置行为树变量
            SetVariable("hpPct", bossBlackboard.hpPct);
            SetVariable("phase", bossBlackboard.phase);
            SetVariable("numPortals", bossBlackboard.numPortals);
            SetVariable("lastPortalType", bossBlackboard.lastPortalType);
            SetVariable("target", bossBlackboard.target);
            SetVariable("angerOn", bossBlackboard.angerOn);
            SetVariable("fearOn", bossBlackboard.fearOn);
            SetVariable("disabledOn", bossBlackboard.disabledOn);
            
            // 设置冷却变量
            SetVariable("cooldown_bombard", bossBlackboard.cooldown_bombard);
            SetVariable("cooldown_flood", bossBlackboard.cooldown_flood);
            SetVariable("cooldown_tentacle", bossBlackboard.cooldown_tentacle);
            SetVariable("cooldown_vortex", bossBlackboard.cooldown_vortex);
            SetVariable("cooldown_wallThrow", bossBlackboard.cooldown_wallThrow);
            SetVariable("cooldown_roar", bossBlackboard.cooldown_roar);
            
            Debug.Log("[BossBehaviorTree] 行为树初始化完成");
        }
        
        #endregion
        
        #region 公共方法
        
        /// <summary>
        /// 获取当前活跃的子树
        /// </summary>
        /// <returns>当前活跃的子树名称</returns>
        public string GetCurrentActiveSubtree()
        {
            return _currentActiveSubtree;
        }
        
        /// <summary>
        /// 强制切换到指定阶段
        /// </summary>
        /// <param name="phaseName">阶段名称</param>
        [Button("强制切换阶段")]
        public void ForcePhaseChange(string phaseName)
        {
            switch (phaseName.ToLower())
            {
                case "p1":
                case "normal":
                    bossBlackboard.hpPct.Value = 1.0f;
                    break;
                case "p2":
                case "anger":
                    bossBlackboard.hpPct.Value = bossBlackboard.angerThreshold - 0.1f;
                    break;
                case "p3":
                case "fear":
                    bossBlackboard.hpPct.Value = bossBlackboard.fearThreshold - 0.1f;
                    break;
                case "disabled":
                    bossBlackboard.disabledOn.Value = true;
                    break;
                case "dead":
                    bossBlackboard.hpPct.Value = 0f;
                    break;
            }
            
            _lastPhaseChange = phaseName;
            _lastPhaseChangeTime = Time.time;
            Debug.Log($"[BossBehaviorTree] 强制切换到阶段: {phaseName}");
        }
        
        /// <summary>
        /// 重置到正常状态
        /// </summary>
        [Button("重置到正常状态")]
        public void ResetToNormal()
        {
            bossBlackboard.ResetToNormalState();
            _currentActiveSubtree = "P1_Normal";
            _lastPhaseChange = "Reset";
            _lastPhaseChangeTime = Time.time;
        }
        
        #endregion
        
        #region 调试方法
        
        [Button("测试P1阶段")]
        public void TestP1Phase()
        {
            ForcePhaseChange("P1");
        }
        
        [Button("测试P2阶段")]
        public void TestP2Phase()
        {
            ForcePhaseChange("P2");
        }
        
        [Button("测试P3阶段")]
        public void TestP3Phase()
        {
            ForcePhaseChange("P3");
        }
        
        [Button("测试失能状态")]
        public void TestDisabledState()
        {
            ForcePhaseChange("disabled");
        }
        
        [Button("测试死亡状态")]
        public void TestDeathState()
        {
            ForcePhaseChange("dead");
        }
        
        #endregion
    }
    
    #region 条件节点
    
    /// <summary>
    /// 检查Boss是否死亡
    /// </summary>
    [TaskDescription("检查Boss是否死亡")]
    [TaskIcon("{SkinColor}ConditionIcon.png")]
    public class IsBossDead : Conditional
    {
        [UnityEngine.Tooltip("血量百分比变量")]
        public SharedFloat hpPct;
        
        public override TaskStatus OnUpdate()
        {
            if (hpPct.Value <= 0f)
            {
                return TaskStatus.Success;
            }
            return TaskStatus.Failure;
        }
    }
    
    /// <summary>
    /// 检查Boss是否处于失能状态
    /// </summary>
    [TaskDescription("检查Boss是否处于失能状态")]
    [TaskIcon("{SkinColor}ConditionIcon.png")]
    public class IsBossDisabled : Conditional
    {
        [UnityEngine.Tooltip("失能状态变量")]
        public SharedBool disabledOn;
        
        public override TaskStatus OnUpdate()
        {
            if (disabledOn.Value)
            {
                return TaskStatus.Success;
            }
            return TaskStatus.Failure;
        }
    }
    
    /// <summary>
    /// 检查Boss是否处于恐惧状态
    /// </summary>
    [TaskDescription("检查Boss是否处于恐惧状态")]
    [TaskIcon("{SkinColor}ConditionIcon.png")]
    public class IsBossFearful : Conditional
    {
        [UnityEngine.Tooltip("恐惧状态变量")]
        public SharedBool fearOn;
        
        public override TaskStatus OnUpdate()
        {
            if (fearOn.Value)
            {
                return TaskStatus.Success;
            }
            return TaskStatus.Failure;
        }
    }
    
    /// <summary>
    /// 检查Boss是否处于愤怒状态
    /// </summary>
    [TaskDescription("检查Boss是否处于愤怒状态")]
    [TaskIcon("{SkinColor}ConditionIcon.png")]
    public class IsBossAngry : Conditional
    {
        [UnityEngine.Tooltip("愤怒状态变量")]
        public SharedBool angerOn;
        
        public override TaskStatus OnUpdate()
        {
            if (angerOn.Value)
            {
                return TaskStatus.Success;
            }
            return TaskStatus.Failure;
        }
    }
    
    #endregion
    
    #region 动作节点
    
    /// <summary>
    /// 播放死亡动画
    /// </summary>
    [TaskDescription("播放Boss死亡动画")]
    [TaskIcon("{SkinColor}ActionIcon.png")]
    public class PlayBossDeath : Action
    {
        [UnityEngine.Tooltip("Boss AI控制器")]
        public SharedGameObject bossAI;
        
        [UnityEngine.Tooltip("死亡动画触发参数")]
        public SharedString deathTrigger = "IsDead";
        
        public override void OnStart()
        {
            var ai = bossAI.Value?.GetComponent<NonHumanoidBossAI>();
            if (ai && ai.animator)
            {
                ai.animator.SetBool(deathTrigger.Value, true);
                Debug.Log("[PlayBossDeath] 播放死亡动画");
            }
        }
        
        public override TaskStatus OnUpdate()
        {
            // 死亡动画播放完成后返回成功
            return TaskStatus.Success;
        }
    }
    
    /// <summary>
    /// 失能状态循环 - 颤抖恐慌
    /// </summary>
    [TaskDescription("Boss失能状态循环 - 颤抖恐慌")]
    [TaskIcon("{SkinColor}ActionIcon.png")]
    public class LoopShiverPanic : Action
    {
        [UnityEngine.Tooltip("Boss AI控制器")]
        public SharedGameObject bossAI;
        
        [UnityEngine.Tooltip("颤抖动画触发参数")]
        public SharedString shiverTrigger = "IsShivering";
        
        [UnityEngine.Tooltip("恐慌动画触发参数")]
        public SharedString panicTrigger = "IsPanicking";
        
        private bool _isShivering = false;
        private float _shiverTimer = 0f;
        private float _shiverInterval = 2f;
        
        public override void OnStart()
        {
            var ai = bossAI.Value?.GetComponent<NonHumanoidBossAI>();
            if (ai && ai.animator)
            {
                ai.animator.SetBool(panicTrigger.Value, true);
                Debug.Log("[LoopShiverPanic] 开始失能状态循环");
            }
        }
        
        public override TaskStatus OnUpdate()
        {
            var ai = bossAI.Value?.GetComponent<NonHumanoidBossAI>();
            if (!ai || !ai.animator) return TaskStatus.Failure;
            
            // 交替播放颤抖和恐慌动画
            _shiverTimer += Time.deltaTime;
            if (_shiverTimer >= _shiverInterval)
            {
                _isShivering = !_isShivering;
                ai.animator.SetBool(shiverTrigger.Value, _isShivering);
                _shiverTimer = 0f;
            }
            
            // 失能状态持续运行
            return TaskStatus.Running;
        }
        
        public override void OnEnd()
        {
            var ai = bossAI.Value?.GetComponent<NonHumanoidBossAI>();
            if (ai && ai.animator)
            {
                ai.animator.SetBool(shiverTrigger.Value, false);
                ai.animator.SetBool(panicTrigger.Value, false);
                Debug.Log("[LoopShiverPanic] 结束失能状态循环");
            }
        }
    }
    
    /// <summary>
    /// 启动子树
    /// </summary>
    [TaskDescription("启动指定的子树")]
    [TaskIcon("{SkinColor}ActionIcon.png")]
    public class StartSubtree : Action
    {
        [UnityEngine.Tooltip("要启动的子树GameObject")]
        public SharedGameObject subtreeGameObject;
        
        [UnityEngine.Tooltip("是否等待子树完成")]
        public SharedBool waitForCompletion = false;
        
        private Behavior _subtreeBehavior;
        
        public override void OnStart()
        {
            if (subtreeGameObject.Value)
            {
                _subtreeBehavior = subtreeGameObject.Value.GetComponent<Behavior>();
                if (_subtreeBehavior)
                {
                    _subtreeBehavior.EnableBehavior();
                    Debug.Log($"[StartSubtree] 启动子树: {subtreeGameObject.Value.name}");
                }
            }
        }
        
        public override TaskStatus OnUpdate()
        {
            if (!_subtreeBehavior) return TaskStatus.Failure;
            
            if (waitForCompletion.Value)
            {
                // 等待子树完成
                return _subtreeBehavior.enabled ? TaskStatus.Running : TaskStatus.Success;
            }
            else
            {
                // 不等待，立即返回成功
                return TaskStatus.Success;
            }
        }
        
        public override void OnEnd()
        {
            if (_subtreeBehavior && !waitForCompletion.Value)
            {
                _subtreeBehavior.DisableBehavior();
            }
        }
    }
    
    #endregion
}
