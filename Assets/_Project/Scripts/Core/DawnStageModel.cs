using UnityEngine;

namespace Morae.Game.Core
{
    /// <summary>
    /// 여명 연출의 계산부 (명세 v0.7 §1·§2) — 순수 함수만. EditMode 테스트 대상.
    ///
    /// <para>
    /// <b>왜 이 모델이 존재하는가 (설계 역전의 교정).</b> v0.6까지 여명 판별은 "방 대비 창이 밝은가"라는
    /// <b>상대적 대비</b>에 기대고 있었다. 그런데 방 조도는 v0.5 흑화 감광이 좌우한다 —
    /// 결과적으로 <b>방어를 잘한 플레이어일수록(흑화 0 → 방이 밝음) 진실 채널이 안 보였다.</b>
    /// 실력과 정보량이 반대로 붙어 있었다는 뜻이다.
    /// 그래서 여명을 <b>밝기</b>가 아니라 <b>모양과 색</b>으로 옮긴다 — 둘 다 절대량이라
    /// 방이 얼마나 어둡든 똑같이 읽힌다.
    /// </para>
    ///
    /// <para>
    /// <b>무오염의 증명 방식.</b> 이 모델의 <b>모든</b> 공개 함수는 <c>dawn01</c>(또는 그것에서 나온 stage)
    /// <b>하나만</b> 받는다. 소금 상태·페이즈 <c>roomLightBias</c>·학습 배율·에디터 오버라이드를
    /// 넣을 자리가 <b>구조적으로 없다</b>(v0.5 감광 예외① 유지). 표현 계층이 실수로 섞으려 해도
    /// 인자를 만들 수 없다. 회귀 방어는 <c>DawnLegibilityTests</c>가 소스까지 훑어 못 박는다.
    /// </para>
    ///
    /// <para>
    /// <b>왜 계단인가.</b> 사람은 느리고 균일한 밝기 변화를 감지하지 못한다 — 비교 기준이 없기 때문이다.
    /// 420초에 걸쳐 연속 보간하면 "변하고 있다"가 영영 학습되지 않는다.
    /// 그래서 3개의 경계로 <b>4단계</b>로 끊는다. 각 경계는 "아까와 다르다"가 성립하는 사건이 된다.
    /// </para>
    ///
    /// <para>
    /// <b>⚠ P6 함정의 애매함은 보호 대상이다.</b> P6(최후의 함정)의 여명 구간은 정확히 0.30~0.50이며,
    /// 그때 시계는 07:25를 가리키고 문밖에서 아침이라고 말한다. 이 구간이 "딱 떨어지는 한 단계"로
    /// 보이면 플레이어는 "아직 아니다"를 <b>확신</b>하게 되어 함정이 죽는다. 그래서 경계 하나(<c>0.38</c>)를
    /// <b>P6 한복판에</b> 둔다 — 함정 도중에 창이 한 단계 올라가므로 "아침이 오는 중"으로도 읽히고
    /// "아직 마지막 단계가 아니다"로도 읽힌다. 모호함이 유지되는 이유가 이것이다.
    /// (경계를 0.30이나 0.50에 두면 P6 전체가 단일 색이 되어 판별이 쉬워진다 — 금지.)
    /// </para>
    /// </summary>
    public static class DawnStageModel
    {
        /// <summary>여명 단계 수 — 명세 v0.7 "3~4단계 계단".</summary>
        public const int StageCount = 4;

        /// <summary>
        /// 계단 경계 (dawn01, 오름차순). <c>dawn &gt;= Thresholds[i]</c> 이면 최소 i+1단계다.
        /// <list type="bullet">
        ///   <item>0.06 — P5 진입 17초(전체 267s). "창이 변하기 시작한다"의 <b>첫 학습</b>.</item>
        ///   <item>0.38 — <b>P6 한복판</b>(진입 16초/40초). 함정의 모호함을 지키는 경계.</item>
        ///   <item>0.62 — P7 진입 10초(전체 385s). "진짜 아침"의 시작.</item>
        /// </list>
        /// </summary>
        public static readonly float[] Thresholds = { 0.06f, 0.38f, 0.62f };

