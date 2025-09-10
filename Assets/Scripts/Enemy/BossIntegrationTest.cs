using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections;

namespace Invector.vCharacterController.AI
{
    /// <summary>
    /// Boss集成测试脚本
    /// 验证行为树与现有AI系统的兼容性
    /// </summary>
    public class BossIntegrationTest : MonoBehaviour
    {
        [Header("测试配置")]
        [Tooltip("是否启用自动测试")]
        public bool enableAutoTest = false;
        
        [Tooltip("测试间隔时间")]
        public float testInterval = 5f;
        
        [Header("组件引用")]
        [Tooltip("Boss黑板变量")]
        public BossBlackboard bossBlackboard;
        
        [Tooltip("Boss行为树")]
        public BossBehaviorTree bossBehaviorTree;
        
        [Tooltip("传送门管理器")]
        public PortalManager portalManager;
        
        [Tooltip("阶段权重管理器")]
        public BossPhaseWeights bossPhaseWeights;
        
        [Tooltip("中断系统")]
        public BossInterruptSystem bossInterruptSystem;
        
        [Tooltip("Boss AI控制器")]
        public NonHumanoidBossAI bossAI;
        
        [Header("测试状态")]
        [ShowInInspector, ReadOnly]
        private bool _isTestRunning = false;
        
        [ShowInInspector, ReadOnly]
        private int _currentTestStep = 0;
        
        [ShowInInspector, ReadOnly]
        private string _testStatus = "未开始";
        
        [ShowInInspector, ReadOnly]
        private float _testStartTime;
        
        // 测试步骤
        private readonly string[] _testSteps = {
            "初始化检查",
            "P1阶段测试",
            "P2阶段测试", 
            "P3阶段测试",
            "失能状态测试",
            "死亡状态测试",
            "传送门系统测试",
            "技能权重测试",
            "中断系统测试",
            "集成测试完成"
        };
        
        #region Unity生命周期
        
        private void Start()
        {
            InitializeComponents();
            
            if (enableAutoTest)
            {
                StartCoroutine(AutoTestCoroutine());
            }
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
            
            if (!bossBehaviorTree)
                bossBehaviorTree = GetComponent<BossBehaviorTree>();
            
            if (!portalManager)
                portalManager = GetComponent<PortalManager>();
            
            if (!bossPhaseWeights)
                bossPhaseWeights = GetComponent<BossPhaseWeights>();
            
            if (!bossInterruptSystem)
                bossInterruptSystem = GetComponent<BossInterruptSystem>();
            
            if (!bossAI)
                bossAI = GetComponent<NonHumanoidBossAI>();
            
            Debug.Log("[BossIntegrationTest] 组件初始化完成");
        }
        
        #endregion
        
        #region 自动测试
        
        /// <summary>
        /// 自动测试协程
        /// </summary>
        private IEnumerator AutoTestCoroutine()
        {
            while (enableAutoTest)
            {
                yield return new WaitForSeconds(testInterval);
                
                if (!_isTestRunning)
                {
                    StartIntegrationTest();
                }
            }
        }
        
        #endregion
        
        #region 集成测试
        
        /// <summary>
        /// 开始集成测试
        /// </summary>
        [Button("开始集成测试")]
        public void StartIntegrationTest()
        {
            if (_isTestRunning)
            {
                Debug.LogWarning("[BossIntegrationTest] 测试已在运行中");
                return;
            }
            
            _isTestRunning = true;
            _currentTestStep = 0;
            _testStartTime = Time.time;
            _testStatus = "测试进行中";
            
            Debug.Log("[BossIntegrationTest] 开始集成测试");
            StartCoroutine(IntegrationTestCoroutine());
        }
        
        /// <summary>
        /// 集成测试协程
        /// </summary>
        private IEnumerator IntegrationTestCoroutine()
        {
            // 步骤1: 初始化检查
            yield return StartCoroutine(TestInitialization());
            
            // 步骤2: P1阶段测试
            yield return StartCoroutine(TestP1Phase());
            
            // 步骤3: P2阶段测试
            yield return StartCoroutine(TestP2Phase());
            
            // 步骤4: P3阶段测试
            yield return StartCoroutine(TestP3Phase());
            
            // 步骤5: 失能状态测试
            yield return StartCoroutine(TestDisabledState());
            
            // 步骤6: 死亡状态测试
            yield return StartCoroutine(TestDeathState());
            
            // 步骤7: 传送门系统测试
            yield return StartCoroutine(TestPortalSystem());
            
            // 步骤8: 技能权重测试
            yield return StartCoroutine(TestSkillWeights());
            
            // 步骤9: 中断系统测试
            yield return StartCoroutine(TestInterruptSystem());
            
            // 步骤10: 完成测试
            CompleteTest();
        }
        
