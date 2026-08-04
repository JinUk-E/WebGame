using Morae.Game.Core;
using Morae.Game.Data;
using NUnit.Framework;

namespace Morae.Game.Tests.EditMode
{
    /// <summary>
    /// AttackScheduleBuilder — 테스트 1순위 (architecture §2.2).
    /// 횟수 보장 · 페이즈 경계 불침범(전조 판정 포함) · 시드 재현성 · 지터 범위 · dual 귀퉁이 · targetRule 처리.
    /// 테이블은 명세 §1 공격 열(1/1/3/3/1 = 9행) 복제본 — DataAssetBuilder(에디터 asmdef)와 값 동일.
    /// </summary>
    public sealed class AttackScheduleBuilderTests
    {
        private const float Jitter = 0.2f;
        private const float Telegraph = 3f;

        private static PhaseDef[] SpecPhases() => new[]
        {
            new PhaseDef(PhaseId.P1,  60f,  60, 150, ClockMode.Sync,    0, 0f, 0f, 0f),
            new PhaseDef(PhaseId.P2,  75f, 150, 240, ClockMode.Frozen, -5, 0f, 0f, 0f),
            new PhaseDef(PhaseId.P3, 105f, 240, 360, ClockMode.Offset, 40, 0f, 0f, 0f),
            new PhaseDef(PhaseId.P4,  75f, 360, 410, ClockMode.Offset, -30, 0f, 0.35f, 0.5f),
            new PhaseDef(PhaseId.P5,  30f, 410, 420, ClockMode.Fixed, 445, 0.35f, 0.45f, 0.5f),
            new PhaseDef(PhaseId.P6,  45f, 420, 450, ClockMode.Fixed, 445, 0.45f, 0.85f, 0.5f),
            new PhaseDef(PhaseId.P7,  30f, 450, 460, ClockMode.Fixed, 445, 0.85f, 1f, 0.5f),
        };

        private static AttackDef[] SpecAttacks() => new[]
        {
            new AttackDef("atk-p1-1", PhaseId.P1, 30f, Jitter, false, AttackTargetRule.RandomCorner, Telegraph, true),
            new AttackDef("atk-p2-1", PhaseId.P2, 40f, Jitter, false, AttackTargetRule.RandomCorner, Telegraph, true),
            new AttackDef("atk-p3-1", PhaseId.P3, 20f, Jitter, false, AttackTargetRule.RandomCorner, Telegraph, true),
            new AttackDef("atk-p3-2", PhaseId.P3, 48f, Jitter, false, AttackTargetRule.RandomCorner, Telegraph, true),
            new AttackDef("atk-p3-3", PhaseId.P3, 78f, Jitter, true,  AttackTargetRule.RandomCorner, Telegraph, true),
            new AttackDef("atk-p4-1", PhaseId.P4, 15f, Jitter, true,  AttackTargetRule.RandomCorner, Telegraph, true),
            new AttackDef("atk-p4-2", PhaseId.P4, 35f, Jitter, false, AttackTargetRule.RandomCorner, Telegraph, true),
            new AttackDef("atk-p4-3", PhaseId.P4, 55f, Jitter, true,  AttackTargetRule.RandomCorner, Telegraph, true),
            new AttackDef("atk-p5-1", PhaseId.P5, 10f, Jitter, false, AttackTargetRule.FarthestFromPlayer, Telegraph, true),
        };

        private static float PhaseDuration(PhaseDef[] phases, PhaseId id)
        {
            foreach (PhaseDef p in phases)
            {
                if (p.PhaseId == id) return p.Duration;
            }
            return -1f;
        }

        // ---- 횟수 보장 ----

        [Test]
        public void Build_ProducesOneEntryPerTableRow()
        {
            ScheduledAttack[] schedule = AttackScheduleBuilder.Build(SpecAttacks(), SpecPhases(), 42);

            Assert.AreEqual(9, schedule.Length);
            int p1 = 0, p2 = 0, p3 = 0, p4 = 0, p5 = 0;
            foreach (ScheduledAttack a in schedule)
            {
                switch (a.PhaseId)
                {
                    case PhaseId.P1: p1++; break;
                    case PhaseId.P2: p2++; break;
                    case PhaseId.P3: p3++; break;
                    case PhaseId.P4: p4++; break;
                    case PhaseId.P5: p5++; break;
                }
            }
            Assert.AreEqual(1, p1); // 명세 §1 공격 열 1/1/3/3/1
            Assert.AreEqual(1, p2);
            Assert.AreEqual(3, p3);
            Assert.AreEqual(3, p4);
            Assert.AreEqual(1, p5);
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
                Assert.AreEqual(a[i].CornerA, b[i].CornerA);
                Assert.AreEqual(a[i].CornerB, b[i].CornerB);
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

        // ---- 귀퉁이 확정 규칙 ----

        [Test]
        public void Build_ManySeeds_DualAttackGetsTwoDistinctCorners()
        {
            PhaseDef[] phases = SpecPhases();
            AttackDef[] attacks = SpecAttacks();
            for (int seed = 0; seed < 100; seed++)
            {
                ScheduledAttack[] schedule = AttackScheduleBuilder.Build(attacks, phases, seed);
                foreach (ScheduledAttack a in schedule)
                {
                    if (a.TargetRule != AttackTargetRule.RandomCorner) continue;
                    Assert.That(a.CornerA, Is.InRange(0, CornerIndex.Count - 1), $"seed={seed} {a.Id}");
                    if (a.DualCorner)
                    {
                        Assert.That(a.CornerB, Is.InRange(0, CornerIndex.Count - 1), $"seed={seed} {a.Id}");
                        Assert.AreNotEqual(a.CornerA, a.CornerB, $"seed={seed} {a.Id}: dual은 서로 다른 두 귀퉁이");
                    }
                    else
                    {
                        Assert.AreEqual(CornerIndex.None, a.CornerB, $"seed={seed} {a.Id}: 단일 공격은 B 없음");
                    }
                }
            }
        }

        [Test]
        public void Build_FarthestRule_DefersCornerToRuntime()
        {
            ScheduledAttack[] schedule = AttackScheduleBuilder.Build(SpecAttacks(), SpecPhases(), 7);
            foreach (ScheduledAttack a in schedule)
            {
                if (a.TargetRule != AttackTargetRule.FarthestFromPlayer) continue;
                Assert.AreEqual(CornerIndex.None, a.CornerA, "최원거리 규칙은 발동 시점 해석 — 빌드 시 미확정");
                Assert.AreEqual(CornerIndex.None, a.CornerB);
            }
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
