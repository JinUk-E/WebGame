using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Morae.Game.Core
{
    /// <summary>
    /// 타이틀 화면 (2026-08-04 개편) = WebGL 오디오 게이트 (§8.2).
    /// - 시작은 "게임 시작" 버튼으로만 (아무 키 시작 제거). 사망/엔딩 후에도 항상 타이틀로 돌아온다.
    /// - 프롤로그 스킵 토글: 첫 엔딩(SessionContext.HasEnded) 후에만 노출, 선택은 SessionContext에 보존.
    /// - ? 버튼: 게임 방법 안내 패널 — ◀ ▶ 좌우 페이징.
    /// </summary>
    public sealed class TitleScreenView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Button startButton;
        [SerializeField] private GameObject skipRow;      // 첫 엔딩 후에만 활성
        [SerializeField] private Button skipButton;
        [SerializeField] private TMP_Text skipLabel;
        [SerializeField] private Button helpButton;
        [SerializeField] private GameObject helpPanel;
        [SerializeField] private TMP_Text helpText;
        [SerializeField] private TMP_Text helpPageLabel;
        [SerializeField] private Button helpPrevButton;
        [SerializeField] private Button helpNextButton;
        [SerializeField] private Button helpCloseButton;

        private static readonly string[] HelpPages =
        {
            "<b>목표</b>\n\n팔척귀신에게 홀린 밤 — 직접 봉인한 방에서 아침까지 버틴다.\n\n"
            + "벽시계는 귀신의 간섭으로 거짓말을 한다. 멈추고, 튀고, 되감긴다.\n"
            + "<b>진짜 아침은 소리가 아니라 빛으로 온다 — 창밖이 밝아야 아침이다.</b>\n\n"
            + "진짜 신호(할머니 울음+염불+아침 밝기) 후에 문을 열면 탈출.\n그 전에 열면… 죽는다.",

            "<b>조작</b>\n\nWASD / 방향키 — 이동\nE — 상호작용 (가까이 가면 하단에 안내 표시)\n\n"
            + "TV — E 탭: 이성 회복 +1/s, 대신 공격이 1.33배 빨라진다\n"
            + "이불 — E 탭: 이성 회복 +3/s, 대신 아무 대응도 못 한다 (나올 때 1초)\n"
            + "문 — E 홀드: 귀 대기(문밖 소리가 선명해짐, 이성 −3/s)\n"
            + "    귀 대기 중 문 쪽 방향키 1.5초 유지 = 걸쇠 개방",

            "<b>결계 — 소금 네 귀퉁이</b>\n\n귀신의 공격은 귀퉁이를 노린다. 전조 3초(붉은 점멸+소리) 후 판정.\n\n"
            + "불상 앞에서 E 홀드 3초 + <b>대각 방향키로 귀퉁이 조준</b> = 기도\n"
            + "· 전조 중인 귀퉁이에 기도 완료 → 공격 상쇄 (능동 방어)\n"
            + "· 전조가 없으면 → 오염 1단계 정화\n\n"
            + "소금은 백 → 회 → 흑으로 오염된다. <b>네 곳 전부 흑 = 봉인 붕괴.</b>",

            "<b>이성과 부적</b>\n\n하단의 심장이 빠르고 붉게 뛸수록 위험하다. 0이 되면 공황.\n"
            + "심장이 <color=#CCa626>노랗게 점멸</color>하면 요의 — 회복이 막힌다. 요강(E, 5초 무방비)으로 해소.\n\n"
            + "부적은 붕괴·공황을 딱 한 번 대신 막아준다. 검게 타면 끝.\n\n"
            + "<b>문밖의 단독 목소리는 전부 가짜다. 절대 믿지 마라.</b>",
        };

        private Action _onStart;
        private int _page;

        private void Awake()
        {
            if (startButton != null) startButton.onClick.AddListener(HandleStart);
            if (skipButton != null) skipButton.onClick.AddListener(HandleSkipToggle);
            if (helpButton != null) helpButton.onClick.AddListener(() => SetHelpVisible(true));
            if (helpCloseButton != null) helpCloseButton.onClick.AddListener(() => SetHelpVisible(false));
            if (helpPrevButton != null) helpPrevButton.onClick.AddListener(() => TurnPage(-1));
            if (helpNextButton != null) helpNextButton.onClick.AddListener(() => TurnPage(+1));
        }

        public void Show(Action onStart)
        {
            _onStart = onStart;
            if (root != null) root.SetActive(true);
            if (skipRow != null) skipRow.SetActive(SessionContext.HasEnded);
            RefreshSkipLabel();
            SetHelpVisible(false);
        }

        private void HandleStart()
        {
            if (root != null) root.SetActive(false);
            Action callback = _onStart;
            _onStart = null;
            Debug.Log("[TITLE] 게임 시작 — 오디오 게이트 통과"
                      + (SessionContext.HasEnded ? $", 프롤로그 스킵={SessionContext.SkipPrologue}" : ""));
            callback?.Invoke();
        }

        private void HandleSkipToggle()
        {
            SessionContext.SetSkipPrologue(!SessionContext.SkipPrologue);
            RefreshSkipLabel();
        }

        private void RefreshSkipLabel()
        {
            if (skipLabel != null)
            {
                skipLabel.text = SessionContext.SkipPrologue ? "인트로 스킵: 켜짐" : "인트로 스킵: 꺼짐";
            }
        }

        private void SetHelpVisible(bool visible)
        {
            if (helpPanel == null) return;
            helpPanel.SetActive(visible);
            if (visible)
            {
                _page = 0;
                RefreshHelpPage();
            }
        }

        private void TurnPage(int delta)
        {
            _page = (_page + delta + HelpPages.Length) % HelpPages.Length; // 좌우 순환
            RefreshHelpPage();
        }

        private void RefreshHelpPage()
        {
            if (helpText != null) helpText.text = HelpPages[_page];
            if (helpPageLabel != null) helpPageLabel.text = $"{_page + 1} / {HelpPages.Length}";
        }
    }
}
