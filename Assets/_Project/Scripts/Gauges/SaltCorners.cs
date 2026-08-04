using Morae.Game.Core;
using Morae.Game.Data;
using UnityEngine;

namespace Morae.Game.Gauges
{
    /// <summary>
    /// 소금 4귀퉁이 0~2단계 (명세 §2). 인덱스 규약: 0=좌상 1=우상 2=좌하 3=우하 (CornerIndex).
    /// 전이: 공격 미상쇄 +1(AttackScheduler 직접 호출) / 기도 −1(PrayerInteractable) / 부적 전 귀퉁이 −1(Talisman).
    /// 전 귀퉁이 흑(2) = 봉인 붕괴 — Talisman.TryIntercept가 1회 가로채고, 소모돼 있으면 GameOver(SealCollapsed).
    /// 상태 변화는 CornerStageChanged 발행 — 표현 계층(귀퉁이 라이트·진행음)은 구독만.
    /// </summary>
    public sealed class SaltCorners : MonoBehaviour
    {
        [SerializeField] private Talisman talisman;
        [SerializeField] private Transform[] cornerTransforms = new Transform[CornerIndex.Count]; // FarthestFromPlayer 해석용

        private readonly int[] _stages = new int[CornerIndex.Count];

        public bool IsCollapsed { get; private set; }
        public int GetStage(int corner) => _stages[corner];
        /// <summary>흑(2단계) = 이미 죽은 결계 — 공격 대상 제외 (2026-08-04 결정: 죽은 귀퉁이 공격은 낭비).</summary>
        public bool IsDead(int corner) => _stages[corner] >= (int)CornerStage.Black;

        public Vector2 GetCornerPosition(int corner)
        {
            Transform t = cornerTransforms != null && corner < cornerTransforms.Length ? cornerTransforms[corner] : null;
            return t != null ? (Vector2)t.position : Vector2.zero;
        }

        /// <summary>
        /// 기준 위치에서 가장 먼 귀퉁이(dual이면 2곳)를 고른다 — P5 원거리 전조 (§2.2 FarthestFromPlayer).
        /// 흑화(사망) 귀퉁이는 후보 제외 — 살아있는 곳이 부족하면 그만큼 None.
        /// </summary>
        public void SelectFarthestCorners(Vector2 from, bool dual, out int cornerA, out int cornerB)
        {
            cornerA = CornerIndex.None;
            cornerB = CornerIndex.None;
            float bestA = -1f;
            float bestB = -1f;
            for (int i = 0; i < CornerIndex.Count; i++)
            {
                if (IsDead(i)) continue;
                float sqr = (GetCornerPosition(i) - from).sqrMagnitude;
                if (sqr > bestA)
                {
                    bestB = bestA; cornerB = cornerA;
                    bestA = sqr; cornerA = i;
                }
                else if (sqr > bestB)
                {
                    bestB = sqr; cornerB = i;
                }
            }
            if (!dual) cornerB = CornerIndex.None;
        }

        /// <summary>공격 미상쇄 오염 +1. 전 귀퉁이 흑이면 붕괴 처리 (부적 가로채기 경유).</summary>
        public void Contaminate(int corner)
        {
            if (IsCollapsed) return;
            if (_stages[corner] < (int)CornerStage.Black)
            {
                _stages[corner]++;
                GameEvents.RaiseCornerStageChanged(corner, _stages[corner]);
                Debug.Log($"[SALT] 귀퉁이 {corner} 오염 → {(CornerStage)_stages[corner]}");
            }
            else
            {
                Debug.Log($"[SALT] 귀퉁이 {corner} 이미 흑 — 단계 유지");
            }
            if (AllBlack()) HandleCollapse();
        }

        /// <summary>기도 정화 −1 (전조가 없을 때의 사후 정화 — 상쇄 판정은 AttackScheduler.TryCounter).</summary>
        public void Purify(int corner)
        {
            if (IsCollapsed) return;
            if (_stages[corner] <= 0)
            {
                Debug.Log($"[SALT] 귀퉁이 {corner} 이미 백 — 정화 효과 없음");
                return;
            }
            _stages[corner]--;
            GameEvents.RaiseCornerStageChanged(corner, _stages[corner]);
            Debug.Log($"[SALT] 귀퉁이 {corner} 정화 → {(CornerStage)_stages[corner]}");
        }

        /// <summary>부적 발동: 전 귀퉁이 −amount (명세 §2 talismanSaltRestore).</summary>
        public void RestoreAll(int amount)
        {
            for (int i = 0; i < CornerIndex.Count; i++)
            {
                int restored = Mathf.Max(0, _stages[i] - amount);
                if (restored == _stages[i]) continue;
                _stages[i] = restored;
                GameEvents.RaiseCornerStageChanged(i, restored);
            }
            Debug.Log($"[SALT] 부적 복구 — 전 귀퉁이 −{amount} → [{_stages[0]}{_stages[1]}{_stages[2]}{_stages[3]}]");
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
