using Morae.Game.Core;
using Morae.Game.Data;
using NUnit.Framework;
using UnityEngine;

namespace Morae.Game.Tests
{
    /// <summary>
    /// 프롤로그 대사 수동 진행 게이트 회귀 테스트 (2026-08-06).
    /// 계약 3가지: ① 시간으로는 절대 안 넘어간다 ② 최소 표시 시간 전의 입력은 무시된다(연타로 대사가
    /// 통째로 날아가지 않는다) ③ 구간이 끝나면 어떤 입력도 통과하지 않는다(학습·본편으로 새지 않는다).
    /// </summary>
    public class DialogueAdvanceTests
    {
        private const float Min = 0.3f;   // BalanceConfig.PrologueLineMinShowSec 기본값
        private const float Frame = 1f / 60f;

        private static DialogueAdvanceModel Started(int lines = 3)
        {
            var m = new DialogueAdvanceModel();
            m.Begin(lines);
            return m;
        }

        /// <summary>입력 없이 seconds를 넘길 때까지 굴린다 — 그동안 어떤 명령도 나오면 안 된다.</summary>
        private static void Idle(DialogueAdvanceModel m, float seconds)
        {
            for (float t = 0f; t <= seconds; t += Frame) // 부동소수 누적 오차로 경계에 걸리지 않게 한 프레임 더
            {
                Assert.AreEqual(DialogueCommand.None, m.Step(Frame, false, Min));
            }
        }

        // ---------- 시작 상태 ----------

        [Test]
        public void Begin_ShowsFirstLine_AndOwnsInput()
        {
            var m = Started();
            Assert.AreEqual(0, m.Index);
            Assert.IsTrue(m.IsActive);
            Assert.AreEqual(3, m.LineCount);
        }

        [Test]
        public void Begin_WithNoLines_IsInactive()
        {
            var m = new DialogueAdvanceModel();
            m.Begin(0);
            Assert.IsFalse(m.IsActive);
            Assert.AreEqual(DialogueCommand.None, m.Step(Frame, true, Min));
        }

        // ---------- ① 시간으로는 넘어가지 않는다 (이번 변경의 본질) ----------

        [Test]
        public void Time_Alone_NeverAdvances()
        {
            var m = Started();
            Idle(m, 30f); // 어떤 줄의 duration보다도 긴 시간
            Assert.AreEqual(0, m.Index);
            Assert.IsTrue(m.IsActive);
        }

        // ---------- ② 최소 표시 시간 = 연타 방어 ----------

        [Test]
        public void Advance_BeforeMinShowSec_IsIgnored()
        {
            var m = Started();
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(DialogueCommand.None, m.Step(0.01f, true, Min)); // 0.1s 동안 10연타
            }
            Assert.AreEqual(0, m.Index);
        }

        [Test]
        public void Advance_AtMinShowSec_MovesExactlyOneLine()
        {
            var m = Started();
            Idle(m, Min);
            Assert.AreEqual(DialogueCommand.ShowLine, m.Step(0f, true, Min));
            Assert.AreEqual(1, m.Index);
        }

        [Test]
        public void Advance_ResetsElapsedPerLine()
        {
            var m = Started();
            Idle(m, Min);
            Assert.AreEqual(DialogueCommand.ShowLine, m.Step(0f, true, Min));
            // 넘긴 직후의 두 번째 입력은 다시 최소 시간을 채워야 한다 (한 프레임에 두 줄 소비 금지)
            Assert.AreEqual(DialogueCommand.None, m.Step(Frame, true, Min));
            Assert.AreEqual(1, m.Index);
        }

        [Test]
        public void HoldingInput_ConsumesOneLinePerMinShowSec()
        {
            // 손가락을 계속 누르고 있어도(매 프레임 true) 줄당 최소 표시 시간은 보장된다
            var m = Started(4);
            int shown = 0;
            float elapsed = 0f;
            for (int i = 0; i < 600; i++)
            {
                DialogueCommand cmd = m.Step(Frame, true, Min);
                elapsed += Frame;
                if (cmd == DialogueCommand.ShowLine) shown++;
                if (cmd == DialogueCommand.Finish) break;
            }
            Assert.AreEqual(3, shown);                       // 0번은 Begin이 띄웠으므로 남은 3줄
            Assert.GreaterOrEqual(elapsed, Min * 4f - Frame); // 4줄 × 최소 표시 시간
        }

        [Test]
        public void MinShowZero_AdvancesImmediately()
        {
            var m = Started();
            Assert.AreEqual(DialogueCommand.ShowLine, m.Step(0f, true, 0f));
            Assert.AreEqual(1, m.Index);
        }

        // ---------- ③ 종료 후 입력 격리 ----------

        [Test]
        public void LastLine_Advance_Finishes()
        {
            var m = Started(2);
            Idle(m, Min);
            Assert.AreEqual(DialogueCommand.ShowLine, m.Step(0f, true, Min));
            Idle(m, Min);
            Assert.AreEqual(DialogueCommand.Finish, m.Step(0f, true, Min));
            Assert.IsFalse(m.IsActive);
            Assert.AreEqual(-1, m.Index);
        }

