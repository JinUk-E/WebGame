using Morae.Game.Core;
using Morae.Game.Data;
using UnityEngine;

namespace Morae.Game.Gauges
{
    /// <summary>
    /// 부적 — <b>비회복 60초 타이머</b> (v0.7). 방 벽에 한 장 걸려 있고, 소금이 더러운 동안만 탄다.
    ///
    /// <para>
    /// <b>이전 설계와 무엇이 다른가.</b> v0.6까지 부적은 게임오버를 1회 가로채는 목숨이었다(TryIntercept).
    /// 이제는 목숨이 아니라 <b>시간</b>이다. 오염된 귀퉁이가 하나라도 있으면 초당 1씩 타고, 다 타면 게임오버.
    /// 복구해도 되살아나지 않는다.
    /// </para>
    ///
    /// <para>
    /// <b>왜 비회복인가.</b> 리셋되는 타이머는 꾸물거림에 대가가 없다 — 8초를 허비하고 겨우 도착해도
    /// 다음 오염 때 다시 만땅이다. 비회복이면 늦은 1초가 영구히 사라지고 그게 화면에서 보이므로,
    /// "빨리 가야 한다"는 압박이 국소적으로 오히려 강해진다. 덤으로 <b>잔여 시간이 그대로 엔딩 등급</b>이 되어
    /// (DoorInteractable이 읽는다) 별도 판정 지표를 만들 필요가 없다.
    /// </para>
    ///
    /// <para>
    /// <b>소유권(§1.2).</b> "부적이 얼마나 탔는가"의 단일 소유자는 여기다. SaltCorners는 시간 축이 없는
    /// 순수 이산 클래스로 남겨 두고(그쪽에 Update를 넣으면 EditMode 테스트 전략이 무너진다),
    /// 오염 여부는 <b>폴링</b>으로 읽는다 — AttackScheduler가 PhaseSequencer를 읽는 것과 같은 관용구다.
    /// 게임플레이 내부에서 이벤트를 소비하지 않는다.
    /// </para>
    /// </summary>
    public sealed class Talisman : MonoBehaviour
    {
        [SerializeField] private BalanceConfig config;
        [SerializeField] private SaltCorners salt;

        /// <summary>
        /// 이벤트 발행 최소 변화폭. 표현 계층은 burn01을 4~8단계 스프라이트로 양자화하므로
        /// 매 프레임 발행할 이유가 없다 — 1/128이면 60초 기준 약 0.47초에 한 번이라 연출에 충분하다.
        /// </summary>
        private const float BurnEventEpsilon = 1f / 128f;

        private float _lastRaisedBurn01 = -1f;

        public bool IsRunning { get; private set; }

        /// <summary>남은 시간(초). 개문 시 엔딩 등급 판정에 쓰인다.</summary>
        public float RemainingSec { get; private set; }

        public float TotalSec => config != null ? config.TalismanTotalSec : 60f;

        /// <summary>탄 정도 0(멀쩡)~1(전소).</summary>
        public float Burn01 => TotalSec > 0f ? 1f - Mathf.Clamp01(RemainingSec / TotalSec) : 1f;

        /// <summary>임계 구간 — 표현 계층이 불씨·재·흔들림을 켜는 조건.</summary>
        public bool IsCritical =>
            IsRunning && config != null && RemainingSec <= config.TalismanCriticalRemainSec;

        /// <summary>본편 시작 — GameFlowController가 호출.</summary>
        public void Begin()
        {
            if (config == null || salt == null)
            {
                Debug.LogError("[TALISMAN] config/salt 미배선 — 시작 불가", this);
                return;
            }
            RemainingSec = config.TalismanTotalSec;
            _lastRaisedBurn01 = -1f;
            IsRunning = true;
            RaiseBurnIfChanged(force: true);
            Debug.Log($"[TALISMAN] 부적 {RemainingSec:F0}초 — 소금이 더러운 동안만 탄다 (복구해도 되살아나지 않음)");
        }

        /// <summary>게임오버·엔딩 시 정지.</summary>
        public void Stop() => IsRunning = false;

        private void Update()
        {
            if (!IsRunning) return;
            if (salt.ContaminatedCornerCount <= 0) return; // 전부 깨끗하면 타지 않는다

            float before = RemainingSec;
            RemainingSec = Mathf.Max(0f, RemainingSec - Time.deltaTime);
            if (Mathf.Approximately(before, RemainingSec)) return;

            RaiseBurnIfChanged(force: false);

            if (RemainingSec > 0f) return;

            // 전소 = 게임오버. GameOverReason.SealCollapsed는 v0.7에서 "부적 소진"으로 재정의됐다
            // (기존 "네 귀퉁이 전부 흑" 붕괴 판정은 삭제 — 부적이 항상 먼저 터져서 도달 불가능한 축이었다).
            IsRunning = false;
            Debug.Log("[TALISMAN] 부적 전소 — 게임오버");
            GameEvents.RaiseGameOver(GameOverReason.SealCollapsed);
        }

        private void RaiseBurnIfChanged(bool force)
        {
            float burn01 = Burn01;
            if (!force && Mathf.Abs(burn01 - _lastRaisedBurn01) < BurnEventEpsilon && burn01 < 1f) return;
            _lastRaisedBurn01 = burn01;
            GameEvents.RaiseTalismanBurnChanged(burn01);
        }
    }
}
