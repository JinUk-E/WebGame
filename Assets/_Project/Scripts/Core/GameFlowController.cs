using Morae.Game.Data;
using Morae.Game.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Morae.Game.Core
{
    /// <summary>
    /// GameState 전환 골격 (architecture §3.2). Title/Prologue/MainLoop/Ending/GameOver 소유.
    /// 아래 방향 제어는 SerializeField 직접 참조(§1.2) — §4 순서 4~6에서 AttackScheduler·EventDirector 참조 추가 예정.
    /// 재시작 = 씬 리로드 + SessionContext(시드·프롤로그 스킵)만 생존.
    /// </summary>
    public sealed class GameFlowController : MonoBehaviour
    {
        [SerializeField] private BalanceConfig config;
        [SerializeField] private PhaseSequencer phaseSequencer;

        public GameState State { get; private set; } = GameState.Title;

        private void OnEnable()
        {
            GameEvents.GameOver += HandleGameOver;
            GameEvents.EndingStarted += HandleEndingStarted;
        }

        private void OnDisable()
        {
            GameEvents.GameOver -= HandleGameOver;
            GameEvents.EndingStarted -= HandleEndingStarted;
        }

        private void Start()
        {
            SessionContext.EnsureInitialized();
            Debug.Log($"[FLOW] 세션 시작 — seed={SessionContext.Seed}, skipPrologue={SessionContext.SkipPrologue}");
            // TitleScreen(오디오 게이트 — §8.2, Epic 2)이 생기면 이 호출은 타이틀 클릭 콜백으로 옮긴다.
            BeginFromTitle();
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
            // PrologueDirector(Epic 2) 완료(걸쇠 잠금) 콜백이 EnterMainLoop()를 호출할 예정 — 지금은 즉시 본편 진입
            EnterMainLoop();
        }

        private void EnterMainLoop()
        {
            SetState(GameState.MainLoop);
            phaseSequencer.Begin();
        }

        private void HandleGameOver(GameOverReason reason)
        {
            if (State != GameState.MainLoop) return;
            SetState(GameState.GameOver);
            phaseSequencer.StopSequence();
            Debug.Log($"[FLOW] 게임오버: {reason} — E로 재시작");
        }

        private void HandleEndingStarted(EndingKind kind)
        {
            if (State != GameState.MainLoop) return;
            SetState(GameState.Ending);
            phaseSequencer.StopSequence();
            Debug.Log($"[FLOW] 엔딩: {kind}");
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

        /// <summary>재시작 = 씬 리로드 (§3.2 — 손 리셋 금지). 프롤로그 스킵 + 새 시드.</summary>
        public void Restart()
        {
            SessionContext.PrepareRestart(config == null || config.PrologueSkipAvailable);
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
