using Morae.Game.Core;
using Morae.Game.Data;
using UnityEngine;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 채널 진행 바 (표현 계층 — 구독만). 소품 위 월드 스프라이트 바 — 진행 중에만 보인다.
    /// source로 구독 이벤트를 고른다: 소금 뿌리기 1.5s / 문 걸쇠 1.5s / 이불 이탈 0.5s.
    ///
    /// <para>
    /// <b>v0.7: 자라는 방향으로 종류를 가른다.</b> 조작을 E 홀드 하나로 통일하면서
    /// <b>안전한 홀드(소금)와 즉사하는 홀드(문)가 같은 키·같은 문법</b>이 됐고, 길이도 1.5s로 같다.
    /// 색으로 가르면 붉은 전조와 문법이 겹치므로(색 문법 분리 규칙), <b>형태 축</b>을 쓴다:
    ///   · 안전·이득(소금·이불) = 바깥에서 <b>안쪽으로 모이는</b> fill — 채워짐·회복
    ///   · 위험(문 걸쇠)        = 안쪽에서 <b>바깥으로 벌어지는</b> fill — 문이 벌어지는 형태 그 자체
    /// 밝기 축도 색 문법도 건드리지 않으면서 "이건 다른 종류의 행동"이 학습 없이 읽힌다.
    /// </para>
    /// </summary>
    public sealed class ChannelBarView : MonoBehaviour
    {
        public enum Source { Salt, DoorLatch, BlanketExit }

        [SerializeField] private Source source;
        /// <summary>Salt 전용 — 이 바가 담당하는 귀퉁이. 다른 귀퉁이의 진행은 무시한다.</summary>
        [SerializeField] private int cornerIndex = CornerIndex.TopLeft;
        [SerializeField] private GameObject barRoot;   // 배경+fill 컨테이너 — 표시/숨김
        [SerializeField] private Transform fill;       // localScale.x = fillWidth × 진행률
        [SerializeField] private float fillWidth = 0.9f;
        /// <summary>바깥으로 벌어지는 형태 — 되돌리기 어려운 행동에만 켠다 (문 걸쇠).</summary>
        [SerializeField] private bool growOutward;

        private void OnEnable()
        {
            switch (source)
            {
                case Source.Salt: GameEvents.SaltChannelChanged += HandleSalt; break;
                case Source.DoorLatch: GameEvents.DoorLatchProgressChanged += Apply; break;
                case Source.BlanketExit: GameEvents.BlanketExitChanged += Apply; break;
            }
            if (barRoot != null) barRoot.SetActive(false);
        }

        private void OnDisable()
        {
            switch (source)
            {
                case Source.Salt: GameEvents.SaltChannelChanged -= HandleSalt; break;
                case Source.DoorLatch: GameEvents.DoorLatchProgressChanged -= Apply; break;
                case Source.BlanketExit: GameEvents.BlanketExitChanged -= Apply; break;
            }
        }

        private void HandleSalt(int corner, float progress01)
        {
            if (corner != cornerIndex) return;
            Apply(progress01);
        }

        /// <summary>
        /// progress01 &gt; 0 = 진행 중. PlayerInteraction이 <b>경과를 더한 뒤에</b> OnHoldTick을 부르므로
        /// 첫 틱부터 값이 0을 넘는다 — "눌렀는데 한 프레임 아무 반응이 없다"는 학습 실패 루프가 생기지 않는다.
        /// </summary>
        private void Apply(float progress01)
        {
            bool active = progress01 > 0f;
            if (barRoot != null && barRoot.activeSelf != active) barRoot.SetActive(active);
            if (!active || fill == null) return;

            Vector3 scale = fill.localScale;
            scale.x = fillWidth * progress01;
            fill.localScale = scale;

            // 폭은 같게 자라되 **어디로** 자라는지가 다르다.
            //   growOutward: 중앙에 붙어 좌우로 벌어진다 (앵커 고정, 위치 0)
            //   기본:        가장자리에서 중앙으로 당겨진다 (남은 빈 폭의 절반만큼 안쪽으로 이동)
            Vector3 pos = fill.localPosition;
            pos.x = growOutward ? 0f : fillWidth * (1f - progress01) * 0.5f;
            fill.localPosition = pos;
        }
    }
}
