using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Invector.vCharacterController.AI;

namespace Xuwu.UI
{
    /// <summary>
    /// 传送门指示器UI - 在屏幕边缘显示视野外的传送门
    /// </summary>
    public class PortalIndicatorUI : MonoBehaviour
    {
        [Header("UI组件")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Transform playerTransform;
        
        [Header("指示器配置")]
        [SerializeField] private GameObject indicatorPrefab;
        [SerializeField] private float screenEdgeOffset = 50f;
        [SerializeField] private float indicatorSize = 40f;
        [SerializeField] private float visibilityMargin = 100f; // 视野边缘的宽松范围
        
        [Header("颜色配置")]
        [SerializeField] private Color bluePortalColor = Color.blue;
        [SerializeField] private Color orangePortalColor = new Color(1f, 0.5f, 0f); // 橙色
        [SerializeField] private Color giantOrangePortalColor = new Color(1f, 0.3f, 0f); // 深橙色
        
        [Header("动画配置")]
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float pulseScale = 1.2f;
        
        // 组件引用
        private PortalManager _portalManager;
        private List<PortalIndicator> _activeIndicators = new List<PortalIndicator>();
        
        // 指示器类
        private class PortalIndicator
        {
            public GameObject indicatorObject;
            public Image indicatorImage;
            public PortalData portalData;
            public bool isVisible;
            
            public PortalIndicator(GameObject obj, Image img, PortalData data)
            {
                indicatorObject = obj;
                indicatorImage = img;
                portalData = data;
                isVisible = false;
            }
        }
        
        private void Start()
        {
            InitializeComponents();
        }
        
        private void Update()
        {
            UpdateIndicators();
        }
        
        /// <summary>
        /// 初始化组件
        /// </summary>
        private void InitializeComponents()
        {
            // 获取PortalManager
            _portalManager = FindObjectOfType<PortalManager>();
            if (_portalManager == null)
            {
                Debug.LogError("[PortalIndicatorUI] 未找到PortalManager组件！");
                return;
            }
            
            // 获取Canvas
            if (canvas == null)
            {
                canvas = GetComponent<Canvas>();
                if (canvas == null)
                {
                    Debug.LogError("[PortalIndicatorUI] 未找到Canvas组件！");
                    return;
                }
            }
            
            // 获取玩家相机
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
                if (playerCamera == null)
                {
                    playerCamera = FindObjectOfType<Camera>();
                }
                if (playerCamera == null)
                {
                    Debug.LogError("[PortalIndicatorUI] 未找到玩家相机！");
                    return;
                }
            }
            
            // 获取玩家Transform
            if (playerTransform == null)
            {
                // 尝试通过标签查找玩家
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    playerTransform = player.transform;
                }
                else
                {
                    // 如果没找到，尝试查找CharacterSizeController
                    var sizeController = FindObjectOfType<Xuwu.Character.CharacterSizeController>();
                    if (sizeController != null)
                    {
                        playerTransform = sizeController.transform;
                    }
                    else
                    {
                        Debug.LogError("[PortalIndicatorUI] 未找到玩家Transform！");
                        return;
                    }
                }
            }
            
            // 创建指示器预制体（如果没有指定）
            if (indicatorPrefab == null)
            {
                CreateDefaultIndicatorPrefab();
            }
        }
        
        /// <summary>
        /// 创建默认指示器预制体
        /// </summary>
        private void CreateDefaultIndicatorPrefab()
        {
            // 创建指示器GameObject
            GameObject indicator = new GameObject("PortalIndicator");
            indicator.transform.SetParent(canvas.transform, false);
            
            // 添加Image组件
            Image image = indicator.AddComponent<Image>();
            image.color = Color.white;
            
            // 设置RectTransform
            RectTransform rectTransform = indicator.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(indicatorSize, indicatorSize);
            
            // 创建简单的圆形指示器
            CreateCircleSprite(image);
            
            indicatorPrefab = indicator;
            indicator.SetActive(false); // 默认隐藏
        }
        
