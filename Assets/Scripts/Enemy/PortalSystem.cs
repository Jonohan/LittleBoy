using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace Invector.vCharacterController.AI
{
    /// <summary>
    /// 传送门系统 - 高层决策者
    /// 负责决定传送门类型、颜色、VFX配置，并管理1号/2号传送门
    /// </summary>
    public class PortalSystem : MonoBehaviour
    {
        [Header("传送门配置")]
        [Tooltip("1号传送门对象")]
        public GameObject portal1;
        
        [Tooltip("2号传送门对象")]
        public GameObject portal2;
        
        [Header("VFX配置")]
        [Tooltip("蓝色传送门VFX配置")]
        public PortalVfxConfig bluePortalVfx;
        
        [Tooltip("橙色传送门VFX配置")]
        public PortalVfxConfig orangePortalVfx;
        
        [Tooltip("巨型橙色传送门VFX配置")]
        public PortalVfxConfig giantOrangePortalVfx;
        
        [Header("调试")]
        [ShowInInspector, ReadOnly]
        private PortalData _portal1Data;
        
        [ShowInInspector, ReadOnly]
        private PortalData _portal2Data;
        
        [ShowInInspector, ReadOnly]
        private PortalManager _portalManager;
        
        #region Unity生命周期
        
        private void Start()
        {
            _portalManager = GetComponent<PortalManager>();
            if (!_portalManager)
            {
                Debug.LogError("[PortalSystem] 需要PortalManager组件");
            }
        }
        
        #endregion
        
        #region 公共方法
        
        /// <summary>
        /// 生成传送门
        /// </summary>
        /// <param name="portalType">传送门类型</param>
        /// <param name="portalColor">传送门颜色</param>
        /// <param name="portalNumber">传送门编号(1或2)</param>
        /// <param name="preferredSlot">首选插槽</param>
        /// <returns>生成的传送门数据</returns>
        public PortalData GeneratePortal(PortalType portalType, PortalColor portalColor, int portalNumber, PortalSlot preferredSlot = null)
        {
            if (!_portalManager)
            {
                Debug.LogError("[PortalSystem] PortalManager未找到");
                return null;
            }
            
            // 获取VFX配置
            PortalVfxConfig vfxConfig = GetVfxConfig(portalColor);
            if (vfxConfig == null)
            {
                Debug.LogError($"[PortalSystem] 未找到颜色 {portalColor} 的VFX配置");
                return null;
            }
            
            // 开始生成传送门
            PortalData portalData = _portalManager.StartPortalGeneration(portalType, portalColor, preferredSlot);
            if (portalData == null)
            {
                Debug.LogError("[PortalSystem] 传送门生成失败");
                return null;
            }
            
            // 配置PortalSlot的VFX
            ConfigurePortalSlotVfx(portalData.portalSlot, vfxConfig);
            
            // 设置传送门对象
            SetPortalObject(portalData, portalNumber);
            
            // 更新传送门颜色
            UpdatePortalColor(portalData, portalColor);
            
            // 保存传送门数据
            if (portalNumber == 1)
            {
                _portal1Data = portalData;
            }
            else if (portalNumber == 2)
            {
                _portal2Data = portalData;
            }
            
            Debug.Log($"[PortalSystem] 生成传送门: {portalType} {portalColor} 编号{portalNumber}");
            
            return portalData;
        }
        
        /// <summary>
        /// 开始传送门前摇
        /// </summary>
        /// <param name="portalNumber">传送门编号(1或2)</param>
        /// <param name="telegraphDuration">前摇持续时间</param>
        public void StartPortalTelegraphing(int portalNumber, float telegraphDuration)
        {
            PortalData portalData = GetPortalData(portalNumber);
            if (portalData == null)
            {
                Debug.LogWarning($"[PortalSystem] 传送门{portalNumber}不存在");
                return;
            }
            
            _portalManager.StartPortalTelegraphing(portalData, telegraphDuration);
        }
        
        /// <summary>
        /// 关闭传送门
        /// </summary>
        /// <param name="portalNumber">传送门编号(1或2)</param>
        public void ClosePortal(int portalNumber)
        {
            PortalData portalData = GetPortalData(portalNumber);
            if (portalData == null)
            {
                Debug.LogWarning($"[PortalSystem] 传送门{portalNumber}不存在");
                return;
            }
            
            _portalManager.ClosePortal(portalData);
            
            // 清除传送门数据
            if (portalNumber == 1)
            {
                _portal1Data = null;
            }
            else if (portalNumber == 2)
            {
                _portal2Data = null;
            }
        }
        
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
                Debug.LogWarning($"[PortalSystem] 传送门{portalNumber}不存在");
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
            
            Debug.Log($"[PortalSystem] 传送门{portalNumber}颜色更改为: {newColor}");
        }
        
        #endregion
        
        #region 私有方法
        
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
        /// 配置PortalSlot的VFX
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
            
            // 查找传送门的FrameColor组件或材质
            var frameColorComponent = portalData.portalObject.GetComponent<PortalFrameColor>();
            if (frameColorComponent)
            {
                frameColorComponent.SetColor(color);
            }
            else
            {
                // 如果没有FrameColor组件，直接修改材质
                var renderer = portalData.portalObject.GetComponent<Renderer>();
                if (renderer)
                {
                    Color materialColor = GetColorFromPortalColor(color);
                    renderer.material.color = materialColor;
                }
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
            return (portalNumber == 1) ? _portal1Data : _portal2Data;
        }
        
        #endregion
        
        #region 调试方法
        
        [Button("测试生成1号蓝色传送门")]
        public void TestGeneratePortal1Blue()
        {
            GeneratePortal(PortalType.Ceiling, PortalColor.Blue, 1);
        }
        
        [Button("测试生成2号橙色传送门")]
        public void TestGeneratePortal2Orange()
        {
            GeneratePortal(PortalType.Ground, PortalColor.Orange, 2);
        }
        
        [Button("测试1号传送门前摇")]
        public void TestTelegraphPortal1()
        {
            StartPortalTelegraphing(1, 2f);
        }
        
        [Button("测试2号传送门前摇")]
        public void TestTelegraphPortal2()
        {
            StartPortalTelegraphing(2, 2f);
        }
        
        [Button("更改1号传送门为巨型橙色")]
        public void TestChangePortal1Color()
        {
            ChangePortalColor(1, PortalColor.GiantOrange);
        }
        
        [Button("更改2号传送门为蓝色")]
        public void TestChangePortal2Color()
        {
            ChangePortalColor(2, PortalColor.Blue);
        }
        
        [Button("关闭1号传送门")]
        public void TestClosePortal1()
        {
            ClosePortal(1);
        }
        
        [Button("关闭2号传送门")]
        public void TestClosePortal2()
        {
            ClosePortal(2);
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
        
        [Tooltip("VFX颜色")]
        public Color vfxColor = Color.white;
    }
    
    /// <summary>
    /// 传送门边框颜色组件
    /// </summary>
    public class PortalFrameColor : MonoBehaviour
    {
        [Tooltip("边框材质")]
        public Material frameMaterial;
        
        /// <summary>
        /// 设置传送门颜色
        /// </summary>
        /// <param name="color">传送门颜色</param>
        public void SetColor(PortalColor color)
        {
            if (!frameMaterial) return;
            
            Color materialColor = GetColorFromPortalColor(color);
            frameMaterial.color = materialColor;
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
    }
}
