using System;
using UnityEngine;

namespace Morae.Game.Data
{
    /// <summary>공격 1행 (architecture §2.2). 런타임 읽기 전용.</summary>
    [Serializable]
    public sealed class AttackDef
    {
        [SerializeField] private string id;                  // 로그·디버그용
        [SerializeField] private PhaseId phaseId;
        [SerializeField] private float baseOffset;           // 페이즈 시작 기준(s). duration의 90% 이내 배치
        [SerializeField] private float jitterRatio;          // ±비율, 기본 0.2 (재시작 변주)
        [SerializeField] private bool dualCorner;            // 동시 2곳 (P3 ×1, P4 ×2)
        [SerializeField] private AttackTargetRule targetRule;
        [SerializeField] private float telegraphDuration;    // 기본 3.0
        [SerializeField] private bool resolves;              // false = 전조만 (P5 튜닝 여지, 기본 true)

        public string Id => id;
        public PhaseId PhaseId => phaseId;
        public float BaseOffset => baseOffset;
        public float JitterRatio => jitterRatio;
        public bool DualCorner => dualCorner;
        public AttackTargetRule TargetRule => targetRule;
        public float TelegraphDuration => telegraphDuration;
        public bool Resolves => resolves;

        public AttackDef() { }

        /// <summary>에디터 빌더·EditMode 테스트용 생성자.</summary>
        public AttackDef(string id, PhaseId phaseId, float baseOffset, float jitterRatio,
            bool dualCorner, AttackTargetRule targetRule, float telegraphDuration, bool resolves)
        {
            this.id = id;
            this.phaseId = phaseId;
            this.baseOffset = baseOffset;
            this.jitterRatio = jitterRatio;
            this.dualCorner = dualCorner;
            this.targetRule = targetRule;
            this.telegraphDuration = telegraphDuration;
            this.resolves = resolves;
        }
    }

    /// <summary>공격 테이블 (명세 §1 공격 열: 1/1/3/3/1 = 9행). 런타임 읽기 전용.</summary>
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
