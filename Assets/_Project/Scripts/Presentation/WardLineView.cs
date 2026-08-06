using Morae.Game.Core;
using UnityEngine;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 결계선 (v0.6 — 표현 계층, 구독만 §1.2).
    ///
    /// 소금 넷을 잇는 선. **상시로 깔면 다다미 위의 UI 와이어처럼 보여서** 오히려 몰입을 깬다.
    /// 그래서 평소엔 알파 0으로 숨어 있다가, 주의 유도(프롤로그 "네 귀퉁이에 소금을 쌓았다")가
    /// 들어올 때만 떠올랐다 사라진다 — "네 점이 하나의 결계"를 한 번만 못 박고 물러나는 역할이다.
    ///
    /// 색은 흰색 계열 고정. 붉은 계열을 쓰면 전조(대응해야 하는 것)와 문법이 겹친다.
    /// </summary>
    public sealed class WardLineView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer[] segments;
        [SerializeField] private float maxAlpha = 0.4f;
        [SerializeField] private float fadeInSec = 0.25f;
        [SerializeField] private float holdSec = 2.2f;   // 마지막 반짝임 이후 유지
        [SerializeField] private float fadeOutSec = 1.4f;

        private float _visibleUntil;
        private float _alpha;
        private bool _praying;   // 기도 중에는 계속 보인다 — 빛이 지나갈 길이 안 보이면 흐름이 안 읽힌다

        private void OnEnable()
        {
            GameEvents.SaltAttentionRequested += HandleAttention;
            GameEvents.PrayerChannelChanged += HandlePrayer;
        }

        private void OnDisable()
        {
            GameEvents.SaltAttentionRequested -= HandleAttention;
            GameEvents.PrayerChannelChanged -= HandlePrayer;
        }

        private void Start() => Apply(0f);

        private void HandleAttention(int corner, float seconds)
            => _visibleUntil = Mathf.Max(_visibleUntil, Time.time + seconds + holdSec);

        private void HandlePrayer(float progress01, int aimedCorner)
        {
            _praying = progress01 > 0f;
            // 기도가 끝나도 잔상처럼 잠깐 남긴다 — 빛이 닿은 자리가 곧바로 사라지면 인과가 끊긴다
            if (!_praying) _visibleUntil = Mathf.Max(_visibleUntil, Time.time + holdSec * 0.5f);
        }

        private void Update()
        {
            bool show = _praying || Time.time < _visibleUntil;
            float speed = show
                ? 1f / Mathf.Max(0.01f, fadeInSec)
                : 1f / Mathf.Max(0.01f, fadeOutSec);
            float target = show ? 1f : 0f;
            float next = Mathf.MoveTowards(_alpha, target, speed * Time.deltaTime);
            if (Mathf.Approximately(next, _alpha)) return;   // 정지 상태에서는 렌더러를 건드리지 않는다
            _alpha = next;
            Apply(_alpha);
        }

        private void Apply(float a01)
        {
            if (segments == null) return;
            for (int i = 0; i < segments.Length; i++)
            {
                SpriteRenderer sr = segments[i];
                if (sr == null) continue;
                Color c = sr.color;
                c.a = maxAlpha * a01;
                sr.color = c;
                sr.enabled = c.a > 0.001f;
            }
        }
    }
}
