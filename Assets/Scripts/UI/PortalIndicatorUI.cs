using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Invector.vCharacterController.AI;

namespace Xuwu.UI
{
    /// <summary>
    /// 传送门指示器UI - 使用4个固定方向的白色长条显示视野外的传送门
    /// </summary>
    public class PortalIndicatorUI : MonoBehaviour
    {
        [Header("UI组件")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Transform playerTransform;
        
        [Header("方向指示器")]
        [SerializeField] private Image upIndicator;      // 上方指示器
        [SerializeField] private Image downIndicator;    // 下方指示器
        [SerializeField] private Image leftIndicator;    // 左方指示器
        [SerializeField] private Image rightIndicator;   // 右方指示器
        
        [Header("颜色配置")]
        [SerializeField] private Color bluePortalColor = Color.blue;
        [SerializeField] private Color orangePortalColor = new Color(1f, 0.5f, 0f); // 橙色
        [SerializeField] private Color giantOrangePortalColor = new Color(1f, 0.3f, 0f); // 深橙色
        [SerializeField] private Color inactiveColor = Color.white; // 未激活时的颜色
        
        [Header("视野检测配置")]
        [SerializeField] private float visibilityMargin = 100f; // 视野边缘的宽松范围
        
        // 组件引用
        private PortalManager _portalManager;
        
        // 方向与PortalType的映射
        private Dictionary<PortalType, Image> _directionIndicators;
        
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
            
            // 初始化方向指示器映射
            InitializeDirectionMapping();
            
            // 初始化所有指示器为未激活状态
            ResetAllIndicators();
        }
        
        /// <summary>
        /// 初始化方向映射
        /// </summary>
        private void InitializeDirectionMapping()
        {
            _directionIndicators = new Dictionary<PortalType, Image>
            {
                { PortalType.Ceiling, upIndicator },      // 天花板 -> 上方指示器
                { PortalType.Ground, downIndicator },     // 地面 -> 下方指示器
                { PortalType.WallLeft, leftIndicator },   // 左墙 -> 左方指示器
                { PortalType.WallRight, rightIndicator }  // 右墙 -> 右方指示器
            };
        }
        
        /// <summary>
        /// 重置所有指示器为未激活状态
        /// </summary>
        private void ResetAllIndicators()
        {
            foreach (var indicator in _directionIndicators.Values)
            {
                if (indicator != null)
                {
                    indicator.color = inactiveColor;
                }
            }
        }
        
        /// <summary>
        /// 更新指示器
        /// </summary>
        private void UpdateIndicators()
        {
            if (_portalManager == null || playerCamera == null) return;
            
            // 重置所有指示器
            ResetAllIndicators();
            
            // 检查每个方向的传送门
            CheckDirectionPortals(PortalType.Ceiling);
            CheckDirectionPortals(PortalType.Ground);
            CheckDirectionPortals(PortalType.WallLeft);
            CheckDirectionPortals(PortalType.WallRight);
        }
        
        /// <summary>
        /// 检查指定方向的传送门
        /// </summary>
        /// <param name="portalType">传送门类型</param>
        private void CheckDirectionPortals(PortalType portalType)
        {
            // 获取该类型的所有活跃传送门
            var portals = _portalManager.GetActivePortalsByType(portalType);
            
            if (portals.Count == 0) return;
            
            // 检查是否有传送门在视野外
            bool hasOutOfViewPortal = false;
            PortalColor activeColor = PortalColor.Blue; // 默认颜色
            
            foreach (var portal in portals)
            {
                if (portal.isActive && portal.slot != null)
                {
                    // 检查传送门是否在视野外
                    if (IsPortalOutOfView(portal))
                    {
                        hasOutOfViewPortal = true;
                        
                        // 尝试从BossSkillTask获取telegraph阶段的颜色
                        PortalColor telegraphColor = GetTelegraphColorFromBossSkill();
                        if (telegraphColor != PortalColor.Blue) // 如果获取到了有效的telegraph颜色
                        {
                            activeColor = telegraphColor;
                        }
                        else
                        {
                            activeColor = portal.color; // 回退到使用传送门数据中的颜色
                        }
                        
                        break; // 找到第一个视野外的传送门就够了
                    }
                }
            }
            
            // 如果有视野外的传送门，激活对应方向的指示器
            if (hasOutOfViewPortal && _directionIndicators.ContainsKey(portalType))
            {
                Image indicator = _directionIndicators[portalType];
                if (indicator != null)
                {
                    indicator.color = GetColorForPortalType(activeColor);
                }
            }
        }
        
        /// <summary>
        /// 从BossBlackboard获取telegraph阶段的传送门颜色
        /// </summary>
        /// <returns>telegraph阶段的传送门颜色，如果获取失败返回默认颜色</returns>
        private PortalColor GetTelegraphColorFromBossSkill()
        {
            // 通过BossBlackboard获取当前活跃的技能任务
            if (_portalManager != null)
            {
                // 尝试从PortalManager获取BossBlackboard
                var bossBlackboard = _portalManager.GetComponent<BossBlackboard>();
                if (bossBlackboard != null)
                {
                    // 从BossBlackboard获取telegraph阶段的颜色
                    PortalColor telegraphColor = bossBlackboard.GetCurrentTelegraphColor();
                    return telegraphColor;
                }
            }
            
            return PortalColor.Blue; // 默认颜色
        }
        
        /// <summary>
        /// 检查传送门是否在视野外
        /// </summary>
        /// <param name="portal">传送门数据</param>
        /// <returns>是否在视野外</returns>
        private bool IsPortalOutOfView(PortalData portal)
        {
            if (portal.slot == null) return false;
            
            // 将传送门世界坐标转换为屏幕坐标
            Vector3 screenPos = playerCamera.WorldToScreenPoint(portal.slot.position);
            
            // 检查是否在屏幕内（使用宽松的判定范围）
            bool isOnScreen = screenPos.x >= -visibilityMargin && screenPos.x <= Screen.width + visibilityMargin &&
                             screenPos.y >= -visibilityMargin && screenPos.y <= Screen.height + visibilityMargin &&
                             screenPos.z > 0;
            
            return !isOnScreen; // 返回是否在视野外
        }
        
        /// <summary>
        /// 根据传送门颜色类型获取对应颜色
        /// </summary>
        /// <param name="portalColor">传送门颜色类型</param>
        /// <returns>对应的颜色</returns>
        private Color GetColorForPortalType(PortalColor portalColor)
        {
            switch (portalColor)
            {
                case PortalColor.Blue:
                    return bluePortalColor;
                case PortalColor.Orange:
                    return orangePortalColor;
                case PortalColor.GiantOrange:
                    return giantOrangePortalColor;
                default:
                    return inactiveColor;
            }
        }
        
    }
}
