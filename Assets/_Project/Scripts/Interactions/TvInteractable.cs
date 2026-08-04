using Morae.Game.Core;
using Morae.Game.Data;
using Morae.Game.Player;
using UnityEngine;

namespace Morae.Game.Interactions
{
    /// <summary>
    /// TV 토글 (명세 §3 — E 탭. 켜짐 동안 이성 +1/s, 공격 간격 ×0.75, 귀 대기 상세 자막 불가).
    /// [껍데기] 토글 상태 소유 + TVToggled 발행까지만. 이성 회복 연동은 Sanity(§4 순서 5),
    /// 공격 가속 연동은 AttackScheduler(§4 순서 4)가 TVToggled 구독 또는 IsOn 읽기로 처리.
    /// </summary>
    public sealed class TvInteractable : Interactable
    {
        public bool IsOn { get; private set; }

        public override InteractionKind Kind => InteractionKind.Tap;
        public override string PromptLabel => IsOn ? "TV 끄기" : "TV 켜기";

        public override void OnTap(PlayerController player)
        {
            IsOn = !IsOn;
            GameEvents.RaiseTVToggled(IsOn);
            Debug.Log($"[TV] 토글 → {(IsOn ? "켜짐" : "꺼짐")}");
        }
    }
}