        #endregion
        
        #region 测试步骤实现
        
        /// <summary>
        /// 测试初始化
        /// </summary>
        private IEnumerator TestInitialization()
        {
            _currentTestStep = 1;
            Debug.Log($"[BossIntegrationTest] 步骤 {_currentTestStep}: {_testSteps[_currentTestStep - 1]}");
            
            bool allComponentsFound = true;
            
            // 检查所有必需组件
            if (!bossBlackboard)
            {
                Debug.LogError("[BossIntegrationTest] BossBlackboard 组件未找到");
                allComponentsFound = false;
            }
            
            if (!bossBehaviorTree)
            {
                Debug.LogError("[BossIntegrationTest] BossBehaviorTree 组件未找到");
                allComponentsFound = false;
            }
            
            if (!portalManager)
            {
                Debug.LogError("[BossIntegrationTest] PortalManager 组件未找到");
                allComponentsFound = false;
            }
            
            if (!bossPhaseWeights)
            {
                Debug.LogError("[BossIntegrationTest] BossPhaseWeights 组件未找到");
                allComponentsFound = false;
            }
            
            if (!bossInterruptSystem)
            {
                Debug.LogError("[BossIntegrationTest] BossInterruptSystem 组件未找到");
                allComponentsFound = false;
            }
            
            if (!bossAI)
            {
                Debug.LogError("[BossIntegrationTest] NonHumanoidBossAI 组件未找到");
                allComponentsFound = false;
            }
            
            if (allComponentsFound)
            {
                Debug.Log("[BossIntegrationTest] ✓ 所有组件检查通过");
            }
            
            yield return new WaitForSeconds(1f);
        }
        
        /// <summary>
        /// 测试P1阶段
        /// </summary>
        private IEnumerator TestP1Phase()
        {
            _currentTestStep = 2;
            Debug.Log($"[BossIntegrationTest] 步骤 {_currentTestStep}: {_testSteps[_currentTestStep - 1]}");
            
            // 设置P1阶段
            if (bossBlackboard)
            {
                bossBlackboard.ResetToNormalState();
                yield return new WaitForSeconds(0.5f);
                
                // 验证阶段状态
                if (bossBlackboard.phase.Value == "P1_Normal")
                {
                    Debug.Log("[BossIntegrationTest] ✓ P1阶段设置成功");
                }
                else
                {
                    Debug.LogError($"[BossIntegrationTest] P1阶段设置失败，当前阶段: {bossBlackboard.phase.Value}");
                }
            }
            
            yield return new WaitForSeconds(2f);
        }
        
        /// <summary>
        /// 测试P2阶段
        /// </summary>
        private IEnumerator TestP2Phase()
        {
            _currentTestStep = 3;
            Debug.Log($"[BossIntegrationTest] 步骤 {_currentTestStep}: {_testSteps[_currentTestStep - 1]}");
            
            // 设置P2阶段
            if (bossBlackboard)
            {
                bossBlackboard.ForceAngerState();
                yield return new WaitForSeconds(0.5f);
                
                // 验证阶段状态
                if (bossBlackboard.phase.Value == "P2_Anger" && bossBlackboard.angerOn.Value)
                {
                    Debug.Log("[BossIntegrationTest] ✓ P2阶段设置成功");
                }
                else
                {
                    Debug.LogError($"[BossIntegrationTest] P2阶段设置失败，当前阶段: {bossBlackboard.phase.Value}");
                }
            }
            
            yield return new WaitForSeconds(2f);
        }
        
        /// <summary>
        /// 测试P3阶段
        /// </summary>
        private IEnumerator TestP3Phase()
        {
            _currentTestStep = 4;
            Debug.Log($"[BossIntegrationTest] 步骤 {_currentTestStep}: {_testSteps[_currentTestStep - 1]}");
            
            // 设置P3阶段
            if (bossBlackboard)
            {
                bossBlackboard.ForceFearState();
                yield return new WaitForSeconds(0.5f);
                
                // 验证阶段状态
                if (bossBlackboard.phase.Value == "P3_Fear" && bossBlackboard.fearOn.Value)
                {
                    Debug.Log("[BossIntegrationTest] ✓ P3阶段设置成功");
                }
                else
                {
                    Debug.LogError($"[BossIntegrationTest] P3阶段设置失败，当前阶段: {bossBlackboard.phase.Value}");
                }
            }
            
            yield return new WaitForSeconds(2f);
        }
        
