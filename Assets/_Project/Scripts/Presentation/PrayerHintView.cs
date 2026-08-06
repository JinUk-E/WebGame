using System;
using Morae.Game.Core;
using Morae.Game.Data;
using Morae.Game.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 기도 조작 힌트 (표현 계층 — 구독만, §1.2. 2026-08-06 프롤로그 강제 학습 전용).
    ///
    /// <para>
    /// <b>왜 대사가 아니라 UI인가</b>: 대사에 "E를 누르거라"가 들어가면 할아버지가 게임 시스템을 아는 존재가 되어
    /// 몰입이 깨진다. 플레이어는 대사(픽션)와 UI(시스템)를 다른 채널로 읽으므로, 채널을 나누면
    /// 명시적이면서도 픽션이 상하지 않는다. 그래서 대사는 "불상 앞에서 빌어라"까지만 말하고,
    /// 무엇을 누르는지는 이 키캡이 보여준다.
    /// </para>
    /// <para>
    /// <b>표시 구간</b>: 학습 안내 대사(id <c>prologue-controls</c>)와 같은 순간에 뜨고, <b>본편 진입(PhaseChanged)에
    /// 사라진다</b> — 재시도 중에는 계속 떠 있다(못 막은 이유가 조작을 몰라서인 경우가 대부분이다).
    /// </para>
    /// <para>
    /// <b>방향 강조</b>: 전조가 뜬 귀퉁이의 두 방향키(세로 1 + 가로 1)가 밝아지고 커진다. 매핑은 순수 모델
    /// <see cref="PrayerAimHint"/> — 기도 조준 판정의 역방향이라 규칙이 바뀌면 같이 깨진다.
    /// 기기 분기는 <see cref="TouchSupport.IsTouchDevice"/> (온스크린 컨트롤과 같은 판별).
    /// </para>
    /// </summary>
    public sealed class PrayerHintView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private GameObject keyboardGroup;   // [E] + 방향키 크로스
        [SerializeField] private GameObject touchGroup;      // 버튼 + 스틱
        // 0=Up 1=Down 2=Left 3=Right (AimKey 순서 — 배열 순서를 바꾸면 강조가 어긋난다)
        [SerializeField] private Graphic[] arrowKeys = new Graphic[4];
        [SerializeField] private RectTransform touchKnob;    // 스틱 기울임 표시
        [SerializeField] private float knobOffset = 30f;
        [SerializeField] private string showEventId = "prologue-controls";

        [Header("강조")]
        [SerializeField] private Color idleColor = new Color(0.72f, 0.7f, 0.65f, 0.55f);
        [SerializeField] private Color litColor = new Color(1f, 0.93f, 0.72f, 1f);
        [SerializeField] private float pulseScale = 0.14f;
        [SerializeField] private float pulseHz = 1.6f;

        private int _corner = CornerIndex.None;
        private bool _shown;
        private bool _touch;

        private void Awake()
        {
            _touch = TouchSupport.IsTouchDevice;
            if (keyboardGroup != null) keyboardGroup.SetActive(!_touch);
            if (touchGroup != null) touchGroup.SetActive(_touch);
            SetShown(false);
        }

        private void OnEnable()
        {
            GameEvents.GameEventFired += HandleGameEventFired;
            GameEvents.AttackTelegraphStarted += HandleTelegraphStarted;
            GameEvents.PhaseChanged += HandlePhaseChanged;
        }

        private void OnDisable()
        {
            GameEvents.GameEventFired -= HandleGameEventFired;
            GameEvents.AttackTelegraphStarted -= HandleTelegraphStarted;
            GameEvents.PhaseChanged -= HandlePhaseChanged;
        }

        private void HandleGameEventFired(EventDef def)
        {
            if (def == null || string.IsNullOrEmpty(showEventId)) return;
            if (!string.Equals(def.Id, showEventId, StringComparison.Ordinal)) return;
            SetShown(true);
        }

        /// <summary>학습 전조 = 지금 겨눌 귀퉁이. 표시 중이 아니면 무시한다(본편 전조에는 반응하지 않는다).</summary>
        private void HandleTelegraphStarted(int corner, float duration)
        {
            if (!_shown) return;
            _corner = corner;
            ApplyHighlight();
        }

        /// <summary>본편 시작(P1) — 학습은 끝났다. 이후 전조에는 관여하지 않는다.</summary>
        private void HandlePhaseChanged(PhaseId phase) => SetShown(false);

        private void SetShown(bool shown)
        {
            _shown = shown;
            if (!shown) _corner = CornerIndex.None;
            if (root != null && root.activeSelf != shown) root.SetActive(shown);
            ApplyHighlight();
        }

        private void ApplyHighlight()
        {
            if (arrowKeys != null)
            {
                for (int i = 0; i < arrowKeys.Length; i++)
                {
                    Graphic key = arrowKeys[i];
                    if (key == null) continue;
                    key.color = PrayerAimHint.IsKeyLit(_corner, (AimKey)i) ? litColor : idleColor;
                }
            }

            if (touchKnob != null)
            {
                touchKnob.anchoredPosition = PrayerAimHint.StickDirection(_corner) * knobOffset;
            }
        }

        private void Update()
        {
            if (!_shown || _corner == CornerIndex.None) return;

            // 색은 강조가 바뀔 때만 쓰고(메시 재생성), 매 프레임 움직이는 것은 스케일뿐 — 트랜스폼은 리빌드가 없다
            float pulse = 1f + pulseScale * 0.5f * (1f + Mathf.Sin(Time.unscaledTime * pulseHz * 2f * Mathf.PI));
            if (_touch)
            {
                if (touchKnob != null) touchKnob.localScale = Vector3.one * pulse;
                return;
            }
            if (arrowKeys == null) return;
            for (int i = 0; i < arrowKeys.Length; i++)
            {
                Graphic key = arrowKeys[i];
                if (key == null) continue;
                key.transform.localScale = PrayerAimHint.IsKeyLit(_corner, (AimKey)i)
                    ? Vector3.one * pulse
                    : Vector3.one;
            }
        }
    }
}
