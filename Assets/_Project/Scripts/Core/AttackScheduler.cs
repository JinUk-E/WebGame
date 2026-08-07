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
    /// 공격 스케줄 실행 (§2.2). Begin(seed)에서 AttackScheduleBuilder로 전 공격을 확정한 뒤,
    /// 페이즈 로컬 공격 시계로 소비한다 — TV 켜짐 동안 시계가 ×TvAttackClockRate로 빨리 흘러
    /// 공격이 당겨질 뿐 횟수는 불변, 페이즈 경계도 넘지 않는다 (로컬 시계는 페이즈 진입 시 0 리셋).
    /// 전조: AttackTelegraphStarted 발행 + 이성 −8. 전조 종료 시 오염 +1 (SaltCorners 직접 호출).
    ///
    /// <para>
    /// <b>v0.7에서 사라진 것 — 이 클래스의 절반이다.</b>
    /// ① <b>상쇄(TryCounter)</b> — "전조 안에 기도를 완료하면 오염을 막는다"는 능동 방어가 사라졌다.
    ///    이제 전조는 예고이고, 대응은 오염된 뒤 그 자리에 가서 뿌리는 것이다.
    /// ② <b>다중 귀퉁이 동시 공격</b> — 공격 1건 = 1귀퉁이. 이로써 발동 프레임 버퍼(_fireBuffer/_fireTaken),
    ///    포화 재타겟(RetargetToAvailable/HasActiveTelegraph)이 통째로 필요 없어졌다.
    /// ③ <b>Resolves 갈래</b> — 전 행이 true였다(실사용 0).
    /// P6 함정도 "4귀퉁이 동시 웨이브"에서 <b>최원거리 단일 연발</b>로 바뀐다 — 자세한 이유는 TickTrapSequence 주석.
    /// </para>
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
        }

        private ScheduledAttack[] _schedule;
        private int _next;
        private int _phaseIndex;
        private float _localClock;
        private readonly List<ActiveTelegraph> _telegraphs = new List<ActiveTelegraph>(CornerIndex.Count);
        // 만료 전조를 리스트에서 다 뺀 뒤에 판정하기 위한 스냅샷 버퍼 (재진입 방지 — TickTelegraphs 주석 참조)
        private readonly List<ActiveTelegraph> _resolveBuffer = new List<ActiveTelegraph>(CornerIndex.Count);

        // P6 연발 — 스케줄 밖 전용 시퀀스
        private int _trapPhaseIndex = -1;
        private int _trapWavesFired;

        // 프롤로그 강제 학습 — 스케줄·연발 없이 오염 하나만 굴리는 안전 모드
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

            ClearTrainingMode();
            _schedule = AttackScheduleBuilder.Build(attackTable, phaseTable, seed, config.MinAttackGapSec);
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
            ClearTrainingMode();
            DiscardActiveTelegraphs();
        }

        /// <summary>
        /// 학습 모드를 내리면서 <b>반드시 TrainingModeChanged(false)를 발행</b>한다.
        /// 조용히 <c>_onTrainingResolved = null</c>만 하면 학습 연출을 구독한 표현 계층
        /// (LightingController 스포트라이트·DestinationMarkerView)이 켜진 채로 굳어
        /// 본편이 어두운 방 + 목적지 서클을 달고 시작한다.
        /// </summary>
        private void ClearTrainingMode()
        {
            if (_onTrainingResolved == null) return;
            _onTrainingResolved = null;
            GameEvents.RaiseTrainingModeChanged(false);
        }

        /// <summary>
        /// 활성 전조를 폐기하면서 <b>반드시 AttackResolved(countered:true)를 발행</b>한다.
        /// 조용히 Clear만 하면 구독자(조명·소금 뷰·실루엣)의 전조 상태가 굳어
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

        // ---------- 프롤로그 강제 학습 (안전 구간) ----------

        /// <summary>
        /// 학습 모드 진입 (PrologueDirector가 호출). 스케줄·연발 없이 전조 하나만 굴린다.
        /// 판정 결과는 콜백으로 나가되 <b>오염은 실제로 일어난다</b> — 새 조작에서는 "더러워진 것을 지우는 것"이
        /// 배워야 할 동사이기 때문이다. 대신 사망은 없다(부적이 아직 돌지 않는다).
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
            _trapPhaseIndex = -1;
            _trapWavesFired = 0;
            _onTrainingResolved = onResolved;
            IsRunning = true;
            GameEvents.RaiseTrainingModeChanged(true);
            Debug.Log("[ATTACK] 학습 모드 시작 — 사망 없음 (프롤로그 안전 구간)");
        }

        /// <summary>학습 전조 1회 발사. 이성 감소는 없다 (프롤로그에는 이성 게이지가 아직 돌지 않는다).</summary>
        public void FireTrainingTelegraph(int corner, float duration)
        {
            if (!IsTraining) return;
            StartTelegraph(corner, duration);
            Debug.Log($"[ATTACK] 학습 전조 — 귀퉁이 {corner} ({duration:F1}s 뒤 오염)");
        }

        /// <summary>학습 종료 — 본편 Begin이 상태를 덮어쓰므로 정리만 한다.</summary>
        public void EndTraining()
        {
            if (!IsTraining) return;
            IsRunning = false;
            ClearTrainingMode();
            DiscardActiveTelegraphs(); // 조용히 비우면 조명·소금 뷰의 전조 연출이 굳는다
            Debug.Log("[ATTACK] 학습 모드 종료");
        }

        public bool IsTraining => _onTrainingResolved != null;

        /// <summary>
        /// 학습 구간에서 플레이어가 소금 복구에 성공했음을 알린다 (PrologueDirector가 CornerStageChanged로 감지해 호출).
        /// 본편에는 대응 경로가 없다 — 본편의 복구는 그냥 SaltCorners.Purify이고 스케줄러가 알 필요가 없다.
        /// </summary>
        public void NotifyTrainingCleared(int corner)
        {
            if (!IsTraining) return;
            _onTrainingResolved(corner, true);
        }

        private void Update()
        {
            if (!IsRunning) return;

            float dt = Time.deltaTime;

            // 페이즈 전이 감지 (폴링 — 게임플레이 내부는 이벤트 경유 금지 §1.2)
            int currentPhaseIndex = sequencer.CurrentPhaseIndex;
            if (currentPhaseIndex != _phaseIndex)
            {
                OnPhaseEntered(currentPhaseIndex);
            }

            bool tvOn = tv != null && tv.IsOn;
            _localClock += dt * (tvOn ? config.TvAttackClockRate : 1f);

            FireDueAttacks(currentPhaseIndex);
            TickTrapSequence(currentPhaseIndex);
            TickTelegraphs(dt);
        }

        private void OnPhaseEntered(int phaseIndex)
        {
            _phaseIndex = phaseIndex;
            _localClock = 0f;

            // 방어 코드: 이전 페이즈에서 못 쏜 공격은 건너뛴다 (클램프 규칙상 발생하면 안 됨)
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

        // ---------- P6 최후의 연발 ----------

        /// <summary>
        /// P6 전용 타임라인 — PhaseElapsed 실시간 기준 (TV 배속·로컬 시계 무관, 연출 고정).
        /// <para>
        /// <b>v0.7: "4귀퉁이 동시 웨이브" → "최원거리 단일 연발".</b> 동시 웨이브는 새 조작에서 확정 사망이었다
        /// (순회 5.23초 + 홀드 4회). 대신 <b>플레이어에게서 가장 먼 귀퉁이</b>를 짧은 간격으로 연달아 치면,
        /// 방 최장 거리 10.44u(2.98초)를 계속 왕복하게 되어 압박은 그대로면서 <b>한 번에 하나만 보면 된다</b>.
        /// 4개를 동시에 읽으라고 요구하지 않으므로 무튜토리얼 목표와도 맞는다.
        /// </para>
        /// </summary>
        private void TickTrapSequence(int currentPhaseIndex)
        {
            if (currentPhaseIndex != _trapPhaseIndex || _trapWavesFired >= config.TrapWaveCount) return;

            float waveStart = TrapTimeline.WaveStartTime(_trapWavesFired,
                config.TrapVoiceLeadSec, config.TrapQuietSec, config.TrapTelegraphSec, config.TrapWaveGapSec);
            if (sequencer.PhaseElapsed < waveStart) return;

            _trapWavesFired++;
            FireTrapShot();
        }

        /// <summary>연발 1발 — 플레이어 최원거리 귀퉁이 ("확인하러 갈 수 없는 곳이 가장 위협적").</summary>
        private void FireTrapShot()
        {
            int corner = salt.SelectFarthestCorner(PlayerPosition());
            if (corner == CornerIndex.None) return;

            StartTelegraph(corner, config.TrapTelegraphSec);
            if (sanity != null) sanity.ApplyDelta(-config.SanityTelegraphHit);
            Debug.Log($"[ATTACK] 최후의 연발 {_trapWavesFired}/{config.TrapWaveCount} — 귀퉁이 {corner}" +
                      $" (판정까지 {config.TrapTelegraphSec:F1}s)");
        }

        // ---------- 스케줄 공격 발동 ----------

        private void Fire(in ScheduledAttack attack)
        {
            int corner = attack.TargetRule == AttackTargetRule.FarthestFromPlayer
                ? salt.SelectFarthestCorner(PlayerPosition())
                : attack.Corner;
            if (corner == CornerIndex.None) return;

            StartTelegraph(corner, attack.TelegraphDuration);
            if (sanity != null) sanity.ApplyDelta(-config.SanityTelegraphHit);

            Debug.Log($"[ATTACK] {attack.Id} 전조 시작 — 귀퉁이 {corner} (판정까지 {attack.TelegraphDuration:F1}s)");
        }

        private Vector2 PlayerPosition() => player != null ? (Vector2)player.transform.position : Vector2.zero;

        private void StartTelegraph(int corner, float duration)
        {
            _telegraphs.Add(new ActiveTelegraph { Corner = corner, Remaining = duration });
            GameEvents.RaiseAttackTelegraphStarted(corner, duration);
        }

        /// <summary>
        /// 전조는 실시간 진행 (반응 창은 TV 배속과 무관 — §2.2).
        /// ⚠ 만료 항목을 <b>리스트에서 전부 뺀 뒤에</b> 판정한다. Resolve는 오염 → (부적 전소) → GameOver →
        /// GameFlowController.StopMainLoop → Stop() → <c>_telegraphs.Clear()</c>로 되돌아올 수 있어,
        /// 순회 도중 호출하면 인덱스가 깨진다.
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
            GameEvents.RaiseAttackResolved(telegraph.Corner, false);
            salt.Contaminate(telegraph.Corner);

            if (IsTraining)
            {
                // 학습 구간에서도 오염은 실제로 일어난다 — 지울 대상이 있어야 지우는 법을 배운다.
                // 성공 통보는 PrologueDirector가 정화를 감지해 NotifyTrainingCleared로 준다.
                Debug.Log($"[ATTACK] 학습 오염 — 귀퉁이 {telegraph.Corner} (이제 가서 소금을 뿌려라)");
                _onTrainingResolved(telegraph.Corner, false);
                return;
            }
            Debug.Log($"[ATTACK] 귀퉁이 {telegraph.Corner} 판정 — 오염 +1");
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
            sb.Append($"[ATTACK] 스케줄 확정 (seed={seed}, {_schedule.Length}건 + 최후의 연발 {config.TrapWaveCount}발):");
            // 새 설계의 성립 부등식을 실수치로 한 줄 남긴다 — 두 파일에 흩어진 수치의 **관계**가 깨졌는지는
            // 각 값만 봐서는 안 보인다 (v0.6.1의 3.0s/3.0s 사고가 정확히 그랬다).
            sb.Append($"\n  [타이밍] {SaltTimingModel.Describe(config.SaltHoldSec, config.MoveSpeed, config.TalismanTotalSec, _schedule.Length + config.TrapWaveCount)}");
            for (int i = 0; i < _schedule.Length; i++)
            {
                ScheduledAttack a = _schedule[i];
                sb.Append($"\n  {a.Id} @{a.PhaseId}+{a.TriggerTime:F1}s corner=");
                sb.Append(a.TargetRule == AttackTargetRule.FarthestFromPlayer ? "최원거리(발동 시)" : a.Corner.ToString());
            }
            Debug.Log(sb.ToString());
        }
    }
}
