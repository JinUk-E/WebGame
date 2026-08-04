namespace Morae.Game.Data
{
    // architecture.md §1.4 — 상태 머신 enum 전부 (명세 §5)

    /// <summary>게임 흐름 상태. GameFlowController가 소유.</summary>
    public enum GameState { Title, Prologue, MainLoop, Ending, GameOver }

    /// <summary>본편 페이즈 (명세 §1 — 420초 배분표).</summary>
    public enum PhaseId { P1, P2, P3, P4, P5, P6, P7 }

    /// <summary>플레이어 상태 머신 (명세 §5).</summary>
    public enum PlayerState
    {
        Idle,
        Move,
        Praying,          // 3s 채널 + 방향 지정
        WatchingTV,
        InBlanket,
        ListeningAtDoor,
        UsingJar,         // 5s 무방비
        OpeningDoor,      // 1.5s 홀드
        Dead,
        Escaped,
    }

    /// <summary>귀퉁이 소금 단계. 전이는 AttackResolved(미상쇄) +1, 기도 −1만.</summary>
    public enum CornerStage { White = 0, Gray = 1, Black = 2 }

    /// <summary>문 상태. Open 시점에 TrueSignalStarted 발화 여부로 사망/엔딩 분기.</summary>
    public enum DoorState { Latched, Opening, Open }

    /// <summary>게임오버 사유 3종 (명세 §0).</summary>
    public enum GameOverReason { OpenedDoor, SealCollapsed, Panic }

    /// <summary>엔딩 3종 — Perfect(부적 미소모)/Survived(부적 소모)/Rescued(07:40 K씨 개문).</summary>
    public enum EndingKind { Perfect, Survived, Rescued }

    /// <summary>사운드 리슨 상태 3종 (architecture §8.1 — SoundRouter 볼륨 테이블 행).</summary>
    public enum ListenState { Normal, InBlanket, ListeningAtDoor }

    /// <summary>시계 오염 방식 (architecture §2.1).</summary>
    public enum ClockMode
    {
        Sync,    // 표시 = 진실 시각
        Frozen,  // 진실 시각으로 진행하다 (페이즈 종료 시각 + clockParamMin)에서 정지 (-5 = 5분 전 값에서 멈춤)
        Offset,  // 표시 = 진실 + clockParamMin (+40 / −30)
        Fixed,   // 표시 = clockParamMin 고정 (07:25 = 445)
    }

    /// <summary>공격 대상 귀퉁이 선정 규칙 (architecture §2.2).</summary>
    public enum AttackTargetRule { RandomCorner, FarthestFromPlayer }

    /// <summary>EventTable 이벤트 종류 (architecture §2.3).</summary>
    public enum GameEventKind { FakeVoice, TrueSignal, Scare, Scripted, Hint }

    /// <summary>오디오 라우팅 채널 (architecture §2.3 — SoundRouter가 소비).</summary>
    public enum AudioChannel { Door, Window, Room, Phone, Corner }

    /// <summary>상호작용 E 문법 (명세 §3).</summary>
    public enum InteractionKind
    {
        Tap,           // E 탭 즉시 (TV 토글, 이불 진입/이탈)
        HoldMaintain,  // E 누르는 동안 효과 유지, 떼면 종료 (귀 대기)
        HoldComplete,  // E를 Duration 동안 유지하면 완료, 조기 해제 = 취소 (기도 3s, 걸쇠 1.5s)
        ChannelLocked, // 시작 후 Duration 동안 잠금, 취소 불가 (요강 5s)
    }

    /// <summary>부적이 가로채는 게임오버 트리거 2종 (명세 §2 — 트리거된 쪽의 복구를 적용).</summary>
    public enum TalismanTrigger { SealCollapse, Panic }

    /// <summary>귀퉁이 인덱스 규약: 0=좌상(NW), 1=우상(NE), 2=좌하(SW), 3=우하(SE).</summary>
    public static class CornerIndex
    {
        public const int TopLeft = 0;
        public const int TopRight = 1;
        public const int BottomLeft = 2;
        public const int BottomRight = 3;
        public const int Count = 4;
        public const int None = -1;
    }
}
