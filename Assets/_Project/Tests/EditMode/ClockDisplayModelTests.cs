using Morae.Game.Core;
using Morae.Game.Data;
using NUnit.Framework;

namespace Morae.Game.Tests.EditMode
{
    /// <summary>ClockDisplayModel 최소 케이스 — clockMode 4종 + 포맷 (명세 §1 페이즈 값 기준).</summary>
    public sealed class ClockDisplayModelTests
    {
        private static PhaseDef Phase(PhaseId id, float duration, int startMin, int endMin, ClockMode mode, int paramMin)
            => new PhaseDef(id, duration, startMin, endMin, mode, paramMin, 0f, 0f, 0f);

        // P1: 01:00~02:30, Sync — 표시 = 진실 (플로어)
        [Test]
        public void Sync_PassesTrueTimeThrough_Floored()
        {
            var p1 = Phase(PhaseId.P1, 60f, 60, 150, ClockMode.Sync, 0);
            Assert.AreEqual(97, ClockDisplayModel.DisplayedMinutes(97.9f, p1));
            Assert.AreEqual(60, ClockDisplayModel.DisplayedMinutes(60f, p1));
        }

        // P2: 02:30~04:00, Frozen -5 — 03:55(235)까지 진행 후 정지
        [Test]
        public void Frozen_BeforeFreezePoint_ShowsTrueTime()
        {
            var p2 = Phase(PhaseId.P2, 75f, 150, 240, ClockMode.Frozen, -5);
            Assert.AreEqual(200, ClockDisplayModel.DisplayedMinutes(200.4f, p2));
        }

        [Test]
        public void Frozen_AfterFreezePoint_CapsAtEndPlusParam()
        {
            var p2 = Phase(PhaseId.P2, 75f, 150, 240, ClockMode.Frozen, -5);
            Assert.AreEqual(235, ClockDisplayModel.DisplayedMinutes(236f, p2));
            Assert.AreEqual(235, ClockDisplayModel.DisplayedMinutes(239.9f, p2));
        }

        // P3: 04:00~06:00, Offset +40 — 노골적 점프
        [Test]
        public void Offset_Positive_AddsParam()
        {
            var p3 = Phase(PhaseId.P3, 105f, 240, 360, ClockMode.Offset, 40);
            Assert.AreEqual(340, ClockDisplayModel.DisplayedMinutes(300f, p3));
        }

        // P4: 06:00~06:50, Offset −30 — 역행
        [Test]
        public void Offset_Negative_SubtractsParam()
        {
            var p4 = Phase(PhaseId.P4, 75f, 360, 410, ClockMode.Offset, -30);
            Assert.AreEqual(370, ClockDisplayModel.DisplayedMinutes(400.2f, p4));
        }

        // P5: Fixed 445 — 진실과 무관하게 07:25 (핵심 기만)
        [Test]
        public void Fixed_AlwaysShowsParam()
        {
            var p5 = Phase(PhaseId.P5, 30f, 410, 420, ClockMode.Fixed, 445);
            Assert.AreEqual(445, ClockDisplayModel.DisplayedMinutes(410f, p5));
            Assert.AreEqual(445, ClockDisplayModel.DisplayedMinutes(419.9f, p5));
        }

        [Test]
        public void Format_PadsHoursAndMinutes()
        {
            Assert.AreEqual("07:25", ClockDisplayModel.Format(445));
            Assert.AreEqual("01:00", ClockDisplayModel.Format(60));
            Assert.AreEqual("03:55", ClockDisplayModel.Format(235));
        }

        [Test]
        public void Format_WrapsAroundMidnight()
        {
            Assert.AreEqual("01:00", ClockDisplayModel.Format(1440 + 60));
            Assert.AreEqual("23:50", ClockDisplayModel.Format(-10));
            Assert.AreEqual("00:00", ClockDisplayModel.Format(1440));
        }
    }
}
