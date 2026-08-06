using UnityEngine;

namespace Morae.Game.Data
{
    /// <summary>
    /// 튜닝 값 전부 한 곳 (architecture §2.4 — 명세 §2·§3 수치).
    /// 런타임 읽기 전용. 필드 초기값 = 명세 시작값 (에셋 생성 시 그대로 직렬화, 이후 튜닝은 에셋에서).
    /// 크기 값은 전부 양수로 보관 — 부호(회복/감소)는 사용처가 적용한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Morae/Balance Config", fileName = "BalanceConfig")]
    public sealed class BalanceConfig : ScriptableObject
    {
        [Header("이동")]
        [SerializeField] private float moveSpeed = 3.5f;               // 유닛/s — 명세 미지정, 튜닝 시작값 (방 내부 13.2×8.2유닛 기준)

        [Header("이성 (명세 §2 — 0~100)")]
        [SerializeField] private float sanityMax = 100f;
        [SerializeField] private float sanityTvRegenPerSec = 1f;       // TV 켜짐 +1/s
        [SerializeField] private float sanityBlanketRegenPerSec = 3f;  // 이불 속 +3/s
        [SerializeField] private float sanityDoorDrainPerSec = 3f;     // 귀 대기 −3/s
        [SerializeField] private float sanityTelegraphHit = 8f;        // 공격 전조 발생 −8

        [Header("상호작용 (명세 §3)")]
        [SerializeField] private float prayerChannelSec = 3f;          // 기도 채널
        [SerializeField] private float doorOpenHoldSec = 1.5f;         // 걸쇠 열기 홀드
        [SerializeField] private float jarLockSec = 5f;                // 요강 무방비
        [SerializeField] private float blanketExitSec = 1f;            // 이불에서 나오는 시간

        [Header("공격·TV")]
        [SerializeField] private float tvAttackClockRate = 1.333333f;  // 공격 간격 ×0.75 = 페이즈 로컬 공격 시계 배속 (§2.2. v0.3 유보: 부족 시 1.5)

        [Header("최후의 함정 (명세 v0.3 — P6 시퀀스, 실시간 기준·TV 배속 무관)")]
        [SerializeField] private float trapVoiceLeadSec = 9f;          // 가짜 목소리 ② 발화 구간 (P6 진입~대사 종료)
        [SerializeField] private float trapQuietSec = 5f;              // 완전 무공격 정적 (소금 전조 금지 — 고민 구간)
        [SerializeField] private float trapTelegraphSec = 3f;          // 함정 웨이브 전조 길이
        [SerializeField] private float trapWaveGapSec = 5f;            // 웨이브 판정 → 다음 웨이브 전조 시작 간격
        [SerializeField] private int trapWaveCount = 2;                // 4귀퉁이 동시 공격 횟수

        [Header("흑화 심화 (명세 v0.3 — 흑 귀퉁이 추가 피격 시 1회 플래그)")]
        [SerializeField] private float prayerDeepenedMultiplier = 1.5f; // 심화 귀퉁이 기도 채널 배율 (3s → 4.5s)

        // ---- 명세 v0.5 §1 — 흑화 귀퉁이당 즉시 대가 (흑 개수 n에 선형 누적) ----
        // "벌이 4개 붕괴 시점에 한 번에 오므로 하나 버렸다가 아무 느낌이 없다"를 깨는 장치.
        // ⚠ n=4는 구조상 지속 불가 — 네 번째 흑이 생기는 순간 봉인 붕괴 판정(부적 또는 게임오버)이 나므로
        //    실제로 유지되는 최대는 n=3이다. 밸런스는 그 전제로 잡혀 있다.
        [Header("흑화 대가 (명세 v0.5 §1)")]
        // 실내 전역광 감광(코너 1개당). 명세 초안 −0.18은 조도 스케일 1.0 기준값이고,
        // 이 프로젝트의 전역광은 base 0.12 + 여명 0~0.18 스케일이라 같은 비율(n=4에서 기준 조도의 60% 소실)로 환산했다.
        [SerializeField] private float blackCornerLightPenalty = 0.018f;
        // 감광 바닥 — 플레이어 실루엣·소품 윤곽이 남는 하한 (기존 P6 최암부 0.074보다 약간 아래).
        [SerializeField] private float minRoomLight = 0.055f;
        [SerializeField] private float roomLightSmoothSec = 0.3f;        // 감광 전환 러프 (단차로 튀지 않게)
        [SerializeField] private float blackCornerSanityDrainPerSec = 0.15f; // 이성 −0.15/s × n (페이즈 드레인과 별도 누적)
        [SerializeField] private float blackCornerAttackIntervalReduction = 0.05f; // 공격 간격 ×(1 − 0.05n)
        [SerializeField] private float minAttackIntervalScale = 0.6f;    // 간격 배수 하한 (계수를 올려도 붕괴하지 않게)
        // 귀퉁이 속삭임 볼륨 — 단계별(0 백 /1 회 /2 흑 /3 흑+심화). 어느 쪽이 뚫렸는지 방향으로 상시 들린다.
        [SerializeField] private float[] cornerWhisperVolumes = { 0f, 0.14f, 0.42f, 0.6f };

