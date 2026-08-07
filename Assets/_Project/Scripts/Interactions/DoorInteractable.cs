using Morae.Game.Core;
using Morae.Game.Data;
using Morae.Game.Gauges;
using Morae.Game.Player;
using UnityEngine;

namespace Morae.Game.Interactions
{
    /// <summary>
    /// 문. 문 상태(DoorState) 소유 (architecture §1.1).
    /// - E 홀드 = 귀 대기 (ListeningAtDoor — 이성 −3/s는 Sanity가 상태를 보고 적용). 떼면 정상 종료.
    /// - 귀 대기 유지 중 문 방향키를 doorOpenHoldSec(1.5s) 지속 = 걸쇠 개방.
    ///   진행률은 DoorLatchProgressChanged 발행. 방향 해제 = 취소·귀 대기 복귀.
    /// - 개방 시 TrueSignal 발화 여부로 분기: 이전 = GameOver(OpenedDoor 즉사) /
    ///   이후 = Ending(<b>부적 잔여 ≥ EndingPerfectRemainSec</b>이면 Perfect, 아니면 Survived).
    ///
    /// <para>
    /// <b>v0.7 안전장치 둘 — 새 조작이 이 문을 흉기로 만든다.</b>
    /// 소금 뿌리기가 "그 위치로 이동하면서 E를 홀드"이므로, 좌상 소금(-4.5, 1.5)으로 가려면
    /// <b>왼쪽 위로</b> 달려야 한다. 문은 상단 벽에 있고 미는 방향이 <b>↑</b>다
    /// (<c>pushDirection</c>의 코드 기본값은 <c>Vector2.left</c>지만 <b>씬에서 up으로 덮여 있다</b> —
    /// 실제 동작은 씬 값이 결정하므로 이 필드를 읽을 때 코드 기본값을 믿으면 안 된다).
    /// 그런데 문 트리거(x −4.2~−2.0, y 1.1~4.1)에 소금보다 <b>1.9u 먼저</b> 닿고,
    /// 그 구간에서는 문이 유일 후보다. 옛 코드는 (a) E가 눌리는 즉시 귀 대기에 들어가고
    /// (b) 밀기 판정이 <c>InputReader.MoveAxis</c>를 그냥 읽어서,
    /// <b>소금으로 가느라 눌러 둔 ↑가 그대로 걸쇠를 돌린다.</b>
    /// 1.5초 뒤 진짜 신호 전이면 즉사다. 이건 코너 케이스가 아니라 가장 자연스러운 조작 시퀀스라 상시 재현된다.
    /// 그래서:
    ///   ① <b>진입 지연</b> — E를 누른 채 이동 입력이 없는 상태가 DoorArmingSec 지속되어야 귀 대기가 시작된다.
    ///      달려가면서 누른 E는 문을 잡지 않고, 문 앞에 멈춰 선 사람은 비용이 0이다.
    ///   ② <b>밀기 무장</b> — 귀 대기 진입 후 방향 입력이 <b>한 번 중립을 거쳐야</b> 밀기로 인정한다.
    ///      진입 프레임의 잔류 입력이 곧바로 개방이 되는 경로를 끊는다. 터치에서는 스틱에 얹은 엄지가
    ///      계속 값을 주입하므로(밀고 있다는 자각조차 없다) 선택이 아니라 필수다.
    /// </para>
    /// </summary>
    public sealed class DoorInteractable : Interactable
    {
        [SerializeField] private GameFlowController flow;
        [SerializeField] private Talisman talisman;
        [SerializeField] private Vector2 pushDirection = Vector2.left; // 플레이어 기준 "문을 미는" 방향키 (씬 배치: 문 = 좌측 벽)

        private Vector2 _pushDir;
        private float _armingHeld;   // 정지 상태로 E를 누르고 있은 시간 (귀 대기 진입 전)
        private bool _armed;         // 귀 대기 진입 완료
        private bool _pushArmed;     // 진입 후 방향 입력이 한 번 중립을 거쳤는가

        /// <summary>
        /// 문을 미는 방향 (정규화). 조작 힌트 UI가 화살표를 고를 때 읽는다 —
        /// 여기서 끌어가야 문을 다른 벽으로 옮겼을 때 안내가 따라온다.
        /// Awake 순서와 무관하게 같은 값을 주려고 직렬화 필드에서 직접 계산한다.
        /// </summary>
        public Vector2 PushDirection
            => pushDirection.sqrMagnitude > 0f ? pushDirection.normalized : Vector2.left;
        private float _latchHeld;

