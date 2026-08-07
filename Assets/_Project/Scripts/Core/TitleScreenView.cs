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

        /// <summary>
        /// 도움말 (2026-08-07 개정 — <b>정답지 제거</b>).
        ///
        /// <para>
        /// 이전 판은 이 게임의 코어 퍼즐 답을 먼저 말해버렸다 — 시계를 믿지 말라는 것,
        /// 진짜 아침은 빛으로 온다는 것, 진짜 신호의 구성, 문밖 목소리가 전부 가짜라는 것.
        /// 그건 <b>플레이 중 발견하거나 프롤로그에서 할아버지가 픽션으로 가르치는</b> 몫이다.
        /// 도움말은 "무엇을 누르면 무엇이 되는가"까지만 말한다.
        /// </para>
        ///
        /// <para>
        /// 수치는 실제 구현과 반드시 일치시킬 것 (v0.6.1 확정값: 전조 4.5s / 기도 채널 2.5s / 심화 3.75s).
        /// 여기 적힌 값이 틀리면 플레이어는 자기 손이 느린 줄 안다.
        /// </para>
        /// </summary>
        private static readonly string[] HelpPages =
        {
            "<b>목표</b>\n\n팔척귀신에게 홀린 밤 — 직접 봉인한 방에서 아침까지 버틴다.\n\n"
            + "<b>조작</b>\n\nWASD / 방향키 — 이동\n"
            + "E — 상호작용 (가까이 가면 화면 하단에 안내가 뜬다)\n"
            + "방향키 — 기도 중에는 이동이 아니라 <b>귀퉁이 조준</b>\n\n"
            + "모바일: 왼쪽 아래 스틱으로 이동, 오른쪽 아래 버튼이 E다.",

            "<b>방 안의 것들</b>\n\nTV — E 탭: 이성 회복 +1/s, 대신 공격이 1.33배 빨라진다\n"
            + "이불 — E 탭: 이성 회복 +3/s, 대신 아무 대응도 못 한다 (나올 때 1초)\n"
            + "요강 — E 탭: 5초 동안 꼼짝 못 한다\n"
            + "문 — E 홀드: 귀 대기(문밖 소리가 선명해짐, 이성 −3/s)\n"
            + "    귀 대기 중 문 쪽 방향키 1.5초 유지 = 걸쇠 개방\n\n"
            + "불상 — E 홀드 + 방향키: 기도 (다음 장)",

            "<b>결계 — 소금 네 귀퉁이</b>\n\n"
            + "귀신의 공격은 귀퉁이를 노린다. 그 자리에 <b>어둠이 고이고 소금이 흔들리며 밖에서 두드리는 소리</b>가 나면 전조다.\n"
            + "전조 <b>4.5초</b> 뒤에 판정이 떨어진다.\n\n"
            + "불상 앞에서 E 홀드 <b>2.5초</b> + <b>대각 방향키로 귀퉁이 조준</b> = 기도\n"
            + "· 전조 중인 귀퉁이에 기도 완료 → 공격 상쇄 (능동 방어)\n"
            + "· 전조가 없으면 → 오염 1단계 정화\n\n"
            + "소금은 백 → 회 → 흑으로 오염된다. 이미 검은 곳이 또 맞으면 더 굳어져 정화에 <b>3.75초</b>가 든다.\n"
            + "검어진 곳이 늘수록 <b>방이 어두워지고 공격이 빨라진다.</b> <b>네 곳 전부 흑 = 봉인 붕괴.</b>",

            "<b>이성과 부적</b>\n\n왼쪽 아래의 심장이 빠르고 붉게 뛸수록 위험하다. 0이 되면 공황.\n"
            + "심장이 <color=#CCa626>노랗게 점멸</color>하면 요의 — 회복이 막힌다. 요강(E, 5초 무방비)으로 해소.\n\n"
            + "부적은 붕괴·공황을 딱 한 번 대신 막아준다. 검게 타면 끝.",
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
