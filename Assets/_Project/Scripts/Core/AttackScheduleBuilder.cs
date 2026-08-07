using System;
using System.Collections.Generic;
using Morae.Game.Data;

namespace Morae.Game.Core
{
    /// <summary>
    /// 스케줄 확정된 공격 1건 (AttackScheduleBuilder 산출물 — 불변).
    /// TriggerTime은 "페이즈 로컬 공격 시계" 기준 전조 시작 시각 — TV 켜짐 동안 이 시계가 빨리 흐른다 (§2.2).
    /// 전조(TelegraphDuration)는 실시간으로 진행한다 (반응 시간 창은 TV와 무관).
    /// <para>
    /// v0.7: 공격은 항상 <b>한 귀퉁이</b>다. Corner는 RandomCorner일 때 빌드 시 확정되고,
    /// FarthestFromPlayer면 <see cref="CornerIndex.None"/>으로 남아 발동 시점에 해석된다.
    /// </para>
    /// </summary>
    public readonly struct ScheduledAttack
    {
        public readonly string Id;
        public readonly PhaseId PhaseId;
        public readonly int PhaseIndex;          // PhaseTable 행 인덱스 (전이·정렬 비교용)
        public readonly float TriggerTime;       // 페이즈 로컬 공격 시계(s) — 전조 시작 시각
        public readonly float TelegraphDuration;
        public readonly AttackTargetRule TargetRule;
        public readonly int Corner;              // RandomCorner: 확정값 / FarthestFromPlayer: CornerIndex.None

        public ScheduledAttack(string id, PhaseId phaseId, int phaseIndex, float triggerTime,
            float telegraphDuration, AttackTargetRule targetRule, int corner)
        {
            Id = id;
            PhaseId = phaseId;
            PhaseIndex = phaseIndex;
            TriggerTime = triggerTime;
            TelegraphDuration = telegraphDuration;
            TargetRule = targetRule;
            Corner = corner;
        }
    }

    /// <summary>
    /// 본편 시작 시 시드로 전 공격의 발동 시각·대상 귀퉁이를 확정 (§2.2 — 순수 함수).
    /// - 지터: baseOffset × (1 ± jitterRatio) — 재시작 변주는 시드 교체만으로 생긴다.
    /// - 페이즈 경계 불침범: trigger를 [0, 페이즈 duration − telegraph]로 클램프.
    ///   TV 가속은 공격 시계를 빨리 돌려 발동을 "당길" 뿐이므로 이 클램프만으로 전조 판정이 항상 페이즈 안에서 끝난다.
    /// - <b>v0.7 최소 간격 클램프</b>: 같은 페이즈 안에서 연속 공격이 minGapSec보다 붙지 않게 뒤로 민다.
    ///   지터에 하한이 없어서 두 공격이 5.6초까지 붙을 수 있었고, 그러면 앞 오염을 정리하기도 전에
    ///   다음 오염이 겹쳐 부적이 두 배로 탄다. 하한을 두면 지터는 "변주"로만 작동한다.
    /// </summary>
    public static class AttackScheduleBuilder
    {
        /// <summary>SO 테이블 편의 오버로드 — AttackScheduler.Begin에서 호출 (핫패스 아님).</summary>
        public static ScheduledAttack[] Build(AttackTable table, PhaseTable phases, int seed, float minGapSec)
        {
            var attacks = new AttackDef[table.Count];
            for (int i = 0; i < attacks.Length; i++) attacks[i] = table.GetAttack(i);
            var phaseDefs = new PhaseDef[phases.Count];
            for (int i = 0; i < phaseDefs.Length; i++) phaseDefs[i] = phases.GetPhase(i);
            return Build(attacks, phaseDefs, seed, minGapSec);
        }

        public static ScheduledAttack[] Build(IReadOnlyList<AttackDef> attacks, IReadOnlyList<PhaseDef> phases,
            int seed, float minGapSec)
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
                float trigger = ClampToPhase(atk.BaseOffset * (1f + jitter), phaseDuration, atk.TelegraphDuration);

                // RandomCorner여도 난수를 1회 소비한다 — 규칙이 섞여 있어도 소비 순서를 고정해야 재현된다
                int roll = rng.Next(CornerIndex.Count);
                int corner = atk.TargetRule == AttackTargetRule.RandomCorner ? roll : CornerIndex.None;

                result.Add(new ScheduledAttack(atk.Id, atk.PhaseId, phaseIndex, trigger,
                    atk.TelegraphDuration, atk.TargetRule, corner));
            }

            result.Sort(CompareByPhaseThenTime);
            EnforceMinGap(result, phases, minGapSec);
            return result.ToArray();
        }

        /// <summary>
        /// 같은 페이즈 안에서 앞 공격과의 간격이 minGapSec 미만이면 뒤로 민다.
        /// 페이즈 경계 클램프가 우선이라 밀 자리가 없으면 경계에 붙는다 — 그 경우 간격이 하한보다 좁아질 수 있지만,
        /// 그건 테이블 배치가 페이즈에 비해 빽빽하다는 신호이므로 조용히 넘기지 않고 호출자가 로그로 본다.
        /// </summary>
        private static void EnforceMinGap(List<ScheduledAttack> sorted, IReadOnlyList<PhaseDef> phases, float minGapSec)
        {
            if (minGapSec <= 0f) return;

            for (int i = 1; i < sorted.Count; i++)
            {
                ScheduledAttack prev = sorted[i - 1];
                ScheduledAttack cur = sorted[i];
                if (cur.PhaseIndex != prev.PhaseIndex) continue;

                float earliest = prev.TriggerTime + minGapSec;
                if (cur.TriggerTime >= earliest) continue;

                float phaseDuration = cur.PhaseIndex >= 0 && cur.PhaseIndex < phases.Count
                    ? phases[cur.PhaseIndex].Duration
                    : float.MaxValue;
                float pushed = ClampToPhase(earliest, phaseDuration, cur.TelegraphDuration);

                sorted[i] = new ScheduledAttack(cur.Id, cur.PhaseId, cur.PhaseIndex, pushed,
                    cur.TelegraphDuration, cur.TargetRule, cur.Corner);
            }
        }

        private static float ClampToPhase(float trigger, float phaseDuration, float telegraphDuration)
        {
            float maxTrigger = Math.Max(0f, phaseDuration - telegraphDuration);
            if (trigger < 0f) return 0f;
            return trigger > maxTrigger ? maxTrigger : trigger;
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
