using Morae.Game.Player;
using NUnit.Framework;
using UnityEngine;

namespace Morae.Game.Tests
{
    /// <summary>
    /// 모바일 온스크린 컨트롤의 순수 로직 회귀 테스트 (2026-08-05).
    /// 스틱 스냅은 "키보드와 같은 값"이 계약이고, 버튼 래치는 "엣지 유실·중복 없음"이 계약이다.
    /// </summary>
    public class TouchInputTests
    {
        private const float R = 100f;      // 스틱 반경
        private const float Dead = 0.25f;  // 데드존 비율
        private const float Diag = 0.70710678f;
        private const float Eps = 1e-4f;

        private static Vector2 Resolve(Vector2 delta, TouchStickModel.SnapMode mode
            = TouchStickModel.SnapMode.EightWay)
            => TouchStickModel.Resolve(delta, R, Dead, mode);

        // ---------- 스틱: 데드존 ----------

        [Test]
        public void Stick_InsideDeadZone_ReturnsZero()
        {
            Assert.AreEqual(Vector2.zero, Resolve(Vector2.zero));
            Assert.AreEqual(Vector2.zero, Resolve(new Vector2(20f, 0f)));   // 반경의 20% < 25%
            Assert.AreEqual(Vector2.zero, Resolve(new Vector2(15f, 15f)));  // 크기 21.2 < 25
        }

        [Test]
        public void Stick_JustOutsideDeadZone_Responds()
        {
            Vector2 v = Resolve(new Vector2(30f, 0f));
            Assert.AreEqual(1f, v.x, Eps);
            Assert.AreEqual(0f, v.y, Eps);
        }

        // ---------- 스틱: 8방향 스냅 = 키보드와 동일한 값 ----------

        [Test]
        public void Stick_CardinalDirections_MatchKeyboardValues()
        {
            AssertVec(new Vector2(1f, 0f), Resolve(new Vector2(90f, 5f)));    // D
            AssertVec(new Vector2(-1f, 0f), Resolve(new Vector2(-90f, -5f))); // A
            AssertVec(new Vector2(0f, 1f), Resolve(new Vector2(3f, 80f)));    // W
            AssertVec(new Vector2(0f, -1f), Resolve(new Vector2(-3f, -80f))); // S
        }

        [Test]
        public void Stick_Diagonals_AreNormalized_SameAsKeyboard()
        {
            // 키보드 W+D = (1,1).normalized — 대각 등속 규칙이 터치에서도 동일해야 한다
            AssertVec(new Vector2(Diag, Diag), Resolve(new Vector2(70f, 60f)));
            AssertVec(new Vector2(-Diag, Diag), Resolve(new Vector2(-60f, 70f)));
            AssertVec(new Vector2(-Diag, -Diag), Resolve(new Vector2(-70f, -70f)));
            AssertVec(new Vector2(Diag, -Diag), Resolve(new Vector2(65f, -70f)));
        }

        [Test]
        public void Stick_AllOutputs_AreUnitLength()
        {
            for (int deg = 0; deg < 360; deg += 7)
            {
                float rad = deg * Mathf.Deg2Rad;
                var delta = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * 90f;
                Vector2 v = Resolve(delta);
                Assert.AreEqual(1f, v.magnitude, 1e-3f, $"각도 {deg}에서 크기가 1이 아니다 — 이동 속도가 달라진다");
            }
        }

        [Test]
        public void Stick_DoorPushDirection_PassesDotThreshold()
        {
            // DoorInteractable: Dot(MoveAxis, pushDir) > 0.5 — 좌측 문 기준
            var push = new Vector2(-1f, 0f);
            Assert.Greater(Vector2.Dot(Resolve(new Vector2(-90f, 0f)), push), 0.5f);
            Assert.Greater(Vector2.Dot(Resolve(new Vector2(-80f, -70f)), push), 0.5f); // 좌하 대각도 인정
            Assert.Less(Vector2.Dot(Resolve(new Vector2(0f, 90f)), push), 0.5f);       // 위쪽은 불인정
        }

        // ---------- 스틱: 기도 조준(Corners) ----------

        [Test]
        public void Stick_CornerMode_AlwaysMapsToDiagonal()
        {
            for (int deg = 0; deg < 360; deg += 5)
            {
                float rad = deg * Mathf.Deg2Rad;
                var delta = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * 90f;
                Vector2 v = Resolve(delta, TouchStickModel.SnapMode.Corners);
                // PrayerInteractable의 귀퉁이 매핑 조건: |x|>0.1 && |y|>0.1
                Assert.Greater(Mathf.Abs(v.x), 0.1f, $"각도 {deg}: x축 성분 부족 — 귀퉁이 매핑 실패");
                Assert.Greater(Mathf.Abs(v.y), 0.1f, $"각도 {deg}: y축 성분 부족 — 귀퉁이 매핑 실패");
            }
        }

