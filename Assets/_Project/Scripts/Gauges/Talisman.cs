using Morae.Game.Core;
using Morae.Game.Data;
using UnityEngine;

namespace Morae.Game.Gauges
{
    /// <summary>
    /// 부적 1회 방어 (명세 §2). 붕괴/공황 트리거 순간 SaltCorners/Sanity가 TryIntercept를 호출한다.
    /// 발동: 트리거된 쪽의 복구(소금 전 귀퉁이 −1 또는 이성 +30) + TalismanBurned 발행. 2회째는 false(통과 = 게임오버).
    /// 발동 연출(talismanFxSec, 검게 탐)은 표현 계층이 TalismanBurned 구독으로 처리 — 게임플레이는 멈추지 않는다.
    /// 엔딩 분기: 미발동(Consumed=false) = 퍼펙트 (DoorInteractable이 읽음).
    /// </summary>
    public sealed class Talisman : MonoBehaviour
    {
        [SerializeField] private BalanceConfig config;
        [SerializeField] private SaltCorners salt;
        [SerializeField] private Sanity sanity;

        public bool Consumed { get; private set; }

        /// <summary>게임오버 트리거 가로채기. true = 방어 성공 (호출자는 게임오버를 내지 않는다).</summary>
        public bool TryIntercept(TalismanTrigger trigger)
        {
            if (Consumed)
            {
                Debug.Log($"[TALISMAN] 이미 소모됨 — {trigger} 통과");
                return false;
            }
            if (config == null)
            {
                Debug.LogError("[TALISMAN] BalanceConfig 미배선 — 방어 불가", this);
                return false;
            }

            Consumed = true;
            switch (trigger)
            {
                case TalismanTrigger.SealCollapse:
                    if (salt != null) salt.RestoreAll(config.TalismanSaltRestore);
                    break;
                case TalismanTrigger.Panic:
                    if (sanity != null) sanity.ForceRestore(config.TalismanSanityRestore);
                    break;
            }
            Debug.Log($"[TALISMAN] 부적 발동 — {trigger} 방어 (검게 탐, 연출 {config.TalismanFxSec:F0}s)");
            GameEvents.RaiseTalismanBurned();
            return true;
        }
    }
}
