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
        // v0.7 — 이불 회복 상한. **이 값이 없으면 이불이 지배 전략이 된다**:
        //   420초 중 294초를 이불에서 보내면 +882로 총 지출(−404)을 압도해 이성 축이 통째로 사라진다.
        //   소금 드레인을 올려서 막으려면 초당 11이 필요한데(홀드 1회 −16.5 = 전조 피격의 2배) 그건 벌이 아니라 처형이다.
        //   상한을 두면 이불은 "만회 수단"이 아니라 "바닥 유지 수단"이 되고, P6 진입 이성이 최대 75로 고정돼
        //   후반 난이도가 예측 가능해진다.
        [SerializeField] private float sanityBlanketRegenCeiling = 75f;
        // [v0.7] sanityDoorDrainPerSec(귀 대기 −3/s) 제거 — 체감되지 않는 감소였고,
        //   문 앞에 머무는 대가는 이제 "그동안 부적이 탄다"가 대신한다 (Sanity 주석 참조).
        [SerializeField] private float sanityTelegraphHit = 8f;        // 공격 전조 발생 −8
        // v0.7 — 소금 뿌리는 동안 초당 감소. 홀드 1.5s × 2.0 = **1회당 −3.0** (전조 피격 −8의 37.5%).
        //   데스 스파이럴이 아님의 근거: 공격 1건 대응 비용 8 + 3 ≈ 11, 공격 간 평균 35초 중 자리를 비우는
        //   시간이 ~5초라 회복 가용 30초 × 3/s = 90 ≫ 11. 상한 근거: P6에서 d ≥ 4.4면 확정 사망.
        [SerializeField] private float saltSanityDrainPerSec = 2f;

        [Header("상호작용 (v0.7 — E 홀드 단일 문법)")]
        // 소금 뿌리기 홀드. **상한 1.6초**가 계산으로 정해져 있다 — 최악 이동 3.04초(방 임의 지점 → 최원 귀퉁이,
        // L자 우회 반영)에 홀드를 더한 값이 부적 예산 안에 들어와야 한다. 문 홀드(1.5s)와 같은 길이로 맞춘 건
        // "E를 길게 누른다"는 감각을 두 번 가르치지 않기 위해서다.
        [SerializeField] private float saltHoldSec = 1.5f;
        // 손을 뗀 뒤 정화 진행도가 사라지는 속도. 0이면 "조금씩 발라두고 도망"이 최적해가 되어 긴장이 사라지고,
        // 무한대(= 즉시 리셋)면 오조작 1회가 확정 사망이 된다(남은 예산으로 처음부터 다시 할 수 없다).
        // 0.25/s = 4초에 걸쳐 소멸 — 실수는 복구 가능하되 방치는 무의미하다.
        [SerializeField] private float saltProgressDecayPerSec = 0.25f;
        [SerializeField] private float doorOpenHoldSec = 1.5f;         // 걸쇠 열기 홀드
        // v0.7 — 문 귀 대기 진입 지연. **E를 누른 채 이동 입력이 없는 상태가 이만큼 지속되어야** 귀 대기가 시작된다.
        //   없으면: 좌상 소금(-4.5,1.5)으로 ←를 누른 채 달려가다 E를 누르면 문 트리거(x −4.2~−2.0)에 먼저 닿고,
        //   귀 대기 진입 후 **아직 눌려 있는 ←**가 그대로 밀기 판정(Dot > 0.5)을 만족해 1.5초 뒤 즉사한다.
        //   "이동하면서 E 홀드"가 새 조작의 기본 자세라 이건 우발이 아니라 상시 재현이다.
        //   의도적으로 문 앞에 멈춰 선 사람에게는 체감 비용이 0이다.
        [SerializeField] private float doorArmingSec = 0.3f;
        // v0.7: 1.0 → 0.5. 이불 안에서는 부적이 안 보이므로 이탈 지연이 곧 정보 공백이다.
        [SerializeField] private float blanketExitSec = 0.5f;

        [Header("공격·TV")]
        [SerializeField] private float tvAttackClockRate = 1.333333f;  // 공격 간격 ×0.75 = 페이즈 로컬 공격 시계 배속 (§2.2)
        // v0.7 — 같은 페이즈 안에서 연속 공격의 최소 간격. **지터에 하한이 없어서 생긴 구멍을 막는다**:
        //   baseOffset 32와 55가 각각 ×1.2, ×0.8이면 38.4s / 44.0s = 5.6초 간격이 되고,
        //   앞 공격을 정리하기도 전에 다음 오염이 겹쳐 부적이 두 배로 탄다.
        //   12초 = 최악 이동 3.04 + 홀드 1.5에 인지·여유를 얹은 값. 이 하한 위에서만 지터가 변주로 작동한다.
        [SerializeField] private float minAttackGapSec = 12f;

        [Header("최후의 함정 (명세 v0.3 — P6 시퀀스, 실시간 기준·TV 배속 무관)")]
        [SerializeField] private float trapVoiceLeadSec = 9f;          // 가짜 목소리 ② 발화 구간 (P6 진입~대사 종료)
        [SerializeField] private float trapQuietSec = 5f;              // 완전 무공격 정적 (소금 전조 금지 — 고민 구간)
        // 함정 웨이브 전조 길이 — 스케줄 공격의 telegraphDuration과 **같은 값을 유지**한다.
        // 다르면 "전조 길이"라는 학습된 감각이 P6에서만 어긋나 상쇄가 되던 손이 갑자기 안 먹는다.
        [SerializeField] private float trapTelegraphSec = 4.5f;        // (v0.6.1: 3.0 → 4.5)
        [SerializeField] private float trapWaveGapSec = 5f;            // 웨이브 판정 → 다음 웨이브 전조 시작 간격
        [SerializeField] private int trapWaveCount = 2;                // 4귀퉁이 동시 공격 횟수

        // ---- v0.7 흑화 대가 재조정 ----
        // 이성 드레인(0.15/s×n)과 공격 간격 단축(×(1−0.05n))은 **제거했다(0으로 고정)**. 이유가 둘이다:
        //   ① 이중 처벌 — 오염은 이미 부적을 태우는 것으로 벌을 받는다. 같은 것에 두 번 물릴 이유가 없다.
        //   ② 무력화 — 오염 지속이 부적 예산 때문에 사실상 상한이 걸려서 n이 오래 유지되지 않는다.
        //      총 기여가 예산의 7%(−29), 공격 1건당 −1.7로 지각 임계 이하다. 있으나 마나 한 수치는 없는 게 낫다.
        // 감광(blackCornerLightPenalty)만 남긴다 — 이건 자원이 아니라 **피드백**이라 이중 처벌이 아니다.
        [Header("흑화 대가 (v0.7 — 감광만 유지)")]
        // 실내 전역광 감광(코너 1개당). 명세 초안 −0.18은 조도 스케일 1.0 기준값이고,
        // 이 프로젝트의 전역광은 base 0.12 + 여명 0~0.18 스케일이라 같은 비율(n=4에서 기준 조도의 60% 소실)로 환산했다.
        [SerializeField] private float blackCornerLightPenalty = 0.018f;
        // 감광 바닥 — 플레이어 실루엣·소품 윤곽이 남는 하한 (기존 P6 최암부 0.074보다 약간 아래).
        [SerializeField] private float minRoomLight = 0.055f;
        [SerializeField] private float roomLightSmoothSec = 0.3f;        // 감광 전환 러프 (단차로 튀지 않게)
        [SerializeField] private float blackCornerSanityDrainPerSec = 0f;          // v0.7 제거 (위 주석)
        [SerializeField] private float blackCornerAttackIntervalReduction = 0f;    // v0.7 제거 (위 주석)
        [SerializeField] private float minAttackIntervalScale = 0.6f;    // 간격 배수 하한 (계수를 올려도 붕괴하지 않게)
        // 귀퉁이 속삭임 볼륨 — 단계별(0 백 /1 회 /2 흑). v0.7에서 심화 단계가 사라져 4번째 원소를 뺐다.
        // 어느 쪽이 뚫렸는지 방향으로 상시 들린다 — 이불 안에서 부적이 안 보일 때 유일하게 남는 단서이기도 하다.
        [SerializeField] private float[] cornerWhisperVolumes = { 0f, 0.14f, 0.42f };

        // [v0.7] 어둠 속 실루엣(흰 유령) 전량 제거 — SilhouetteDirector·SilhouetteSpawnModel과 함께.
        //   분위기 전용이라 피해도 상호작용도 없었는데, 새 설계에서는 오염이 부적 예산 때문에 오래 유지되지 않아
        //   출현 조건(흑 귀퉁이 n>0)이 "10초짜리 깜빡임"이 됐다. 나올 만하면 사라지는 연출은 분위기가 아니라 노이즈고,
        //   무엇보다 소금·부적이라는 **읽어야 할 신호와 같은 화면에서 시선을 뺏는다**.

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

        [Header("부적 (v0.7 — 비회복 60초 타이머)")]
        // 부적은 더 이상 목숨을 대신 내주지 않는다. **오염된 귀퉁이가 하나라도 있는 동안만** 초당 1씩 타고,
        // 다 타면 게임오버다. 복구해도 되살아나지 않는다 — 이게 핵심이다.
        //
        // 왜 비회복인가: 리셋되는 타이머는 꾸물거림에 대가가 없다(8초를 허비하고 겨우 도착해도 잃은 게 없다).
        // 비회복이면 1초를 늦는 만큼 영구히 사라지고 그게 화면에서 보인다 — "빨리 가야 한다"가 국소적으로 더 세진다.
        // 덤으로 잔여 시간이 그대로 엔딩 등급이 되어 별도 판정 지표가 필요 없다.
        //
        // 왜 60인가 (공격 12건 전제):
        //   회당 더러운 시간 = 인지 0.5 + 이동 평균 2.3(최악 3.04) + 홀드 1.5 ≈ 4.3초
        //   능숙(3.2초/회) → 38.4 소모, 잔여 21.6 / 보통(4.3초/회) → 51.6 소모, 잔여 8.4 / 5.5초/회 → 사망
        // ⚠ 이 값과 AttackTable의 공격 건수는 **한 쌍**이다. 건수를 늘리면 여기도 같이 올려야 한다.
        [SerializeField] private float talismanTotalSec = 60f;
        // 잔여가 이 값 이하로 떨어지면 부적이 다르게 군다 (불씨·재·흔들림·심박 상시 상승).
        // 비회복 풀은 후반까지 조용하다가 갑자기 끝나기 때문에, 리셋 타이머와 달리 임계 연출이 **필수**다.
        [SerializeField] private float talismanCriticalRemainSec = 10f;
        // 엔딩 분기 — 개문 시점의 부적 잔여가 이 값 이상이면 Perfect, 미만이면 Survived.
        [SerializeField] private float endingPerfectRemainSec = 18f;

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
        public float SanityBlanketRegenCeiling => sanityBlanketRegenCeiling;
        public float SanityTelegraphHit => sanityTelegraphHit;
        public float SaltSanityDrainPerSec => saltSanityDrainPerSec;
        public float SaltHoldSec => saltHoldSec;
        public float SaltProgressDecayPerSec => saltProgressDecayPerSec;
        public float DoorOpenHoldSec => doorOpenHoldSec;
        public float DoorArmingSec => doorArmingSec;
        public float BlanketExitSec => blanketExitSec;
        public float TvAttackClockRate => tvAttackClockRate;
        public float MinAttackGapSec => minAttackGapSec;
        public float TrapVoiceLeadSec => trapVoiceLeadSec;
        public float TrapQuietSec => trapQuietSec;
        public float TrapTelegraphSec => trapTelegraphSec;
        public float TrapWaveGapSec => trapWaveGapSec;
        public int TrapWaveCount => trapWaveCount;
        public float BlackCornerLightPenalty => blackCornerLightPenalty;
        public float MinRoomLight => minRoomLight;
        public float RoomLightSmoothSec => roomLightSmoothSec;
        public float BlackCornerSanityDrainPerSec => blackCornerSanityDrainPerSec;
        public float BlackCornerAttackIntervalReduction => blackCornerAttackIntervalReduction;
        public float MinAttackIntervalScale => minAttackIntervalScale;
        /// <summary>단계별 귀퉁이 속삭임 볼륨 — 배열 자체를 넘기지 않는다(SO는 런타임 읽기 전용, 원소를 쓰면 에셋이 영구 오염된다).</summary>
        public float GetCornerWhisperVolume(int stage) => Core.CornerPenaltyModel.WhisperVolume(stage, cornerWhisperVolumes);
        public float PrologueLineMinShowSec => prologueLineMinShowSec;
        public float PrologueWarningSec => prologueWarningSec;
        public float PrologueTelegraphTravelSec => prologueTelegraphTravelSec;
        public float PrologueRetryGapSec => prologueRetryGapSec;
        public int PrologueMaxAttempts => prologueMaxAttempts;
        public float TalismanTotalSec => talismanTotalSec;
        public float TalismanCriticalRemainSec => talismanCriticalRemainSec;
        public float EndingPerfectRemainSec => endingPerfectRemainSec;
        public float RescueAutoOpenDelaySec => rescueAutoOpenDelaySec;
        public float TrueSignalIgnoreDrainPerSec => trueSignalIgnoreDrainPerSec;
        public bool PrologueSkipAvailable => prologueSkipAvailable;
    }
}
