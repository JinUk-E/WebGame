using Morae.Game.Core;
using Morae.Game.Data;
using UnityEngine;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 소금 귀퉁이 시각화 (표현 계층 — 구독만, §1.2. D3).
    /// 오염 단계 3색(백/회/흑) + 전조 중 적색 펄스 + 상쇄 성공 백색 플래시.
    /// 인덱스 규약: 0=좌상 1=우상 2=좌하 3=우하 (CornerIndex — SaltCorners와 동일).
    /// 전조·플래시 타이밍은 게임플레이 시간(Time.time) — F1 배속과 동기.
    /// </summary>
    public sealed class SaltCornersView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer[] cornerRenderers = new SpriteRenderer[CornerIndex.Count];
        [SerializeField] private Color stageWhite = new Color(0.95f, 0.95f, 0.92f);
        [SerializeField] private Color stageGray = new Color(0.55f, 0.53f, 0.5f);
        [SerializeField] private Color stageBlack = new Color(0.14f, 0.12f, 0.12f);
        [SerializeField] private Color telegraphColor = new Color(0.9f, 0.15f, 0.1f);
        [SerializeField] private Color aimColor = new Color(1f, 0.85f, 0.3f); // 기도 조준 — 금빛
        [SerializeField] private float telegraphPulseHz = 3f;
        [SerializeField] private float counterFlashSec = 0.4f;

        private readonly int[] _stages = new int[CornerIndex.Count];
        private readonly float[] _telegraphUntil = new float[CornerIndex.Count];
        private readonly float[] _flashUntil = new float[CornerIndex.Count];
        private int _aimedCorner = CornerIndex.None; // v1.4 — 기도 채널 중 조준 귀퉁이

        private void OnEnable()
        {
            GameEvents.CornerStageChanged += HandleStageChanged;
            GameEvents.AttackTelegraphStarted += HandleTelegraph;
            GameEvents.AttackResolved += HandleResolved;
            GameEvents.PrayerChannelChanged += HandlePrayerChanged;
        }

        private void OnDisable()
        {
            GameEvents.CornerStageChanged -= HandleStageChanged;
            GameEvents.AttackTelegraphStarted -= HandleTelegraph;
            GameEvents.AttackResolved -= HandleResolved;
            GameEvents.PrayerChannelChanged -= HandlePrayerChanged;
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

                Color baseColor = _stages[i] >= 2 ? stageBlack : _stages[i] == 1 ? stageGray : stageWhite;

                Color color;
                if (Time.time < _telegraphUntil[i])
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
