using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Boss血条控制器 - 直接挂载在BossHealthBar GameObject上
    /// </summary>
    public class BossHealthBarController : MonoBehaviour
    {
        [Header("Boss设置")]
        [Tooltip("Boss Transform引用")]
        public Transform bossTransform;
        [Tooltip("Boss名称")]
        public string bossName = "Boss";
        
        [Header("血条样式")]
        [Tooltip("血条颜色")]
        public Color healthColor = Color.red;
        [Tooltip("血条背景颜色")]
        public Color backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        [Tooltip("血量不足时的颜色")]
        public Color lowHealthColor = Color.yellow;
        [Tooltip("血量不足阈值")]
        [Range(0f, 1f)]
        public float lowHealthThreshold = 0.3f;
        
        [Header("显示控制")]
        [Tooltip("是否显示血量数值")]
        public bool showHealthNumbers = true;
        [Tooltip("是否在Boss死亡时隐藏血条")]
        public bool hideOnDeath = true;
        
        // 组件引用
        private Slider healthSlider;
        private Image healthFill;
        private Image backgroundImage;
        private Text bossNameText;
        private Text healthText;
        private Invector.vCharacterController.AI.NonHumanoidBossAI bossAI;
        
        // 血量数据
        private float maxHealth = 100f;
        private float currentHealth = 100f;
        private bool isInitialized = false;
        
        void Start()
        {
            InitializeComponents();
        }
        
        void Update()
        {
            if (isInitialized && bossAI != null)
            {
                UpdateHealthDisplay();
            }
        }
        
        /// <summary>
        /// 初始化组件
        /// </summary>
        private void InitializeComponents()
        {
            // 获取Slider组件
            healthSlider = GetComponentInChildren<Slider>();
            if (healthSlider == null)
            {
                Debug.LogError("找不到HealthSlider组件！");
                return;
            }
            
            // 获取Fill组件
            healthFill = healthSlider.fillRect?.GetComponent<Image>();
            if (healthFill == null)
            {
                Debug.LogError("找不到HealthFill组件！");
                return;
            }
            
            // 获取背景组件
            backgroundImage = transform.Find("Background")?.GetComponent<Image>();
            if (backgroundImage == null)
            {
                Debug.LogError("找不到Background组件！");
                return;
            }

            bossNameText = transform.Find("BossName")?.GetComponent<Text>();
            
            // 获取血量文本（如果没有，创建一个）
            healthText = transform.Find("HealthText")?.GetComponent<Text>();
            if (healthText == null && showHealthNumbers)
            {
                CreateHealthText();
            }
            
            // 设置样式
            SetupStyles();
            
            // 获取Boss AI控制器
            if (bossTransform != null)
            {
            
                bossAI = bossTransform.GetComponent<Invector.vCharacterController.AI.NonHumanoidBossAI>();
                if (bossAI != null)
                {                                
            
                    maxHealth = bossAI.maxHealth;
                    currentHealth = bossAI.currentHealth;
                    
                    // 初始化血条
                    healthSlider.maxValue = maxHealth;
                    healthSlider.value = currentHealth;
                    
                    isInitialized = true;
                    Debug.Log($"Boss血条初始化完成: {bossName} (血量: {currentHealth}/{maxHealth})");
                }
                else
                {
                    Debug.LogError($"Boss {bossTransform.name} 没有找到NonHumanoidBossAI组件！");
                }
            }
            else
            {
                Debug.LogError("Boss Transform未设置！");
            }
        }
        
        /// <summary>
        /// 设置样式
        /// </summary>
        private void SetupStyles()
        {
            // 设置背景颜色
            if (backgroundImage != null)
            {
                backgroundImage.color = backgroundColor;
            }
            
            // 设置血条颜色
            if (healthFill != null)
            {
                healthFill.color = healthColor;
            }
            
            // Boss名称文本保持原样，不修改
            
            // 设置血量文本
            if (healthText != null)
            {
                healthText.fontSize = 18;
                healthText.color = Color.white;
                healthText.alignment = TextAnchor.MiddleCenter;
                healthText.gameObject.SetActive(showHealthNumbers);
            }
        }
        
        /// <summary>
        /// 创建血量文本
        /// </summary>
        private void CreateHealthText()
        {
            GameObject healthTextObj = new GameObject("HealthText");
            healthTextObj.transform.SetParent(transform, false);
            
            RectTransform healthRect = healthTextObj.AddComponent<RectTransform>();
            healthRect.anchorMin = Vector2.zero;
            healthRect.anchorMax = Vector2.one;
            healthRect.sizeDelta = Vector2.zero;
            healthRect.anchoredPosition = Vector2.zero;
            
            healthText = healthTextObj.AddComponent<Text>();
            healthText.text = $"{currentHealth:F0} / {maxHealth:F0}";
            healthText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            healthText.fontSize = 18;
            healthText.color = Color.white;
            healthText.alignment = TextAnchor.MiddleCenter;
            healthText.gameObject.SetActive(showHealthNumbers);
        }
        
        /// <summary>
        /// 更新血量显示
        /// </summary>
        private void UpdateHealthDisplay()
        {
            float newHealth = bossAI.currentHealth;
            
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
                bossAI = boss.GetComponent<Invector.vCharacterController.AI.NonHumanoidBossAI>();
                if (bossAI != null)
                {
                    maxHealth = bossAI.maxHealth;
                    currentHealth = bossAI.currentHealth;
                    
                    if (healthSlider != null)
                    {
                        healthSlider.maxValue = maxHealth;
                        healthSlider.value = currentHealth;
                    }
                    
                    if (healthText != null)
                    {
                        healthText.text = $"{currentHealth:F0} / {maxHealth:F0}";
                    }
                    
                    isInitialized = true;
                    Debug.Log($"设置Boss: {boss.name}");
                }
                else
                {
                    Debug.LogError($"Boss {boss.name} 没有找到NonHumanoidBossAI组件！");
                }
            }
        }
        
        /// <summary>
        /// 设置Boss名称（保持现有BossName对象不变）
        /// </summary>
        /// <param name="name">Boss名称</param>
        public void SetBossName(string name)
        {
            bossName = name;
            // 不修改现有的BossName文本对象
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
        /// 重新初始化组件
        /// </summary>
        [ContextMenu("重新初始化")]
        public void Reinitialize()
        {
            isInitialized = false;
            InitializeComponents();
        }
    }
}
