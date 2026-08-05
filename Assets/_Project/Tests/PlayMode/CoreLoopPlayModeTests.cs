using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Morae.Game.Core;
using Morae.Game.Data;
using Morae.Game.Gauges;
using Morae.Game.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Morae.Game.Tests.PlayMode
{
    /// <summary>
    /// 코어 루프 배속 자동 검증 (D2 보조) — 미니 타임라인(페이즈 2s×2, 공격 1건·지터 0)을 timeScale 10으로 완주:
    /// 공격 발동 → 전조 → 미상쇄 오염 → 이성 −8 → 페이즈 전이가 실제 플레이 루프(Update)에서 이벤트로 흐르는지 확인.
    /// 씬·입력 없이 리플렉션 배선한 최소 리그 사용 (본편 420초 씬 완주는 수동 — DebugHud·F1 배속).
    /// </summary>
    public sealed class CoreLoopPlayModeTests
    {
        private readonly List<Object> _cleanup = new List<Object>();
        private readonly List<int> _telegraphCorners = new List<int>();
        private readonly List<(int corner, bool countered)> _resolved = new List<(int, bool)>();
        private readonly List<(int corner, int stage)> _cornerStages = new List<(int, int)>();
        private readonly List<PhaseId> _phaseChanges = new List<PhaseId>();

        [SetUp]
        public void SetUp()
        {
            _telegraphCorners.Clear();
            _resolved.Clear();
            _cornerStages.Clear();
            _phaseChanges.Clear();
            GameEvents.AttackTelegraphStarted += OnTelegraph;
            GameEvents.AttackResolved += OnResolved;
            GameEvents.CornerStageChanged += OnCornerStage;
            GameEvents.PhaseChanged += OnPhaseChanged;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            GameEvents.AttackTelegraphStarted -= OnTelegraph;
            GameEvents.AttackResolved -= OnResolved;
            GameEvents.CornerStageChanged -= OnCornerStage;
            GameEvents.PhaseChanged -= OnPhaseChanged;
            foreach (Object o in _cleanup)
            {
                if (o != null) Object.Destroy(o);
            }
            _cleanup.Clear();
        }

        private void OnTelegraph(int corner, float duration) => _telegraphCorners.Add(corner);
        private void OnResolved(int corner, bool countered) => _resolved.Add((corner, countered));
        private void OnCornerStage(int corner, int stage) => _cornerStages.Add((corner, stage));
        private void OnPhaseChanged(PhaseId phase) => _phaseChanges.Add(phase);

        private static void Wire(object target, string field, object value)
        {
            FieldInfo info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"{target.GetType().Name}.{field} 필드 없음 — 배선 계약 파괴");
            info.SetValue(target, value);
        }

        [UnityTest]
        public IEnumerator Scheduler_MiniTimeline_TelegraphResolvesAndContaminates()
        {
#if UNITY_EDITOR
            // ---- 미니 테이블 (에디터 실행 전용 — EditorSet*는 UNITY_EDITOR 한정) ----
            var phaseTable = ScriptableObject.CreateInstance<PhaseTable>();
            phaseTable.EditorSetPhases(new[]
            {
                new PhaseDef(PhaseId.P1, 2f, 60, 62, ClockMode.Sync, 0, 0f, 0f, 0f),
                new PhaseDef(PhaseId.P2, 2f, 62, 64, ClockMode.Sync, 0, 0f, 0f, 0f),
            });
            var attackTable = ScriptableObject.CreateInstance<AttackTable>();
            attackTable.EditorSetAttacks(new[]
            {
                // 지터 0 — 발동 0.5s 확정, 전조 0.4s → P1 안에서 판정 완료 (v0.3 스키마: 단일 = min 1, max 1)
                new AttackDef("mini-1", PhaseId.P1, 0.5f, 0f, 1, 1, AttackTargetRule.RandomCorner, 0.4f, true),
            });
            var config = ScriptableObject.CreateInstance<BalanceConfig>();
            _cleanup.Add(phaseTable);
            _cleanup.Add(attackTable);
            _cleanup.Add(config);

            // ---- 리그 조립 (비활성 상태에서 배선 → 활성화로 Awake 지연) ----
            var systemsGo = new GameObject("MiniSystems");
            systemsGo.SetActive(false);
            _cleanup.Add(systemsGo);
            var sequencer = systemsGo.AddComponent<PhaseSequencer>();
            var scheduler = systemsGo.AddComponent<AttackScheduler>();
            var salt = systemsGo.AddComponent<SaltCorners>();
            var sanity = systemsGo.AddComponent<Sanity>();
            var talisman = systemsGo.AddComponent<Talisman>();

            var playerGo = new GameObject("MiniPlayer");
            playerGo.SetActive(false);
            _cleanup.Add(playerGo);
            var player = playerGo.AddComponent<PlayerController>();

            Wire(sequencer, "phaseTable", phaseTable);
            Wire(scheduler, "attackTable", attackTable);
            Wire(scheduler, "phaseTable", phaseTable);
            Wire(scheduler, "config", config);
            Wire(scheduler, "sequencer", sequencer);
            Wire(scheduler, "salt", salt);
            Wire(scheduler, "sanity", sanity);
            Wire(scheduler, "player", player);
            Wire(salt, "talisman", talisman);
            Wire(sanity, "config", config);
            Wire(sanity, "sequencer", sequencer);
            Wire(sanity, "player", player);
            Wire(sanity, "talisman", talisman);
            Wire(talisman, "config", config);
            Wire(talisman, "salt", salt);
            Wire(talisman, "sanity", sanity);
            Wire(player, "config", config);
            playerGo.SetActive(true);
            systemsGo.SetActive(true);

            // ---- 배속 실행 ----
            sequencer.Begin();
            scheduler.Begin(seed: 123);
            sanity.Begin();
            Time.timeScale = 10f;

            float deadline = Time.realtimeSinceStartup + 10f;
            while (sequencer.CurrentPhase != PhaseId.P2 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Time.timeScale = 1f;

            // ---- 판정 ----
            Assert.AreEqual(PhaseId.P2, sequencer.CurrentPhase, "미니 타임라인이 P2로 전이해야 한다 (배속 완주)");
            CollectionAssert.AreEqual(new[] { PhaseId.P1, PhaseId.P2 }, _phaseChanges, "페이즈 전이 이벤트 순서");

            Assert.AreEqual(1, _telegraphCorners.Count, "공격 1건 = 전조 1회 (횟수 보장)");
            int corner = _telegraphCorners[0];
            Assert.AreEqual(1, _resolved.Count, "전조 1회 = 판정 1회");
            Assert.AreEqual(corner, _resolved[0].corner);
            Assert.IsFalse(_resolved[0].countered, "기도 없음 — 미상쇄");
            Assert.AreEqual(1, _cornerStages.Count);
            Assert.AreEqual((corner, 1), _cornerStages[0], "미상쇄 오염 +1");
            Assert.AreEqual(1, salt.GetStage(corner));
            Assert.AreEqual(config.SanityMax - config.SanityTelegraphHit, sanity.Value, 0.01f, "전조 이성 −8");
#else
            Assert.Ignore("에디터 PlayMode 전용 (EditorSet* 의존)");
            yield break;
#endif
        }
    }
}
