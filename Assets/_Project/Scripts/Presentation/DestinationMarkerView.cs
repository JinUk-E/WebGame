using System;
using Morae.Game.Core;
using Morae.Game.Data;
using UnityEngine;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 목적지 서클 — 불상 앞 바닥의 "여기 서라" 마커 (v0.6.1 — 표현 계층, 구독만 §1.2).
    ///
    /// <para>
    /// <b>왜 필요한가</b>: 학습 구간에서 소금 귀퉁이의 흰 섬광(SaltAttentionRequested)이 방향을 가리키지만,
    /// 후광은 <b>물건</b>을 가리킬 뿐 <b>설 자리</b>를 가리키지 못한다. 어두운 방에서 처음 온 플레이어는
    /// "저게 불상이구나"까지는 알아도 "얼마나 가까이 가야 기도가 되는가"를 모른 채 근처를 서성인다.
    /// 서클은 기도 판정 범위(불상 트리거) 안의 한 점을 바닥에 찍어 그 질문을 없앤다.
    /// </para>
    ///
    /// <para>
    /// <b>도착 피드백</b>: 마커 위에 서면 맥동이 멈추고 색이 바뀐다 — "여기가 맞다"가 손이 아니라 눈으로 온다.
    /// 판정 반경은 소금 트리거(2.2×2.2u)보다 <b>충분히 안쪽</b>이라
    /// 서클이 켜졌는데 상호작용이 안 되는 거짓 신호가 나오지 않는다.
    /// </para>
    ///
    /// <para>
    /// <b>시각 문법</b> (v0.6 규약): 붉은색은 전조의 것이므로 쓰지 않는다 — 주의 유도는 따뜻한 아이보리 계열.
    /// 결계 소금길(salt_ward, 방 둘레를 도는 가는 폐곡선)·기도 빔(직선 광선)과 형태가 겹치지 않게
    /// <b>바닥에 깔린 작은 원반 + 브래킷</b>으로 그린다. 렌더러는 무광(Sprites-Default) —
    /// 학습 스포트라이트로 방이 더 어두워질수록 오히려 또렷해야 한다.
    /// </para>
    ///
    /// <para>
    /// <b>수명</b>: 등장 = 학습 구간 + 조작 안내 이벤트(<c>prologue-controls</c>, 후광과 같은 순간).
    /// 소멸 = 학습 종료(TrainingModeChanged=false) 또는 본편 진입(PhaseChanged) — 두 겹으로 막는다.
    /// 프롤로그를 건너뛴 2회차는 학습 자체가 없으므로 애초에 뜨지 않는다.
    /// </para>
    /// </summary>
    public sealed class DestinationMarkerView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer marker;
        [SerializeField] private Transform player;
        [SerializeField] private string showEventId = "prologue-controls";

        [Header("판정")]
        // 불상 트리거는 중심 (-1,2)·2.2×2.2u라 x∈[-2.1,0.1] · y∈[0.9,3.1]. 마커는 그 안쪽 바닥에 있고,
        // 이 반경은 마커 원반의 시각 반경과 같게 잡는다 — 보이는 것과 판정이 다르면 피드백이 거짓말이 된다.
        [SerializeField] private float arriveRadius = 0.55f;
        // 바닥 원반이 탑뷰 원근으로 눌린 비율 — 스프라이트(gen_marker.py SQUASH)와 같은 값이어야
        // "보이는 타원 = 판정"이 유지된다. 스프라이트를 다시 뽑으면 이 값도 같이 맞출 것.
        [SerializeField] private float verticalSquash = 0.78f;

        [Header("연출")]
        [SerializeField] private Color idleColor = new Color(1f, 0.92f, 0.72f, 0.5f);   // 따뜻한 아이보리 (전조의 붉은색과 분리)
        [SerializeField] private Color arrivedColor = new Color(1f, 0.98f, 0.9f, 0.85f); // 도착 = 더 밝고 차분하게
        [SerializeField] private float fadeSec = 0.35f;
        [SerializeField] private float pulseHz = 0.75f;
        [SerializeField] private float pulseDepth = 0.35f;   // 맥동 깊이 (도착하면 0으로 잦아든다)
        [SerializeField] private float pulseScale = 0.08f;
        [SerializeField] private float colorLerpSec = 0.25f;

        private bool _trainingActive;
        private bool _cued;
        private float _alpha;
        private float _arrived01;      // 0 = 밖, 1 = 마커 위 (색·맥동을 같이 끈다)
        private Vector3 _baseScale = Vector3.one;

        private bool Shown => _trainingActive && _cued;

        private void Awake()
        {
            if (marker == null) marker = GetComponent<SpriteRenderer>();
            if (marker != null)
            {
                _baseScale = marker.transform.localScale;
                marker.enabled = false;
            }
        }

        private void OnEnable()
        {
            GameEvents.TrainingModeChanged += HandleTrainingModeChanged;
            GameEvents.GameEventFired += HandleGameEventFired;
            GameEvents.PhaseChanged += HandlePhaseChanged;
            GameEvents.SaltAttentionRequested += HandleSaltAttention;
        }

        private void OnDisable()
        {
            GameEvents.TrainingModeChanged -= HandleTrainingModeChanged;
            GameEvents.GameEventFired -= HandleGameEventFired;
            GameEvents.PhaseChanged -= HandlePhaseChanged;
            GameEvents.SaltAttentionRequested -= HandleSaltAttention;
        }

        /// <summary>
        /// v0.7 — 마커가 불상 앞 <b>고정 자리</b>였던 시절에는 옮길 필요가 없었다. 이제 학습 대상이
        /// 소금 귀퉁이라 "여기로 가라"의 여기가 매번 다르다. 주의 유도가 오면 그 귀퉁이 앞으로 옮겨간다.
        /// <para>
        /// 좌표는 <see cref="TrainingStageModel.StandPointFor"/>가 준다 — 귀퉁이 정중앙이 아니라
        /// <b>실제로 설 수 있는 자리</b>다. 좌상단은 플레이어 이동 상한(y 1.515)에 0.015u까지 붙어 있어
        /// 정중앙에 서클을 놓으면 벽에 끼여 도착 판정이 영영 안 난다.
        /// </para>
        /// </summary>
        private void HandleSaltAttention(int corner, float seconds)
        {
            if (marker == null) return;
            Vector2 stand = TrainingStageModel.StandPointFor(corner);
            Vector3 p = marker.transform.position;
            marker.transform.position = new Vector3(stand.x, stand.y, p.z);
        }

        private void HandleTrainingModeChanged(bool active)
        {
            _trainingActive = active;
            if (!active) _cued = false;   // 다음 학습(재시작)은 조작 안내부터 다시 시작한다
        }

        private void HandleGameEventFired(EventDef def)
        {
            if (def == null || string.IsNullOrEmpty(showEventId)) return;
            if (!string.Equals(def.Id, showEventId, StringComparison.Ordinal)) return;
            _cued = true;
        }

        /// <summary>본편 시작(P1) — 학습 무대는 끝났다. TrainingModeChanged와 겹치는 안전망이다.</summary>
        private void HandlePhaseChanged(PhaseId phase)
        {
            _trainingActive = false;
            _cued = false;
        }

        private void Update()
        {
            if (marker == null) return;

            bool show = Shown;
            _alpha = Mathf.MoveTowards(_alpha, show ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, fadeSec));
            if (_alpha <= 0.001f)
            {
                if (marker.enabled) marker.enabled = false;
                return;
            }
            if (!marker.enabled) marker.enabled = true;

            bool on = show && player != null
                      && TrainingStageModel.IsOnMarker(player.position, marker.transform.position,
                          arriveRadius, verticalSquash);
            _arrived01 = Mathf.MoveTowards(_arrived01, on ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, colorLerpSec));

            // 도착하면 맥동이 잦아들고 색이 밝아진다 — 두 신호가 같은 값(_arrived01)에 묶여 있어 어긋나지 않는다
            float breathe = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseHz * 2f * Mathf.PI);
            float depth = pulseDepth * (1f - _arrived01);
            Color c = Color.Lerp(idleColor, arrivedColor, _arrived01);
            c.a *= _alpha * (1f - depth + depth * breathe);
            marker.color = c;

            float s = 1f + pulseScale * (1f - _arrived01) * breathe;
            marker.transform.localScale = _baseScale * s;
        }
    }
}
