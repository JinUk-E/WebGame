using Morae.Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 심장 UI (하단 중앙 반투명 — 명세 §2 "비네트+심박"의 시각 절반. 구독만: SanityChanged/UrgeChanged).
    /// 이성이 낮을수록 빠르고 크게 뛰고 색이 짙어진다. 요의 중에는 탁한 황색 점멸 — 회복 무효의 상시 표시.
    /// 박동은 unscaledTime — F1 배속에 심박이 끌려가지 않는다 (연출 계층 시간).
    /// </summary>
    public sealed class HeartView : MonoBehaviour
    {
        [SerializeField] private Image heart;
        [SerializeField] private float baseAlpha = 0.45f;
        [SerializeField] private Color calmColor = new Color(0.55f, 0.16f, 0.16f);
        [SerializeField] private Color panicColor = new Color(0.95f, 0.1f, 0.08f);
        [SerializeField] private Color urgeColor = new Color(0.8f, 0.65f, 0.15f);
        [SerializeField] private float minBpm = 48f;   // 이성 100
        [SerializeField] private float maxBpm = 140f;  // 이성 0 직전
        [SerializeField] private float minPulseScale = 0.05f;
        [SerializeField] private float maxPulseScale = 0.22f;
        [SerializeField] private float urgeBlinkHz = 2.2f;

        private float _sanity01 = 1f;
        private bool _urge;
        private float _phase;

        private void OnEnable()
        {
            GameEvents.SanityChanged += HandleSanityChanged;
            GameEvents.UrgeChanged += HandleUrgeChanged;
        }

        private void OnDisable()
        {
            GameEvents.SanityChanged -= HandleSanityChanged;
            GameEvents.UrgeChanged -= HandleUrgeChanged;
        }

        private void HandleSanityChanged(float s01) => _sanity01 = s01;

        private void HandleUrgeChanged(bool active) => _urge = active;

        private void Update()
        {
            if (heart == null) return;

            float fear = 1f - _sanity01;
            float bpm = Mathf.Lerp(minBpm, maxBpm, fear);
            _phase += bpm / 60f * Time.unscaledDeltaTime;

            // 수축기 펄스 — sin 양의 반주기를 네제곱해 "툭" 치는 박동 모양
            float beat = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(_phase * 2f * Mathf.PI)), 4f);
            float amp = Mathf.Lerp(minPulseScale, maxPulseScale, fear);
            heart.transform.localScale = Vector3.one * (1f + amp * beat);

            Color color = Color.Lerp(calmColor, panicColor, fear);
            float alpha = baseAlpha + 0.25f * beat * fear; // 위급할수록 박동 순간 또렷해진다
            if (_urge)
            {
                float blink = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * urgeBlinkHz * 2f * Mathf.PI);
                color = Color.Lerp(color, urgeColor, 0.4f + 0.5f * blink);
            }
            color.a = alpha;
            heart.color = color;
        }
    }
}
