using Morae.Game.Core;
using Morae.Game.Data;
using TMPro;
using UnityEngine;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 게임오버 화면 (표현 계층 — GameOver 구독만. D4). 사유 3종별 문구 + 짧은 지연 후 페이드인.
    /// 재시작(E)은 GameFlow가 처리 — 여기는 보여주기만. 씬 리로드로 자연 초기화.
    /// </summary>
    public sealed class GameOverScreenView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private float delaySec = 0.9f;   // 사망 연출 여백
        [SerializeField] private float fadeSec = 1.4f;

        private float _timer = -1f; // < 0 = 비활성

        private void OnEnable()
        {
            GameEvents.GameOver += HandleGameOver;
            if (root != null) root.SetActive(false);
        }

        private void OnDisable()
        {
            GameEvents.GameOver -= HandleGameOver;
        }

        private void HandleGameOver(GameOverReason reason)
        {
            if (titleLabel != null)
            {
                titleLabel.text = reason switch
                {
                    GameOverReason.OpenedDoor => "문이 열렸다.\n…아직 아침이 아니었다.",
                    GameOverReason.SealCollapsed => "네 귀퉁이가 전부 검게 물들었다.\n결계가 무너졌다.",
                    _ => "심장이 견디지 못했다.",
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
