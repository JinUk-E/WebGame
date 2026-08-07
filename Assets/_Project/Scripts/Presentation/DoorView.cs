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
    ///
    /// <para>
    /// P5 삼중 습격의 <b>손잡이 덜컹</b>도 여기서 그린다 (<see cref="RattlePattern.DoorHandle"/> 박자,
    /// 소리는 SoundManager가 같은 표로 낸다). 문짝의 위치를 만지는 코드가 둘이면 서로 덮어쓰므로
    /// <b>이 컴포넌트가 문짝 변위를 단독 소유</b>한다 — 걸쇠 흔들림과 손잡이 덜컹은 여기서 합쳐진다.
    /// </para>
    /// </summary>
    public sealed class DoorView : MonoBehaviour
    {
        [SerializeField] private GameObject closedVisual;
        [SerializeField] private GameObject openVisual;
        [SerializeField] private Transform shakeTarget;      // 보통 닫힘 스프라이트

        [SerializeField] private float shakeAmplitude = 0.035f;
        [SerializeField] private float shakeHz = 17f;

        [Header("손잡이 덜컹 (P5 삼중 습격)")]
        [SerializeField] private string handleRattleEventId = TripleAssaultCue.EventId;
        [Tooltip("실기 튜닝용 배수. 1 = RattlePattern 기본 진폭")]
        [SerializeField] private float handleRattleScale = 1f;

        private Vector3 _restPos;
        private bool _opened;
        private float _latch;
        private float _rattleStart;
        private bool _rattling;

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
            GameEvents.GameEventFired += HandleGameEventFired;
        }

        private void OnDisable()
        {
            GameEvents.DoorOpened -= HandleOpened;
            GameEvents.EndingStarted -= HandleEnding;
            GameEvents.DoorLatchProgressChanged -= HandleLatch;
            GameEvents.GameEventFired -= HandleGameEventFired;
            _rattling = false;
            if (shakeTarget != null) shakeTarget.localPosition = _restPos;
        }

        private void HandleOpened() => Open();

        private void HandleGameEventFired(EventDef def)
        {
            if (def.Id != handleRattleEventId) return;
            _rattleStart = Time.time;   // SoundManager가 같은 프레임에 같은 시계로 시작한다
            _rattling = true;
        }

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
            _rattling = false;
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

            // 손잡이 덜컹 — 짧고 강한 변위 후 감쇠 (박자·감쇠는 RattlePattern이 소유)
            Vector2 rattle = Vector2.zero;
            if (_rattling)
            {
                float elapsed = Time.time - _rattleStart;
                if (RattlePattern.IsActive(RattleKind.DoorHandle, elapsed))
                    rattle = RattlePattern.Offset(RattleKind.DoorHandle, elapsed) * handleRattleScale;
                else
                    _rattling = false;
            }

            // 걸쇠 진행 — 진행할수록 크게. 걸쇠가 버티다 못해 흔들리는 그림
            Vector2 latch = Vector2.zero;
            if (_latch > 0.001f)
            {
                float amp = shakeAmplitude * _latch;
                float ph = Time.time * shakeHz * 2f * Mathf.PI;
                latch = new Vector2(Mathf.Sin(ph) * amp, Mathf.Sin(ph * 1.31f) * amp * 0.4f);
            }

            // 두 흔들림이 겹치면 **더 센 쪽만** 쓴다 — 더하면 합이 진폭 한계를 넘어 문이 벽을 뚫는다
            Vector2 offset = rattle.sqrMagnitude >= latch.sqrMagnitude ? rattle : latch;
            if (offset == Vector2.zero)
            {
                if (shakeTarget.localPosition != _restPos) shakeTarget.localPosition = _restPos;
                return;
            }
            shakeTarget.localPosition = _restPos + new Vector3(offset.x, offset.y, 0f);
        }
    }
}
