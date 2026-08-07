using Morae.Game.Core;
using Morae.Game.Data;
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
        // v0.7 — 소금 뿌리는 동안의 가산 강도. HeartView와 같은 이유로 값 추종만으로는 절대 체감되지 않는다:
        //   전 구간 폭이 0.33인데 드레인 2/s면 초당 0.0066 변화라 육안 한계 이하다.
        //   상태 전이는 별도 속도로 즉시 반응시킨다 (0.05초 도달).
        [SerializeField] private float saltingExtraIntensity = 0.12f;
        [SerializeField] private float saltingLerpSpeed = 2.5f;
        // 이불 속 — 시야가 열린다. "안정된 느낌"의 나머지 절반.
        [SerializeField] private float blanketRelief = 0.15f;
        [SerializeField] private float blanketLerpSpeed = 1.2f;
        // v0.7 이산 손실 펀치 — 값 추종(느림)과 별개로 "방금 잃었다"를 순간에 찍는다.
        // 손실 크기에 비례하되 상한을 둔다: 큰 손실이라고 화면이 완전히 닫히면 안 된다.
        [SerializeField] private float lossPunchPerUnit = 1.6f;   // lost01 1.0당 얹는 강도
        [SerializeField] private float lossPunchMax = 0.22f;
        [SerializeField] private float lossPunchDecaySec = 0.55f;

        private Vignette _vignette;
        private float _sanityTarget;
        private bool _salting;
        private bool _inBlanket;
        private float _salting01;
        private float _blanket01;
        private float _lossPunch;

        private void Awake()
        {
            _sanityTarget = calmIntensity;
            if (volume == null || !volume.profile.TryGet(out _vignette)) // .profile = 런타임 인스턴스 (에셋 보호)
            {
                Debug.LogWarning("[SANITY-FX] Volume/Vignette 미배선 — 비네트 피드백 비활성", this);
            }
        }

        private void OnEnable()
        {
            GameEvents.SanityChanged += HandleSanityChanged;
            GameEvents.SanityLost += HandleSanityLost;
            GameEvents.SaltChannelChanged += HandleSaltChannel;
            GameEvents.PlayerStateChanged += HandlePlayerStateChanged;
        }

        private void OnDisable()
        {
            GameEvents.SanityChanged -= HandleSanityChanged;
            GameEvents.SanityLost -= HandleSanityLost;
            GameEvents.SaltChannelChanged -= HandleSaltChannel;
            GameEvents.PlayerStateChanged -= HandlePlayerStateChanged;
        }

        private void HandleSanityLost(float lost01)
            => _lossPunch = Mathf.Min(lossPunchMax, _lossPunch + lost01 * lossPunchPerUnit);

        private void HandleSanityChanged(float sanity01)
        {
            _sanityTarget = Mathf.Lerp(panicIntensity, calmIntensity, sanity01);
        }

        private void HandleSaltChannel(int corner, float progress01) => _salting = progress01 > 0f;

        private void HandlePlayerStateChanged(PlayerState state) => _inBlanket = state == PlayerState.InBlanket;

        private void Update()
        {
            if (_vignette == null) return;

            float dt = Time.deltaTime;
            // 상태 오버레이는 각자의 속도로 — 값 추종(느림)과 상태 전이(빠름)를 섞지 않는다
            _salting01 = Mathf.MoveTowards(_salting01, _salting ? 1f : 0f, saltingLerpSpeed * dt);
            _blanket01 = Mathf.MoveTowards(_blanket01, _inBlanket ? 1f : 0f, blanketLerpSpeed * dt);
            _lossPunch = Mathf.MoveTowards(_lossPunch, 0f, dt / Mathf.Max(0.01f, lossPunchDecaySec) * lossPunchMax);

            float current = _vignette.intensity.value;
            float baseValue = Mathf.MoveTowards(current - Overlay(), _sanityTarget, lerpSpeed * dt);
            float next = Mathf.Clamp01(baseValue + Overlay());
            if (Mathf.Approximately(current, next)) return;
            _vignette.intensity.value = next;
        }

        private float Overlay()
            => saltingExtraIntensity * _salting01 - blanketRelief * _blanket01 + _lossPunch;
    }
}
