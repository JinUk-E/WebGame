using System;
using UnityEngine;

namespace Morae.Game.Data
{
    /// <summary>
    /// 공격 1행. 런타임 읽기 전용.
    /// <para>
    /// <b>v0.7 스키마 축소: minCorners/maxCorners 제거 — 공격은 항상 한 귀퉁이만 친다.</b>
    /// 동시 공격은 새 조작에서 성립하지 않는다: 4귀퉁이 동시면 최단 순회 18.3u(5.23초)에 홀드 4회가 붙어
    /// 부적 예산 안에 절대 들어오지 않는다(홀드 1.19초 이하여야 하는데 그건 홀드가 아니라 탭이다).
    /// 한 방향씩만 오면 "지금 할 일은 항상 하나"가 되어 무튜토리얼 목표와도 맞고,
    /// 스케줄러의 동시 발동 중복 방지·포화 재타겟 로직 45줄이 통째로 사라진다.
    /// <b>resolves도 제거</b> — 전 행이 true였고(실사용 0), 상쇄 개념이 사라져 의미도 없어졌다.
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class AttackDef
    {
        [SerializeField] private string id;                  // 로그·디버그용
        [SerializeField] private PhaseId phaseId;
        [SerializeField] private float baseOffset;           // 페이즈 시작 기준(s). (baseOffset×(1+jitter))+telegraph ≤ duration 배치
        [SerializeField] private float jitterRatio;          // ±비율, 기본 0.2 (재시작 변주)
        [SerializeField] private AttackTargetRule targetRule;
        [SerializeField] private float telegraphDuration;    // 기본 4.5

        public string Id => id;
        public PhaseId PhaseId => phaseId;
        public float BaseOffset => baseOffset;
        public float JitterRatio => jitterRatio;
        public AttackTargetRule TargetRule => targetRule;
        public float TelegraphDuration => telegraphDuration;

        public AttackDef() { }

        /// <summary>에디터 빌더용 생성자.</summary>
        public AttackDef(string id, PhaseId phaseId, float baseOffset, float jitterRatio,
            AttackTargetRule targetRule, float telegraphDuration)
        {
            this.id = id;
            this.phaseId = phaseId;
            this.baseOffset = baseOffset;
            this.jitterRatio = jitterRatio;
            this.targetRule = targetRule;
            this.telegraphDuration = telegraphDuration;
        }
    }

    /// <summary>공격 테이블 (v0.7: 12행, 전부 단일 귀퉁이 + P6 연발은 코드 시퀀스). 런타임 읽기 전용.</summary>
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
