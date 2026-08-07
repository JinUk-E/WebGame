using Morae.Game.Core;
using Morae.Game.Data;
using Morae.Game.Interactions;
using Morae.Game.Player;
using TMPro;
using UnityEngine;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 모바일 브라우저용 온스크린 컨트롤 (2026-08-05 — 가상 스틱 + 상호작용 버튼).
    /// <para>
    /// <b>데스크톱 무영향 원칙</b>: <see cref="TouchSupport.IsTouchDevice"/>가 false면 Awake에서 루트를 끄고
    /// 이후 어떤 코드도 돌지 않는다 (Update 없음, InputReader 주입 없음) — 키보드 경험 회귀 0.
    /// </para>
    /// <para>
    /// <b>입력 경로</b>: 게임 로직은 여전히 InputReader만 본다. 이 뷰는 레거시 <c>Input.GetTouch</c>로
    /// 직접 히트 테스트해 InputReader에 값을 주입한다. EventSystem 레이캐스트를 쓰지 않는 이유는
    /// ① 멀티터치(좌 스틱 + 우 버튼 동시)를 입력 모듈 설정과 무관하게 보장 ② 타이틀 등 상위 캔버스의
    /// 레이캐스터와 경합하지 않기 위함. 이 뷰의 이미지는 전부 raycastTarget=false다.
    /// </para>
    /// <para>
    /// <b>터치 문법 대응</b> (명세 §3 상호작용 매트릭스):
    /// 탭/홀드/채널 = 버튼을 누르고 있는 동안 InteractHeld 유지 (키보드 E와 동일 의미).
    /// 기도 조준 = 버튼을 누른 채 스틱을 기울이면 그 방향 귀퉁이 (Praying 중에는 스틱이 대각 4방향으로 스냅).
    /// 문 걸쇠 = 귀 대기(버튼 홀드) 중 문 방향으로 스틱 기울임 1.5s — 기존 DoorInteractable 규칙 그대로.
    /// </para>
    /// 표현 계층 규칙 준수: GameEvents는 구독만 하고, 게임플레이 상태는 PlayerInteraction의 읽기 프로퍼티만 본다
    /// (InteractPromptView와 동일한 소비 경로 — 프롬프트 정보 재사용).
    /// </summary>
    public sealed class TouchControlsView : MonoBehaviour
    {
        private const int NoTouch = -1;
        private const int MouseTouch = -2; // 에디터 검증용 가상 터치 id

        [Header("배선")]
        [SerializeField] private PlayerInteraction interaction;
        // 프롤로그 대사 구간 판정용 (읽기 프로퍼티만 — PlayerInteraction과 같은 소비 방식)
        [SerializeField] private GameFlowController flow;
        [SerializeField] private GameObject controlsRoot;   // 스틱+버튼 컨테이너 (게임오버 시 숨김)
        [SerializeField] private RectTransform stickBase;
        [SerializeField] private RectTransform stickKnob;
        [SerializeField] private RectTransform interactButton;
        [SerializeField] private GameObject interactButtonRoot;
        [SerializeField] private TMP_Text interactLabel;
        [SerializeField] private TMP_Text[] keyboardHints;  // "E — 타이틀로" 류 — 터치 문구로 교체
        [SerializeField] private GameObject mobileAudioHint; // 타이틀 "이어폰 권장" 안내 (터치에서만 표시)

        [Header("튜닝")]
        [SerializeField] private float stickRadius = 130f;      // 캔버스 기준 px (1920×1080)
        [SerializeField] private float stickZoneScale = 1.6f;   // 잡히는 범위 = 반경 × 이 값
        [SerializeField] private float stickDeadZone01 = 0.28f;
        [SerializeField] private float knobMaxOffset = 78f;
        [SerializeField] private float buttonRadius = 130f;
        [SerializeField] private string touchHintText = "화면을 탭하면 타이틀로";
        [SerializeField] private bool forceEnable;              // 에디터 검증용 — 마우스로 조작

        private bool _active;
        private bool _useMouse;                 // forceEnable + 비터치 기기 (에디터 검증)
        private int _stickTouch = NoTouch;
        private int _buttonTouch = NoTouch;
        private int _tapTouch = NoTouch;
        private bool _tapToContinue;            // 게임오버·엔딩 — 아무 데나 탭 = 재시작
        private bool _dialogueLocked;           // 프롤로그 대사 구간 — 컨트롤 숨김·입력 주입 중단
        private bool _stickSeen;
        private bool _buttonSeen;
        private bool _tapSeen;
        private Vector2 _stickDelta;
        // [v0.7] _snapMode 제거 — 스틱은 항상 8방향 이동이다 (기도 조준 폐기).
        private Interactable _shownTarget;
        private string _shownLabel;
        private bool _buttonVisible = true;

        private void Awake()
        {
            _active = TouchSupport.IsTouchDevice || forceEnable;
            _useMouse = forceEnable && !TouchSupport.IsTouchDevice;

            if (!_active)
            {
                // 데스크톱: 완전 비활성 — 이 프레임 이후 어떤 코드도 돌지 않는다
                if (controlsRoot != null) controlsRoot.SetActive(false);
                enabled = false;
                return;
            }

            InputReader.ResetTouchState(); // 씬 리로드 시 정적 잔존 상태 제거
            ApplyTouchHints();
            if (mobileAudioHint != null) mobileAudioHint.SetActive(true);
            SetButtonVisible(false);
        }

        private void OnEnable()
        {
            if (!_active) return;
            GameEvents.GameOver += HandleGameOver;
            GameEvents.EndingStarted += HandleEndingStarted;
        }

        private void OnDisable()
        {
            GameEvents.GameOver -= HandleGameOver;
            GameEvents.EndingStarted -= HandleEndingStarted;
            if (_active) InputReader.ResetTouchState();
        }

        private void Update()
        {
            // 프롤로그 대사 구간에는 온스크린 컨트롤을 치운다 (2026-08-06).
            // 그동안 상호작용은 어차피 잠겨 있고, 스틱을 잡는 터치가 곧 "다음 대사"로 읽혀
            // 걸어다니려다 대사를 날리는 사고가 난다 — 화면 전체가 대사 넘김 영역이기 때문.
            bool dialogueLock = flow != null && flow.PrologueDialogueLock;
            if (dialogueLock != _dialogueLocked)
            {
                _dialogueLocked = dialogueLock;
                ApplyDialogueLock();
            }
            if (_dialogueLocked) return;

            PollPointers();
            ApplyInput();
            UpdateVisuals();
        }

        /// <summary>대사 구간 진입·이탈 시 컨트롤 표시와 잔존 터치 정리.</summary>
        private void ApplyDialogueLock()
        {
            if (_dialogueLocked)
            {
                ReleaseStick();
                _buttonTouch = NoTouch;
                _tapTouch = NoTouch;
                InputReader.ResetTouchState(); // 쥐고 있던 손가락이 대사 뒤 기도로 이어지지 않게
            }
            if (controlsRoot != null && !_tapToContinue) controlsRoot.SetActive(!_dialogueLocked);
        }

        // ---------- 포인터 수집 ----------

        private void PollPointers()
        {
            _stickSeen = false;
            _buttonSeen = false;
            _tapSeen = false;

            if (_useMouse)
            {
                PollMouse();
            }
            else if (!PollLegacyTouches())
            {
                // 레거시 Input이 터치를 못 받는 경우의 보험 (같은 프레임에 두 소스를 섞지 않는다 —
                // 소스마다 손가락 id 체계가 달라 중복 배정이 생긴다)
                PollInputSystemTouches();
            }

            // 추적 중이던 포인터가 이번 프레임에 사라졌으면 해제 (Ended 유실 방어)
            if (_stickTouch != NoTouch && !_stickSeen) ReleaseStick();
            if (_buttonTouch != NoTouch && !_buttonSeen) _buttonTouch = NoTouch;
            if (_tapTouch != NoTouch && !_tapSeen) _tapTouch = NoTouch;
        }

        /// <summary>레거시 Input 터치. 이번 프레임에 하나라도 처리했으면 true.</summary>
        private bool PollLegacyTouches()
        {
            int count = Input.touchCount;
            for (int i = 0; i < count; i++)
            {
                Touch touch = Input.GetTouch(i);
                HandlePointer(touch.fingerId, touch.position,
                    touch.phase == TouchPhase.Began,
                    touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled);
            }
            return count > 0;
        }

        /// <summary>Input System Touchscreen 폴백 (Active Input Handling = Both라 둘 다 살아 있다).</summary>
        private void PollInputSystemTouches()
        {
#if ENABLE_INPUT_SYSTEM
            var screen = UnityEngine.InputSystem.Touchscreen.current;
            if (screen == null) return;

            var touches = screen.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                var touch = touches[i];
                UnityEngine.InputSystem.TouchPhase phase = touch.phase.ReadValue();
                if (phase == UnityEngine.InputSystem.TouchPhase.None) continue;

                HandlePointer(touch.touchId.ReadValue(), touch.position.ReadValue(),
                    phase == UnityEngine.InputSystem.TouchPhase.Began,
                    phase == UnityEngine.InputSystem.TouchPhase.Ended
                    || phase == UnityEngine.InputSystem.TouchPhase.Canceled);
            }
#endif
        }

        private void PollMouse()
        {
            bool down = Input.GetMouseButtonDown(0);
            bool up = Input.GetMouseButtonUp(0);
            if (!down && !up && !Input.GetMouseButton(0)) return;
            HandlePointer(MouseTouch, Input.mousePosition, down, up);
        }

        private void HandlePointer(int id, Vector2 screenPos, bool began, bool ended)
        {
            if (ended)
            {
                if (id == _stickTouch) ReleaseStick();
                if (id == _buttonTouch) _buttonTouch = NoTouch;
                if (id == _tapTouch) _tapTouch = NoTouch;
                return;
            }

            if (began)
            {
                if (_tapToContinue)
                {
                    _tapTouch = id;
                    _tapSeen = true;
                    return;
                }
                if (_buttonVisible && interactButton != null && InsideCircle(interactButton, screenPos, buttonRadius))
                {
                    _buttonTouch = id;
                    _buttonSeen = true;
                    return;
                }
                if (stickBase != null && InsideCircle(stickBase, screenPos, stickRadius * stickZoneScale))
                {
                    _stickTouch = id;
                    _stickSeen = true;
                    UpdateStickDelta(screenPos);
                    return;
                }
                return;
            }

            // 이동·유지 프레임 — 이미 배정된 포인터만 갱신
            if (id == _stickTouch)
            {
                _stickSeen = true;
                UpdateStickDelta(screenPos);
            }
            else if (id == _buttonTouch)
            {
                _buttonSeen = true;
            }
            else if (id == _tapTouch)
            {
                _tapSeen = true;
            }
        }

        private void UpdateStickDelta(Vector2 screenPos)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(stickBase, screenPos, null, out Vector2 local))
            {
                _stickDelta = local;
            }
        }

        private void ReleaseStick()
        {
            _stickTouch = NoTouch;
            _stickDelta = Vector2.zero;
        }

        /// <summary>스크린 좌표가 rect 중심에서 radius(캔버스 로컬 단위) 안인지. 오버레이 캔버스라 카메라 null.</summary>
        private static bool InsideCircle(RectTransform rect, Vector2 screenPos, float radius)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPos, null, out Vector2 local))
            {
                return false;
            }
            return local.sqrMagnitude <= radius * radius;
        }

        // ---------- InputReader 주입 ----------

        private void ApplyInput()
        {
            Vector2 axis = _stickTouch != NoTouch
                ? TouchStickModel.Resolve(_stickDelta, stickRadius, stickDeadZone01)
                : Vector2.zero;
            InputReader.SetTouchMove(axis);
            InputReader.SetTouchInteract(_buttonTouch != NoTouch || _tapTouch != NoTouch);
        }

        // ---------- 표시 ----------

        private void UpdateVisuals()
        {
            if (stickKnob != null)
            {
                Vector2 knob = _stickTouch != NoTouch
                    ? TouchStickModel.ClampKnob(_stickDelta, knobMaxOffset)
                    : Vector2.zero;
                if (stickKnob.anchoredPosition != knob) stickKnob.anchoredPosition = knob;
            }

            if (_tapToContinue)
            {
                SetButtonVisible(false);
                return;
            }

            // InteractPromptView와 같은 정보원 — 진행 중이면 그 대상, 아니면 최근접 후보
            Interactable target = interaction != null ? interaction.CurrentCandidate : null;
            string label = target != null ? target.PromptLabel : null;
            if (target != _shownTarget || label != _shownLabel)
            {
                _shownTarget = target;
                _shownLabel = label;
                SetButtonVisible(target != null);
                if (interactLabel != null && target != null) interactLabel.text = label;
            }
        }

        private void SetButtonVisible(bool visible)
        {
            if (_buttonVisible == visible) return;
            _buttonVisible = visible;
            if (interactButtonRoot != null) interactButtonRoot.SetActive(visible);
            if (!visible) _buttonTouch = NoTouch; // 사라지는 순간 홀드 해제 (유령 홀드 방지)
        }

        private void ApplyTouchHints()
        {
            if (keyboardHints == null) return;
            for (int i = 0; i < keyboardHints.Length; i++)
            {
                if (keyboardHints[i] != null) keyboardHints[i].text = touchHintText;
            }
        }

        // ---------- 구독 핸들러 ----------

        private void HandleGameOver(GameOverReason reason) => EnterTapToContinue();

        private void HandleEndingStarted(EndingKind kind) => EnterTapToContinue();

        /// <summary>게임오버·엔딩: 스틱·버튼을 치우고 화면 아무 데나 탭 = 재시작(InteractDown).</summary>
        private void EnterTapToContinue()
        {
            _tapToContinue = true;
            // 죽는 순간 누르고 있던 손가락이 그대로 재시작을 삼키지 않도록 전부 해제 — 새 탭만 인정
            ReleaseStick();
            _buttonTouch = NoTouch;
            _tapTouch = NoTouch;
            InputReader.ResetTouchState();
            if (controlsRoot != null) controlsRoot.SetActive(false);
        }
    }
}
