using UnityEngine;
using Sirenix.OdinInspector;

namespace Invector.vCharacterController.AI
{
    /// <summary>
    /// Boss设置指南
    /// 提供Boss行为树系统的设置指导和示例配置
    /// </summary>
    [CreateAssetMenu(fileName = "BossSetupGuide", menuName = "Boss/Setup Guide")]
    public class BossSetupGuide : ScriptableObject
    {
        [Header("设置步骤")]
        [TextArea(5, 10)]
        public string setupSteps = @"
Boss行为树系统设置步骤：

1. 在Boss GameObject上添加以下组件：
   - NonHumanoidBossAI (现有)
   - BossBlackboard
   - BossBehaviorTree
   - PortalManager
   - BossPhaseWeights
   - BossInterruptSystem
   - BossIntegrationTest (可选，用于测试)

2. 配置BossBlackboard：
   - 设置血量阈值 (angerThreshold: 0.7, fearThreshold: 0.4)
   - 配置传送门插槽 (arenaSlots)
   - 设置最大传送门数量 (maxPortals: 2)

3. 配置PortalManager：
   - 设置传送门预制体 (portalPrefab, giantPortalPrefab)
   - 配置各类型插槽 (ceilingSlots, wallLeftSlots, wallRightSlots, groundSlots)
   - 设置传送门持续时间 (portalDuration: 10f)

4. 配置BossPhaseWeights：
   - 调整各阶段技能权重
   - P1: 教学期，均衡权重
   - P2: 愤怒期，物理攻击权重提升
   - P3: 恐惧期，漩涡权重提升，禁用轰炸

5. 配置BossInterruptSystem：
   - 设置动画触发参数
   - 配置中断优先级
   - 设置中断冷却时间

6. 在Behavior Designer中创建行为树：
   - 使用Priority Selector作为根节点
   - 按优先级添加条件节点和子树
   - 配置各阶段子树

7. 测试系统：
   - 使用BossIntegrationTest进行集成测试
   - 验证各阶段切换和技能执行
";

        [Header("示例配置")]
        [Tooltip("P1阶段技能权重配置")]
        public PhaseWeightConfig p1Weights = new PhaseWeightConfig
        {
            p1_bombardWeight = 20f,
            p1_wallThrowWeight = 25f,
            p1_tentacleWeight = 25f,
            p1_floodWeight = 20f,
            p1_vortexWeight = 10f
        };

        [Tooltip("P2阶段技能权重配置")]
        public PhaseWeightConfig p2Weights = new PhaseWeightConfig
        {
            p2_physicalWeight = 50f,
            p2_bombardWeight = 25f,
            p2_floodWeight = 25f,
            p2_roarWeight = 15f
        };

        [Tooltip("P3阶段技能权重配置")]
        public PhaseWeightConfig p3Weights = new PhaseWeightConfig
        {
            p3_vortexWeight = 35f,
            p3_tentacleWeight = 30f,
            p3_floodWeight = 35f,
            p3_disableBombard = true
        };

        [Header("传送门插槽示例")]
        [Tooltip("天花板插槽示例")]
        public Transform[] exampleCeilingSlots;

        [Tooltip("左墙插槽示例")]
        public Transform[] exampleWallLeftSlots;

        [Tooltip("右墙插槽示例")]
        public Transform[] exampleWallRightSlots;

        [Tooltip("地面插槽示例")]
        public Transform[] exampleGroundSlots;

        [Header("技能配置示例")]
        [Tooltip("异能轰炸配置")]
        public SkillConfigExample bombardConfig = new SkillConfigExample
        {
            skillName = "bombard",
            cooldownTime = 8f,
            telegraphTime = 3f,
            postAttackTime = 2f,
            portalType = PortalType.Ceiling,
            portalColor = PortalColor.Blue
        };

        [Tooltip("触手横扫配置")]
        public SkillConfigExample tentacleConfig = new SkillConfigExample
        {
            skillName = "tentacle",
            cooldownTime = 7f,
            telegraphTime = 2.5f,
            postAttackTime = 1.5f,
            portalType = PortalType.Ground,
            portalColor = PortalColor.Orange
        };

        [Tooltip("漩涡发射配置")]
        public SkillConfigExample vortexConfig = new SkillConfigExample
        {
            skillName = "vortex",
            cooldownTime = 12f,
            telegraphTime = 2f,
            postAttackTime = 3f,
            portalType = PortalType.Ground,
            portalColor = PortalColor.Orange
        };

        [Header("行为树结构示例")]
        [TextArea(10, 15)]
        public string behaviorTreeStructure = @"
Root (Priority Selector)
├── IsBossDead? → PlayBossDeath
├── IsBossDisabled? → LoopShiverPanic
├── IsBossFearful? → Subtree_P3_Fear
├── IsBossAngry? → Subtree_P2_Anger
└── Subtree_P1_Normal

Subtree_P1_Normal (Selector)
├── Random Weighted Selector
│   ├── CeilingEnergyBombard (20%)
│   ├── WallLineThrow (25%)
│   ├── TentacleSwipe (25%)
│   ├── GroundFlood (20%)
│   └── VortexLaunch (10%)

Subtree_P2_Anger (Selector)
├── Random Weighted Selector
│   ├── TentacleSwipe (50%)
│   ├── CeilingEnergyBombard (25%)
│   ├── GroundFlood (25%)
│   └── BossRoar (15%)

Subtree_P3_Fear (Selector)
├── Random Weighted Selector
│   ├── VortexLaunch (35%)
│   ├── TentacleSwipe (30%)
│   └── GroundFlood (35%)
";

        [Header("调试建议")]
        [TextArea(5, 10)]
        public string debuggingTips = @"
调试建议：

1. 使用BossIntegrationTest进行系统测试
2. 在Inspector中观察黑板变量变化
3. 使用Scene视图的Gizmos查看传送门插槽
4. 检查Animator状态机是否正确配置
5. 验证技能预制体和特效是否正确设置
6. 测试玩家体型变化对Boss状态的影响

常见问题：
- 传送门不生成：检查插槽配置和预制体设置
- 阶段不切换：检查血量阈值和黑板变量更新
- 技能不执行：检查冷却时间和权重配置
- 动画不播放：检查Animator参数名称和触发条件
";

        [System.Serializable]
        public class SkillConfigExample
        {
            public string skillName;
            public float cooldownTime;
            public float telegraphTime;
            public float postAttackTime;
            public PortalType portalType;
            public PortalColor portalColor;
        }

        [Button("应用示例配置")]
        public void ApplyExampleConfig()
        {
            Debug.Log("[BossSetupGuide] 示例配置已准备，请手动应用到Boss组件中");
        }

        [Button("显示设置检查清单")]
        public void ShowSetupChecklist()
        {
            Debug.Log(@"
Boss设置检查清单：

□ NonHumanoidBossAI 组件已添加
□ BossBlackboard 组件已添加并配置
□ BossBehaviorTree 组件已添加
□ PortalManager 组件已添加并配置插槽
□ BossPhaseWeights 组件已添加并配置权重
□ BossInterruptSystem 组件已添加
□ 传送门预制体已设置
□ 传送门插槽已配置
□ 动画参数已设置
□ 技能预制体已准备
□ 行为树已在Behavior Designer中创建
□ 集成测试已通过
");
        }
    }
}
