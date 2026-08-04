using Morae.Game.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 이성의 유일한 표현 — 비네트 강도 (architecture §1.1 체감 피드백. 심박 템포는 SoundRouter와 함께 Epic 2).
    /// 표현 계층: GameEvents.SanityChanged 구독만 — 게임플레이를 호출하지 않는다.
    /// volume.profile(런타임 복제본)에만 쓴다 — sharedProfile(SO 에셋)에 쓰면 에디터에서 영구 반영되는 함정.
    /// 강도 값은 표현 컴포넌트의 SerializeField (표현 계층 값은 표현 컴포넌트에 — §7.2 일관).
    /// </summary>
    public sealed class SanityFeedback : MonoBehaviour
    {
        [SerializeField] private Volume volume;
        [SerializeField] private float calmIntensity = 0.35f;   // 이성 100
        [SerializeField] private float panicIntensity = 0.68f;  // 이성 0
        [SerializeField] private float lerpSpeed = 0.5f;        // 강도/s — 급락 시에도 스르륵 조여든다

        private Vignette _vignette;
        private float _target;

        private void Awake()
        {
            _target = calmIntensity;
            if (volume == null || !volume.profile.TryGet(out _vignette)) // .profile = 런타임 인스턴스 (에셋 보호)
            {
                Debug.LogWarning("[SANITY-FX] Volume/Vignette 미배선 — 비네트 피드백 비활성", this);
            }
        }

        private void OnEnable() => GameEvents.SanityChanged += HandleSanityChanged;
        private void OnDisable() => GameEvents.SanityChanged -= HandleSanityChanged;

        private void HandleSanityChanged(float sanity01)
        {
            _target = Mathf.Lerp(panicIntensity, calmIntensity, sanity01);
        }

        private void Update()
        {
            if (_vignette == null) return;
            float current = _vignette.intensity.value;
            if (Mathf.Approximately(current, _target)) return;
            _vignette.intensity.value = Mathf.MoveTowards(current, _target, lerpSpeed * Time.deltaTime);
        }
    }
}
