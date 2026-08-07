using Morae.Game.Core;
using UnityEngine;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 벽에 걸린 부적 (표현 계층 — 구독만: TalismanBurnChanged/PlayerStateChanged).
    /// 부적이 타들어간 정도를 스프라이트 단계로 보여주고, 다 타갈 때 흔들린다.
    ///
    /// <para>
    /// <b>v0.7: 좌하단 HUD → 월드 스프라이트.</b> 이유가 둘이다.
    /// ① 플레이어의 시선은 방(월드)에 있는데 부적이 화면 구석에 있으면 <b>화면 대각 최장 사케이드</b>가 된다.
    ///    벽에 걸어 두면 소금 귀퉁이들과 같은 공간에 있어 "저기가 급하다"까지 한 번에 읽힌다.
    /// ② 요구사항이 "이불에 들어가면 부적이 안 보인다"인데, <b>월드에 있어야 그게 규칙이 아니라 사실</b>이 된다
    ///    (이불을 뒤집어썼으니 벽이 안 보이는 것). HUD였다면 임의의 룰로 느껴진다.
    /// </para>
    ///
    /// <para>
    /// <b>연소는 이제 애니메이션이 아니라 값이다.</b> 옛 코드는 TalismanBurned(1회성)를 받아
    /// 총 2.5초짜리 4단계 애니메이션을 재생하고 끝이었다. 부적이 60초에 걸쳐 타는 지금은
    /// burn01을 단계에 <b>직접 매핑</b>한다 — 60초를 4단계로 나누면 15초에 한 칸이라 너무 거칠어서
    /// 8단계를 기본으로 잡았다(배선된 스프라이트 수에 맞춰 자동으로 접힌다).
    /// </para>
    /// </summary>
    public sealed class TalismanStatusView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer talisman;
        [SerializeField] private Sprite[] stageSprites = new Sprite[8]; // 0=멀쩡 … 마지막=완전 연소
        // 임계 구간 흔들림 — 비회복 풀은 후반까지 조용하다가 갑자기 끝나므로 예고가 반드시 있어야 한다
        [SerializeField] private float criticalBurn01 = 0.83f;   // BalanceConfig.TalismanCriticalRemainSec와 같은 뜻 (10/60)
        [SerializeField] private float shakeAmplitude = 0.03f;   // 월드 유닛
        [SerializeField] private float shakeHz = 11f;

        private Vector3 _basePos;
        private float _burn01;
        private int _shownStage = -1;

        private void Awake()
        {
            if (talisman != null) _basePos = talisman.transform.localPosition;
        }

        private void OnEnable() => GameEvents.TalismanBurnChanged += HandleBurnChanged;

        private void OnDisable() => GameEvents.TalismanBurnChanged -= HandleBurnChanged;

        private void HandleBurnChanged(float burn01) => _burn01 = Mathf.Clamp01(burn01);

        private void Update()
        {
            if (talisman == null) return;

            int stageCount = stageSprites != null ? stageSprites.Length : 0;
            if (stageCount > 0)
            {
                // burn01 → 단계. 1.0(전소)이 마지막 칸을 넘지 않게 클램프
                int stage = Mathf.Min(Mathf.FloorToInt(_burn01 * stageCount), stageCount - 1);
                if (stage != _shownStage)
                {
                    _shownStage = stage;
                    if (stageSprites[stage] != null) talisman.sprite = stageSprites[stage];
                }
            }

            Transform t = talisman.transform;
            if (_burn01 >= criticalBurn01)
            {
                float n = Time.unscaledTime * shakeHz; // 연출 계층 시간 — F1 배속에 끌려가지 않는다
                t.localPosition = _basePos + new Vector3(
                    (Mathf.PerlinNoise(n, 0.3f) - 0.5f) * 2f * shakeAmplitude,
                    (Mathf.PerlinNoise(0.7f, n) - 0.5f) * 2f * shakeAmplitude,
                    0f);
            }
            else if (t.localPosition != _basePos)
            {
                t.localPosition = _basePos;
            }
        }
    }
}
