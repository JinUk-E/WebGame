using Morae.Game.Data;
using Morae.Game.Gauges;
using Morae.Game.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Morae.Game.Core
{
    /// <summary>
    /// GameState 전환 (architecture §3.2). Title/Prologue/MainLoop/Ending/GameOver 소유.
    /// 아래 방향 제어는 SerializeField 직접 참조(§1.2) — 시퀀서·스케줄러·게이지의 시작/정지.
    /// 재시작 = 씬 리로드 + SessionContext(시드·프롤로그 스킵)만 생존 — 새 시드가 곧 지터 변주.
    /// TrueSignalFired: P7 진짜 신호 발화 여부 (게임 흐름 상태 — DoorInteractable의 개문 분기 기준).
    /// </summary>
    public sealed class GameFlowController : MonoBehaviour
    {
        [SerializeField] private BalanceConfig config;
        [SerializeField] private PhaseSequencer phaseSequencer;
        [SerializeField] private AttackScheduler attackScheduler;
        [SerializeField] private Sanity sanity;
        [SerializeField] private PlayerController player;
        [SerializeField] private PrologueDirector prologueDirector;
        [SerializeField] private TitleScreenView titleScreen;

        public GameState State { get; private set; } = GameState.Title;
        /// <summary>P7 진짜 신호 발화 여부 — 개문 시 사망/엔딩 분기 기준 (§1.4 DoorState).</summary>
        public bool TrueSignalFired { get; private set; }

        /// <summary>
        /// 프롤로그 <b>대사 구간</b>이 입력을 쥐고 있는가 (2026-08-06 수동 진행).
        /// 대사를 넘기는 클릭·탭·E가 월드 상호작용으로 새지 않도록 PlayerInteraction이 이걸 보고 잠근다.
        /// 대사가 끝난 뒤의 <b>강제 학습 구간에서는 false</b> — 그때 E·터치 버튼은 기도의 것이어야 한다.
        /// </summary>
        public bool PrologueDialogueLock =>
            State == GameState.Prologue && prologueDirector != null && prologueDirector.OwnsInput;

        private void OnEnable()
        {
            GameEvents.GameOver += HandleGameOver;
            GameEvents.EndingStarted += HandleEndingStarted;
            GameEvents.TrueSignalStarted += HandleTrueSignalStarted;
        }

        private void OnDisable()
        {
            GameEvents.GameOver -= HandleGameOver;
            GameEvents.EndingStarted -= HandleEndingStarted;
            GameEvents.TrueSignalStarted -= HandleTrueSignalStarted;
        }

        private void Start()
        {
            SessionContext.EnsureInitialized();
            Debug.Log($"[FLOW] 세션 시작 — seed={SessionContext.Seed}, skipPrologue={SessionContext.SkipPrologue}");
            // 항상 타이틀부터 — 게임 시작 버튼으로만 진입 (2026-08-04 타이틀 개편. 오디오 게이트 §8.2 겸용)
            if (titleScreen != null)
            {
                titleScreen.Show(BeginFromTitle);
            }
            else
            {
                BeginFromTitle();
            }
        }

        /// <summary>타이틀 게이트 통과 시 호출 (지금은 Start에서 즉시 — TitleScreen 도입 시 클릭 콜백으로 이동).</summary>
        public void BeginFromTitle()
        {
            if (State != GameState.Title) return;
            bool skip = SessionContext.SkipPrologue && (config == null || config.PrologueSkipAvailable);
            if (skip)
            {
                EnterMainLoop();
            }
            else
            {
                EnterPrologue();
            }
        }

        private void EnterPrologue()
        {
            SetState(GameState.Prologue);
            if (prologueDirector != null)
            {
                prologueDirector.Play(EnterMainLoop); // 완료(걸쇠 잠금) 콜백 = 본편 진입
            }
            else
            {
                EnterMainLoop();
            }
        }

        private void EnterMainLoop()
        {
            SetState(GameState.MainLoop);
            TrueSignalFired = false;
            phaseSequencer.Begin(); // 시퀀서 먼저 — 스케줄러·게이지가 페이즈를 읽는 쪽 (§4 의존 방향)
            if (attackScheduler != null) attackScheduler.Begin(SessionContext.Seed);
            if (sanity != null) sanity.Begin();
        }

        private void HandleTrueSignalStarted()
        {
            TrueSignalFired = true;
            Debug.Log("[FLOW] 진짜 신호 발화 — 이제 개문 = 엔딩");
        }

        private void HandleGameOver(GameOverReason reason)
        {
            if (State != GameState.MainLoop) return;
            SetState(GameState.GameOver);
            StopMainLoop();
            SessionContext.MarkEnded(); // 첫 엔딩 기록 — 타이틀 스킵 토글 노출
            if (player != null) player.EnterTerminalState(PlayerState.Dead);
            Debug.Log($"[FLOW] 게임오버: {reason} — E로 타이틀 복귀");
        }

        private void HandleEndingStarted(EndingKind kind)
        {
            if (State != GameState.MainLoop) return;
            SetState(GameState.Ending);
            StopMainLoop();
            SessionContext.MarkEnded();
            if (player != null) player.EnterTerminalState(PlayerState.Escaped);
            Debug.Log($"[FLOW] 엔딩: {kind} — E로 타이틀 복귀");
        }

        private void StopMainLoop()
        {
            phaseSequencer.StopSequence();
            if (attackScheduler != null) attackScheduler.Stop();
            if (sanity != null) sanity.Stop();
        }

        private void Update()
        {
            // 골격: 게임오버/엔딩에서 E = 재시작 (GameOverScreen/EndingScreen UI는 Epic 2에서 이 위에 얹힌다)
            if ((State == GameState.GameOver || State == GameState.Ending) && InputReader.InteractDown)
            {
                Restart();
                return;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 개발용: 본편 중 Esc = 즉시 재시작 (릴리스에서는 Esc를 일시정지 등으로 재배정 예정)
            if (State == GameState.MainLoop && InputReader.EscapeDown)
            {
                Debug.Log("[FLOW] 디버그 재시작 (Esc)");
                Restart();
            }
#endif
        }

        /// <summary>재시작 = 씬 리로드 (§3.2 — 손 리셋 금지). 새 시드(지터 변주), 리로드 후 타이틀부터.</summary>
        public void Restart()
        {
            SessionContext.PrepareRestart();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void SetState(GameState next)
        {
            if (State == next) return;
            State = next;
            Debug.Log($"[FLOW] GameState → {next}");
        }
    }
}
