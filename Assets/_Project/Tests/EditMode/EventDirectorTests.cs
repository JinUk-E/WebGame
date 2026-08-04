using System.Collections.Generic;
using Morae.Game.Core;
using Morae.Game.Data;
using NUnit.Framework;
using UnityEngine;

namespace Morae.Game.Tests.EditMode
{
    /// <summary>
    /// EventDirector.BuildPhaseQueue — 페이즈 필터·offset 오름차순 정렬 (순수 로직).
    /// 발화·진짜 신호·구조 타이머는 MonoBehaviour 경로라 PlayMode/실기 확인 대상.
    /// </summary>
    public sealed class EventDirectorTests
    {
        private static SubtitleLine[] Lines() => new[] { new SubtitleLine("", "테스트", 1f) };

        private static EventTable Table(params EventDef[] events)
        {
            var table = ScriptableObject.CreateInstance<EventTable>();
            table.EditorSetEvents(events);
            return table;
        }

        [Test]
        public void BuildPhaseQueue_해당_페이즈만_필터()
        {
            EventTable table = Table(
                new EventDef("a", PhaseId.P1, 15f, GameEventKind.Hint, AudioChannel.Room, 0f, false, Lines()),
                new EventDef("b", PhaseId.P3, 45f, GameEventKind.Scare, AudioChannel.Window, -10f, false, Lines()),
                new EventDef("c", PhaseId.P1, 40f, GameEventKind.Scare, AudioChannel.Window, 0f, false, Lines()));

            var result = new List<EventDef>();
            EventDirector.BuildPhaseQueue(table, PhaseId.P1, result);

            Assert.AreEqual(2, result.Count);
            foreach (EventDef def in result) Assert.AreEqual(PhaseId.P1, def.PhaseId);
        }

        [Test]
        public void BuildPhaseQueue_offset_오름차순_정렬()
        {
            EventTable table = Table(
                new EventDef("late", PhaseId.P3, 75f, GameEventKind.Scare, AudioChannel.Window, 0f, false, Lines()),
                new EventDef("early", PhaseId.P3, 15f, GameEventKind.Scare, AudioChannel.Door, 0f, false, Lines()),
                new EventDef("mid", PhaseId.P3, 45f, GameEventKind.Scare, AudioChannel.Window, 0f, false, Lines()));

            var result = new List<EventDef>();
            EventDirector.BuildPhaseQueue(table, PhaseId.P3, result);

            Assert.AreEqual(3, result.Count);
            Assert.AreEqual("early", result[0].Id);
            Assert.AreEqual("mid", result[1].Id);
            Assert.AreEqual("late", result[2].Id);
        }

        [Test]
        public void BuildPhaseQueue_빈_페이즈는_빈_큐()
        {
            EventTable table = Table(
                new EventDef("a", PhaseId.P1, 15f, GameEventKind.Hint, AudioChannel.Room, 0f, false, Lines()));

            var result = new List<EventDef>();
            EventDirector.BuildPhaseQueue(table, PhaseId.P6, result); // P6 = 정적 — 이벤트 없음

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void 명세_테이블_진짜_신호는_정확히_1행()
        {
            // 명세 §4 — 유일한 진짜 신호. DataAssetBuilder가 생성하는 실제 에셋 규약의 회귀 방어.
            EventTable table = Table(
                new EventDef("fake-1", PhaseId.P2, 35f, GameEventKind.FakeVoice, AudioChannel.Door, 0f, false, Lines()),
                new EventDef("true-signal", PhaseId.P7, 0f, GameEventKind.TrueSignal, AudioChannel.Door, 0f, true, Lines()),
                new EventDef("rescue-open", PhaseId.P7, 60f, GameEventKind.Scripted, AudioChannel.Door, 0f, false, Lines()));

            var result = new List<EventDef>();
            EventDirector.BuildPhaseQueue(table, PhaseId.P7, result);

            int trueSignals = 0;
            foreach (EventDef def in result)
            {
                if (def.IsTrueSignal) trueSignals++;
            }
            Assert.AreEqual(1, trueSignals);
            Assert.AreEqual("true-signal", result[0].Id); // offset 0 — P7 진입 즉시
        }
    }
}
