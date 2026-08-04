using Morae.Game.Data;
using Morae.Game.Gauges;
using Morae.Game.Player;
using UnityEngine;

namespace Morae.Game.Interactions
{
    /// <summary>
    /// 요강 (명세 §3 — E로 시작, 5초 무방비 채널, 취소 불가. 요의 해소).
    /// 요의 발생은 EventDirector(Epic 2)가 "urge" 이벤트에서 Sanity.SetUrgeActive(true) — 여기서는 해소만 담당.
    /// </summary>
    public sealed class JarInteractable : Interactable
    {
        [SerializeField] private Sanity sanity;

        public override InteractionKind Kind => InteractionKind.ChannelLocked;
        public override float Duration => Config.JarLockSec;

        public override void OnBegin(PlayerController player)
        {
            player.TryEnterActionState(PlayerState.UsingJar);
            Debug.Log("[JAR] 요강 사용 시작 — 5초 무방비 (취소 불가)");
        }

        public override void OnComplete(PlayerController player)
        {
            if (sanity != null) sanity.SetUrgeActive(false); // 요의 해소 — 회복 재개
            Debug.Log("[JAR] 요강 사용 완료 — 요의 해소");
            player.ReturnToIdle();
        }
    }
}
