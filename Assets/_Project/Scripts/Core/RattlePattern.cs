using UnityEngine;

namespace Morae.Game.Core
{
    /// <summary>흔들림 패턴 종류 — 소리 클립 1개와 1:1로 짝지어진다.</summary>
    public enum RattleKind
    {
        /// <summary>창문 통통 (window-knock, 약) — SFX_Window/window_knock.wav</summary>
        WindowKnock = 0,
        /// <summary>유리창 드르륵 (window-rattle, 중) — SFX_Window/window_rattle.wav</summary>
        WindowRattle = 1,
        /// <summary>문 손잡이 덜컹 (triple-assault, 강) — SFX_Handle/handle_rattle.wav</summary>
        DoorHandle = 2,
    }

    /// <summary>
    /// 문·창문 흔들림의 박자 (순수 계산 — <see cref="KnockRhythm"/>과 같은 역할·같은 규칙).
    ///
    /// <para>
    /// <b>소리와 그림이 같은 함수를 쓴다.</b> 각자 타이머를 굴리면 한 프레임씩 어긋나고,
    /// 어긋나는 순간 "덜컹 —(늦게) 흔들림"으로 분리되어 들린다 — 흔들림이 그 소리의 결과로 안 읽히면
    /// 창문·문은 그냥 혼자 떨리는 배경이 된다.
    /// </para>
    ///
    /// <para>
    /// <b>타격 시각의 원본은 실제 wav다.</b> 창문 2종은 이미 있던 클립의 온셋을 재서 옮겨 적었고,
    /// 손잡이는 <c>Tools/gen_assault_sfx.py</c>의 <c>HANDLE_HITS</c>와 같은 표다.
    /// EditMode <c>RattleSyncTests</c>가 wav를 직접 파싱해 이 표와 대조한다 —
    /// 클립을 다시 뽑으면서 박자를 바꾸면 거기가 먼저 빨개진다.
    /// </para>
    ///
    /// <para>
    /// <b>전조(소금 흔들림)와 혼동되면 안 된다.</b> 전조는 "대응해야 하는 신호"이고 이것은 분위기다.
    /// 구분은 세 겹: ① 대상이 다르다(소금 더미 ↔ 벽에 걸린 창·문) ② 붉은 어둠(gloom)이 없다
    /// ③ 박자가 다르다 — 전조는 4.5초에 걸친 느린 3타, 이건 1초 안에 몰린 연타이거나 감쇠하는 떨림이다.
    /// 진폭도 통통 &lt; 유리창 &lt; 손잡이 순으로 이벤트 강도를 따른다.
    /// </para>
    ///
    /// <para>
    /// <b>위치만 흔든다 — 회전 금지.</b> 탑뷰라 문·창을 회전시키면 "열리는" 것으로 보인다.
    /// 또한 변위는 항상 원점 기준 <b>절대값</b>으로 계산한다(누적 가산 금지) — 그래야 종료 시
    /// 원위치 복구가 정확하고 드리프트가 원리적으로 불가능하다.
    /// </para>
    /// </summary>
    public static class RattlePattern
    {
        // 타격표 (초, 세기). 창문 2종은 실제 wav의 측정 온셋, 손잡이는 생성 스크립트와 공유하는 표.
        private static readonly float[] WindowKnockTimes = { 0.00f, 0.42f };
        private static readonly float[] WindowKnockWeights = { 1.00f, 1.00f };

        // 유리창 드르륵은 개별 타격이 분리되지 않는 **연속 떨림**이다 (측정 포락선이 exp(-t/0.30)에 붙는다).
        // 그래서 타격표는 시작점 하나뿐이고 세기는 SustainTau가 만든다.
        private static readonly float[] WindowRattleTimes = { 0.00f };
        private static readonly float[] WindowRattleWeights = { 1.00f };

        // ⚠ Tools/gen_assault_sfx.py 의 HANDLE_HITS 와 같은 값 (덜컹 3타 × 2회)
        private static readonly float[] DoorHandleTimes = { 0.000f, 0.115f, 0.230f, 0.800f, 0.915f, 1.030f };
        private static readonly float[] DoorHandleWeights = { 1.00f, 0.62f, 0.78f, 0.95f, 0.60f, 0.72f };

        /// <summary>끝에서 0으로 데려가는 구간 — 없으면 마지막 프레임에 툭 끊겨 스냅백이 보인다.</summary>
        private const float TailTaperSec = 0.12f;

        // ---------- 표 접근 ----------

        private static float[] Times(RattleKind kind) => kind switch
        {
            RattleKind.WindowKnock => WindowKnockTimes,
            RattleKind.WindowRattle => WindowRattleTimes,
            _ => DoorHandleTimes,
        };

        private static float[] Weights(RattleKind kind) => kind switch
        {
            RattleKind.WindowKnock => WindowKnockWeights,
            RattleKind.WindowRattle => WindowRattleWeights,
            _ => DoorHandleWeights,
        };

        public static int HitCount(RattleKind kind) => Times(kind).Length;

        public static float HitTime(RattleKind kind, int index)
        {
            float[] t = Times(kind);
            return index >= 0 && index < t.Length ? t[index] : 0f;
        }

