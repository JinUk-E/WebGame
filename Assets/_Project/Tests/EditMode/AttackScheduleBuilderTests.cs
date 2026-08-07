using System.Collections.Generic;
using Morae.Game.Core;
using Morae.Game.Data;
using NUnit.Framework;

namespace Morae.Game.Tests.EditMode
{
    /// <summary>
    /// AttackScheduleBuilder — 테스트 1순위 (architecture §2.2, 명세 v0.3).
    /// 횟수 보장(3/3/3/2/4=15) · 페이즈 경계 불침범(전조 판정 포함) · 시드 재현성 · 지터 범위 ·
    /// N동시 랜덤(min~max·서로 다른 귀퉁이) · P6 무공격(함정 정적 보장) · targetRule 처리.
    /// 테이블은 명세 v0.3 복제본 — DataAssetBuilder(에디터 asmdef)와 값 동일.
    /// </summary>
    public sealed class AttackScheduleBuilderTests
    {
        private const float Jitter = 0.2f;
        private const float Telegraph = 4.5f;   // 실제 AttackTable과 같은 값 유지 (v0.6.1 정정)

        // 명세 v0.3 — 8페이즈, 본편 01:00~07:30 (합 420s)
        private static PhaseDef[] SpecPhases() => new[]
        {
            new PhaseDef(PhaseId.P1, 60f,  60, 140, ClockMode.Sync,    0, 0f,   0f,   0f),
            new PhaseDef(PhaseId.P2, 60f, 140, 220, ClockMode.Frozen, -5, 0f,   0f,   0f),
            new PhaseDef(PhaseId.P3, 70f, 220, 300, ClockMode.Offset, 40, 0f,   0f,   0f),
            new PhaseDef(PhaseId.P4, 60f, 300, 340, ClockMode.Offset, -30, 0f,  0f,   0f),
            new PhaseDef(PhaseId.P5, 85f, 340, 410, ClockMode.Offset, -30, 0f,  0.3f, 0.5f),
            new PhaseDef(PhaseId.P6, 40f, 410, 420, ClockMode.Fixed, 445, 0.3f, 0.5f, 0.5f),
            new PhaseDef(PhaseId.P7, 30f, 420, 450, ClockMode.Fixed, 445, 0.5f, 0.85f, 0.5f),
            new PhaseDef(PhaseId.P8, 15f, 450, 470, ClockMode.Fixed, 445, 0.85f, 1f,  0.5f),
        };

        // 명세 v0.3 공격 열: 3/3/3/2/4 = 15행 (P6 함정 2웨이브는 코드 시퀀스 — 테이블 밖)
        private static AttackDef[] SpecAttacks() => new[]
        {
            new AttackDef("atk-p1-1", PhaseId.P1, 12f, Jitter, 1, 1, AttackTargetRule.RandomCorner, Telegraph, true),
            new AttackDef("atk-p1-2", PhaseId.P1, 28f, Jitter, 1, 1, AttackTargetRule.RandomCorner, Telegraph, true),
            new AttackDef("atk-p1-3", PhaseId.P1, 45f, Jitter, 1, 1, AttackTargetRule.RandomCorner, Telegraph, true),
            new AttackDef("atk-p2-1", PhaseId.P2, 10f, Jitter, 2, 2, AttackTargetRule.RandomCorner, Telegraph, true),
            new AttackDef("atk-p2-2", PhaseId.P2, 27f, Jitter, 2, 2, AttackTargetRule.RandomCorner, Telegraph, true),
            new AttackDef("atk-p2-3", PhaseId.P2, 44f, Jitter, 2, 2, AttackTargetRule.RandomCorner, Telegraph, true),
            new AttackDef("atk-p3-1", PhaseId.P3, 12f, Jitter, 2, 3, AttackTargetRule.RandomCorner, Telegraph, true),
            new AttackDef("atk-p3-2", PhaseId.P3, 32f, Jitter, 2, 3, AttackTargetRule.RandomCorner, Telegraph, true),
            new AttackDef("atk-p3-3", PhaseId.P3, 55f, Jitter, 2, 3, AttackTargetRule.RandomCorner, Telegraph, true),
            new AttackDef("atk-p4-1", PhaseId.P4,  8f, Jitter, 1, 2, AttackTargetRule.RandomCorner, Telegraph, true),
            new AttackDef("atk-p4-2", PhaseId.P4, 34f, Jitter, 1, 2, AttackTargetRule.RandomCorner, Telegraph, true),
            new AttackDef("atk-p5-1", PhaseId.P5,  5f, Jitter, 2, 4, AttackTargetRule.RandomCorner, Telegraph, true),
            new AttackDef("atk-p5-2", PhaseId.P5, 24f, Jitter, 1, 4, AttackTargetRule.RandomCorner, Telegraph, true),
            new AttackDef("atk-p5-3", PhaseId.P5, 44f, Jitter, 1, 4, AttackTargetRule.RandomCorner, Telegraph, true),
            new AttackDef("atk-p5-4", PhaseId.P5, 66f, Jitter, 1, 4, AttackTargetRule.RandomCorner, Telegraph, true),
        };

