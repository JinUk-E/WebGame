using Morae.Game.Interactions;
using Morae.Game.Player;
using TMPro;
using UnityEngine;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// E 프롬프트 (D4 — 아트 2단계에서 키캡 스프라이트 도입). PlayerInteraction의 읽기 프로퍼티를 소비 — §1.1에 예정된 소비처.
    /// promptRoot(키캡+라벨 행) 배선 시: 후보가 있을 때만 행을 켜고 라벨은 행동명만 표시.
    /// promptRoot 미배선 시 구 방식 "[E] 행동명" 텍스트 폴백. 상호작용 진행 중에는 숨김 (진행은 ChannelBarView 담당).
    /// SetActive·문자열 할당은 변경 시에만 (레이아웃 재계산 빈도 최소화).
    /// </summary>
    public sealed class InteractPromptView : MonoBehaviour
    {
        [SerializeField] private PlayerInteraction interaction;
        [SerializeField] private TMP_Text label;
        [SerializeField] private GameObject promptRoot; // 키캡 스프라이트 + 라벨 행 (아트 2단계)

        private Interactable _shown;   // 라벨 재작성 최소화 (문자열 할당은 변경 시에만)
        private string _shownLabel;    // TV처럼 라벨이 동적인 대상 대응 (PromptLabel은 상수 반환 — 비교 저렴)

        private void Update()
        {
            if (interaction == null || label == null) return;

            Interactable candidate = interaction.ActiveTarget == null ? interaction.CurrentCandidate : null;
            string prompt = candidate != null ? candidate.PromptLabel : null;
            if (candidate == _shown && prompt == _shownLabel) return;

            _shown = candidate;
            _shownLabel = prompt;
            if (promptRoot != null)
            {
                promptRoot.SetActive(candidate != null);
                label.text = candidate != null ? prompt : string.Empty;
            }
            else
            {
                label.text = candidate != null ? "[E] " + prompt : string.Empty;
            }
        }
    }
}
