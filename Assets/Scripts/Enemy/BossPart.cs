using UnityEngine;
using Invector;
using Invector.vCharacterController;
using Sirenix.OdinInspector;

namespace Invector.vCharacterController.AI
{
    /// <summary>
    /// Boss部件系统
    /// 独立的Boss部件，可以有自己的攻击判定和受击判定
    /// 作为Boss的一部分，但可以独立激活/非激活
    /// 支持手动选择Body part Collider，不强制要求组件自身有Collider
    /// </summary>
    public class BossPart : MonoBehaviour, vIDamageReceiver
    {
        [Header("Boss部件配置")]
        [Tooltip("Boss对象（直接拖拽Boss GameObject到这里）")]
        public GameObject bossObject;
        
        [Tooltip("所属的Boss对象（自动从bossObject获取BossBlackboard）")]
        public BossBlackboard bossBlackboard;
        
        [Tooltip("部件名称，用于调试和识别")]
        public string partName = "BossPart";
        
        [Header("激活状态")]
        [Tooltip("部件是否激活")]
        public bool isActive = true;
        
        [Tooltip("激活时是否显示")]
        public bool showWhenActive = true;
        
        [Tooltip("非激活时是否隐藏")]
        public bool hideWhenInactive = true;
        
        [Header("攻击判定")]
        [Tooltip("是否具有攻击能力")]
        public bool canAttack = true;
        
        [Tooltip("攻击伤害值")]
        public float attackDamage = 20f;
        
        [Tooltip("攻击判定碰撞器")]
        public Collider attackCollider;
        
        [Tooltip("攻击判定是否激活")]
        public bool attackColliderActive = false;
        
        [Header("受击判定")]
        [Tooltip("受击判定碰撞器（Body part Collider）")]
        public Collider damageCollider;
        
        [Tooltip("是否自动设置受击碰撞器（如果为true，将使用组件自身的碰撞器）")]
        public bool autoSetDamageCollider = true;
        
        [Tooltip("伤害倍数")]
        public float damageMultiplier = 1f;
        
        [Tooltip("是否可以被玩家攻击")]
        public bool canReceiveDamage = true;
        
        [Header("视觉效果")]
        [Tooltip("激活时的视觉效果")]
        public GameObject[] activeEffects;
        
        [Tooltip("非激活时的视觉效果")]
        public GameObject[] inactiveEffects;
        
        [Tooltip("受击时的视觉效果")]
        public GameObject[] hitEffects;
        
        [Header("音效")]
        [Tooltip("激活音效")]
        public AudioClip activateSound;
        
        [Tooltip("非激活音效")]
        public AudioClip deactivateSound;
        
        [Tooltip("受击音效")]
        public AudioClip hitSound;
        
        [Tooltip("攻击音效")]
        public AudioClip attackSound;
        
        [Header("事件")]
        [Tooltip("开始受击事件")]
        public OnReceiveDamage onStartReceiveDamage = new OnReceiveDamage();
        
        [Tooltip("受击事件")]
        public OnReceiveDamage onReceiveDamage = new OnReceiveDamage();
        
        // 实现vIDamageReceiver接口的事件属性
        OnReceiveDamage vIDamageReceiver.onStartReceiveDamage => onStartReceiveDamage;
        OnReceiveDamage vIDamageReceiver.onReceiveDamage => onReceiveDamage;
        
        [Header("调试信息")]
        [ShowInInspector, ReadOnly]
        private bool _isCurrentlyActive;
        
        [ShowInInspector, ReadOnly]
        private bool _isAttackColliderActive;
        
        [ShowInInspector, ReadOnly]
        private float _lastHitTime;
        
        // 私有变量
        private AudioSource _audioSource;
        private Renderer[] _renderers;
        private Collider[] _colliders;
        private bool _initialized = false;
        
        #region Unity生命周期
        
        private void Awake()
        {
            InitializeComponents();
        }
        
        private void Start()
        {
            InitializeBossPart();
        }
        
        private void Update()
        {
            UpdateVisualState();
        }
        
        #endregion
        
        #region 初始化
        
