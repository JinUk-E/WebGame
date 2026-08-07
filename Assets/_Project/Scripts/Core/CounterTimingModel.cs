using UnityEngine;

namespace Morae.Game.Core
{
    /// <summary>
    /// 능동 방어(전조 → 기도 상쇄)가 성립하는 조건 — 순수 계산. EditMode 테스트 대상.
    ///
    /// <para>
    /// <b>왜 모델로 뽑았는가</b>: 2026-08-07까지 이 게임의 핵심 규칙("전조 안에 기도 채널을 완료하면 상쇄")은
    /// 실제로 성립한 적이 없었다. 전조 3.0s · 채널 3.0s라 <b>이동 시간 0을 가정해도 정확히 동시</b>였고,
    /// 심화 귀퉁이(×1.5 = 4.5s)는 불상 앞에 미리 서 있어도 불가능했다. 두 수치가 서로 다른 파일
    /// (AttackTable / BalanceConfig)에 있어서, 각각은 멀쩡해 보이고 관계만 깨져 있었다.
    /// </para>
    ///
    /// 그래서 관계를 코드로 적어 둔다. 지켜야 할 부등식은 셋이고, 셋 다 성격이 다르다:
    /// <list type="number">
    ///   <item><b>상쇄 성립</b> — <c>channel + 이동 여유 ≤ telegraph</c>.
    ///         여유가 0이면 전조를 보고 출발한 사람은 무조건 늦는다.</item>
    ///   <item><b>트리아지 보존</b> — <c>channel × 2 &gt; telegraph</c>.
    ///         한 전조 창에 두 곳을 막을 수 있으면 "전부는 못 막는다 · 무엇을 버릴지 고른다"는 설계가 사라진다.</item>
    ///   <item><b>심화의 벌</b> — <c>channel × 1.5 ≤ telegraph</c>이되 <b>이동 여유는 남지 않는다</b>.
    ///         가능하긴 하되 미리 불상 앞에 서 있는 대가를 치러야 한다.</item>
    /// </list>
    /// </summary>
    public static class CounterTimingModel
    {
        /// <summary>
        /// 설계 기준 이동 거리(유닛). 방 바닥은 10.06×8.49u이고 불상 앞 자리는 (-1, 1.25) —
        /// 가장 먼 귀퉁이에서의 실제 거리는 약 7.5u, 평균은 약 4.7u다.
        /// 6.0u = "방 대부분의 위치에서 출발해도 닿는다"의 기준선 (전부는 아니다 — 그건 트리아지의 몫).
        /// </summary>
        public const float ReferenceTravelUnits = 6f;

        /// <summary>전조를 보고 출발해 불상 앞에 서기까지 필요한 최소 여유(초).</summary>
        public static float RequiredSlackSec(float moveSpeed, float travelUnits = ReferenceTravelUnits)
            => moveSpeed <= 0f ? float.PositiveInfinity : Mathf.Max(0f, travelUnits) / moveSpeed;

        /// <summary>채널을 시작하기 전에 쓸 수 있는 시간(초) = 전조 − 채널. 음수면 애초에 불가능하다.</summary>
        public static float SlackSec(float channelSec, float telegraphSec) => telegraphSec - channelSec;

        /// <summary>이 전조 길이 안에 (이동 여유를 확보하고) 상쇄를 완료할 수 있는가.</summary>
        public static bool CanCounter(float channelSec, float telegraphSec, float requiredSlackSec)
            => SlackSec(channelSec, telegraphSec) >= requiredSlackSec;

        /// <summary>
        /// 한 전조 창 안에 완료할 수 있는 채널 횟수 — 이동 시간 0을 가정한 <b>상한</b>이다.
        /// 2 이상이면 트리아지가 깨진다(실제로는 이동이 있어 더 어렵지만, 상한이 1이어야 설계가 보장된다).
        /// </summary>
        public static int MaxCountersPerWindow(float channelSec, float telegraphSec)
        {
            if (channelSec <= 0f) return int.MaxValue;
            if (telegraphSec < channelSec) return 0;
            return Mathf.FloorToInt(telegraphSec / channelSec + 1e-4f);
        }

        /// <summary>로그 한 줄 — 시작 시 실제 수치로 부등식이 어떻게 서 있는지 남긴다(밸런스 검증용).</summary>
        public static string Describe(float channelSec, float telegraphSec, float deepenedMultiplier, float moveSpeed)
        {
            float need = RequiredSlackSec(moveSpeed);
            float slack = SlackSec(channelSec, telegraphSec);
            float deep = channelSec * deepenedMultiplier;
            return $"채널 {channelSec:F2}s / 전조 {telegraphSec:F2}s → 여유 {slack:F2}s (필요 {need:F2}s, " +
                   $"{(slack >= need ? "충족" : "부족")}) · 한 창 최대 상쇄 {MaxCountersPerWindow(channelSec, telegraphSec)}회 · " +
                   $"심화 {deep:F2}s ({(deep <= telegraphSec ? "가능" : "불가")}, 여유 {telegraphSec - deep:F2}s)";
        }
    }
}
