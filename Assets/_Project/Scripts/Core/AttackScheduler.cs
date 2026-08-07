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
    /// 공격 스케줄 실행 (architecture §2.2, v0.3 개정). Begin(seed)에서 AttackScheduleBuilder로 전 공격을 확정한 뒤,
    /// 페이즈 로컬 공격 시계로 소비한다 — TV 켜짐 동안 시계가 ×BalanceConfig.TvAttackClockRate로 빨리 흘러
    /// 공격이 당겨질 뿐 횟수는 불변, 페이즈 경계도 넘지 않는다 (로컬 시계는 페이즈 진입 시 0 리셋).
    /// 전조: AttackTelegraphStarted 발행 + 이성 −8 (공격 1건당 1회 — N동시도 1회. "공격 전조 발생 −8"의 해석, 결정 기록).
    /// 전조는 실시간 telegraphDuration 후 판정: 상쇄면 AttackResolved(true), 미상쇄면 AttackResolved(false) + 오염(직접 호출).
    /// 능동 방어: PrayerInteractable 완료 → TryCounter(corner) — 해당 귀퉁이 전조 활성 시 즉시 상쇄.
    /// v0.3 함정 시퀀스(P6): 스케줄 테이블이 아닌 전용 타임라인(TrapTimeline·BalanceConfig trap*) —
    ///   가짜 목소리 ② → 정적(무공격) → 4귀퉁이 동시 웨이브 ×trapWaveCount. PhaseElapsed 실시간 기준(TV 배속 무관).
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
        private readonly List<ActiveTelegraph> _telegraphs = new List<ActiveTelegraph>(CornerIndex.Count * 2);
        // 만료 전조를 리스트에서 다 뺀 뒤에 판정하기 위한 스냅샷 버퍼 (재진입 크래시 방지 — TickTelegraphs 주석 참조)
        private readonly List<ActiveTelegraph> _resolveBuffer = new List<ActiveTelegraph>(CornerIndex.Count * 2);
        private readonly int[] _fireBuffer = new int[CornerIndex.Count];   // 발동 프레임 귀퉁이 확정 버퍼 (할당 없음)
        private readonly bool[] _fireTaken = new bool[CornerIndex.Count];  // 같은 공격 내 중복 방지

        // v0.3 최후의 함정 (P6) — 스케줄 밖 전용 시퀀스
        private int _trapPhaseIndex = -1;
        private int _trapWavesFired;

        // v0.5 §3 프롤로그 강제 학습 — 스케줄·함정 없이 전조 하나만 굴리는 안전 모드
        private System.Action<int, bool> _onTrainingResolved;
        private static readonly ScheduledAttack[] EmptySchedule = System.Array.Empty<ScheduledAttack>();

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

            _onTrainingResolved = null;
            _schedule = AttackScheduleBuilder.Build(attackTable, phaseTable, seed);
            _next = 0;
            _phaseIndex = sequencer.CurrentPhaseIndex;
            _localClock = 0f;
            _telegraphs.Clear();
            _trapPhaseIndex = FindPhaseIndex(PhaseId.P6);
            _trapWavesFired = 0;
            IsRunning = true;
            LogSchedule(seed);
        }

        /// <summary>게임오버·엔딩 시 정지 — 활성 전조도 폐기.</summary>
        public void Stop()
        {
            IsRunning = false;
            _onTrainingResolved = null;
            DiscardActiveTelegraphs();
        }

        /// <summary>
        /// 활성 전조를 폐기하면서 **반드시 AttackResolved(countered:true)를 발행**한다.
        /// 조용히 Clear만 하면 구독자(조명·소금 뷰·실루엣·부적 UI)의 전조 상태가 굳어
        /// 엔딩·게임오버 화면 뒤에서 귀퉁이 붉은 점멸이 계속 뛴다.
        /// </summary>
        private void DiscardActiveTelegraphs()
        {
            // 매번 Count를 다시 읽는다 — 이 메서드는 Resolve 안에서 재진입해 들어올 수 있다(게임오버 경로).
            while (_telegraphs.Count > 0)
            {
                int last = _telegraphs.Count - 1;
                int corner = _telegraphs[last].Corner;
                _telegraphs.RemoveAt(last);
                GameEvents.RaiseAttackResolved(corner, true);
            }
        }

        // ---------- v0.5 §3 프롤로그 강제 학습 (안전 구간) ----------

        /// <summary>
        /// 학습 모드 진입 (PrologueDirector가 호출). 스케줄·함정 없이 전조 하나만 굴린다.
        /// 판정 결과는 오염이 아니라 콜백으로 나간다 — **실패해도 소금이 더러워지지 않고 사망하지도 않는다.**
        /// 상쇄 경로는 본편과 완전히 같다 (PrayerInteractable → TryCounter) — 여기서 배운 손이 본편에 그대로 쓰인다.
        /// </summary>
        public void BeginTraining(System.Action<int, bool> onResolved)
        {
            if (config == null || sequencer == null || salt == null)
            {
                Debug.LogError("[ATTACK] 참조 미배선 — 학습 모드 시작 불가", this);
                return;
            }
            _schedule = EmptySchedule;
            _next = 0;
            _phaseIndex = sequencer.CurrentPhaseIndex; // Begin과 대칭 — 첫 프레임에 헛 전이가 잡히지 않게
            _localClock = 0f;
            _telegraphs.Clear();
            _trapPhaseIndex = -1;   // 함정 시퀀스 비활성
            _trapWavesFired = 0;
            _onTrainingResolved = onResolved;
            IsRunning = true;
            GameEvents.RaiseTrainingModeChanged(true);
            Debug.Log("[ATTACK] 학습 모드 시작 — 오염·사망 없음 (프롤로그 안전 구간)");
        }

        /// <summary>학습 전조 1회 발사. 이성 감소도 없다 (프롤로그에는 이성 게이지가 아직 돌지 않는다).</summary>
        public void FireTrainingTelegraph(int corner, float duration)
        {
            if (!IsTraining) return;
            StartTelegraph(corner, duration, resolves: true);
            Debug.Log($"[ATTACK] 학습 전조 — 귀퉁이 {corner} ({duration:F1}s 안에 그 방향으로 기도)");
        }

        /// <summary>학습 종료 — 본편 Begin이 상태를 덮어쓰므로 정리만 한다.</summary>
        public void EndTraining()
        {
            if (!IsTraining) return;
            _onTrainingResolved = null;
            IsRunning = false;
            DiscardActiveTelegraphs(); // 조용히 비우면 조명·소금 뷰의 전조 연출이 굳는다
            GameEvents.RaiseTrainingModeChanged(false);
            Debug.Log("[ATTACK] 학습 모드 종료");
        }

        public bool IsTraining => _onTrainingResolved != null;

        /// <summary>
        /// 능동 방어 (명세 §2): 해당 귀퉁이에 활성 전조가 있으면 즉시 상쇄 판정. true = 상쇄됨.
        /// false면 호출자(PrayerInteractable)가 사후 정화(SaltCorners.Purify)로 처리.
        /// 함정 웨이브 전조도 동일 경로 — "사이 구간 기도·전조 상쇄는 정상 동작" (v0.3).
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
                if (IsTraining) _onTrainingResolved(corner, true);
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

            // v0.5 §1 — 흑화 개수만큼 공격 간격 ×(1 − 0.05n). TV 가속과는 곱연산 (무너질수록 빨라지는 하강 나선).
            bool tvOn = tv != null && tv.IsOn;
            float tvRate = tvOn ? config.TvAttackClockRate : 1f;
            int blackCount = salt != null ? salt.BlackCornerCount : 0;
            _localClock += dt * CornerPenaltyModel.AttackClockRate(blackCount,
                config.BlackCornerAttackIntervalReduction, config.MinAttackIntervalScale, tvRate);

            FireDueAttacks(currentPhaseIndex);
            TickTrapSequence(currentPhaseIndex);
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

        // ---------- v0.3 최후의 함정 (P6) ----------

        /// <summary>
        /// P6 전용 타임라인 — PhaseElapsed 실시간 기준 (TV 배속·로컬 시계 무관, 연출 고정).
        /// 스케줄 테이블에 P6 행이 없고 P5 전조는 P5 안에서 판정 완료(클램프 규칙)라
        /// 가짜 목소리 ② + 정적 구간의 "소금 전조 절대 금지"가 구조적으로 보장된다.
        /// 미개문 조건은 별도 검사 불필요 — 진짜 신호 전 개문은 즉시 게임오버라 스케줄러가 이미 정지한다.
        /// </summary>
        private void TickTrapSequence(int currentPhaseIndex)
        {
            if (currentPhaseIndex != _trapPhaseIndex || _trapWavesFired >= config.TrapWaveCount) return;

            float waveStart = TrapTimeline.WaveStartTime(_trapWavesFired,
                config.TrapVoiceLeadSec, config.TrapQuietSec, config.TrapTelegraphSec, config.TrapWaveGapSec);
            if (sequencer.PhaseElapsed < waveStart) return;

            _trapWavesFired++;
            FireTrapWave();
        }

        /// <summary>4귀퉁이 동시 공격 ("유혹을 거부한 대가"). 포화(흑+심화) 귀퉁이는 전조 생략 — 변화가 없는 위협은 소음.</summary>
        private void FireTrapWave()
        {
            int started = 0;
            for (int i = 0; i < CornerIndex.Count; i++)
            {
                if (salt.IsSaturated(i)) continue;
                StartTelegraph(i, config.TrapTelegraphSec, resolves: true);
                started++;
            }

            if (started > 0 && sanity != null) sanity.ApplyDelta(-config.SanityTelegraphHit); // 웨이브 1회 = 공격 1건

            Debug.Log($"[ATTACK] 함정 웨이브 {_trapWavesFired}/{config.TrapWaveCount} — 동시 전조 {started}곳" +
                      $" (판정까지 {config.TrapTelegraphSec:F1}s)");
        }

        // ---------- 스케줄 공격 발동 ----------

        private void Fire(in ScheduledAttack attack)
        {
            // 1) 귀퉁이 확정 — RandomCorner는 빌드 시 배정, FarthestFromPlayer는 발동 시점 해석
            int count;
            if (attack.TargetRule == AttackTargetRule.FarthestFromPlayer)
            {
                Vector2 from = player != null ? (Vector2)player.transform.position : Vector2.zero;
                count = salt.SelectFarthestCorners(from, Mathf.Min(attack.CornerCount, CornerIndex.Count), _fireBuffer);
            }
            else
            {
                count = Mathf.Min(attack.CornerCount, attack.Corners.Length);
                for (int i = 0; i < count; i++) _fireBuffer[i] = attack.Corners[i];
            }

            // 2) 포화(흑+심화) 재지정 (v0.3: 흑 미심화는 유효 타깃 — 피격 = 심화 스택).
            //    사전 배정이 포화 귀퉁이를 가리키면 이 공격이 노리지 않은 비포화 귀퉁이로 돌린다 — 낭비 공격 방지, 압박 유지.
            for (int i = 0; i < CornerIndex.Count; i++) _fireTaken[i] = false;
            for (int i = 0; i < count; i++)
            {
                if (_fireBuffer[i] != CornerIndex.None) _fireTaken[_fireBuffer[i]] = true;
            }
            int started = 0;
            for (int i = 0; i < count; i++)
            {
                int corner = _fireBuffer[i];
                if (corner == CornerIndex.None) continue;
                if (salt.IsSaturated(corner))
                {
                    _fireTaken[corner] = false;
                    corner = RetargetToAvailable();
                    if (corner == CornerIndex.None) continue; // 대체 불가 — 이 갈래는 소멸
                    _fireTaken[corner] = true;
                }
                StartTelegraph(corner, attack.TelegraphDuration, attack.Resolves);
                started++;
            }

            if (started == 0)
            {
                Debug.Log($"[ATTACK] {attack.Id} 스킵 — 공격 가능한 귀퉁이 없음 (붕괴 판정 대기)");
                return;
            }

            // 전조 발생 이성 −8 — 공격 1건당 1회 (N동시도 1회)
            if (sanity != null) sanity.ApplyDelta(-config.SanityTelegraphHit);

            Debug.Log($"[ATTACK] {attack.Id} 전조 시작 — 동시 {started}곳 (판정까지 {attack.TelegraphDuration:F1}s)");
        }

        /// <summary>
        /// 이 공격이 아직 안 잡은(비 _fireTaken) 비포화 귀퉁이 중 재지정 — 활성 전조가 없는 곳 중 플레이어 최원거리 우선
        /// (확인하러 갈 수 없는 곳이 가장 위협적). 후보가 전조뿐이면 겹침 허용.
        /// </summary>
        private int RetargetToAvailable()
        {
            Vector2 from = player != null ? (Vector2)player.transform.position : Vector2.zero;
            int best = CornerIndex.None;
            float bestSqr = -1f;
            int bestAny = CornerIndex.None;
            float bestAnySqr = -1f;
            for (int i = 0; i < CornerIndex.Count; i++)
            {
                if (_fireTaken[i] || salt.IsSaturated(i)) continue;
                float sqr = (salt.GetCornerPosition(i) - from).sqrMagnitude;
                if (sqr > bestAnySqr)
                {
                    bestAnySqr = sqr;
                    bestAny = i;
                }
                if (HasActiveTelegraph(i)) continue;
                if (sqr > bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }
            return best != CornerIndex.None ? best : bestAny;
        }

        private bool HasActiveTelegraph(int corner)
        {
            for (int i = 0; i < _telegraphs.Count; i++)
            {
                if (_telegraphs[i].Corner == corner) return true;
            }
            return false;
        }

        private void StartTelegraph(int corner, float duration, bool resolves)
        {
            _telegraphs.Add(new ActiveTelegraph { Corner = corner, Remaining = duration, Resolves = resolves });
            GameEvents.RaiseAttackTelegraphStarted(corner, duration);
        }

        /// <summary>
        /// 전조는 실시간 진행 (반응 창은 TV 배속과 무관 — §2.2).
        /// ⚠ 만료 항목을 **리스트에서 전부 뺀 뒤에** 판정한다. Resolve는 오염 → 붕괴 → GameOver →
        /// GameFlowController.StopMainLoop → Stop() → `_telegraphs.Clear()`로 되돌아올 수 있어,
        /// 순회 도중 호출하면 인덱스가 깨진다 (P6 함정 웨이브의 4동시 전조가 정확히 그 조건).
        /// </summary>
        private void TickTelegraphs(float dt)
        {
            _resolveBuffer.Clear();
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
                _resolveBuffer.Add(t);
            }

            for (int i = 0; i < _resolveBuffer.Count; i++)
            {
                // 앞선 판정이 게임오버를 냈으면 남은 판정은 무효 — 죽은 뒤에 소금이 더 더러워지지 않는다
                if (!IsRunning) break;
                Resolve(_resolveBuffer[i]);
            }
            _resolveBuffer.Clear();
        }

        private void Resolve(in ActiveTelegraph telegraph)
        {
            // 학습 모드 — 오염 없이 콜백만 (실패해도 벌이 없는 안전 구간, v0.5 §3)
            if (IsTraining)
            {
                Debug.Log($"[ATTACK] 학습 전조 판정 — 귀퉁이 {telegraph.Corner} 미상쇄 (오염 없음, 재시도)");
                GameEvents.RaiseAttackResolved(telegraph.Corner, false);
                _onTrainingResolved(telegraph.Corner, false);
                return;
            }

            if (!telegraph.Resolves)
            {
                // 전조만 내는 공격 (튜닝 여지) — 오염 없음. countered=true로 발행해 표현 계층이 전조 연출을 정리하게 한다 (결정 기록)
                Debug.Log($"[ATTACK] 귀퉁이 {telegraph.Corner} 전조 종료 — 판정 생략 (resolves=false)");
                GameEvents.RaiseAttackResolved(telegraph.Corner, true);
                return;
            }
            Debug.Log($"[ATTACK] 귀퉁이 {telegraph.Corner} 판정 — 미상쇄, 오염 +1");
            GameEvents.RaiseAttackResolved(telegraph.Corner, false);
            salt.Contaminate(telegraph.Corner);
        }

        private int FindPhaseIndex(PhaseId id)
        {
            for (int i = 0; i < phaseTable.Count; i++)
            {
                if (phaseTable.GetPhase(i).PhaseId == id) return i;
            }
            return -1;
        }

        private void LogSchedule(int seed)
        {
            var sb = new StringBuilder(256); // Begin 1회 — 핫패스 아님
            sb.Append($"[ATTACK] 스케줄 확정 (seed={seed}, {_schedule.Length}건 + 함정 {config.TrapWaveCount}웨이브):");
            // 능동 방어 부등식을 실수치로 한 줄 남긴다 — 두 파일에 흩어진 수치의 **관계**가 깨졌는지는
            // 각 값만 봐서는 안 보인다 (v0.6.1 이전 3.0s/3.0s 사고). 회귀 방어는 CounterTimingTests.
            float telegraph = _schedule.Length > 0 ? _schedule[0].TelegraphDuration : config.TrapTelegraphSec;
            sb.Append($"\n  [타이밍] {CounterTimingModel.Describe(config.PrayerChannelSec, telegraph, config.PrayerDeepenedMultiplier, config.MoveSpeed)}");
            for (int i = 0; i < _schedule.Length; i++)
            {
                ScheduledAttack a = _schedule[i];
                sb.Append($"\n  {a.Id} @{a.PhaseId}+{a.TriggerTime:F1}s x{a.CornerCount} corner=");
                if (a.TargetRule == AttackTargetRule.FarthestFromPlayer)
                {
                    sb.Append("최원거리(발동 시 해석)");
                }
                else
                {
                    for (int c = 0; c < a.Corners.Length; c++)
                    {
                        if (c > 0) sb.Append('+');
                        sb.Append(a.Corners[c]);
                    }
                }
            }
            Debug.Log(sb.ToString());
        }
    }
}
