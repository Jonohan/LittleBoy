using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Xuwu.Character;

namespace Invector.vCharacterController.AI
{
    /// <summary>
    /// 中断优先级枚举
    /// </summary>
    public enum InterruptPriority
    {
        None = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }
    
    /// <summary>
    /// 中断事件类型
    /// </summary>
    public enum InterruptType
    {
        None,
        Death,           // 死亡
        Disabled,        // 失能
        Fear,           // 恐惧
        Anger,          // 愤怒
        Hit,            // 受击
        Stagger,        // 踉跄
        PhaseChange,    // 阶段切换
        PlayerSizeChange // 玩家体型变化
    }
    
    /// <summary>
    /// 中断事件数据
    /// </summary>
    [System.Serializable]
    public class InterruptEvent
    {
        public InterruptType type;
        public InterruptPriority priority;
        public float timestamp;
        public object data;
        
        public InterruptEvent(InterruptType t, InterruptPriority p, object d = null)
        {
            type = t;
            priority = p;
            timestamp = Time.time;
            data = d;
        }
    }
    
    /// <summary>
    /// Boss中断优先级系统
    /// 管理与Animator配合的紧急状态切换
    /// </summary>
    public class BossInterruptSystem : MonoBehaviour
    {
        [Header("中断配置")]
        [Tooltip("是否启用中断系统")]
        public bool enableInterruptSystem = true;
        
        [Tooltip("中断冷却时间")]
        public float interruptCooldown = 0.5f;
        
        [Header("组件引用")]
        [Tooltip("Boss AI控制器")]
        public NonHumanoidBossAI bossAI;
        
        [Tooltip("Boss黑板变量")]
        public BossBlackboard bossBlackboard;
        
        [Tooltip("Boss行为树")]
        public BossBehaviorTree bossBehaviorTree;
        
        [Header("动画参数")]
        [Tooltip("受击动画触发参数")]
        public string hitTrigger = "Hit";
        
        [Tooltip("踉跄动画触发参数")]
        public string staggerTrigger = "Stagger";
        
        [Tooltip("死亡动画触发参数")]
        public string deathTrigger = "IsDead";
        
        [Tooltip("失能动画触发参数")]
        public string disabledTrigger = "IsDisabled";
        
        [Tooltip("恐惧动画触发参数")]
        public string fearTrigger = "IsFearful";
        
        [Tooltip("愤怒动画触发参数")]
        public string angerTrigger = "IsAngry";
        
        [Header("调试")]
        [ShowInInspector, ReadOnly]
        private List<InterruptEvent> _pendingInterrupts = new List<InterruptEvent>();
        
        [ShowInInspector, ReadOnly]
        private InterruptEvent _currentInterrupt;
        
        [ShowInInspector, ReadOnly]
        private float _lastInterruptTime;
        
        [ShowInInspector, ReadOnly]
        private bool _isInterrupting = false;
        
        // 私有变量
        private Animator _animator;
        private float _lastPhaseChangeTime;
        private string _lastPhase;
        private float _lastPlayerSize;
        
        #region Unity生命周期
        
        private void Awake()
        {
            InitializeComponents();
        }
        
        private void Start()
        {
            InitializeInterruptSystem();
        }
        
        private void Update()
        {
            if (!enableInterruptSystem) return;
            
            CheckForInterrupts();
            ProcessInterrupts();
        }
        
        #endregion
        
        #region 初始化
        
        /// <summary>
        /// 初始化组件引用
        /// </summary>
        private void InitializeComponents()
        {
            if (!bossAI)
                bossAI = GetComponent<NonHumanoidBossAI>();
            
            if (!bossBlackboard)
                bossBlackboard = GetComponent<BossBlackboard>();
            
            if (!bossBehaviorTree)
                bossBehaviorTree = GetComponent<BossBehaviorTree>();
            
            if (bossAI)
                _animator = bossAI.animator;
            
            if (!_animator)
            {
                Debug.LogError($"[BossInterruptSystem] 未找到Animator组件在 {gameObject.name}");
                enabled = false;
                return;
            }
        }
        