        /// <summary>
        /// 测试失能状态
        /// </summary>
        private IEnumerator TestDisabledState()
        {
            _currentTestStep = 5;
            Debug.Log($"[BossIntegrationTest] 步骤 {_currentTestStep}: {_testSteps[_currentTestStep - 1]}");
            
            // 设置失能状态
            if (bossBlackboard)
            {
                bossBlackboard.ForceDisabledState();
                yield return new WaitForSeconds(0.5f);
                
                // 验证失能状态
                if (bossBlackboard.disabledOn.Value)
                {
                    Debug.Log("[BossIntegrationTest] ✓ 失能状态设置成功");
                }
                else
                {
                    Debug.LogError("[BossIntegrationTest] 失能状态设置失败");
                }
            }
            
            yield return new WaitForSeconds(2f);
        }
        
        /// <summary>
        /// 测试死亡状态
        /// </summary>
        private IEnumerator TestDeathState()
        {
            _currentTestStep = 6;
            Debug.Log($"[BossIntegrationTest] 步骤 {_currentTestStep}: {_testSteps[_currentTestStep - 1]}");
            
            // 设置死亡状态
            if (bossBlackboard)
            {
                bossBlackboard.hpPct.Value = 0f;
                yield return new WaitForSeconds(0.5f);
                
                // 验证死亡状态
                if (bossBlackboard.phase.Value == "Dead")
                {
                    Debug.Log("[BossIntegrationTest] ✓ 死亡状态设置成功");
                }
                else
                {
                    Debug.LogError($"[BossIntegrationTest] 死亡状态设置失败，当前阶段: {bossBlackboard.phase.Value}");
                }
            }
            
            yield return new WaitForSeconds(2f);
        }
        
        /// <summary>
        /// 测试传送门系统
        /// </summary>
        private IEnumerator TestPortalSystem()
        {
            _currentTestStep = 7;
            Debug.Log($"[BossIntegrationTest] 步骤 {_currentTestStep}: {_testSteps[_currentTestStep - 1]}");
            
            if (portalManager)
            {
                // 重置到正常状态
                if (bossBlackboard)
                {
                    bossBlackboard.ResetToNormalState();
                }
                
                yield return new WaitForSeconds(0.5f);
                
                // 测试生成传送门
                var portal1 = portalManager.StartPortalGeneration(PortalType.Ceiling, PortalColor.Blue);
                if (portal1 != null)
                {
                    Debug.Log("[BossIntegrationTest] ✓ 传送门生成成功");
                }
                else
                {
                    Debug.LogError("[BossIntegrationTest] 传送门生成失败");
                }
                
                yield return new WaitForSeconds(1f);
                
                // 测试生成第二个传送门
                var portal2 = portalManager.StartPortalGeneration(PortalType.Ground, PortalColor.Orange);
                if (portal2 != null)
                {
                    Debug.Log("[BossIntegrationTest] ✓ 第二个传送门生成成功");
                }
                else
                {
                    Debug.LogError("[BossIntegrationTest] 第二个传送门生成失败");
                }
                
                yield return new WaitForSeconds(2f);
                
                // 测试关闭传送门
                portalManager.CloseAllPortals();
                Debug.Log("[BossIntegrationTest] ✓ 传送门关闭成功");
            }
            
            yield return new WaitForSeconds(1f);
        }
        
