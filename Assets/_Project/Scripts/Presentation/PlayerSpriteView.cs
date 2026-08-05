using Morae.Game.Core;
using Morae.Game.Data;
using Morae.Game.Player;
using UnityEngine;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 플레이어 스프라이트 표시 제어 (표현 계층 — PlayerStateChanged 구독만, §1.2).
    /// InBlanket 동안 렌더러를 숨긴다 — 몸은 이불 융기(BlanketView의 bulge)가 대신 표현.
    /// 이탈(다른 상태로 전환) 시 복원. 이탈 지연(1s) 중에도 상태는 InBlanket이므로
    /// 융기 유지·플레이어 숨김이 함께 유지된다 — BlanketView 규칙과 일치.
    /// 이동 방향 회전은 PlayerController.Facing을 읽는다 — 연속값이라 이벤트가 없다
    /// (LightingController가 PhaseSequencer를 직접 읽는 것과 같은 표현 계층 읽기 전용 참조).
    /// </summary>
    public sealed class PlayerSpriteView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private PlayerController player;
        // player_boy 스프라이트는 정수리·등을 그린 완전 탑뷰 — 기본 방향이 화면 위(+Y)다.
        [SerializeField] private float turnSpeedDegPerSec = 720f;

        private void OnEnable()
        {
            GameEvents.PlayerStateChanged += HandleStateChanged;
            HandleStateChanged(PlayerState.Idle); // 씬 시작 상태 동기화 (재시작 = 씬 리로드 → 항상 Idle)
        }

        private void OnDisable()
        {
            GameEvents.PlayerStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(PlayerState state)
        {
            if (body == null) return;
            bool visible = state != PlayerState.InBlanket;
            if (body.enabled != visible) body.enabled = visible;
        }

        private void Update()
        {
            if (body == null || player == null) return;

            Vector2 facing = player.Facing;
            if (facing.sqrMagnitude < 0.0001f) return;

            // 스프라이트 로컬 +Y를 이동 방향에 맞춘다. Z회전 θ는 up을 (−sinθ, cosθ)로 보내므로 θ = atan2(−x, y).
            float target = Mathf.Atan2(-facing.x, facing.y) * Mathf.Rad2Deg;
            body.transform.localRotation = Quaternion.RotateTowards(
                body.transform.localRotation,
                Quaternion.Euler(0f, 0f, target),
                turnSpeedDegPerSec * Time.deltaTime);
        }
    }
}
