using Morae.Game.Core;
using UnityEngine;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 기도 채널 진행 바 (표현 계층 — PrayerChannelChanged 구독만. v1.4 기도 시각 피드백).
    /// 불상 위 월드 스프라이트 바 — 채널 중에만 보이고, fill이 중앙에서 좌우로 자란다.
    /// 조준 귀퉁이 하이라이트는 SaltCornersView가 같은 이벤트로 처리.
    /// </summary>
    public sealed class PrayerView : MonoBehaviour
    {
        [SerializeField] private GameObject barRoot;   // 배경+fill 컨테이너 — 표시/숨김
        [SerializeField] private Transform fill;       // localScale.x = 진행률
        [SerializeField] private float fillWidth = 0.9f;

        private void OnEnable()
        {
            GameEvents.PrayerChannelChanged += HandleChanged;
            if (barRoot != null) barRoot.SetActive(false);
        }

        private void OnDisable()
        {
            GameEvents.PrayerChannelChanged -= HandleChanged;
        }

        private void HandleChanged(float progress01, int aimedCorner)
        {
            bool active = progress01 > 0f;
            if (barRoot != null && barRoot.activeSelf != active) barRoot.SetActive(active);
            if (active && fill != null)
            {
                Vector3 scale = fill.localScale;
                scale.x = fillWidth * progress01;
                fill.localScale = scale;
            }
        }
    }
}
