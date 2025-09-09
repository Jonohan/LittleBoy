using UnityEngine;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using Sirenix.OdinInspector;
using System.Linq;
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
        [Header("技能配置")]
        [UnityEngine.Tooltip("技能名称")]
        public string skillName = "Unknown";
        
        [UnityEngine.Tooltip("技能冷却时间")]
        public float cooldownTime = 5f;
        
        [UnityEngine.Tooltip("前摇时间")]
        public float telegraphTime = 2f;
        
        [UnityEngine.Tooltip("后摇时间")]
        public float postAttackTime = 1f;
        
        [Header("传送门配置")]
        [UnityEngine.Tooltip("传送门类型")]
        public PortalType portalType = PortalType.Ceiling;
        
        [UnityEngine.Tooltip("传送门颜色")]
        public PortalColor portalColor = PortalColor.Blue;
        
        [Header("组件引用")]
        [UnityEngine.Tooltip("传送门管理器")]
        public SharedGameObject portalManager;
        
        [UnityEngine.Tooltip("Boss黑板变量")]
        public SharedGameObject bossBlackboard;
        
        [UnityEngine.Tooltip("Boss AI控制器")]
        public SharedGameObject bossAI;
        
        // 私有变量
        protected PortalManager _portalManager;
        protected BossBlackboard _bossBlackboard;
        protected NonHumanoidBossAI _bossAI;
        protected PortalData _currentPortal;
        protected float _skillStartTime;
        protected SkillPhase _currentPhase;
        
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
            _skillStartTime = Time.time;
            _currentPhase = SkillPhase.SpawnPortal;
            
            Debug.Log($"[{skillName}] 开始执行技能");
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
            if (!_portalManager)
            {
                Debug.LogError($"[{skillName}] 传送门管理器未找到");
                return TaskStatus.Failure;
            }
            
            // 生成传送门
            _currentPortal = _portalManager.SpawnPortal(portalType, portalColor);
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
            }
            
            _currentPhase = SkillPhase.Telegraph;
            return TaskStatus.Running;
        }
        
        /// <summary>
        /// 处理前摇阶段
        /// </summary>
        protected virtual TaskStatus HandleTelegraph()
        {
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
            // 执行技能效果
            ExecuteSkillEffect();
            
            _currentPhase = SkillPhase.PostAttack;
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
            
            // 清理传送门（如果需要）
            if (ShouldClosePortalAfterSkill())
            {
                _portalManager?.ClosePortal(_currentPortal);
            }
            
            Debug.Log($"[{skillName}] 技能执行完成");
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
        
        /// <summary>
        /// 技能结束后是否关闭传送门
        /// </summary>
        /// <returns>是否关闭</returns>
        protected abstract bool ShouldClosePortalAfterSkill();
        
        #endregion
        
        #region 辅助方法
        
        /// <summary>
        /// 初始化组件引用
        /// </summary>
        private void InitializeComponents()
        {
            if (portalManager.Value)
                _portalManager = portalManager.Value.GetComponent<PortalManager>();
            
            if (bossBlackboard.Value)
                _bossBlackboard = bossBlackboard.Value.GetComponent<BossBlackboard>();
            
            if (bossAI.Value)
                _bossAI = bossAI.Value.GetComponent<NonHumanoidBossAI>();
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
            skillName = "bombard";
            portalType = PortalType.Ceiling;
            portalColor = PortalColor.Blue;
            cooldownTime = 8f;
            telegraphTime = 3f;
            postAttackTime = 2f;
            
            base.OnStart();
        }
        
        protected override void PlayTelegraphEffects()
        {
            // 播放全场散点预警特效
            if (_currentPortal?.portalObject)
            {
                // 这里可以播放预警特效，比如地面上的红色圆圈
                Debug.Log($"[{skillName}] 播放全场散点预警特效");
            }
        }
        
        protected override void ExecuteSkillEffect()
        {
            if (!projectilePrefab || !_currentPortal?.portalObject)
            {
                Debug.LogError($"[{skillName}] 投掷物预制体或传送门未找到");
                return;
            }
            
            // 生成投掷物
            for (int i = 0; i < projectileCount; i++)
            {
                // 随机位置
                Vector3 randomPos = _currentPortal.slot.position + 
                    new Vector3(
                        Random.Range(-bombardRadius, bombardRadius),
                        0,
                        Random.Range(-bombardRadius, bombardRadius)
                    );
                
                // 生成投掷物
                GameObject projectile = UnityEngine.Object.Instantiate(projectilePrefab, _currentPortal.slot.position, Quaternion.identity);
                
                // 设置投掷物方向
                Vector3 direction = (randomPos - _currentPortal.slot.position).normalized;
                projectile.GetComponent<Rigidbody>()?.AddForce(direction * projectileSpeed, ForceMode.VelocityChange);
                
                // 设置伤害
                var damageComponent = projectile.GetComponent<vObjectDamage>();
                if (damageComponent)
                {
                    damageComponent.damage.damageValue = damage;
                }
            }
            
            Debug.Log($"[{skillName}] 执行异能轰炸，生成 {projectileCount} 个投掷物");
        }
        
        protected override void PlayPostAttackEffects()
        {
            // 播放后摇动画
            if (_bossAI?.animator)
            {
                _bossAI.animator.SetTrigger("PostAttack");
            }
            
            Debug.Log($"[{skillName}] 播放后摇特效");
        }
        
        protected override bool ShouldClosePortalAfterSkill()
        {
            return true; // 轰炸后关闭传送门
        }
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
            skillName = "wallthrow";
            portalType = PortalType.WallLeft; // 默认左墙，可通过参数调整
            portalColor = PortalColor.Blue;
            cooldownTime = 6f;
            telegraphTime = 2f;
            postAttackTime = 1f;
            
            base.OnStart();
        }
        
        protected override void PlayTelegraphEffects()
        {
            // 播放直线预警特效
            if (_currentPortal?.portalObject)
            {
                // 这里可以播放直线预警特效
                Debug.Log($"[{skillName}] 播放直线预警特效");
            }
        }
        
        protected override void ExecuteSkillEffect()
        {
            if (!projectilePrefab || !_currentPortal?.portalObject)
            {
                Debug.LogError($"[{skillName}] 投掷物预制体或传送门未找到");
                return;
            }
            
            // 生成投掷物
            GameObject projectile = UnityEngine.Object.Instantiate(projectilePrefab, _currentPortal.slot.position, Quaternion.identity);
            
            // 设置投掷方向
            Vector3 direction = _currentPortal.slot.TransformDirection(throwDirection).normalized;
            projectile.GetComponent<Rigidbody>()?.AddForce(direction * projectileSpeed, ForceMode.VelocityChange);
            
            // 设置伤害
            var damageComponent = projectile.GetComponent<vObjectDamage>();
            if (damageComponent)
            {
                damageComponent.damage.damageValue = damage;
            }
            
            Debug.Log($"[{skillName}] 执行直线投掷攻击");
        }
        
        protected override void PlayPostAttackEffects()
        {
            // 播放后摇动画
            if (_bossAI?.animator)
            {
                _bossAI.animator.SetTrigger("PostAttack");
            }
            
            Debug.Log($"[{skillName}] 播放后摇特效");
        }
        
        protected override bool ShouldClosePortalAfterSkill()
        {
            return true; // 投掷后关闭传送门
        }
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
            skillName = "tentacle";
            portalType = PortalType.Ground; // 触手从地面伸出
            portalColor = PortalColor.Orange;
            cooldownTime = 7f;
            telegraphTime = 2.5f;
            postAttackTime = 1.5f;
            
            base.OnStart();
        }
        
        protected override void PlayTelegraphEffects()
        {
            // 播放扇形预警特效
            if (_currentPortal?.portalObject)
            {
                // 这里可以播放扇形预警特效
                Debug.Log($"[{skillName}] 播放扇形预警特效");
            }
        }
        
        protected override void ExecuteSkillEffect()
        {
            if (!_currentPortal?.portalObject)
            {
                Debug.LogError($"[{skillName}] 传送门未找到");
                return;
            }
            
            // 执行扇形攻击
            Vector3 center = _currentPortal.slot.position;
            Vector3 forward = _currentPortal.slot.forward;
            
            // 检测范围内的玩家
            Collider[] colliders = Physics.OverlapSphere(center, swipeRadius);
            foreach (var collider in colliders)
            {
                if (collider.CompareTag("Player"))
                {
                    Vector3 direction = (collider.transform.position - center).normalized;
                    float angle = Vector3.Angle(forward, direction);
                    
                    // 检查是否在扇形范围内
                    if (angle <= swipeAngle / 2f)
                    {
                        // 造成伤害
                        var health = collider.GetComponent<vIHealthController>();
                        if (health != null)
                        {
                            var vDamage = new vDamage((int)damage);
                            health.TakeDamage(vDamage);
                        }
                        
                        // 击退效果
                        var rigidbody = collider.GetComponent<Rigidbody>();
                        if (rigidbody)
                        {
                            rigidbody.AddForce(direction * knockbackForce, ForceMode.Impulse);
                        }
                    }
                }
            }
            
            Debug.Log($"[{skillName}] 执行触手横扫攻击");
        }
        
        protected override void PlayPostAttackEffects()
        {
            // 播放后摇动画 - 触手收回
            if (_bossAI?.animator)
            {
                _bossAI.animator.SetTrigger("TentacleRetract");
            }
            
            Debug.Log($"[{skillName}] 播放触手收回动画");
        }
        
        protected override bool ShouldClosePortalAfterSkill()
        {
            return false; // 触手攻击后保持传送门开启
        }
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
            skillName = "flood";
            portalType = PortalType.Ground;
            portalColor = PortalColor.Orange;
            cooldownTime = 10f;
            telegraphTime = 3f;
            postAttackTime = 1f;
            
            base.OnStart();
        }
        
        protected override void PlayTelegraphEffects()
        {
            // 播放水位预警特效
            if (_currentPortal?.portalObject)
            {
                // 这里可以播放水位预警特效
                Debug.Log($"[{skillName}] 播放水位预警特效");
            }
        }
        
        protected override void ExecuteSkillEffect()
        {
            if (!floodPrefab || !_currentPortal?.portalObject)
            {
                Debug.LogError($"[{skillName}] 洪水预制体或传送门未找到");
                return;
            }
            
            // 生成洪水
            _floodObject = UnityEngine.Object.Instantiate(floodPrefab, _currentPortal.slot.position, Quaternion.identity);
            _floodStartTime = Time.time;
            
            Debug.Log($"[{skillName}] 执行洪水攻击");
        }
        
        protected override void PlayPostAttackEffects()
        {
            // 洪水持续期间的处理
            if (_floodObject && Time.time - _floodStartTime < floodDuration)
            {
                // 水位上升逻辑
                float currentHeight = Mathf.Lerp(0, maxWaterHeight, (Time.time - _floodStartTime) / floodDuration);
                _floodObject.transform.localScale = new Vector3(1, currentHeight, 1);
                
                // 检测水位中的玩家
                CheckFloodDamage();
            }
            else if (_floodObject)
            {
                // 洪水结束
                UnityEngine.Object.Destroy(_floodObject);
                _floodObject = null;
            }
        }
        
        protected override bool ShouldClosePortalAfterSkill()
        {
            return false; // 洪水期间保持传送门开启
        }
        
        /// <summary>
        /// 检查洪水伤害
        /// </summary>
        private void CheckFloodDamage()
        {
            if (!_floodObject) return;
            
            // 检测洪水范围内的玩家
            Collider[] colliders = Physics.OverlapBox(
                _floodObject.transform.position + Vector3.up * _floodObject.transform.localScale.y / 2f,
                new Vector3(10f, _floodObject.transform.localScale.y / 2f, 10f)
            );
            
            foreach (var collider in colliders)
            {
                if (collider.CompareTag("Player"))
                {
                    // 造成持续伤害
                    var health = collider.GetComponent<vIHealthController>();
                    if (health != null)
                    {
                        var vDamage = new vDamage((int)(damage * Time.deltaTime));
                        health.TakeDamage(vDamage);
                    }
                }
            }
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
            skillName = "vortex";
            portalType = PortalType.Ground;
            portalColor = PortalColor.Orange;
            cooldownTime = 12f;
            telegraphTime = 2f;
            postAttackTime = 3f;
            
            base.OnStart();
        }
        
        protected override void PlayTelegraphEffects()
        {
            // 播放漩涡预警特效
            if (_currentPortal?.portalObject)
            {
                // 这里可以播放漩涡预警特效
                Debug.Log($"[{skillName}] 播放漩涡预警特效");
            }
        }
        
        protected override void ExecuteSkillEffect()
        {
            if (!vortexPrefab || !_currentPortal?.portalObject)
            {
                Debug.LogError($"[{skillName}] 漩涡预制体或传送门未找到");
                return;
            }
            
            // 检查是否有天花板传送门
            var ceilingPortals = _portalManager?.GetActivePortalsByType(PortalType.Ceiling);
            _ceilingPortal = (ceilingPortals != null && ceilingPortals.Count > 0) ? ceilingPortals[0] : null;
            if (_ceilingPortal == null)
            {
                Debug.LogWarning($"[{skillName}] 没有天花板传送门，无法执行漩涡发射");
                return;
            }
            
            // 生成漩涡
            _vortexObject = UnityEngine.Object.Instantiate(vortexPrefab, _currentPortal.slot.position, Quaternion.identity);
            
            Debug.Log($"[{skillName}] 执行漩涡发射攻击");
        }
        
        protected override void PlayPostAttackEffects()
        {
            if (!_vortexObject || _ceilingPortal == null) return;
            
            // 执行吸入和抛出逻辑
            Vector3 vortexCenter = _currentPortal.slot.position;
            Vector3 ceilingCenter = _ceilingPortal.slot.position;
            
            // 检测范围内的玩家
            Collider[] colliders = Physics.OverlapSphere(vortexCenter, suckRadius);
            foreach (var collider in colliders)
            {
                if (collider.CompareTag("Player"))
                {
                    var rigidbody = collider.GetComponent<Rigidbody>();
                    if (rigidbody)
                    {
                        // 吸入阶段
                        Vector3 suckDirection = (vortexCenter - collider.transform.position).normalized;
                        rigidbody.AddForce(suckDirection * suckForce, ForceMode.Force);
                        
                        // 如果玩家接近漩涡中心，执行抛出
                        if (Vector3.Distance(collider.transform.position, vortexCenter) < 1f)
                        {
                            Vector3 launchDirection = (ceilingCenter - vortexCenter).normalized;
                            rigidbody.AddForce(launchDirection * launchForce, ForceMode.Impulse);
                            
                            // 造成伤害
                            var health = collider.GetComponent<vIHealthController>();
                            if (health != null)
                            {
                                var vDamage = new vDamage((int)damage);
                                health.TakeDamage(vDamage);
                            }
                        }
                    }
                }
            }
        }
        
        protected override bool ShouldClosePortalAfterSkill()
        {
            return true; // 漩涡发射后关闭传送门
        }
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
            skillName = "roar";
            portalType = PortalType.None; // 吼叫不需要传送门
            portalColor = PortalColor.Blue;
            cooldownTime = 15f;
            telegraphTime = 1f;
            postAttackTime = 2f;
            
            base.OnStart();
        }
        
        protected override void PlayTelegraphEffects()
        {
            // 播放吼叫预警特效
            Debug.Log($"[{skillName}] 播放吼叫预警特效");
        }
        
        protected override void ExecuteSkillEffect()
        {
            // 播放吼叫动画
            if (_bossAI?.animator)
            {
                _bossAI.animator.SetTrigger(roarTrigger);
            }
            
            // 播放吼叫音效
            if (roarSound && _bossAI?.GetComponent<AudioSource>())
            {
                _bossAI.GetComponent<AudioSource>().PlayOneShot(roarSound);
            }
            
            // 执行击退效果
            Vector3 bossCenter = _bossAI.transform.position;
            Collider[] colliders = Physics.OverlapSphere(bossCenter, knockbackRadius);
            foreach (var collider in colliders)
            {
                if (collider.CompareTag("Player"))
                {
                    Vector3 direction = (collider.transform.position - bossCenter).normalized;
                    var rigidbody = collider.GetComponent<Rigidbody>();
                    if (rigidbody)
                    {
                        rigidbody.AddForce(direction * knockbackForce, ForceMode.Impulse);
                    }
                }
            }
            
            Debug.Log($"[{skillName}] 执行Boss吼叫");
        }
        
        protected override void PlayPostAttackEffects()
        {
            // 吼叫后摇动画
            if (_bossAI?.animator)
            {
                _bossAI.animator.SetTrigger("PostRoar");
            }
            
            Debug.Log($"[{skillName}] 播放吼叫后摇特效");
        }
        
        protected override bool ShouldClosePortalAfterSkill()
        {
            return false; // 吼叫不需要传送门
        }
    }
    
    #endregion
}
