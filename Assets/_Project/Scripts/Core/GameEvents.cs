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
        // [v0.7] 이산 손실 순간 — 크기(정규화)를 함께 보낸다.
        // SanityChanged만으로는 손실이 보이지 않는다: 소금 뿌리기 −3, 전조 −8은 100점 만점에서 각각 3%·8%인데
        // 비네트 전 구간 폭이 0.33이라 화면에서 0.01~0.026밖에 안 움직인다(육안 한계 이하).
        // 값 추종은 "지금 상태"를 말하고, 이 이벤트는 "방금 잃었다"를 말한다 — 둘은 다른 일이다.
        public static event Action<float /*lost01*/> SanityLost;
        // [v0.7] 부적이 1회 방어에서 **60초 비회복 타이머**로 바뀌면서 무인자 1회성 이벤트로는 표현이 안 된다.
        // burn01 = 탄 정도 0(멀쩡)~1(전소). 값이 바뀔 때만 발행 — 매 프레임이 아니다(뷰가 4~8단계로 양자화한다).
        public static event Action<float /*burn01*/> TalismanBurnChanged;
        public static event Action<EventDef> GameEventFired;                        // EventTable 발화 (자막·SFX·연출)
        public static event Action TrueSignalStarted;                               // P7 진짜 신호
        public static event Action<PlayerState> PlayerStateChanged;
        public static event Action<bool> TVToggled;
        public static event Action<GameOverReason> GameOver;                        // OpenedDoor / SealCollapsed / Panic
        public static event Action<EndingKind> EndingStarted;                       // Perfect / Survived / Rescued(07:40)
        // v1.3 추가 (2026-08-04 문 입력 규칙 확정에 따름 — _shared.md 기록): 걸쇠 개방 진행률 0~1. 취소 시 0 발행
        public static event Action<float /*0~1*/> DoorLatchProgressChanged;
        // [v0.7] PrayerChannelChanged(기도 조준) 폐기 → SaltChannelChanged로 대체.
        //   조준 인자가 사라진 대신 **어느 귀퉁이를 뿌리는 중인지**가 들어간다 (진행 바를 그 귀퉁이에 띄운다).
        //   진행 중 매 틱, 종료(완료·취소) 시 (corner, 0) 발행 — 옛 규약 그대로.
        //   ⚠ 심박·비네트도 이걸 구독한다. 이성 초당 감소는 SanityChanged로는 체감되지 않는다
        //   (전 구간 폭 0.33에 초당 델타 0.03 → 비네트 0.0099/s로 육안 한계 이하). "지금 뿌리는 중"이라는
        //   **상태 자체**가 표현 계층에 도달해야 가산 오프셋을 얹을 수 있다.
        public static event Action<int /*corner*/, float /*0~1*/> SaltChannelChanged;
        // v1.5 (채널 진행 바 일반화): 이불 이탈 진행률. 발행 규약은 걸쇠와 동일 — 진행 중 매 틱, 종료 시 0
        // [v0.7] JarChannelChanged는 요강 상호작용 삭제와 함께 폐기 (요강 오브젝트는 소품으로 존치)
        public static event Action<float /*0~1*/> BlanketExitChanged;
        // v0.6 추가 (2026-08-06 첫인상 개선): "이 귀퉁이를 봐라" 주의 유도. 오염·전조와 무관한 순수 연출 채널이라
        // 색 문법도 분리한다 (흰 섬광 — 붉은 전조와 겹치면 대응해야 하는 것으로 오인된다).
        // 학습 구간에서 "막으러 갈 곳"을 화면이 직접 가리키는 것도 이 채널이다.
        // [v0.7] AltarAttentionRequested(불상판) 폐기 — 학습 대상이 불상에서 소금 귀퉁이로 옮겨가면서
        //   SaltAttentionRequested 하나로 합쳐졌다. 가리킬 곳이 한 종류뿐이면 채널도 하나여야 한다.
        public static event Action<int /*corner*/, float /*seconds*/> SaltAttentionRequested;
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
        public static void RaiseSanityLost(float lost01) => SanityLost?.Invoke(lost01);
        public static void RaiseTalismanBurnChanged(float burn01) => TalismanBurnChanged?.Invoke(burn01);
        public static void RaiseGameEventFired(EventDef def) => GameEventFired?.Invoke(def);
        public static void RaiseTrueSignalStarted() => TrueSignalStarted?.Invoke();
        public static void RaisePlayerStateChanged(PlayerState state) => PlayerStateChanged?.Invoke(state);
        public static void RaiseTVToggled(bool isOn) => TVToggled?.Invoke(isOn);
        public static void RaiseGameOver(GameOverReason reason) => GameOver?.Invoke(reason);
        public static void RaiseEndingStarted(EndingKind kind) => EndingStarted?.Invoke(kind);
        public static void RaiseDoorLatchProgressChanged(float progress01) => DoorLatchProgressChanged?.Invoke(progress01);
        public static void RaiseSaltChannelChanged(int corner, float progress01)
            => SaltChannelChanged?.Invoke(corner, progress01);
        public static void RaiseBlanketExitChanged(float progress01) => BlanketExitChanged?.Invoke(progress01);
        public static void RaiseSaltAttentionRequested(int corner, float seconds)
            => SaltAttentionRequested?.Invoke(corner, seconds);
        public static void RaiseTrainingModeChanged(bool active) => TrainingModeChanged?.Invoke(active);
        public static void RaiseDoorOpened() => DoorOpened?.Invoke();
    }
}
