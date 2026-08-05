using Morae.Game.Core;
using Morae.Game.Data;
using UnityEngine;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 이불 스프라이트 스왑 (표현 계층 — PlayerStateChanged 구독만, §1.2. 아트 2단계).
    /// InBlanket = 사람 들어간 융기(bulge), 그 외 = 펼침(flat).
    /// 이탈 지연(1s) 중에도 상태는 InBlanket이므로 융기가 유지된다 — BlanketInteractable 규칙과 일치.
    /// </summary>
    public sealed class BlanketView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer blanket;
        [SerializeField] private Sprite flatSprite;
        [SerializeField] private Sprite bulgeSprite;

        private void OnEnable()
        {
            GameEvents.PlayerStateChanged += HandleStateChanged;
            HandleStateChanged(PlayerState.Idle);
        }

        private void OnDisable()
        {
            GameEvents.PlayerStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(PlayerState state)
        {
            if (blanket == null) return;
            Sprite next = state == PlayerState.InBlanket ? bulgeSprite : flatSprite;
            if (next != null && blanket.sprite != next) blanket.sprite = next;
        }
    }
}
