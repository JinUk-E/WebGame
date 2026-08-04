using Morae.Game.Core;
using Morae.Game.Data;
using Morae.Game.Interactions;
using Morae.Game.Player;
using UnityEngine;

namespace Morae.Game.Gauges
{
    /// <summary>
    /// 이성 0~100 (명세 §2). 시작 = BalanceConfig.SanityMax.
    /// 하락: 공격 전조 −8(AttackScheduler가 ApplyDelta) / 연출 이벤트(EventDirector가 Epic 2에서 ApplyDelta) /
    ///       귀 대기·걸쇠 개방 중 −3/s / 페이즈 상시 드레인(CurrentPhaseDef.PassiveSanityDrain — P4 이후 0.5/s).
    /// 회복: TV 켜짐 +1/s, 이불 속 +3/s — 요의(UrgeActive) 동안 회복 무효 (하락은 그대로).
    /// 0 = 공황 — Talisman.TryIntercept가 1회 가로채고(+30), 소모돼 있으면 GameOver(Panic).
    /// 값 변화는 SanityChanged(0~1 정규화) 발행 — 표현(비네트·심박)은 구독만.
    /// </summary>
    public sealed class Sanity : MonoBehaviour
    {
        [SerializeField] private BalanceConfig config;
        [SerializeField] private PhaseSequencer sequencer;
        [SerializeField] private PlayerController player;
        [SerializeField] private TvInteractable tv;
        [SerializeField] private Talisman talisman;

        private bool _handlingZero;

        public bool IsRunning { get; private set; }
        public float Value { get; private set; }
        public float Max => config != null ? config.SanityMax : 100f;
        /// <summary>요의 발생~해소 동안 true — 회복 무효 (설정: EventDirector "urge" / 해소: JarInteractable).</summary>
        public bool UrgeActive { get; private set; }

        /// <summary>본편 시작 — GameFlowController가 호출.</summary>
        public void Begin()
        {
            if (config == null || player == null)
            {
                Debug.LogError("[SANITY] config/player 미배선 — 시작 불가", this);
                return;
            }
            Value = config.SanityMax;
            UrgeActive = false;
            IsRunning = true;
            GameEvents.RaiseSanityChanged(1f);
        }

        /// <summary>게임오버·엔딩 시 정지.</summary>
        public void Stop() => IsRunning = false;

        public void SetUrgeActive(bool active)
        {
            if (UrgeActive == active) return;
            UrgeActive = active;
            Debug.Log($"[SANITY] 요의 {(active ? "발생 — 회복 무효" : "해소 — 회복 재개")}");
        }

        /// <summary>
        /// 이산 증감 (전조 −8, 연출 −10 등). 양수 델타는 요의 중 무효.
        /// 부적 복구는 ForceRestore 사용 (회복 무효 규칙을 우회 — 게임오버 방어는 "회복"이 아니다).
        /// </summary>
        public void ApplyDelta(float delta)
        {
            if (!IsRunning) return;
            if (delta > 0f && UrgeActive) return;
            Apply(delta);
        }

        /// <summary>부적 발동 복구 (+30) — 요의 회복 무효를 우회.</summary>
        public void ForceRestore(float amount) => Apply(amount);

        private void Update()
        {
            if (!IsRunning) return;

            float dt = Time.deltaTime;
            float delta = 0f;

            PhaseDef phase = sequencer != null ? sequencer.CurrentPhaseDef : null;
            if (phase != null) delta -= phase.PassiveSanityDrain * dt;

            PlayerState state = player.State;
            if (state == PlayerState.ListeningAtDoor || state == PlayerState.OpeningDoor)
            {
                delta -= config.SanityDoorDrainPerSec * dt; // 걸쇠 개방 중에도 문에 붙어 있음 — 드레인 유지 (결정 기록)
            }

            if (!UrgeActive)
            {
                if (tv != null && tv.IsOn) delta += config.SanityTvRegenPerSec * dt;
                if (state == PlayerState.InBlanket) delta += config.SanityBlanketRegenPerSec * dt;
            }

            if (delta != 0f) Apply(delta);
        }

        private void Apply(float delta)
        {
            float next = Mathf.Clamp(Value + delta, 0f, Max);
            if (Mathf.Approximately(next, Value)) return;
            Value = next;
            GameEvents.RaiseSanityChanged(Max > 0f ? Value / Max : 0f);

            if (Value > 0f || _handlingZero || !IsRunning) return;

            // 이성 0 = 공황 — 부적 1회 가로채기, 2회째는 그대로 게임오버
            _handlingZero = true;
            if (talisman == null || !talisman.TryIntercept(TalismanTrigger.Panic))
            {
                Debug.Log("[SANITY] 이성 0 — 공황");
                IsRunning = false;
                GameEvents.RaiseGameOver(GameOverReason.Panic);
            }
            _handlingZero = false;
        }
    }
}
