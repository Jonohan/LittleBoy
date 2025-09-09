using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace Invector.vCharacterController.AI
{
    /// <summary>
    /// 传送门类型枚举
    /// </summary>
    public enum PortalType
    {
        None,
        Ceiling,     // 天花板
        WallLeft,    // 左墙
        WallRight,   // 右墙
        Ground       // 地面
    }
    
    /// <summary>
    /// 传送门颜色类型
    /// </summary>
    public enum PortalColor
    {
        Blue,        // 蓝色 - 缩小
        Orange,      // 橙色 - 放大
        GiantOrange  // 巨型橙色 - 大幅放大(4.x级别)
    }
    
    /// <summary>
    /// 传送门数据类
    /// </summary>
    [System.Serializable]
    public class PortalData
    {
        public GameObject portalObject;
        public PortalType type;
        public PortalColor color;
        public Transform slot;
        public float spawnTime;
        public bool isActive;
        
        public PortalData(GameObject obj, PortalType t, PortalColor c, Transform s)
        {
            portalObject = obj;
            type = t;
            color = c;
            slot = s;
            spawnTime = Time.time;
            isActive = true;
        }
    }
    
    /// <summary>
    /// 传送门管理器
    /// 负责传送门的生成、管理和清理
    /// </summary>
    public class PortalManager : MonoBehaviour
    {
        [Header("传送门配置")]
        [Tooltip("传送门预制体")]
        public GameObject portalPrefab;
        
        [Tooltip("巨型传送门预制体")]
        public GameObject giantPortalPrefab;
        
        [Tooltip("最大传送门数量")]
        public int maxPortals = 2;
        
        [Tooltip("传送门持续时间")]
        public float portalDuration = 10f;
        
        [Header("传送门插槽")]
        [Tooltip("天花板插槽")]
        public Transform[] ceilingSlots;
        
        [Tooltip("左墙插槽")]
        public Transform[] wallLeftSlots;
        
        [Tooltip("右墙插槽")]
        public Transform[] wallRightSlots;
        
        [Tooltip("地面插槽")]
        public Transform[] groundSlots;
        
        [Header("传送门效果")]
        [Tooltip("传送门生成特效")]
        public GameObject spawnEffect;
        
        [Tooltip("传送门关闭特效")]
        public GameObject closeEffect;
        
        [Header("调试")]
        [ShowInInspector, ReadOnly]
        private List<PortalData> _activePortals = new List<PortalData>();
        
        [ShowInInspector, ReadOnly]
        private int _totalPortalsSpawned = 0;
        
        [ShowInInspector, ReadOnly]
        private int _totalPortalsClosed = 0;
        
        // 事件
        public System.Action<PortalData> OnPortalSpawned;
        public System.Action<PortalData> OnPortalClosed;
        public System.Action<PortalData, PortalData> OnPortalConnection;
        
        #region Unity生命周期
        
        private void Start()
        {
            InitializePortalManager();
        }
        
        private void Update()
        {
            UpdatePortalStates();
        }
        
        #endregion
        
        #region 初始化
        
        /// <summary>
        /// 初始化传送门管理器
        /// </summary>
        private void InitializePortalManager()
        {
            // 验证插槽配置
            ValidateSlots();
            
            Debug.Log("[PortalManager] 传送门管理器初始化完成");
        }
        
        /// <summary>
        /// 验证插槽配置
        /// </summary>
        private void ValidateSlots()
        {
            int totalSlots = 0;
            
            if (ceilingSlots != null) totalSlots += ceilingSlots.Length;
            if (wallLeftSlots != null) totalSlots += wallLeftSlots.Length;
            if (wallRightSlots != null) totalSlots += wallRightSlots.Length;
            if (groundSlots != null) totalSlots += groundSlots.Length;
            
            if (totalSlots == 0)
            {
                Debug.LogWarning("[PortalManager] 没有配置任何传送门插槽");
            }
            else
            {
                Debug.Log($"[PortalManager] 配置了 {totalSlots} 个传送门插槽");
            }
        }
        
        #endregion
        
        #region 传送门管理
        
        /// <summary>
        /// 生成传送门
        /// </summary>
        /// <param name="type">传送门类型</param>
        /// <param name="color">传送门颜色</param>
        /// <param name="preferredSlot">首选插槽</param>
        /// <returns>生成的传送门数据</returns>
        public PortalData SpawnPortal(PortalType type, PortalColor color, Transform preferredSlot = null)
        {
            // 检查是否达到最大数量
            if (_activePortals.Count >= maxPortals)
            {
                // 关闭最早的传送门
                CloseOldestPortal();
            }
            
            // 获取可用插槽
            Transform slot = GetAvailableSlot(type, preferredSlot);
            if (!slot)
            {
                Debug.LogWarning($"[PortalManager] 没有可用的 {type} 插槽");
                return null;
            }
            
            // 选择预制体
            GameObject prefab = (color == PortalColor.GiantOrange) ? giantPortalPrefab : portalPrefab;
            if (!prefab)
            {
                Debug.LogError("[PortalManager] 传送门预制体未配置");
                return null;
            }
            
            // 生成传送门
            GameObject portalObj = Instantiate(prefab, slot.position, slot.rotation, slot);
            
            // 创建传送门数据
            PortalData portalData = new PortalData(portalObj, type, color, slot);
            _activePortals.Add(portalData);
            _totalPortalsSpawned++;
            
            // 播放生成特效
            PlaySpawnEffect(slot.position);
            
            // 触发事件
            OnPortalSpawned?.Invoke(portalData);
            
            Debug.Log($"[PortalManager] 生成传送门: {type} {color} 在 {slot.name}");
            
            return portalData;
        }
        
        /// <summary>
        /// 关闭传送门
        /// </summary>
        /// <param name="portalData">要关闭的传送门数据</param>
        public void ClosePortal(PortalData portalData)
        {
            if (portalData == null || !portalData.isActive) return;
            
            // 播放关闭特效
            PlayCloseEffect(portalData.slot.position);
            
            // 销毁传送门对象
            if (portalData.portalObject)
            {
                Destroy(portalData.portalObject);
            }
            
            // 标记为非活跃
            portalData.isActive = false;
            _activePortals.Remove(portalData);
            _totalPortalsClosed++;
            
            // 触发事件
            OnPortalClosed?.Invoke(portalData);
            
            Debug.Log($"[PortalManager] 关闭传送门: {portalData.type} {portalData.color}");
        }
        
        /// <summary>
        /// 关闭最早的传送门
        /// </summary>
        public void CloseOldestPortal()
        {
            if (_activePortals.Count == 0) return;
            
            PortalData oldestPortal = null;
            float oldestTime = float.MaxValue;
            
            foreach (var portal in _activePortals)
            {
                if (portal.spawnTime < oldestTime)
                {
                    oldestTime = portal.spawnTime;
                    oldestPortal = portal;
                }
            }
            
            if (oldestPortal != null)
            {
                ClosePortal(oldestPortal);
            }
        }
        
        /// <summary>
        /// 关闭所有传送门
        /// </summary>
        public void CloseAllPortals()
        {
            var portalsToClose = new List<PortalData>(_activePortals);
            foreach (var portal in portalsToClose)
            {
                ClosePortal(portal);
            }
        }
        
        #endregion
        
        #region 插槽管理
        
        /// <summary>
        /// 获取可用插槽
        /// </summary>
        /// <param name="type">传送门类型</param>
        /// <param name="preferredSlot">首选插槽</param>
        /// <returns>可用插槽</returns>
        private Transform GetAvailableSlot(PortalType type, Transform preferredSlot = null)
        {
            // 如果指定了首选插槽且可用，使用它
            if (preferredSlot && IsSlotAvailable(preferredSlot))
            {
                return preferredSlot;
            }
            
            // 根据类型获取插槽数组
            Transform[] slots = GetSlotsByType(type);
            if (slots == null || slots.Length == 0)
            {
                return null;
            }
            
            // 查找可用插槽
            var availableSlots = new List<Transform>();
            foreach (var slot in slots)
            {
                if (slot && IsSlotAvailable(slot))
                {
                    availableSlots.Add(slot);
                }
            }
            
            if (availableSlots.Count == 0)
            {
                return null;
            }
            
            // 随机选择一个可用插槽
            return availableSlots[Random.Range(0, availableSlots.Count)];
        }
        
        /// <summary>
        /// 根据类型获取插槽数组
        /// </summary>
        /// <param name="type">传送门类型</param>
        /// <returns>插槽数组</returns>
        private Transform[] GetSlotsByType(PortalType type)
        {
            switch (type)
            {
                case PortalType.Ceiling:
                    return ceilingSlots;
                case PortalType.WallLeft:
                    return wallLeftSlots;
                case PortalType.WallRight:
                    return wallRightSlots;
                case PortalType.Ground:
                    return groundSlots;
                default:
                    return null;
            }
        }
        
        /// <summary>
        /// 检查插槽是否可用
        /// </summary>
        /// <param name="slot">插槽</param>
        /// <returns>是否可用</returns>
        private bool IsSlotAvailable(Transform slot)
        {
            if (!slot) return false;
            
            // 检查是否有传送门占用此插槽
            foreach (var portal in _activePortals)
            {
                if (portal.slot == slot && portal.isActive)
                {
                    return false;
                }
            }
            
            return true;
        }
        
        #endregion
        
        #region 传送门状态更新
        
        /// <summary>
        /// 更新传送门状态
        /// </summary>
        private void UpdatePortalStates()
        {
            var portalsToClose = new List<PortalData>();
            
            foreach (var portal in _activePortals)
            {
                // 检查传送门是否超时
                if (Time.time - portal.spawnTime >= portalDuration)
                {
                    portalsToClose.Add(portal);
                }
            }
            
            // 关闭超时的传送门
            foreach (var portal in portalsToClose)
            {
                ClosePortal(portal);
            }
        }
        
        #endregion
        
        #region 特效播放
        
        /// <summary>
        /// 播放生成特效
        /// </summary>
        /// <param name="position">位置</param>
        private void PlaySpawnEffect(Vector3 position)
        {
            if (spawnEffect)
            {
                Instantiate(spawnEffect, position, Quaternion.identity);
            }
        }
        
        /// <summary>
        /// 播放关闭特效
        /// </summary>
        /// <param name="position">位置</param>
        private void PlayCloseEffect(Vector3 position)
        {
            if (closeEffect)
            {
                Instantiate(closeEffect, position, Quaternion.identity);
            }
        }
        
        #endregion
        
        #region 公共方法
        
        /// <summary>
        /// 获取活跃传送门数量
        /// </summary>
        /// <returns>活跃传送门数量</returns>
        public int GetActivePortalCount()
        {
            return _activePortals.Count;
        }
        
        /// <summary>
        /// 获取指定类型的活跃传送门
        /// </summary>
        /// <param name="type">传送门类型</param>
        /// <returns>传送门列表</returns>
        public List<PortalData> GetActivePortalsByType(PortalType type)
        {
            var result = new List<PortalData>();
            foreach (var portal in _activePortals)
            {
                if (portal.type == type && portal.isActive)
                {
                    result.Add(portal);
                }
            }
            return result;
        }
        
        /// <summary>
        /// 获取指定颜色的活跃传送门
        /// </summary>
        /// <param name="color">传送门颜色</param>
        /// <returns>传送门列表</returns>
        public List<PortalData> GetActivePortalsByColor(PortalColor color)
        {
            var result = new List<PortalData>();
            foreach (var portal in _activePortals)
            {
                if (portal.color == color && portal.isActive)
                {
                    result.Add(portal);
                }
            }
            return result;
        }
        
        /// <summary>
        /// 检查是否有传送门连接
        /// </summary>
        /// <returns>是否有连接</returns>
        public bool HasPortalConnection()
        {
            return _activePortals.Count >= 2;
        }
        
        /// <summary>
        /// 获取传送门连接状态
        /// </summary>
        /// <returns>连接状态描述</returns>
        public string GetConnectionStatus()
        {
            if (_activePortals.Count == 0)
                return "无传送门";
            else if (_activePortals.Count == 1)
                return "单传送门（关闭状态）";
            else
                return "双传送门（连接状态）";
        }
        
        #endregion
        
        #region 调试方法
        
        [Button("生成测试传送门")]
        public void SpawnTestPortal()
        {
            SpawnPortal(PortalType.Ceiling, PortalColor.Blue);
        }
        
        [Button("生成巨型传送门")]
        public void SpawnGiantPortal()
        {
            SpawnPortal(PortalType.Ground, PortalColor.GiantOrange);
        }
        
        [Button("关闭所有传送门")]
        public void CloseAllPortalsDebug()
        {
            CloseAllPortals();
        }
        
        [Button("关闭最早传送门")]
        public void CloseOldestPortalDebug()
        {
            CloseOldestPortal();
        }
        
        #endregion
        
        #region 调试显示
        
        private void OnDrawGizmosSelected()
        {
            // 绘制插槽位置
            DrawSlotGizmos(ceilingSlots, Color.blue, "天花板");
            DrawSlotGizmos(wallLeftSlots, Color.green, "左墙");
            DrawSlotGizmos(wallRightSlots, Color.red, "右墙");
            DrawSlotGizmos(groundSlots, Color.yellow, "地面");
            
            // 绘制活跃传送门
            DrawActivePortalGizmos();
        }
        
        private void DrawSlotGizmos(Transform[] slots, Color color, string label)
        {
            if (slots == null) return;
            
            Gizmos.color = color;
            foreach (var slot in slots)
            {
                if (slot)
                {
                    Gizmos.DrawWireSphere(slot.position, 0.5f);
                    Gizmos.DrawWireCube(slot.position, Vector3.one * 0.3f);
                }
            }
        }
        
        private void DrawActivePortalGizmos()
        {
            Gizmos.color = Color.cyan;
            foreach (var portal in _activePortals)
            {
                if (portal.isActive && portal.slot)
                {
                    Gizmos.DrawWireSphere(portal.slot.position, 1f);
                }
            }
        }
        
        #endregion
    }
}
