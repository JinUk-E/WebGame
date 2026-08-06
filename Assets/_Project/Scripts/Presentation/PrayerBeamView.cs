using System.Collections.Generic;
using Morae.Game.Core;
using Morae.Game.Data;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 기도 빛줄기 (v0.6 — 표현 계층, 구독만 §1.2).
    ///
    /// **왜 필요한가**: 전조는 귀퉁이에서 울리는데 대응은 불상 앞에서 한다. 그 둘을 잇는 그림이 없어서
    /// "왜 여기로 가야 하는지"가 안 읽혔다.
    ///
    /// **왜 고리를 타지 않고 직행하는가**: 한때 빛을 소금길(결계 고리)을 따라 돌게 했는데,
    /// 아래 귀퉁이를 겨누면 빛이 **위 귀퉁이를 지나가면서** 그쪽까지 정화되는 것처럼 보였다.
    /// 정화 대상은 정확히 하나여야 한다 — 그래서 지금은 불상에서 목표 귀퉁이로 곧장 간다.
    /// 다만 직선은 광선(무기)처럼 보이므로 완만한 활 모양으로 휘어 "기를 보내는" 인상을 유지한다.
    ///
    /// 진행률 = 빛이 간 거리. 도달하는 순간이 곧 상쇄 순간이라 채널 진행 바를 따로 볼 필요가 없다.
    /// 머리에 달린 Light2D가 바닥을 실제로 밝히며 지나간다 — "빛이 흐른다"를 은유가 아니라 조명으로 보인다.
    /// 선은 조명을 받지 않는 머티리얼로 그린다 (감광이 심할수록 또렷해져야 하는 신호).
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class PrayerBeamView : MonoBehaviour
    {
        [SerializeField] private LineRenderer line;
        [SerializeField] private Light2D headLight;                 // 빛의 머리 — 지나가며 바닥을 밝힌다
        [SerializeField] private Transform altar;
        [SerializeField] private Transform[] cornerTransforms = new Transform[CornerIndex.Count];

        [SerializeField] private Color nearColor = new Color(1f, 0.88f, 0.5f, 0.7f);   // 불상 쪽 (꼬리)
        [SerializeField] private Color farColor = new Color(1f, 0.95f, 0.72f, 0.95f);  // 머리 쪽 — 가장 밝다
        [SerializeField] private float width = 0.14f;
        [SerializeField] private float minReach = 0.06f;
        [SerializeField] private float bow = 0.5f;                  // 활처럼 휘는 정도 (유닛 — 거리에 비례)
        [SerializeField] private float wobbleAmp = 0.09f;
        [SerializeField] private float wobbleHz = 0.7f;
        [SerializeField] private int pathSamples = 24;              // 경로 곡선 해상도
        [SerializeField] private int drawPoints = 48;               // 실제로 찍는 점 수 (고정 — 할당 없음)
        [SerializeField] private float headLightIntensity = 1.1f;
        [SerializeField] private float headLightRadius = 1.5f;

        private readonly List<Vector3> _path = new List<Vector3>(64);
        private Vector3[] _drawn;            // 길이 고정 버퍼 — 매 프레임 재할당하지 않는다

        private void Reset() => line = GetComponent<LineRenderer>();

        private void Awake()
        {
            if (line == null) line = GetComponent<LineRenderer>();
            _drawn = new Vector3[Mathf.Max(8, drawPoints)];
            SetupLine();
            Hide();
        }

        /// <summary>굵기·색 곡선은 코드가 소유한다 — 인스펙터에서 손으로 그린 커브는 조용히 깨진다.</summary>
        private void SetupLine()
        {
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 4;      // 끝이 뾰족하면 다시 각져 보인다
            line.numCornerVertices = 4;
            line.widthMultiplier = width;
            line.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.25f),  // 꼬리: 불상 쪽에서 가늘게 피어오른다
                new Keyframe(0.7f, 0.8f),
                new Keyframe(1f, 1.35f)); // 머리: 굵고 밝게 — 시선이 진행 방향을 따라간다
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(nearColor, 0f), new GradientColorKey(farColor, 1f) },
                new[] { new GradientAlphaKey(nearColor.a, 0f), new GradientAlphaKey(farColor.a, 1f) });
            line.colorGradient = grad;

            if (headLight != null)
            {
                headLight.color = farColor;
                headLight.intensity = 0f;
                headLight.enabled = false;
            }
        }

        private void OnEnable() => GameEvents.PrayerChannelChanged += HandlePrayer;
        private void OnDisable() => GameEvents.PrayerChannelChanged -= HandlePrayer;

        private void HandlePrayer(float progress01, int aimedCorner)
        {
            if (line == null || altar == null || progress01 <= 0f || !Valid(aimedCorner))
            {
                Hide();
                return;
            }

            BuildPath(altar.position, aimedCorner);
            if (_path.Count < 2)
            {
                Hide();
                return;
            }

            float reach = Mathf.Lerp(minReach, 1f, Mathf.Clamp01(progress01));
            if (!Reveal(reach))
            {
                Hide();
                return;
            }

            line.positionCount = _drawn.Length;
            line.SetPositions(_drawn);
            line.enabled = true;

            if (headLight != null)
            {
                Vector3 head = _drawn[_drawn.Length - 1];
                headLight.transform.position = head;
                headLight.pointLightOuterRadius = headLightRadius;
                headLight.pointLightInnerRadius = headLightRadius * 0.25f;
                // 도달 직전에 가장 밝다 — 상쇄가 "닿는 순간"으로 읽히게
                headLight.intensity = headLightIntensity * Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(progress01));
                headLight.enabled = true;
            }
        }

        /// <summary>
        /// 불상 → 목표 귀퉁이로 곧장. 다른 귀퉁이를 스치지 않는 게 이 함수의 존재 이유다 —
        /// 경유하는 순간 그 귀퉁이도 정화되는 것으로 읽힌다.
        /// 직선 대신 완만한 활로 휘어 광선이 아니라 "보내는 기"로 보이게 한다.
        /// </summary>
        private void BuildPath(Vector2 from, int corner)
        {
            _path.Clear();

            Vector2 to = cornerTransforms[corner].position;
            Vector2 delta = to - from;
            float len = delta.magnitude;
            if (len < 0.01f) return;

            Vector2 dir = delta / len;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            // 짧을 땐 덜 휜다 — 가까운 귀퉁이에서 같은 폭으로 휘면 과장돼 보인다
            float bowAmount = bow * Mathf.Clamp01(len / 6f);

            int seg = Mathf.Max(6, pathSamples);
            for (int i = 0; i <= seg; i++)
            {
                float t = (float)i / seg;
                float arc = Mathf.Sin(t * Mathf.PI);   // 양 끝은 정확히 불상·귀퉁이에 붙는다
                Vector2 p = from + delta * t + perp * (arc * bowAmount);
                _path.Add(new Vector3(p.x, p.y, 0f));
            }
        }

        /// <summary>
        /// 진행률만큼의 구간을 <see cref="drawPoints"/>개로 균등 리샘플한다.
        /// 점 수를 고정해야 (a) 매 프레임 배열 재할당이 없고 (b) 굵기·색 곡선이 항상 보이는 구간
        /// 전체(꼬리 0 → 머리 1)에 걸린다 — 머리가 늘 가장 밝고 굵게 유지되는 이유다.
        /// </summary>
        private bool Reveal(float reach01)
        {
            float total = 0f;
            for (int i = 1; i < _path.Count; i++) total += Vector3.Distance(_path[i - 1], _path[i]);
            if (total <= 0.001f) return false;

            float want = total * Mathf.Clamp01(reach01);
            int last = _drawn.Length - 1;
            float phase = Time.time * wobbleHz * 2f * Mathf.PI;

            int cursor = 1;          // _path에서 진행 중인 구간의 끝점
            float travelled = 0f;    // cursor 직전까지의 누적 길이
            for (int k = 0; k <= last; k++)
            {
                float target = want * k / last;
                while (cursor < _path.Count - 1)
                {
                    float d = Vector3.Distance(_path[cursor - 1], _path[cursor]);
                    if (travelled + d >= target) break;
                    travelled += d;
                    cursor++;
                }
                float seg = Vector3.Distance(_path[cursor - 1], _path[cursor]);
                float t = seg > 0.0001f ? Mathf.Clamp01((target - travelled) / seg) : 0f;
                Vector3 p = Vector3.Lerp(_path[cursor - 1], _path[cursor], t);
                // 소금길 위에서 아주 미세하게만 흔들린다 — 살아있는 빛이되 길을 벗어나지 않게
                float sway = Mathf.Sin(phase + k * 0.45f) * wobbleAmp * Mathf.Sin((float)k / last * Mathf.PI);
                _drawn[k] = new Vector3(p.x, p.y + sway, p.z);
            }
            return true;
        }

        private void Hide()
        {
            if (line != null) line.enabled = false;
            if (headLight != null)
            {
                headLight.intensity = 0f;
                headLight.enabled = false;
            }
        }

        private bool Valid(int corner)
            => cornerTransforms != null && corner >= 0 && corner < cornerTransforms.Length
               && cornerTransforms[corner] != null;
    }
}
