using Morae.Game.Data;
using Morae.Game.Player;
using UnityEngine;

namespace Morae.Game.Interactions
{
    /// <summary>
    /// 상호작용 기반 클래스 — <b>E 홀드 단일 문법</b> (v0.7).
    /// 소품 루트에 부착 + 같은 오브젝트(또는 자식)에 트리거 콜라이더 = 상호작용 범위.
    /// 타이밍 구동은 PlayerInteraction이 담당 — 파생은 콜백만 구현한다. 수치는 전부 BalanceConfig 경유.
    ///
    /// <para>
    /// <b>InteractionKind 4종(Tap/HoldMaintain/HoldComplete/ChannelLocked)을 없앤 이유.</b>
    /// 조작 축을 하나로 줄이는 게 이번 개편의 최우선 목표인데, 축의 개수는 키 개수가 아니라
    /// <b>"E를 누르면 무슨 일이 일어나는가"의 규칙 수</b>로 정해진다. 대상에 따라 문법이 4가지로 갈리면
    /// 키가 하나여도 플레이어는 네 가지를 외워야 한다. 남은 차이는 아래 두 프로퍼티로 충분히 표현되고,
    /// <see cref="Duration"/>이 0이면 시작 다음 틱에 완료되므로 옛 Tap은 홀드의 특수해로 흡수된다.
    /// </para>
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        [SerializeField] private BalanceConfig config;

        protected BalanceConfig Config => config;

        /// <summary>
        /// 이 소품의 상호작용 범위 콜라이더 — Awake에서 한 번만 해석한다.
        /// PlayerInteraction이 후보 선택에서 매 프레임 읽으므로 그때 탐색하면 핫패스 탐색이 된다.
        /// </summary>
        public Collider2D RangeCollider { get; private set; }

        /// <summary>
        /// 후보가 겹칠 때의 선택 우선순위 (높을수록 먼저). 거리는 동순위 안에서만 따진다.
        /// <para>
        /// <b>왜 거리만으로는 안 되는가.</b> 탐지는 면적(트리거 겹침) 기준인데 선택은 점(transform.position)
        /// 기준이라 두 지표가 어긋난다. 소품은 원점이 큰 트리거 한복판이고 소금은 원점이 발밑이라
        /// 편향 방향까지 반대다. 실측하면 좌상 소금(-4.5,1.5) 위에 선 플레이어는 이미 문 트리거
        /// (x -4.2~-2.0) 안이고, 소금에 닿기 전 1.9u 구간에서는 <b>문이 유일 후보</b>다.
        /// 그 상태로 E를 누르면 걸어오느라 눌러 둔 방향키가 그대로 걸쇠를 돌려 즉사한다.
        /// 그래서 위험 대상은 음수 우선순위를 줘서 <b>다른 후보가 하나라도 있으면 절대 안 잡히게</b> 한다.
        /// </para>
        /// </summary>
        public virtual int Priority => 0;

        /// <summary>E를 떼면 <b>완료</b>로 처리한다 (문 귀 대기). false면 떼는 것이 취소다.</summary>
        public virtual bool CompleteOnRelease => false;

        /// <summary>E를 떼서 중단할 수 있는가. false면 Duration까지 잠긴다.</summary>
        public virtual bool Cancelable => true;

        /// <summary>E 프롬프트에 표시되는 행동명 (InteractPromptView가 소비).</summary>
        public virtual string PromptLabel => "상호작용";

        /// <summary>홀드 소요 시간. 0이면 즉시 완료(옛 Tap).</summary>
        public virtual float Duration => 0f;

        /// <summary>
        /// 홀드 시작 시의 초기 경과 시간 — <b>부분 진행도 이어받기</b>용 훅.
        /// 소금은 귀퉁이에 남아 있던 진행도에서 이어서 시작한다. 이게 없으면 오조작 1회가
        /// 확정 사망이 된다(부적 예산이 남은 상태에서 처음부터 다시 할 시간이 없다).
        /// </summary>
        public virtual float InitialElapsed => 0f;

        /// <summary>상호작용 가능 조건. 기본: 플레이어가 Idle/Move일 때만.</summary>
        public virtual bool CanInteract(PlayerController player) => player.IsMovable;

        public virtual void OnBegin(PlayerController player) { }
        public virtual void OnHoldTick(PlayerController player, float heldSeconds) { }
        public virtual void OnComplete(PlayerController player) { }
        public virtual void OnCancel(PlayerController player) { }

        protected virtual void Awake()
        {
            if (config == null)
            {
                Debug.LogError($"[INTERACT] {name}: BalanceConfig 미배선", this);
            }
            RangeCollider = GetComponentInChildren<Collider2D>();
            if (RangeCollider == null)
            {
                Debug.LogError($"[INTERACT] {name}: 트리거 콜라이더 없음 — 후보로 잡히지 않는다", this);
            }
        }
    }
}