        /// <summary>
        /// 初始化组件
        /// </summary>
        private void InitializeComponents()
        {
            // 获取或添加AudioSource
            _audioSource = GetComponent<AudioSource>();
            if (!_audioSource)
                _audioSource = gameObject.AddComponent<AudioSource>();
            
            // 获取所有渲染器
            _renderers = GetComponentsInChildren<Renderer>();
            
            // 获取所有碰撞器
            _colliders = GetComponentsInChildren<Collider>();
            
            // 设置攻击碰撞器（如果组件自身有Collider）
            if (!attackCollider)
            {
                var selfCollider = GetComponent<Collider>();
                if (selfCollider)
                    attackCollider = selfCollider;
            }
            
            // 设置受击碰撞器（Body part Collider）
            if (autoSetDamageCollider && !damageCollider)
            {
                var selfCollider = GetComponent<Collider>();
                if (selfCollider)
                    damageCollider = selfCollider;
            }
            
            // 确保攻击碰撞器是触发器
            if (attackCollider)
            {
                attackCollider.isTrigger = true;
                attackCollider.enabled = false; // 默认关闭
            }
            
            // 确保受击碰撞器不是触发器
            if (damageCollider)
            {
                damageCollider.isTrigger = false;
                damageCollider.enabled = true; // 默认开启
            }
        }
        
        /// <summary>
        /// 初始化Boss部件
        /// </summary>
        private void InitializeBossPart()
        {
            // 优先从拖拽的Boss对象获取BossBlackboard
            if (bossObject && !bossBlackboard)
            {
                bossBlackboard = bossObject.GetComponent<BossBlackboard>();
            }
            
            // 如果还没找到，使用自动查找
            if (!bossBlackboard)
            {
                FindBossBlackboard();
            }
            
            // 设置初始状态
            SetActive(isActive);
            
            _initialized = true;
        }
        
        /// <summary>
        /// 查找BossBlackboard
        /// </summary>
        private void FindBossBlackboard()
        {
            // 首先尝试在父对象中查找
            bossBlackboard = GetComponentInParent<BossBlackboard>();
            
            // 如果没找到，直接通过"enemy"标签查找Boss对象
            if (!bossBlackboard)
            {
                var bossObj = GameObject.FindGameObjectWithTag("enemy");
                if (bossObj)
                {
                    bossBlackboard = bossObj.GetComponent<BossBlackboard>();
                }
            }
            
            // 如果还是没找到，尝试查找场景中所有的BossBlackboard（备用方案）
            if (!bossBlackboard)
            {
                var allBosses = FindObjectsOfType<BossBlackboard>();
                if (allBosses.Length > 0)
                {
                    bossBlackboard = allBosses[0]; // 使用第一个找到的Boss
                }
            }
            
            if (!bossBlackboard)
            {
                Debug.LogWarning($"[BossPart] {partName} 未找到BossBlackboard组件，请拖拽Boss对象到bossObject字段或确保场景中有Tag为'enemy'的Boss对象");
            }
        }
        
        #endregion
        
        #region 激活状态管理
        
        /// <summary>
        /// 设置部件激活状态
        /// </summary>
        /// <param name="active">是否激活</param>
        public void SetActive(bool active)
        {
            if (_isCurrentlyActive == active) return;
            
            _isCurrentlyActive = active;
            isActive = active;
            
            // 更新视觉效果
            UpdateVisualState();
            
            // 播放音效
            if (active && activateSound)
                PlaySound(activateSound);
            else if (!active && deactivateSound)
                PlaySound(deactivateSound);
        }
        
        /// <summary>
        /// 切换激活状态
        /// </summary>
        public void ToggleActive()
        {
            SetActive(!_isCurrentlyActive);
        }
        