        private static float PhaseDuration(PhaseDef[] phases, PhaseId id)
        {
            foreach (PhaseDef p in phases)
            {
                if (p.PhaseId == id) return p.Duration;
            }
            return -1f;
        }

        // ---- 횟수 보장 (v0.3: 3/3/3/2/4 = 15) ----

        [Test]
        public void Build_ProducesOneEntryPerTableRow()
        {
            ScheduledAttack[] schedule = AttackScheduleBuilder.Build(SpecAttacks(), SpecPhases(), 42);

            Assert.AreEqual(15, schedule.Length);
            var counts = new Dictionary<PhaseId, int>();
            foreach (ScheduledAttack a in schedule)
            {
                counts.TryGetValue(a.PhaseId, out int c);
                counts[a.PhaseId] = c + 1;
            }
            Assert.AreEqual(3, counts[PhaseId.P1]); // 명세 v0.3 공격 열 3/3/3/2/4
            Assert.AreEqual(3, counts[PhaseId.P2]);
            Assert.AreEqual(3, counts[PhaseId.P3]);
            Assert.AreEqual(2, counts[PhaseId.P4]);
            Assert.AreEqual(4, counts[PhaseId.P5]);
        }

        [Test]
        public void Build_ManySeeds_NoAttackInTrapOrSilencePhases()
        {
            // v0.3 함정 시퀀스의 "완전 무공격 정적" 보장 — 스케줄에 P6~P8 행이 없어야 한다
            PhaseDef[] phases = SpecPhases();
            AttackDef[] attacks = SpecAttacks();
            for (int seed = 0; seed < 100; seed++)
            {
                foreach (ScheduledAttack a in AttackScheduleBuilder.Build(attacks, phases, seed))
                {
                    Assert.That(a.PhaseId, Is.Not.EqualTo(PhaseId.P6), $"seed={seed}: P6은 함정 전용 — 스케줄 공격 금지");
                    Assert.That(a.PhaseId, Is.Not.EqualTo(PhaseId.P7), $"seed={seed}: P7 정적 — 공격 없음");
                    Assert.That(a.PhaseId, Is.Not.EqualTo(PhaseId.P8), $"seed={seed}: P8 탈출 — 공격 없음");
                }
            }
        }

        // ---- 시드 재현성 ----

