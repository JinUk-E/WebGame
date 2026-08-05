using Morae.Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 좌하단 부적 상태 UI (표현 계층 — 구독만: TalismanBurned/SanityChanged/AttackTelegraphStarted/AttackResolved).
    /// 규칙 (2026-08-05 확정 해석 — 표시 전용, 1회 방어 메커니즘 무변경):
    /// - 평상시: 단계 0(멀쩡) 고정.
    /// - 위기(이성 30% 미만 && 전조~판정 진행 중): 단계 1의 은은한 그을림 + 미세 흔들림 — 프리뷰 연출, 소모 아님.
    /// - TalismanBurned 순간: 1→4 연소 애니메이션(잔불 느낌 순차 재생, 기본 약 2.5s) 후 4(완전 연소) 고정.
    /// 흔들림·연소는 unscaledTime — F1 배속에 끌려가지 않는 연출 계층 시간 (HeartView 전례).
    /// </summary>
    public sealed class TalismanStatusView : MonoBehaviour
    {
        [SerializeField] private Image talisman;
        [SerializeField] private Sprite[] stageSprites = new Sprite[5]; // 0=멀쩡 … 4=완전 연소
        [SerializeField] private float crisisSanityThreshold = 0.3f;
        [SerializeField] private float crisisShakeAmplitude = 2.5f;   // px (1920×1080 기준)
        [SerializeField] private float crisisShakeHz = 11f;
        [SerializeField] private float[] burnStageSec = { 0.7f, 0.7f, 0.6f, 0.5f }; // 1→2→3→4 각 단계 체류

        private RectTransform _rect;
        private Vector2 _basePos;
        private float _sanity01 = 1f;
        private int _activeTelegraphs;
        private bool _burned;
        private float _burnTimer = -1f; // >= 0 = 연소 애니메이션 진행 중
        private int _shownStage = -1;

        private void Awake()
        {
            _rect = talisman != null ? talisman.rectTransform : null;
            if (_rect != null) _basePos = _rect.anchoredPosition;
        }

        private void OnEnable()
        {
            GameEvents.TalismanBurned += HandleBurned;
            GameEvents.SanityChanged += HandleSanityChanged;
            GameEvents.AttackTelegraphStarted += HandleTelegraphStarted;
            GameEvents.AttackResolved += HandleResolved;
        }

        private void OnDisable()
        {
            GameEvents.TalismanBurned -= HandleBurned;
            GameEvents.SanityChanged -= HandleSanityChanged;
            GameEvents.AttackTelegraphStarted -= HandleTelegraphStarted;
            GameEvents.AttackResolved -= HandleResolved;
        }

        private void HandleBurned()
        {
            if (_burned) return;
            _burned = true;
            _burnTimer = 0f;
        }

        private void HandleSanityChanged(float s01) => _sanity01 = s01;

        private void HandleTelegraphStarted(int corner, float duration) => _activeTelegraphs++;

        private void HandleResolved(int corner, bool countered)
            => _activeTelegraphs = Mathf.Max(0, _activeTelegraphs - 1);

        private void Update()
        {
            if (talisman == null) return;

            int stage;
            bool shake = false;

            if (_burned)
            {
                if (_burnTimer >= 0f)
                {
                    _burnTimer += Time.unscaledDeltaTime;
                    stage = BurnStageAt(_burnTimer);
                    shake = true; // 타는 동안 떨림 — 잔불 연출
                    float total = 0f;
                    for (int i = 0; i < burnStageSec.Length; i++) total += burnStageSec[i];
                    if (_burnTimer >= total)
                    {
                        stage = 4;
                        _burnTimer = -1f; // 완료 — 4 고정
                    }
                }
                else
                {
                    stage = 4; // 소모됨 표시 고정
                }
            }
            else if (_sanity01 < crisisSanityThreshold && _activeTelegraphs > 0)
            {
                stage = 1; // 위기 프리뷰 — 은은한 그을림 (실제 소모 아님)
                shake = true;
            }
            else
            {
                stage = 0;
            }

            if (stage != _shownStage)
            {
                _shownStage = stage;
                if (stageSprites != null && stage < stageSprites.Length && stageSprites[stage] != null)
                {
                    talisman.sprite = stageSprites[stage];
                }
            }

            if (_rect != null)
            {
                if (shake)
                {
                    float t = Time.unscaledTime * crisisShakeHz;
                    _rect.anchoredPosition = _basePos + new Vector2(
                        (Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2f * crisisShakeAmplitude,
                        (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f * crisisShakeAmplitude);
                }
                else if (_rect.anchoredPosition != _basePos)
                {
                    _rect.anchoredPosition = _basePos;
                }
            }
        }

        /// <summary>연소 경과 시간 → 표시 단계 (1..4). burnStageSec를 순서대로 소진.</summary>
        private int BurnStageAt(float elapsed)
        {
            float acc = 0f;
            for (int i = 0; i < burnStageSec.Length; i++)
            {
                acc += burnStageSec[i];
                if (elapsed < acc) return Mathf.Min(1 + i, 4);
            }
            return 4;
        }
    }
}
