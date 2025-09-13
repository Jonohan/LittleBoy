using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 居中Boss血条 - 确保文本完美居中
    /// </summary>
    public class CenteredBossHealthBar : MonoBehaviour
    {
        [Header("Canvas设置")]
        [Tooltip("现有的Canvas（如果不设置会自动查找）")]
        public Canvas targetCanvas;
        
        [Header("Boss设置")]
        [Tooltip("Boss Transform引用")]
        public Transform bossTransform;
        [Tooltip("Boss名称")]
        public string bossName = "Boss";
        
        [Header("血条UI设置")]
        [Tooltip("血条位置（相对于Canvas中心）")]
        public Vector2 healthBarPosition = new Vector2(0, 200);
        [Tooltip("血条大小")]
        public Vector2 healthBarSize = new Vector2(400, 30);
        [Tooltip("血条背景颜色")]
        public Color backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        [Tooltip("血条前景颜色")]
        public Color healthColor = new Color(1f, 0.2f, 0.2f, 1f);
        [Tooltip("血量不足时的颜色")]
        public Color lowHealthColor = Color.yellow;
        [Tooltip("血量不足阈值")]
        [Range(0f, 1f)]
        public float lowHealthThreshold = 0.3f;
        
        [Header("文本设置")]
        [Tooltip("Boss名称字体大小")]
        public int bossNameFontSize = 24;
        [Tooltip("血量文本字体大小")]
        public int healthTextFontSize = 18;
        [Tooltip("文本颜色")]
        public Color textColor = Color.white;
        
        [Header("显示控制")]
        [Tooltip("是否显示血量数值")]
        public bool showHealthNumbers = true;
        [Tooltip("是否显示Boss名称")]
        public bool showBossName = true;
        [Tooltip("是否在Boss死亡时隐藏血条")]
        public bool hideOnDeath = true;
        
        // 私有变量
        private GameObject healthBarUI;
        private Slider healthSlider;
        private Image healthFill;
        private Text bossNameText;
        private Text healthText;
        private Invector.vHealthController bossHealthController;
        private float maxHealth = 100f;
        private float currentHealth = 100f;
        private bool isInitialized = false;
        
        void Start()
        {
            CreateHealthBarUI();
        }
        
        void Update()
        {
            if (isInitialized && bossHealthController != null)
            {
                UpdateHealthDisplay();
            }
        }
        
        /// <summary>
        /// 创建血条UI
        /// </summary>
        private void CreateHealthBarUI()
        {
            // 获取Canvas
            if (targetCanvas == null)
            {
                targetCanvas = FindObjectOfType<Canvas>();
                if (targetCanvas == null)
                {
                    Debug.LogError("找不到Canvas！请确保场景中有Canvas组件。");
                    return;
                }
            }
            
            // 获取Boss血量控制器
            if (bossTransform != null)
            {
                bossHealthController = bossTransform.GetComponent<Invector.vHealthController>();
            }
            
            if (bossHealthController == null)
            {
                Debug.LogError("找不到Boss的vHealthController组件！");
                return;
            }
            
            // 创建血条UI
            healthBarUI = new GameObject("BossHealthBar");
            healthBarUI.transform.SetParent(targetCanvas.transform, false);
            
            // 设置RectTransform
            RectTransform rectTransform = healthBarUI.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 1f);
            rectTransform.anchorMax = new Vector2(0.5f, 1f);
            rectTransform.anchoredPosition = healthBarPosition;
            rectTransform.sizeDelta = healthBarSize;
            
            // 创建背景
            CreateBackground();
            
            // 创建血条Slider
            CreateHealthSlider();
            
            // 创建Boss名称文本
            if (showBossName)
            {
                CreateBossNameText();
            }
            
            // 创建血量文本
            if (showHealthNumbers)
            {
                CreateHealthText();
            }
            
            // 初始化血量
            maxHealth = bossHealthController.maxHealth;
            currentHealth = bossHealthController.currentHealth;
            
            if (healthSlider != null)
            {
                healthSlider.maxValue = maxHealth;
                healthSlider.value = currentHealth;
            }
            
            isInitialized = true;
            Debug.Log($"Boss血条UI创建完成: {bossName} (血量: {currentHealth}/{maxHealth})");
        }
        
        /// <summary>
        /// 创建背景
        /// </summary>
        private void CreateBackground()
        {
            GameObject background = new GameObject("Background");
            background.transform.SetParent(healthBarUI.transform, false);
            
            RectTransform bgRect = background.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            bgRect.anchoredPosition = Vector2.zero;
            
            Image bgImage = background.AddComponent<Image>();
            bgImage.color = backgroundColor;
            bgImage.sprite = CreateRoundedRectangleSprite();
            bgImage.type = Image.Type.Sliced;
        }
        
        /// <summary>
        /// 创建血条Slider
        /// </summary>
        private void CreateHealthSlider()
        {
            GameObject slider = new GameObject("HealthSlider");
            slider.transform.SetParent(healthBarUI.transform, false);
            
            RectTransform sliderRect = slider.AddComponent<RectTransform>();
            sliderRect.anchorMin = Vector2.zero;
            sliderRect.anchorMax = Vector2.one;
            sliderRect.sizeDelta = Vector2.zero;
            sliderRect.anchoredPosition = Vector2.zero;
            
            healthSlider = slider.AddComponent<Slider>();
            healthSlider.direction = Slider.Direction.LeftToRight;
            healthSlider.minValue = 0f;
            healthSlider.maxValue = 100f;
            healthSlider.value = 100f;
            
            // 创建Fill Area
            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(slider.transform, false);
            
            RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.sizeDelta = Vector2.zero;
            fillAreaRect.anchoredPosition = Vector2.zero;
            
            // 创建Fill
            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            
            RectTransform fillRect = fill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;
            fillRect.anchoredPosition = Vector2.zero;
            
            healthFill = fill.AddComponent<Image>();
            healthFill.color = healthColor;
            healthFill.sprite = CreateRoundedRectangleSprite();
            healthFill.type = Image.Type.Sliced;
            
            // 设置Slider的Fill Rect
            healthSlider.fillRect = fillRect;
        }
        
        /// <summary>
        /// 创建Boss名称文本（完美居中）
        /// </summary>
        private void CreateBossNameText()
        {
            GameObject bossNameObj = new GameObject("BossName");
            bossNameObj.transform.SetParent(healthBarUI.transform, false);
            
            RectTransform nameRect = bossNameObj.AddComponent<RectTransform>();
            // 使用中心锚点确保完美居中
            nameRect.anchorMin = new Vector2(0.5f, 1f);
            nameRect.anchorMax = new Vector2(0.5f, 1f);
            nameRect.sizeDelta = new Vector2(healthBarSize.x, 30);
            nameRect.anchoredPosition = new Vector2(0, 20);
            nameRect.pivot = new Vector2(0.5f, 0.5f); // 设置中心点
            
            bossNameText = bossNameObj.AddComponent<Text>();
            bossNameText.text = bossName;
            bossNameText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            bossNameText.fontSize = bossNameFontSize;
            bossNameText.color = textColor;
            bossNameText.alignment = TextAnchor.MiddleCenter;
            bossNameText.horizontalOverflow = HorizontalWrapMode.Overflow;
            bossNameText.verticalOverflow = VerticalWrapMode.Overflow;
        }
        
        /// <summary>
        /// 创建血量文本（完美居中）
        /// </summary>
        private void CreateHealthText()
        {
            GameObject healthTextObj = new GameObject("HealthText");
            healthTextObj.transform.SetParent(healthBarUI.transform, false);
            
            RectTransform healthRect = healthTextObj.AddComponent<RectTransform>();
            // 使用中心锚点确保完美居中
            healthRect.anchorMin = new Vector2(0.5f, 0.5f);
            healthRect.anchorMax = new Vector2(0.5f, 0.5f);
            healthRect.sizeDelta = new Vector2(healthBarSize.x, healthBarSize.y);
            healthRect.anchoredPosition = Vector2.zero;
            healthRect.pivot = new Vector2(0.5f, 0.5f); // 设置中心点
            
            healthText = healthTextObj.AddComponent<Text>();
            healthText.text = $"{currentHealth:F0} / {maxHealth:F0}";
            healthText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            healthText.fontSize = healthTextFontSize;
            healthText.color = textColor;
            healthText.alignment = TextAnchor.MiddleCenter;
            healthText.horizontalOverflow = HorizontalWrapMode.Overflow;
            healthText.verticalOverflow = VerticalWrapMode.Overflow;
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
                    healthBarUI.SetActive(false);
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
        /// 创建圆角矩形精灵
        /// </summary>
        /// <returns>圆角矩形精灵</returns>
        private Sprite CreateRoundedRectangleSprite()
        {
            // 创建一个简单的白色精灵作为占位符
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            
            return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
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
                    // 重新创建UI
                    if (healthBarUI != null)
                    {
                        DestroyImmediate(healthBarUI);
                    }
                    CreateHealthBarUI();
                }
            }
        }
        
        /// <summary>
        /// 显示/隐藏血条
        /// </summary>
        /// <param name="show">是否显示</param>
        public void ShowHealthBar(bool show)
        {
            if (healthBarUI != null)
            {
                healthBarUI.SetActive(show);
            }
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
        /// 重新创建血条UI
        /// </summary>
        [ContextMenu("重新创建血条UI")]
        public void RecreateHealthBarUI()
        {
            if (healthBarUI != null)
            {
                DestroyImmediate(healthBarUI);
            }
            CreateHealthBarUI();
        }
    }
}