        /// <summary>
        /// 测试技能权重
        /// </summary>
        private IEnumerator TestSkillWeights()
        {
            _currentTestStep = 8;
            Debug.Log($"[BossIntegrationTest] 步骤 {_currentTestStep}: {_testSteps[_currentTestStep - 1]}");
            
            if (bossPhaseWeights)
            {
                // 测试P1阶段权重
                if (bossBlackboard)
                {
                    bossBlackboard.ResetToNormalState();
                }
                
                yield return new WaitForSeconds(0.5f);
                
                string selectedSkill = bossPhaseWeights.SelectSkillByWeight();
                Debug.Log($"[BossIntegrationTest] ✓ P1阶段技能选择: {selectedSkill}");
                
                // 测试P2阶段权重
                if (bossBlackboard)
                {
                    bossBlackboard.ForceAngerState();
                }
                
                yield return new WaitForSeconds(0.5f);
                
                selectedSkill = bossPhaseWeights.SelectSkillByWeight();
                Debug.Log($"[BossIntegrationTest] ✓ P2阶段技能选择: {selectedSkill}");
                
                // 测试P3阶段权重
                if (bossBlackboard)
                {
                    bossBlackboard.ForceFearState();
                }
                
                yield return new WaitForSeconds(0.5f);
                
                selectedSkill = bossPhaseWeights.SelectSkillByWeight();
                Debug.Log($"[BossIntegrationTest] ✓ P3阶段技能选择: {selectedSkill}");
            }
            
            yield return new WaitForSeconds(1f);
        }
        
        /// <summary>
        /// 测试中断系统
        /// </summary>
        private IEnumerator TestInterruptSystem()
        {
            _currentTestStep = 9;
            Debug.Log($"[BossIntegrationTest] 步骤 {_currentTestStep}: {_testSteps[_currentTestStep - 1]}");
            
            if (bossInterruptSystem)
            {
                // 测试受击中断
                bossInterruptSystem.TriggerHitInterrupt();
                Debug.Log("[BossIntegrationTest] ✓ 受击中断触发成功");
                
                yield return new WaitForSeconds(1f);
                
                // 测试踉跄中断
                bossInterruptSystem.TriggerStaggerInterrupt();
                Debug.Log("[BossIntegrationTest] ✓ 踉跄中断触发成功");
                
                yield return new WaitForSeconds(1f);
                
                // 测试阶段变化中断
                if (bossBlackboard)
                {
                    bossBlackboard.ForceAngerState();
                    yield return new WaitForSeconds(0.5f);
                    Debug.Log("[BossIntegrationTest] ✓ 阶段变化中断触发成功");
                }
            }
            
            yield return new WaitForSeconds(1f);
        }
        
        /// <summary>
        /// 完成测试
        /// </summary>
        private void CompleteTest()
        {
            _currentTestStep = 10;
            _isTestRunning = false;
            _testStatus = "测试完成";
            
            float testDuration = Time.time - _testStartTime;
            Debug.Log($"[BossIntegrationTest] ✓ 集成测试完成，耗时: {testDuration:F2}秒");
            
            // 重置到正常状态
            if (bossBlackboard)
            {
                bossBlackboard.ResetToNormalState();
            }
        }
        
        #endregion
        
        #region 调试方法
        
        [Button("快速测试P1阶段")]
        public void QuickTestP1()
        {
            StartCoroutine(TestP1Phase());
        }
        
        [Button("快速测试P2阶段")]
        public void QuickTestP2()
        {
            StartCoroutine(TestP2Phase());
        }
        
        [Button("快速测试P3阶段")]
        public void QuickTestP3()
        {
            StartCoroutine(TestP3Phase());
        }
        
        [Button("测试传送门系统")]
        public void QuickTestPortals()
        {
            StartCoroutine(TestPortalSystem());
        }
        
        [Button("停止测试")]
        public void StopTest()
        {
            StopAllCoroutines();
            _isTestRunning = false;
            _testStatus = "测试停止";
            Debug.Log("[BossIntegrationTest] 测试已停止");
        }
        
        [Button("重置所有状态")]
        public void ResetAllStates()
        {
            if (bossBlackboard)
            {
                bossBlackboard.ResetToNormalState();
            }
            
            if (portalManager)
            {
                portalManager.CloseAllPortals();
            }
            
            if (bossInterruptSystem)
            {
                bossInterruptSystem.ClearAllInterrupts();
            }
            
            Debug.Log("[BossIntegrationTest] 所有状态已重置");
        }
        
        #endregion
        
        #region 调试显示
        
        private void OnDrawGizmosSelected()
        {
            // 在Scene视图中显示测试状态
            if (Application.isPlaying)
            {
                Vector3 position = transform.position + Vector3.up * 2f;
                string status = $"测试状态: {_testStatus}\n";
                status += $"当前步骤: {_currentTestStep}/10\n";
                status += $"步骤: {(_currentTestStep > 0 && _currentTestStep <= _testSteps.Length ? _testSteps[_currentTestStep - 1] : "无")}";
                
                UnityEditor.Handles.Label(position, status);
            }
        }
        
        #endregion
    }
}
