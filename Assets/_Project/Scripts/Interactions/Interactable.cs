using Morae.Game.Data;
using Morae.Game.Player;
using UnityEngine;

namespace Morae.Game.Interactions
{
    /// <summary>
    /// 상호작용 기반 클래스 (architecture §1.1 — E 문법: 탭/홀드/채널).
    /// 소품 루트에 부착 + 같은 오브젝트(또는 자식)에 트리거 콜라이더 = 상호작용 범위.
    /// 타이밍 구동은 PlayerInteraction이 담당 — 파생은 콜백만 구현한다.
    /// 수치는 전부 BalanceConfig 경유 (하드코딩 금지).
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        [SerializeField] private BalanceConfig config;

        protected BalanceConfig Config => config;

        public abstract InteractionKind Kind { get; }

        /// <summary>HoldComplete/ChannelLocked의 소요 시간. 파생이 BalanceConfig 값으로 구현.</summary>
        public virtual float Duration => 0f;

        /// <summary>상호작용 가능 조건. 기본: 플레이어가 Idle/Move일 때만.</summary>
        public virtual bool CanInteract(PlayerController player) => player.IsMovable;

        public virtual void OnTap(PlayerController player) { }
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
        }
    }
}
