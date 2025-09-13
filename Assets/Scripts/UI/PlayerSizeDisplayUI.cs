using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Xuwu.Character;

namespace Xuwu.UI
{
    /// <summary>
    /// 玩家体型显示UI - 显示当前体型相对于初始体型的百分比
    /// </summary>
    public class PlayerSizeDisplayUI : MonoBehaviour
    {
        [Header("UI组件")]
        [SerializeField] private Text sizeText;
        
        [Header("显示设置")]
        [SerializeField] private string displayFormat = "{0:F1}%";
        [SerializeField] private float animationDuration = 0.5f;
        [SerializeField] private AnimationCurve easeOutCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        [Header("颜色设置")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color maxRedColor = Color.red;
        [SerializeField] private int redColorStartLevel = 4; // 开始变红的等级
        [SerializeField] private float maxRedLevel = 4.5f; // 最大红色等级
        
        [Header("调试")]
        [SerializeField] private bool showDebugLogs = false;
        
        private CharacterSizeController _sizeController;
        private float _currentDisplayValue = 100f;
        private float _targetValue = 100f;
        private Coroutine _animationCoroutine;
        
        private void Start()
        {
            InitializeComponents();
            UpdateDisplay();
        }
        
        private void Update()
        {
            if (_sizeController != null)
            {
                UpdateSizeDisplay();
            }
        }
        
        /// <summary>
        /// 初始化组件
        /// </summary>
        private void InitializeComponents()
        {
            // 获取CharacterSizeController
            _sizeController = FindObjectOfType<CharacterSizeController>();
            if (_sizeController == null)
            {
                Debug.LogError("[PlayerSizeDisplayUI] 未找到CharacterSizeController组件！");
                return;
            }
            
            // 如果没有指定Text组件，尝试从当前GameObject获取
            if (sizeText == null)
            {
                sizeText = GetComponent<Text>();
                if (sizeText == null)
                {
                    Debug.LogError("[PlayerSizeDisplayUI] 未找到Text组件！请在Inspector中指定sizeText。");
                    return;
                }
            }
            
            if (showDebugLogs)
            {
                Debug.Log("[PlayerSizeDisplayUI] 组件初始化完成");
            }
        }
        
        /// <summary>
        /// 更新体型显示
        /// </summary>
        private void UpdateSizeDisplay()
        {
            // 获取当前体型等级
            CharacterSizeLevel currentLevel = _sizeController.GetCurrentSizeLevel();
            int limitBreakerLevel = _sizeController.GetCurrentLimitBreakerLevel();
            
            // 计算总等级（4级时加上限制器突破等级的小数部分）
            float totalLevel = (int)currentLevel;
            if (currentLevel == CharacterSizeLevel.LimitBreaker)
            {
                totalLevel += limitBreakerLevel * 0.1f; // 4.1, 4.2, 4.3, 4.4, 4.5
            }
            
            // 确定目标显示值
            float targetDisplayValue;
            if (totalLevel >= maxRedLevel)
            {
                // 4.5级时显示固定1000%
                targetDisplayValue = 1000f;
            }
            else
            {
                // 其他等级显示实际体型百分比
                float currentSizeScale = _sizeController.GetCurrentSizeScale();
                float initialSizeScale = 1f; // 初始体型缩放为1
                targetDisplayValue = (currentSizeScale / initialSizeScale) * 100f;
            }
            
            if (showDebugLogs)
            {
                Debug.Log($"[PlayerSizeDisplayUI] 当前体型等级: {currentLevel}, 限制器突破等级: {limitBreakerLevel}, 总等级: {totalLevel}, 目标显示值: {targetDisplayValue}%");
            }
            
            // 如果目标值发生变化，启动动画
            if (!Mathf.Approximately(_targetValue, targetDisplayValue))
            {
                _targetValue = targetDisplayValue;
                StartSizeAnimation();
            }
            
            // 更新字体颜色
            UpdateTextColor(totalLevel);
        }
        
        /// <summary>
        /// 启动体型动画
        /// </summary>
        private void StartSizeAnimation()
        {
            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
            }
            
            _animationCoroutine = StartCoroutine(AnimateSizeChange());
        }
        
