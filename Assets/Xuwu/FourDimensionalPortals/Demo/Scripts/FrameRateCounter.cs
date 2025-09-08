using UnityEngine;
using UnityEngine.UI;

namespace Xuwu.FourDimensionalPortals.Demo
{
    public class FrameRateCounter : MonoBehaviour
    {
        [SerializeField] private Text _frameRateText;
        [SerializeField] private float _pollingTime = .5f;

        private float _accumulatedTime;
        private int _accumulatedFrameCount;

        private void Update()
        {
            _accumulatedTime += Time.unscaledDeltaTime;
            _accumulatedFrameCount++;

            if (_accumulatedTime > _pollingTime)
            {
                _frameRateText.text = Mathf.RoundToInt(_accumulatedFrameCount / _accumulatedTime).ToString();
                _accumulatedTime = 0f;
                _accumulatedFrameCount = 0;
            }
        }
    }
}