        [Test]
        public void Build_SameSeed_IsReproducible()
        {
            ScheduledAttack[] a = AttackScheduleBuilder.Build(SpecAttacks(), SpecPhases(), 12345);
            ScheduledAttack[] b = AttackScheduleBuilder.Build(SpecAttacks(), SpecPhases(), 12345);

            Assert.AreEqual(a.Length, b.Length);
            for (int i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(a[i].Id, b[i].Id);
                Assert.AreEqual(a[i].TriggerTime, b[i].TriggerTime, "같은 시드는 같은 발동 시각이어야 한다");
                Assert.AreEqual(a[i].CornerCount, b[i].CornerCount, "같은 시드는 같은 동시 수여야 한다");
                CollectionAssert.AreEqual(a[i].Corners, b[i].Corners, "같은 시드는 같은 귀퉁이 배정이어야 한다");
            }
        }

        [Test]
        public void Build_DifferentSeed_ChangesTiming()
        {
            ScheduledAttack[] a = AttackScheduleBuilder.Build(SpecAttacks(), SpecPhases(), 1);
            ScheduledAttack[] b = AttackScheduleBuilder.Build(SpecAttacks(), SpecPhases(), 2);

            bool anyDifferent = false;
            for (int i = 0; i < a.Length; i++)
            {
                if (!UnityEngine.Mathf.Approximately(a[i].TriggerTime, b[i].TriggerTime)) { anyDifferent = true; break; }
            }
            Assert.IsTrue(anyDifferent, "다른 시드는 발동 시각이 달라야 한다 (재시작 지터 변주)");
        }

        // ---- 페이즈 경계 불침범 (전조 판정 완료까지) ----

        [Test]
        public void Build_ManySeeds_TelegraphAlwaysResolvesInsidePhase()
        {
            PhaseDef[] phases = SpecPhases();
            AttackDef[] attacks = SpecAttacks();
            for (int seed = 0; seed < 200; seed++)
            {
                ScheduledAttack[] schedule = AttackScheduleBuilder.Build(attacks, phases, seed);
                foreach (ScheduledAttack a in schedule)
                {
                    float duration = PhaseDuration(phases, a.PhaseId);
                    Assert.GreaterOrEqual(a.TriggerTime, 0f, $"seed={seed} {a.Id}: 발동 시각 음수");
                    Assert.LessOrEqual(a.TriggerTime + a.TelegraphDuration, duration,
                        $"seed={seed} {a.Id}: 전조 판정이 페이즈 경계를 넘음");
                }
            }
        }

        // ---- 지터 범위 (baseOffset × (1 ± ratio), 경계 클램프 반영) ----

        [Test]
        public void Build_ManySeeds_JitterStaysWithinRatioBounds()
        {
            PhaseDef[] phases = SpecPhases();
            AttackDef[] attacks = SpecAttacks();
            for (int seed = 0; seed < 200; seed++)
            {
                ScheduledAttack[] schedule = AttackScheduleBuilder.Build(attacks, phases, seed);
                foreach (ScheduledAttack a in schedule)
                {
                    AttackDef def = FindDef(attacks, a.Id);
                    float duration = PhaseDuration(phases, a.PhaseId);
                    float low = System.Math.Max(0f, def.BaseOffset * (1f - def.JitterRatio));
                    float high = System.Math.Min(def.BaseOffset * (1f + def.JitterRatio), duration - def.TelegraphDuration);
                    Assert.GreaterOrEqual(a.TriggerTime, low - 1e-4f, $"seed={seed} {a.Id}: 지터 하한 위반");
                    Assert.LessOrEqual(a.TriggerTime, high + 1e-4f, $"seed={seed} {a.Id}: 지터 상한 위반");
                }
            }
        }

        // ---- v0.3 N동시 랜덤: 동시 수 범위 + 서로 다른 귀퉁이 ----

        [Test]
        public void Build_ManySeeds_CornerCountStaysWithinMinMax()
        {
            PhaseDef[] phases = SpecPhases();
            AttackDef[] attacks = SpecAttacks();
            for (int seed = 0; seed < 200; seed++)
            {
                ScheduledAttack[] schedule = AttackScheduleBuilder.Build(attacks, phases, seed);
                foreach (ScheduledAttack a in schedule)
                {
                    AttackDef def = FindDef(attacks, a.Id);
                    Assert.That(a.CornerCount, Is.InRange(def.MinCorners, def.MaxCorners),
                        $"seed={seed} {a.Id}: 동시 수가 min~max 밖");
                }
            }
        }

