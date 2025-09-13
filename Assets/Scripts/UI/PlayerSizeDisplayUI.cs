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
            // 根据玩家体型等级获取对应的缩放值
            float currentSizeScale = _sizeController.GetCurrentSizeScale();
            float initialSizeScale = 1f; // 初始体型缩放为1
            float sizePercentage = (currentSizeScale / initialSizeScale) * 100f;
            
            if (showDebugLogs)
            {
                Debug.Log($"[PlayerSizeDisplayUI] 当前体型等级: {_sizeController.GetCurrentSizeLevel()}, 体型缩放: {currentSizeScale}, 体型百分比: {sizePercentage}%");
            }
            
            // 如果目标值发生变化，启动动画
            if (!Mathf.Approximately(_targetValue, sizePercentage))
            {
                _targetValue = sizePercentage;
                StartSizeAnimation();
            }
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
                sizeText.text = string.Format(displayFormat, _currentDisplayValue);
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
        
        private void OnDestroy()
        {
            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
            }
        }
    }
}
