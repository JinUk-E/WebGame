namespace Morae.Game.Data
{
    // architecture.md §1.4 — 상태 머신 enum 전부 (명세 §5)

    /// <summary>게임 흐름 상태. GameFlowController가 소유.</summary>
    public enum GameState { Title, Prologue, MainLoop, Ending, GameOver }

    /// <summary>본편 페이즈 (명세 v0.3 — 8페이즈 420초: 시동/교란/본색/소강/절정/최후의 함정/정적/탈출).</summary>
    public enum PhaseId { P1, P2, P3, P4, P5, P6, P7, P8 }

    /// <summary>
    /// 플레이어 상태 머신 (명세 §5).
    /// <para>
    /// [v0.7] <b>Praying → Salting</b>. 조작이 "불상 앞에서 방향을 겨눈다"에서 "더러워진 귀퉁이에 가서 뿌린다"로
    /// 바뀌었다 — 상태의 자리(행동 중·이동 불가)는 같으므로 <b>enum 순서를 유지</b>한 채 이름만 갈았다.
    /// 요강 삭제로 UsingJar가 빠지면서 뒤 값들의 정수 인덱스가 하나씩 당겨지는데, PlayerState는
    /// 런타임 전용이라(직렬화 필드 없음 — PlayerController가 소유하고 뷰는 매 프레임 읽는다) 안전하다.
    /// </para>
    /// </summary>
    public enum PlayerState
    {
        Idle,
        Move,
        Salting,          // BalanceConfig.SaltHoldSec 홀드 — 이동 불가, 이성 초당 감소
        WatchingTV,
        InBlanket,
        ListeningAtDoor,
        OpeningDoor,      // 1.5s 홀드
        Dead,
        Escaped,
    }

    /// <summary>
    /// 귀퉁이 소금 단계. 오염 +1(공격 판정 미대응), 정화 −1(소금 뿌리기 완료).
    /// <para>
    /// [v0.7] DeepBlack(3) 제거 — 흑화 심화의 <b>유일한 게임플레이 효과</b>가 기도 채널 ×1.5였고,
    /// 기도가 사라지면서 아무것도 하지 않는 플래그가 됐다. 표현 계층의 stage 3 갈래도 함께 정리한다.
    /// </para>
    /// </summary>
    public enum CornerStage { White = 0, Gray = 1, Black = 2 }

    /// <summary>문 상태. Open 시점에 TrueSignalStarted 발화 여부로 사망/엔딩 분기.</summary>
    public enum DoorState { Latched, Opening, Open }

    /// <summary>
    /// 게임오버 사유 3종.
    /// <para>
    /// [v0.7] <b>SealCollapsed의 의미가 바뀌었다</b> — 기존 "네 귀퉁이 전부 흑" 붕괴 판정은 삭제됐고
    /// (부적 소진이 항상 먼저 터져서 도달 불가능한 죽은 축이었다), 이제 <b>부적이 다 탄 것</b>을 뜻한다.
    /// enum 값을 지우지 않고 재정의한 이유: GameOverScreenView의 사유별 문구가 씬에 배열로 직렬화돼 있어
    /// 값을 빼면 인덱스가 밀린다. Main.unity는 수동 배선이라 건드리지 않는다.
    /// </para>
    /// </summary>
    public enum GameOverReason { OpenedDoor, SealCollapsed, Panic }

    /// <summary>
    /// 엔딩 3종 — Perfect/Survived는 <b>부적 잔여 시간</b>으로 가른다 (BalanceConfig.EndingPerfectRemainSec).
    /// Rescued는 07:40 K씨 개문.
    /// </summary>
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

    // [v0.7] InteractionKind(Tap/HoldMaintain/HoldComplete/ChannelLocked) 폐기.
    //   조작 축을 하나로 줄이는 것이 이번 개편의 최우선 목표라, "대상에 따라 E의 문법이 달라진다"는
    //   구조 자체를 없앴다. 남은 차이는 Interactable의 프로퍼티 두 개(CompleteOnRelease/Cancelable)로 표현하고,
    //   Duration == 0이면 시작 다음 틱에 완료되므로 옛 Tap은 홀드의 특수해로 흡수된다.
    // [v0.7] TalismanTrigger 폐기 — 부적이 게임오버를 가로채는 개념이 사라졌다(이제 타이머다).

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