        /// <summary>
        /// 更新视觉效果
        /// </summary>
        private void UpdateVisualState()
        {
            if (!_initialized) return;
            
            // 更新渲染器显示状态
            if (_renderers != null)
            {
                foreach (var renderer in _renderers)
                {
                    if (renderer)
                    {
                        if (_isCurrentlyActive)
                        {
                            renderer.enabled = showWhenActive;
                        }
                        else
                        {
                            renderer.enabled = !hideWhenInactive;
                        }
                    }
                }
            }
            
            // 更新激活效果
            if (activeEffects != null)
            {
                foreach (var effect in activeEffects)
                {
                    if (effect)
                        effect.SetActive(_isCurrentlyActive);
                }
            }
            
            // 更新非激活效果
            if (inactiveEffects != null)
            {
                foreach (var effect in inactiveEffects)
                {
                    if (effect)
                        effect.SetActive(!_isCurrentlyActive);
                }
            }
        }
        
        #endregion
        
        #region 攻击系统
        
        /// <summary>
        /// 激活攻击判定
        /// </summary>
        public void ActivateAttack()
        {
            if (!canAttack || !_isCurrentlyActive) return;
            
            if (attackCollider)
            {
                attackCollider.enabled = true;
                attackColliderActive = true;
                _isAttackColliderActive = true;
            }
            
            // 播放攻击音效
            if (attackSound)
                PlaySound(attackSound);
        }
        
        /// <summary>
        /// 关闭攻击判定
        /// </summary>
        public void DeactivateAttack()
        {
            if (attackCollider)
            {
                attackCollider.enabled = false;
                attackColliderActive = false;
                _isAttackColliderActive = false;
            }
        }
        
        /// <summary>
        /// 攻击碰撞检测
        /// </summary>
        /// <param name="other">碰撞对象</param>
        private void OnTriggerEnter(Collider other)
        {
            if (!_isAttackColliderActive || !canAttack) return;
            
            // 检查是否是玩家
            if (other.CompareTag("Player"))
            {
                DealDamageToPlayer(other.gameObject);
            }
        }
        
        /// <summary>
        /// 对玩家造成伤害
        /// </summary>
        /// <param name="player">玩家对象</param>
        private void DealDamageToPlayer(GameObject player)
        {
            var healthController = player.GetComponent<vHealthController>();
            if (healthController)
            {
                var damage = new vDamage((int)attackDamage);
                damage.sender = transform;
                damage.receiver = player.transform;
                damage.hitPosition = transform.position;
                damage.damageType = "BossPartAttack";
                
                healthController.TakeDamage(damage);
            }
        }
        
        #endregion
        
        #region 受击系统
        
        /// <summary>
        /// 实现vIDamageReceiver接口
        /// </summary>
        /// <param name="damage">伤害信息</param>
        public void TakeDamage(vDamage damage)
        {
            if (!canReceiveDamage || !_isCurrentlyActive) return;
            
            // 触发开始受击事件
            onStartReceiveDamage.Invoke(damage);
            
            // 应用伤害倍数
            var modifiedDamage = new vDamage(damage);
            modifiedDamage.damageValue *= damageMultiplier;
            
            // 将伤害传递给Boss
            if (bossBlackboard)
            {
                var bossAI = bossBlackboard.GetComponent<NonHumanoidBossAI>();
                if (bossAI)
                {
                    bossAI.TakeDamage(modifiedDamage);
                }
            }
            
            // 播放受击效果
            PlayHitEffects();
            
            // 播放受击音效
            if (hitSound)
                PlaySound(hitSound);
            
            // 触发受击事件
            onReceiveDamage.Invoke(modifiedDamage);
            
            _lastHitTime = Time.time;
        }
        
        /// <summary>
        /// 播放受击效果
        /// </summary>
        private void PlayHitEffects()
        {
            if (hitEffects != null)
            {
                foreach (var effect in hitEffects)
                {
                    if (effect)
                    {
                        var instance = Instantiate(effect, transform.position, transform.rotation);
                        Destroy(instance, 5f); // 5秒后销毁
                    }
                }
            }
        }
        
        #endregion
        
        #region 音效系统
        
        /// <summary>
        /// 播放音效
        /// </summary>
        /// <param name="clip">音效片段</param>
        private void PlaySound(AudioClip clip)
        {
            if (_audioSource && clip)
            {
                _audioSource.PlayOneShot(clip);
            }
        }
        
        #endregion
        
        #region 公共方法
        