        /// <summary>
        /// 初始化中断系统
        /// </summary>
        private void InitializeInterruptSystem()
        {
            if (bossBlackboard)
            {
                _lastPhase = bossBlackboard.phase.Value;
                _lastPlayerSize = GetPlayerSize();
            }
            
            Debug.Log("[BossInterruptSystem] 中断系统初始化完成");
        }
        
        #endregion
        
        #region 中断检测
        
        /// <summary>
        /// 检查中断事件
        /// </summary>
        private void CheckForInterrupts()
        {
            if (!bossBlackboard) return;
            
            // 检查死亡
            CheckDeathInterrupt();
            
            // 检查失能状态
            CheckDisabledInterrupt();
            
            // 检查阶段变化
            CheckPhaseChangeInterrupt();
            
            // 检查玩家体型变化
            CheckPlayerSizeChangeInterrupt();
        }
        
        /// <summary>
        /// 检查死亡中断
        /// </summary>
        private void CheckDeathInterrupt()
        {
            if (bossBlackboard.hpPct.Value <= 0f)
            {
                AddInterrupt(InterruptType.Death, InterruptPriority.Critical);
            }
        }
        
        /// <summary>
        /// 检查失能中断
        /// </summary>
        private void CheckDisabledInterrupt()
        {
            if (bossBlackboard.disabledOn.Value)
            {
                AddInterrupt(InterruptType.Disabled, InterruptPriority.High);
            }
        }
        
        /// <summary>
        /// 检查阶段变化中断
        /// </summary>
        private void CheckPhaseChangeInterrupt()
        {
            string currentPhase = bossBlackboard.phase.Value;
            if (currentPhase != _lastPhase)
            {
                InterruptPriority priority = GetPhaseChangePriority(_lastPhase, currentPhase);
                if (priority > InterruptPriority.None)
                {
                    AddInterrupt(InterruptType.PhaseChange, priority, currentPhase);
                }
                _lastPhase = currentPhase;
                _lastPhaseChangeTime = Time.time;
            }
        }
        
        /// <summary>
        /// 检查玩家体型变化中断
        /// </summary>
        private void CheckPlayerSizeChangeInterrupt()
        {
            float currentSize = GetPlayerSize();
            if (Mathf.Abs(currentSize - _lastPlayerSize) > 0.1f)
            {
                // 玩家体型变化，特别是达到4.5倍时
                if (currentSize >= 4.5f && _lastPlayerSize < 4.5f)
                {
                    AddInterrupt(InterruptType.PlayerSizeChange, InterruptPriority.High, currentSize);
                }
                _lastPlayerSize = currentSize;
            }
        }
        
        #endregion
        
        #region 中断处理
        
        /// <summary>
        /// 处理中断事件
        /// </summary>
        private void ProcessInterrupts()
        {
            if (_pendingInterrupts.Count == 0) return;
            
            // 按优先级排序
            _pendingInterrupts.Sort((a, b) => b.priority.CompareTo(a.priority));
            
            // 处理最高优先级的中断
            InterruptEvent interrupt = _pendingInterrupts[0];
            _pendingInterrupts.RemoveAt(0);
            
            // 检查中断冷却
            if (Time.time - _lastInterruptTime < interruptCooldown)
            {
                // 如果冷却中，将中断重新加入队列
                _pendingInterrupts.Add(interrupt);
                return;
            }
            
            ExecuteInterrupt(interrupt);
        }
        
