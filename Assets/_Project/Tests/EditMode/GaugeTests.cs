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
            // private 필드는 선언 타입에서만 보인다 — 기반 클래스(Interactable.config 등)까지 거슬러 탐색
            FieldInfo info = null;
            for (var type = target.GetType(); type != null && info == null; type = type.BaseType)
            {
                info = type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            }
            Assert.IsNotNull(info, $"{target.GetType().Name}.{field} 필드 없음 — 배선 계약 파괴");
            info.SetValue(target, value);
        }

        // ---- 소금: 단계 클램프 + v0.3 흑화 심화 스택 ----

        [Test]
        public void Salt_StageClampsBetweenWhiteAndBlack()
        {
            _salt.Purify(0); // 백에서 정화 — 변화 없음
            Assert.AreEqual(0, _salt.GetStage(0));
            Assert.AreEqual(0, _cornerEvents.Count);

            _salt.Contaminate(0);
            _salt.Contaminate(0);
            Assert.AreEqual((int)CornerStage.Black, _salt.GetStage(0));

            _salt.Contaminate(0); // v0.3: 흑에서 추가 피격 = 심화 플래그 (단계는 유지 — 일부만 흑, 붕괴 아님)
            Assert.AreEqual((int)CornerStage.Black, _salt.GetStage(0));
            Assert.IsTrue(_salt.IsDeepened(0));
            Assert.IsEmpty(_gameOvers);
        }

        [Test]
        public void Salt_DeepenOnBlackHit_RaisesDeepBlackEvent_NoStacking()
        {
            _salt.Contaminate(1);
            _salt.Contaminate(1); // 흑
            Assert.IsFalse(_salt.IsDeepened(1));
            Assert.IsFalse(_salt.IsSaturated(1), "흑 미심화는 아직 유효 타깃 (피격 = 심화)");

            _cornerEvents.Clear();
            _salt.Contaminate(1); // 심화
            Assert.IsTrue(_salt.IsDeepened(1));
            Assert.IsTrue(_salt.IsSaturated(1), "흑+심화 = 포화 — 공격 대상 제외");
            Assert.AreEqual(1, _cornerEvents.Count);
            Assert.AreEqual((1, (int)CornerStage.DeepBlack), _cornerEvents[0], "심화는 stage=3으로 발행 (표현 계층 구분)");

            _cornerEvents.Clear();
            _salt.Contaminate(1); // 무중첩 — 재피격은 무효
            Assert.AreEqual(0, _cornerEvents.Count, "심화는 1회 플래그 — 중첩·재발행 없음");
            Assert.AreEqual((int)CornerStage.Black, _salt.GetStage(1), "내부 단계는 여전히 흑(2)");
        }

        [Test]
        public void Salt_PurifyFromBlack_ClearsDeepenedFlag()
        {
            _salt.Contaminate(2);
            _salt.Contaminate(2);
            _salt.Contaminate(2); // 흑+심화
            Assert.IsTrue(_salt.IsDeepened(2));

            _salt.Purify(2); // 흑→회 — 심화 해제 (v0.3)
            Assert.AreEqual((int)CornerStage.Gray, _salt.GetStage(2));
            Assert.IsFalse(_salt.IsDeepened(2));

            // 다시 흑까지 오염 — 심화는 새로 쌓아야 한다 (해제가 영구 면역이 아님)
            _salt.Contaminate(2);
            Assert.IsFalse(_salt.IsDeepened(2));
            _salt.Contaminate(2);
            Assert.IsTrue(_salt.IsDeepened(2));
        }

        [Test]
        public void Salt_TalismanRestore_AlsoClearsDeepenedFlag()
        {
            // 귀퉁이 0을 흑+심화로 만든 뒤 전 귀퉁이 흑 → 부적 가로채기(전 귀퉁이 −1 = 흑→회)
            _salt.Contaminate(0);
            _salt.Contaminate(0);
            _salt.Contaminate(0); // 심화
            Assert.IsTrue(_salt.IsDeepened(0));
            for (int corner = 1; corner < CornerIndex.Count; corner++)
            {
                _salt.Contaminate(corner);
                _salt.Contaminate(corner);
            }

            Assert.AreEqual(1, _talismanBurnedCount, "전 귀퉁이 흑 — 부적 1회 가로채기");
            Assert.IsFalse(_salt.IsCollapsed);
            Assert.AreEqual((int)CornerStage.Gray, _salt.GetStage(0));
            Assert.IsFalse(_salt.IsDeepened(0), "부적 복구(흑→회)도 심화 해제");
        }

        [Test]
        public void Salt_CollapseJudgment_UnchangedByDeepening()
        {
            // 붕괴 판정은 4귀퉁이 흑(2) 그대로 — 심화는 복구 난이도만 올린다 (v0.3)
            _salt.Contaminate(0);
            _salt.Contaminate(0);
            _salt.Contaminate(0); // 흑+심화 — 이것만으로 붕괴 아님
            Assert.IsEmpty(_gameOvers);
            Assert.AreEqual(0, _talismanBurnedCount, "심화는 붕괴 트리거가 아니다");
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

        [Test]
        public void Salt_SelectFarthestCorners_ExcludesSaturatedCorners()
        {
            // v0.3: 흑 미심화는 여전히 유효 타깃(피격 = 심화) — 제외는 흑+심화(포화)만
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

            // 우하(3) 흑 (미심화) — 여전히 후보: 좌상 기준 최원거리 유지
            _salt.Contaminate(CornerIndex.BottomRight);
            _salt.Contaminate(CornerIndex.BottomRight);
            Assert.IsFalse(_salt.IsSaturated(CornerIndex.BottomRight));

            _salt.SelectFarthestCorners(positions[0], dual: true, out int a, out int b);
            Assert.AreEqual(CornerIndex.BottomRight, a, "흑 미심화는 아직 유효 타깃");
            Assert.AreEqual(CornerIndex.TopRight, b);

            // 우하(3) 심화 → 포화 — 이제 제외
            _salt.Contaminate(CornerIndex.BottomRight);
            Assert.IsTrue(_salt.IsSaturated(CornerIndex.BottomRight));

            _salt.SelectFarthestCorners(positions[0], dual: true, out a, out b);
            Assert.AreEqual(CornerIndex.TopRight, a);    // 비포화 중 최원거리 (가로 12 > 세로 7)
            Assert.AreEqual(CornerIndex.BottomLeft, b);

            // 좌상(0)만 남기고 전부 포화 — 후보 1곳뿐이면 A만, dual이어도 B는 None
            _salt.Contaminate(CornerIndex.TopRight);
            _salt.Contaminate(CornerIndex.TopRight);
            _salt.Contaminate(CornerIndex.TopRight);
            _salt.Contaminate(CornerIndex.BottomLeft);
            _salt.Contaminate(CornerIndex.BottomLeft);
            _salt.Contaminate(CornerIndex.BottomLeft);

            _salt.SelectFarthestCorners(positions[0], dual: true, out a, out b);
            Assert.AreEqual(CornerIndex.TopLeft, a);
            Assert.AreEqual(CornerIndex.None, b);
        }

        // ---- v0.3 심화 귀퉁이 기도 채널 연장 (3s → ×1.5 = 4.5s) ----

        [Test]
        public void Prayer_DeepenedCorner_ExtendsChannelDuration()
        {
            var prayerGo = new GameObject("TestPrayer");
            _cleanup.Add(prayerGo);
            var prayer = prayerGo.AddComponent<Morae.Game.Interactions.PrayerInteractable>();
            Wire(prayer, "config", _config); // 기반 클래스(Interactable) private 필드 — 계층 탐색 Wire
            Wire(prayer, "salt", _salt);

            // 조준 없음 → 기본 3s
            Assert.AreEqual(_config.PrayerChannelSec, prayer.Duration, 1e-4f);

            // 귀퉁이 0 흑+심화 후 조준 → ×1.5
            _salt.Contaminate(0);
            _salt.Contaminate(0);
            _salt.Contaminate(0);
            Assert.IsTrue(_salt.IsDeepened(0));
            Wire(prayer, "_aimedCorner", 0);
            Assert.AreEqual(_config.PrayerChannelSec * _config.PrayerDeepenedMultiplier, prayer.Duration, 1e-4f,
                "심화 귀퉁이 조준 시 채널 4.5s");

            // 비심화 귀퉁이 조준 → 기본 3s
            Wire(prayer, "_aimedCorner", 1);
            Assert.AreEqual(_config.PrayerChannelSec, prayer.Duration, 1e-4f);

            // 흑→회 정화(심화 해제) 후 다시 조준 → 기본 3s
            _salt.Purify(0);
            Wire(prayer, "_aimedCorner", 0);
            Assert.AreEqual(_config.PrayerChannelSec, prayer.Duration, 1e-4f, "정화로 심화 해제 시 채널 원복");
        }
    }
}