        /// <summary>P6(최후의 함정) 여명 구간 — PhaseTable의 P6 dawnStart/dawnEnd와 같은 값.</summary>
        public const float TrapDawnStart = 0.30f;
        public const float TrapDawnEnd = 0.50f;

        /// <summary>
        /// 계단 전환에 주는 시간(초). 0이면 한 프레임에 튀어 버그처럼 보이고,
        /// 길면 다시 "연속 보간"이 되어 감지 불가로 돌아간다. 경계 간격이 최소 80초인 것에 비하면
        /// 0.35초는 여전히 <b>사건</b>이다 — 계단의 모서리를 다듬는 정도.
        /// </summary>
        public const float StepBlendSec = 0.35f;

        // ---------- 단계 ----------

        /// <summary>
        /// 여명 진행도 → 단계(0~3). <b>인자는 dawn01 하나뿐</b>이라는 것이 이 함수의 계약이다.
        /// dawn01은 페이즈 진행에 따라 단조 증가하므로 히스테리시스가 필요 없다
        /// (되돌아가는 경로가 없어 경계에서 떨릴 수 없다).
        /// </summary>
        public static int Stage(float dawn01)
        {
            float d = Mathf.Clamp01(dawn01);
            int stage = 0;
            for (int i = 0; i < Thresholds.Length; i++)
            {
                if (d >= Thresholds[i]) stage = i + 1;
            }
            return stage;
        }

        // ---------- ② 창호지 색 (명세 v0.7 §2) ----------

        /// <summary>
        /// 창호지에 얹는 틴트. <b>색상 변화는 밝기 변화보다 훨씬 잘 감지된다</b> —
        /// 남색 → 회청 → 옅은 주황 → 아침으로 <b>색상(hue)이 파랑에서 주황으로 넘어간다.</b>
        ///
        /// <para>
        /// 창 렌더러는 <b>무광</b>(Sprites-Default)이라 이 색이 그대로 화면에 나간다 —
        /// 실내 조도가 얼마든(흑화 4개든 학습 스포트라이트든) 창은 같은 색이다.
        /// v0.6에서 소금을 무광으로 바꿔 "감광이 심할수록 오히려 또렷하게" 만든 것과 같은 처방이다.
        /// </para>
        /// </summary>
        public static Color PaperColor(int stage)
        {
            switch (Mathf.Clamp(stage, 0, StageCount - 1))
            {
                case 0: return new Color(0.10f, 0.13f, 0.30f);  // 밤 남색 — 파랑이 지배
                case 1: return new Color(0.34f, 0.44f, 0.58f);  // 회청 — 여전히 파랑이나 확실히 밝다
                case 2: return new Color(0.74f, 0.55f, 0.40f);  // 옅은 주황 — 여기서 색상이 넘어간다
                default: return new Color(1.00f, 0.78f, 0.50f); // 아침 — 창이 방에서 가장 밝은 것
            }
        }

        // ---------- ① 바닥 창틀 빛 무늬 (명세 v0.7 §1) ----------

        /// <summary>바닥 무늬가 시작되는 y — 우측 구역 벽 발치(Wall_Top_Right 아래 0.675) 바로 아래.</summary>
        public const float PatchAnchorY = 0.62f;

        /// <summary>창 중심 x (Room/Window 좌표) — 빛은 창 아래에서 시작한다.</summary>
        public const float WindowCenterX = 2.5f;

        /// <summary>
        /// 바닥 무늬의 길이(유닛). <b>아침이 갈수록 길어진다</b> — 위치·형태가 바뀌므로
        /// "달라졌다"가 한눈에 읽힌다(밝기 변화와 달리 비교 기준이 화면 안에 있다).
        /// 0단계는 0 = 무늬 자체가 없다.
        /// </summary>
        public static float PatchLength(int stage)
        {
            switch (Mathf.Clamp(stage, 0, StageCount - 1))
            {
                case 0: return 0f;
                case 1: return 0.85f;
                case 2: return 1.55f;
                default: return 2.40f;
            }
        }

