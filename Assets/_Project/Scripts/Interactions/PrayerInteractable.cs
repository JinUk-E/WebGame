using Morae.Game.Data;
using Morae.Game.Player;
using UnityEngine;

namespace Morae.Game.Interactions
{
    /// <summary>
    /// 기도 (명세 §3 — 불상 앞, E 홀드 + 방향키로 귀퉁이 지정, 3초 채널).
    /// E를 떼면 취소. 채널 중 방향키는 이동이 아니라 귀퉁이 조준 (Praying 상태에서 이동 입력 차단됨).
    /// [껍데기] 완료 시 SaltCorners 정화·전조 상쇄 연결은 §4 순서 4에서 (직접 호출 — 같은 계층).
    /// </summary>
    public sealed class PrayerInteractable : Interactable
    {
        private int _aimedCorner = CornerIndex.None;

        public override InteractionKind Kind => InteractionKind.HoldComplete;
        public override float Duration => Config.PrayerChannelSec;

        /// <summary>현재 조준 중인 귀퉁이 (0~3, 미지정 -1) — 표현·후속 로직용 읽기 프로퍼티.</summary>
        public int AimedCorner => _aimedCorner;

        public override void OnBegin(PlayerController player)
        {
            player.TryEnterActionState(PlayerState.Praying);
            _aimedCorner = CornerIndex.None;
            Debug.Log("[PRAY] 기도 채널 시작 — 방향키(대각)로 귀퉁이 지정");
        }

        public override void OnHoldTick(PlayerController player, float heldSeconds)
        {
            // Praying 중 방향키 = 조준. 대각 입력만 귀퉁이로 매핑, 단일 축 입력은 이전 조준 유지
            Vector2 aim = InputReader.MoveAxis;
            if (Mathf.Abs(aim.x) > 0.1f && Mathf.Abs(aim.y) > 0.1f)
            {
                _aimedCorner = aim.y > 0f
                    ? (aim.x < 0f ? CornerIndex.TopLeft : CornerIndex.TopRight)
                    : (aim.x < 0f ? CornerIndex.BottomLeft : CornerIndex.BottomRight);
            }
        }

        public override void OnComplete(PlayerController player)
        {
            Debug.Log($"[PRAY] 기도 완료 — 조준 귀퉁이 {_aimedCorner} (정화/상쇄 적용은 SaltCorners 연결 후 — §4 순서 4)");
            player.ReturnToIdle();
        }

        public override void OnCancel(PlayerController player)
        {
            Debug.Log("[PRAY] 기도 취소 (E 조기 해제)");
            player.ReturnToIdle();
        }
    }
}
