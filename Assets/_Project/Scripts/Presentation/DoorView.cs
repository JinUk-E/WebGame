using Morae.Game.Core;
using Morae.Game.Data;
using UnityEngine;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 문 개폐 표시 (v0.6 — 표현 계층, 구독만 §1.2).
    ///
    /// 아트(닫힘/열림/문틈 빛)는 v0.6에 들어왔지만 스왑해줄 코드가 없어 늘 닫힌 그림만 나왔다.
    /// 문이 열리는 순간은 이 게임에서 되돌릴 수 없는 유일한 선택이라, 그림으로 한 번은 보여야 한다.
    ///
    /// 여는 경로는 둘이고 문법이 다르다:
    ///   ① 플레이어가 민다 (<see cref="GameEvents.DoorOpened"/>) — 걸쇠가 풀리고 열린다
    ///   ② 07:40 K씨가 밖에서 연다 (<see cref="EndingKind.Rescued"/>) — 내가 열지 않았는데 열린다
    /// 둘 다 결과 화면이 곧 덮으므로 전환은 즉시다. 페이드를 넣으면 열린 문을 못 보고 끝난다.
    ///
    /// 걸쇠를 미는 동안에는 닫힌 문이 진행률만큼 덜컹거린다 — 진행 바(ChannelBarView)의 촉각적 짝이다.
    /// </summary>
    public sealed class DoorView : MonoBehaviour
    {
        [SerializeField] private GameObject closedVisual;
        [SerializeField] private GameObject openVisual;
        [SerializeField] private Transform shakeTarget;      // 보통 닫힘 스프라이트

        [SerializeField] private float shakeAmplitude = 0.035f;
        [SerializeField] private float shakeHz = 17f;

        private Vector3 _restPos;
        private bool _opened;
        private float _latch;

        private void Awake()
        {
            if (shakeTarget != null) _restPos = shakeTarget.localPosition;
            Apply(false);
        }

        private void OnEnable()
        {
            GameEvents.DoorOpened += HandleOpened;
            GameEvents.EndingStarted += HandleEnding;
            GameEvents.DoorLatchProgressChanged += HandleLatch;
        }

        private void OnDisable()
        {
            GameEvents.DoorOpened -= HandleOpened;
            GameEvents.EndingStarted -= HandleEnding;
            GameEvents.DoorLatchProgressChanged -= HandleLatch;
        }

        private void HandleOpened() => Open();

        private void HandleEnding(EndingKind kind)
        {
            // Perfect·Survived는 플레이어가 연 직후라 이미 열려 있다. Rescued만 여기서 처음 열린다.
            if (kind == EndingKind.Rescued) Open();
        }

        private void HandleLatch(float progress01) => _latch = Mathf.Clamp01(progress01);

        private void Open()
        {
            if (_opened) return;
            _opened = true;
            _latch = 0f;
            if (shakeTarget != null) shakeTarget.localPosition = _restPos;
            Apply(true);
            Debug.Log("[DOOR-VIEW] 문 열림 표시");
        }

        private void Apply(bool open)
        {
            if (closedVisual != null && closedVisual.activeSelf == open) closedVisual.SetActive(!open);
            if (openVisual != null && openVisual.activeSelf != open) openVisual.SetActive(open);
        }

        private void Update()
        {
            if (_opened || shakeTarget == null) return;
            if (_latch <= 0.001f)
            {
                if (shakeTarget.localPosition != _restPos) shakeTarget.localPosition = _restPos;
                return;
            }
            // 진행할수록 크게 — 걸쇠가 버티다 못해 흔들리는 그림
            float amp = shakeAmplitude * _latch;
            float ph = Time.time * shakeHz * 2f * Mathf.PI;
            shakeTarget.localPosition = _restPos + new Vector3(Mathf.Sin(ph) * amp,
                                                              Mathf.Sin(ph * 1.31f) * amp * 0.4f, 0f);
        }
    }
}
