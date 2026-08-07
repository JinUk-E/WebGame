using UnityEngine;

namespace Morae.Game.Core
{
    /// <summary>
    /// P5 삼중 습격(<c>triple-assault</c>)의 소리 배치 (순수 계산).
    ///
    /// <para>
    /// 자막은 "(손잡이가 덜컹거린다 — 전화벨 — 노크 소리가 <b>동시에</b> 울린다)"라고 말한다.
    /// 그런데 실제로는 문 효과음 하나만 났다 — 자막이 거짓말을 하고 있었다.
    /// 여기서 세 층의 시각을 정하고 <c>SoundManager</c>가 그대로 재생한다.
    /// </para>
    ///
    /// <para>
    /// 세 층은 <b>대역이 겹치지 않게</b> 고른다 — 전화벨 600~1200Hz(두 톤 종),
    /// 노크 저역 일색, 손잡이 저역 + 2~4kHz 금속. 같은 대역 셋을 겹치면 "동시에 세 소리"가 아니라
    /// 한 덩어리 소음이 되고, 자막은 여전히 거짓말이 된다.
    /// </para>
    ///
    /// <para>
    /// 손잡이 층만 그림(문짝 흔들림)을 동반한다 — 박자는 <see cref="RattlePattern.DoorHandle"/>이 소유한다.
    /// </para>
    /// </summary>
    public static class TripleAssaultCue
    {
        /// <summary>EventTable의 id. 표현 계층이 이 이벤트를 특별 취급하는 유일한 근거.</summary>
        public const string EventId = "triple-assault";

        // ---- 전화벨 (다른 방/복도) ----
        public const float PhoneStartSec = 0f;
        /// <summary>⚠ SFX_Phone/phone_ring.wav 의 실제 길이와 같아야 한다 (RattleSyncTests가 대조).</summary>
        public const float PhoneDurationSec = 3.60f;

        // ---- 손잡이 덜컹 (문) ----
        /// <summary>전화벨보다 조금 늦게 — 셋이 정확히 같은 프레임에 시작하면 앞머리가 뭉쳐 한 소리로 들린다.</summary>
        public const float HandleStartSec = 0.10f;

        // ---- 노크 (문밖) ----
        /// <summary>⚠ SFX_Knock/knock.wav 의 실제 길이와 같아야 한다.</summary>
        public const float KnockDurationSec = 0.62f;
        private static readonly float[] KnockTimes = { 0.30f, 0.72f, 1.14f };

        public static int KnockCount => KnockTimes.Length;

        public static float KnockTime(int index)
            => index >= 0 && index < KnockTimes.Length ? KnockTimes[index] : 0f;

        /// <summary>경과까지 몇 번 두드렸는가 — SoundManager가 매 프레임 이 값까지 따라붙는다.</summary>
        public static int KnockCountUpTo(float elapsed)
        {
            int n = 0;
            for (int i = 0; i < KnockTimes.Length; i++)
                if (elapsed >= KnockTimes[i]) n++;
            return n;
        }

        public static float HandleDurationSec => RattlePattern.DurationSec(RattleKind.DoorHandle);
        public static float HandleEndSec => HandleStartSec + HandleDurationSec;
        public static float PhoneEndSec => PhoneStartSec + PhoneDurationSec;
        public static float KnockStartSec => KnockTime(0);
        public static float KnockEndSec => KnockTime(KnockCount - 1) + KnockDurationSec;

        /// <summary>세 층이 <b>모두</b> 울리고 있는 구간의 시작(초).</summary>
        public static float OverlapStartSec => Mathf.Max(PhoneStartSec, Mathf.Max(HandleStartSec, KnockStartSec));

        /// <summary>세 층이 모두 울리고 있는 구간의 끝(초).</summary>
        public static float OverlapEndSec => Mathf.Min(PhoneEndSec, Mathf.Min(HandleEndSec, KnockEndSec));

        /// <summary>"동시에"가 성립하는 길이(초). 0 이하면 자막이 거짓말을 하는 것이다.</summary>
        public static float OverlapSec => Mathf.Max(0f, OverlapEndSec - OverlapStartSec);

        /// <summary>연출 전체 길이 — 마지막 층이 끝나는 시각.</summary>
        public static float TotalDurationSec => Mathf.Max(PhoneEndSec, Mathf.Max(HandleEndSec, KnockEndSec));
    }
}