        [Test]
        public void Stick_CornerMode_PicksNearestCorner()
        {
            // 정확히 위로 밀어도 조준이 풀리지 않고 가장 가까운 귀퉁이로 (엄지 정밀도 보정)
            AssertVec(new Vector2(Diag, Diag), Resolve(new Vector2(50f, 80f), TouchStickModel.SnapMode.Corners));
            AssertVec(new Vector2(-Diag, Diag), Resolve(new Vector2(-50f, 80f), TouchStickModel.SnapMode.Corners));
            AssertVec(new Vector2(-Diag, -Diag), Resolve(new Vector2(-90f, -10f), TouchStickModel.SnapMode.Corners));
            AssertVec(new Vector2(Diag, -Diag), Resolve(new Vector2(90f, -10f), TouchStickModel.SnapMode.Corners));
        }

        [Test]
        public void Stick_CornerMode_RespectsDeadZone()
        {
            Assert.AreEqual(Vector2.zero, Resolve(new Vector2(10f, 10f), TouchStickModel.SnapMode.Corners));
        }

        // ---------- 노브 표시 ----------

        [Test]
        public void Knob_ClampsToMaxOffset()
        {
            Assert.AreEqual(30f, TouchStickModel.ClampKnob(new Vector2(30f, 0f), 78f).x, Eps);
            Assert.AreEqual(78f, TouchStickModel.ClampKnob(new Vector2(300f, 0f), 78f).magnitude, 1e-3f);
        }

        // ---------- 버튼 엣지 래치 ----------

        [Test]
        public void Latch_PressAndRelease_ProduceExactlyOneEdgeEach()
        {
            var latch = new TouchButtonLatch();
            Assert.IsFalse(latch.Down(1));
            Assert.IsFalse(latch.Held);

            latch.Set(true);
            Assert.IsTrue(latch.Held, "누른 즉시 Held는 참이어야 홀드 문법이 성립한다");
            Assert.IsTrue(latch.Down(2));
            Assert.IsTrue(latch.Down(2), "같은 프레임 안에서는 모든 소비자가 같은 값을 봐야 한다");
            Assert.IsFalse(latch.Down(3), "다음 프레임에 Down이 재발화하면 상호작용이 중복 시작된다");

            latch.Set(false);
            Assert.IsFalse(latch.Held);
            Assert.IsTrue(latch.Up(4));
            Assert.IsFalse(latch.Up(5));
        }

        [Test]
        public void Latch_PressAfterFrameSynced_IsNotLost()
        {
            // 포인터 이벤트가 그 프레임의 첫 읽기보다 늦게 도착한 경우 — 다음 프레임에 정확히 1회 소비
            var latch = new TouchButtonLatch();
            Assert.IsFalse(latch.Down(10));  // 프레임 10 동기화 완료
            latch.Set(true);                 // 늦게 도착
            Assert.IsFalse(latch.Down(10), "이미 동기화된 프레임의 값이 도중에 바뀌면 안 된다");
            Assert.IsTrue(latch.Down(11));
            Assert.IsFalse(latch.Down(12));
        }

        [Test]
        public void Latch_TapWithinOneFrameGap_KeepsBothEdges()
        {
            var latch = new TouchButtonLatch();
            latch.Set(true);
            latch.Set(false); // 같은 프레임 간극에서 누르고 뗌
            Assert.IsTrue(latch.Down(1));
            Assert.IsTrue(latch.Up(1), "탭이 통째로 유실되면 안 된다");
            Assert.IsFalse(latch.Down(2));
        }

        [Test]
        public void Latch_Reset_ClearsEverything()
        {
            var latch = new TouchButtonLatch();
            latch.Set(true);
            latch.Reset();
            Assert.IsFalse(latch.Held);
            Assert.IsFalse(latch.Down(1), "씬 리로드 후 눌림이 살아남으면 재시작 직후 오작동한다");
        }

        private static void AssertVec(Vector2 expected, Vector2 actual)
        {
            Assert.AreEqual(expected.x, actual.x, Eps, $"x: 기대 {expected} 실제 {actual}");
            Assert.AreEqual(expected.y, actual.y, Eps, $"y: 기대 {expected} 실제 {actual}");
        }
    }
}
