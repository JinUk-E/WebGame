using Morae.Game.Core;
using UnityEngine;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 불상 하이라이트 (v0.6 — 표현 계층, 구독만 §1.2).
    ///
    /// 학습 구간에서 "막는 법은 불상 앞에서 비는 것"이라고 **말로만** 알려주면, 처음 온 플레이어는
    /// 그 불상이 화면 어디에 있는 물건인지 모른 채 문장을 듣는다. 후광을 한 번 띄워 대사와 대상을 붙인다.
    ///
    /// 촛불 밝기는 건드리지 않는다 — 그건 LightingController가 소유한다(감광 예외②).
    /// 여기서는 후광 스프라이트만 켠다. 소유권을 나눠야 두 컴포넌트가 같은 값을 놓고 싸우지 않는다.
    /// </summary>
    public sealed class AltarHighlightView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer halo;
        [SerializeField] private float maxAlpha = 0.75f;
        [SerializeField] private float fadeInSec = 0.3f;
        [SerializeField] private float fadeOutSec = 0.9f;
        [SerializeField] private float pulseHz = 0.9f;
        [SerializeField] private float pulseDepth = 0.25f;
        [SerializeField] private float scaleBreath = 0.06f;

        private float _visibleUntil;
        private float _alpha;
        private Vector3 _baseScale = Vector3.one;

        private void Awake()
        {
            if (halo != null) _baseScale = halo.transform.localScale;
        }

        private void OnEnable() => GameEvents.AltarAttentionRequested += HandleRequest;
        private void OnDisable() => GameEvents.AltarAttentionRequested -= HandleRequest;

        private void Start() => Apply(0f);

        private void HandleRequest(float seconds)
            => _visibleUntil = Mathf.Max(_visibleUntil, Time.time + Mathf.Max(0f, seconds));

        private void Update()
        {
            if (halo == null) return;
            bool show = Time.time < _visibleUntil;
            float speed = 1f / Mathf.Max(0.01f, show ? fadeInSec : fadeOutSec);
            _alpha = Mathf.MoveTowards(_alpha, show ? 1f : 0f, speed * Time.deltaTime);
            Apply(_alpha);
        }

        private void Apply(float a01)
        {
            if (halo == null) return;
            if (a01 <= 0.001f)
            {
                if (halo.enabled) halo.enabled = false;
                return;
            }
            if (!halo.enabled) halo.enabled = true;

            float breathe = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseHz * 2f * Mathf.PI);
            Color c = halo.color;
            c.a = maxAlpha * a01 * (1f - pulseDepth + pulseDepth * breathe);
            halo.color = c;
            float s = 1f + scaleBreath * breathe * a01;
            halo.transform.localScale = _baseScale * s;
        }
    }
}
