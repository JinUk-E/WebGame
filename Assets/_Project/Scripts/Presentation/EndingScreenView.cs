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
                titleLabel.text = kind switch
                {
                    EndingKind.Perfect => "아침 — 완벽한 밤샘.\n부적은 끝내 타지 않았다.",
                    EndingKind.Survived => "아침.\n부적이 너 대신 탔다.",
                    _ => "07:40 — 구조.\nK씨가 문을 열었다. 밤은 끝났다.",
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