        /// <summary>
        /// 执行中断
        /// </summary>
        /// <param name="interrupt">中断事件</param>
        private void ExecuteInterrupt(InterruptEvent interrupt)
        {
            _currentInterrupt = interrupt;
            _lastInterruptTime = Time.time;
            _isInterrupting = true;
            
            Debug.Log($"[BossInterruptSystem] 执行中断: {interrupt.type} (优先级: {interrupt.priority})");
            
            switch (interrupt.type)
            {
                case InterruptType.Death:
                    HandleDeathInterrupt();
                    break;
                case InterruptType.Disabled:
                    HandleDisabledInterrupt();
                    break;
                case InterruptType.Fear:
                    HandleFearInterrupt();
                    break;
                case InterruptType.Anger:
                    HandleAngerInterrupt();
                    break;
                case InterruptType.Hit:
                    HandleHitInterrupt();
                    break;
                case InterruptType.Stagger:
                    HandleStaggerInterrupt();
                    break;
                case InterruptType.PhaseChange:
                    HandlePhaseChangeInterrupt(interrupt.data);
                    break;
                case InterruptType.PlayerSizeChange:
                    HandlePlayerSizeChangeInterrupt(interrupt.data);
                    break;
            }
            
            _isInterrupting = false;
        }
        
        #endregion
        
        #region 中断处理实现
        
        /// <summary>
        /// 处理死亡中断
        /// </summary>
        private void HandleDeathInterrupt()
        {
            if (_animator)
            {
                _animator.SetBool(deathTrigger, true);
            }
            
            // 停止所有行为
            if (bossBehaviorTree)
            {
                bossBehaviorTree.DisableBehavior();
            }
        }
        
        /// <summary>
        /// 处理失能中断
        /// </summary>
        private void HandleDisabledInterrupt()
        {
            if (_animator)
            {
                _animator.SetBool(disabledTrigger, true);
            }
        }
        
        /// <summary>
        /// 处理恐惧中断
        /// </summary>
        private void HandleFearInterrupt()
        {
            if (_animator)
            {
                _animator.SetBool(fearTrigger, true);
            }
        }
        
        /// <summary>
        /// 处理愤怒中断
        /// </summary>
        private void HandleAngerInterrupt()
        {
            if (_animator)
            {
                _animator.SetBool(angerTrigger, true);
            }
        }
        
        /// <summary>
        /// 处理受击中断
        /// </summary>
        private void HandleHitInterrupt()
        {
            if (_animator)
            {
                _animator.SetTrigger(hitTrigger);
            }
        }
        
        /// <summary>
        /// 处理踉跄中断
        /// </summary>
        private void HandleStaggerInterrupt()
        {
            if (_animator)
            {
                _animator.SetTrigger(staggerTrigger);
            }
        }
        
        /// <summary>
        /// 处理阶段变化中断
        /// </summary>
        /// <param name="data">阶段数据</param>
        private void HandlePhaseChangeInterrupt(object data)
        {
            string newPhase = data?.ToString() ?? "Unknown";
            
            // 根据新阶段设置动画参数
            if (_animator)
            {
                _animator.SetBool(fearTrigger, newPhase == "P3_Fear");
                _animator.SetBool(angerTrigger, newPhase == "P2_Anger");
            }
            
            Debug.Log($"[BossInterruptSystem] 阶段变化中断: {newPhase}");
        }
        
        /// <summary>
        /// 处理玩家体型变化中断
        /// </summary>
        /// <param name="data">体型数据</param>
        private void HandlePlayerSizeChangeInterrupt(object data)
        {
            float newSize = 1f;
            if (data is float)
            {
                newSize = (float)data;
            }
            
            // 玩家达到4.5倍体型时触发失能
            if (newSize >= 4.5f)
            {
                if (bossBlackboard)
                {
                    bossBlackboard.disabledOn.Value = true;
                }
            }
            
            Debug.Log($"[BossInterruptSystem] 玩家体型变化中断: {newSize}");
        }
        
        #endregion
        
        #region 辅助方法
        
