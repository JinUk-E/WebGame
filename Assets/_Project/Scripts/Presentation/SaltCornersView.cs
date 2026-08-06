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
        [SerializeField] private Sprite[] stageSprites = new Sprite[4];
        [SerializeField] private Color stageWhite = new Color(0.95f, 0.95f, 0.92f);
        [SerializeField] private Color stageGray = new Color(0.55f, 0.53f, 0.5f);
        [SerializeField] private Color stageBlack = new Color(0.14f, 0.12f, 0.12f);
        [SerializeField] private Color stageDeepBlack = new Color(0.18f, 0.04f, 0.07f); // v0.3 심화 — 검붉은 흑 (stage=3)
        [SerializeField] private Color telegraphColor = new Color(0.9f, 0.15f, 0.1f);
        [SerializeField] private Color aimColor = new Color(1f, 0.85f, 0.3f); // 기도 조준 — 금빛
        [SerializeField] private float telegraphPulseHz = 3f;
        [SerializeField] private float counterFlashSec = 0.4f;
        // v0.6 — 흑화 단계는 "어둡게"가 아니라 "다르게" 보여야 한다. 무광 머티리얼 위에서 1을 넘는 틴트는
        // 스프라이트의 붉은 균열만 들어올린다 (검은 몸통은 어차피 어두워서 눈에 띄게 밝아지지 않는다).
        [SerializeField] private Color blackGlow = new Color(1.15f, 0.95f, 0.95f);
        [SerializeField] private Color deepGlow = new Color(1.6f, 0.75f, 0.8f);
        [SerializeField] private float deepGlowPulseHz = 0.8f;
        // 주의 유도(프롤로그 "네 귀퉁이에 소금을 쌓았다") — 전조와 구분되는 흰 섬광
        [SerializeField] private Color attentionColor = new Color(1.6f, 1.6f, 1.5f);

        private readonly int[] _stages = new int[CornerIndex.Count];
        private readonly float[] _telegraphUntil = new float[CornerIndex.Count];
        private readonly float[] _flashUntil = new float[CornerIndex.Count];
        private readonly float[] _attentionUntil = new float[CornerIndex.Count];
        private int _aimedCorner = CornerIndex.None; // v1.4 — 기도 채널 중 조준 귀퉁이

        private void OnEnable()
        {
            GameEvents.CornerStageChanged += HandleStageChanged;
            GameEvents.AttackTelegraphStarted += HandleTelegraph;
            GameEvents.AttackResolved += HandleResolved;
            GameEvents.PrayerChannelChanged += HandlePrayerChanged;
            GameEvents.SaltAttentionRequested += HandleAttention;
        }

        private void OnDisable()
        {
            GameEvents.CornerStageChanged -= HandleStageChanged;
            GameEvents.AttackTelegraphStarted -= HandleTelegraph;
            GameEvents.AttackResolved -= HandleResolved;
            GameEvents.PrayerChannelChanged -= HandlePrayerChanged;
            GameEvents.SaltAttentionRequested -= HandleAttention;
        }

        private void HandleAttention(int corner, float seconds)
        {
            if (!Valid(corner)) return;
            _attentionUntil[corner] = Time.time + Mathf.Max(0f, seconds);
        }

        private void HandlePrayerChanged(float progress01, int aimedCorner)
            => _aimedCorner = progress01 > 0f ? aimedCorner : CornerIndex.None;

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

                // stage 3 = 흑+심화 (CornerStage.DeepBlack — SaltCorners가 심화 시 발행, v0.3)
                int stage = Mathf.Clamp(_stages[i], 0, 3);
                Sprite stageSprite = stageSprites != null && stage < stageSprites.Length ? stageSprites[stage] : null;

                Color baseColor;
                if (stageSprite != null)
                {
                    // 스프라이트 스왑 방식 — 틴트는 white 기준 (전조·플래시·조준 블렌드가 곱으로 얹힘)
                    if (sr.sprite != stageSprite) sr.sprite = stageSprite;
                    // v0.6: 흑(2)·심화(3)는 흰색 대신 발광 틴트를 깔아 "어두워져서 안 보이는" 역전을 막는다.
                    // 심화는 느리게 맥동 — 흑과 심화의 명도차가 2뿐이라 정지 화면으로는 구분이 안 된다.
                    if (stage >= 3)
                    {
                        float breathe = 0.5f + 0.5f * Mathf.Sin(Time.time * deepGlowPulseHz * 2f * Mathf.PI);
                        baseColor = Color.Lerp(blackGlow, deepGlow, breathe);
                    }
                    else
                    {
                        baseColor = stage == 2 ? blackGlow : Color.white;
                    }
                }
                else
                {
                    baseColor = stage >= 3 ? stageDeepBlack
                        : stage == 2 ? stageBlack
                        : stage == 1 ? stageGray : stageWhite;
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

                // 기도 조준 — 금빛 블렌드 (전조 위에도 겹침: 조준이 맞는지 보여야 능동 방어가 성립)
                if (i == _aimedCorner) color = Color.Lerp(color, aimColor, 0.5f);

                sr.color = color;
            }
        }

        private bool Valid(int corner) => corner >= 0 && corner < cornerRenderers.Length;
    }
}
