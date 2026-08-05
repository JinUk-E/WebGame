using Morae.Game.Core;
using Morae.Game.Data;
using UnityEngine;

namespace Morae.Game.Gauges
{
    /// <summary>
    /// 소금 4귀퉁이 0~2단계 + 흑화 심화 플래그 (명세 §2·v0.3). 인덱스 규약: 0=좌상 1=우상 2=좌하 3=우하 (CornerIndex).
    /// 전이: 공격 미상쇄 +1(AttackScheduler 직접 호출) / 기도 −1(PrayerInteractable) / 부적 전 귀퉁이 −1(Talisman).
    /// v0.3 심화 스택: 흑(2) 귀퉁이 추가 피격 → 심화 플래그(무중첩) — 기도 채널 ×1.5. 흑→회 정화 시 해제.
    ///   흑+심화(포화) 귀퉁이만 공격 대상 제외 — 흑 미심화는 여전히 유효 타깃 (피격 = 심화).
    /// 전 귀퉁이 흑(2) = 봉인 붕괴 (심화 여부 무관 — 기존 판정 그대로). Talisman.TryIntercept가 1회 가로챈다.
    /// 상태 변화는 CornerStageChanged 발행 — 심화는 stage=3(CornerStage.DeepBlack)으로 표기 (표현 계층 구분용).
    /// </summary>
    public sealed class SaltCorners : MonoBehaviour
    {
        [SerializeField] private Talisman talisman;
        [SerializeField] private Transform[] cornerTransforms = new Transform[CornerIndex.Count]; // FarthestFromPlayer 해석용

        private readonly int[] _stages = new int[CornerIndex.Count];
        private readonly bool[] _deepened = new bool[CornerIndex.Count];

        public bool IsCollapsed { get; private set; }
        public int GetStage(int corner) => _stages[corner];
        /// <summary>흑화 심화 플래그 (v0.3) — 기도 채널 3s → ×1.5 (PrayerInteractable이 읽음).</summary>
        public bool IsDeepened(int corner) => _deepened[corner];
        /// <summary>흑+심화 = 포화 — 추가 피격이 무의미해 공격 대상에서 제외 (v0.3: 흑 미심화는 유효 타깃 — 피격 시 심화).</summary>
        public bool IsSaturated(int corner) => _stages[corner] >= (int)CornerStage.Black && _deepened[corner];

        public Vector2 GetCornerPosition(int corner)
        {
            Transform t = cornerTransforms != null && corner < cornerTransforms.Length ? cornerTransforms[corner] : null;
            return t != null ? (Vector2)t.position : Vector2.zero;
        }

        /// <summary>
        /// 기준 위치에서 먼 순서로 살아있는(비포화) 귀퉁이를 count곳까지 고른다 (FarthestFromPlayer 해석).
        /// buffer 길이 ≥ count. 반환 = 실제 채운 수 (살아있는 곳이 부족하면 그만큼만).
        /// </summary>
        public int SelectFarthestCorners(Vector2 from, int count, int[] buffer)
        {
            int filled = 0;
            for (int i = 0; i < CornerIndex.Count; i++)
            {
                if (IsSaturated(i)) continue;
                float sqr = (GetCornerPosition(i) - from).sqrMagnitude;

                // 삽입 정렬 (최대 4개 — 할당 없음)
                int insert = filled;
                for (int k = 0; k < filled; k++)
                {
                    if (sqr > (GetCornerPosition(buffer[k]) - from).sqrMagnitude)
                    {
                        insert = k;
                        break;
                    }
                }
                int limit = Mathf.Min(filled + 1, count);
                for (int k = limit - 1; k > insert; k--) buffer[k] = buffer[k - 1];
                if (insert < count) buffer[insert] = i;
                filled = limit;
            }
            return filled;
        }

