using Morae.Game.Core;
using Morae.Game.Data;
using UnityEngine;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 창문 흔들림 (표현 계층 — <see cref="GameEvents.GameEventFired"/> 구독만. 게임플레이 역참조 없음).
    ///
    /// <para>
    /// 창문 이벤트 2종은 소리만 나고 <b>그림은 가만히 있었다</b> — "통, 통" 자막이 뜨는데
    /// 창호지는 미동도 없으니 소리의 출처가 화면 어디에도 없었다.
    /// 이제 창틀·창호지가 그 박자에 맞춰 떤다. 박자는 <see cref="RattlePattern"/>이 소유하고
    /// 소리(클립 내부 온셋)와 같은 표를 본다.
    /// </para>
    ///
    /// <para>
    /// 창은 벽에 물려 있으므로 <b>미세한 격자 진동</b>이다 — 크게 흔들면 창문이 뜯어지는 그림이 되고,
    /// 무엇보다 귀퉁이 소금의 전조 흔들림(대응해야 하는 신호)과 헷갈린다.
    /// </para>
    ///
    /// <para>
    /// 위치만 만진다. 회전은 금지 — 탑뷰라 창을 돌리면 열리는 것처럼 보인다.
    /// 변위는 항상 원위치 + 절대 오프셋이라 누적 드리프트가 원리적으로 없다.
    /// </para>
    /// </summary>
    public sealed class WindowRattleView : MonoBehaviour
    {
        [Tooltip("흔들 대상 — 보통 Room/Window/Visual (루트를 흔들면 여명 라이트와 어긋난다)")]
        [SerializeField] private Transform target;

        [Header("이벤트 id — EventTable과 짝")]
        [SerializeField] private string knockEventId = "window-knock";
        [SerializeField] private string rattleEventId = "window-rattle";

        [Tooltip("실기 튜닝용 배수. 1 = RattlePattern 기본 진폭")]
        [SerializeField] private float amplitudeScale = 1f;

        private Vector3 _restPos;
        private RattleKind _kind;
        private float _startTime;
        private bool _active;

        private void Awake()
        {
            if (target == null) target = transform;
            _restPos = target.localPosition;
        }

        private void OnEnable() => GameEvents.GameEventFired += HandleGameEventFired;

        private void OnDisable()
        {
            GameEvents.GameEventFired -= HandleGameEventFired;
            Rest(); // 씬 리로드·비활성 시 흔들리던 자리에 얼어붙지 않게
        }

        private void HandleGameEventFired(EventDef def)
        {
            if (def.Id == knockEventId) Begin(RattleKind.WindowKnock);
            else if (def.Id == rattleEventId) Begin(RattleKind.WindowRattle);
        }

        private void Begin(RattleKind kind)
        {
            _kind = kind;
            _startTime = Time.time;   // SoundManager의 두드림 계산과 같은 시계 (§KnockRhythm 선례)
            _active = true;
        }

        private void Update()
        {
            if (!_active || target == null) return;

            float elapsed = Time.time - _startTime;
            if (!RattlePattern.IsActive(_kind, elapsed))
            {
                _active = false;
                Rest();
                return;
            }

            Vector2 offset = RattlePattern.Offset(_kind, elapsed) * amplitudeScale;
            target.localPosition = _restPos + new Vector3(offset.x, offset.y, 0f);
        }

        /// <summary>정확한 원위치 복구 — 가산이 아니라 대입이라 오차가 쌓일 자리가 없다.</summary>
        private void Rest()
        {
            if (target != null) target.localPosition = _restPos;
        }
    }
}
