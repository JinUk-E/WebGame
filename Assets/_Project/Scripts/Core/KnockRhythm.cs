using UnityEngine;

namespace Morae.Game.Core
{
    /// <summary>
    /// 전조 두드림의 박자 (v0.6 — 순수 계산).
    ///
    /// 소리(SoundManager)와 그림(CornerTelegraphView)이 **같은 함수**로 시각을 구한다.
    /// 각자 타이머를 굴리면 한 프레임씩 어긋나고, 두드림은 어긋나는 순간 "쿵-(늦게)흔들림"으로
    /// 분리되어 들린다 — 두드림이 벽을 때린 결과로 안 읽히면 연출 전체가 무너진다.
    ///
    /// 박자: 전조 길이를 균등 분할하되 **끝에 여유**를 둔다. 마지막 두드림이 판정과 겹치면
    /// 상쇄 성공음과 뭉개진다.
    /// </summary>
    public static class KnockRhythm
    {
        public const int Count = 3;
        private const float TailMargin = 0.22f;   // 마지막 두드림 이후 남기는 비율

        /// <summary>i번째(0~) 두드림이 전조 시작 후 몇 초에 오는가.</summary>
        public static float TimeOf(int index, float telegraphDuration)
        {
            if (telegraphDuration <= 0f || Count <= 0) return 0f;
            float usable = telegraphDuration * (1f - TailMargin);
            // 첫 타는 시작 직후가 아니라 살짝 뒤 — 전조 시작음과 겹치지 않게
            return usable * (index + 0.35f) / Count;
        }

        /// <summary>경과 시간까지 몇 번 두드렸는가 (0~Count).</summary>
        public static int CountUpTo(float elapsed, float telegraphDuration)
        {
            int n = 0;
            for (int i = 0; i < Count; i++)
                if (elapsed >= TimeOf(i, telegraphDuration)) n++;
            return n;
        }

        /// <summary>
        /// 마지막 두드림 이후 흐른 시간으로 만드는 충격 감쇠(1 → 0).
        /// 흔들림·어둠 맥동이 이 값을 곱해 쓰면 "때린 순간 가장 세고 곧 잦아든다"가 공짜로 나온다.
        /// </summary>
        public static float ImpactEnvelope(float elapsed, float telegraphDuration, float decaySec = 0.45f)
        {
            float best = 0f;
            for (int i = 0; i < Count; i++)
            {
                float since = elapsed - TimeOf(i, telegraphDuration);
                if (since < 0f) continue;
                float e = Mathf.Exp(-since / Mathf.Max(0.01f, decaySec));
                if (e > best) best = e;
            }
            return best;
        }
    }
}
