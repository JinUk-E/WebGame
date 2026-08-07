using Morae.Game.Core;
using Morae.Game.Data;
using Morae.Game.Player;
using UnityEngine;

namespace Morae.Game.Interactions
{
    /// <summary>
    /// 이불 — 진입/이탈. 속에서 이성 +3/s(Sanity가 상태를 보고 적용, <b>상한 75</b>), 나오는 데 0.5초.
    /// 이탈 → BlanketExitSec 지연 후 Idle 복귀 — 지연 중에는 재상호작용 불가 (즉시 반응 불가가 이불의 대가).
    /// 지연 중에도 상태는 InBlanket — 회복이 그만큼 이어지는 것은 허용.
    ///
    /// <para>
    /// <b>들어갈 때는 즉발, 나올 때는 유예.</b> 이 비대칭이 이불의 성격이다 —
    /// 숨는 건 반사적으로 되어야 하고(무서우면 바로 뒤집어쓴다), 나오는 데는 대가가 있어야 한다.
    /// 진입 <see cref="Duration"/>은 0(옛 Tap과 같은 체감)이고, 이탈만 BlanketExitSec만큼 기다린다.
    /// 그동안 방은 여전히 어둡다 — 이불 감광이 이탈 완료까지 유지되므로 유예가 곧 정보 공백이다.
    /// </para>
    /// </summary>
    public sealed class BlanketInteractable : Interactable
    {
        [SerializeField] private float enterHoldSec;

        private float _exitTimer = -1f; // < 0 = 이탈 진행 중 아님
        private PlayerController _exitingPlayer;

        public override float Duration => enterHoldSec;
        public override string PromptLabel => "이불";

        // InBlanket에서도 이탈이 가능해야 함 — 단 이탈 지연 중에는 불가
        public override bool CanInteract(PlayerController player)
            => player.IsMovable || (player.State == PlayerState.InBlanket && _exitTimer < 0f);

        public override void OnComplete(PlayerController player)
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

            if (_exitTimer >= 0f)
            {
                // v1.5 — 이탈 진행 바 (ChannelBarView 구독)
                float total = Config.BlanketExitSec;
                GameEvents.RaiseBlanketExitChanged(total > 0f ? Mathf.Clamp01(1f - _exitTimer / total) : 1f);
                return;
            }

            GameEvents.RaiseBlanketExitChanged(0f);
            if (_exitingPlayer != null)
            {
                _exitingPlayer.ReturnToIdle(); // 종단 상태(Dead/Escaped)면 내부에서 무시됨
                _exitingPlayer = null;
            }
            Debug.Log("[BLANKET] 이불 이탈 완료");
        }
    }
}
