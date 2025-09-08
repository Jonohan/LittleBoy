using UnityEngine;
using UnityEngine.Rendering;

namespace Xuwu.FourDimensionalPortals.Demo
{
    [DefaultExecutionOrder(-500)]
    [ExecuteAlways]
    public class AutoLoadPipeline : MonoBehaviour
    {
        [SerializeField] private RenderPipelineAsset _pipelineAsset;

        private bool _overridePipeline;
        private RenderPipelineAsset _previousDefaultPipelineAsset;
        private int _previousQualityLevel;
        private RenderPipelineAsset _previousQualityPipelineAsset;

        private void OnEnable()
        {
            UpdatePipeline();
        }

        private void OnDisable()
        {
            ResetPipeline();
        }

        private void UpdatePipeline()
        {
            _overridePipeline = GraphicsSettings.defaultRenderPipeline != _pipelineAsset || QualitySettings.renderPipeline != _pipelineAsset;

            if (!_overridePipeline)
                return;

            _previousDefaultPipelineAsset = GraphicsSettings.defaultRenderPipeline;
            _previousQualityLevel = QualitySettings.GetQualityLevel();
            _previousQualityPipelineAsset = QualitySettings.renderPipeline;

            GraphicsSettings.defaultRenderPipeline = _pipelineAsset;
            QualitySettings.renderPipeline = _pipelineAsset;
        }

        private void ResetPipeline()
        {
            if (!_overridePipeline)
                return;

            GraphicsSettings.defaultRenderPipeline = _previousDefaultPipelineAsset;
            QualitySettings.SetQualityLevel(_previousQualityLevel);
            QualitySettings.renderPipeline = _previousQualityPipelineAsset;

            _overridePipeline = false;
            _previousDefaultPipelineAsset = null;
            _previousQualityLevel = 0;
            _previousQualityPipelineAsset = null;
        }
    }
}
