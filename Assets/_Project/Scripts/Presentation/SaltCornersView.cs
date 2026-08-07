using Morae.Game.Core;
using Morae.Game.Data;
using UnityEngine;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 소금 귀퉁이 시각화 (표현 계층 — 구독만, §1.2. D3).
    /// 오염 단계는 스프라이트 4종 스왑(백/회/흑/심화 — 아트 2단계) + 전조 중 적색 펄스 + 상쇄 성공 백색 플래시.
    /// stageSprites 미배선 시 구 방식(색 틴트) 폴백 — 스프라이트 스왑 시 틴트는 white 기준 곱연산.
    /// 인덱스 규약: 0=좌상 1=우상 2=좌하 3=우하 (CornerIndex — SaltCorners와 동일).
    /// 전조·플래시 타이밍은 게임플레이 시간(Time.time) — F1 배속과 동기.
    /// </summary>
    public sealed class SaltCornersView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer[] cornerRenderers = new SpriteRenderer[CornerIndex.Count];
        // 아트 2단계 — 단계별 스프라이트 (0=백 1=회 2=흑 3=심화). 비어 있으면 색 틴트 폴백
        [SerializeField] private Sprite[] stageSprites = new Sprite[3];
        [SerializeField] private Color stageWhite = new Color(0.95f, 0.95f, 0.92f);
        [SerializeField] private Color stageGray = new Color(0.55f, 0.53f, 0.5f);
        [SerializeField] private Color stageBlack = new Color(0.14f, 0.12f, 0.12f);
        [SerializeField] private Color telegraphColor = new Color(0.9f, 0.15f, 0.1f);
        [SerializeField] private Color saltingColor = new Color(1f, 0.97f, 0.88f); // 뿌리는 중 — 소금빛
        [SerializeField] private float telegraphPulseHz = 3f;
        [SerializeField] private float counterFlashSec = 0.4f;
        // v0.6 — 흑화 단계는 "어둡게"가 아니라 "다르게" 보여야 한다. 무광 머티리얼 위에서 1을 넘는 틴트는
        // 스프라이트의 붉은 균열만 들어올린다 (검은 몸통은 어차피 어두워서 눈에 띄게 밝아지지 않는다).
        [SerializeField] private Color blackGlow = new Color(1.15f, 0.95f, 0.95f);
        // v0.7 — 오염 확정 상태(회·흑)의 <b>느린 숨</b>. 진폭이 작고 평균 밝기는 blackGlow 그대로다.
        //   왜 필요한가: 옛 코드에서 맥동은 심화(stage 3)에만 있었고 흑(2)은 완전 정지였다. 그런데 새 설계에서
        //   플레이어가 봐야 할 것은 전조 중이 아니라 **오염이 확정된 뒤의 정적 상태**다. 정지해 있으면
        //   네 구석을 순차 탐색해야 하고, 어두운 색이라 오히려 눈에 덜 띈다("어두워져서 안 보이는 역전").
        //   느리게 숨쉬면 "저기만 움직인다"가 되어 스캔 4회가 사케이드 1회로 줄어든다.
        //   변하는 것은 밝기 총량이 아니라 **시간**이라 밝기 축 금지 규칙에 걸리지 않는다.
        [SerializeField] private Color dirtyBreathGlow = new Color(1.35f, 0.9f, 0.9f);
        [SerializeField] private float dirtyBreathHz = 0.35f;
        // 주의 유도(프롤로그) — 전조와 구분되는 흰 섬광
        [SerializeField] private Color attentionColor = new Color(1.6f, 1.6f, 1.5f);

        private readonly int[] _stages = new int[CornerIndex.Count];
        private readonly float[] _telegraphUntil = new float[CornerIndex.Count];
        private readonly float[] _flashUntil = new float[CornerIndex.Count];
        private readonly float[] _attentionUntil = new float[CornerIndex.Count];
        private int _saltingCorner = CornerIndex.None; // 지금 뿌리는 중인 귀퉁이
        private float _saltingProgress;                // 그 귀퉁이의 진행률 0~1
        private readonly Vector3[] _baseScales = new Vector3[CornerIndex.Count];

        private void OnEnable()
        {
            GameEvents.CornerStageChanged += HandleStageChanged;
            GameEvents.AttackTelegraphStarted += HandleTelegraph;
            GameEvents.AttackResolved += HandleResolved;
            GameEvents.SaltChannelChanged += HandleSaltChannel;
            GameEvents.SaltAttentionRequested += HandleAttention;
        }

        private void OnDisable()
        {
            GameEvents.CornerStageChanged -= HandleStageChanged;
            GameEvents.AttackTelegraphStarted -= HandleTelegraph;
            GameEvents.AttackResolved -= HandleResolved;
            GameEvents.SaltChannelChanged -= HandleSaltChannel;
            GameEvents.SaltAttentionRequested -= HandleAttention;
        }

        private void HandleAttention(int corner, float seconds)
        {
            if (!Valid(corner)) return;
            _attentionUntil[corner] = Time.time + Mathf.Max(0f, seconds);
        }

        private void HandleSaltChannel(int corner, float progress01)
        {
            _saltingCorner = progress01 > 0f ? corner : CornerIndex.None;
            _saltingProgress = Mathf.Clamp01(progress01);
        }

        private void Awake()
        {
            for (int i = 0; i < cornerRenderers.Length; i++)
            {
                _baseScales[i] = cornerRenderers[i] != null ? cornerRenderers[i].transform.localScale : Vector3.one;
            }
        }

        private void HandleStageChanged(int corner, int stage)
        {
            if (!Valid(corner)) return;
            _stages[corner] = stage;
        }

        private void HandleTelegraph(int corner, float duration)
        {
            if (!Valid(corner)) return;
            _telegraphUntil[corner] = Time.time + duration;
        }

        private void HandleResolved(int corner, bool countered)
        {
            if (!Valid(corner)) return;
            _telegraphUntil[corner] = 0f;
            if (countered) _flashUntil[corner] = Time.time + counterFlashSec; // 상쇄 성공 — 정화 플래시
        }

        private void Update()
        {
            for (int i = 0; i < cornerRenderers.Length; i++)
            {
                SpriteRenderer sr = cornerRenderers[i];
                if (sr == null) continue;

                int stage = Mathf.Clamp(_stages[i], 0, (int)CornerStage.Black);

                // v0.7 — 뿌리는 **동안** 실제로 깨끗해지는 게 보여야 한다.
                // 이전에는 홀드 내내 아무 변화가 없다가 완료 순간에 단계가 툭 바뀌었다. 그러면 플레이어 눈에는
                // "누르고 있는 나"와 "갑자기 하얘진 소금"이 따로 놀아서 **인과가 안 읽힌다** —
                // 무튜토리얼로 "E 홀드 = 소금 복구"를 가르쳐야 하는데 정작 그 연결이 화면에 없었다.
                // 진행률 절반을 넘기면 한 단계 깨끗한 스프라이트로 미리 넘어가 눈에 띄는 계단을 하나 만든다.
                bool salting = i == _saltingCorner && _saltingProgress > 0f;
                if (salting && stage > 0 && _saltingProgress >= 0.5f) stage--;

                Sprite stageSprite = stageSprites != null && stage < stageSprites.Length ? stageSprites[stage] : null;

                Color baseColor;
                if (stageSprite != null)
                {
                    // 스프라이트 스왑 방식 — 틴트는 white 기준 (전조·플래시 블렌드가 곱으로 얹힘)
                    if (sr.sprite != stageSprite) sr.sprite = stageSprite;
                    if (stage > 0)
                    {
                        // 더러운 귀퉁이는 느리게 숨쉰다 — 정지 화면에서 "저기만 움직인다"가 유일한 방향 단서다
                        float breathe = 0.5f + 0.5f * Mathf.Sin(Time.time * dirtyBreathHz * 2f * Mathf.PI);
                        baseColor = Color.Lerp(blackGlow, dirtyBreathGlow, breathe * (stage / (float)CornerStage.Black));
                    }
                    else
                    {
                        baseColor = Color.white;
                    }
                }
                else
                {
                    baseColor = stage == 2 ? stageBlack : stage == 1 ? stageGray : stageWhite;
                }

                Color color;
                if (Time.time < _attentionUntil[i])
                {
                    // 주의 유도가 최우선 — 프롤로그에서 "여기를 봐라"가 전조·오염 표시보다 먼저 읽혀야 한다
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f * 2f * Mathf.PI);
                    color = Color.Lerp(baseColor, attentionColor, 0.45f + 0.55f * pulse);
                }
                else if (Time.time < _telegraphUntil[i])
                {
                    // 전조 — 적색 펄스 (남은 시간 무관 일정 주기. 위급함은 소리·페이즈가 전달)
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * telegraphPulseHz * 2f * Mathf.PI);
                    color = Color.Lerp(baseColor, telegraphColor, 0.35f + 0.65f * pulse);
                }
                else if (Time.time < _flashUntil[i])
                {
                    color = Color.Lerp(baseColor, Color.white, 0.8f);
                }
                else
                {
                    color = baseColor;
                }

                // 뿌리는 중 — 진행률만큼 소금빛으로 물든다 (전조 위에도 겹침: 지금 고치는 중이라는 게 보여야 한다).
                // 여기서 밝아지는 건 추상 상태를 밝기로 말하는 게 아니라 **소금이 실제로 하얘지는 것**이라
                // 이 게임의 색 문법(백/회/흑)과 같은 축이다.
                if (salting)
                {
                    color = Color.Lerp(color, saltingColor, 0.35f + 0.65f * _saltingProgress);
                }

                // 쌓이는 것도 형태로 — 진행률만큼 부풀었다가 완료와 함께 제자리로.
                // 색만 바뀌면 정지 화면에서 놓치기 쉽다. 크기 변화는 주변시로도 잡힌다.
                Transform tr = sr.transform;
                Vector3 wanted = salting ? _baseScales[i] * (1f + 0.18f * _saltingProgress) : _baseScales[i];
                if (tr.localScale != wanted) tr.localScale = wanted;

                sr.color = color;
            }
        }

        private bool Valid(int corner) => corner >= 0 && corner < cornerRenderers.Length;
    }
}
