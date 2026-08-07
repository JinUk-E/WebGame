using UnityEngine;

namespace Morae.Game.Core
{
    /// <summary>
    /// 심박수 계산 — 순수 함수. <b>시각(HeartView)과 청각(SoundManager)이 같은 값을 쓰게</b> 하려고 뽑았다.
    ///
    /// <para>
    /// <b>왜 하나로 묶는가.</b> 이 게임은 회복·소모를 문장 없이 가르쳐야 하는데, 그러려면
    /// <b>심박 하나가 몸 상태의 공용 언어</b>여야 한다 — 이불에서 멎고, TV에서 느려지고, 소금 뿌릴 때 튄다.
    /// 눈으로 본 박동과 귀로 들은 박동이 어긋나면 그 언어가 깨진다.
    /// v0.6까지 둘은 서로 다른 식을 썼다(뷰는 bpm 보간, 사운드는 pitch = 0.85 + 0.65×fear).
    /// </para>
    ///
    /// <para>
    /// 심박 클립이 1초 = 1박이라 <c>pitch = bpm / 60</c>으로 바로 환산된다.
    /// </para>
    /// </summary>
    public static class HeartRateModel
    {
        /// <summary>박동 클립 1회 길이(초) — pitch 환산 기준.</summary>
        public const float ClipBeatSec = 1f;

        /// <summary>
        /// 현재 심박수(bpm).
        /// </summary>
        /// <param name="sanity01">이성 0~1 (낮을수록 빠르다)</param>
        /// <param name="minBpm">이성 100일 때</param>
        /// <param name="maxBpm">이성 0 직전</param>
        /// <param name="salting01">소금 뿌리는 중 0~1 — 행위에 붙는 가산. 값 변화(초당 0.02)로는
        ///   절대 체감되지 않으므로 <b>상태 자체</b>를 얹어야 한다</param>
        /// <param name="saltingBoostBpm">뿌리는 중 가산 bpm</param>
        /// <param name="calm01">진정 0~1 — 이불(1)·TV(부분). 곱연산으로 눌러 "몸이 편해진다"를 만든다</param>
        /// <param name="calmScale">진정이 최대일 때의 배율 (0이면 완전 정지)</param>
        public static float Bpm(float sanity01, float minBpm, float maxBpm,
            float salting01, float saltingBoostBpm, float calm01, float calmScale)
        {
            float fear = 1f - Mathf.Clamp01(sanity01);
            float bpm = Mathf.Lerp(minBpm, maxBpm, fear) + saltingBoostBpm * Mathf.Clamp01(salting01);
            return bpm * Mathf.Lerp(1f, calmScale, Mathf.Clamp01(calm01));
        }

        /// <summary>
        /// 진정도 — 이불이 TV보다 강하다. 둘 다면 이불이 이긴다(더 안전한 쪽이 몸을 지배한다).
        /// </summary>
        public static float Calm01(float blanket01, bool tvOn, float tvCalmWeight)
            => Mathf.Max(Mathf.Clamp01(blanket01), tvOn ? Mathf.Clamp01(tvCalmWeight) : 0f);

        /// <summary>bpm → 1초 클립의 재생 피치.</summary>
        public static float PitchFor(float bpm) => Mathf.Max(0.01f, bpm / 60f * ClipBeatSec);
    }
}