        [Test]
        public void AfterFinish_InputDoesNotLeak()
        {
            var m = Started(1);
            Idle(m, Min);
            Assert.AreEqual(DialogueCommand.Finish, m.Step(0f, true, Min));
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(DialogueCommand.None, m.Step(Frame, true, Min));
            }
        }

        [Test]
        public void Stop_EndsOwnershipImmediately()
        {
            var m = Started();
            m.Stop();
            Assert.IsFalse(m.IsActive);
            Assert.AreEqual(DialogueCommand.None, m.Step(Frame, true, 0f)); // 스킵 후 잔여 입력 무시
        }

        [Test]
        public void CanAdvance_TracksMinShowSec()
        {
            var m = Started();
            Assert.IsFalse(m.CanAdvance(Min));
            Idle(m, Min);
            Assert.IsTrue(m.CanAdvance(Min));
        }

        // ---------- 스킵 영역 판정 (해상도 무관) ----------

        private static readonly Rect SkipZone = new Rect(0.80f, 0.88f, 0.20f, 0.12f);

        [Test]
        public void SkipZone_TopRightCorner_Hits()
        {
            Assert.IsTrue(DialogueAdvanceModel.InViewportZone(new Vector2(1800f, 1000f), 1920f, 1080f, SkipZone));
        }

        [Test]
        public void SkipZone_ScalesWithResolution()
        {
            // 같은 비율 위치면 해상도가 달라도 같은 판정 (모바일 letterbox 대응)
            Assert.IsTrue(DialogueAdvanceModel.InViewportZone(new Vector2(750f, 400f), 800f, 420f, SkipZone));
        }

        [Test]
        public void SkipZone_ElsewhereMisses()
        {
            Assert.IsFalse(DialogueAdvanceModel.InViewportZone(new Vector2(960f, 540f), 1920f, 1080f, SkipZone));
            Assert.IsFalse(DialogueAdvanceModel.InViewportZone(new Vector2(1800f, 200f), 1920f, 1080f, SkipZone));
            Assert.IsFalse(DialogueAdvanceModel.InViewportZone(new Vector2(100f, 1000f), 1920f, 1080f, SkipZone));
        }

        [Test]
        public void SkipZone_ZeroScreen_IsSafe()
        {
            Assert.IsFalse(DialogueAdvanceModel.InViewportZone(Vector2.zero, 0f, 0f, SkipZone));
        }

        // ---------- 기도 조작 힌트: 귀퉁이 → 키/스틱 방향 ----------
        // 조준 판정(PrayerInteractable)의 역방향이므로, 조준 규칙이 바뀌면 여기가 먼저 깨져야 한다.

        [Test]
        public void AimHint_EachCorner_LightsOneVerticalAndOneHorizontal()
        {
            for (int corner = 0; corner < CornerIndex.Count; corner++)
            {
                int vertical = (PrayerAimHint.IsKeyLit(corner, AimKey.Up) ? 1 : 0)
                               + (PrayerAimHint.IsKeyLit(corner, AimKey.Down) ? 1 : 0);
                int horizontal = (PrayerAimHint.IsKeyLit(corner, AimKey.Left) ? 1 : 0)
                                 + (PrayerAimHint.IsKeyLit(corner, AimKey.Right) ? 1 : 0);
                Assert.AreEqual(1, vertical, $"corner {corner} 세로키");
                Assert.AreEqual(1, horizontal, $"corner {corner} 가로키");
            }
        }

        [Test]
        public void AimHint_TopRight_IsUpAndRight()
        {
            Assert.IsTrue(PrayerAimHint.IsKeyLit(CornerIndex.TopRight, AimKey.Up));
            Assert.IsTrue(PrayerAimHint.IsKeyLit(CornerIndex.TopRight, AimKey.Right));
            Assert.IsFalse(PrayerAimHint.IsKeyLit(CornerIndex.TopRight, AimKey.Down));
            Assert.IsFalse(PrayerAimHint.IsKeyLit(CornerIndex.TopRight, AimKey.Left));
        }

        [Test]
        public void AimHint_BottomLeft_IsDownAndLeft()
        {
            Assert.IsTrue(PrayerAimHint.IsKeyLit(CornerIndex.BottomLeft, AimKey.Down));
            Assert.IsTrue(PrayerAimHint.IsKeyLit(CornerIndex.BottomLeft, AimKey.Left));
        }

        [Test]
        public void AimHint_NoCorner_LightsNothing()
        {
            for (int k = 0; k < 4; k++)
            {
                Assert.IsFalse(PrayerAimHint.IsKeyLit(CornerIndex.None, (AimKey)k));
            }
            Assert.AreEqual(Vector2.zero, PrayerAimHint.StickDirection(CornerIndex.None));
        }

        [Test]
        public void AimHint_StickDirection_MatchesPrayerAimRule()
        {
            // PrayerInteractable: aim.y > 0 = 위쪽 귀퉁이 / aim.x < 0 = 왼쪽 귀퉁이
            Vector2 topLeft = PrayerAimHint.StickDirection(CornerIndex.TopLeft);
            Assert.Less(topLeft.x, 0f);
            Assert.Greater(topLeft.y, 0f);

            Vector2 bottomRight = PrayerAimHint.StickDirection(CornerIndex.BottomRight);
            Assert.Greater(bottomRight.x, 0f);
            Assert.Less(bottomRight.y, 0f);

            // 대각 정규화 — 터치 스냅 규약과 같은 각도 (0.7071)
            Assert.AreEqual(1f, topLeft.magnitude, 1e-3f);
        }

        [Test]
        public void AimHint_StickDirection_PassesPrayerAimJudgement()
        {
            // 힌트가 가리키는 방향을 그대로 조준 판정에 넣으면 원래 귀퉁이가 나와야 한다 (왕복 검증)
            for (int corner = 0; corner < CornerIndex.Count; corner++)
            {
                Vector2 aim = PrayerAimHint.StickDirection(corner);
                int judged = aim.y > 0f
                    ? (aim.x < 0f ? CornerIndex.TopLeft : CornerIndex.TopRight)
                    : (aim.x < 0f ? CornerIndex.BottomLeft : CornerIndex.BottomRight);
                Assert.AreEqual(corner, judged);
            }
        }
    }
}