        /// <summary>
        /// 获取部件是否激活
        /// </summary>
        /// <returns>是否激活</returns>
        public bool IsActive()
        {
            return _isCurrentlyActive;
        }
        
        /// <summary>
        /// 获取攻击判定是否激活
        /// </summary>
        /// <returns>攻击判定是否激活</returns>
        public bool IsAttackActive()
        {
            return _isAttackColliderActive;
        }
        
        /// <summary>
        /// 设置攻击伤害
        /// </summary>
        /// <param name="damage">伤害值</param>
        public void SetAttackDamage(float damage)
        {
            attackDamage = damage;
        }
        
        /// <summary>
        /// 设置伤害倍数
        /// </summary>
        /// <param name="multiplier">倍数</param>
        public void SetDamageMultiplier(float multiplier)
        {
            damageMultiplier = multiplier;
        }
        
        #endregion
        
        #region 调试方法
        
        [Button("激活部件")]
        public void DebugActivate()
        {
            SetActive(true);
        }
        
        [Button("非激活部件")]
        public void DebugDeactivate()
        {
            SetActive(false);
        }
        
        [Button("激活攻击")]
        public void DebugActivateAttack()
        {
            ActivateAttack();
        }
        
        [Button("关闭攻击")]
        public void DebugDeactivateAttack()
        {
            DeactivateAttack();
        }
        
        [Button("测试受击")]
        public void DebugTestHit()
        {
            var testDamage = new vDamage(10);
            testDamage.sender = transform;
            testDamage.receiver = transform;
            testDamage.hitPosition = transform.position;
            testDamage.damageType = "Test";
            TakeDamage(testDamage);
        }
        
        [Button("自动设置Body Part Collider")]
        public void DebugAutoSetDamageCollider()
        {
            autoSetDamageCollider = true;
            var selfCollider = GetComponent<Collider>();
            if (selfCollider)
            {
                damageCollider = selfCollider;
            }
            else
            {
                Debug.LogWarning($"[BossPart] {partName} 组件自身没有Collider，请手动设置Body Part Collider");
            }
        }
        
        [Button("清除Body Part Collider")]
        public void DebugClearDamageCollider()
        {
            damageCollider = null;
        }
        
        [Button("验证Body Part Collider设置")]
        public void DebugValidateDamageCollider()
        {
            if (damageCollider == null)
            {
                Debug.LogWarning($"[BossPart] {partName} Body Part Collider 未设置！");
                return;
            }
            
            Debug.Log($"[BossPart] {partName} Body Part Collider 信息:");
            Debug.Log($"  - 碰撞器名称: {damageCollider.name}");
            Debug.Log($"  - 是否为触发器: {damageCollider.isTrigger}");
            Debug.Log($"  - 是否启用: {damageCollider.enabled}");
            Debug.Log($"  - 碰撞器类型: {damageCollider.GetType().Name}");
            
            if (damageCollider.isTrigger)
            {
                Debug.LogWarning($"[BossPart] {partName} Body Part Collider 是触发器，这可能导致受击判定问题！");
            }
        }
        
        [Button("自动查找Boss对象")]
        public void DebugAutoFindBoss()
        {
            var bossObj = GameObject.FindGameObjectWithTag("enemy");
            if (bossObj)
            {
                bossObject = bossObj;
                bossBlackboard = bossObj.GetComponent<BossBlackboard>();
            }
            else
            {
                Debug.LogWarning($"[BossPart] {partName} 未找到Tag为'enemy'的Boss对象");
            }
        }
        
        [Button("验证Boss连接")]
        public void DebugValidateBossConnection()
        {
            if (bossObject == null)
            {
                Debug.LogWarning($"[BossPart] {partName} Boss对象未设置！请拖拽Boss对象到bossObject字段");
                return;
            }
            
            if (bossBlackboard == null)
            {
                Debug.LogWarning($"[BossPart] {partName} BossBlackboard未找到！Boss对象可能没有BossBlackboard组件");
                return;
            }
            
            Debug.Log($"[BossPart] {partName} Boss连接正常");
        }
        
        #endregion
    }
}
