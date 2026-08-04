using Morae.Game.Data;
using Morae.Game.Player;
using UnityEngine;

namespace Morae.Game.Interactions
{
    /// <summary>
    /// 이불 (명세 §3 — E 탭 진입/이탈. 속에서 이성 +3/s(Sanity가 상태를 보고 적용), 나오는 데 1초).
    /// 이탈 탭 → BlanketExitSec 지연 후 Idle 복귀 — 지연 중에는 재상호작용 불가 (즉시 반응 불가가 이불의 대가).
    /// 지연 중에도 상태는 InBlanket — 회복이 그만큼 이어지는 것은 허용 (미미, 결정 기록).
    /// </summary>
    public sealed class BlanketInteractable : Interactable
    {
        private float _exitTimer = -1f; // < 0 = 이탈 진행 중 아님
        private PlayerController _exitingPlayer;

        public override InteractionKind Kind => InteractionKind.Tap;

        // InBlanket에서도 이탈 탭이 가능해야 함 — 단 이탈 지연 중에는 불가
        public override bool CanInteract(PlayerController player)
            => player.IsMovable || (player.State == PlayerState.InBlanket && _exitTimer < 0f);

        public override void OnTap(PlayerController player)
        {
            if (player.State == PlayerState.InBlanket)
            {
                _exitTimer = Config.BlanketExitSec;
                _exitingPlayer = player;
                Debug.Log($"[BLANKET] 이불 이탈 시작 — {Config.BlanketExitSec:F1}s 후 복귀");
            }
            else if (player.TryEnterActionState(PlayerState.InBlanket))
            {
                _exitTimer = -1f;
                Debug.Log("[BLANKET] 이불 진입 — 이성 +3/s");
            }
        }

        private void Update()
        {
            if (_exitTimer < 0f) return;
            _exitTimer -= Time.deltaTime;
            if (_exitTimer >= 0f) return;

            if (_exitingPlayer != null)
            {
                _exitingPlayer.ReturnToIdle(); // 종단 상태(Dead/Escaped)면 내부에서 무시됨
                _exitingPlayer = null;
            }
            Debug.Log("[BLANKET] 이불 이탈 완료");
        }
    }
}
