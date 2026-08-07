using Morae.Game.Core;
using Morae.Game.Data;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 조명 연출 (표현 계층 — D3. architecture §3.1 조명 골격의 런타임 제어).
    /// - 여명: 창밖 밝기 = 진실 채널 (§2 — 소리는 흉내 내도 빛은 못 낸다). Dawn01은 ClockView 전례대로
    ///   시퀀서 직접 읽기 (표현 계층의 읽기 전용 참조 — 이벤트가 없는 연속값).
    /// - TV 광원: TVToggled 구독. 귀퉁이 광원: CornerStageChanged 구독 — 오염될수록 어두워진다.
    ///
    /// v0.5 §1 — 흑화 귀퉁이당 즉시 대가 중 **실내 전역 조도**를 이 컴포넌트가 소유한다.
    ///   실내등 = clamp(base + 여명보정 + PhaseDef.roomLightBias − penalty×흑개수, minRoomLight, ∞)
    ///   ⚠ 페이즈 bias와 흑화 감광을 **합산한 뒤 한 번만** 클램프한다 (따로 클램프 = 이중 감광 → 암전).
    ///   흑 개수는 CornerStageChanged로 센다 — 표현 계층은 게임플레이를 직접 참조하지 않는다 (§1.2).
    ///
    /// 감광 예외 3종 (절대 준수):
    ///   ① 창밖 여명(windowDawnLight) — 진실 채널. 소금 상태·bias 어느 것도 섞지 않는다.
    ///   ② 불상 촛불(buddhaCandleLight) — 항상 일정. 암전 시 "기도하러 갈 곳"이 등대가 되어 행동을 지시한다.
    ///   ③ 공격 전조 점멸(telegraphIntensity) — 감광 무관 원래 강도. 어두울수록 화면에서 가장 밝은 것이 된다.
    ///
    /// v0.6.1 — **프롤로그 학습 구간 한정** 스포트라이트(TrainingModeChanged 구독): 실내 전역광 ×trainingRoomDimScale,
    ///   촛불 ×(1+trainingCandleBoost). 예외 3종을 깨지 않는다(여명 무개입 / 촛불은 위로만 / 전조는 원래 강도)이며
    ///   배율은 클램프 **전에** 곱해 minRoomLight 바닥을 지킨다. 학습이 끝나면 배율이 정확히 1로 돌아온다
    ///   (<see cref="Core.TrainingStageModel"/> — 본편에 잔여 감광이 남으면 밸런스가 통째로 어긋난다).
    /// </summary>
    public sealed class LightingController : MonoBehaviour
    {
        [SerializeField] private PhaseSequencer sequencer;
        [SerializeField] private BalanceConfig config;
        [SerializeField] private Light2D globalLight;
        [SerializeField] private Light2D tvLight;
        [SerializeField] private Light2D windowDawnLight;
        [SerializeField] private Light2D buddhaCandleLight;  // 예외② — 상수 밝기 등대
        [SerializeField] private Light2D[] cornerLights = new Light2D[CornerIndex.Count];

        [SerializeField] private float dawnMaxIntensity = 2.5f;   // 여명 최대 (Dawn01 = 1)
        [SerializeField] private float globalBase = 0.12f;        // §3.1 골격값
        [SerializeField] private float globalDawnBoost = 0.18f;   // 아침이 방 전체를 서서히 밝힌다
        [SerializeField] private float globalMinIntensity = 0.05f; // config 미배선 시의 폴백 바닥
        [SerializeField] private float tvIntensity = 1.1f;
        [SerializeField] private float cornerBaseIntensity = 0.25f; // 단계별 ×1 / ×0.45 / ×0.1
        [SerializeField] private float candleIntensity = 0.55f;     // 예외② 상수
        [SerializeField] private float telegraphIntensity = 1.0f;   // 예외③ — 감광과 무관한 고정 강도
        [SerializeField] private float telegraphPulseHz = 3f;       // SaltCornersView의 적색 펄스와 같은 주기

        [Header("v0.6 — 결계 상태를 밝기가 아니라 색으로")]
        // 흑화 귀퉁이를 ×0.1로 끄면 "위험한 곳일수록 안 보이는" 역전이 생긴다. 끄지 말고 색을 바꾼다:
        // 결계가 꺼진 게 아니라 **다른 것이 켜졌다**로 읽혀야 한다.
        [SerializeField] private Color cornerWardColor = new Color(0.85f, 0.9f, 1f);   // 평시 — 차가운 결계광
        [SerializeField] private Color cornerBreachColor = new Color(1f, 0.25f, 0.2f); // 흑화 — 붉은 균열광
        [SerializeField] private float breachFactor = 0.5f;         // 흑화 시 강도 배율 (평시 대비)
        [SerializeField] private float breachPulseHz = 0.8f;        // 심화 맥동 — SaltCornersView와 같은 주기
        // 예외② 유지: 촛불은 **아래로는 절대 안 내려간다**. 전조 때만 위로 튀어 "이리로 와라"를 지시한다.
        [SerializeField] private float candleFlareSec = 0.6f;
        [SerializeField] private float candleFlareBoost = 0.9f;

        [Header("v0.6.1 — 학습 구간 스포트라이트 (프롤로그 한정)")]
        // 어두운 방에서 후광만으로는 "저기가 목적지"가 안 읽힌다. 학습 동안만 실내 전역광을 한 단계 더 내리고
        // 불상 촛불(반경 2.8u 포인트 라이트)을 올려 **불상 주변만** 남긴다 — 시선이 갈 곳이 하나가 된다.
        // ⚠ 감광 예외 3종은 그대로다: 창밖 여명 무개입 / 촛불은 위로만 / 전조 점멸은 원래 강도.
        //   배율은 minRoomLight 클램프 **전에** 곱해지므로 바닥도 뚫리지 않는다 (CornerPenaltyModel).
        //   본편 잔여 방지: 배율의 소유는 TrainingStageModel이고, 학습이 꺼지면 정확히 1을 돌려준다.
        [SerializeField, Range(TrainingStageModel.MinDimScale, 1f)] private float trainingRoomDimScale = 0.55f;
        [SerializeField] private float trainingCandleBoost = 0.7f;

#if UNITY_EDITOR
        // 이 블록은 에디터에서만 컴파일된다 — 빌드 산출물에는 필드도 로직도 남지 않는다.
        // 컨트롤러가 매 프레임 전역광을 덮어쓰기 때문에 인스펙터로 밝기를 올려도 즉시 되돌아가던 작업 불편을 푼다.
        [Header("에디터 전용 — 빌드 무관")]
        [Tooltip("에디터 전용: 실내 전역광을 코드가 제어하지 않음(작업용). 빌드 무관")]
        [SerializeField] private bool editorFreeLight;
        [Tooltip("에디터 전용: 계산된 실내 전역광에 더해지는 가산치(플레이 중 실시간). 빌드 무관")]
        [SerializeField, Range(-0.5f, 1f)] private float editorLightBoost;
        private bool _editorOverrideWarned;
#endif

        private readonly int[] _stages = new int[CornerIndex.Count];
        // corner당 **개수**로 센다 — 같은 귀퉁이에 전조가 2개 겹칠 수 있어(AttackScheduler.RetargetToAvailable)
        // 단일 만료 시각으로 두면 첫 판정이 두 번째 전조의 점멸까지 꺼버린다.
        private readonly int[] _telegraphCount = new int[CornerIndex.Count];
        private int _blackCount;
        private float _smoothedGlobal = -1f; // 첫 프레임은 목표값으로 스냅 (페이드인 없이 시작)
        private float _candleFlareUntil;     // v0.6 — 전조 시 촛불이 위로만 튀는 구간
        private bool _trainingActive;        // v0.6.1 — 프롤로그 학습 구간 스포트라이트
        private float _smoothedCandleScale = 1f; // 촛불 배율도 러프 — 학습 종료 시 뚝 끊기지 않게

        private float MinRoomLight => config != null ? config.MinRoomLight : globalMinIntensity;
        private float LightPenalty => config != null ? config.BlackCornerLightPenalty : 0f;
        private float SmoothSec => config != null ? config.RoomLightSmoothSec : 0.3f;

        private void OnEnable()
        {
            GameEvents.TVToggled += HandleTvToggled;
            GameEvents.CornerStageChanged += HandleCornerStage;
            GameEvents.AttackTelegraphStarted += HandleTelegraphStarted;
            GameEvents.AttackResolved += HandleAttackResolved;
            GameEvents.TrainingModeChanged += HandleTrainingModeChanged;
        }

        private void OnDisable()
        {
            GameEvents.TVToggled -= HandleTvToggled;
            GameEvents.CornerStageChanged -= HandleCornerStage;
            GameEvents.AttackTelegraphStarted -= HandleTelegraphStarted;
            GameEvents.AttackResolved -= HandleAttackResolved;
            GameEvents.TrainingModeChanged -= HandleTrainingModeChanged;
        }

        private void Start()
        {
            // 예외② 불상 촛불 — 켜두고 이후 어떤 경로로도 건드리지 않는다 (완전 암전 방지 + 행동 지시)
            if (buddhaCandleLight != null) buddhaCandleLight.intensity = candleIntensity;
        }

        private void HandleTvToggled(bool isOn)
        {
            if (tvLight != null) tvLight.intensity = isOn ? tvIntensity : 0f;
        }

        private void HandleCornerStage(int corner, int stage)
        {
            if (corner < 0 || corner >= _stages.Length) return;
            _stages[corner] = stage;
            _blackCount = CornerPenaltyModel.CountBlack(_stages);
        }

        private void HandleTelegraphStarted(int corner, float duration)
        {
            if (corner < 0 || corner >= _telegraphCount.Length) return;
            _telegraphCount[corner]++;
            // 등대가 "지금" 필요하다는 신호 — 밝기 하한은 그대로 두고 위로만 튄다 (예외② 불변)
            _candleFlareUntil = Time.time + candleFlareSec;
        }

        private void HandleAttackResolved(int corner, bool countered)
        {
            if (corner < 0 || corner >= _telegraphCount.Length) return;
            _telegraphCount[corner] = Mathf.Max(0, _telegraphCount[corner] - 1);
        }

        /// <summary>
        /// 학습 구간 스포트라이트 — 켜고 끄는 것은 이 이벤트 하나뿐이다(끄는 쪽이 유실되면 본편이 계속 어둡다).
        /// AttackScheduler는 Begin/Stop/EndTraining **모든 경로**에서 false를 발행한다.
        /// </summary>
        private void HandleTrainingModeChanged(bool active)
        {
            _trainingActive = active;
            Debug.Log($"[LIGHT] 학습 스포트라이트 {(active ? "ON" : "OFF — 실내 조도 원복")}" +
                      $" (배율 {TrainingStageModel.RoomDimScale(active, trainingRoomDimScale):F2})");
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float dawn = sequencer != null ? sequencer.Dawn01 : 0f;
            float bias = sequencer != null ? sequencer.RoomLightBias : 0f;

            // 예외① 창밖 여명은 진실 채널 — RoomLightBias(연출)도 흑화 감광도 절대 섞지 않는다.
            if (windowDawnLight != null) windowDawnLight.intensity = dawn * dawnMaxIntensity;

            // 실내 전역광에만 연출 가감 + 흑화 감광을 얹고, 합산 후 한 번만 바닥으로 클램프한다.
            if (globalLight != null)
            {
                float target = CornerPenaltyModel.RoomLightIntensity(
                    globalBase, dawn, globalDawnBoost, bias, _blackCount, LightPenalty, MinRoomLight,
                    TrainingStageModel.RoomDimScale(_trainingActive, trainingRoomDimScale));
                _smoothedGlobal = _smoothedGlobal < 0f
                    ? target
                    : Mathf.Lerp(_smoothedGlobal, target, CornerPenaltyModel.SmoothFactor(dt, SmoothSec));
#if UNITY_EDITOR
                // 에디터 오버라이드는 **클램프가 끝난 최종 출력 직전**에만 개입한다 —
                // v0.5 감광 계산(페이즈 bias + 흑화 −penalty×n → 바닥 클램프)은 손대지 않는다.
                // editorFreeLight면 전역광에만 손을 떼고, 귀퉁이·촛불·여명 로직은 그대로 돈다.
                WarnEditorOverrideOnce();
                if (!editorFreeLight) globalLight.intensity = _smoothedGlobal + editorLightBoost;
#else
                globalLight.intensity = _smoothedGlobal;
#endif
            }

            UpdateCornerLights();
            UpdateCandle();
        }

        /// <summary>
        /// 촛불 — 상시 candleIntensity를 유지하고, 전조 직후에만 그 위로 짧게 일렁인다.
        /// 감광 예외②의 취지(암전 방지·행동 지시)를 깨지 않으면서 "지금 여기로"만 덧붙인다.
        /// </summary>
        private void UpdateCandle()
        {
            if (buddhaCandleLight == null) return;

            // v0.6.1 학습 스포트라이트 — 촛불은 **위로만**(예외② 유지). 러프해서 학습 종료가 뚝 끊기지 않게.
            float candleTarget = TrainingStageModel.CandleScale(_trainingActive, trainingCandleBoost);
            _smoothedCandleScale = Mathf.Lerp(_smoothedCandleScale, candleTarget,
                CornerPenaltyModel.SmoothFactor(Time.deltaTime, SmoothSec));
            float baseIntensity = candleIntensity * _smoothedCandleScale;

            float left = _candleFlareUntil - Time.time;
            if (left <= 0f)
            {
                buddhaCandleLight.intensity = baseIntensity;
                return;
            }
            float k = Mathf.Clamp01(left / Mathf.Max(0.01f, candleFlareSec));
            float flicker = 0.6f + 0.4f * Mathf.Sin(Time.time * 11f);
            buddhaCandleLight.intensity = baseIntensity * (1f + candleFlareBoost * k * flicker);
        }

#if UNITY_EDITOR
        /// <summary>
        /// 오버라이드가 켜진 채로 밝기를 판단하면 "실제 빌드보다 밝은 화면"으로 밸런스를 정하게 된다 —
        /// 그 사고를 막으려고 플레이 시작 후 한 번만 경고한다.
        /// </summary>
        private void WarnEditorOverrideOnce()
        {
            if (_editorOverrideWarned) return;
            if (!editorFreeLight && Mathf.Approximately(editorLightBoost, 0f)) return;
            _editorOverrideWarned = true;
            Debug.LogWarning("[LIGHT] 에디터 오버라이드 활성 — 실제 빌드와 밝기가 다릅니다", this);
        }
#endif

        /// <summary>
        /// 귀퉁이 광원 — 평시엔 단계별로 어두워지지만, 전조 중에는 예외③으로 원래 강도의 점멸을 낸다.
        /// 감광이 걸릴수록 이 점멸이 화면에서 유일하게 밝은 것이 되어 "저기로 가라"가 자연 강조된다.
        /// </summary>
        private void UpdateCornerLights()
        {
            float now = Time.time;
            for (int i = 0; i < cornerLights.Length; i++)
            {
                Light2D light = cornerLights[i];
                if (light == null) continue;

                if (_telegraphCount[i] > 0)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(now * telegraphPulseHz * 2f * Mathf.PI);
                    light.color = cornerBreachColor;
                    light.intensity = telegraphIntensity * (0.55f + 0.45f * pulse);
                    continue;
                }

                int stage = _stages[i];
                if (stage >= (int)CornerStage.Black)
                {
                    // 흑화 = 결계가 꺼진 자리에 붉은 균열광이 켜진다. 심화면 그 빛이 느리게 숨쉰다.
                    float breathe = stage >= (int)CornerStage.DeepBlack
                        ? 0.75f + 0.25f * Mathf.Sin(now * breachPulseHz * 2f * Mathf.PI)
                        : 1f;
                    light.color = cornerBreachColor;
                    light.intensity = cornerBaseIntensity * breachFactor * breathe;
                    continue;
                }

                light.color = cornerWardColor;
                light.intensity = cornerBaseIntensity * (stage == (int)CornerStage.Gray ? 0.55f : 1f);
            }
        }
    }
}