        /// <summary>
        /// 创建圆形精灵
        /// </summary>
        private void CreateCircleSprite(Image image)
        {
            // 创建一个简单的圆形纹理
            Texture2D circleTexture = new Texture2D(64, 64);
            Color[] pixels = new Color[64 * 64];
            
            Vector2 center = new Vector2(32, 32);
            float radius = 30f;
            
            for (int x = 0; x < 64; x++)
            {
                for (int y = 0; y < 64; y++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    if (distance <= radius)
                    {
                        pixels[y * 64 + x] = Color.white;
                    }
                    else
                    {
                        pixels[y * 64 + x] = Color.clear;
                    }
                }
            }
            
            circleTexture.SetPixels(pixels);
            circleTexture.Apply();
            
            Sprite circleSprite = Sprite.Create(circleTexture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
            image.sprite = circleSprite;
        }
        
        /// <summary>
        /// 更新指示器
        /// </summary>
        private void UpdateIndicators()
        {
            if (_portalManager == null || playerCamera == null) return;
            
            // 获取所有活跃传送门
            var activePortals = GetAllActivePortals();
            
            // 更新现有指示器
            UpdateExistingIndicators(activePortals);
            
            // 创建新指示器
            CreateNewIndicators(activePortals);
            
            // 移除无效指示器
            RemoveInvalidIndicators(activePortals);
        }
        
        /// <summary>
        /// 获取所有活跃传送门
        /// </summary>
        private List<PortalData> GetAllActivePortals()
        {
            var allPortals = new List<PortalData>();
            
            // 获取所有类型的传送门
            allPortals.AddRange(_portalManager.GetActivePortalsByType(PortalType.Ceiling));
            allPortals.AddRange(_portalManager.GetActivePortalsByType(PortalType.WallLeft));
            allPortals.AddRange(_portalManager.GetActivePortalsByType(PortalType.WallRight));
            allPortals.AddRange(_portalManager.GetActivePortalsByType(PortalType.Ground));
            
            return allPortals;
        }
        
        /// <summary>
        /// 更新现有指示器
        /// </summary>
        private void UpdateExistingIndicators(List<PortalData> activePortals)
        {
            foreach (var indicator in _activeIndicators)
            {
                if (indicator.portalData != null && indicator.portalData.isActive)
                {
                    UpdateIndicatorPosition(indicator);
                    UpdateIndicatorColor(indicator);
                    UpdateIndicatorAnimation(indicator);
                }
            }
        }
        
        /// <summary>
        /// 创建新指示器
        /// </summary>
        private void CreateNewIndicators(List<PortalData> activePortals)
        {
            foreach (var portal in activePortals)
            {
                // 检查是否已经存在指示器
                bool exists = false;
                foreach (var indicator in _activeIndicators)
                {
                    if (indicator.portalData == portal)
                    {
                        exists = true;
                        break;
                    }
                }
                
                if (!exists)
                {
                    CreateIndicator(portal);
                }
            }
        }
        
        /// <summary>
        /// 移除无效指示器
        /// </summary>
        private void RemoveInvalidIndicators(List<PortalData> activePortals)
        {
            for (int i = _activeIndicators.Count - 1; i >= 0; i--)
            {
                var indicator = _activeIndicators[i];
                bool isValid = false;
                
                foreach (var portal in activePortals)
                {
                    if (indicator.portalData == portal)
                    {
                        isValid = true;
                        break;
                    }
                }
                
                if (!isValid)
                {
                    DestroyIndicator(indicator);
                    _activeIndicators.RemoveAt(i);
                }
            }
        }
        
        /// <summary>
        /// 创建指示器
        /// </summary>
        private void CreateIndicator(PortalData portalData)
        {
            if (indicatorPrefab == null) return;
            
            // 实例化指示器
            GameObject indicatorObj = Instantiate(indicatorPrefab, canvas.transform);
            indicatorObj.SetActive(true);
            
            // 设置RectTransform
            RectTransform rectTransform = indicatorObj.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                // 设置锚点为屏幕中心
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.sizeDelta = new Vector2(indicatorSize, indicatorSize);
                
                // 设置初始位置为屏幕外（右下角）
                Vector2 initialPos = new Vector2(1000f, -1000f);
                rectTransform.anchoredPosition = initialPos;
            }
            
            // 获取Image组件
            Image indicatorImage = indicatorObj.GetComponent<Image>();
            if (indicatorImage == null)
            {
                indicatorImage = indicatorObj.AddComponent<Image>();
            }
            
            // 创建指示器对象
            PortalIndicator indicator = new PortalIndicator(indicatorObj, indicatorImage, portalData);
            _activeIndicators.Add(indicator);
            
            // 设置初始颜色
            UpdateIndicatorColor(indicator);
        }
        
        /// <summary>
        /// 更新指示器位置
        /// </summary>
        private void UpdateIndicatorPosition(PortalIndicator indicator)
        {
            // 如果传送门没有分配槽位，隐藏指示器
            if (indicator.portalData.slot == null)
            {
                indicator.indicatorObject.SetActive(false);
                return;
            }
            
            // 将世界坐标转换为屏幕坐标
            Vector3 screenPos = playerCamera.WorldToScreenPoint(indicator.portalData.slot.position);
            
            // 检查是否在屏幕内（使用更宽松的判定范围）
            bool isOnScreen = screenPos.x >= -visibilityMargin && screenPos.x <= Screen.width + visibilityMargin &&
                             screenPos.y >= -visibilityMargin && screenPos.y <= Screen.height + visibilityMargin &&
                             screenPos.z > 0;
            
            indicator.isVisible = isOnScreen;
            
            if (!isOnScreen)
            {
                // 计算屏幕边缘位置
                Vector2 edgePos = GetScreenEdgePosition(screenPos);
                
                // 设置指示器位置
                RectTransform rectTransform = indicator.indicatorObject.GetComponent<RectTransform>();
                rectTransform.anchoredPosition = edgePos;
                
                // 显示指示器
                indicator.indicatorObject.SetActive(true);
            }
            else
            {
                // 隐藏指示器（在屏幕内）
                indicator.indicatorObject.SetActive(false);
            }
        }
        
