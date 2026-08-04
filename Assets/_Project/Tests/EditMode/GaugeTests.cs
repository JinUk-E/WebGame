using System.Collections.Generic;
using System.Reflection;
using Morae.Game.Data;
using Morae.Game.Gauges;
using Morae.Game.Player;
using NUnit.Framework;
using UnityEngine;

namespace Morae.Game.Tests.EditMode
{
    /// <summary>
    /// 게이지 3종(SaltCorners·Sanity·Talisman)의 이산 로직 — 붕괴/공황의 부적 1회 가로채기와 2회째 통과,
    /// 요의 회복 무효, 단계 클램프, 최원거리 귀퉁이 선택.
    /// MonoBehaviour를 EditMode에서 생성 (Awake·Update 미실행 — 이산 메서드만 검증). 배선은 리플렉션.
    /// </summary>
    public sealed class GaugeTests
    {
        private readonly List<Object> _cleanup = new List<Object>();
        private readonly List<(int corner, int stage)> _cornerEvents = new List<(int, int)>();
        private readonly List<GameOverReason> _gameOvers = new List<GameOverReason>();
        private int _talismanBurnedCount;

        private BalanceConfig _config;
        private SaltCorners _salt;
        private Sanity _sanity;
        private Talisman _talisman;

        [SetUp]
        public void SetUp()
        {
            _cornerEvents.Clear();
            _gameOvers.Clear();
            _talismanBurnedCount = 0;
            Morae.Game.Core.GameEvents.CornerStageChanged += OnCornerStageChanged;
            Morae.Game.Core.GameEvents.GameOver += OnGameOver;
            Morae.Game.Core.GameEvents.TalismanBurned += OnTalismanBurned;

            _config = ScriptableObject.CreateInstance<BalanceConfig>(); // 필드 초기값 = 명세값
            _cleanup.Add(_config);

            var systemsGo = new GameObject("TestSystems");
            _cleanup.Add(systemsGo);
            _salt = systemsGo.AddComponent<SaltCorners>();
            _sanity = systemsGo.AddComponent<Sanity>();
            _talisman = systemsGo.AddComponent<Talisman>();

            var playerGo = new GameObject("TestPlayer");
            _cleanup.Add(playerGo);
            var player = playerGo.AddComponent<PlayerController>();

            Wire(_salt, "talisman", _talisman);
            Wire(_sanity, "config", _config);
            Wire(_sanity, "player", player);
            Wire(_sanity, "talisman", _talisman);
            Wire(_talisman, "config", _config);
            Wire(_talisman, "salt", _salt);
            Wire(_talisman, "sanity", _sanity);
        }

        [TearDown]
        public void TearDown()
        {
            Morae.Game.Core.GameEvents.CornerStageChanged -= OnCornerStageChanged;
            Morae.Game.Core.GameEvents.GameOver -= OnGameOver;
            Morae.Game.Core.GameEvents.TalismanBurned -= OnTalismanBurned;
            foreach (Object o in _cleanup)
            {
                if (o != null) Object.DestroyImmediate(o);
            }
            _cleanup.Clear();
        }

        private void OnCornerStageChanged(int corner, int stage) => _cornerEvents.Add((corner, stage));
        private void OnGameOver(GameOverReason reason) => _gameOvers.Add(reason);
        private void OnTalismanBurned() => _talismanBurnedCount++;

        private static void Wire(object target, string field, object value)
        {
            FieldInfo info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"{target.GetType().Name}.{field} 필드 없음 — 배선 계약 파괴");
            info.SetValue(target, value);
        }

        // ---- 소금: 단계 클램프 ----

        [Test]
        public void Salt_StageClampsBetweenWhiteAndBlack()
        {
            _salt.Purify(0); // 백에서 정화 — 변화 없음
            Assert.AreEqual(0, _salt.GetStage(0));
            Assert.AreEqual(0, _cornerEvents.Count);

            _salt.Contaminate(0);
            _salt.Contaminate(0);
            Assert.AreEqual((int)CornerStage.Black, _salt.GetStage(0));

            _salt.Contaminate(0); // 흑에서 오염 — 단계 유지 (일부만 흑 — 붕괴 아님)
            Assert.AreEqual((int)CornerStage.Black, _salt.GetStage(0));
            Assert.AreEqual(2, _cornerEvents.Count, "변화가 없으면 이벤트도 없어야 한다");
            Assert.IsEmpty(_gameOvers);
        }

        // ---- 소금: 붕괴 — 부적 1회 가로채기, 2회째 게임오버 ----

