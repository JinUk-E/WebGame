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
        private readonly float[] _telegraphUntil = new float[CornerIndex.Count];
        private int _blackCount;
        private float _smoothedGlobal = -1f; // 첫 프레임은 목표값으로 스냅 (페이드인 없이 시작)

        private float MinRoomLight => config != null ? config.MinRoomLight : globalMinIntensity;
        private float LightPenalty => config != null ? config.BlackCornerLightPenalty : 0f;
        private float SmoothSec => config != null ? config.RoomLightSmoothSec : 0.3f;

        private void OnEnable()
        {
            GameEvents.TVToggled += HandleTvToggled;
            GameEvents.CornerStageChanged += HandleCornerStage;
            GameEvents.AttackTelegraphStarted += HandleTelegraphStarted;
            GameEvents.AttackResolved += HandleAttackResolved;
        }

        private void OnDisable()
        {
            GameEvents.TVToggled -= HandleTvToggled;
            GameEvents.CornerStageChanged -= HandleCornerStage;
            GameEvents.AttackTelegraphStarted -= HandleTelegraphStarted;
            GameEvents.AttackResolved -= HandleAttackResolved;
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
            if (corner < 0 || corner >= _telegraphUntil.Length) return;
            _telegraphUntil[corner] = Time.time + duration;
        }

        private void HandleAttackResolved(int corner, bool countered)
        {
            if (corner < 0 || corner >= _telegraphUntil.Length) return;
            _telegraphUntil[corner] = 0f;
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
                    globalBase, dawn, globalDawnBoost, bias, _blackCount, LightPenalty, MinRoomLight);
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

                if (now < _telegraphUntil[i])
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(now * telegraphPulseHz * 2f * Mathf.PI);
                    light.intensity = telegraphIntensity * (0.55f + 0.45f * pulse);
                    continue;
                }

                int stage = _stages[i];
                float factor = stage >= (int)CornerStage.Black ? 0.1f : stage == (int)CornerStage.Gray ? 0.45f : 1f;
                light.intensity = cornerBaseIntensity * factor;
            }
        }
    }
}
