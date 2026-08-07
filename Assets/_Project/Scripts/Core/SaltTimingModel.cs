using UnityEngine;

namespace Morae.Game.Core
{
    /// <summary>
    /// 새 조작(가서 뿌린다)이 부적 예산 안에서 성립하는지 — 순수 계산.
    ///
    /// <para>
    /// <b>왜 모델로 뽑았는가.</b> 전임자인 CounterTimingModel이 남긴 교훈을 그대로 물려받는다:
    /// 2026-08-07까지 이 게임의 핵심 규칙이 실제로 성립한 적이 없었는데, 원인은 어느 한 수치가 틀려서가 아니라
    /// <b>서로 다른 파일에 있는 두 수치의 관계</b>가 깨져 있었기 때문이다(전조 3.0s / 채널 3.0s — 각각은 멀쩡해 보인다).
    /// v0.7에서 축이 "전조 vs 채널"에서 "부적 예산 vs 이동+홀드"로 바뀌었을 뿐, 같은 함정이 그대로 있다.
    /// BalanceConfig(홀드·이동속도·부적 총량)와 AttackTable(공격 건수)은 여전히 다른 파일이다.
    /// </para>
    ///
    /// 지켜야 할 부등식은 둘이고 성격이 다르다:
    /// <list type="number">
    ///   <item><b>1회 대응 성립</b> — <c>최악 이동 + 홀드 &lt; 부적 총량</c>.
    ///         한 번의 오염조차 처리 못 하면 게임이 성립하지 않는다. 실제로는 훨씬 큰 여유가 필요하다.</item>
    ///   <item><b>완주 가능</b> — <c>공격 건수 × 회당 더러운 시간 &lt; 부적 총량</c>.
    ///         이게 깨지면 실력과 무관하게 중반에 확정 사망한다 — 어렵게 만드는 게 아니라 부수는 것이다.</item>
    /// </list>
    /// </summary>
    public static class SaltTimingModel
    {
        /// <summary>
        /// 방 안 임의 지점에서 최원 귀퉁이까지의 최악 거리(유닛). L자 우회를 반영한 실측값이다
        /// (계단 모서리 (0.02, 0.675)를 플레이어 반경 0.35로 팽창시킨 사분평면의 접선+호로 계산).
        /// 우회 비용 자체는 미미했다 — C0↔C1이 직선 9.093u 대비 9.153u로 +0.06u뿐이다.
        /// 정작 최악은 우하단 구석 (4.68, −3.895) → C0(−4.5, 1.5)의 10.65u다.
        /// </summary>
        public const float WorstTravelUnits = 10.65f;

        /// <summary>귀퉁이 간 최장 거리 — P6 연발이 강제하는 왕복 거리 (C0 ↔ C3).</summary>
        public const float LongestCornerPairUnits = 10.44f;

        /// <summary>오염을 인지하고 손이 움직이기까지의 설계 기준 반응 시간(초).</summary>
        public const float ReactionSec = 0.5f;

        /// <summary>평균 접근 거리 — 회당 소모 추정에 쓴다 (최악이 아니라 기대값).</summary>
        public const float AverageTravelUnits = 8.0f;

        public static float TravelSec(float moveSpeed, float units)
            => moveSpeed <= 0f ? float.PositiveInfinity : units / moveSpeed;

        /// <summary>최악 위치에서 오염을 처리하는 데 걸리는 시간 = 반응 + 이동 + 홀드.</summary>
        public static float WorstResponseSec(float saltHoldSec, float moveSpeed)
            => ReactionSec + TravelSec(moveSpeed, WorstTravelUnits) + saltHoldSec;

        /// <summary>평균적으로 한 번의 오염이 부적에서 빼앗는 시간.</summary>
        public static float AverageDirtySec(float saltHoldSec, float moveSpeed)
            => ReactionSec + TravelSec(moveSpeed, AverageTravelUnits) + saltHoldSec;

        /// <summary>부등식 ① — 최악의 한 번조차 예산 안에서 처리할 수 있는가.</summary>
        public static bool CanHandleWorstSingle(float saltHoldSec, float moveSpeed, float talismanTotalSec)
            => WorstResponseSec(saltHoldSec, moveSpeed) < talismanTotalSec;

        /// <summary>부등식 ② — 이 건수를 평균 실력으로 완주할 수 있는가.</summary>
        public static bool CanFinishRun(float saltHoldSec, float moveSpeed, float talismanTotalSec, int attackCount)
            => AverageDirtySec(saltHoldSec, moveSpeed) * attackCount < talismanTotalSec;

        /// <summary>완주 후 남는 부적 시간 추정 — 엔딩 등급 구간이 의미 있는 폭인지 보는 값.</summary>
        public static float EstimatedRemainSec(float saltHoldSec, float moveSpeed, float talismanTotalSec, int attackCount)
            => talismanTotalSec - AverageDirtySec(saltHoldSec, moveSpeed) * attackCount;

        /// <summary>시작 시 실제 수치로 부등식이 어떻게 서 있는지 한 줄로 남긴다 (밸런스 검증용).</summary>
        public static string Describe(float saltHoldSec, float moveSpeed, float talismanTotalSec, int attackCount)
        {
            float worst = WorstResponseSec(saltHoldSec, moveSpeed);
            float avg = AverageDirtySec(saltHoldSec, moveSpeed);
            float remain = EstimatedRemainSec(saltHoldSec, moveSpeed, talismanTotalSec, attackCount);
            float roundTrip = TravelSec(moveSpeed, LongestCornerPairUnits);
            return $"홀드 {saltHoldSec:F2}s / 이동 {moveSpeed:F2}u/s → 최악 대응 {worst:F2}s " +
                   $"({(worst < talismanTotalSec ? "성립" : "불가")}) · 회당 평균 {avg:F2}s × {attackCount}건 = " +
                   $"{avg * attackCount:F1}s / 부적 {talismanTotalSec:F0}s → 예상 잔여 {remain:F1}s " +
                   $"({(remain > 0f ? "완주 가능" : "확정 사망")}) · 최장 왕복 {roundTrip:F2}s";
        }
    }
}
