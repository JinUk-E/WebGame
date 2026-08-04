using Morae.Game.Data;
using Morae.Game.Player;
using UnityEngine;

namespace Morae.Game.Interactions
{
    /// <summary>
    /// 문 (명세 §3 — 귀 대기: E 홀드 유지 / 걸쇠 열기: E 1.5s 홀드). 문 상태(DoorState) 소유 (architecture §1.1).
    /// [껍데기] 지금은 귀 대기(HoldMaintain)만 구동.
    /// TODO(§4 순서 6 — FR-9): 같은 위치·같은 E 홀드인 "귀 대기"와 "걸쇠 열기"의 입력 구분이 명세에 미정의.
    ///   해소안 후보: ① 별도 키(예: 홀드 중 방향키 문 쪽 밀기 = 개문 전환) ② 귀 대기 N초 후 프롬프트 전환.
    ///   개문 완료 시 DoorState.Open + TrueSignalStarted 발화 여부로 GameOver(OpenedDoor)/Ending 분기.
    /// </summary>
    public sealed class DoorInteractable : Interactable
    {
        public DoorState Door { get; private set; } = DoorState.Latched;

        public override InteractionKind Kind => InteractionKind.HoldMaintain;

        public override void OnBegin(PlayerController player)
        {
            player.TryEnterActionState(PlayerState.ListeningAtDoor);
            Debug.Log("[DOOR] 귀 대기 시작 — 문밖 선명화·이성 드레인 연동은 §4 순서 5~6");
        }

        public override void OnComplete(PlayerController player)
        {
            Debug.Log("[DOOR] 귀 대기 종료 (E 해제)");
            player.ReturnToIdle();
        }

        public override void OnCancel(PlayerController player)
        {
            Debug.Log("[DOOR] 귀 대기 중단");
            player.ReturnToIdle();
        }
    }
}
