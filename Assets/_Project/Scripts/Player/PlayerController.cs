using Morae.Game.Core;
using Morae.Game.Data;
using UnityEngine;

namespace Morae.Game.Player
{
    /// <summary>
    /// 8방향 이동 + 플레이어 상태 머신 골격 (architecture §1.1·§1.4).
    /// 이동은 Idle/Move에서만. 행동 상태 진입/복귀는 Interactable이 TryEnterActionState/ReturnToIdle로 요청.
    /// 상태 변화는 GameEvents.PlayerStateChanged 발행만 — 표현 계층 직접 참조 금지.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private BalanceConfig config;

        private Rigidbody2D _body;
        private Vector2 _moveInput;

        public PlayerState State { get; private set; } = PlayerState.Idle;
        public bool IsMovable => State == PlayerState.Idle || State == PlayerState.Move;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            if (config == null)
            {
                Debug.LogError("[PLAYER] BalanceConfig 미배선 — 이동 불가", this);
            }
        }

        private void Update()
        {
            _moveInput = IsMovable ? InputReader.MoveAxis : Vector2.zero;

            if (State == PlayerState.Idle && _moveInput.sqrMagnitude > 0f)
            {
                SetState(PlayerState.Move);
            }
            else if (State == PlayerState.Move && _moveInput.sqrMagnitude <= 0f)
            {
                SetState(PlayerState.Idle);
            }
        }

        private void FixedUpdate()
        {
            // 물리는 FixedUpdate (플레이북) — 벽 콜라이더와의 충돌은 Rigidbody2D가 처리
            float speed = config != null ? config.MoveSpeed : 0f;
            _body.linearVelocity = _moveInput * speed;
        }

        /// <summary>Idle/Move에서만 행동 상태 진입 허용. 성공 여부 반환.</summary>
        public bool TryEnterActionState(PlayerState actionState)
        {
            if (!IsMovable) return false;
            SetState(actionState);
            return true;
        }

        /// <summary>
        /// 행동 상태 간 직접 전환 (문: 귀 대기 ↔ 걸쇠 개방). 현재 상태가 from일 때만 허용.
        /// </summary>
        public bool SwitchActionState(PlayerState from, PlayerState to)
        {
            if (State != from) return false;
            if (to == PlayerState.Dead || to == PlayerState.Escaped) return false; // 종단은 EnterTerminalState로만
            SetState(to);
            return true;
        }

        /// <summary>행동 상태에서 Idle 복귀. Dead/Escaped는 종단 상태 — 복귀 불가.</summary>
        public void ReturnToIdle()
        {
            if (IsMovable) return;
            if (State == PlayerState.Dead || State == PlayerState.Escaped) return;
            SetState(PlayerState.Idle);
        }

        /// <summary>종단 상태 진입 (게임오버·엔딩 연출용 — §4 순서 6에서 사용).</summary>
        public void EnterTerminalState(PlayerState terminal)
        {
            if (terminal != PlayerState.Dead && terminal != PlayerState.Escaped) return;
            SetState(terminal);
        }

        private void SetState(PlayerState next)
        {
            if (State == next) return;
            State = next;
            GameEvents.RaisePlayerStateChanged(next);
        }
    }
}
