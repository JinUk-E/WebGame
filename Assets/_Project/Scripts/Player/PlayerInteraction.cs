using System.Collections.Generic;
using Morae.Game.Core;
using Morae.Game.Data;
using Morae.Game.Interactions;
using UnityEngine;

namespace Morae.Game.Player
{
    /// <summary>
    /// 근접 대상 탐지 + <b>E 홀드 단일 문법</b> 구동 (v0.7).
    /// 탐지: 소품의 트리거 콜라이더에 플레이어 본체 콜라이더가 들어오면 후보 등록.
    /// 한 번에 하나의 상호작용만 활성. 진행률은 읽기 프로퍼티로 노출 (InteractPromptView가 구독).
    ///
    /// <para>
    /// <b>선택 규칙이 바뀌었다.</b> v0.6까지는 "범위 안 최근접 하나"였는데, 소금 4곳이 상호작용 대상이 되면서
    /// 그 규칙이 사람을 죽인다 — <see cref="Interactable.Priority"/> 주석 참조. 이제 (우선순위 ↓, 거리 ↑)
    /// 사전식으로 고르고, 거리도 transform 거리가 아니라 <b>콜라이더 간 거리</b>로 잰다.
    /// 콜라이더 거리는 겹치면 음수라, 소금을 밟고 서면 항상 0 이하고 문 가장자리는 양수가 된다 —
    /// 탐지 지표(면적)와 선택 지표가 같아져서 편향이 사라진다.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerInteraction : MonoBehaviour
    {
        // 게임 흐름 게이트. 프롤로그 중에는 소금 뿌리기만 허용한다 — 게이트가 없으면 프롤로그 동안
        // 문·TV·이불이 전부 살아 있고, 프롤로그 중 개문은 GameFlowController가 무시하는 반면 문은 Open으로 남아
        // **문이 열린 채로 본편이 시작**된다.
        [SerializeField] private GameFlowController flow;

        private readonly List<Interactable> _inRange = new List<Interactable>(4);
        private PlayerController _player;
        private Collider2D _bodyCollider;

        public Interactable ActiveTarget { get; private set; }
        public float HoldElapsed { get; private set; }

        /// <summary>프롬프트 표시용 — 진행 중이면 그 대상, 아니면 범위 내 후보 (InteractPromptView가 소비).</summary>
        public Interactable CurrentCandidate => ActiveTarget != null ? ActiveTarget : FindTarget();

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
            _bodyCollider = GetComponent<Collider2D>(); // 핫패스 탐색 금지 — 여기서 한 번만
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // 트리거 진입/이탈은 핫패스가 아님 — GetComponentInParent 허용
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
                End(completed: false); // 범위 이탈 = 취소 (홀드 중엔 이동이 잠기므로 사실상 방어 코드)
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

            Interactable target = FindTarget();
            if (target != null) Begin(target);
        }

        private void Begin(Interactable target)
        {
            ActiveTarget = target;
            // 부분 진행도 이어받기 — 소금은 귀퉁이에 남아 있던 값에서 시작한다
            HoldElapsed = Mathf.Clamp(target.InitialElapsed, 0f, Mathf.Max(0f, target.Duration));
            target.OnBegin(_player);
        }

        /// <summary>
        /// 문법이 하나로 합쳐졌다 — 옛 switch 3분기가 릴리스 처리 한 갈래로 줄었다.
        /// Duration == 0이면 첫 틱에 <c>HoldElapsed >= 0</c>이 성립해 즉시 완료된다(옛 Tap).
        /// </summary>
        private void TickActive()
        {
            HoldElapsed += Time.deltaTime;

            if (!InputReader.InteractHeld && ActiveTarget.Cancelable)
            {
                End(completed: ActiveTarget.CompleteOnRelease);
                return;
            }

            ActiveTarget.OnHoldTick(_player, HoldElapsed);
            if (HoldElapsed >= ActiveTarget.Duration) End(completed: true);
        }

        private void End(bool completed)
        {
            Interactable target = ActiveTarget;
            ActiveTarget = null;
            HoldElapsed = 0f;
            if (completed) target.OnComplete(_player);
            else target.OnCancel(_player);
        }

        /// <summary>
        /// 게임 흐름 상태별 허용 범위.
        /// MainLoop = 전부 / Prologue = 소금 뿌리기만(강제 학습에 필요) / 그 외 = 없음.
        /// flow 미배선 시엔 전부 허용으로 떨어진다 — 게이트는 회귀 방어지 필수 의존이 아니다.
        /// <para>
        /// 프롤로그 <b>대사 구간</b>은 그 소금마저 잠근다 — 대사를 넘기는 클릭·탭·E가 소금 앞에 서 있다는
        /// 이유로 뿌리기를 시작하면 안 된다. 잠금이 풀리는 건 대사가 끝나고 학습 구간이 시작될 때다.
        /// </para>
        /// </summary>
        private bool AllowedInCurrentState(Interactable candidate)
        {
            if (flow == null) return true;
            switch (flow.State)
            {
                case GameState.MainLoop: return true;
                case GameState.Prologue: return !flow.PrologueDialogueLock && candidate is SaltInteractable;
                default: return false;
            }
        }

        /// <summary>(우선순위 내림차순, 콜라이더 거리 오름차순) 사전식 선택.</summary>
        private Interactable FindTarget()
        {
            Interactable best = null;
            int bestPriority = int.MinValue;
            float bestDistance = float.MaxValue;

            for (int i = _inRange.Count - 1; i >= 0; i--)
            {
                Interactable candidate = _inRange[i];
                if (candidate == null) { _inRange.RemoveAt(i); continue; } // 파괴된 오브젝트 정리
                if (!AllowedInCurrentState(candidate)) continue;
                if (!candidate.CanInteract(_player)) continue;

                int priority = candidate.Priority;
                if (priority < bestPriority) continue;

                float distance = DistanceTo(candidate);
                if (priority == bestPriority && distance >= bestDistance) continue;

                bestPriority = priority;
                bestDistance = distance;
                best = candidate;
            }
            return best;
        }

        /// <summary>
        /// 플레이어 콜라이더에서 대상 콜라이더까지의 거리 (겹치면 음수).
        /// 양쪽 콜라이더는 각자 Awake에서 캐싱된 것 — 여기서 탐색하지 않는다.
        /// 캐시가 비면 transform 거리로 떨어진다 (배선 실수로 선택이 완전히 멈추지는 않게).
        /// </summary>
        private float DistanceTo(Interactable candidate)
        {
            Collider2D targetCollider = candidate.RangeCollider;
            if (_bodyCollider != null && targetCollider != null)
            {
                return _bodyCollider.Distance(targetCollider).distance;
            }
            return Vector2.Distance(transform.position, candidate.transform.position);
        }
    }
}
