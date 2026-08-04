using Morae.Game.Data;
using Morae.Game.Player;
using UnityEngine;

namespace Morae.Game.Interactions
{
    /// <summary>
    /// 요강 (명세 §3 — E로 시작, 5초 무방비 채널, 취소 불가. 요의 해소).
    /// [껍데기] 상태 잠금만. 요의 이벤트(id="urge") 발생/해소·회복 무효 연동은 §4 순서 5 (FR-15, 컷 1순위).
    /// </summary>
    public sealed class JarInteractable : Interactable
    {
        public override InteractionKind Kind => InteractionKind.ChannelLocked;
        public override float Duration => Config.JarLockSec;

        public override void OnBegin(PlayerController player)
        {
            player.TryEnterActionState(PlayerState.UsingJar);
            Debug.Log("[JAR] 요강 사용 시작 — 5초 무방비 (취소 불가)");
        }

        public override void OnComplete(PlayerController player)
        {
            Debug.Log("[JAR] 요강 사용 완료 — 요의 해소 연동은 §4 순서 5");
            player.ReturnToIdle();
        }
    }
}
