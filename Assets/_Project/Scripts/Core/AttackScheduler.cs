using System.Collections.Generic;
using System.Text;
using Morae.Game.Data;
using Morae.Game.Gauges;
using Morae.Game.Interactions;
using Morae.Game.Player;
using UnityEngine;

namespace Morae.Game.Core
{
    /// <summary>
    /// 공격 스케줄 실행 (architecture §2.2). Begin(seed)에서 AttackScheduleBuilder로 전 공격을 확정한 뒤,
    /// 페이즈 로컬 공격 시계로 소비한다 — TV 켜짐 동안 시계가 ×BalanceConfig.TvAttackClockRate로 빨리 흘러
    /// 공격이 당겨질 뿐 횟수는 불변, 페이즈 경계도 넘지 않는다 (로컬 시계는 페이즈 진입 시 0 리셋).
    /// 전조: AttackTelegraphStarted 발행 + 이성 −8 (공격 1건당 1회 — dual도 1회. "공격 전조 발생 −8"의 해석, 결정 기록).
    /// 전조는 실시간 telegraphDuration 후 판정: 상쇄면 AttackResolved(true), 미상쇄면 AttackResolved(false) + 오염(직접 호출).
    /// 능동 방어: PrayerInteractable 완료 → TryCounter(corner) — 해당 귀퉁이 전조 활성 시 즉시 상쇄.
    /// 페이즈 전이는 PhaseSequencer 폴링(직접 읽기 — 게임플레이 내부는 이벤트 경유 금지 §1.2).
    /// </summary>
    public sealed class AttackScheduler : MonoBehaviour
    {
        [SerializeField] private AttackTable attackTable;
        [SerializeField] private PhaseTable phaseTable;
        [SerializeField] private BalanceConfig config;
        [SerializeField] private PhaseSequencer sequencer;
        [SerializeField] private SaltCorners salt;
        [SerializeField] private Sanity sanity;
        [SerializeField] private PlayerController player;
        [SerializeField] private TvInteractable tv;

        private struct ActiveTelegraph
        {
            public int Corner;
            public float Remaining;
            public bool Resolves;
        }

        private ScheduledAttack[] _schedule;
        private int _next;
        private int _phaseIndex;
        private float _localClock;
        private readonly List<ActiveTelegraph> _telegraphs = new List<ActiveTelegraph>(CornerIndex.Count);

        public bool IsRunning { get; private set; }
        /// <summary>페이즈 로컬 공격 시계 (디버그 표시용).</summary>
        public float LocalAttackClock => _localClock;
        public int ActiveTelegraphCount => _telegraphs.Count;
        public string NextAttackId =>
            IsRunning && _schedule != null && _next < _schedule.Length ? _schedule[_next].Id : "-";
        public float NextAttackTime =>
            IsRunning && _schedule != null && _next < _schedule.Length ? _schedule[_next].TriggerTime : -1f;

        /// <summary>본편 시작 — GameFlowController가 호출. 시드는 SessionContext(재시작마다 새로 — 지터 변주).</summary>
        public void Begin(int seed)
        {
            if (attackTable == null || phaseTable == null || config == null || sequencer == null || salt == null)
            {
                Debug.LogError("[ATTACK] 테이블/참조 미배선 — 스케줄러 시작 불가", this);
                return;
            }

            _schedule = AttackScheduleBuilder.Build(attackTable, phaseTable, seed);
            _next = 0;
            _phaseIndex = sequencer.CurrentPhaseIndex;
            _localClock = 0f;
            _telegraphs.Clear();
            IsRunning = true;
            LogSchedule(seed);
        }

        /// <summary>게임오버·엔딩 시 정지 — 활성 전조도 폐기.</summary>
        public void Stop()
        {
            IsRunning = false;
            _telegraphs.Clear();
        }

        /// <summary>
        /// 능동 방어 (명세 §2): 해당 귀퉁이에 활성 전조가 있으면 즉시 상쇄 판정. true = 상쇄됨.
        /// false면 호출자(PrayerInteractable)가 사후 정화(SaltCorners.Purify)로 처리.
        /// </summary>
        public bool TryCounter(int corner)
        {
            if (!IsRunning) return false;
            for (int i = 0; i < _telegraphs.Count; i++)
            {
                if (_telegraphs[i].Corner != corner) continue;
                _telegraphs.RemoveAt(i);
                Debug.Log($"[ATTACK] 귀퉁이 {corner} 전조 상쇄 — 기도 채널 완료");
                GameEvents.RaiseAttackResolved(corner, true);
                return true;
            }
            return false;
        }