        /// <summary>
        /// 获取屏幕边缘位置
        /// </summary>
        private Vector2 GetScreenEdgePosition(Vector3 screenPos)
        {
            // 获取Canvas的RectTransform
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            if (canvasRect == null) return Vector2.zero;
            
            // 获取Canvas的尺寸
            Vector2 canvasSize = canvasRect.sizeDelta;
            
            // 计算屏幕中心点
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            
            // 使用玩家位置+2Y作为参考点计算方向
            Vector3 playerCenterPos = playerTransform.position + Vector3.up * 2f; // 玩家重心位置
            Vector3 playerScreenPos = playerCamera.WorldToScreenPoint(playerCenterPos);
            
            // 计算从玩家重心到传送门的方向
            Vector2 direction = new Vector2(screenPos.x - playerScreenPos.x, screenPos.y - playerScreenPos.y);
            
            // 如果方向向量为零，返回屏幕外位置
            if (direction.magnitude < 0.001f)
            {
                return new Vector2(1000f, -1000f);
            }
            
            direction = direction.normalized;
            
            // 计算与屏幕边缘的交点
            float edgeX, edgeY;
            
            // 使用更稳定的边缘检测算法
            float rightEdge = Screen.width - screenEdgeOffset;
            float leftEdge = screenEdgeOffset;
            float topEdge = Screen.height - screenEdgeOffset;
            float bottomEdge = screenEdgeOffset;
            
            // 计算与各边缘的交点
            float tRight = (rightEdge - playerScreenPos.x) / direction.x;
            float tLeft = (leftEdge - playerScreenPos.x) / direction.x;
            float tTop = (topEdge - playerScreenPos.y) / direction.y;
            float tBottom = (bottomEdge - playerScreenPos.y) / direction.y;
            
            // 选择最小的正t值（最近的边缘）
            float t = float.MaxValue;
            if (tRight > 0) t = Mathf.Min(t, tRight);
            if (tLeft > 0) t = Mathf.Min(t, tLeft);
            if (tTop > 0) t = Mathf.Min(t, tTop);
            if (tBottom > 0) t = Mathf.Min(t, tBottom);
            
            // 计算边缘位置
            edgeX = playerScreenPos.x + direction.x * t;
            edgeY = playerScreenPos.y + direction.y * t;
            
            // 转换为Canvas本地坐标
            Vector2 canvasPos = new Vector2(
                (edgeX / Screen.width - 0.5f) * canvasSize.x,
                (edgeY / Screen.height - 0.5f) * canvasSize.y
            );
            
            return canvasPos;
        }
        
        /// <summary>
        /// 更新指示器颜色
        /// </summary>
        private void UpdateIndicatorColor(PortalIndicator indicator)
        {
            Color color = Color.white;
            
            switch (indicator.portalData.color)
            {
                case PortalColor.Blue:
                    color = bluePortalColor;
                    break;
                case PortalColor.Orange:
                    color = orangePortalColor;
                    break;
                case PortalColor.GiantOrange:
                    color = giantOrangePortalColor;
                    break;
            }
            
            indicator.indicatorImage.color = color;
        }
        
        /// <summary>
        /// 更新指示器动画
        /// </summary>
        private void UpdateIndicatorAnimation(PortalIndicator indicator)
        {
            if (indicator.isVisible) return;
            
            // 脉冲动画
            float pulse = Mathf.Sin(Time.time * pulseSpeed) * 0.5f + 0.5f;
            float scale = 1f + pulse * (pulseScale - 1f);
            
            indicator.indicatorObject.transform.localScale = Vector3.one * scale;
        }
        
        /// <summary>
        /// 销毁指示器
        /// </summary>
        private void DestroyIndicator(PortalIndicator indicator)
        {
            if (indicator.indicatorObject != null)
            {
                Destroy(indicator.indicatorObject);
            }
        }
        
        private void OnDestroy()
        {
            // 清理所有指示器
            foreach (var indicator in _activeIndicators)
            {
                DestroyIndicator(indicator);
            }
            _activeIndicators.Clear();
        }
    }
}
