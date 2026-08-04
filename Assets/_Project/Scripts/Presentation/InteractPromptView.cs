using Morae.Game.Interactions;
using Morae.Game.Player;
using TMPro;
using UnityEngine;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// E 프롬프트 (D4). PlayerInteraction의 읽기 프로퍼티를 소비 — §1.1에 예정된 소비처.
    /// 범위 내 후보가 있으면 "[E] 행동명" 표시, 상호작용 진행 중에는 숨김 (진행은 ChannelBarView 담당).
    /// </summary>
    public sealed class InteractPromptView : MonoBehaviour
    {
        [SerializeField] private PlayerInteraction interaction;
        [SerializeField] private TMP_Text label;

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
            label.text = candidate != null ? "[E] " + prompt : string.Empty;
        }
    }
}