        public static float HitWeight(RattleKind kind, int index)
        {
            float[] w = Weights(kind);
            return index >= 0 && index < w.Length ? w[index] : 0f;
        }

        /// <summary>연속형 감쇠 시상수(초). 0이면 개별 타격형이다.</summary>
        public static float SustainTau(RattleKind kind) => kind == RattleKind.WindowRattle ? 0.32f : 0f;

        /// <summary>한 타의 시각적 여운(초). 소리보다 조금 길다 — 소리는 20ms면 끝나지만 눈은 그걸 못 본다.</summary>
        public static float DecaySec(RattleKind kind) => kind switch
        {
            RattleKind.WindowKnock => 0.09f,
            RattleKind.WindowRattle => 0.11f,
            _ => 0.13f,
        };

        /// <summary>흔들림 총 길이(초). 짝 클립의 가청 구간을 넘지 않는다 — 소리가 끝났는데 계속 떨면 유령이 된다.</summary>
        public static float DurationSec(RattleKind kind) => kind switch
        {
            RattleKind.WindowKnock => 0.62f,
            RattleKind.WindowRattle => 1.00f,
            _ => 1.35f,
        };

        /// <summary>최대 변위(유닛). 이벤트 강도 순서: 통통 &lt; 유리창 &lt; 손잡이.</summary>
        public static float Amplitude(RattleKind kind) => kind switch
        {
            RattleKind.WindowKnock => 0.018f,
            RattleKind.WindowRattle => 0.030f,
            _ => 0.048f,
        };

        /// <summary>가로 진동수(Hz). 창은 잘게(격자 진동), 문은 무겁게 — 전조 소금(26Hz)과도 겹치지 않게 벌렸다.</summary>
        public static float ShakeHzX(RattleKind kind) => kind switch
        {
            RattleKind.WindowKnock => 29f,
            RattleKind.WindowRattle => 33f,
            _ => 19f,
        };

        /// <summary>세로 진동수(Hz). 가로와 다른 주기여야 대각선으로 미끄러지지 않는다.</summary>
        public static float ShakeHzY(RattleKind kind) => kind switch
        {
            RattleKind.WindowKnock => 41f,
            RattleKind.WindowRattle => 47f,
            _ => 27f,
        };

        /// <summary>세로 변위 비율. 창도 문도 벽에 물려 있어 세로로는 거의 못 움직인다.</summary>
        public static float VerticalRatio(RattleKind kind) => kind == RattleKind.DoorHandle ? 0.35f : 0.55f;

        // ---------- 계산 ----------

        /// <summary>흔들림이 진행 중인가 (경과 &lt; 총 길이).</summary>
        public static bool IsActive(RattleKind kind, float elapsed)
            => elapsed >= 0f && elapsed < DurationSec(kind);

        /// <summary>경과까지 몇 번 때렸는가 — 소리 트리거와 그림이 공유하는 카운터.</summary>
        public static int CountUpTo(RattleKind kind, float elapsed)
        {
            float[] t = Times(kind);
            int n = 0;
            for (int i = 0; i < t.Length; i++)
                if (elapsed >= t[i]) n++;
            return n;
        }

        /// <summary>세기 포락선 0~1. 총 길이 밖에서는 **정확히 0** (원위치 복구의 근거).</summary>
        public static float Envelope(RattleKind kind, float elapsed)
        {
            float duration = DurationSec(kind);
            if (elapsed < 0f || elapsed >= duration) return 0f;

            float e;
            float tau = SustainTau(kind);
            if (tau > 0f)
            {
                e = Mathf.Exp(-elapsed / tau);
            }
            else
            {
                e = 0f;
                float decay = Mathf.Max(0.01f, DecaySec(kind));
                float[] t = Times(kind);
                float[] w = Weights(kind);
                for (int i = 0; i < t.Length; i++)
                {
                    float since = elapsed - t[i];
                    if (since < 0f) continue;
                    float v = w[i] * Mathf.Exp(-since / decay);
                    if (v > e) e = v;
                }
            }

            float left = duration - elapsed;
            if (left < TailTaperSec) e *= left / TailTaperSec;
            return Mathf.Clamp01(e);
        }

        /// <summary>
        /// 원위치 기준 <b>절대</b> 변위(유닛). 누적하지 않으므로 드리프트가 구조적으로 불가능하고,
        /// 총 길이 밖에서는 정확히 <see cref="Vector2.zero"/>다.
        /// 위상 기준을 경과 시간으로 잡아 순수 함수를 유지한다(Time.time을 안 본다 → 테스트 가능).
        /// </summary>
        public static Vector2 Offset(RattleKind kind, float elapsed)
        {
            float e = Envelope(kind, elapsed);
            if (e <= 0f) return Vector2.zero;
            float amp = Amplitude(kind) * e;
            float ph = elapsed * 2f * Mathf.PI;
            // 세로 위상을 어긋내 두 축이 동시에 0을 지나지 않게 한다 (동시에 지나면 순간 정지처럼 보인다)
            return new Vector2(
                Mathf.Sin(ph * ShakeHzX(kind)) * amp,
                Mathf.Sin(ph * ShakeHzY(kind) + 1.1f) * amp * VerticalRatio(kind));
        }
    }
}
