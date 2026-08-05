using Morae.Game.Core;
using Morae.Game.Data;
using TMPro;
using UnityEngine;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 엔딩 화면 (표현 계층 — EndingStarted 구독만. D4). Perfect/Survived/Rescued 3종 문구 + 페이드인.
    /// 게임오버와 달리 밝은 톤 — 아침이 왔다. 스틸컷은 후속(아트 패스)에서 이 위에 얹는다.
    /// </summary>
    public sealed class EndingScreenView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private float delaySec = 1.2f;
        [SerializeField] private float fadeSec = 2f;

        private float _timer = -1f;

        private void OnEnable()
        {
            GameEvents.EndingStarted += HandleEnding;
            if (root != null) root.SetActive(false);
        }

        private void OnDisable()
        {
            GameEvents.EndingStarted -= HandleEnding;
        }

        private void HandleEnding(EndingKind kind)
        {
            if (titleLabel != null)
            {
                // v0.4 — 등급 서열을 명시한다. 무응답(Rescued)은 "버텼지만 판별에 실패한" 하위 결말.
                titleLabel.text = kind switch
                {
                    EndingKind.Perfect =>
                        "완전한 아침\n\n부적은 끝내 타지 않았다.\n너는 진짜 아침을 스스로 알아봤다.",
                    EndingKind.Survived =>
                        "아침\n\n부적이 너 대신 탔다.\n그래도 문을 연 것은 너였다.",
                    _ =>
                        "구조됨\n\n07:40. 네가 열지 않은 문을 K씨가 열었다.\n진짜 아침은 이미 와 있었다.",
                };
            }
            _timer = 0f;
        }

        private void Update()
        {
            if (_timer < 0f) return;
            _timer += Time.unscaledDeltaTime;

            if (_timer < delaySec) return;
            if (root != null && !root.activeSelf) root.SetActive(true);
            if (group != null) group.alpha = Mathf.Clamp01((_timer - delaySec) / fadeSec);
        }
    }
}
