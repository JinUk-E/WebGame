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
    /// </summary>
    public sealed class LightingController : MonoBehaviour
    {
        [SerializeField] private PhaseSequencer sequencer;
        [SerializeField] private Light2D globalLight;
        [SerializeField] private Light2D tvLight;
        [SerializeField] private Light2D windowDawnLight;
        [SerializeField] private Light2D[] cornerLights = new Light2D[CornerIndex.Count];

        [SerializeField] private float dawnMaxIntensity = 2.5f;   // 여명 최대 (Dawn01 = 1)
        [SerializeField] private float globalBase = 0.12f;        // §3.1 골격값
        [SerializeField] private float globalDawnBoost = 0.18f;   // 아침이 방 전체를 서서히 밝힌다
        [SerializeField] private float globalMinIntensity = 0.05f; // 연출 가감이 겹쳐도 암전되지 않는 하한
        [SerializeField] private float tvIntensity = 1.1f;
        [SerializeField] private float cornerBaseIntensity = 0.25f; // 단계별 ×1 / ×0.45 / ×0.1

        private void OnEnable()
        {
            GameEvents.TVToggled += HandleTvToggled;
            GameEvents.CornerStageChanged += HandleCornerStage;
        }

        private void OnDisable()
        {
            GameEvents.TVToggled -= HandleTvToggled;
            GameEvents.CornerStageChanged -= HandleCornerStage;
        }

        private void HandleTvToggled(bool isOn)
        {
            if (tvLight != null) tvLight.intensity = isOn ? tvIntensity : 0f;
        }

        private void HandleCornerStage(int corner, int stage)
        {
            if (corner < 0 || corner >= cornerLights.Length || cornerLights[corner] == null) return;
            float factor = stage >= 2 ? 0.1f : stage == 1 ? 0.45f : 1f;
            cornerLights[corner].intensity = cornerBaseIntensity * factor;
        }

        private void Update()
        {
            if (sequencer == null) return;
            float dawn = sequencer.Dawn01;
            // 창밖 여명은 진실 채널 — RoomLightBias(연출)를 절대 섞지 않는다.
            if (windowDawnLight != null) windowDawnLight.intensity = dawn * dawnMaxIntensity;
            // 실내 전역광에만 연출 가감을 얹는다 (P4 밝음 → P5·P6 어두움).
            if (globalLight != null)
            {
                globalLight.intensity = Mathf.Max(globalMinIntensity,
                    globalBase + dawn * globalDawnBoost + sequencer.RoomLightBias);
            }
        }
    }
}
