using System;
using Morae.Game.Data;

namespace Morae.Game.Core
{
    /// <summary>
    /// 정적 이벤트 허브 (architecture §1.3 — 시그니처 동결. 이것이 모듈 간 API 전부).
    /// 게임플레이 모듈이 Raise*를 호출해 발행하고, 표현 계층(조명/사운드/자막/피드백/UI)은 구독만 한다.
    /// 게임플레이 코드는 표현 모듈을 직접 참조하지 않는다 (§1.2).
    /// 재시작 = 씬 리로드이므로 구독/해제는 반드시 OnEnable/OnDisable 쌍 — 해제 누락은 곧 누수·유령 콜백.
    /// </summary>
    public static class GameEvents
    {
        public static event Action<PhaseId> PhaseChanged;
        public static event Action<int /*corner*/, float> AttackTelegraphStarted;   // 전조 시작 (기본 3s)
        public static event Action<int, bool /*countered*/> AttackResolved;         // 판정: 상쇄 여부
        public static event Action<int, int /*stage 0~2*/> CornerStageChanged;      // 기도 정화·오염 공통
        public static event Action<float /*0~1*/> SanityChanged;
        public static event Action TalismanBurned;
        public static event Action<EventDef> GameEventFired;                        // EventTable 발화 (자막·SFX·연출)
        public static event Action TrueSignalStarted;                               // P7 진짜 신호
        public static event Action<PlayerState> PlayerStateChanged;
        public static event Action<bool> TVToggled;
        public static event Action<GameOverReason> GameOver;                        // OpenedDoor / SealCollapsed / Panic
        public static event Action<EndingKind> EndingStarted;                       // Perfect / Survived / Rescued(07:40)
        // v1.3 추가 (2026-08-04 문 입력 규칙 확정에 따름 — _shared.md 기록): 걸쇠 개방 진행률 0~1. 취소 시 0 발행
        public static event Action<float /*0~1*/> DoorLatchProgressChanged;
        // v1.4 추가 (2026-08-04 기도 시각 피드백 — _shared.md 기록 필요): 채널 진행률 0~1 + 조준 귀퉁이(-1=미지정).
        // 채널 중 매 틱 발행, 종료(완료·취소) 시 (0, -1) 발행
        public static event Action<float /*0~1*/, int /*aimedCorner*/> PrayerChannelChanged;
        // v1.5 추가 (2026-08-04 채널 진행 바 일반화 — _shared.md 기록 필요): 요강 5s·이불 이탈 1s 진행률.
        // 발행 규약은 걸쇠·기도와 동일 — 진행 중 매 틱, 종료 시 0
        public static event Action<float /*0~1*/> JarChannelChanged;
        public static event Action<float /*0~1*/> BlanketExitChanged;
        // v1.6 추가 (2026-08-04 심장 UI — _shared.md 기록 필요): 요의 발생/해소. 회복 무효 상태의 유일한 상시 표시
        public static event Action<bool> UrgeChanged;
        // v0.6 추가 (2026-08-06 첫인상 개선): "이 귀퉁이를 봐라" 주의 유도. 오염·전조와 무관한 순수 연출 채널이라
        // 색 문법도 분리한다 (흰 섬광 — 붉은 전조와 겹치면 대응해야 하는 것으로 오인된다).
        public static event Action<int /*corner*/, float /*seconds*/> SaltAttentionRequested;
        // 같은 목적의 불상판 — 학습 구간에서 "막으러 갈 곳"을 화면이 직접 가리킨다.
        public static event Action<float /*seconds*/> AltarAttentionRequested;
        // 학습(프롤로그) 구간 진입/이탈 — 표현 계층이 "진짜 실패"와 "연습 실패"를 구분해야 할 때 쓴다.
        // 예) 팔척님의 웃음은 할아버지가 붙잡고 있는 연습 구간에서 나면 안 된다.
        public static event Action<bool> TrainingModeChanged;
        // 문이 실제로 열린 순간 (걸쇠 개방 완료). 엔딩/게임오버보다 **먼저** 발행해 문이 열리는 그림이 먼저 보이게 한다 —
        // 결과 화면이 먼저 덮이면 "왜 죽었는지"가 안 남는다.
        public static event Action DoorOpened;

        // ---- 발행 (게임플레이 모듈 전용) ----

        public static void RaisePhaseChanged(PhaseId phase) => PhaseChanged?.Invoke(phase);
        public static void RaiseAttackTelegraphStarted(int corner, float telegraphDuration)
            => AttackTelegraphStarted?.Invoke(corner, telegraphDuration);
        public static void RaiseAttackResolved(int corner, bool countered) => AttackResolved?.Invoke(corner, countered);
        public static void RaiseCornerStageChanged(int corner, int stage) => CornerStageChanged?.Invoke(corner, stage);
        public static void RaiseSanityChanged(float normalized01) => SanityChanged?.Invoke(normalized01);
        public static void RaiseTalismanBurned() => TalismanBurned?.Invoke();
        public static void RaiseGameEventFired(EventDef def) => GameEventFired?.Invoke(def);
        public static void RaiseTrueSignalStarted() => TrueSignalStarted?.Invoke();
        public static void RaisePlayerStateChanged(PlayerState state) => PlayerStateChanged?.Invoke(state);
        public static void RaiseTVToggled(bool isOn) => TVToggled?.Invoke(isOn);
        public static void RaiseGameOver(GameOverReason reason) => GameOver?.Invoke(reason);
        public static void RaiseEndingStarted(EndingKind kind) => EndingStarted?.Invoke(kind);
        public static void RaiseDoorLatchProgressChanged(float progress01) => DoorLatchProgressChanged?.Invoke(progress01);
        public static void RaisePrayerChannelChanged(float progress01, int aimedCorner)
            => PrayerChannelChanged?.Invoke(progress01, aimedCorner);
        public static void RaiseJarChannelChanged(float progress01) => JarChannelChanged?.Invoke(progress01);
        public static void RaiseBlanketExitChanged(float progress01) => BlanketExitChanged?.Invoke(progress01);
        public static void RaiseUrgeChanged(bool active) => UrgeChanged?.Invoke(active);
        public static void RaiseSaltAttentionRequested(int corner, float seconds)
            => SaltAttentionRequested?.Invoke(corner, seconds);
        public static void RaiseAltarAttentionRequested(float seconds)
            => AltarAttentionRequested?.Invoke(seconds);
        public static void RaiseTrainingModeChanged(bool active) => TrainingModeChanged?.Invoke(active);
        public static void RaiseDoorOpened() => DoorOpened?.Invoke();
    }
}
