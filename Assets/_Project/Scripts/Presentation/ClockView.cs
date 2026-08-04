using Morae.Game.Core;
using TMPro;
using UnityEngine;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 벽시계 소품 표시 (architecture §1.1 시계 뷰 — 월드 스페이스 TMP 골격).
    /// PhaseSequencer.DisplayedClockMin 읽기만 — 게임플레이를 호출하지 않는다.
    /// 분 값이 바뀔 때만 문자열 생성 (핫패스 할당 회피).
    /// </summary>
    public sealed class ClockView : MonoBehaviour
    {
        [SerializeField] private PhaseSequencer sequencer;
        [SerializeField] private TMP_Text label;

        private int _lastShownMin = int.MinValue;

        private void LateUpdate()
        {
            if (sequencer == null || label == null) return;

            int minutes = sequencer.DisplayedClockMin;
            if (minutes == _lastShownMin) return;
            _lastShownMin = minutes;
            label.text = ClockDisplayModel.Format(minutes);
        }
    }
}
