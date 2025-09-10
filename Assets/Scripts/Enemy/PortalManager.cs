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
        public PortalSlot portalSlot; // 插槽引用
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
        [Tooltip("最大传送门数量")]
        public int maxPortals = 2;
        
        [Tooltip("传送门持续时间")]
        public float portalDuration = 10f;
        
        [Header("传送门对象")]
        [Tooltip("1号传送门对象")]
        public GameObject portal1;
        
        [Tooltip("2号传送门对象")]
        public GameObject portal2;
        
        [Header("传送门材质")]
        [Tooltip("1号传送门材质")]
        public Material portal1Material;
        
        [Tooltip("2号传送门材质")]
        public Material portal2Material;
        
        [Header("VFX配置")]
        [Tooltip("蓝色传送门VFX配置")]
        public PortalVfxConfig bluePortalVfx;
        
        [Tooltip("橙色传送门VFX配置")]
        public PortalVfxConfig orangePortalVfx;
        
        [Tooltip("巨型橙色传送门VFX配置")]
        public PortalVfxConfig giantOrangePortalVfx;
        
        [Header("传送门插槽")]
        [Tooltip("天花板插槽")]
        public PortalSlot[] ceilingSlots;
        
        [Tooltip("左墙插槽")]
        public PortalSlot[] wallLeftSlots;
        
        [Tooltip("右墙插槽")]
        public PortalSlot[] wallRightSlots;
        
        [Tooltip("地面插槽")]
        public PortalSlot[] groundSlots;
        
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
        
        [ShowInInspector, ReadOnly]
        private PortalData _portal1Data;
        
        [ShowInInspector, ReadOnly]
        private PortalData _portal2Data;
        
        // 事件
        public System.Action<PortalData> OnPortalSpawned;
        public System.Action<PortalData> OnPortalClosed;
        public System.Action<PortalData, PortalData> OnPortalConnection;
        
        #region Unity生命周期
        
        private void Start()
        {
            InitializePortalManager();
            RefreshAvailableSlots();
        }
        
        private void Update()
        {
            // 传送门不需要状态更新，它们一直存在
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
            
            // 初始化传送门对象（传送门一直存在，只是在不同位置）
            InitializePortals();
            
            Debug.Log("[PortalManager] 传送门管理器初始化完成");
        }
        
        /// <summary>
        /// 初始化传送门对象
        /// </summary>
        private void InitializePortals()
        {
            // 初始化1号传送门
            if (portal1)
            {
                _portal1Data = new PortalData(portal1, PortalType.Ceiling, PortalColor.Blue, null);
                _activePortals.Add(_portal1Data);
                Debug.Log($"[PortalManager] 1号传送门初始化完成: {portal1.name}");
            }
            else
            {
                Debug.LogError("[PortalManager] portal1对象为空，无法初始化1号传送门！");
            }
            
            // 初始化2号传送门
            if (portal2)
            {
                _portal2Data = new PortalData(portal2, PortalType.Ceiling, PortalColor.Blue, null);
                _activePortals.Add(_portal2Data);
                Debug.Log($"[PortalManager] 2号传送门初始化完成: {portal2.name}");
            }
            else
            {
                Debug.LogError("[PortalManager] portal2对象为空，无法初始化2号传送门！");
            }
            
            Debug.Log($"[PortalManager] 传送门对象初始化完成 - _portal1Data: {_portal1Data != null}, _portal2Data: {_portal2Data != null}");
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
        /// 开始生成传送门（第一阶段：Generating）
        /// </summary>
        /// <param name="type">传送门类型</param>
        /// <param name="color">传送门颜色</param>
        /// <param name="portalNumber">传送门编号(1或2)</param>
        /// <param name="preferredSlot">首选插槽</param>
        /// <returns>生成的传送门数据</returns>
        public PortalData StartPortalGeneration(PortalType type, PortalColor color, int portalNumber, PortalSlot preferredSlot = null)
        {
            // 获取VFX配置
            PortalVfxConfig vfxConfig = GetVfxConfig(color);
            if (vfxConfig == null)
            {
                Debug.LogError($"[PortalManager] 未找到颜色 {color} 的VFX配置");
                return null;
            }
            
            // 获取可用插槽
            PortalSlot slot = GetAvailableSlot(type, preferredSlot);
            if (!slot)
            {
                Debug.LogWarning($"[PortalManager] 没有可用的 {type} 插槽");
                return null;
            }
            
            
            // 获取或创建传送门数据
            PortalData portalData = GetPortalData(portalNumber);
            if (portalData == null)
            {
                Debug.LogError($"[PortalManager] 传送门{portalNumber}数据不存在，请检查初始化");
                return null;
            }
            
            // 生成阶段只记录插槽信息，临时保存颜色用于后续阶段
            portalData.portalSlot = slot;
            portalData.color = color; // 临时保存颜色，但不在传送门上应用
            
            // 开始生成过程（第一阶段：Generating）
            slot.StartGenerating(color, vfxConfig.generatingVfxPrefab);
            
            // 生成阶段不设置传送门对象，不更改颜色，只处理VFX
            
            // 更新传送门时间戳
            portalData.spawnTime = Time.time;
            
            _totalPortalsSpawned++;
            
            // 播放生成特效
            PlaySpawnEffect(slot.transform.position);
            
            // 触发事件
            OnPortalSpawned?.Invoke(portalData);
            
            Debug.Log($"[PortalManager] 开始生成传送门: {type} {color} 编号{portalNumber} 在 {slot.name}");
            
            return portalData;
        }
        
        
        /// <summary>
        /// 开始传送门前摇（第二阶段：Telegraphing）
        /// </summary>
        /// <param name="portalNumber">传送门编号(1或2)</param>
        /// <param name="telegraphDuration">前摇持续时间</param>
        public void StartPortalTelegraphing(int portalNumber, float telegraphDuration)
        {
            PortalData portalData = GetPortalData(portalNumber);
            if (portalData == null)
            {
                Debug.LogWarning($"[PortalManager] 传送门{portalNumber}不存在");
                return;
            }
            
            if (portalData.portalSlot == null)
            {
                Debug.LogWarning("[PortalManager] 传送门插槽为空");
                return;
            }
            
            // 获取VFX配置
            PortalVfxConfig vfxConfig = GetVfxConfig(portalData.color);
            if (vfxConfig == null)
            {
                Debug.LogError($"[PortalManager] 未找到颜色 {portalData.color} 的VFX配置");
                return;
            }
            
            // 前摇阶段更新所有传送门数据
            portalData.type = GetPortalTypeFromSlot(portalData.portalSlot);
            portalData.slot = portalData.portalSlot.transform;
            
            // 设置传送门对象
            SetPortalObject(portalData, portalNumber);
            
            // 更新传送门颜色
            UpdatePortalColor(portalData, portalData.color);
            
            // 开始前摇阶段（第二阶段：Telegraphing）
            portalData.portalSlot.StartTelegraphing(telegraphDuration, vfxConfig.telegraphingVfxPrefab, portalData.portalObject);
            
            // 更新传送门时间戳（传送门被移动，变为较新的）
            UpdatePortalTimestamp(portalNumber);
            
            Debug.Log($"[PortalManager] 开始传送门前摇: {portalData.type} 编号{portalNumber}");
        }
        
        /// <summary>
        /// 生成传送门（兼容性方法）
        /// </summary>
        /// <param name="type">传送门类型</param>
        /// <param name="color">传送门颜色</param>
        /// <param name="portalNumber">传送门编号(1或2)</param>
        /// <param name="preferredSlot">首选插槽</param>
        /// <returns>生成的传送门数据</returns>
        public PortalData GeneratePortal(PortalType type, PortalColor color, int portalNumber, PortalSlot preferredSlot = null)
        {
            return StartPortalGeneration(type, color, portalNumber, preferredSlot);
        }
        
        // 传送门不需要关闭，它们一直存在
        
        
        
        #endregion
        
        #region 插槽管理
        
        /// <summary>
        /// 获取可用插槽
        /// </summary>
        /// <param name="type">传送门类型</param>
        /// <param name="preferredSlot">首选插槽</param>
        /// <returns>可用插槽</returns>
        private PortalSlot GetAvailableSlot(PortalType type, PortalSlot preferredSlot = null)
        {
            // 如果指定了首选插槽且可用，使用它
            if (preferredSlot && IsSlotAvailable(preferredSlot))
            {
                return preferredSlot;
            }
            
            // 根据类型获取插槽数组
            PortalSlot[] slots = GetSlotsByType(type);
            if (slots == null || slots.Length == 0)
            {
                return null;
            }
            
            // 查找可用插槽
            var availableSlots = new List<PortalSlot>();
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
        private PortalSlot[] GetSlotsByType(PortalType type)
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
        private bool IsSlotAvailable(PortalSlot slot)
        {
            if (!slot) return false;
            
            // 检查插槽状态
            if (slot._currentState != PortalSlotState.Idle)
            {
                return false;
            }
            
            return true;
        }
        
        #endregion
        
        // 传送门不需要状态更新，它们一直存在
        
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
        
        [Button("测试生成阶段 - 蓝色")]
        public void TestGeneratingBlue()
        {
            if (!testSlot)
            {
                Debug.LogWarning("[PortalManager] 请先选择测试插槽！");
                return;
            }
            PortalType slotType = GetPortalTypeFromSlot(testSlot);
            StartPortalGeneration(slotType, PortalColor.Blue, 1, testSlot);
        }
        
        [Button("测试生成阶段 - 橙色")]
        public void TestGeneratingOrange()
        {
            if (!testSlot)
            {
                Debug.LogWarning("[PortalManager] 请先选择测试插槽！");
                return;
            }
            PortalType slotType = GetPortalTypeFromSlot(testSlot);
            StartPortalGeneration(slotType, PortalColor.Orange, 1, testSlot);
        }
        
        [Button("测试生成阶段 - 巨型橙色")]
        public void TestGeneratingGiantOrange()
        {
            if (!testSlot)
            {
                Debug.LogWarning("[PortalManager] 请先选择测试插槽！");
                return;
            }
            PortalType slotType = GetPortalTypeFromSlot(testSlot);
            StartPortalGeneration(slotType, PortalColor.GiantOrange, 1, testSlot);
        }
        
        [Button("测试前摇阶段")]
        public void TestTelegraphing()
        {
            // 获取最老的传送门进行前摇
            int oldestPortalNumber = GetOldestPortalNumber();
            if (oldestPortalNumber > 0)
            {
                Debug.Log($"[PortalManager] 使用最老的传送门进行前摇: {oldestPortalNumber}号");
                StartPortalTelegraphing(oldestPortalNumber, 2f);
                
                // 显示传送门队列信息
                ShowPortalQueueInfo();
            }
            else
            {
                Debug.LogWarning("[PortalManager] 没有可用的传送门进行前摇");
            }
        }
        
        /// <summary>
        /// 显示传送门队列信息
        /// </summary>
        private void ShowPortalQueueInfo()
        {
            Debug.Log("=== 传送门队列信息 ===");
            
            // 显示下一个要用的传送门
            int nextPortalNumber = GetOldestPortalNumber();
            if (nextPortalNumber > 0)
            {
                PortalData nextPortal = GetPortalData(nextPortalNumber);
                string nextPortalName = nextPortal?.portalObject?.name ?? "null";
                Debug.Log($"下一个要用的传送门: {nextPortalNumber}号 ({nextPortalName})");
            }
            else
            {
                Debug.Log("下一个要用的传送门: 无可用传送门");
            }
            
            // 显示队列中所有传送门
            Debug.Log("队列中所有传送门:");
            
            if (_portal1Data != null && _portal1Data.portalSlot != null)
            {
                string portal1Name = _portal1Data.portalObject?.name ?? "null";
                string portal1Slot = _portal1Data.portalSlot?.name ?? "null";
                Debug.Log($"  1号传送门: {portal1Name} (插槽: {portal1Slot}, 时间戳: {_portal1Data.spawnTime:F2})");
            }
            else
            {
                Debug.Log("  1号传送门: 未分配插槽");
            }
            
            if (_portal2Data != null)
            {
                string portal2Name = _portal2Data.portalObject?.name ?? "null";
                if (_portal2Data.portalSlot != null)
                {
                    string portal2Slot = _portal2Data.portalSlot.name;
                    Debug.Log($"  2号传送门: {portal2Name} (插槽: {portal2Slot}, 时间戳: {_portal2Data.spawnTime:F2})");
                }
                else
                {
                    Debug.Log($"  2号传送门: {portal2Name} (插槽: null, 时间戳: {_portal2Data.spawnTime:F2}) - 未分配插槽");
                }
            }
            else
            {
                Debug.Log("  2号传送门: 数据为空");
            }
            
            Debug.Log("==================");
        }
        
        /// <summary>
        /// 获取最老的传送门编号
        /// </summary>
        /// <returns>最老的传送门编号，如果没有可用传送门返回0</returns>
        private int GetOldestPortalNumber()
        {
            float oldestTime = float.MaxValue;
            int oldestNumber = 0;
            
            if (_portal1Data != null && _portal1Data.portalSlot != null)
            {
                if (_portal1Data.spawnTime < oldestTime)
                {
                    oldestTime = _portal1Data.spawnTime;
                    oldestNumber = 1;
                }
            }
            
            if (_portal2Data != null && _portal2Data.portalSlot != null)
            {
                if (_portal2Data.spawnTime < oldestTime)
                {
                    oldestTime = _portal2Data.spawnTime;
                    oldestNumber = 2;
                }
            }
            
            return oldestNumber;
        }
        
        /// <summary>
        /// 更新传送门时间戳（当传送门被移动时调用）
        /// </summary>
        /// <param name="portalNumber">传送门编号</param>
        private void UpdatePortalTimestamp(int portalNumber)
        {
            PortalData portalData = GetPortalData(portalNumber);
            if (portalData != null)
            {
                portalData.spawnTime = Time.time;
                Debug.Log($"[PortalManager] 更新{portalNumber}号传送门时间戳: {portalData.spawnTime}");
            }
        }
        
        [Button("显示传送门队列信息")]
        public void ShowPortalQueueInfoButton()
        {
            ShowPortalQueueInfo();
        }
        
        // 传送门不需要关闭，它们一直存在
        
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
        
        private void DrawSlotGizmos(PortalSlot[] slots, Color color, string label)
        {
            if (slots == null) return;
            
            Gizmos.color = color;
            foreach (var slot in slots)
            {
                if (slot)
                {
                    Gizmos.DrawWireSphere(slot.transform.position, 0.5f);
                    Gizmos.DrawWireCube(slot.transform.position, Vector3.one * 0.3f);
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
        
        #region PortalSystem功能
        
        /// <summary>
        /// 更改传送门颜色
        /// </summary>
        /// <param name="portalNumber">传送门编号(1或2)</param>
        /// <param name="newColor">新颜色</param>
        public void ChangePortalColor(int portalNumber, PortalColor newColor)
        {
            PortalData portalData = GetPortalData(portalNumber);
            if (portalData == null)
            {
                Debug.LogWarning($"[PortalManager] 传送门{portalNumber}不存在");
                return;
            }
            
            // 更新传送门数据
            portalData.color = newColor;
            
            // 更新传送门颜色
            UpdatePortalColor(portalData, newColor);
            
            // 更新VFX配置
            PortalVfxConfig vfxConfig = GetVfxConfig(newColor);
            if (vfxConfig != null)
            {
                ConfigurePortalSlotVfx(portalData.portalSlot, vfxConfig);
            }
            
            Debug.Log($"[PortalManager] 传送门{portalNumber}颜色更改为: {newColor}");
        }
        
        /// <summary>
        /// 获取VFX配置
        /// </summary>
        /// <param name="color">传送门颜色</param>
        /// <returns>VFX配置</returns>
        private PortalVfxConfig GetVfxConfig(PortalColor color)
        {
            switch (color)
            {
                case PortalColor.Blue:
                    return bluePortalVfx;
                case PortalColor.Orange:
                    return orangePortalVfx;
                case PortalColor.GiantOrange:
                    return giantOrangePortalVfx;
                default:
                    return null;
            }
        }
        
        
        /// <summary>
        /// 设置传送门对象
        /// </summary>
        /// <param name="portalData">传送门数据</param>
        /// <param name="portalNumber">传送门编号</param>
        private void SetPortalObject(PortalData portalData, int portalNumber)
        {
            GameObject portalObject = (portalNumber == 1) ? portal1 : portal2;
            if (portalObject)
            {
                portalData.portalObject = portalObject;
            }
        }
        
        /// <summary>
        /// 更新传送门颜色
        /// </summary>
        /// <param name="portalData">传送门数据</param>
        /// <param name="color">颜色</param>
        private void UpdatePortalColor(PortalData portalData, PortalColor color)
        {
            if (portalData.portalObject == null) return;
            
            // 获取传送门编号对应的材质
            Material targetMaterial = GetPortalMaterial(portalData);
            if (targetMaterial == null) return;
            
            // 获取VFX配置中的FrameColor
            PortalVfxConfig vfxConfig = GetVfxConfig(color);
            if (vfxConfig == null) return;
            
            // 设置材质的FrameColor属性
            if (targetMaterial.HasProperty("_FrameColor"))
            {
                targetMaterial.SetColor("_FrameColor", vfxConfig.frameColor);
            }
            else
            {
                Debug.LogWarning($"[PortalManager] 材质 {targetMaterial.name} 没有 _FrameColor 属性");
            }
        }
        
        /// <summary>
        /// 从PortalColor获取Unity Color
        /// </summary>
        /// <param name="portalColor">传送门颜色</param>
        /// <returns>Unity Color</returns>
        private Color GetColorFromPortalColor(PortalColor portalColor)
        {
            switch (portalColor)
            {
                case PortalColor.Blue:
                    return Color.blue;
                case PortalColor.Orange:
                    return new Color(1f, 0.5f, 0f);
                case PortalColor.GiantOrange:
                    return Color.red;
                default:
                    return Color.white;
            }
        }
        
        /// <summary>
        /// 获取传送门数据
        /// </summary>
        /// <param name="portalNumber">传送门编号</param>
        /// <returns>传送门数据</returns>
        private PortalData GetPortalData(int portalNumber)
        {
            PortalData result = (portalNumber == 1) ? _portal1Data : _portal2Data;
            if (result == null)
            {
                Debug.LogError($"[PortalManager] 传送门{portalNumber}数据为空！portal1={portal1 != null}, portal2={portal2 != null}, _portal1Data={_portal1Data != null}, _portal2Data={_portal2Data != null}");
            }
            return result;
        }
        
        /// <summary>
        /// 获取传送门材质
        /// </summary>
        /// <param name="portalData">传送门数据</param>
        /// <returns>传送门材质</returns>
        private Material GetPortalMaterial(PortalData portalData)
        {
            if (portalData == null) return null;
            
            // 根据传送门对象确定使用哪个材质
            if (portalData.portalObject == portal1)
            {
                return portal1Material;
            }
            else if (portalData.portalObject == portal2)
            {
                return portal2Material;
            }
            
            return null;
        }
        
        /// <summary>
        /// 配置PortalSlot的VFX（兼容性方法）
        /// </summary>
        /// <param name="portalSlot">传送门插槽</param>
        /// <param name="vfxConfig">VFX配置</param>
        private void ConfigurePortalSlotVfx(PortalSlot portalSlot, PortalVfxConfig vfxConfig)
        {
            if (portalSlot == null || vfxConfig == null) return;
            
            portalSlot.generatingVfxPrefab = vfxConfig.generatingVfxPrefab;
            portalSlot.telegraphingVfxPrefab = vfxConfig.telegraphingVfxPrefab;
        }
        
        /// <summary>
        /// 根据插槽获取传送门类型
        /// </summary>
        /// <param name="slot">插槽</param>
        /// <returns>传送门类型</returns>
        private PortalType GetPortalTypeFromSlot(PortalSlot slot)
        {
            if (slot == null) return PortalType.Ceiling;
            
            // 检查插槽属于哪个类型
            if (ceilingSlots != null && System.Array.IndexOf(ceilingSlots, slot) >= 0)
                return PortalType.Ceiling;
            if (wallLeftSlots != null && System.Array.IndexOf(wallLeftSlots, slot) >= 0)
                return PortalType.WallLeft;
            if (wallRightSlots != null && System.Array.IndexOf(wallRightSlots, slot) >= 0)
                return PortalType.WallRight;
            if (groundSlots != null && System.Array.IndexOf(groundSlots, slot) >= 0)
                return PortalType.Ground;
            
            // 默认返回天花板类型
            return PortalType.Ceiling;
        }
        
        #endregion
        
        #region 调试方法（更新）
        
        [Header("测试配置")]
        [Tooltip("测试时使用的插槽（所有测试都会在这个插槽上运行）")]
        public PortalSlot testSlot;
        
        [ShowInInspector, ReadOnly]
        private string[] _availableSlotNames;
        
        [ShowInInspector, ReadOnly]
        private PortalSlot[] _availableSlots;
        
        [Tooltip("选择插槽的索引（在可用插槽列表中的位置）")]
        public int selectedSlotIndex = 0;
        
        [Button("刷新可用插槽列表")]
        public void RefreshAvailableSlots()
        {
            var slotList = new System.Collections.Generic.List<PortalSlot>();
            var nameList = new System.Collections.Generic.List<string>();
            
            // 收集所有插槽
            if (ceilingSlots != null)
            {
                foreach (var slot in ceilingSlots)
                {
                    if (slot != null)
                    {
                        slotList.Add(slot);
                        nameList.Add($"[天花板] {slot.name}");
                    }
                }
            }
            
            if (wallLeftSlots != null)
            {
                foreach (var slot in wallLeftSlots)
                {
                    if (slot != null)
                    {
                        slotList.Add(slot);
                        nameList.Add($"[左墙] {slot.name}");
                    }
                }
            }
            
            if (wallRightSlots != null)
            {
                foreach (var slot in wallRightSlots)
                {
                    if (slot != null)
                    {
                        slotList.Add(slot);
                        nameList.Add($"[右墙] {slot.name}");
                    }
                }
            }
            
            if (groundSlots != null)
            {
                foreach (var slot in groundSlots)
                {
                    if (slot != null)
                    {
                        slotList.Add(slot);
                        nameList.Add($"[地面] {slot.name}");
                    }
                }
            }
            
            _availableSlots = slotList.ToArray();
            _availableSlotNames = nameList.ToArray();
            
            Debug.Log($"[PortalManager] 刷新完成，找到 {_availableSlots.Length} 个可用插槽");
        }
        
        [Button("选择插槽")]
        public void SelectSlot()
        {
            if (_availableSlots == null || _availableSlots.Length == 0)
            {
                Debug.LogWarning("[PortalManager] 没有可用插槽，请先刷新插槽列表");
                return;
            }
            
            if (selectedSlotIndex >= 0 && selectedSlotIndex < _availableSlots.Length)
            {
                testSlot = _availableSlots[selectedSlotIndex];
                Debug.Log($"[PortalManager] 已选择插槽: {_availableSlotNames[selectedSlotIndex]}");
            }
            else
            {
                Debug.LogWarning($"[PortalManager] 插槽索引 {selectedSlotIndex} 超出范围 (0-{_availableSlots.Length - 1})");
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// 传送门VFX配置
    /// </summary>
    [System.Serializable]
    public class PortalVfxConfig
    {
        [Tooltip("生成阶段VFX预制体")]
        public GameObject generatingVfxPrefab;
        
        [Tooltip("前摇阶段VFX预制体")]
        public GameObject telegraphingVfxPrefab;
        
        [Tooltip("传送门边框颜色")]
        public Color frameColor = Color.white;
    }
    
}
