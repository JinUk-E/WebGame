using System.Collections.Generic;
using Morae.Game.Data;
using Morae.Game.Gauges;
using UnityEngine;

namespace Morae.Game.Core
{
    /// <summary>
    /// EventTable 발화 시퀀서 (architecture §2.3 — D3 "신호"). 지터 없음 — 연출 타이밍은 고정.
    /// 페이즈 진입 시 해당 페이즈 행을 offset 오름차순 큐로 만들고, PhaseElapsed가 offset을 넘으면 발화.
    /// 발화 = GameEventFired 발행 + sanityDelta 적용 + 요의("urge") 설정 + 진짜 신호(TrueSignalStarted).
    /// 진짜 신호 후 RescueAutoOpenDelaySec(60s) 무응답 시 Rescued 엔딩 발행 — 소프트락 없음 (명세 §4).
    /// P7은 종단 페이즈라 PhaseElapsed가 duration을 넘어 계속 증가 — rescue-open(offset 60) 발화 가능.
    /// 시퀀서가 멈추면(게임오버·엔딩) 같이 멈춘다 — 별도 Begin/Stop 불필요.
    /// </summary>
    public sealed class EventDirector : MonoBehaviour
    {
        [SerializeField] private EventTable eventTable;
        [SerializeField] private BalanceConfig config;
        [SerializeField] private PhaseSequencer sequencer;
        [SerializeField] private Sanity sanity;

        private readonly List<EventDef> _queue = new List<EventDef>();
        private int _next;
        private float _rescueAtTotal = -1f; // TotalElapsed 기준. 음수 = 미예약

        private void OnEnable()
        {
            GameEvents.PhaseChanged += HandlePhaseChanged;
        }

        private void OnDisable()
        {
            GameEvents.PhaseChanged -= HandlePhaseChanged;
        }

        private void HandlePhaseChanged(PhaseId phase)
        {
            _queue.Clear();
            _next = 0;
            if (eventTable == null)
            {
                Debug.LogError("[EVENT] EventTable 미배선 — 이벤트 발화 불가", this);
                return;
            }
            BuildPhaseQueue(eventTable, phase, _queue);
        }

        /// <summary>페이즈 행 필터 + offset 오름차순 정렬 (순수 로직 — EditMode 테스트 대상).</summary>
        public static void BuildPhaseQueue(EventTable table, PhaseId phase, List<EventDef> result)
        {
            for (int i = 0; i < table.Count; i++)
            {
                EventDef def = table.GetEvent(i);
                if (def.PhaseId == phase) result.Add(def);
            }
            result.Sort((a, b) => a.Offset.CompareTo(b.Offset));
        }

        private void Update()
        {
            if (sequencer == null || !sequencer.IsRunning) return;

            while (_next < _queue.Count && sequencer.PhaseElapsed >= _queue[_next].Offset)
            {
                Fire(_queue[_next]);
                _next++;
            }

            if (_rescueAtTotal >= 0f && sequencer.TotalElapsed >= _rescueAtTotal)
            {
                _rescueAtTotal = -1f;
                Debug.Log("[EVENT] 진짜 신호 무응답 — 07:40 K씨 개문 (Rescued)");
                GameEvents.RaiseEndingStarted(EndingKind.Rescued); // MainLoop가 아니면 GameFlow가 무시
            }
        }

        private void Fire(EventDef def)
        {
            Debug.Log($"[EVENT] {def.Id} 발화 ({def.Kind}, {def.Channel})");
            GameEvents.RaiseGameEventFired(def);

            if (sanity != null)
            {
                if (!Mathf.Approximately(def.SanityDelta, 0f)) sanity.ApplyDelta(def.SanityDelta);
                if (def.Id == "urge") sanity.SetUrgeActive(true); // 해소는 JarInteractable (FR-15)
            }

            if (def.IsTrueSignal)
            {
                GameEvents.RaiseTrueSignalStarted();
                float delay = config != null ? config.RescueAutoOpenDelaySec : 60f;
                _rescueAtTotal = sequencer.TotalElapsed + delay;
            }
        }
    }
}
