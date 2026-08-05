using UnityEngine;
using UnityEngine.UI;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 타이틀 볼륨 슬라이더 (architecture §8.2 — WebGL 볼륨은 AudioListener.volume 단일 노브).
    /// AudioListener.volume은 static — 씬 리로드(재시작)에도 유지되므로 Awake에서 현재값으로 초기화한다.
    /// 타이틀 루트가 비활성으로 시작하므로 Awake는 첫 Show 시점에 실행된다.
    /// </summary>
    public sealed class VolumeSliderView : MonoBehaviour
    {
        [SerializeField] private Slider slider;

        private void Awake()
        {
            if (slider == null) return;
            slider.SetValueWithoutNotify(AudioListener.volume);
            slider.onValueChanged.AddListener(HandleChanged);
        }

        private static void HandleChanged(float value) => AudioListener.volume = value;
    }
}
