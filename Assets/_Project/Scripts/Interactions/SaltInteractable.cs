using Morae.Game.Core;
using Morae.Game.Data;
using Morae.Game.Gauges;
using Morae.Game.Player;
using UnityEngine;

namespace Morae.Game.Interactions
{
    /// <summary>
    /// 소금 뿌리기 (v0.7 — 이 게임의 핵심 동사). 더러워진 귀퉁이로 가서 E를 홀드하면 한 단계 정화된다.
    ///
    /// <para>
    /// <b>무엇을 대체했나.</b> v0.6까지는 불상 앞에서 E를 홀드한 채 방향키로 귀퉁이를 <b>겨눴다</b>.
    /// 조준이라는 두 번째 축이 있었고, 모바일에서는 같은 스틱이 상황에 따라 이동과 조준으로 의미가 바뀌었다.
    /// 이제는 축이 하나다 — 이동해서 가고, 거기서 누른다. 위험한 방향으로 <b>몸이 직접 가야 한다</b>는 점에서
    /// 원격 조준보다 공포 연출과도 맞는다.
    /// </para>
    ///
    /// <para>
    /// <b>진행도는 여기가 아니라 SaltCorners가 소유한다.</b> 손을 떼도 귀퉁이에 남아야 하기 때문이다
    /// (자세한 이유는 SaltCorners 주석). 여기서는 매 틱 밀어 넣고, 1에 도달하면 Purify를 부른다.
    /// </para>
    /// </summary>
    public sealed class SaltInteractable : Interactable
    {
        [SerializeField] private SaltCorners salt;
        [SerializeField] private int cornerIndex = CornerIndex.TopLeft;

        /// <summary>
        /// 배선이 비면 Awake에서 한 번 찾아 채운다.
        /// <para>
        /// <b>왜 방어하는가</b>: 이 컴포넌트는 Room.prefab 안에 살고 <see cref="SaltCorners"/>는 씬의 Systems에 있다.
        /// 프리팹은 씬 오브젝트를 참조할 수 없어서 이 필드는 <b>인스턴스 오버라이드</b>로만 존재하는데,
        /// 프리팹 구조를 고쳐 다시 저장하면 그 오버라이드가 조용히 사라진다(실제로 한 번 그렇게 날아갔다).
        /// 그러면 CanInteract가 항상 false가 되어 <b>소금이 후보에서 통째로 빠지고</b>,
        /// 하필 그 자리에서 문이 유일 후보가 되어 잡힌다 — 증상이 원인과 전혀 안 닮은 종류의 실패다.
        /// 탐색은 Awake 1회뿐이라 핫패스 금지 규칙과 무관하다.
        /// </para>
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            if (salt != null) return;
            salt = FindFirstObjectByType<SaltCorners>();
            if (salt == null) Debug.LogError($"[SALT] {name}: SaltCorners를 찾지 못했다 — 소금 뿌리기 불가", this);
            else Debug.LogWarning($"[SALT] {name}: salt 미배선 — 런타임 탐색으로 대체 (씬에서 배선할 것)", this);
        }

        /// <summary>핵심 동사이므로 무조건 1순위 — 문·TV 옆에 서 있어도 소금이 이긴다.</summary>
        public override int Priority => 100;

        public override float Duration => Config.SaltHoldSec;

        /// <summary>귀퉁이에 남아 있던 진행도에서 이어서 시작한다.</summary>
        public override float InitialElapsed =>
            salt != null ? salt.GetPurifyProgress01(cornerIndex) * Duration : 0f;

        public override string PromptLabel => "소금 뿌리기";

        /// <summary>이미 깨끗하면 상호작용 대상이 아니다.</summary>
        public override bool CanInteract(PlayerController player)
            => player.IsMovable && salt != null && salt.IsContaminated(cornerIndex);

        public override void OnBegin(PlayerController player)
        {
            player.TryEnterActionState(PlayerState.Salting);
            Debug.Log($"[SALT] 귀퉁이 {cornerIndex} 뿌리기 시작 — {Duration:F1}s (이성 초당 감소)");
        }

        public override void OnHoldTick(PlayerController player, float heldSeconds)
        {
            float progress = Duration > 0f ? Mathf.Clamp01(heldSeconds / Duration) : 1f;
            if (salt != null) salt.SetPurifyProgress(cornerIndex, progress);
            // 심박·비네트가 이걸 구독한다 — 이성 값 변화(초당 0.02)로는 체감이 불가능하다
            GameEvents.RaiseSaltChannelChanged(cornerIndex, progress);
        }

        public override void OnComplete(PlayerController player)
        {
            GameEvents.RaiseSaltChannelChanged(cornerIndex, 0f);
            player.ReturnToIdle();
            if (salt != null) salt.Purify(cornerIndex);
        }

        public override void OnCancel(PlayerController player)
        {
            // 진행도는 SaltCorners에 남는다 (감쇠하며 사라짐) — 여기서는 바만 내리고 상태만 되돌린다
            GameEvents.RaiseSaltChannelChanged(cornerIndex, 0f);
            player.ReturnToIdle();
            Debug.Log($"[SALT] 귀퉁이 {cornerIndex} 뿌리기 중단 — 진행도 유지 (감쇠 시작)");
        }
    }
}
