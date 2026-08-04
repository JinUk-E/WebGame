using Morae.Game.Data;
using Morae.Game.Player;
using UnityEngine;

namespace Morae.Game.Interactions
{
    /// <summary>
    /// 이불 (명세 §3 — E 탭 진입/이탈. 속에서 이성 +3/s, 화면 가림, 나오는 데 1초).
    /// [껍데기] 상태 전환만. 이성 회복은 Sanity(§4 순서 5), 이탈 지연(BlanketExitSec)은 그때 함께 구현.
    /// </summary>
    public sealed class BlanketInteractable : Interactable
    {
        public override InteractionKind Kind => InteractionKind.Tap;

        // InBlanket 상태에서도 이탈 탭이 가능해야 함 — 기본 조건(IsMovable) 확장
        public override bool CanInteract(PlayerController player)
            => player.IsMovable || player.State == PlayerState.InBlanket;

        public override void OnTap(PlayerController player)
        {
            if (player.State == PlayerState.InBlanket)
            {
                // TODO(§4 순서 5): Config.BlanketExitSec 지연 후 복귀 (즉시 이탈은 임시)
                player.ReturnToIdle();
                Debug.Log("[BLANKET] 이불 이탈 (이탈 지연 1s는 §4 순서 5에서)");
            }
            else if (player.TryEnterActionState(PlayerState.InBlanket))
            {
                Debug.Log("[BLANKET] 이불 진입 — 이성 회복 연동은 §4 순서 5");
            }
        }
    }
}
