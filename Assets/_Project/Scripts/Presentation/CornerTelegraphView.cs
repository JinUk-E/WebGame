using Morae.Game.Core;
using Morae.Game.Data;
using UnityEngine;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 전조의 촉각적 연출 (v0.6 — 표현 계층, 구독만 §1.2).
    ///
    /// 기존 전조는 **붉은 점멸 + 방향음**뿐이라 "저기서 뭔가 온다"가 소리에 전적으로 얹혀 있었다.
    /// 소리를 놓치면 아무것도 못 본 것과 같다. 그래서 같은 사건을 세 감각으로 겹쳐 준다:
    ///   ① 그 귀퉁이에 **어둠이 고인다** — 결계가 밀리고 있다는 공간적 신호
    ///   ② 소금이 **흔들린다** — 두드림이 닿는 지점이 어디인지 못 박는다
    ///   ③ 밖에서 **두드린다** (박자는 <see cref="KnockRhythm"/>, 소리는 SoundManager)
    /// 어둠은 소금 **아래**(정렬 1)에 깔린다 — 위에 덮으면 정작 봐야 할 소금 상태를 가린다.
    ///
    /// 흔들림은 소금 렌더러의 로컬 위치만 만진다. 색은 SaltCornersView가 소유한다 — 소유권을 나눠
    /// 두 컴포넌트가 같은 속성을 놓고 싸우지 않게 한다.
    /// </summary>
    public sealed class CornerTelegraphView : MonoBehaviour
    {
        [SerializeField] private Transform[] saltTransforms = new Transform[CornerIndex.Count];
        [SerializeField] private SpriteRenderer[] gloomRenderers = new SpriteRenderer[CornerIndex.Count];

        [SerializeField] private float gloomMaxAlpha = 0.82f;
        [SerializeField] private float gloomBaseScale = 1.15f;
        [SerializeField] private float gloomImpactScale = 0.22f;   // 두드릴 때 부풀어 오르는 정도
        [SerializeField] private float shakeAmplitude = 0.055f;    // 유닛 — 크면 소금이 굴러다니는 것처럼 보인다
        [SerializeField] private float shakeHz = 26f;
        [SerializeField] private float fadeOutSec = 0.5f;

        private readonly float[] _startTime = new float[CornerIndex.Count];
        private readonly float[] _duration = new float[CornerIndex.Count];
        private readonly float[] _endTime = new float[CornerIndex.Count];   // 판정 후 잦아드는 구간
        private readonly bool[] _active = new bool[CornerIndex.Count];
        private Vector3[] _restPos;

        private void Awake()
        {
            _restPos = new Vector3[CornerIndex.Count];
            for (int i = 0; i < CornerIndex.Count; i++)
            {
                if (saltTransforms != null && i < saltTransforms.Length && saltTransforms[i] != null)
                    _restPos[i] = saltTransforms[i].localPosition;
                if (gloomRenderers != null && i < gloomRenderers.Length && gloomRenderers[i] != null)
                {
                    SetAlpha(gloomRenderers[i], 0f);
                    gloomRenderers[i].enabled = false;
                }
            }
        }

        private void OnEnable()
        {
            GameEvents.AttackTelegraphStarted += HandleTelegraph;
            GameEvents.AttackResolved += HandleResolved;
        }

        private void OnDisable()
        {
            GameEvents.AttackTelegraphStarted -= HandleTelegraph;
            GameEvents.AttackResolved -= HandleResolved;
        }

        private void HandleTelegraph(int corner, float duration)
        {
            if (!Valid(corner)) return;
            _startTime[corner] = Time.time;
            _duration[corner] = Mathf.Max(0.01f, duration);
            _endTime[corner] = 0f;
            _active[corner] = true;
        }

        private void HandleResolved(int corner, bool countered)
        {
            if (!Valid(corner)) return;
            // 상쇄든 오염이든 어둠은 물러난다 — 결과의 옳고 그름은 소금 색·플래시가 말한다
            _endTime[corner] = Time.time;
        }

        private void Update()
        {
            for (int i = 0; i < CornerIndex.Count; i++)
            {
                if (!_active[i]) continue;

                float elapsed = Time.time - _startTime[i];
                float k = 1f;   // 전체 세기 (판정 후 감쇠)
                if (_endTime[i] > 0f)
                {
                    float since = Time.time - _endTime[i];
                    k = 1f - Mathf.Clamp01(since / Mathf.Max(0.01f, fadeOutSec));
                    if (k <= 0f)
                    {
                        Stop(i);
                        continue;
                    }
                }
                else if (elapsed > _duration[i] + 1.5f)
                {
                    // 판정 이벤트를 놓친 경우의 안전망 — 어둠이 영원히 남는 것보다 낫다
                    Stop(i);
                    continue;
                }

                // 어둠은 전조 진행에 따라 차오르고, 두드릴 때마다 한 번씩 부푼다
                float grow = Mathf.Clamp01(elapsed / (_duration[i] * 0.6f));
                float impact = KnockRhythm.ImpactEnvelope(elapsed, _duration[i]);
                ApplyGloom(i, grow * k, impact * k);
                ApplyShake(i, impact * k);
            }
        }

        private void ApplyGloom(int i, float grow, float impact)
        {
            SpriteRenderer sr = gloomRenderers != null && i < gloomRenderers.Length ? gloomRenderers[i] : null;
            if (sr == null) return;
            if (!sr.enabled) sr.enabled = true;
            SetAlpha(sr, gloomMaxAlpha * Mathf.Clamp01(grow * (0.75f + 0.25f * impact)));
            float s = gloomBaseScale * (1f + gloomImpactScale * impact);
            sr.transform.localScale = new Vector3(s, s, 1f);
        }

        private void ApplyShake(int i, float impact)
        {
            Transform t = saltTransforms != null && i < saltTransforms.Length ? saltTransforms[i] : null;
            if (t == null) return;
            if (impact <= 0.001f)
            {
                t.localPosition = _restPos[i];
                return;
            }
            // 가로세로 주기를 다르게 — 같은 주기면 대각선으로 미끄러지는 것처럼 보인다
            float ph = Time.time * shakeHz * 2f * Mathf.PI;
            float dx = Mathf.Sin(ph) * shakeAmplitude * impact;
            float dy = Mathf.Sin(ph * 1.37f + 1.1f) * shakeAmplitude * 0.6f * impact;
            t.localPosition = _restPos[i] + new Vector3(dx, dy, 0f);
        }

        private void Stop(int i)
        {
            _active[i] = false;
            if (saltTransforms != null && i < saltTransforms.Length && saltTransforms[i] != null)
                saltTransforms[i].localPosition = _restPos[i];
            if (gloomRenderers != null && i < gloomRenderers.Length && gloomRenderers[i] != null)
            {
                SetAlpha(gloomRenderers[i], 0f);
                gloomRenderers[i].enabled = false;
            }
        }

        private static void SetAlpha(SpriteRenderer sr, float a)
        {
            Color c = sr.color;
            c.a = a;
            sr.color = c;
        }

        private bool Valid(int corner) => corner >= 0 && corner < CornerIndex.Count;
    }
}
