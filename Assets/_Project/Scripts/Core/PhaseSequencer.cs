using Morae.Game.Data;
using UnityEngine;

namespace Morae.Game.Core
{
    /// <summary>
    /// 본편 420초 진행·페이즈 전이 관리 (architecture §1.1).
    /// PhaseTable SO만 읽고, 결과는 GameEvents.PhaseChanged + 읽기 프로퍼티로 노출.
    /// 마지막 페이즈는 종료하지 않는다 — PhaseElapsed가 duration을 넘어 계속 흐른다
    /// (P8의 duration 초과 offset 이벤트(K씨 개문 60s)를 EventDirector가 소비할 수 있게. 보간 값은 끝에서 클램프).
    /// </summary>
    public sealed class PhaseSequencer : MonoBehaviour
    {
        [SerializeField] private PhaseTable phaseTable;

        private int _phaseIndex;
        private PhaseDef _current;

        public bool IsRunning { get; private set; }
        public PhaseId CurrentPhase => _current != null ? _current.PhaseId : PhaseId.P1;
        /// <summary>PhaseTable 행 인덱스 — AttackScheduler의 페이즈 전이 폴링·스케줄 비교용.</summary>
        public int CurrentPhaseIndex => _phaseIndex;
        public PhaseDef CurrentPhaseDef => _current;
        public float TotalElapsed { get; private set; }
        public float PhaseElapsed { get; private set; }

        /// <summary>진실 게임 시각(분, 페이즈 내 선형 보간). 01:00 = 60.</summary>
        public float TrueGameTimeMin { get; private set; }
        /// <summary>시계 표시(오염 채널) — ClockView가 읽는 값.</summary>
        public int DisplayedClockMin { get; private set; }
        /// <summary>창밖 여명 진행도 0~1 (진실 채널) — LightingController가 읽는 값.</summary>
        public float Dawn01 { get; private set; }

        private void Awake()
        {
            // 시작 전에도 뷰가 초기값(01:00, 여명 0)을 읽을 수 있게 페이즈 0 기준으로 셋업
            if (phaseTable != null && phaseTable.Count > 0)
            {
                _current = phaseTable.GetPhase(0);
                Recalculate();
            }
        }

        /// <summary>본편 시작 — GameFlowController가 호출 (SerializeField 직접 참조, §1.2 아래 방향 제어).</summary>
        public void Begin()
        {
            if (phaseTable == null || phaseTable.Count == 0)
            {
                Debug.LogError("[PHASE] PhaseTable이 배선되지 않음 — 시퀀서 시작 불가", this);
                return;
            }
            _phaseIndex = 0;
            _current = phaseTable.GetPhase(0);
            TotalElapsed = 0f;
            PhaseElapsed = 0f;
            IsRunning = true;
            Recalculate();
            GameEvents.RaisePhaseChanged(_current.PhaseId);
        }

        /// <summary>게임오버·엔딩 시 정지 — 값은 정지 시점 그대로 유지.</summary>
        public void StopSequence() => IsRunning = false;

        private void Update()
        {
            if (!IsRunning) return;

            float dt = Time.deltaTime;
            TotalElapsed += dt;
            PhaseElapsed += dt;

            // 페이즈 전이 (마지막 페이즈는 유지)
            while (_phaseIndex < phaseTable.Count - 1 && PhaseElapsed >= _current.Duration)
            {
                PhaseElapsed -= _current.Duration;
                _phaseIndex++;
                _current = phaseTable.GetPhase(_phaseIndex);
                GameEvents.RaisePhaseChanged(_current.PhaseId);
            }

            Recalculate();
        }

        private void Recalculate()
        {
            float t = _current.Duration > 0f ? Mathf.Clamp01(PhaseElapsed / _current.Duration) : 1f;
            TrueGameTimeMin = Mathf.Lerp(_current.GameTimeStartMin, _current.GameTimeEndMin, t);
            Dawn01 = Mathf.Lerp(_current.DawnStart, _current.DawnEnd, t);
            DisplayedClockMin = ClockDisplayModel.DisplayedMinutes(TrueGameTimeMin, _current);
        }
    }
}
