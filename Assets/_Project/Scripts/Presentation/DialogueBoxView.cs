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
    /// 본편 진입(PhaseChanged) 즉시 숨김 — E 스킵으로 프롤로그가 중단돼도 대사가 남지 않는다.
    /// 화자→초상 매핑에 없는 화자(독백 "나" 제외 임의 화자)는 초상 슬롯을 비운다.
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
        [SerializeField] private GameObject root;          // 대화상자 전체 (프레임+초상+이름+본문)
        [SerializeField] private Image portrait;
        [SerializeField] private GameObject namePanel;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private PortraitEntry[] portraits;
        // 줄 종료 후 상자를 접기까지 유예 — PrologueDirector 줄 간격(0.4s)보다 길어야 줄 사이 깜빡임이 없다
        [SerializeField] private float hideDelaySec = 0.8f;

        private readonly Queue<SubtitleLine> _queue = new Queue<SubtitleLine>();
        private float _remaining;
        private float _linger;

        private void OnEnable()
        {
            GameEvents.GameEventFired += HandleGameEventFired;
            GameEvents.PhaseChanged += HandlePhaseChanged;
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
            foreach (SubtitleLine line in lines) _queue.Enqueue(line);
        }

        private void HandlePhaseChanged(PhaseId phase)
        {
            // 본편 시작(PhaseSequencer.Begin → P1 발행) — 스킵 포함 어떤 경로든 대화상자를 접는다
            _queue.Clear();
            _remaining = 0f;
            _linger = 0f;
            Hide();
        }

        private void Update()
        {
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
            if (root != null && root.activeSelf) root.SetActive(false);
        }
    }
}
