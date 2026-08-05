using Morae.Game.Core;
using UnityEngine;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// TV 화면 스프라이트 스왑 (표현 계층 — TVToggled 구독만, §1.2. 아트 2단계).
    /// 켜짐 = 청백광 노이즈 스프라이트. 점등 광원은 LightingController(TVLight)가 별도 구독으로 유지.
    /// </summary>
    public sealed class TvScreenView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer screen;
        [SerializeField] private Sprite offSprite;
        [SerializeField] private Sprite onSprite;

        private void OnEnable()
        {
            GameEvents.TVToggled += HandleToggled;
            HandleToggled(false); // 씬 시작·리로드 시 꺼짐 기준 (TvInteractable 초기 상태와 동일)
        }

        private void OnDisable()
        {
            GameEvents.TVToggled -= HandleToggled;
        }

        private void HandleToggled(bool isOn)
        {
            if (screen == null) return;
            Sprite next = isOn ? onSprite : offSprite;
            if (next != null && screen.sprite != next) screen.sprite = next;
        }
    }
}