        [Test]
        public void Salt_CollapseInterceptedOnceByTalisman_ThenGameOver()
        {
            // 전 귀퉁이 흑 직전까지
            for (int corner = 0; corner < CornerIndex.Count; corner++)
            {
                _salt.Contaminate(corner);
                if (corner != 3) _salt.Contaminate(corner);
            }
            Assert.IsEmpty(_gameOvers);
            Assert.AreEqual(0, _talismanBurnedCount);

            _salt.Contaminate(3); // 전 귀퉁이 흑 → 부적 가로채기 (전 귀퉁이 −1)
            Assert.AreEqual(1, _talismanBurnedCount);
            Assert.IsTrue(_talisman.Consumed);
            Assert.IsEmpty(_gameOvers, "부적이 가로챘으면 게임오버 없음");
            Assert.IsFalse(_salt.IsCollapsed);
            for (int corner = 0; corner < CornerIndex.Count; corner++)
            {
                Assert.AreEqual((int)CornerStage.Gray, _salt.GetStage(corner), $"부적 복구 후 귀퉁이 {corner}는 회색");
            }

            // 다시 전 귀퉁이 흑 → 부적 소모됨 → 붕괴 게임오버
            for (int corner = 0; corner < CornerIndex.Count; corner++) _salt.Contaminate(corner);
            Assert.AreEqual(1, _gameOvers.Count);
            Assert.AreEqual(GameOverReason.SealCollapsed, _gameOvers[0]);
            Assert.IsTrue(_salt.IsCollapsed);
            Assert.AreEqual(1, _talismanBurnedCount, "부적은 1회만");
        }

        // ---- 이성: 공황 — 부적 1회 가로채기(+30), 2회째 게임오버 ----

        [Test]
        public void Sanity_PanicInterceptedOnceByTalisman_ThenGameOver()
        {
            _sanity.Begin();
            Assert.AreEqual(_config.SanityMax, _sanity.Value);

            _sanity.ApplyDelta(-_config.SanityMax - 50f); // 0 도달 → 부적 +30
            Assert.AreEqual(1, _talismanBurnedCount);
            Assert.IsEmpty(_gameOvers);
            Assert.AreEqual(_config.TalismanSanityRestore, _sanity.Value, 0.001f);
            Assert.IsTrue(_sanity.IsRunning);

            _sanity.ApplyDelta(-100f); // 다시 0 → 부적 소모됨 → 공황
            Assert.AreEqual(1, _gameOvers.Count);
            Assert.AreEqual(GameOverReason.Panic, _gameOvers[0]);
            Assert.IsFalse(_sanity.IsRunning);
        }

        // ---- 이성: 요의 동안 회복 무효 (하락은 유효) ----

        [Test]
        public void Sanity_UrgeBlocksRecoveryButNotDrain()
        {
            _sanity.Begin();
            _sanity.ApplyDelta(-20f);
            Assert.AreEqual(80f, _sanity.Value, 0.001f);

            _sanity.SetUrgeActive(true);
            _sanity.ApplyDelta(10f); // 회복 무효
            Assert.AreEqual(80f, _sanity.Value, 0.001f);
            _sanity.ApplyDelta(-10f); // 하락은 유효
            Assert.AreEqual(70f, _sanity.Value, 0.001f);

            _sanity.SetUrgeActive(false);
            _sanity.ApplyDelta(10f);
            Assert.AreEqual(80f, _sanity.Value, 0.001f);
        }

        // ---- 최원거리 귀퉁이 선택 (P5 FarthestFromPlayer) ----

        [Test]
        public void Salt_SelectFarthestCorners_PicksDiagonalFirst()
        {
            // 씬 배치와 동일한 4귀퉁이 좌표 (0=좌상 1=우상 2=좌하 3=우하)
            Vector2[] positions = { new Vector2(-6f, 3.5f), new Vector2(6f, 3.5f), new Vector2(-6f, -3.5f), new Vector2(6f, -3.5f) };
            var transforms = new Transform[CornerIndex.Count];
            for (int i = 0; i < positions.Length; i++)
            {
                var go = new GameObject($"corner{i}");
                _cleanup.Add(go);
                go.transform.position = positions[i];
                transforms[i] = go.transform;
            }
            Wire(_salt, "cornerTransforms", transforms);

            // 좌상 귀퉁이에서: 최원거리 = 우하(3), 2순위 = 우상(1) — 단일이면 B 없음
            _salt.SelectFarthestCorners(positions[0], dual: false, out int a, out int b);
            Assert.AreEqual(CornerIndex.BottomRight, a);
            Assert.AreEqual(CornerIndex.None, b);

            _salt.SelectFarthestCorners(positions[0], dual: true, out a, out b);
            Assert.AreEqual(CornerIndex.BottomRight, a);
            Assert.AreEqual(CornerIndex.TopRight, b);
        }
    }
}
