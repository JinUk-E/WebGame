using Morae.Game.Data;
using UnityEngine;

namespace Morae.Game.Core
{
    /// <summary>
    /// 시계 표시(오염 채널) 계산 — 순수 C# (architecture §2.1, EditMode 테스트 대상).
    /// 입력: 진실 게임 시각(분) + 현재 PhaseDef → 출력: 표시할 분 값.
    /// </summary>
    public static class ClockDisplayModel
    {
        /// <summary>clockMode 4종에 따른 표시 시각(분). 표시는 정수 분 단위(플로어).</summary>
        public static int DisplayedMinutes(float trueGameTimeMin, PhaseDef phase)
        {
            int trueMin = Mathf.FloorToInt(trueGameTimeMin);
            switch (phase.ClockMode)
            {
                case ClockMode.Sync:
                    return trueMin;
                case ClockMode.Frozen:
                    // 진실 시각으로 진행하다 (페이즈 종료 시각 + param)에서 정지.
                    // 예) P2: end 240, param -5 → 03:55까지 가고 멈춤 (미세 오작동 — 불신 학습)
                    return Mathf.Min(trueMin, phase.GameTimeEndMin + phase.ClockParamMin);
                case ClockMode.Offset:
                    return trueMin + phase.ClockParamMin;
                case ClockMode.Fixed:
                    return phase.ClockParamMin;
                default:
                    return trueMin;
            }
        }

        /// <summary>분 값 → "HH:MM" (24시간 랩어라운드). 할당이 있으므로 뷰는 분 변화 시에만 호출할 것.</summary>
        public static string Format(int minutes)
        {
            int m = ((minutes % 1440) + 1440) % 1440;
            return $"{m / 60:D2}:{m % 60:D2}";
        }
    }
}
