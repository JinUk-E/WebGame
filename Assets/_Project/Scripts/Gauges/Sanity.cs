using Morae.Game.Core;
using Morae.Game.Data;
using Morae.Game.Interactions;
using Morae.Game.Player;
using UnityEngine;

namespace Morae.Game.Gauges
{
    /// <summary>
    /// 이성 0~100. 시작 = BalanceConfig.SanityMax.
    /// 하락: 공격 전조 −8(AttackScheduler) / 연출 이벤트(EventDirector) /
    ///       <b>소금 뿌리는 중 −2/s</b> / 페이즈 상시 드레인(CurrentPhaseDef.PassiveSanityDrain) /
    ///       진짜 신호를 듣고도 문을 안 여는 동안 −2/s.
    /// 회복: TV 켜짐 +1/s, 이불 속 +3/s (단 <b>상한 75까지만</b>).
    /// 0 = 공황 → 즉시 GameOver(Panic).
    ///
    /// <para>
    /// <b>v0.7 변경 셋.</b>
    /// ① <b>부적 가로채기 제거</b> — 이성 0이 곧 죽음이다. 부적은 이제 목숨이 아니라 타이머라 여기 개입하지 않는다.
    /// ② <b>요의(UrgeActive) 제거</b> — 해소 경로가 요강 상호작용 하나뿐이었는데 그게 사라졌다.
    ///    이벤트만 남기면 요의가 영구 지속돼 회복이 완전히 차단되므로 사슬 전체를 걷어냈다.
    /// ③ <b>이불 회복 상한</b> — 무제한이면 420초 중 294초를 이불에서 보내는 게 최적해가 되어
    ///    이성 축이 통째로 사라진다. 상한을 두면 이불은 만회 수단이 아니라 바닥 유지 수단이 된다.
    /// 흑화 개수 상시 드레인도 뺐다 — 오염은 이미 부적을 태워서 벌을 받는다(이중 처벌).
    /// </para>
    /// </summary>
    public sealed class Sanity : MonoBehaviour
    {
        [SerializeField] private BalanceConfig config;
        [SerializeField] private PhaseSequencer sequencer;
        [SerializeField] private PlayerController player;
        [SerializeField] private TvInteractable tv;

        private bool _handlingZero;
        private float _saltingAccum;   // 뿌리는 동안 누적한 손실 — 손을 뗄 때 한 번에 발행
        // 진짜 신호가 온 뒤 아직 문을 열지 않은 상태 — 이 동안 추가 드레인 (판별 축에 실질 위험 부여)
        private bool _trueSignalPending;

        private void OnEnable() => GameEvents.TrueSignalStarted += HandleTrueSignalStarted;
        private void OnDisable() => GameEvents.TrueSignalStarted -= HandleTrueSignalStarted;

        private void HandleTrueSignalStarted()
        {
            if (!IsRunning) return;
            _trueSignalPending = true;
            Debug.Log("[SANITY] 진짜 신호 — 무응답 드레인 시작");
        }

        public bool IsRunning { get; private set; }
        public float Value { get; private set; }
        public float Max => config != null ? config.SanityMax : 100f;

        /// <summary>본편 시작 — GameFlowController가 호출.</summary>
        public void Begin()
        {
            if (config == null || player == null)
            {
                Debug.LogError("[SANITY] config/player 미배선 — 시작 불가", this);
                return;
            }
            Value = config.SanityMax;
            _trueSignalPending = false;
            IsRunning = true;
            GameEvents.RaiseSanityChanged(1f);
        }

        /// <summary>게임오버·엔딩 시 정지.</summary>
        public void Stop() => IsRunning = false;

        /// <summary>
        /// 이산 증감 (전조 −8, 연출 −10 등). 감소면 <see cref="GameEvents.SanityLost"/>를 함께 발행한다 —
        /// 값 추종만으로는 이 크기의 손실이 화면에서 보이지 않는다(GameEvents 주석 참조).
        /// </summary>
        public void ApplyDelta(float delta)
        {
            if (!IsRunning) return;
            float before = Value;
            Apply(delta);
            RaiseLossIfAny(before);
        }

