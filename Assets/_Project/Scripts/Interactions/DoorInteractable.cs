using Morae.Game.Core;
using Morae.Game.Data;
using Morae.Game.Gauges;
using Morae.Game.Player;
using UnityEngine;

namespace Morae.Game.Interactions
{
    /// <summary>
    /// 문 (명세 §3 + 문 입력 규칙 확정 2026-08-04 — _shared.md). 문 상태(DoorState) 소유 (architecture §1.1).
    /// - E 홀드 = 귀 대기 (기본·즉시. ListeningAtDoor — 이성 −3/s는 Sanity가 상태를 보고 적용).
    /// - 귀 대기 유지 중 문 방향키를 doorOpenHoldSec(1.5s) 지속 = 걸쇠 개방.
    ///   진행률은 DoorLatchProgressChanged 발행 (UI 진행 링은 Epic 2 구독). 방향 해제 = 취소·귀 대기 복귀 (L-4 관용).
    /// - 개방 시 TrueSignal 발화 여부(GameFlowController.TrueSignalFired)로 분기:
    ///   이전 = GameOver(OpenedDoor 즉사) / 이후 = Ending(부적 미소모 Perfect / 소모 Survived).
    /// </summary>
    public sealed class DoorInteractable : Interactable
    {
        [SerializeField] private GameFlowController flow;
        [SerializeField] private Talisman talisman;
        [SerializeField] private Vector2 pushDirection = Vector2.left; // 플레이어 기준 "문을 미는" 방향키 (씬 배치: 문 = 좌측 벽)

        private Vector2 _pushDir;
        private float _latchHeld;

        public DoorState Door { get; private set; } = DoorState.Latched;
        public float LatchProgress01 => Config != null && Config.DoorOpenHoldSec > 0f
            ? Mathf.Clamp01(_latchHeld / Config.DoorOpenHoldSec)
            : 0f;

        public override InteractionKind Kind => InteractionKind.HoldMaintain;

        public override bool CanInteract(PlayerController player)
            => Door != DoorState.Open && player.IsMovable;

        protected override void Awake()
        {
            base.Awake();
            _pushDir = pushDirection.sqrMagnitude > 0f ? pushDirection.normalized : Vector2.left;
        }

        public override void OnBegin(PlayerController player)
        {
            player.TryEnterActionState(PlayerState.ListeningAtDoor);
            _latchHeld = 0f;
            Debug.Log("[DOOR] 귀 대기 시작 — 문 방향키 지속 입력으로 걸쇠 개방");
        }

        public override void OnHoldTick(PlayerController player, float heldSeconds)
        {
            if (Door == DoorState.Open) return;

            bool pushing = Vector2.Dot(InputReader.MoveAxis, _pushDir) > 0.5f;

            if (pushing)
            {
                if (player.State == PlayerState.ListeningAtDoor
                    && player.SwitchActionState(PlayerState.ListeningAtDoor, PlayerState.OpeningDoor))
                {
                    Door = DoorState.Opening;
                    Debug.Log("[DOOR] 걸쇠 개방 시작 — 방향 유지 " + Config.DoorOpenHoldSec.ToString("F1") + "s");
                }
                if (player.State == PlayerState.OpeningDoor)
                {
                    _latchHeld += Time.deltaTime;
                    GameEvents.RaiseDoorLatchProgressChanged(LatchProgress01);
                    if (_latchHeld >= Config.DoorOpenHoldSec)
                    {
                        OpenDoor(player);
                    }
                }
            }
            else if (player.State == PlayerState.OpeningDoor)
            {
                CancelLatch(player); // 방향 해제 = 취소, 귀 대기 복귀 (마지막 관용)
            }
        }

        public override void OnComplete(PlayerController player) => EndListening(player); // E 해제 = 귀 대기 정상 종료

        public override void OnCancel(PlayerController player) => EndListening(player);   // 범위 이탈 등 방어 코드

        private void EndListening(PlayerController player)
        {
            if (Door == DoorState.Open) return; // 이미 개방 — 종단 처리 완료
            if (Door == DoorState.Opening)
            {
                Door = DoorState.Latched;
                _latchHeld = 0f;
                GameEvents.RaiseDoorLatchProgressChanged(0f);
            }
            Debug.Log("[DOOR] 귀 대기 종료");
            player.ReturnToIdle();
        }

        private void CancelLatch(PlayerController player)
        {
            Door = DoorState.Latched;
            _latchHeld = 0f;
            GameEvents.RaiseDoorLatchProgressChanged(0f);
            player.SwitchActionState(PlayerState.OpeningDoor, PlayerState.ListeningAtDoor);
            Debug.Log("[DOOR] 걸쇠 개방 취소 — 귀 대기 복귀");
        }

        private void OpenDoor(PlayerController player)
        {
            Door = DoorState.Open;
            _latchHeld = 0f;

            bool trueSignal = flow != null && flow.TrueSignalFired;
            if (trueSignal)
            {
                bool perfect = talisman == null || !talisman.Consumed;
                EndingKind kind = perfect ? EndingKind.Perfect : EndingKind.Survived;
                Debug.Log($"[DOOR] 개문 — 진짜 신호 이후 → 엔딩 {kind}");
                player.EnterTerminalState(PlayerState.Escaped);
                GameEvents.RaiseEndingStarted(kind);
            }
            else
            {
                Debug.Log("[DOOR] 개문 — 진짜 신호 이전 → 즉사");
                player.EnterTerminalState(PlayerState.Dead);
                GameEvents.RaiseGameOver(GameOverReason.OpenedDoor);
            }
        }
    }
}
