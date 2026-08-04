using System;
using System.Collections.Generic;
using Morae.Game.Data;

namespace Morae.Game.Core
{
    /// <summary>
    /// 스케줄 확정된 공격 1건 (AttackScheduleBuilder 산출물 — 불변).
    /// TriggerTime은 "페이즈 로컬 공격 시계" 기준 전조 시작 시각 — TV 켜짐 동안 이 시계가 빨리 흐른다 (architecture §2.2).
    /// 전조(TelegraphDuration)는 실시간으로 진행한다 (반응 시간 창은 TV와 무관).
    /// </summary>
    public readonly struct ScheduledAttack
    {
        public readonly string Id;
        public readonly PhaseId PhaseId;
        public readonly int PhaseIndex;          // PhaseTable 행 인덱스 (전이·정렬 비교용)
        public readonly float TriggerTime;       // 페이즈 로컬 공격 시계(s) — 전조 시작 시각
        public readonly float TelegraphDuration;
        public readonly bool Resolves;           // false = 전조만 내고 판정 생략 (P5 튜닝 여지)
        public readonly AttackTargetRule TargetRule;
        public readonly bool DualCorner;
        public readonly int CornerA;             // RandomCorner: 확정 / FarthestFromPlayer: CornerIndex.None (발동 시점에 해석)
        public readonly int CornerB;             // 단일 공격 = CornerIndex.None

        public ScheduledAttack(string id, PhaseId phaseId, int phaseIndex, float triggerTime,
            float telegraphDuration, bool resolves, AttackTargetRule targetRule, bool dualCorner,
            int cornerA, int cornerB)
        {
            Id = id;
            PhaseId = phaseId;
            PhaseIndex = phaseIndex;
            TriggerTime = triggerTime;
            TelegraphDuration = telegraphDuration;
            Resolves = resolves;
            TargetRule = targetRule;
            DualCorner = dualCorner;
            CornerA = cornerA;
            CornerB = cornerB;
        }
    }

    /// <summary>
    /// 본편 시작 시 시드로 전 공격의 발동 시각·대상 귀퉁이를 확정 (architecture §2.2 — 순수 함수, EditMode 테스트 1순위).
    /// - 지터: baseOffset × (1 ± jitterRatio) — 재시작 변주는 시드 교체만으로 생긴다.
    /// - 페이즈 경계 불침범: trigger를 [0, 페이즈 duration − telegraph]로 클램프.
    ///   TV 가속은 공격 시계를 빨리 돌려 발동을 "당길" 뿐(실시간 발동 ≤ trigger)이므로
    ///   이 클램프만으로 전조 판정이 실시간에서도 항상 페이즈 안에서 끝난다.
    /// - FarthestFromPlayer는 발동 시점의 플레이어 위치가 필요 — 귀퉁이를 None으로 두고 AttackScheduler가 발동 프레임에 해석.
    /// </summary>
    public static class AttackScheduleBuilder
    {
        /// <summary>SO 테이블 편의 오버로드 — AttackScheduler.Begin에서 호출 (핫패스 아님).</summary>
        public static ScheduledAttack[] Build(AttackTable table, PhaseTable phases, int seed)
        {
            var attacks = new AttackDef[table.Count];
            for (int i = 0; i < attacks.Length; i++) attacks[i] = table.GetAttack(i);
            var phaseDefs = new PhaseDef[phases.Count];
            for (int i = 0; i < phaseDefs.Length; i++) phaseDefs[i] = phases.GetPhase(i);
            return Build(attacks, phaseDefs, seed);
        }

        public static ScheduledAttack[] Build(IReadOnlyList<AttackDef> attacks, IReadOnlyList<PhaseDef> phases, int seed)
        {
            var rng = new Random(seed);
            var result = new List<ScheduledAttack>(attacks.Count);

            // 테이블 행 순서대로 난수를 소비 — 같은 시드는 항상 같은 스케줄 (재현성 보장의 전제)
            for (int i = 0; i < attacks.Count; i++)
            {
                AttackDef atk = attacks[i];
                int phaseIndex = FindPhaseIndex(phases, atk.PhaseId);
                float phaseDuration = phaseIndex >= 0 ? phases[phaseIndex].Duration : float.MaxValue;

                float jitter = (float)(rng.NextDouble() * 2.0 - 1.0) * atk.JitterRatio;
                float trigger = atk.BaseOffset * (1f + jitter);
                float maxTrigger = Math.Max(0f, phaseDuration - atk.TelegraphDuration);
                if (trigger < 0f) trigger = 0f;
                else if (trigger > maxTrigger) trigger = maxTrigger;

                int cornerA = CornerIndex.None;
                int cornerB = CornerIndex.None;
                if (atk.TargetRule == AttackTargetRule.RandomCorner)
                {
                    cornerA = rng.Next(0, CornerIndex.Count);
                    if (atk.DualCorner)
                    {
                        // 서로 다른 두 귀퉁이 보장: A 제외 3곳 중 하나
                        cornerB = (cornerA + 1 + rng.Next(0, CornerIndex.Count - 1)) % CornerIndex.Count;
                    }
                }

                result.Add(new ScheduledAttack(atk.Id, atk.PhaseId, phaseIndex, trigger,
                    atk.TelegraphDuration, atk.Resolves, atk.TargetRule, atk.DualCorner, cornerA, cornerB));
            }

            result.Sort(CompareByPhaseThenTime);
            return result.ToArray();
        }

        private static int FindPhaseIndex(IReadOnlyList<PhaseDef> phases, PhaseId id)
        {
            for (int i = 0; i < phases.Count; i++)
            {
                if (phases[i].PhaseId == id) return i;
            }
            return -1;
        }

        private static int CompareByPhaseThenTime(ScheduledAttack a, ScheduledAttack b)
        {
            int byPhase = a.PhaseIndex.CompareTo(b.PhaseIndex);
            if (byPhase != 0) return byPhase;
            int byTime = a.TriggerTime.CompareTo(b.TriggerTime);
            if (byTime != 0) return byTime;
            return string.CompareOrdinal(a.Id, b.Id); // 동시각 타이브레이크 — 정렬 결정성 보장
        }
    }
}
