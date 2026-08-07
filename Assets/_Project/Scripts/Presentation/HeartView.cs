using Morae.Game.Core;
using Morae.Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 심장 UI (하단 중앙 반투명). 이성이 낮을수록 빠르고 크게 뛰고 색이 짙어진다.
    /// 박동은 unscaledTime — F1 배속에 심박이 끌려가지 않는다 (연출 계층 시간).
    ///
    /// <para>
    /// <b>v0.7: 소금 뿌리는 동안의 즉시 반응.</b> 이성 값만 보면 절대 체감되지 않는다 —
    /// 드레인 2/s는 초당 <c>(140−48) × 0.02 = 1.84 bpm</c>이고, 홀드 1.5초 전체를 다 해도 2.8bpm이다.
    /// 값의 문제가 아니라 <b>매핑 기울기</b>의 문제라, lerp 속도를 아무리 올려도 안 보인다.
    /// 그래서 "지금 뿌리는 중"이라는 <b>상태</b>를 직접 받아 가산 오프셋을 얹고,
    /// 시작 프레임에는 위상을 수축기 직전으로 스냅해 <b>다음 박동을 즉시 터뜨린다</b>.
    /// 행위에 붙은 피드백이라 초당 델타 문제를 우회한다.
    /// </para>
    /// <para>
    /// 이불 안에서는 심박이 멎는다 — 사용자가 요구한 "안정된 느낌"의 절반이 이것이다.
    /// 알파 상승은 쓰지 않는다(사실상 밝기 축) — 크기와 주기로만 싣는다.
    /// </para>
    /// </summary>
    public sealed class HeartView : MonoBehaviour
    {
        [SerializeField] private Image heart;
        [SerializeField] private float baseAlpha = 0.45f;
        [SerializeField] private Color calmColor = new Color(0.55f, 0.16f, 0.16f);
        [SerializeField] private Color panicColor = new Color(0.95f, 0.1f, 0.08f);
        [SerializeField] private float minBpm = 48f;   // 이성 100
        [SerializeField] private float maxBpm = 140f;  // 이성 0 직전
        [SerializeField] private float minPulseScale = 0.05f;
        [SerializeField] private float maxPulseScale = 0.22f;
        // 뿌리는 동안 얹는 가산 bpm — 값 변화로는 못 만드는 즉시성을 여기서 만든다
        [SerializeField] private float saltingBpmBoost = 30f;
        [SerializeField] private float saltingBoostFadeSec = 0.8f;
        // 이불 안 — 심박이 잦아든다 (0이면 완전 정지)
        [SerializeField] private float blanketBpmScale = 0f;
        [SerializeField] private float blanketFadeSec = 0.6f;
        // TV — 이불보다 약한 진정. "켜면 조금 편해진다"가 몸으로 느껴져야 회복 수단인 줄 안다.
        // 0.8 = 20%만 느려진다 (이불의 완전 정지와 확실히 구분되는 정도)
        [SerializeField, Range(0f, 1f)] private float tvCalmWeight = 0.35f;
        [SerializeField] private float tvCalmScale = 0.8f;
        // 이산 손실 킥 — 박동 크기를 순간 부풀린다 (bpm이 아니라 진폭이라 한 박에 다 보인다)
        [SerializeField] private float lossKickPerUnit = 4f;
        [SerializeField] private float lossKickScale = 0.3f;
        [SerializeField] private float lossKickDecaySec = 0.6f;

        private float _sanity01 = 1f;
        private bool _salting;
        private float _saltingBoost01;
        private bool _inBlanket;
        private bool _tvOn;
        private float _blanket01;   // 0 = 밖, 1 = 이불 속 (전환 러프값)
        private float _calm01;      // 이불·TV를 합친 진정도 — 표현(알파·크기)에도 쓴다
        private float _lossKick;    // 이산 손실 직후 박동 진폭 가산 0~1
        private float _phase;

        private void OnEnable()
        {
            GameEvents.SanityChanged += HandleSanityChanged;
            GameEvents.SanityLost += HandleSanityLost;
            GameEvents.SaltChannelChanged += HandleSaltChannel;
            GameEvents.PlayerStateChanged += HandlePlayerStateChanged;
            GameEvents.TVToggled += HandleTVToggled;
        }

        private void OnDisable()
        {
            GameEvents.SanityChanged -= HandleSanityChanged;
            GameEvents.SanityLost -= HandleSanityLost;
            GameEvents.SaltChannelChanged -= HandleSaltChannel;
            GameEvents.PlayerStateChanged -= HandlePlayerStateChanged;
            GameEvents.TVToggled -= HandleTVToggled;
        }

        private void HandleTVToggled(bool isOn) => _tvOn = isOn;

        /// <summary>
        /// 이산 손실 순간 — 위상을 수축기 직전으로 스냅해 <b>그 프레임에 한 번 크게 뛴다</b>.
        /// 이성 값이 3%밖에 안 줄어도 "방금 잃었다"는 몸으로 전달된다.
        /// </summary>
        private void HandleSanityLost(float lost01)
        {
            _phase = Mathf.Floor(_phase) + 0.25f;
            _lossKick = Mathf.Min(1f, _lossKick + lost01 * lossKickPerUnit);
        }

        private void HandleSanityChanged(float s01) => _sanity01 = s01;

        private void HandlePlayerStateChanged(PlayerState state) => _inBlanket = state == PlayerState.InBlanket;

        private void HandleSaltChannel(int corner, float progress01)
        {
            bool salting = progress01 > 0f;
            if (salting && !_salting)
            {
                // 위상을 수축기 직전으로 스냅 — 누른 그 프레임에 박동이 터진다 (한 줄로 얻는 즉시성)
                _phase = Mathf.Floor(_phase) + 0.25f;
                _saltingBoost01 = 1f;
            }
            _salting = salting;
        }

        private void Update()
        {
            if (heart == null) return;

            // 이불 진입/이탈은 급전환하면 튄다 — 짧게 러프한다. 이게 "잦아든다"의 실체다
            _blanket01 = Mathf.MoveTowards(_blanket01, _inBlanket ? 1f : 0f,
                Time.unscaledDeltaTime / Mathf.Max(0.01f, blanketFadeSec));
            if (!_salting)
            {
                _saltingBoost01 = Mathf.MoveTowards(_saltingBoost01, 0f,
                    Time.unscaledDeltaTime / Mathf.Max(0.01f, saltingBoostFadeSec));
            }
            _lossKick = Mathf.MoveTowards(_lossKick, 0f,
                Time.unscaledDeltaTime / Mathf.Max(0.01f, lossKickDecaySec));

            // 이불(강)·TV(약)를 합친 진정도. 계산은 HeartRateModel이 소유한다 —
            // SoundManager의 심박음이 같은 식을 써야 눈과 귀가 어긋나지 않는다.
            _calm01 = HeartRateModel.Calm01(_blanket01, _tvOn, tvCalmWeight);
            float calmScale = _blanket01 > 0.5f ? blanketBpmScale : tvCalmScale;
            float bpm = HeartRateModel.Bpm(_sanity01, minBpm, maxBpm,
                _saltingBoost01, saltingBpmBoost, _calm01, calmScale);
            float fear = 1f - _sanity01;
            _phase += bpm / 60f * Time.unscaledDeltaTime;

            // 수축기 펄스 — sin 양의 반주기를 네제곱해 "툭" 치는 박동 모양
            float beat = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(_phase * 2f * Mathf.PI)), 4f);
            // 손실 킥은 bpm이 아니라 **진폭**에 얹는다 — 한 박 안에 다 보이므로 작은 손실도 놓치지 않는다
            float amp = Mathf.Lerp(minPulseScale, maxPulseScale, fear) + lossKickScale * _lossKick;
            heart.transform.localScale = Vector3.one * (1f + amp * beat);

            Color color = Color.Lerp(calmColor, panicColor, fear);
            // 이불 속에서는 심장 자체가 물러난다 — 박동이 멎었는데 색만 남아 있으면 안정감이 안 온다
            color.a = baseAlpha * Mathf.Lerp(1f, 0.35f, _calm01);
            heart.color = color;
        }
    }
}