        /// <summary>
        /// 添加中断事件
        /// </summary>
        /// <param name="type">中断类型</param>
        /// <param name="priority">优先级</param>
        /// <param name="data">数据</param>
        private void AddInterrupt(InterruptType type, InterruptPriority priority, object data = null)
        {
            // 检查是否已存在相同类型的中断
            for (int i = _pendingInterrupts.Count - 1; i >= 0; i--)
            {
                if (_pendingInterrupts[i].type == type)
                {
                    // 如果新中断优先级更高，替换旧中断
                    if (priority > _pendingInterrupts[i].priority)
                    {
                        _pendingInterrupts[i] = new InterruptEvent(type, priority, data);
                    }
                    return;
                }
            }
            
            // 添加新中断
            _pendingInterrupts.Add(new InterruptEvent(type, priority, data));
        }
        
        /// <summary>
        /// 获取阶段变化优先级
        /// </summary>
        /// <param name="oldPhase">旧阶段</param>
        /// <param name="newPhase">新阶段</param>
        /// <returns>优先级</returns>
        private InterruptPriority GetPhaseChangePriority(string oldPhase, string newPhase)
        {
            // 死亡优先级最高
            if (newPhase == "Dead")
                return InterruptPriority.Critical;
            
            // 失能优先级高
            if (newPhase.Contains("Disabled"))
                return InterruptPriority.High;
            
            // 恐惧优先级中等
            if (newPhase == "P3_Fear")
                return InterruptPriority.Medium;
            
            // 愤怒优先级中等
            if (newPhase == "P2_Anger")
                return InterruptPriority.Medium;
            
            // 其他变化优先级低
            return InterruptPriority.Low;
        }
        
        /// <summary>
        /// 获取玩家当前体型
        /// </summary>
        /// <returns>体型倍数</returns>
        private float GetPlayerSize()
        {
            if (bossBlackboard && bossBlackboard.target.Value)
            {
                var sizeController = bossBlackboard.target.Value.GetComponent<CharacterSizeController>();
                if (sizeController)
                {
                    return sizeController.GetCurrentSize();
                }
            }
            return 1f;
        }
        
        #endregion
        
        #region 公共方法
        
        /// <summary>
        /// 手动触发受击中断
        /// </summary>
        public void TriggerHitInterrupt()
        {
            AddInterrupt(InterruptType.Hit, InterruptPriority.Medium);
        }
        
        /// <summary>
        /// 手动触发踉跄中断
        /// </summary>
        public void TriggerStaggerInterrupt()
        {
            AddInterrupt(InterruptType.Stagger, InterruptPriority.High);
        }
        
        /// <summary>
        /// 清除所有待处理的中断
        /// </summary>
        public void ClearPendingInterrupts()
        {
            _pendingInterrupts.Clear();
        }
        
        /// <summary>
        /// 检查是否正在中断
        /// </summary>
        /// <returns>是否正在中断</returns>
        public bool IsInterrupting()
        {
            return _isInterrupting;
        }
        
        /// <summary>
        /// 获取当前中断信息
        /// </summary>
        /// <returns>当前中断信息</returns>
        public string GetCurrentInterruptInfo()
        {
            if (_currentInterrupt != null)
            {
                return $"当前中断: {_currentInterrupt.type} (优先级: {_currentInterrupt.priority})";
            }
            return "无当前中断";
        }
        
        #endregion
        
        #region 调试方法
        
        [Button("测试受击中断")]
        public void TestHitInterrupt()
        {
            TriggerHitInterrupt();
        }
        
        [Button("测试踉跄中断")]
        public void TestStaggerInterrupt()
        {
            TriggerStaggerInterrupt();
        }
        
        [Button("清除所有中断")]
        public void ClearAllInterrupts()
        {
            ClearPendingInterrupts();
        }
        
        [Button("显示中断状态")]
        public void ShowInterruptStatus()
        {
            Debug.Log($"[BossInterruptSystem] {GetCurrentInterruptInfo()}");
            Debug.Log($"[BossInterruptSystem] 待处理中断数量: {_pendingInterrupts.Count}");
            Debug.Log($"[BossInterruptSystem] 是否正在中断: {_isInterrupting}");
        }
        
        #endregion
    }
}
