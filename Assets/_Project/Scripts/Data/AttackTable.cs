using System;
using UnityEngine;

namespace Morae.Game.Data
{
    /// <summary>
    /// 공격 1행 (architecture §2.2, v0.3 개정). 런타임 읽기 전용.
    /// v0.3 스키마 변경: dualCorner(bool) → minCorners/maxCorners(1~4) — "N동시 랜덤"은
    /// 스케줄 빌드 시 시드로 동시 수를 확정한다 (min==max = 고정 동시 수).
    /// </summary>
    [Serializable]
    public sealed class AttackDef
    {
        [SerializeField] private string id;                  // 로그·디버그용
        [SerializeField] private PhaseId phaseId;
        [SerializeField] private float baseOffset;           // 페이즈 시작 기준(s). (baseOffset×(1+jitter))+telegraph ≤ duration 배치
        [SerializeField] private float jitterRatio;          // ±비율, 기본 0.2 (재시작 변주)
        [SerializeField, Range(1, 4)] private int minCorners = 1; // 동시 공격 귀퉁이 수 하한
        [SerializeField, Range(1, 4)] private int maxCorners = 1; // 상한 (min==max = 고정)
        [SerializeField] private AttackTargetRule targetRule;
        [SerializeField] private float telegraphDuration;    // 기본 3.0
        [SerializeField] private bool resolves;              // false = 전조만 (튜닝 여지, 기본 true)

        public string Id => id;
        public PhaseId PhaseId => phaseId;
        public float BaseOffset => baseOffset;
        public float JitterRatio => jitterRatio;
        public int MinCorners => minCorners;
        public int MaxCorners => maxCorners;
        public AttackTargetRule TargetRule => targetRule;
        public float TelegraphDuration => telegraphDuration;
        public bool Resolves => resolves;

        public AttackDef() { }

        /// <summary>에디터 빌더·EditMode 테스트용 생성자.</summary>
        public AttackDef(string id, PhaseId phaseId, float baseOffset, float jitterRatio,
            int minCorners, int maxCorners, AttackTargetRule targetRule, float telegraphDuration, bool resolves)
        {
            this.id = id;
            this.phaseId = phaseId;
            this.baseOffset = baseOffset;
            this.jitterRatio = jitterRatio;
            this.minCorners = Mathf.Clamp(minCorners, 1, CornerIndex.Count);
            this.maxCorners = Mathf.Clamp(maxCorners, this.minCorners, CornerIndex.Count);
            this.targetRule = targetRule;
            this.telegraphDuration = telegraphDuration;
            this.resolves = resolves;
        }
    }

    /// <summary>공격 테이블 (명세 v0.3 공격 열: 3/3/3/2/4 = 15행 + P6 함정 2웨이브는 코드 시퀀스). 런타임 읽기 전용.</summary>
    [CreateAssetMenu(menuName = "Morae/Attack Table", fileName = "AttackTable")]
    public sealed class AttackTable : ScriptableObject
    {
        [SerializeField] private AttackDef[] attacks;

        public int Count => attacks != null ? attacks.Length : 0;
        public AttackDef GetAttack(int index) => attacks[index];

#if UNITY_EDITOR
        /// <summary>에디터 빌더 전용 — 런타임 호출 금지.</summary>
        public void EditorSetAttacks(AttackDef[] value) => attacks = value;
#endif
    }
}