        /// <summary>구 시그니처 호환 (최대 2곳) — 기존 테스트·호출부용 래퍼.</summary>
        public void SelectFarthestCorners(Vector2 from, bool dual, out int cornerA, out int cornerB)
        {
            var buffer = new int[2]; // 핫패스 아님 — 발동 프레임 1회
            int filled = SelectFarthestCorners(from, dual ? 2 : 1, buffer);
            cornerA = filled > 0 ? buffer[0] : CornerIndex.None;
            cornerB = filled > 1 ? buffer[1] : CornerIndex.None;
        }

        /// <summary>
        /// 공격 미상쇄 오염: 백/회 → +1, 흑 미심화 → 심화 플래그(stage=3 발행), 흑+심화 → 무효 (v0.3).
        /// 전 귀퉁이 흑이면 붕괴 처리 (부적 가로채기 경유 — 심화 여부 무관).
        /// </summary>
        public void Contaminate(int corner)
        {
            if (IsCollapsed) return;
            if (_stages[corner] < (int)CornerStage.Black)
            {
                _stages[corner]++;
                GameEvents.RaiseCornerStageChanged(corner, _stages[corner]);
                Debug.Log($"[SALT] 귀퉁이 {corner} 오염 → {(CornerStage)_stages[corner]}");
            }
            else if (!_deepened[corner])
            {
                _deepened[corner] = true;
                GameEvents.RaiseCornerStageChanged(corner, (int)CornerStage.DeepBlack);
                Debug.Log($"[SALT] 귀퉁이 {corner} 흑화 심화 — 기도 채널 연장 (무중첩)");
            }
            else
            {
                Debug.Log($"[SALT] 귀퉁이 {corner} 이미 흑+심화 — 변화 없음");
            }
            if (AllBlack()) HandleCollapse();
        }

        /// <summary>기도 정화 −1 (전조가 없을 때의 사후 정화 — 상쇄 판정은 AttackScheduler.TryCounter). 흑→회 시 심화 해제.</summary>
        public void Purify(int corner)
        {
            if (IsCollapsed) return;
            if (_stages[corner] <= 0)
            {
                Debug.Log($"[SALT] 귀퉁이 {corner} 이미 백 — 정화 효과 없음");
                return;
            }
            _stages[corner]--;
            ClearDeepenedIfBelowBlack(corner);
            GameEvents.RaiseCornerStageChanged(corner, _stages[corner]);
            Debug.Log($"[SALT] 귀퉁이 {corner} 정화 → {(CornerStage)_stages[corner]}");
        }

        /// <summary>부적 발동: 전 귀퉁이 −amount (명세 §2 talismanSaltRestore). 흑→회 복구도 심화 해제 (v0.3).</summary>
        public void RestoreAll(int amount)
        {
            for (int i = 0; i < CornerIndex.Count; i++)
            {
                int restored = Mathf.Max(0, _stages[i] - amount);
                if (restored == _stages[i]) continue;
                _stages[i] = restored;
                ClearDeepenedIfBelowBlack(i);
                GameEvents.RaiseCornerStageChanged(i, restored);
            }
            Debug.Log($"[SALT] 부적 복구 — 전 귀퉁이 −{amount} → [{_stages[0]}{_stages[1]}{_stages[2]}{_stages[3]}]");
        }

        private void ClearDeepenedIfBelowBlack(int corner)
        {
            if (_deepened[corner] && _stages[corner] < (int)CornerStage.Black)
            {
                _deepened[corner] = false;
                Debug.Log($"[SALT] 귀퉁이 {corner} 심화 해제 (흑→회 정화)");
            }
        }

        private bool AllBlack()
        {
            for (int i = 0; i < CornerIndex.Count; i++)
            {
                if (_stages[i] < (int)CornerStage.Black) return false;
            }
            return true;
        }

        private void HandleCollapse()
        {
            if (talisman != null && talisman.TryIntercept(TalismanTrigger.SealCollapse))
            {
                return; // 부적이 가로챔 — RestoreAll로 붕괴 조건 해소됨
            }
            IsCollapsed = true;
            Debug.Log("[SALT] 봉인 붕괴 — 전 귀퉁이 흑");
            GameEvents.RaiseGameOver(GameOverReason.SealCollapsed);
        }
    }
}
