using Morae.Game.Core;
using Morae.Game.Data;
using Morae.Game.Gauges;
using Morae.Game.Player;
using UnityEngine;

namespace Morae.Game.Interactions
{
    /// <summary>
    /// 기도 (명세 §2·§3 — 불상 앞, E 홀드 + 방향키로 귀퉁이 지정, 3초 채널. E를 떼면 취소).
    /// 채널 중 방향키는 이동이 아니라 조준 — 대각 입력만 귀퉁이로 매핑 (Praying 상태라 이동 입력은 차단됨).
    /// 완료: 조준 귀퉁이에 활성 전조가 있으면 즉시 상쇄(AttackScheduler.TryCounter — 능동 방어),
    ///       없으면 사후 정화(SaltCorners.Purify −1). 같은 계층 직접 호출 (§1.2).
    /// AimedCorner 규약: 0=좌상 1=우상 2=좌하 3=우하 (CornerIndex).
    /// </summary>
    public sealed class PrayerInteractable : Interactable
    {
        [SerializeField] private SaltCorners salt;
        [SerializeField] private AttackScheduler scheduler;

        private int _aimedCorner = CornerIndex.None;

        public override InteractionKind Kind => InteractionKind.HoldComplete;
        public override string PromptLabel => "기도 (홀드+대각 조준)";

        /// <summary>
        /// v0.3 흑화 심화: 조준 귀퉁이가 심화 상태면 채널 ×PrayerDeepenedMultiplier (3s → 4.5s).
        /// PlayerInteraction이 매 틱 Duration을 읽으므로 채널 중 조준 변경도 즉시 반영된다.
        /// </summary>
        public override float Duration
        {
            get
            {
                float baseSec = Config.PrayerChannelSec;
                if (salt != null && _aimedCorner != CornerIndex.None && salt.IsDeepened(_aimedCorner))
                {
                    return baseSec * Config.PrayerDeepenedMultiplier;
                }
                return baseSec;
            }
        }

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

            // v1.4 — 채널 진행·조준 시각 피드백 (PrayerView·SaltCornersView 구독)
            float progress = Duration > 0f ? Mathf.Clamp01(heldSeconds / Duration) : 1f;
            GameEvents.RaisePrayerChannelChanged(progress, _aimedCorner);
        }

        public override void OnComplete(PlayerController player)
        {
            GameEvents.RaisePrayerChannelChanged(0f, CornerIndex.None);
            player.ReturnToIdle();

            if (_aimedCorner == CornerIndex.None)
            {
                Debug.Log("[PRAY] 기도 완료 — 조준 귀퉁이 없음, 효과 없음");
                return;
            }
            if (scheduler != null && scheduler.TryCounter(_aimedCorner))
            {
                return; // 전조 상쇄 성공 — 능동 방어 (로그·이벤트는 scheduler가 발행)
            }
            if (salt != null)
            {
                salt.Purify(_aimedCorner); // 전조 없음 — 사후 정화 −1
            }
        }

        public override void OnCancel(PlayerController player)
        {
            Debug.Log("[PRAY] 기도 취소 (E 조기 해제)");
            GameEvents.RaisePrayerChannelChanged(0f, CornerIndex.None);
            player.ReturnToIdle();
        }
    }
}
