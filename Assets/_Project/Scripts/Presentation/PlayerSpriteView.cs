using Morae.Game.Core;
using Morae.Game.Data;
using UnityEngine;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 플레이어 스프라이트 표시 제어 (표현 계층 — PlayerStateChanged 구독만, §1.2).
    /// InBlanket 동안 렌더러를 숨긴다 — 몸은 이불 융기(BlanketView의 bulge)가 대신 표현.
    /// 이탈(다른 상태로 전환) 시 복원. 이탈 지연(1s) 중에도 상태는 InBlanket이므로
    /// 융기 유지·플레이어 숨김이 함께 유지된다 — BlanketView 규칙과 일치.
    /// </summary>
    public sealed class PlayerSpriteView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer body;

        private void OnEnable()
        {
            GameEvents.PlayerStateChanged += HandleStateChanged;
            HandleStateChanged(PlayerState.Idle); // 씬 시작 상태 동기화 (재시작 = 씬 리로드 → 항상 Idle)
        }

        private void OnDisable()
        {
            GameEvents.PlayerStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(PlayerState state)
        {
            if (body == null) return;
            bool visible = state != PlayerState.InBlanket;
            if (body.enabled != visible) body.enabled = visible;
        }
    }
}
