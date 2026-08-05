using Morae.Game.Core;
using Morae.Game.Data;
using NUnit.Framework;

namespace Morae.Game.Tests.EditMode
{
    /// <summary>ClockDisplayModel — clockMode 4종 + 포맷 (명세 v0.3 페이즈 값 기준, 본편 01:00~07:30).</summary>
    public sealed class ClockDisplayModelTests
    {
        private static PhaseDef Phase(PhaseId id, float duration, int startMin, int endMin, ClockMode mode, int paramMin)
            => new PhaseDef(id, duration, startMin, endMin, mode, paramMin, 0f, 0f, 0f);

        // P1: 01:00~02:20, Sync — 표시 = 진실 (플로어)
        [Test]
        public void Sync_PassesTrueTimeThrough_Floored()
        {
            var p1 = Phase(PhaseId.P1, 60f, 60, 140, ClockMode.Sync, 0);
            Assert.AreEqual(97, ClockDisplayModel.DisplayedMinutes(97.9f, p1));
            Assert.AreEqual(60, ClockDisplayModel.DisplayedMinutes(60f, p1));
        }

        // P2: 02:20~03:40, Frozen -5 — 03:35(215)까지 진행 후 정지 (5분 멈춤)
        [Test]
        public void Frozen_BeforeFreezePoint_ShowsTrueTime()
        {
            var p2 = Phase(PhaseId.P2, 60f, 140, 220, ClockMode.Frozen, -5);
            Assert.AreEqual(180, ClockDisplayModel.DisplayedMinutes(180.4f, p2));
        }

        [Test]
        public void Frozen_AfterFreezePoint_CapsAtEndPlusParam()
        {
            var p2 = Phase(PhaseId.P2, 60f, 140, 220, ClockMode.Frozen, -5);
            Assert.AreEqual(215, ClockDisplayModel.DisplayedMinutes(216f, p2));
            Assert.AreEqual(215, ClockDisplayModel.DisplayedMinutes(219.9f, p2));
        }

        // P3: 03:40~05:00, Offset +40 — 노골적 점프
        [Test]
        public void Offset_Positive_AddsParam()
        {
            var p3 = Phase(PhaseId.P3, 75f, 220, 300, ClockMode.Offset, 40);
            Assert.AreEqual(300, ClockDisplayModel.DisplayedMinutes(260f, p3)); // 진실 04:20 → 표시 05:00
        }

        // P4: 05:00~05:40, Offset −30 — 역행 (P5 절정도 −30 유지: 표시 혼란 지속)
        [Test]
        public void Offset_Negative_SubtractsParam()
        {
            var p4 = Phase(PhaseId.P4, 40f, 300, 340, ClockMode.Offset, -30);
            Assert.AreEqual(290, ClockDisplayModel.DisplayedMinutes(320.2f, p4)); // 진실 05:20 → 표시 04:50
        }

        // P6: Fixed 445 — 진실(06:50~07:00)과 무관하게 07:25 (핵심 기만). P7·P8도 같은 값 = 정지
        [Test]
        public void Fixed_AlwaysShowsParam()
        {
            var p6 = Phase(PhaseId.P6, 40f, 410, 420, ClockMode.Fixed, 445);
            Assert.AreEqual(445, ClockDisplayModel.DisplayedMinutes(410f, p6));
            Assert.AreEqual(445, ClockDisplayModel.DisplayedMinutes(419.9f, p6));
        }

        [Test]
        public void Format_PadsHoursAndMinutes()
        {
            Assert.AreEqual("07:25", ClockDisplayModel.Format(445));
            Assert.AreEqual("01:00", ClockDisplayModel.Format(60));
            Assert.AreEqual("03:35", ClockDisplayModel.Format(215));
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