        public DoorState Door { get; private set; } = DoorState.Latched;
        public float LatchProgress01 => Config != null && Config.DoorOpenHoldSec > 0f
            ? Mathf.Clamp01(_latchHeld / Config.DoorOpenHoldSec)
            : 0f;

        /// <summary>E를 떼면 취소가 아니라 정상 종료 (귀 대기는 진행도가 없는 상태 유지형이다).</summary>
        public override bool CompleteOnRelease => true;

        /// <summary>
        /// 즉사 가능한 유일한 상호작용이라 <b>최후순위</b>. 다른 후보(소금·TV·이불)가 범위에 하나라도 있으면
        /// 절대 선택되지 않는다. 문을 쓰려면 문 앞에 혼자 서야 한다.
        /// </summary>
        public override int Priority => -100;

        /// <summary>귀 대기는 무기한 유지 — 완료 조건은 시간이 아니라 손을 떼는 것이다.</summary>
        public override float Duration => float.PositiveInfinity;

        public override string PromptLabel => "귀 대기 (홀드)";

        public override bool CanInteract(PlayerController player)
            => Door != DoorState.Open && player.IsMovable;

        protected override void Awake()
        {
            base.Awake();
            _pushDir = pushDirection.sqrMagnitude > 0f ? pushDirection.normalized : Vector2.left;
        }

        public override void OnBegin(PlayerController player)
        {
            // 아직 귀 대기가 아니다 — 무장 대기부터 시작한다 (진입 지연, 클래스 주석 ①)
            _latchHeld = 0f;
            _armingHeld = 0f;
            _armed = false;
            _pushArmed = false;
        }

        public override void OnHoldTick(PlayerController player, float heldSeconds)
        {
            if (Door == DoorState.Open) return;

            Vector2 move = InputReader.MoveAxis;

            if (!_armed)
            {
                // 이동 입력이 남아 있으면 무장 시계가 계속 0으로 되돌아간다 —
                // 달려가면서 누른 E는 문을 절대 잡지 않는다
                _armingHeld = move.sqrMagnitude > 0.01f ? 0f : _armingHeld + Time.deltaTime;
                if (_armingHeld < Config.DoorArmingSec) return;

                if (!player.TryEnterActionState(PlayerState.ListeningAtDoor)) return;
                _armed = true;
                Debug.Log("[DOOR] 귀 대기 시작 — 문 방향키 지속 입력으로 걸쇠 개방");
                return;
            }

            // 진입 후 방향 입력이 한 번 중립을 거쳐야 밀기로 인정 (클래스 주석 ②)
            if (!_pushArmed)
            {
                if (move.sqrMagnitude > 0.01f) return;
                _pushArmed = true;
                return;
            }

            bool pushing = Vector2.Dot(move, _pushDir) > 0.5f;

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
            _armingHeld = 0f;
            _pushArmed = false;
            if (Door == DoorState.Open) { _armed = false; return; } // 이미 개방 — 종단 처리 완료
            if (Door == DoorState.Opening)
            {
                Door = DoorState.Latched;
                _latchHeld = 0f;
                GameEvents.RaiseDoorLatchProgressChanged(0f);
            }
            // 무장 전에 손을 뗐으면 애초에 귀 대기에 들어간 적이 없다 — 로그도 상태 복귀도 불필요
            if (!_armed) return;
            _armed = false;
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
            GameEvents.RaiseDoorLatchProgressChanged(0f); // 진행 바 정리 (v1.5 ChannelBarView)

            // 문 그림을 먼저 연다 — 아래 엔딩/게임오버 발행이 화면을 덮기 전에 열린 문이 한 프레임이라도 보여야 한다
            GameEvents.RaiseDoorOpened();

            bool trueSignal = flow != null && flow.TrueSignalFired;
            if (trueSignal)
            {
                // v0.7 — 엔딩 등급은 **부적 잔여 시간**이 정한다. 부적이 비회복 타이머가 되면서
                // 잔여가 그대로 "이 사람이 얼마나 빠르고 정확했는가"의 누적 기록이 됐다.
                // 플레이어가 러닝타임 내내 화면에서 보고 있던 것이 그대로 등급의 근거라, 결과가 납득된다.
                float remain = talisman != null ? talisman.RemainingSec : 0f;
                bool perfect = talisman != null && remain >= Config.EndingPerfectRemainSec;
                EndingKind kind = perfect ? EndingKind.Perfect : EndingKind.Survived;
                Debug.Log($"[DOOR] 개문 — 진짜 신호 이후 → 엔딩 {kind} (부적 잔여 {remain:F1}s)");
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
