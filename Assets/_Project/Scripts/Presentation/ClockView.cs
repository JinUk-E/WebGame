using Morae.Game.Core;
using TMPro;
using UnityEngine;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 벽시계 소품 표시 (architecture §1.1 — 아트 2단계에서 아날로그 문자판+바늘로 개편).
    /// PhaseSequencer.DisplayedClockMin 읽기만 — 게임플레이를 호출하지 않는다.
    /// 바늘 스프라이트는 캔버스 중심축·12시 방향 제작(절차생성) — 밑동이 축 위에서 시작하고,
    /// 스프라이트 피벗은 **Custom으로 캔버스 중심에 박혀 있다**(Tools/art-gen/fix_clock_pivot.py).
    /// 알파 경계로 잘린 rect의 Center를 쓰면 축이 바늘 몸통 중간에 놓여 "가운데가 도는" 그림이 된다 —
    /// 아트를 다시 뽑을 때 그 스크립트를 같이 돌리지 않으면 조용히 그 상태로 돌아간다.
    /// 시계방향 = 음의 Z회전. 분 값이 바뀔 때만 갱신 (핫패스 할당·회전 쓰기 회피).
    /// label(디지털 표시)은 선택 — 미배선(null)이면 바늘만 구동.
    /// </summary>
    public sealed class ClockView : MonoBehaviour
    {
        [SerializeField] private PhaseSequencer sequencer;
        [SerializeField] private TMP_Text label;      // 선택 — 아날로그 전환 후 기본 미사용
        [SerializeField] private Transform hourHand;   // 12시 기준 스프라이트, Z회전 구동
        [SerializeField] private Transform minuteHand;

        private int _lastShownMin = int.MinValue;

        private void LateUpdate()
        {
            if (sequencer == null) return;

            int minutes = sequencer.DisplayedClockMin;
            if (minutes == _lastShownMin) return;
            _lastShownMin = minutes;

            if (label != null) label.text = ClockDisplayModel.Format(minutes);

            if (minuteHand != null)
            {
                minuteHand.localRotation = Quaternion.Euler(0f, 0f, -(minutes % 60) * 6f);
            }
            if (hourHand != null)
            {
                // 시침은 분 진행을 반영해 연속 회전 (12시간 = 720분 = 360°)
                hourHand.localRotation = Quaternion.Euler(0f, 0f, -(minutes % 720) * 0.5f);
            }
        }
    }
}
