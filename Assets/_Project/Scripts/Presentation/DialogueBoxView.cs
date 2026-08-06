using System;
using System.Collections.Generic;
using Morae.Game.Core;
using Morae.Game.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 프롤로그 대화상자 (표현 계층 — GameEventFired 구독만, §1.2. 아트 2단계).
    /// id가 idPrefix("prologue-")로 시작하는 EventDef만 소화 — 초상+화자명 패널+본문으로 표시한다.
    /// 본편 자막은 SubtitleView 그대로 (SubtitleView는 같은 prefix를 건너뛴다 — id 규약은 _shared.md).
    /// 본편 진입(PhaseChanged) 즉시 숨김 — 스킵으로 프롤로그가 중단돼도 대사가 남지 않는다.
    /// 화자→초상 매핑에 없는 화자(독백 "나" 제외 임의 화자)는 초상 슬롯을 비운다.
    ///
    /// <para>
    /// <b>[2026-08-06] 수동 진행</b> — id가 manualPrefix("prologue-line-")로 시작하는 줄은 <b>시간으로 넘어가지
    /// 않는다</b>: 다음 줄이 올 때까지(=플레이어가 클릭·탭·E로 넘길 때까지) 그대로 남고, 우하단 ▼가 깜빡여
    /// "넘길 수 있다"를 알린다. 진행 판정은 게임플레이(PrologueDirector)가 소유하고 이 뷰는 표시만 한다.
    /// 학습 구간 대사(prologue-warn 등)와 본편 자막은 <b>기존 시간 기반 동작 그대로</b>다.
    /// </para>
    /// </summary>
    public sealed class DialogueBoxView : MonoBehaviour
    {
        [Serializable]
        private struct PortraitEntry
        {
            public string speaker;
            public Sprite sprite;
        }

        [SerializeField] private string idPrefix = "prologue-";
        // 이 접두사로 시작하는 줄 = 수동 진행 (입력 대기). PrologueDirector가 대사 줄에만 붙인다.
        [SerializeField] private string manualPrefix = "prologue-line-";
        [SerializeField] private GameObject root;          // 대화상자 전체 (프레임+초상+이름+본문)
        [SerializeField] private Image portrait;
        [SerializeField] private GameObject namePanel;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private PortraitEntry[] portraits;
        // 줄 종료 후 상자를 접기까지 유예 — PrologueDirector 줄 간격(0.4s)보다 길어야 줄 사이 깜빡임이 없다
        [SerializeField] private float hideDelaySec = 0.8f;

        [Header("수동 진행 표시 (2026-08-06)")]
        [SerializeField] private Graphic advanceIndicator;   // 우하단 ▼ — 수동 줄에서만 깜빡인다
        [SerializeField] private GameObject skipHint;        // 우상단 "건너뛰기" — 프롤로그 내내 노출
        [SerializeField] private float indicatorBlinkSec = 0.55f;
        [SerializeField] private float indicatorDimAlpha = 0.15f;

        private readonly Queue<SubtitleLine> _queue = new Queue<SubtitleLine>();
        private float _remaining;
        private float _linger;
        private bool _manualHold;      // 지금 보이는 줄이 입력을 기다리는 줄인가
        private float _blinkTimer;
        private bool _blinkOn;

        private void OnEnable()
        {
            GameEvents.GameEventFired += HandleGameEventFired;
            GameEvents.PhaseChanged += HandlePhaseChanged;
            _manualHold = false;
            if (skipHint != null) skipHint.SetActive(false); // 첫 대사가 올 때 켠다 (본편 스킵 진입 시 안 보이게)
            Hide();
        }

        private void OnDisable()
        {
            GameEvents.GameEventFired -= HandleGameEventFired;
            GameEvents.PhaseChanged -= HandlePhaseChanged;
        }

        private void HandleGameEventFired(EventDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.Id) || !def.Id.StartsWith(idPrefix, StringComparison.Ordinal)) return;
            SubtitleLine[] lines = def.SubtitleLines;
            if (lines == null) return;

            if (skipHint != null && !skipHint.activeSelf) skipHint.SetActive(true); // 프롤로그 시작 = 스킵 안내 노출

            bool manual = !string.IsNullOrEmpty(manualPrefix)
                          && def.Id.StartsWith(manualPrefix, StringComparison.Ordinal);
            if (manual)
            {
                // 수동 줄은 큐를 타지 않는다 — 도착 즉시 교체(진행 판정은 이미 게임플레이가 했다).
                // 큐에 얹으면 앞줄의 남은 시간만큼 늦게 뜨거나, 스킵 후 유령 줄이 남는다.
                _queue.Clear();
                if (lines.Length == 0) return;
                ShowLine(lines[0]); // 수동 줄은 한 줄씩 발화된다 (PrologueDirector 규약)
                _manualHold = true;
                _remaining = 0f;
                _linger = 0f;
                SetIndicatorVisible(true);
                return;
            }

            _manualHold = false; // 자동 줄(학습 구간 대사)이 오면 즉시 시간 기반 동작으로 되돌아온다
            SetIndicatorVisible(false);
            foreach (SubtitleLine line in lines) _queue.Enqueue(line);
        }

        private void HandlePhaseChanged(PhaseId phase)
        {
            // 본편 시작(PhaseSequencer.Begin → P1 발행) — 스킵 포함 어떤 경로든 대화상자를 접는다
            _queue.Clear();
            _remaining = 0f;
            _linger = 0f;
            _manualHold = false;
            SetIndicatorVisible(false);
            if (skipHint != null && skipHint.activeSelf) skipHint.SetActive(false);
            Hide();
        }

        private void Update()
        {
            if (_manualHold)
            {
                TickIndicator(); // 시간으로 넘어가지 않는다 — 다음 줄이 오거나 프롤로그가 끝날 때까지 유지
                return;
            }

            if (_remaining > 0f)
            {
                _remaining -= Time.deltaTime;
                if (_remaining > 0f) return;
                _linger = hideDelaySec; // 줄 종료 — 다음 줄이 유예 안에 오면 이어서 표시 (깜빡임 방지)
            }

            if (_queue.Count > 0)
            {
                SubtitleLine line = _queue.Dequeue();
                ShowLine(line);
                _remaining = Mathf.Max(0.5f, line.Duration);
                _linger = 0f;
                return;
            }

            if (_linger > 0f)
            {
                _linger -= Time.deltaTime;
                if (_linger <= 0f) Hide();
            }
        }

        /// <summary>▼ 점멸 — 알파를 프레임마다 쓰지 않고 전환 순간에만 바꾼다 (Graphic 색 변경 = 메시 재생성).</summary>
        private void TickIndicator()
        {
            if (advanceIndicator == null) return;
            _blinkTimer += Time.deltaTime;
            if (_blinkTimer < indicatorBlinkSec) return;
            _blinkTimer = 0f;
            SetIndicatorAlpha(!_blinkOn);
        }

        private void SetIndicatorVisible(bool visible)
        {
            if (advanceIndicator == null) return;
            if (advanceIndicator.gameObject.activeSelf != visible) advanceIndicator.gameObject.SetActive(visible);
            if (!visible) return;
            _blinkTimer = 0f;
            _blinkOn = false;
            SetIndicatorAlpha(true);
        }

        private void SetIndicatorAlpha(bool on)
        {
            _blinkOn = on;
            Color c = advanceIndicator.color;
            c.a = on ? 1f : indicatorDimAlpha;
            advanceIndicator.color = c;
        }

        private void ShowLine(SubtitleLine line)
        {
            if (root != null && !root.activeSelf) root.SetActive(true);
            if (bodyLabel != null) bodyLabel.text = line.Text;

            bool hasSpeaker = !string.IsNullOrEmpty(line.Speaker);
            if (namePanel != null) namePanel.SetActive(hasSpeaker);
            if (nameLabel != null) nameLabel.text = hasSpeaker ? line.Speaker : string.Empty;

            Sprite face = hasSpeaker ? FindPortrait(line.Speaker) : null;
            if (portrait != null)
            {
                portrait.enabled = face != null;
                if (face != null) portrait.sprite = face;
            }
        }

        private Sprite FindPortrait(string speaker)
        {
            if (portraits == null) return null;
            for (int i = 0; i < portraits.Length; i++)
            {
                if (portraits[i].speaker == speaker) return portraits[i].sprite;
            }
            return null;
        }

        private void Hide()
        {
            SetIndicatorVisible(false);
            if (root != null && root.activeSelf) root.SetActive(false);
        }
    }
}