        [Test]
        public void Build_ManySeeds_RandomCornersAreDistinctAndValid()
        {
            PhaseDef[] phases = SpecPhases();
            AttackDef[] attacks = SpecAttacks();
            var seen = new HashSet<int>();
            for (int seed = 0; seed < 200; seed++)
            {
                ScheduledAttack[] schedule = AttackScheduleBuilder.Build(attacks, phases, seed);
                foreach (ScheduledAttack a in schedule)
                {
                    if (a.TargetRule != AttackTargetRule.RandomCorner) continue;
                    Assert.AreEqual(a.CornerCount, a.Corners.Length, $"seed={seed} {a.Id}: 배정 수 불일치");
                    seen.Clear();
                    foreach (int corner in a.Corners)
                    {
                        Assert.That(corner, Is.InRange(0, CornerIndex.Count - 1), $"seed={seed} {a.Id}");
                        Assert.IsTrue(seen.Add(corner), $"seed={seed} {a.Id}: 같은 귀퉁이 중복 배정");
                    }
                }
            }
        }

        [Test]
        public void Build_RandomCount_ActuallyVariesAcrossSeeds()
        {
            // "N동시 랜덤"이 상수로 퇴화하지 않았는지 — P5(1~4) 행에서 서로 다른 동시 수가 나와야 한다
            var counts = new HashSet<int>();
            for (int seed = 0; seed < 50; seed++)
            {
                foreach (ScheduledAttack a in AttackScheduleBuilder.Build(SpecAttacks(), SpecPhases(), seed))
                {
                    if (a.PhaseId == PhaseId.P5) counts.Add(a.CornerCount);
                }
            }
            Assert.Greater(counts.Count, 1, "P5 1~4동시 랜덤이 항상 같은 수만 뽑았다");
        }

        // ---- targetRule: FarthestFromPlayer는 발동 시점 해석 (합성 테이블 — v0.3 실테이블엔 없지만 코드 경로 보존) ----

        [Test]
        public void Build_FarthestRule_DefersCornerToRuntime()
        {
            var attacks = new[]
            {
                new AttackDef("far-1", PhaseId.P5, 10f, Jitter, 1, 2, AttackTargetRule.FarthestFromPlayer, Telegraph, true),
            };
            ScheduledAttack[] schedule = AttackScheduleBuilder.Build(attacks, SpecPhases(), 7);
            Assert.AreEqual(1, schedule.Length);
            Assert.AreEqual(0, schedule[0].Corners.Length, "최원거리 규칙은 발동 시점 해석 — 빌드 시 미확정");
            Assert.That(schedule[0].CornerCount, Is.InRange(1, 2), "동시 수는 빌드 시 확정");
        }

        // ---- 정렬 ----

        [Test]
        public void Build_IsSortedByPhaseThenTriggerTime()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                ScheduledAttack[] schedule = AttackScheduleBuilder.Build(SpecAttacks(), SpecPhases(), seed);
                for (int i = 1; i < schedule.Length; i++)
                {
                    bool ordered = schedule[i - 1].PhaseIndex < schedule[i].PhaseIndex
                                   || (schedule[i - 1].PhaseIndex == schedule[i].PhaseIndex
                                       && schedule[i - 1].TriggerTime <= schedule[i].TriggerTime);
                    Assert.IsTrue(ordered, $"seed={seed}: 스케줄이 페이즈→시각 순으로 정렬돼야 한다");
                }
            }
        }

        private static AttackDef FindDef(AttackDef[] attacks, string id)
        {
            foreach (AttackDef def in attacks)
            {
                if (def.Id == id) return def;
            }
            Assert.Fail($"AttackDef 없음: {id}");
            return null;
        }
    }
}
