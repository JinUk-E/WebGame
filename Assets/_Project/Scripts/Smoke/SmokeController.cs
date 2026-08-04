using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Morae.Smoke
{
    /// <summary>
    /// D1 WebGL 스모크 테스트 컨트롤러 (architecture.md §8.6).
    /// ① 클릭 게이트 통과 전 오디오 절대 재생 금지 (§8.2 자동재생 정책)
    /// ② 클릭 시 뭉갬/선명 이중 소스를 같은 프레임에 동시 Play (§7.2)
    /// ③ Space 토글 → 0.3s 선형 볼륨 크로스페이드
    /// ④ 방향키 입력 로그 (페이지 스크롤 새는지 확인용)
    /// ⑤ 상태 변화 전부 [SMOKE] 로그 + 5초 평균 프레임레이트
    /// </summary>
    public sealed class SmokeController : MonoBehaviour
    {
        [SerializeField] private AudioSource muffledSource;
        [SerializeField] private AudioSource clearSource;
        [SerializeField] private GameObject clickGateOverlay;
        [SerializeField] private Button clickGateButton;
        [SerializeField] private float crossfadeSeconds = 0.3f;

        private bool _started;
        private bool _listening;      // false = 뭉갬(Normal), true = 선명(ListeningAtDoor)
        private float _fade01;        // 0 = 뭉갬 1.0/선명 0, 1 = 뭉갬 0/선명 1.0
        private bool _fading;
        private float _fpsTimer;
        private int _fpsFrames;

        private void Awake()
        {
            if (clickGateButton != null)
            {
                clickGateButton.onClick.AddListener(OnStartClicked);
            }
            Debug.Log("[SMOKE] Awake — 클릭 게이트 대기. 클릭 전 오디오 재생 없음 (playOnAwake=false, Play 호출 없음)");
        }

        private void OnDestroy()
        {
            if (clickGateButton != null)
            {
                clickGateButton.onClick.RemoveListener(OnStartClicked);
            }
        }

        private void OnStartClicked()
        {
            if (_started) return;
            _started = true;

            if (clickGateOverlay != null)
            {
                clickGateOverlay.SetActive(false);
            }

            // §7.2 — 두 소스를 같은 프레임에 동시 시작 (위치 동기 보장)
            muffledSource.volume = 1f;
            clearSource.volume = 0f;
            muffledSource.Play();
            clearSource.Play();

            Debug.Log($"[SMOKE] 클릭 게이트 통과 (frame {Time.frameCount}) — 오디오 언락");
            Debug.Log($"[SMOKE] 이중 소스 동시 재생 시작 (frame {Time.frameCount}): 뭉갬 1.0 / 선명 0.0, loop=true");
        }

        private void Update()
        {
            TrackFps();

            if (!_started) return;

            if (SpacePressed())
            {
                _listening = !_listening;
                _fading = true;
                Debug.Log(_listening
                    ? "[SMOKE] 크로스페이드 시작: 뭉갬 → 선명 (0.3s 선형)"
                    : "[SMOKE] 크로스페이드 시작: 선명 → 뭉갬 (0.3s 선형)");
            }

            LogArrowKeys();

            if (_fading)
            {
                float target = _listening ? 1f : 0f;
                _fade01 = Mathf.MoveTowards(_fade01, target, Time.deltaTime / Mathf.Max(0.01f, crossfadeSeconds));
                muffledSource.volume = 1f - _fade01;
                clearSource.volume = _fade01;
                if (Mathf.Approximately(_fade01, target))
                {
                    _fading = false;
                    Debug.Log($"[SMOKE] 크로스페이드 완료: {(_listening ? "선명" : "뭉갬")} (뭉갬 {muffledSource.volume:F2} / 선명 {clearSource.volume:F2})");
                }
            }
        }

        private void TrackFps()
        {
            _fpsFrames++;
            _fpsTimer += Time.unscaledDeltaTime;
            if (_fpsTimer >= 5f)
            {
                Debug.Log($"[SMOKE] 프레임레이트 5초 평균: {_fpsFrames / _fpsTimer:F1} fps");
                _fpsTimer = 0f;
                _fpsFrames = 0;
            }
        }

        private static bool SpacePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Space);
#endif
        }

        private static void LogArrowKeys()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.upArrowKey.wasPressedThisFrame) Debug.Log("[SMOKE] 방향키 입력: Up");
            if (kb.downArrowKey.wasPressedThisFrame) Debug.Log("[SMOKE] 방향키 입력: Down");
            if (kb.leftArrowKey.wasPressedThisFrame) Debug.Log("[SMOKE] 방향키 입력: Left");
            if (kb.rightArrowKey.wasPressedThisFrame) Debug.Log("[SMOKE] 방향키 입력: Right");
#else
            if (Input.GetKeyDown(KeyCode.UpArrow)) Debug.Log("[SMOKE] 방향키 입력: Up");
            if (Input.GetKeyDown(KeyCode.DownArrow)) Debug.Log("[SMOKE] 방향키 입력: Down");
            if (Input.GetKeyDown(KeyCode.LeftArrow)) Debug.Log("[SMOKE] 방향키 입력: Left");
            if (Input.GetKeyDown(KeyCode.RightArrow)) Debug.Log("[SMOKE] 방향키 입력: Right");
#endif
        }
    }
}