        /// <summary>
        /// 体型变化动画协程
        /// </summary>
        private IEnumerator AnimateSizeChange()
        {
            float startValue = _currentDisplayValue;
            float elapsedTime = 0f;
            
            if (showDebugLogs)
            {
                Debug.Log($"[PlayerSizeDisplayUI] 开始动画: {startValue:F1}% -> {_targetValue:F1}%");
            }
            
            while (elapsedTime < animationDuration)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / animationDuration;
                
                // 应用easeout曲线
                float easedProgress = easeOutCurve.Evaluate(progress);
                
                // 插值计算当前显示值
                _currentDisplayValue = Mathf.Lerp(startValue, _targetValue, easedProgress);
                
                // 更新UI显示
                UpdateDisplay();
                
                yield return null;
            }
            
            // 确保最终值准确
            _currentDisplayValue = _targetValue;
            UpdateDisplay();
            
            if (showDebugLogs)
            {
                Debug.Log($"[PlayerSizeDisplayUI] 动画完成: {_currentDisplayValue:F1}%");
            }
            
            _animationCoroutine = null;
        }
        
        /// <summary>
        /// 更新显示文本
        /// </summary>
        private void UpdateDisplay()
        {
            if (sizeText != null)
            {
                // 直接使用当前显示值，因为动画系统已经处理了目标值的过渡
                sizeText.text = string.Format(displayFormat, _currentDisplayValue);
            }
        }
        
        /// <summary>
        /// 更新文本颜色
        /// </summary>
        /// <param name="totalLevel">总等级</param>
        private void UpdateTextColor(float totalLevel)
        {
            if (sizeText == null) return;
            
            // 如果达到4.5级，使用最大红色
            if (totalLevel >= maxRedLevel)
            {
                sizeText.color = maxRedColor;
                return;
            }
            
            // 如果等级小于开始变红的等级，使用正常颜色
            if (totalLevel < redColorStartLevel)
            {
                sizeText.color = normalColor;
                return;
            }
            
            // 在4级到4.5级之间，计算红色渐变
            float redProgress = (totalLevel - redColorStartLevel) / (maxRedLevel - redColorStartLevel);
            redProgress = Mathf.Clamp01(redProgress);
            
            // 插值计算颜色
            Color currentColor = Color.Lerp(normalColor, maxRedColor, redProgress);
            sizeText.color = currentColor;
            
            if (showDebugLogs)
            {
                Debug.Log($"[PlayerSizeDisplayUI] 等级: {totalLevel:F1}, 红色进度: {redProgress:F2}, 颜色: {currentColor}");
            }
        }
        
        /// <summary>
        /// 手动设置显示值（用于测试）
        /// </summary>
        [ContextMenu("测试 - 设置为50%")]
        public void TestSet50Percent()
        {
            _targetValue = 50f;
            StartSizeAnimation();
        }
        
        /// <summary>
        /// 手动设置显示值（用于测试）
        /// </summary>
        [ContextMenu("测试 - 设置为200%")]
        public void TestSet200Percent()
        {
            _targetValue = 200f;
            StartSizeAnimation();
        }
        
        /// <summary>
        /// 手动设置显示值（用于测试）
        /// </summary>
        [ContextMenu("测试 - 设置为100%")]
        public void TestSet100Percent()
        {
            _targetValue = 100f;
            StartSizeAnimation();
        }
        
        /// <summary>
        /// 测试颜色变化（用于测试）
        /// </summary>
        [ContextMenu("测试 - 模拟4.0级")]
        public void TestSimulateLevel4()
        {
            UpdateTextColor(4.0f);
        }
        
        /// <summary>
        /// 测试颜色变化（用于测试）
        /// </summary>
        [ContextMenu("测试 - 模拟4.25级")]
        public void TestSimulateLevel4_25()
        {
            UpdateTextColor(4.25f);
        }
        
        /// <summary>
        /// 测试颜色变化（用于测试）
        /// </summary>
        [ContextMenu("测试 - 模拟4.5级")]
        public void TestSimulateLevel4_5()
        {
            UpdateTextColor(4.5f);
        }
        
        private void OnDestroy()
        {
            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
            }
        }
    }
}
