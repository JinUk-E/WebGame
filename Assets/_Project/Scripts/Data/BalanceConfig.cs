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
        [SerializeField] private float tvAttackClockRate = 1.333333f;  // 공격 간격 ×0.75 = 페이즈 로컬 공격 시계 배속 (§2.2)

        [Header("부적 (명세 §2 — 1회 방어)")]
        [SerializeField] private int talismanSaltRestore = 1;          // 전 귀퉁이 −1
        [SerializeField] private float talismanSanityRestore = 30f;    // 이성 +30
        [SerializeField] private float talismanFxSec = 3f;             // 발동 연출(부적이 검게 탐)

        [Header("게임 흐름")]
        [SerializeField] private float rescueAutoOpenDelaySec = 60f;   // 진짜 신호 → K씨 개문 (초안 60)
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
        public int TalismanSaltRestore => talismanSaltRestore;
        public float TalismanSanityRestore => talismanSanityRestore;
        public float TalismanFxSec => talismanFxSec;
        public float RescueAutoOpenDelaySec => rescueAutoOpenDelaySec;
        public bool PrologueSkipAvailable => prologueSkipAvailable;
    }
}
