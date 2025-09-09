using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace Invector.vCharacterController.AI
{
    /// <summary>
    /// 技能权重配置
    /// </summary>
    [System.Serializable]
    public class SkillWeight
    {
        [Tooltip("技能名称")]
        public string skillName;
        
        [Tooltip("权重值 (0-100)")]
        [Range(0f, 100f)]
        public float weight;
        
        [Tooltip("是否启用")]
        public bool enabled = true;
        
        public SkillWeight(string name, float w, bool e = true)
        {
            skillName = name;
            weight = w;
            enabled = e;
        }
    }
    
    /// <summary>
    /// 阶段权重配置
    /// </summary>
    [System.Serializable]
    public class PhaseWeightConfig
    {
        [Header("P1 教学期权重")]
        [Tooltip("异能轰炸权重")]
        [Range(0f, 100f)]
        public float p1_bombardWeight = 20f;
        
        [Tooltip("侧墙直线投掷权重")]
        [Range(0f, 100f)]
        public float p1_wallThrowWeight = 25f;
        
        [Tooltip("地面环扫权重")]
        [Range(0f, 100f)]
        public float p1_tentacleWeight = 25f;
        
        [Tooltip("洪水权重")]
        [Range(0f, 100f)]
        public float p1_floodWeight = 20f;
        
        [Tooltip("漩涡权重")]
        [Range(0f, 100f)]
        public float p1_vortexWeight = 10f;
        
        [Header("P2 愤怒期权重")]
        [Tooltip("物理系总权重 (触手等)")]
        [Range(0f, 100f)]
        public float p2_physicalWeight = 50f;
        
        [Tooltip("异能轰炸权重")]
        [Range(0f, 100f)]
        public float p2_bombardWeight = 25f;
        
        [Tooltip("洪水权重")]
        [Range(0f, 100f)]
        public float p2_floodWeight = 25f;
        
        [Tooltip("吼叫权重")]
        [Range(0f, 100f)]
        public float p2_roarWeight = 15f;
        
        [Header("P3 恐惧期权重")]
        [Tooltip("漩涡权重 (大幅提升)")]
        [Range(0f, 100f)]
        public float p3_vortexWeight = 35f;
        
        [Tooltip("触手权重")]
        [Range(0f, 100f)]
        public float p3_tentacleWeight = 30f;
        
        [Tooltip("洪水权重")]
        [Range(0f, 100f)]
        public float p3_floodWeight = 35f;
        
        [Tooltip("是否禁用轰炸")]
        public bool p3_disableBombard = true;
    }
    
    /// <summary>
    /// Boss阶段权重管理器
    /// 根据当前阶段调整技能选择权重
    /// </summary>
    public class BossPhaseWeights : MonoBehaviour
    {
        [Header("权重配置")]
        [Tooltip("阶段权重配置")]
        public PhaseWeightConfig weightConfig;
        
        [Header("组件引用")]
        [Tooltip("Boss黑板变量")]
        public BossBlackboard bossBlackboard;
        
        [Header("调试")]
        [ShowInInspector, ReadOnly]
        private string _currentPhase;
        
        [ShowInInspector, ReadOnly]
        private Dictionary<string, float> _currentWeights = new Dictionary<string, float>();
        
        [ShowInInspector, ReadOnly]
        private float _totalWeight;
        
        // 技能名称常量
        private const string SKILL_BOMBARD = "bombard";
        private const string SKILL_WALL_THROW = "wallthrow";
        private const string SKILL_TENTACLE = "tentacle";
        private const string SKILL_FLOOD = "flood";
        private const string SKILL_VORTEX = "vortex";
        private const string SKILL_ROAR = "roar";
        
        #region Unity生命周期
        
        private void Start()
        {
            InitializeWeights();
        }
        
        private void Update()
        {
            UpdateWeights();
        }
        
        #endregion
        
        #region 初始化
        
        /// <summary>
        /// 初始化权重系统
        /// </summary>
        private void InitializeWeights()
        {
            if (!bossBlackboard)
            {
                bossBlackboard = GetComponent<BossBlackboard>();
            }
            
            if (!bossBlackboard)
            {
                Debug.LogError($"[BossPhaseWeights] 未找到BossBlackboard组件在 {gameObject.name}");
                enabled = false;
                return;
            }
            
            // 初始化权重配置
            if (weightConfig == null)
            {
                weightConfig = new PhaseWeightConfig();
            }
            
            Debug.Log("[BossPhaseWeights] 权重系统初始化完成");
        }
        
        #endregion
        
        #region 权重更新
        
        /// <summary>
        /// 更新权重
        /// </summary>
        private void UpdateWeights()
        {
            if (!bossBlackboard) return;
            
            string newPhase = bossBlackboard.phase.Value;
            if (_currentPhase != newPhase)
            {
                _currentPhase = newPhase;
                UpdatePhaseWeights();
            }
        }
        
        /// <summary>
        /// 更新阶段权重
        /// </summary>
        private void UpdatePhaseWeights()
        {
            _currentWeights.Clear();
            _totalWeight = 0f;
            
            switch (_currentPhase)
            {
                case "P1_Normal":
                    SetP1Weights();
                    break;
                case "P2_Anger":
                    SetP2Weights();
                    break;
                case "P3_Fear":
                    SetP3Weights();
                    break;
                default:
                    SetDefaultWeights();
                    break;
            }
            
            Debug.Log($"[BossPhaseWeights] 更新到阶段 {_currentPhase}，总权重: {_totalWeight}");
        }
        
        /// <summary>
        /// 设置P1教学期权重
        /// </summary>
        private void SetP1Weights()
        {
            _currentWeights[SKILL_BOMBARD] = weightConfig.p1_bombardWeight;
            _currentWeights[SKILL_WALL_THROW] = weightConfig.p1_wallThrowWeight;
            _currentWeights[SKILL_TENTACLE] = weightConfig.p1_tentacleWeight;
            _currentWeights[SKILL_FLOOD] = weightConfig.p1_floodWeight;
            _currentWeights[SKILL_VORTEX] = weightConfig.p1_vortexWeight;
            _currentWeights[SKILL_ROAR] = 0f; // P1不吼叫
            
            CalculateTotalWeight();
        }
        
        /// <summary>
        /// 设置P2愤怒期权重
        /// </summary>
        private void SetP2Weights()
        {
            // P2重点：物理攻击 + 异能二选一 + 偶尔吼叫
            _currentWeights[SKILL_TENTACLE] = weightConfig.p2_physicalWeight;
            _currentWeights[SKILL_BOMBARD] = weightConfig.p2_bombardWeight;
            _currentWeights[SKILL_FLOOD] = weightConfig.p2_floodWeight;
            _currentWeights[SKILL_ROAR] = weightConfig.p2_roarWeight;
            _currentWeights[SKILL_WALL_THROW] = 0f; // P2减少直线攻击
            _currentWeights[SKILL_VORTEX] = 0f; // P2减少漩涡
            
            CalculateTotalWeight();
        }
        
        /// <summary>
        /// 设置P3恐惧期权重
        /// </summary>
        private void SetP3Weights()
        {
            // P3重点：巨型橙门 + 漩涡 + 禁用轰炸
            _currentWeights[SKILL_VORTEX] = weightConfig.p3_vortexWeight;
            _currentWeights[SKILL_TENTACLE] = weightConfig.p3_tentacleWeight;
            _currentWeights[SKILL_FLOOD] = weightConfig.p3_floodWeight;
            _currentWeights[SKILL_BOMBARD] = weightConfig.p3_disableBombard ? 0f : 10f;
            _currentWeights[SKILL_WALL_THROW] = 0f; // P3减少直线攻击
            _currentWeights[SKILL_ROAR] = 0f; // P3不吼叫
            
            CalculateTotalWeight();
        }
        
        /// <summary>
        /// 设置默认权重
        /// </summary>
        private void SetDefaultWeights()
        {
            // 默认使用P1权重
            SetP1Weights();
        }
        
        /// <summary>
        /// 计算总权重
        /// </summary>
        private void CalculateTotalWeight()
        {
            _totalWeight = 0f;
            foreach (var weight in _currentWeights.Values)
            {
                _totalWeight += weight;
            }
        }
        
        #endregion
        
        #region 公共方法
        
        /// <summary>
        /// 根据权重随机选择技能
        /// </summary>
        /// <returns>选中的技能名称</returns>
        public string SelectSkillByWeight()
        {
            if (_totalWeight <= 0f)
            {
                Debug.LogWarning("[BossPhaseWeights] 总权重为0，无法选择技能");
                return SKILL_TENTACLE; // 默认返回触手攻击
            }
            
            float randomValue = Random.Range(0f, _totalWeight);
            float currentWeight = 0f;
            
            foreach (var kvp in _currentWeights)
            {
                currentWeight += kvp.Value;
                if (randomValue <= currentWeight)
                {
                    Debug.Log($"[BossPhaseWeights] 选择技能: {kvp.Key} (权重: {kvp.Value})");
                    return kvp.Key;
                }
            }
            
            // 如果出现异常，返回第一个可用技能
            foreach (var kvp in _currentWeights)
            {
                if (kvp.Value > 0f)
                {
                    return kvp.Key;
                }
            }
            
            return SKILL_TENTACLE; // 最后的默认值
        }
        
        /// <summary>
        /// 获取指定技能的权重
        /// </summary>
        /// <param name="skillName">技能名称</param>
        /// <returns>权重值</returns>
        public float GetSkillWeight(string skillName)
        {
            if (_currentWeights.ContainsKey(skillName))
            {
                return _currentWeights[skillName];
            }
            return 0f;
        }
        
        /// <summary>
        /// 检查技能是否可用
        /// </summary>
        /// <param name="skillName">技能名称</param>
        /// <returns>是否可用</returns>
        public bool IsSkillAvailable(string skillName)
        {
            if (!_currentWeights.ContainsKey(skillName))
                return false;
            
            if (_currentWeights[skillName] <= 0f)
                return false;
            
            // 检查冷却时间
            if (bossBlackboard && !bossBlackboard.IsSkillAvailable(skillName))
                return false;
            
            return true;
        }
        
        /// <summary>
        /// 获取当前阶段的所有可用技能
        /// </summary>
        /// <returns>可用技能列表</returns>
        public List<string> GetAvailableSkills()
        {
            var availableSkills = new List<string>();
            
            foreach (var kvp in _currentWeights)
            {
                if (IsSkillAvailable(kvp.Key))
                {
                    availableSkills.Add(kvp.Key);
                }
            }
            
            return availableSkills;
        }
        
        /// <summary>
        /// 获取当前阶段信息
        /// </summary>
        /// <returns>阶段信息字符串</returns>
        public string GetPhaseInfo()
        {
            var info = $"阶段: {_currentPhase}\n";
            info += $"总权重: {_totalWeight:F1}\n";
            info += "技能权重:\n";
            
            foreach (var kvp in _currentWeights)
            {
                if (kvp.Value > 0f)
                {
                    info += $"  {kvp.Key}: {kvp.Value:F1}\n";
                }
            }
            
            return info;
        }
        
        #endregion
        
        #region 调试方法
        
        [Button("测试技能选择")]
        public void TestSkillSelection()
        {
            string selectedSkill = SelectSkillByWeight();
            Debug.Log($"[BossPhaseWeights] 测试选择技能: {selectedSkill}");
        }
        
        [Button("显示当前权重")]
        public void ShowCurrentWeights()
        {
            Debug.Log($"[BossPhaseWeights] {GetPhaseInfo()}");
        }
        
        [Button("显示可用技能")]
        public void ShowAvailableSkills()
        {
            var skills = GetAvailableSkills();
            Debug.Log($"[BossPhaseWeights] 可用技能: {string.Join(", ", skills)}");
        }
        
        [Button("强制P1阶段")]
        public void ForceP1Phase()
        {
            if (bossBlackboard)
            {
                bossBlackboard.ForceAngerState(); // 这会触发阶段切换
                bossBlackboard.hpPct.Value = 1.0f; // 然后重置到P1
            }
        }
        
        [Button("强制P2阶段")]
        public void ForceP2Phase()
        {
            if (bossBlackboard)
            {
                bossBlackboard.ForceAngerState();
            }
        }
        
        [Button("强制P3阶段")]
        public void ForceP3Phase()
        {
            if (bossBlackboard)
            {
                bossBlackboard.ForceFearState();
            }
        }
        
        #endregion
        
        #region 调试显示
        
        private void OnDrawGizmosSelected()
        {
            // 在Scene视图中显示当前阶段信息
            if (Application.isPlaying)
            {
                Vector3 position = transform.position + Vector3.up * 3f;
                UnityEditor.Handles.Label(position, GetPhaseInfo());
            }
        }
        
        #endregion
    }
}
