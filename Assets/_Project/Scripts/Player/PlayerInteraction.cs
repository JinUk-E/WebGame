using System.Collections.Generic;
using Morae.Game.Data;
using Morae.Game.Interactions;
using UnityEngine;

namespace Morae.Game.Player
{
    /// <summary>
    /// 근접 대상 탐지 + E 문법(탭/홀드/채널) 구동 (architecture §1.1 상호작용 모듈).
    /// 탐지: 소품의 트리거 콜라이더에 플레이어 본체 콜라이더가 들어오면 후보 등록 (트리거 = 상호작용 범위).
    /// 한 번에 하나의 상호작용만 활성. 진행률은 읽기 프로퍼티로 노출 (InteractPrompt UI가 Epic 2에서 구독).
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerInteraction : MonoBehaviour
    {
        private readonly List<Interactable> _inRange = new List<Interactable>(4);
        private PlayerController _player;

        public Interactable ActiveTarget { get; private set; }
        public float HoldElapsed { get; private set; }
        public float HoldProgress01
        {
            get
            {
                if (ActiveTarget == null || ActiveTarget.Duration <= 0f) return 0f;
                return Mathf.Clamp01(HoldElapsed / ActiveTarget.Duration);
            }
        }

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // 트리거 진입/이탈은 핫패스가 아님 — GetComponentInParent 허용 (Update 안 탐색 금지 규칙과 구분)
            var interactable = other.GetComponentInParent<Interactable>();
            if (interactable != null && !_inRange.Contains(interactable))
            {
                _inRange.Add(interactable);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            var interactable = other.GetComponentInParent<Interactable>();
            if (interactable == null) return;
            _inRange.Remove(interactable);
            if (ActiveTarget == interactable)
            {
                End(completed: false); // 범위 이탈 = 취소 (홀드류는 이동 잠금이라 사실상 방어 코드)
            }
        }

        private void Update()
        {
            if (ActiveTarget != null)
            {
                TickActive();
                return;
            }

            if (!InputReader.InteractDown) return;

            Interactable target = FindNearestTarget();
            if (target == null) return;

            if (target.Kind == InteractionKind.Tap)
            {
                target.OnTap(_player);
            }
            else
            {
                Begin(target);
            }
        }

        private void Begin(Interactable target)
        {
            ActiveTarget = target;
            HoldElapsed = 0f;
            target.OnBegin(_player);
        }

        private void TickActive()
        {
            HoldElapsed += Time.deltaTime;
            bool held = InputReader.InteractHeld;

            switch (ActiveTarget.Kind)
            {
                case InteractionKind.HoldMaintain:
                    // 누르는 동안 유지 — 떼면 정상 종료
                    if (!held) { End(completed: true); return; }
                    ActiveTarget.OnHoldTick(_player, HoldElapsed);
                    break;

                case InteractionKind.HoldComplete:
                    if (!held) { End(completed: false); return; } // 조기 해제 = 취소 (걸쇠의 "마지막 관용" 포함)
                    ActiveTarget.OnHoldTick(_player, HoldElapsed);
                    if (HoldElapsed >= ActiveTarget.Duration) { End(completed: true); }
                    break;

                case InteractionKind.ChannelLocked:
                    // 취소 불가 (요강 5s 무방비)
                    ActiveTarget.OnHoldTick(_player, HoldElapsed);
                    if (HoldElapsed >= ActiveTarget.Duration) { End(completed: true); }
                    break;
            }
        }

        private void End(bool completed)
        {
            Interactable target = ActiveTarget;
            ActiveTarget = null;
            HoldElapsed = 0f;
            if (completed) target.OnComplete(_player);
            else target.OnCancel(_player);
        }

        private Interactable FindNearestTarget()
        {
            Interactable nearest = null;
            float nearestSqr = float.MaxValue;
            Vector2 origin = transform.position;

            for (int i = _inRange.Count - 1; i >= 0; i--)
            {
                Interactable candidate = _inRange[i];
                if (candidate == null) { _inRange.RemoveAt(i); continue; } // 파괴된 오브젝트 정리 (명시 null 비교)
                if (!candidate.CanInteract(_player)) continue;

                float sqr = ((Vector2)candidate.transform.position - origin).sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = candidate;
                }
            }
            return nearest;
        }
    }
}
