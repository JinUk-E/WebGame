using Morae.Game.Core;
using Morae.Game.Data;
using Morae.Game.Player;
using UnityEngine;

namespace Morae.Game.Interactions
{
    /// <summary>
    /// TV 토글. 켜짐 동안 이성 +1/s, 공격 간격 ×0.75, 귀 대기 상세 자막 불가.
    /// 토글 상태 소유 + TVToggled 발행까지만 — 이성 회복은 Sanity, 공격 가속은 AttackScheduler가 IsOn을 읽는다.
    ///
    /// <para>
    /// <b>토글은 즉발이다.</b> <see cref="Duration"/> 0 = 시작 다음 틱에 완료 — 옛 Tap과 같은 체감이면서
    /// 구동 경로는 홀드 하나로 통일돼 있다(문법이 늘지 않는다). 켜고 끄는 건 반복 조작이라
    /// 확인 절차를 붙이면 그냥 답답해진다 — 되돌리기 쉬운 행동에는 마찰이 없어야 한다.
    /// 오조작 위험은 우선순위가 막는다: 더러운 소금이 범위에 있으면 그쪽이 항상 이긴다.
    /// </para>
    /// </summary>
    public sealed class TvInteractable : Interactable
    {
        [SerializeField] private float toggleHoldSec;

        public bool IsOn { get; private set; }

        public override float Duration => toggleHoldSec;
        public override string PromptLabel => IsOn ? "TV 끄기" : "TV 켜기";

        public override void OnComplete(PlayerController player)
        {
            IsOn = !IsOn;
            GameEvents.RaiseTVToggled(IsOn);
            Debug.Log($"[TV] 토글 → {(IsOn ? "켜짐" : "꺼짐")}");
        }
    }
}