        private void Update()
        {
            if (!IsRunning) return;

            float dt = Time.deltaTime;

            // 페이즈 전이 감지 (폴링) — 로컬 공격 시계 리셋
            int currentPhaseIndex = sequencer.CurrentPhaseIndex;
            if (currentPhaseIndex != _phaseIndex)
            {
                OnPhaseEntered(currentPhaseIndex);
            }

            bool tvOn = tv != null && tv.IsOn;
            _localClock += dt * (tvOn ? config.TvAttackClockRate : 1f);

            FireDueAttacks(currentPhaseIndex);
            TickTelegraphs(dt);
        }

        private void OnPhaseEntered(int phaseIndex)
        {
            _phaseIndex = phaseIndex;
            _localClock = 0f;

            // 방어 코드: 이전 페이즈에서 못 쏜 공격은 건너뛴다 (클램프 규칙상 발생하면 안 됨 — 발생 시 경고)
            while (_next < _schedule.Length && _schedule[_next].PhaseIndex < phaseIndex)
            {
                Debug.LogWarning($"[ATTACK] {_schedule[_next].Id} 미발동 스킵 — 페이즈 경계 클램프 확인 필요");
                _next++;
            }
        }

        private void FireDueAttacks(int currentPhaseIndex)
        {
            while (_next < _schedule.Length
                   && _schedule[_next].PhaseIndex == currentPhaseIndex
                   && _localClock >= _schedule[_next].TriggerTime)
            {
                Fire(_schedule[_next]);
                _next++;
            }
        }

        private void Fire(in ScheduledAttack attack)
        {
            int cornerA = attack.CornerA;
            int cornerB = attack.CornerB;
            if (attack.TargetRule == AttackTargetRule.FarthestFromPlayer)
            {
                Vector2 from = player != null ? (Vector2)player.transform.position : Vector2.zero;
                salt.SelectFarthestCorners(from, attack.DualCorner, out cornerA, out cornerB);
            }

            StartTelegraph(cornerA, attack.TelegraphDuration, attack.Resolves);
            if (cornerB != CornerIndex.None)
            {
                StartTelegraph(cornerB, attack.TelegraphDuration, attack.Resolves);
            }

            // 전조 발생 이성 −8 — 공격 1건당 1회 (dual도 1회)
            if (sanity != null) sanity.ApplyDelta(-config.SanityTelegraphHit);

            Debug.Log($"[ATTACK] {attack.Id} 전조 시작 — corner {cornerA}"
                      + (cornerB != CornerIndex.None ? $"+{cornerB}" : "")
                      + $" (판정까지 {attack.TelegraphDuration:F1}s)");
        }

        private void StartTelegraph(int corner, float duration, bool resolves)
        {
            _telegraphs.Add(new ActiveTelegraph { Corner = corner, Remaining = duration, Resolves = resolves });
            GameEvents.RaiseAttackTelegraphStarted(corner, duration);
        }

        private void TickTelegraphs(float dt)
        {
            // 전조는 실시간 진행 (반응 창은 TV 배속과 무관 — §2.2)
            for (int i = _telegraphs.Count - 1; i >= 0; i--)
            {
                ActiveTelegraph t = _telegraphs[i];
                t.Remaining -= dt;
                if (t.Remaining > 0f)
                {
                    _telegraphs[i] = t;
                    continue;
                }
                _telegraphs.RemoveAt(i);
                Resolve(t);
            }
        }

        private void Resolve(in ActiveTelegraph telegraph)
        {
            if (!telegraph.Resolves)
            {
                // 전조만 내는 공격 (P5 튜닝 여지) — 오염 없음. countered=true로 발행해 표현 계층이 전조 연출을 정리하게 한다 (결정 기록)
                Debug.Log($"[ATTACK] 귀퉁이 {telegraph.Corner} 전조 종료 — 판정 생략 (resolves=false)");
                GameEvents.RaiseAttackResolved(telegraph.Corner, true);
                return;
            }
            Debug.Log($"[ATTACK] 귀퉁이 {telegraph.Corner} 판정 — 미상쇄, 오염 +1");
            GameEvents.RaiseAttackResolved(telegraph.Corner, false);
            salt.Contaminate(telegraph.Corner);
        }

        private void LogSchedule(int seed)
        {
            var sb = new StringBuilder(256); // Begin 1회 — 핫패스 아님
            sb.Append($"[ATTACK] 스케줄 확정 (seed={seed}, {_schedule.Length}건):");
            for (int i = 0; i < _schedule.Length; i++)
            {
                ScheduledAttack a = _schedule[i];
                sb.Append($"\n  {a.Id} @{a.PhaseId}+{a.TriggerTime:F1}s corner=")
                  .Append(a.TargetRule == AttackTargetRule.FarthestFromPlayer
                      ? "최원거리(발동 시 해석)"
                      : a.CornerA + (a.CornerB != CornerIndex.None ? $"+{a.CornerB}" : ""));
            }
            Debug.Log(sb.ToString());
        }
    }
}