        [Header("어둠 속 실루엣 (명세 v0.5 §2 — 분위기 전용, 피해·상호작용 없음)")]
        [SerializeField] private float silhouetteBaseIntervalSec = 7f;   // 흑 1개일 때 출현 간격
        [SerializeField] private float silhouetteIntervalGain = 0.55f;   // n이 늘수록 짧아지는 정도
        [SerializeField] private float silhouetteMinIntervalSec = 2.2f;
        [SerializeField] private int silhouetteMaxConcurrent = 3;        // 동시 상한 (가독성)
        [SerializeField] private float silhouetteClearance = 2.2f;       // 플레이어·불상·전조 귀퉁이 회피 반경

        [Header("프롤로그 대사 (수동 진행 — 클릭/탭/E)")]
        // 한 줄이 화면에 최소한 머무는 시간. 연타(또는 눌린 채 넘어온 손가락)로 대사가 통째로 날아가지 않게 하는
        // 유일한 시간 조건이다. 이보다 크게 올리면 "안 넘어간다"는 답답함이 되므로 짧게 유지할 것.
        [SerializeField] private float prologueLineMinShowSec = 0.3f;

        [Header("프롤로그 강제 학습 (명세 v0.5 §3 — 실패해도 사망하지 않는 안전 구간)")]
        [SerializeField] private float prologueWarningSec = 6f;          // 경고 대사 → 전조까지
        [SerializeField] private float prologueTelegraphTravelSec = 11f; // 전조 길이 = 기도 채널 + 이 이동 여유
        [SerializeField] private float prologueRetryGapSec = 3.5f;       // 실패 후 다시 전조가 뜨기까지
        // 시도 상한 — 도달하면 자비 통과. 소프트락 방지가 학습보다 우선이다 (조준을 못 찾는 플레이어를 가두지 않는다).
        [SerializeField] private int prologueMaxAttempts = 3;

        [Header("부적 (명세 §2 — 1회 방어)")]
        [SerializeField] private int talismanSaltRestore = 1;          // 전 귀퉁이 −1
        [SerializeField] private float talismanSanityRestore = 30f;    // 이성 +30
        [SerializeField] private float talismanFxSec = 3f;             // 발동 연출(부적이 검게 탐)

        [Header("게임 흐름")]
        [SerializeField] private float rescueAutoOpenDelaySec = 60f;   // 진짜 신호 → K씨 개문 (초안 60)
        // v0.4 — 진짜 신호를 듣고도 문을 열지 않는 동안 이성 추가 하락.
        // "끝까지 안 열면 안전"을 깨서 판별 축에 실질 위험을 부여한다 (무응답 60s = 총 −120 상당).
        [SerializeField] private float trueSignalIgnoreDrainPerSec = 2f;
        [SerializeField] private bool prologueSkipAvailable = true;

        public float MoveSpeed => moveSpeed;
        public float SanityMax => sanityMax;
        public float SanityTvRegenPerSec => sanityTvRegenPerSec;
        public float SanityBlanketRegenPerSec => sanityBlanketRegenPerSec;
        public float SanityDoorDrainPerSec => sanityDoorDrainPerSec;
        public float SanityTelegraphHit => sanityTelegraphHit;
        public float PrayerChannelSec => prayerChannelSec;
        public float DoorOpenHoldSec => doorOpenHoldSec;
        public float JarLockSec => jarLockSec;
        public float BlanketExitSec => blanketExitSec;
        public float TvAttackClockRate => tvAttackClockRate;
        public float TrapVoiceLeadSec => trapVoiceLeadSec;
        public float TrapQuietSec => trapQuietSec;
        public float TrapTelegraphSec => trapTelegraphSec;
        public float TrapWaveGapSec => trapWaveGapSec;
        public int TrapWaveCount => trapWaveCount;
        public float PrayerDeepenedMultiplier => prayerDeepenedMultiplier;
        public float BlackCornerLightPenalty => blackCornerLightPenalty;
        public float MinRoomLight => minRoomLight;
        public float RoomLightSmoothSec => roomLightSmoothSec;
        public float BlackCornerSanityDrainPerSec => blackCornerSanityDrainPerSec;
        public float BlackCornerAttackIntervalReduction => blackCornerAttackIntervalReduction;
        public float MinAttackIntervalScale => minAttackIntervalScale;
        /// <summary>단계별 귀퉁이 속삭임 볼륨 — 배열 자체를 넘기지 않는다(SO는 런타임 읽기 전용, 원소를 쓰면 에셋이 영구 오염된다).</summary>
        public float GetCornerWhisperVolume(int stage) => Core.CornerPenaltyModel.WhisperVolume(stage, cornerWhisperVolumes);
        public float SilhouetteBaseIntervalSec => silhouetteBaseIntervalSec;
        public float SilhouetteIntervalGain => silhouetteIntervalGain;
        public float SilhouetteMinIntervalSec => silhouetteMinIntervalSec;
        public int SilhouetteMaxConcurrent => silhouetteMaxConcurrent;
        public float SilhouetteClearance => silhouetteClearance;
        public float PrologueLineMinShowSec => prologueLineMinShowSec;
        public float PrologueWarningSec => prologueWarningSec;
        public float PrologueTelegraphTravelSec => prologueTelegraphTravelSec;
        public float PrologueRetryGapSec => prologueRetryGapSec;
        public int PrologueMaxAttempts => prologueMaxAttempts;
        public int TalismanSaltRestore => talismanSaltRestore;
        public float TalismanSanityRestore => talismanSanityRestore;
        public float TalismanFxSec => talismanFxSec;
        public float RescueAutoOpenDelaySec => rescueAutoOpenDelaySec;
        public float TrueSignalIgnoreDrainPerSec => trueSignalIgnoreDrainPerSec;
        public bool PrologueSkipAvailable => prologueSkipAvailable;
    }
}