        /// <summary>바닥 무늬의 폭(유닛). 창호지 폭(1.64u)에서 시작해 조금씩 퍼진다.</summary>
        public static float PatchWidth(int stage)
        {
            switch (Mathf.Clamp(stage, 0, StageCount - 1))
            {
                case 0: return 1.70f;
                case 1: return 1.78f;
                case 2: return 1.90f;
                default: return 2.05f;
            }
        }

        /// <summary>
        /// 무늬 중심의 가로 이동(유닛). 해가 뜨면서 빛이 드는 각도가 바뀌는 것 —
        /// <b>위치 변화</b>는 형태 변화와 함께 "달라졌다"를 만드는 두 번째 축이다.
        /// </summary>
        public static float PatchCenterX(int stage)
        {
            switch (Mathf.Clamp(stage, 0, StageCount - 1))
            {
                case 0: return WindowCenterX;
                case 1: return WindowCenterX + 0.05f;
                case 2: return WindowCenterX + 0.13f;
                default: return WindowCenterX + 0.24f;
            }
        }

        /// <summary>
        /// <b>흐릿한 빛무리</b>의 알파. 초반에는 형태 없는 무리였다가 점점 걷힌다 —
        /// 격자 알파와 반대로 움직여 "선명해진다"를 만든다.
        /// </summary>
        public static float HazeAlpha(int stage)
        {
            switch (Mathf.Clamp(stage, 0, StageCount - 1))
            {
                case 0: return 0f;
                case 1: return 0.55f;
                case 2: return 0.40f;
                default: return 0.22f;
            }
        }

        /// <summary>
        /// <b>창틀 격자</b>의 알파. 뒤로 갈수록 살[창살]의 그림자가 또렷해진다.
        /// (흐림 ↓ + 격자 ↑ = "선명해진다". 스프라이트 한 장으로는 표현할 수 없어 두 겹으로 나눴다.)
        /// </summary>
        public static float GridAlpha(int stage)
        {
            switch (Mathf.Clamp(stage, 0, StageCount - 1))
            {
                case 0: return 0f;
                case 1: return 0.10f;
                case 2: return 0.42f;
                default: return 0.85f;
            }
        }

        /// <summary>
        /// 바닥 무늬의 틴트 — 창호지와 같은 색 계열이되 <b>바닥에 떨어진 빛</b>이라 더 밝다.
        /// 창과 바닥이 다른 색이면 "저 빛이 창에서 왔다"가 안 읽힌다.
        /// </summary>
        public static Color PatchTint(int stage)
        {
            switch (Mathf.Clamp(stage, 0, StageCount - 1))
            {
                case 0: return new Color(0.30f, 0.38f, 0.55f);
                case 1: return new Color(0.56f, 0.66f, 0.82f);  // 회청 — 차가운 새벽빛
                case 2: return new Color(0.92f, 0.78f, 0.60f);
                default: return new Color(1.00f, 0.88f, 0.68f); // 아침 — 따뜻한 햇빛
            }
        }

        // ---------- 전환 ----------

        /// <summary>
        /// 계단 전환 진행률 0~1 (0 = 직전 단계, 1 = 새 단계). 프레임률과 무관하도록
        /// <b>경과 시간</b>으로 계산한다 — <c>Time.time</c> 위상이 아니라 경과라서 순수 함수로 남는다
        /// (<see cref="RattlePattern"/>·<see cref="KnockRhythm"/>과 같은 규칙).
        /// </summary>
        public static float StepBlend01(float sinceStepSec, float blendSec = StepBlendSec)
        {
            if (blendSec <= 0f) return 1f;
            float t = Mathf.Clamp01(sinceStepSec / blendSec);
            return t * t * (3f - 2f * t); // smoothstep — 시작·끝이 부드러워 "튀었다"로 안 보인다
        }
    }
}
