using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Boss血条UI - 固定在屏幕Canvas上显示Boss血量
    /// </summary>
    public class BossHealthBarUI : MonoBehaviour
    {
        [Header("UI组件")]
        [Tooltip("血条Slider组件")]
        public Slider healthSlider;
        [Tooltip("血条背景图片")]
        public Image healthBackground;
        [Tooltip("血条填充图片")]
        public Image healthFill;
        [Tooltip("Boss名称文本")]
        public Text bossNameText;
        [Tooltip("血量数值文本")]
        public Text healthText;
        
        [Header("Boss设置")]
        [Tooltip("Boss名称")]
        public string bossName = "Boss";
        [Tooltip("Boss Transform引用")]
        public Transform bossTransform;
        [Tooltip("Boss血量控制器")]
        public Invector.vHealthController bossHealthController;
        
        [Header("血条样式")]
        [Tooltip("血条颜色")]
        public Color healthColor = Color.red;
        [Tooltip("血条背景颜色")]
        public Color backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        [Tooltip("血量不足时的颜色")]
        public Color lowHealthColor = Color.yellow;
        [Tooltip("血量不足阈值（百分比）")]
        [Range(0f, 1f)]
        public float lowHealthThreshold = 0.3f;
        
        [Header("显示设置")]
        [Tooltip("是否显示血量数值")]
        public bool showHealthNumbers = true;
        [Tooltip("是否显示Boss名称")]
        public bool showBossName = true;
        [Tooltip("是否在Boss死亡时隐藏血条")]
        public bool hideOnDeath = true;
        
        // 私有变量
        private float maxHealth = 100f;
        private float currentHealth = 100f;
        private bool isInitialized = false;
        
        void Start()
        {
            InitializeHealthBar();
        }
        
        void Update()
        {
            if (isInitialized && bossHealthController != null)
            {
                UpdateHealthDisplay();
            }
        }
        
        /// <summary>
        /// 初始化血条
        /// </summary>
        private void InitializeHealthBar()
        {
            // 自动获取组件
            if (healthSlider == null)
                healthSlider = GetComponentInChildren<Slider>();
            
            if (healthBackground == null)
                healthBackground = GetComponent<Image>();
            
            if (healthFill == null && healthSlider != null)
                healthFill = healthSlider.fillRect.GetComponent<Image>();
            
            if (bossNameText == null)
                bossNameText = transform.Find("BossName")?.GetComponent<Text>();
            
            if (healthText == null)
                healthText = transform.Find("HealthText")?.GetComponent<Text>();
            
            // 设置Boss名称
            if (showBossName && bossNameText != null)
            {
                bossNameText.text = bossName;
            }
            else if (bossNameText != null)
            {
                bossNameText.gameObject.SetActive(false);
            }
            
            // 设置血条样式
            if (healthBackground != null)
                healthBackground.color = backgroundColor;
            
            if (healthFill != null)
                healthFill.color = healthColor;
            
            // 初始化血量
            if (bossHealthController != null)
            {
                maxHealth = bossHealthController.maxHealth;
                currentHealth = bossHealthController.currentHealth;
                
                if (healthSlider != null)
                {
                    healthSlider.maxValue = maxHealth;
                    healthSlider.value = currentHealth;
                }
                
                isInitialized = true;
                Debug.Log($"Boss血条初始化完成: {bossName} (血量: {currentHealth}/{maxHealth})");
            }
            else
            {
                Debug.LogWarning("Boss血量控制器未设置！");
            }
        }
        
        /// <summary>
        /// 更新血量显示
        /// </summary>
        private void UpdateHealthDisplay()
        {
            float newHealth = bossHealthController.currentHealth;
            
            if (newHealth != currentHealth)
            {
                currentHealth = newHealth;
                
                // 更新血条
                if (healthSlider != null)
                {
                    healthSlider.value = currentHealth;
                }
                
                // 更新血量文本
                if (showHealthNumbers && healthText != null)
                {
                    healthText.text = $"{currentHealth:F0} / {maxHealth:F0}";
                }
                
                // 更新血条颜色
                UpdateHealthBarColor();
                
                // 检查是否死亡
                if (currentHealth <= 0 && hideOnDeath)
                {
                    gameObject.SetActive(false);
                }
            }
        }
        
        /// <summary>
        /// 更新血条颜色
        /// </summary>
        private void UpdateHealthBarColor()
        {
            if (healthFill == null) return;
            
            float healthPercentage = currentHealth / maxHealth;
            
            if (healthPercentage <= lowHealthThreshold)
            {
                healthFill.color = lowHealthColor;
            }
            else
            {
                healthFill.color = healthColor;
            }
        }
        
        /// <summary>
        /// 设置Boss引用
        /// </summary>
        /// <param name="boss">Boss Transform</param>
        public void SetBoss(Transform boss)
        {
            bossTransform = boss;
            
            if (boss != null)
            {
                bossHealthController = boss.GetComponent<Invector.vHealthController>();
                if (bossHealthController == null)
                {
                    Debug.LogError($"Boss {boss.name} 没有找到vHealthController组件！");
                }
                else
                {
                    InitializeHealthBar();
                }
            }
        }
        
        /// <summary>
        /// 设置Boss名称
        /// </summary>
        /// <param name="name">Boss名称</param>
        public void SetBossName(string name)
        {
            bossName = name;
            if (bossNameText != null)
            {
                bossNameText.text = bossName;
            }
        }
        
        /// <summary>
        /// 显示/隐藏血条
        /// </summary>
        /// <param name="show">是否显示</param>
        public void ShowHealthBar(bool show)
        {
            gameObject.SetActive(show);
        }
        
        /// <summary>
        /// 获取当前血量百分比
        /// </summary>
        /// <returns>血量百分比 (0-1)</returns>
        public float GetHealthPercentage()
        {
            return maxHealth > 0 ? currentHealth / maxHealth : 0f;
        }
        
        /// <summary>
        /// 手动更新血量（用于测试）
        /// </summary>
        /// <param name="health">血量值</param>
        [ContextMenu("测试血量更新")]
        public void TestUpdateHealth(float health)
        {
            if (healthSlider != null)
            {
                healthSlider.value = health;
                currentHealth = health;
                UpdateHealthBarColor();
                
                if (showHealthNumbers && healthText != null)
                {
                    healthText.text = $"{currentHealth:F0} / {maxHealth:F0}";
                }
            }
        }
        
        /// <summary>
        /// 重置血条
        /// </summary>
        [ContextMenu("重置血条")]
        public void ResetHealthBar()
        {
            if (bossHealthController != null)
            {
                maxHealth = bossHealthController.maxHealth;
                currentHealth = bossHealthController.currentHealth;
                
                if (healthSlider != null)
                {
                    healthSlider.maxValue = maxHealth;
                    healthSlider.value = currentHealth;
                }
                
                UpdateHealthBarColor();
                
                if (showHealthNumbers && healthText != null)
                {
                    healthText.text = $"{currentHealth:F0} / {maxHealth:F0}";
                }
                
                gameObject.SetActive(true);
            }
        }
    }
}
