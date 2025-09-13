using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Boss血量控制器 - 管理Boss血条UI的显示和更新
    /// </summary>
    public class BossHealthController : MonoBehaviour
    {
        [Header("Boss设置")]
        [Tooltip("Boss Transform引用")]
        public Transform bossTransform;
        [Tooltip("Boss血量控制器")]
        public Invector.vHealthController bossHealthController;
        [Tooltip("Boss名称")]
        public string bossName = "Boss";
        
        [Header("UI设置")]
        [Tooltip("Boss血条UI组件")]
        public BossHealthBarUI bossHealthBarUI;
        [Tooltip("是否自动查找Boss")]
        public bool autoFindBoss = true;
        [Tooltip("Boss标签")]
        public string bossTag = "Boss";
        
        [Header("显示控制")]
        [Tooltip("是否在游戏开始时显示血条")]
        public bool showOnStart = true;
        [Tooltip("是否在Boss死亡时隐藏血条")]
        public bool hideOnDeath = true;
        [Tooltip("Boss死亡后隐藏延迟时间")]
        public float hideDelay = 2f;
        
        // 私有变量
        private bool isInitialized = false;
        private bool bossIsDead = false;
        
        void Start()
        {
            InitializeBossHealth();
        }
        
        void Update()
        {
            if (isInitialized && !bossIsDead)
            {
                CheckBossStatus();
            }
        }
        
        /// <summary>
        /// 初始化Boss血量系统
        /// </summary>
        private void InitializeBossHealth()
        {
            // 自动查找Boss
            if (autoFindBoss && bossTransform == null)
            {
                FindBoss();
            }
            
            // 获取血量控制器
            if (bossTransform != null && bossHealthController == null)
            {
                bossHealthController = bossTransform.GetComponent<Invector.vHealthController>();
            }
            
            // 获取血条UI组件
            if (bossHealthBarUI == null)
            {
                bossHealthBarUI = GetComponent<BossHealthBarUI>();
            }
            
            // 设置Boss引用
            if (bossHealthBarUI != null && bossTransform != null)
            {
                bossHealthBarUI.SetBoss(bossTransform);
                bossHealthBarUI.SetBossName(bossName);
                
                if (showOnStart)
                {
                    bossHealthBarUI.ShowHealthBar(true);
                }
                
                isInitialized = true;
                Debug.Log($"Boss血量控制器初始化完成: {bossName}");
            }
            else
            {
                Debug.LogError("Boss血量控制器初始化失败！请检查Boss引用和血条UI组件。");
            }
        }
        
        /// <summary>
        /// 自动查找Boss
        /// </summary>
        private void FindBoss()
        {
            // 通过标签查找
            GameObject bossObject = GameObject.FindGameObjectWithTag(bossTag);
            if (bossObject != null)
            {
                bossTransform = bossObject.transform;
                Debug.Log($"通过标签找到Boss: {bossObject.name}");
                return;
            }
            
            // 通过名称查找
            bossObject = GameObject.Find(bossName);
            if (bossObject != null)
            {
                bossTransform = bossObject.transform;
                Debug.Log($"通过名称找到Boss: {bossObject.name}");
                return;
            }
            
            // 查找包含"Boss"关键字的对象
            GameObject[] allObjects = FindObjectsOfType<GameObject>();
            foreach (GameObject obj in allObjects)
            {
                if (obj.name.ToLower().Contains("boss"))
                {
                    bossTransform = obj.transform;
                    Debug.Log($"通过关键字找到Boss: {obj.name}");
                    return;
                }
            }
            
            Debug.LogWarning("未找到Boss对象！请手动设置Boss引用。");
        }
        
        /// <summary>
        /// 检查Boss状态
        /// </summary>
        private void CheckBossStatus()
        {
            if (bossHealthController == null) return;
            
            // 检查Boss是否死亡
            if (bossHealthController.currentHealth <= 0 && !bossIsDead)
            {
                OnBossDeath();
            }
        }
        
        /// <summary>
        /// Boss死亡处理
        /// </summary>
        private void OnBossDeath()
        {
            bossIsDead = true;
            Debug.Log($"Boss {bossName} 死亡！");
            
            if (hideOnDeath && bossHealthBarUI != null)
            {
                // 延迟隐藏血条
                Invoke(nameof(HideHealthBarInternal), hideDelay);
            }
        }
        
        /// <summary>
        /// 隐藏血条（内部方法）
        /// </summary>
        private void HideHealthBarInternal()
        {
            if (bossHealthBarUI != null)
            {
                bossHealthBarUI.ShowHealthBar(false);
            }
        }
        
        /// <summary>
        /// 手动设置Boss
        /// </summary>
        /// <param name="boss">Boss Transform</param>
        public void SetBoss(Transform boss)
        {
            bossTransform = boss;
            
            if (boss != null)
            {
                bossHealthController = boss.GetComponent<Invector.vHealthController>();
                bossName = boss.name;
                
                if (bossHealthBarUI != null)
                {
                    bossHealthBarUI.SetBoss(boss);
                    bossHealthBarUI.SetBossName(bossName);
                }
                
                isInitialized = true;
                Debug.Log($"手动设置Boss: {bossName}");
            }
        }
        
        /// <summary>
        /// 显示血条
        /// </summary>
        public void ShowHealthBar()
        {
            if (bossHealthBarUI != null)
            {
                bossHealthBarUI.ShowHealthBar(true);
            }
        }
        
        /// <summary>
        /// 隐藏血条
        /// </summary>
        public void HideHealthBar()
        {
            if (bossHealthBarUI != null)
            {
                bossHealthBarUI.ShowHealthBar(false);
            }
        }
        
        /// <summary>
        /// 重置血条
        /// </summary>
        public void ResetHealthBar()
        {
            if (bossHealthBarUI != null)
            {
                bossHealthBarUI.ResetHealthBar();
                bossIsDead = false;
            }
        }
        
        /// <summary>
        /// 获取Boss血量百分比
        /// </summary>
        /// <returns>血量百分比 (0-1)</returns>
        public float GetBossHealthPercentage()
        {
            if (bossHealthController != null)
            {
                return bossHealthController.currentHealth / bossHealthController.maxHealth;
            }
            return 0f;
        }
        
        /// <summary>
        /// 检查Boss是否死亡
        /// </summary>
        /// <returns>是否死亡</returns>
        public bool IsBossDead()
        {
            return bossIsDead;
        }
        
        /// <summary>
        /// 重新初始化系统
        /// </summary>
        [ContextMenu("重新初始化")]
        public void Reinitialize()
        {
            isInitialized = false;
            bossIsDead = false;
            InitializeBossHealth();
        }
    }
}
