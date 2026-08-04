using Morae.Game.Core;
using UnityEngine;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 채널 진행 바 (표현 계층 — 구독만. v1.4 기도 → v1.5에서 전 채널로 일반화, PrayerView 대체).
    /// 소품 위 월드 스프라이트 바 — 진행 중에만 보이고, fill이 중앙에서 좌우로 자란다.
    /// source로 구독 이벤트를 고른다: 기도 3s / 문 걸쇠 1.5s / 요강 5s / 이불 이탈 1s.
    /// </summary>
    public sealed class ChannelBarView : MonoBehaviour
    {
        public enum Source { Prayer, DoorLatch, Jar, BlanketExit }

        [SerializeField] private Source source;
        [SerializeField] private GameObject barRoot;   // 배경+fill 컨테이너 — 표시/숨김
        [SerializeField] private Transform fill;       // localScale.x = fillWidth × 진행률
        [SerializeField] private float fillWidth = 0.9f;

        private void OnEnable()
        {
            switch (source)
            {
                case Source.Prayer: GameEvents.PrayerChannelChanged += HandlePrayer; break;
                case Source.DoorLatch: GameEvents.DoorLatchProgressChanged += Apply; break;
                case Source.Jar: GameEvents.JarChannelChanged += Apply; break;
                case Source.BlanketExit: GameEvents.BlanketExitChanged += Apply; break;
            }
            if (barRoot != null) barRoot.SetActive(false);
        }

        private void OnDisable()
        {
            switch (source)
            {
                case Source.Prayer: GameEvents.PrayerChannelChanged -= HandlePrayer; break;
                case Source.DoorLatch: GameEvents.DoorLatchProgressChanged -= Apply; break;
                case Source.Jar: GameEvents.JarChannelChanged -= Apply; break;
                case Source.BlanketExit: GameEvents.BlanketExitChanged -= Apply; break;
            }
        }

        private void HandlePrayer(float progress01, int aimedCorner) => Apply(progress01);

        private void Apply(float progress01)
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
