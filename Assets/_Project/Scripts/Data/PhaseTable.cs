using System;
using UnityEngine;

namespace Morae.Game.Data
{
    /// <summary>페이즈 1행 (architecture §2.1). 런타임 읽기 전용.</summary>
    [Serializable]
    public sealed class PhaseDef
    {
        [SerializeField] private PhaseId phaseId;
        [SerializeField] private float duration;             // 실시간 길이(s), 7행 합 = 420
        [SerializeField] private int gameTimeStartMin;       // 게임 내 시각(분). 01:00 = 60
        [SerializeField] private int gameTimeEndMin;
        [SerializeField] private ClockMode clockMode;
        [SerializeField] private int clockParamMin;          // Frozen: 정지 오프셋 / Offset: 가감 / Fixed: 표시 시각
        [SerializeField, Range(0f, 1f)] private float dawnStart; // 창밖 여명 진행도(진실 채널)
        [SerializeField, Range(0f, 1f)] private float dawnEnd;
        [SerializeField] private float passiveSanityDrain;   // 상시 이성 감소(/s) — P4 이후 0.5

        public PhaseId PhaseId => phaseId;
        public float Duration => duration;
        public int GameTimeStartMin => gameTimeStartMin;
        public int GameTimeEndMin => gameTimeEndMin;
        public ClockMode ClockMode => clockMode;
        public int ClockParamMin => clockParamMin;
        public float DawnStart => dawnStart;
        public float DawnEnd => dawnEnd;
        public float PassiveSanityDrain => passiveSanityDrain;

        public PhaseDef() { }

        /// <summary>에디터 빌더·EditMode 테스트용 생성자. 런타임 게임 코드는 SO 에셋만 읽는다.</summary>
        public PhaseDef(PhaseId phaseId, float duration, int gameTimeStartMin, int gameTimeEndMin,
            ClockMode clockMode, int clockParamMin, float dawnStart, float dawnEnd, float passiveSanityDrain)
        {
            this.phaseId = phaseId;
            this.duration = duration;
            this.gameTimeStartMin = gameTimeStartMin;
            this.gameTimeEndMin = gameTimeEndMin;
            this.clockMode = clockMode;
            this.clockParamMin = clockParamMin;
            this.dawnStart = dawnStart;
            this.dawnEnd = dawnEnd;
            this.passiveSanityDrain = passiveSanityDrain;
        }
    }

    /// <summary>페이즈 배분표 (명세 §1 — 7행). 런타임 읽기 전용.</summary>
    [CreateAssetMenu(menuName = "Morae/Phase Table", fileName = "PhaseTable")]
    public sealed class PhaseTable : ScriptableObject
    {
        [SerializeField] private PhaseDef[] phases;

        public int Count => phases != null ? phases.Length : 0;
        public PhaseDef GetPhase(int index) => phases[index];

#if UNITY_EDITOR
        /// <summary>에디터 빌더 전용 — 런타임 호출 금지.</summary>
        public void EditorSetPhases(PhaseDef[] value) => phases = value;
#endif
    }
}
