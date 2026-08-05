using System.Collections.Generic;
using Morae.Game.Core;
using Morae.Game.Data;
using TMPro;
using UnityEngine;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 자막 표시 (표현 계층 — GameEvents 구독만, §1.2. D3 "자막").
    /// GameEventFired의 SubtitleLines를 큐로 순차 표시. 화자가 있으면 "화자: 내용" (진위 판별은 내용만 — 화자 "???").
    /// 귀 대기 중(ListeningAtDoor·TV 꺼짐)이면 DetailLines 우선 — 문에 귀를 대야 상세가 들린다 (명세 §3).
    /// 상태 추적도 이벤트 구독(PlayerStateChanged/TVToggled)으로만 — 게임플레이 직접 참조 없음.
    /// </summary>
    public sealed class SubtitleView : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private float gapSec = 0.15f; // 줄 사이 공백 — 줄 바뀜을 시각적으로 구분
        // 아트 2단계 — 이 prefix의 이벤트는 DialogueBoxView(프롤로그 대화상자)가 소화한다 (id 규약: _shared.md)
        [SerializeField] private string dialogueIdPrefix = "prologue-";

        private readonly Queue<SubtitleLine> _queue = new Queue<SubtitleLine>();
        private float _remaining;
        private bool _inGap;
        private bool _listening;
        private bool _tvOn;

        private void OnEnable()
        {
            GameEvents.GameEventFired += HandleGameEventFired;
            GameEvents.PlayerStateChanged += HandlePlayerStateChanged;
            GameEvents.TVToggled += HandleTvToggled;
            if (label != null) label.text = string.Empty;
        }

        private void OnDisable()
        {
            GameEvents.GameEventFired -= HandleGameEventFired;
            GameEvents.PlayerStateChanged -= HandlePlayerStateChanged;
            GameEvents.TVToggled -= HandleTvToggled;
        }

        private void HandlePlayerStateChanged(PlayerState state) => _listening = state == PlayerState.ListeningAtDoor;

        private void HandleTvToggled(bool isOn) => _tvOn = isOn;

        private void HandleGameEventFired(EventDef def)
        {
            if (!string.IsNullOrEmpty(dialogueIdPrefix) && def.Id != null
                && def.Id.StartsWith(dialogueIdPrefix, System.StringComparison.Ordinal))
            {
                return; // 프롤로그 대사 — 대화상자 담당 (이중 표시 방지)
            }
            bool detail = _listening && !_tvOn && def.DetailLines != null && def.DetailLines.Length > 0;
            SubtitleLine[] lines = detail ? def.DetailLines : def.SubtitleLines;
            if (lines == null) return;
            foreach (SubtitleLine line in lines) _queue.Enqueue(line);
        }

        private void Update()
        {
            if (label == null) return;

            if (_remaining > 0f)
            {
                _remaining -= Time.deltaTime;
                if (_remaining > 0f) return;
                if (!_inGap)
                {
                    // 줄 표시 종료 — 다음 줄이 있으면 짧은 공백부터
                    label.text = string.Empty;
                    if (_queue.Count > 0)
                    {
                        _inGap = true;
                        _remaining = gapSec;
                    }
                    return;
                }
                _inGap = false; // 공백 종료 — 아래에서 다음 줄 표시
            }

            if (_queue.Count == 0) return;

            SubtitleLine line = _queue.Dequeue();
            label.text = string.IsNullOrEmpty(line.Speaker) ? line.Text : $"{line.Speaker}: {line.Text}";
            _remaining = Mathf.Max(0.5f, line.Duration);
        }
    }
}
