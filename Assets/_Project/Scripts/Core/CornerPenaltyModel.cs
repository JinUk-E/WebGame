using UnityEngine;

namespace Morae.Game.Core
{
    /// <summary>
    /// 흑화 귀퉁이당 즉시 대가의 계산부 (명세 v0.5 §1) — 순수 함수만. EditMode 테스트 대상.
    /// "벌이 4개 붕괴 시점에 한 번에 오므로 하나 버렸다가 아무 느낌이 없다"를 깨는 것이 목적이라
    /// 대가는 전부 흑 개수 n에 선형이며, MonoBehaviour 어디에도 수식이 흩어지지 않게 여기 한 곳에 모은다.
    ///
    /// 감광 예외 3종은 이 모델이 아니라 호출부의 책임이다 (여기서 계산되는 값은 실내 전역광 단 하나):
    ///   ① 창밖 여명 = 진실 채널 ② 불상 촛불 = 상수 ③ 공격 전조 점멸 = 원래 강도.
    /// </summary>
    public static class CornerPenaltyModel
    {
        /// <summary>CornerStageChanged가 실어 보내는 stage 값이 "흑"인가 — 2(흑)와 3(흑+심화) 둘 다 흑이다.</summary>
        public static bool IsBlackStage(int stage) => stage >= (int)Data.CornerStage.Black;

        /// <summary>stage 배열에서 흑 귀퉁이 수 (0~4).</summary>
        public static int CountBlack(int[] stages)
        {
            if (stages == null) return 0;
            int n = 0;
            for (int i = 0; i < stages.Length; i++)
            {
                if (IsBlackStage(stages[i])) n++;
            }
            return n;
        }

        /// <summary>
        /// 실내 전역광 강도. 페이즈 연출 가감(roomLightBias)과 흑화 감광을 **합산한 뒤 한 번만** 바닥으로 클램프한다
        /// — 각각 따로 클램프하면 이중 감광이 되어 P6처럼 이미 어두운 페이즈에서 암전된다 (v0.5 지시).
        /// </summary>
        public static float RoomLightIntensity(float baseIntensity, float dawn01, float dawnBoost,
            float roomLightBias, int blackCorners, float penaltyPerCorner, float minRoomLight)
        {
            float raw = baseIntensity + dawn01 * dawnBoost + roomLightBias - penaltyPerCorner * Mathf.Max(0, blackCorners);
            return Mathf.Max(minRoomLight, raw);
        }

        /// <summary>흑화 상시 이성 드레인(양수 크기, /초). 페이즈 상시 드레인과 **별도로 누적**된다.</summary>
        public static float SanityDrainPerSec(int blackCorners, float perCorner)
            => Mathf.Max(0, blackCorners) * Mathf.Max(0f, perCorner);

        /// <summary>
        /// 공격 간격 배수 ×(1 − k·n). 하한을 둬서 계수를 올려도 간격이 0으로 붕괴하지 않게 한다.
        /// </summary>
        public static float AttackIntervalScale(int blackCorners, float reductionPerCorner, float minScale)
            => Mathf.Max(minScale, 1f - Mathf.Max(0f, reductionPerCorner) * Mathf.Max(0, blackCorners));

        /// <summary>
        /// 페이즈 로컬 공격 시계의 배속 = TV 가속 × (1 / 간격 배수) — TV와 **곱연산**(v0.5 지시).
        /// 간격이 줄면 시계가 그만큼 빨리 흐른다.
        /// </summary>
        public static float AttackClockRate(int blackCorners, float reductionPerCorner, float minScale, float tvRate)
            => tvRate / AttackIntervalScale(blackCorners, reductionPerCorner, minScale);

        /// <summary>
        /// 귀퉁이 속삭임 볼륨 — 단계(0 백 /1 회 /2 흑 /3 흑+심화)별 테이블 조회. 테이블이 짧으면 마지막 값으로 클램프.
        /// 흑 개수가 아니라 **그 귀퉁이의 단계**로 결정된다 — 어느 쪽이 뚫렸는지가 방향으로 들려야 하기 때문.
        /// </summary>
        public static float WhisperVolume(int stage, float[] volumesByStage)
        {
            if (volumesByStage == null || volumesByStage.Length == 0) return 0f;
            int index = Mathf.Clamp(stage, 0, volumesByStage.Length - 1);
            return Mathf.Max(0f, volumesByStage[index]);
        }

        /// <summary>0.3s 러프 등 지수 감쇠 보간 계수 — 프레임률 독립 (dt가 커도 발산하지 않는다).</summary>
        public static float SmoothFactor(float deltaTime, float smoothTimeSec)
        {
            if (smoothTimeSec <= 0f) return 1f;
            return 1f - Mathf.Exp(-deltaTime / smoothTimeSec);
        }
    }
}