        /// <summary>실제로 줄어든 만큼을 정규화해 발행. 클램프로 덜 깎였으면 그 값이 나간다.</summary>
        private void RaiseLossIfAny(float before)
        {
            float lost = before - Value;
            if (lost <= 0f || Max <= 0f) return;
            GameEvents.RaiseSanityLost(lost / Max);
        }

        private void Update()
        {
            if (!IsRunning) return;

            float dt = Time.deltaTime;
            float delta = 0f;

            PhaseDef phase = sequencer != null ? sequencer.CurrentPhaseDef : null;
            if (phase != null) delta -= phase.PassiveSanityDrain * dt;

            // 진짜 신호를 듣고도 문을 열지 않는 동안 — "기다리기만 하면 안전"을 깬다
            if (_trueSignalPending) delta -= config.TrueSignalIgnoreDrainPerSec * dt;

            // [v0.7] 귀 대기 −3/s 제거. 두 가지 이유다:
            //   ① 체감이 안 된다 — 초당 0.03의 정규화 변화라 비네트도 심박도 움직이지 않는다.
            //      대가는 보여야 대가고, 안 보이는 감소는 "이유 없이 죽었다"로만 남는다.
            //   ② 이제 문 앞에 머무는 진짜 대가가 따로 있다 — 그동안 소금이 더러워지면 부적이 탄다.
            //      시간 자체가 자원이 된 뒤로 이 드레인은 같은 벌을 두 번 물리는 것에 가깝다.
            //   진짜 신호 무응답 드레인(_trueSignalPending)은 남긴다 — 그건 "듣고도 안 여는" 판단에 붙은 벌이라
            //   성격이 다르고, 문 앞에 있든 없든 걸린다.
            PlayerState state = player.State;
            bool salting = state == PlayerState.Salting;
            if (salting)
            {
                // v0.7 — 소금을 뿌리는 대가. 홀드 1.5s × 3.0 = 1회당 −4.5.
                // 매 프레임 손실은 너무 잘아서 연출이 안 된다 — 뿌리는 동안 모았다가
                // 손을 뗀 순간 **한 덩어리로** SanityLost를 낸다. "다 뿌리고 나니 대가를 치렀다"가 한 번에 읽힌다.
                float drain = config.SaltSanityDrainPerSec * dt;
                delta -= drain;
                _saltingAccum += drain;
            }
            else if (_saltingAccum > 0f)
            {
                if (Max > 0f) GameEvents.RaiseSanityLost(_saltingAccum / Max);
                _saltingAccum = 0f;
            }

            if (tv != null && tv.IsOn) delta += config.SanityTvRegenPerSec * dt;
            if (state == PlayerState.InBlanket) delta += BlanketRegenDelta(dt);

            if (delta != 0f) Apply(delta);
        }

        /// <summary>이불 회복 — 상한(SanityBlanketRegenCeiling)을 넘지 않는 만큼만.</summary>
        private float BlanketRegenDelta(float dt)
        {
            float ceiling = config.SanityBlanketRegenCeiling;
            if (Value >= ceiling) return 0f;
            return Mathf.Min(config.SanityBlanketRegenPerSec * dt, ceiling - Value);
        }

        private void Apply(float delta)
        {
            float next = Mathf.Clamp(Value + delta, 0f, Max);
            if (Mathf.Approximately(next, Value)) return;
            Value = next;
            GameEvents.RaiseSanityChanged(Max > 0f ? Value / Max : 0f);

            if (Value > 0f || _handlingZero || !IsRunning) return;

            _handlingZero = true;
            Debug.Log("[SANITY] 이성 0 — 공황");
            IsRunning = false;
            GameEvents.RaiseGameOver(GameOverReason.Panic);
            _handlingZero = false;
        }
    }
}
