using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Boss血条设置助手 - 帮助快速创建Boss血条UI
    /// </summary>
    public class BossHealthBarSetup : MonoBehaviour
    {
        [Header("自动设置")]
        [Tooltip("点击此按钮自动创建Boss血条UI")]
        public bool autoSetup = false;
        
        [Header("Canvas设置")]
        [Tooltip("Canvas名称")]
        public string canvasName = "BossHealthCanvas";
        [Tooltip("Canvas排序层级")]
        public int canvasSortOrder = 100;
        
        [Header("血条设置")]
        [Tooltip("血条位置")]
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
        [Tooltip("Boss名称")]
        public string bossName = "Boss";
        [Tooltip("Boss名称字体大小")]
        public int bossNameFontSize = 24;
        [Tooltip("血量文本字体大小")]
        public int healthTextFontSize = 18;
        [Tooltip("文本颜色")]
        public Color textColor = Color.white;
        
        void Start()
        {
            if (autoSetup)
            {
                CreateBossHealthBarUI();
            }
        }
        
        /// <summary>
        /// 创建Boss血条UI
        /// </summary>
        [ContextMenu("创建Boss血条UI")]
        public void CreateBossHealthBarUI()
        {
            // 创建Canvas
            GameObject canvas = CreateCanvas();
            
            // 创建血条UI
            GameObject healthBarUI = CreateHealthBarUI(canvas);
            
            // 配置组件
            ConfigureComponents(healthBarUI);
            
            Debug.Log("Boss血条UI创建完成！");
        }
        
        /// <summary>
        /// 创建Canvas
        /// </summary>
        /// <returns>Canvas GameObject</returns>
        private GameObject CreateCanvas()
        {
            GameObject canvasGO = new GameObject(canvasName);
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            GraphicRaycaster raycaster = canvasGO.AddComponent<GraphicRaycaster>();
            
            // 设置为Screen Space - Overlay
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = canvasSortOrder;
            
            // 设置Canvas Scaler
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            
            Debug.Log($"已创建Canvas: {canvasName}");
            return canvasGO;
        }
        
        /// <summary>
        /// 创建血条UI
        /// </summary>
        /// <param name="parent">父对象</param>
        /// <returns>血条UI GameObject</returns>
        private GameObject CreateHealthBarUI(GameObject parent)
        {
            // 创建主容器
            GameObject healthBarContainer = new GameObject("BossHealthBar");
            healthBarContainer.transform.SetParent(parent.transform, false);
            
            RectTransform containerRect = healthBarContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 1f);
            containerRect.anchorMax = new Vector2(0.5f, 1f);
            containerRect.anchoredPosition = healthBarPosition;
            containerRect.sizeDelta = healthBarSize;
            
            // 添加BossHealthBarUI组件
            BossHealthBarUI healthBarUI = healthBarContainer.AddComponent<BossHealthBarUI>();
            
            // 创建背景
            GameObject background = new GameObject("Background");
            background.transform.SetParent(healthBarContainer.transform, false);
            
            RectTransform bgRect = background.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            bgRect.anchoredPosition = Vector2.zero;
            
            Image bgImage = background.AddComponent<Image>();
            bgImage.color = backgroundColor;
            bgImage.sprite = CreateRoundedRectangleSprite();
            bgImage.type = Image.Type.Sliced;
            
            // 创建血条Slider
            GameObject slider = new GameObject("HealthSlider");
            slider.transform.SetParent(healthBarContainer.transform, false);
            
            RectTransform sliderRect = slider.AddComponent<RectTransform>();
            sliderRect.anchorMin = Vector2.zero;
            sliderRect.anchorMax = Vector2.one;
            sliderRect.sizeDelta = Vector2.zero;
            sliderRect.anchoredPosition = Vector2.zero;
            
            Slider healthSlider = slider.AddComponent<Slider>();
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
            
            Image fillImage = fill.AddComponent<Image>();
            fillImage.color = healthColor;
            fillImage.sprite = CreateRoundedRectangleSprite();
            fillImage.type = Image.Type.Sliced;
            
            // 设置Slider的Fill Rect
            healthSlider.fillRect = fillRect;
            
            // 创建Boss名称文本
            GameObject bossNameText = new GameObject("BossName");
            bossNameText.transform.SetParent(healthBarContainer.transform, false);
            
            RectTransform nameRect = bossNameText.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.sizeDelta = new Vector2(0, 30);
            nameRect.anchoredPosition = new Vector2(0, 20);
            
            Text nameText = bossNameText.AddComponent<Text>();
            nameText.text = bossName;
            nameText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            nameText.fontSize = bossNameFontSize;
            nameText.color = textColor;
            nameText.alignment = TextAnchor.MiddleCenter;
            
            // 创建血量文本
            GameObject healthText = new GameObject("HealthText");
            healthText.transform.SetParent(healthBarContainer.transform, false);
            
            RectTransform healthRect = healthText.AddComponent<RectTransform>();
            healthRect.anchorMin = Vector2.zero;
            healthRect.anchorMax = Vector2.one;
            healthRect.sizeDelta = Vector2.zero;
            healthRect.anchoredPosition = Vector2.zero;
            
            Text healthTextComponent = healthText.AddComponent<Text>();
            healthTextComponent.text = "100 / 100";
            healthTextComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            healthTextComponent.fontSize = healthTextFontSize;
            healthTextComponent.color = textColor;
            healthTextComponent.alignment = TextAnchor.MiddleCenter;
            
            Debug.Log("已创建血条UI组件");
            return healthBarContainer;
        }
        
        /// <summary>
        /// 配置组件
        /// </summary>
        /// <param name="healthBarUI">血条UI GameObject</param>
        private void ConfigureComponents(GameObject healthBarUI)
        {
            BossHealthBarUI healthBarUIComponent = healthBarUI.GetComponent<BossHealthBarUI>();
            
            // 配置血条UI组件
            healthBarUIComponent.healthSlider = healthBarUI.GetComponentInChildren<Slider>();
            healthBarUIComponent.healthBackground = healthBarUI.transform.Find("Background").GetComponent<Image>();
            healthBarUIComponent.healthFill = healthBarUIComponent.healthSlider.fillRect.GetComponent<Image>();
            healthBarUIComponent.bossNameText = healthBarUI.transform.Find("BossName").GetComponent<Text>();
            healthBarUIComponent.healthText = healthBarUI.transform.Find("HealthText").GetComponent<Text>();
            
            healthBarUIComponent.bossName = bossName;
            healthBarUIComponent.healthColor = healthColor;
            healthBarUIComponent.backgroundColor = backgroundColor;
            healthBarUIComponent.lowHealthColor = lowHealthColor;
            healthBarUIComponent.lowHealthThreshold = lowHealthThreshold;
            healthBarUIComponent.showHealthNumbers = true;
            healthBarUIComponent.showBossName = true;
            healthBarUIComponent.hideOnDeath = true;
            
            // 添加BossHealthController组件
            BossHealthController controller = healthBarUI.AddComponent<BossHealthController>();
            controller.bossHealthBarUI = healthBarUIComponent;
            controller.bossName = bossName;
            controller.autoFindBoss = true;
            controller.bossTag = "Boss";
            controller.showOnStart = true;
            controller.hideOnDeath = true;
            controller.hideDelay = 2f;
            
            Debug.Log("组件配置完成");
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
            BossHealthController controller = FindObjectOfType<BossHealthController>();
            if (controller != null)
            {
                controller.SetBoss(boss);
            }
        }
    }
}
