using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace Xuwu.UI
{
    /// <summary>
    /// Boss死亡文字UI - 淡入淡出效果
    /// </summary>
    public class BossDeathTextUI : MonoBehaviour
    {
        [Header("UI组件")]
        [SerializeField] private Text deathText;
        [SerializeField] private CanvasGroup canvasGroup;
        
        [Header("显示设置")]
        [SerializeField] private float fadeInDuration = 1f;
        [SerializeField] private float fadeOutDuration = 1f;
        [SerializeField] private float displayDuration = 2f; // 完全显示的时间
        
        [Header("样式设置")]
        // 字体样式由Inspector中的Text组件设置决定，不在此处配置
        
        private bool _isShowing = false;
        private Coroutine _fadeCoroutine;
        
        private void Start()
        {
            InitializeComponents();
            SetupText();
            HideText();
        }
        
        /// <summary>
        /// 初始化组件
        /// </summary>
        private void InitializeComponents()
        {
            // 获取或创建Text组件
            if (deathText == null)
            {
                deathText = GetComponent<Text>();
                if (deathText == null)
                {
                    deathText = gameObject.AddComponent<Text>();
                }
            }
            
            // 获取或创建CanvasGroup组件
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }
            
            // 设置CanvasGroup初始状态
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        
        /// <summary>
        /// 设置文字样式
        /// </summary>
        private void SetupText()
        {
            // 不修改Text组件的任何内容
            // 文本内容和样式完全由Inspector中的Text组件设置决定
        }
        
        /// <summary>
        /// 显示Boss死亡文字
        /// </summary>
        public void ShowBossDeathText()
        {
            if (_isShowing) return;
            
            // 确保GameObject是激活状态
            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(true);
            }
            
            _isShowing = true;
            
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
            }
            
            _fadeCoroutine = StartCoroutine(FadeInOutCoroutine());
        }
        
        /// <summary>
        /// 淡入淡出协程
        /// </summary>
        private IEnumerator FadeInOutCoroutine()
        {
            // 淡入阶段
            float elapsedTime = 0f;
            while (elapsedTime < fadeInDuration)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / fadeInDuration;
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, progress);
                yield return null;
            }
            
            canvasGroup.alpha = 1f;
            
            // 完全显示阶段
            yield return new WaitForSeconds(displayDuration);
            
            // 淡出阶段
            elapsedTime = 0f;
            while (elapsedTime < fadeOutDuration)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / fadeOutDuration;
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, progress);
                yield return null;
            }
            
            canvasGroup.alpha = 0f;
            _isShowing = false;
            _fadeCoroutine = null;
        }
        
        /// <summary>
        /// 隐藏文字
        /// </summary>
        private void HideText()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
            _isShowing = false;
        }
        
        /// <summary>
        /// 测试显示死亡文字
        /// </summary>
        [ContextMenu("测试 - 显示Boss死亡文字")]
        public void TestShowDeathText()
        {
            ShowBossDeathText();
        }
        
        private void OnDestroy()
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
            }
        }
    }
}
