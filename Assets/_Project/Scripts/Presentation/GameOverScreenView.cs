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

        [Header("사망 영상")]
        [SerializeField] private UnityEngine.Video.VideoPlayer deathVideo;
        [SerializeField] private string deathVideoFile = "Death.mp4"; // StreamingAssets 안의 파일명

        private float _timer = -1f; // < 0 = 비활성

        /// <summary>
        /// 영상 URL은 <b>런타임에</b> StreamingAssets 기준으로 조립한다. 직렬화된 URL로는 안 되는 이유:
        /// <c>Application.streamingAssetsPath</c>가 플랫폼마다 다르다 — 에디터는 로컬 파일 경로,
        /// WebGL은 빌드 서버의 <c>{빌드URL}/StreamingAssets</c>. 프리팹에 절대경로를 박으면 그 머신에서만 돈다.
        /// VideoClip 참조(임베드)도 안 된다 — WebGL 플레이어는 임베디드 클립을 지원하지 않는다.
        /// </summary>
        private void Awake()
        {
            if (deathVideo == null) return;
            deathVideo.source = UnityEngine.Video.VideoSource.Url;
            deathVideo.url = Application.streamingAssetsPath + "/" + deathVideoFile;
        }

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
