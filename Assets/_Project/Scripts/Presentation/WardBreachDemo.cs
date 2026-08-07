using Morae.Game.Core;
using Morae.Game.Data;
using UnityEngine;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 첫 공격 시연 — <b>귀신이 소금에 막히는 장면</b> (표현 계층, 구독만).
    ///
    /// <para>
    /// <b>왜 필요한가.</b> 지금까지 게임은 "검은 소금 = 위험"만 가르치고
    /// <b>"소금이 애초에 널 막아주는 물건"이라는 걸 한 번도 안 보여줬다.</b> 순서가 거꾸로였다 —
    /// 방어 수단인 줄 모르는 물건이 더러워지는 걸 보면, 그건 고쳐야 할 고장이 아니라 그냥 나쁜 표시다.
    /// 그래서 첫 전조 때 인과를 통째로 한 장면에 담는다:
    /// </para>
    /// <code>
    /// 귀신 접근 → 소금선에서 튕김(섬광·귀신 후퇴) → 그 대가로 소금이 검어짐 → 플레이어가 새로 뿌림 → 귀신이 물러남
    /// </code>
    /// <para>
    /// 전조(AttackTelegraphStarted) 동안 다가와서, 판정(AttackResolved) 순간 튕겨 나간다 —
    /// <b>기존 이벤트에 얹기만 하고 게임플레이는 건드리지 않는다.</b> 그래서 이 컴포넌트를 꺼도 규칙은 그대로다.
    /// </para>
    /// <para>
    /// 기본은 <see cref="onlyFirstTime"/> — 첫 1회만 나온다. 매번 나오면 시연이 아니라 연출 소음이 되고,
    /// 무엇보다 "다가오는 게 보이니까 그때 가면 된다"는 잘못된 학습을 준다(실제로는 전조를 못 막는다).
    /// </para>
    /// </summary>
    public sealed class WardBreachDemo : MonoBehaviour
    {
        [SerializeField] private Transform[] cornerAnchors = new Transform[CornerIndex.Count];
        [SerializeField] private SpriteRenderer ghost;
        [SerializeField] private Sprite ghostSprite;
        [SerializeField] private bool onlyFirstTime = true;

        [Header("접근")]
        [SerializeField] private float approachFromOutside = 3.2f; // 벽 바깥 시작 거리
        [SerializeField] private float stopDistance = 1.1f;        // 소금선 앞에서 멈추는 거리
        [SerializeField] private float ghostMaxAlpha = 0.72f;

        [Header("튕김")]
        [SerializeField] private float repelSec = 0.7f;
        [SerializeField] private float repelDistance = 2.6f;
        [SerializeField] private Color flashColor = new Color(1f, 0.98f, 0.9f);
        [SerializeField] private float flashSec = 0.28f;

        private int _corner = CornerIndex.None;
        private float _telegraphSec;
        private float _elapsed;
        private bool _repelling;
        private float _repelElapsed;
        private Vector3 _from;
        private Vector3 _stop;
        private bool _used;

        private void Awake()
        {
            if (ghost != null)
            {
                if (ghostSprite != null) ghost.sprite = ghostSprite;
                ghost.enabled = false;
            }
        }

        private void OnEnable()
        {
            GameEvents.AttackTelegraphStarted += HandleTelegraph;
            GameEvents.AttackResolved += HandleResolved;
        }

        private void OnDisable()
        {
            GameEvents.AttackTelegraphStarted -= HandleTelegraph;
            GameEvents.AttackResolved -= HandleResolved;
        }

        private void HandleTelegraph(int corner, float duration)
        {
            if (_used && onlyFirstTime) return;
            if (ghost == null || corner < 0 || corner >= cornerAnchors.Length) return;
            Transform anchor = cornerAnchors[corner];
            if (anchor == null) return;

            _corner = corner;
            _telegraphSec = Mathf.Max(0.2f, duration);
            _elapsed = 0f;
            _repelling = false;

            // 귀퉁이에서 방 바깥으로 뻗은 방향 — 귀신은 항상 "밖에서" 온다
            Vector3 outward = anchor.position.normalized;
            if (outward.sqrMagnitude < 0.01f) outward = Vector3.up;
            _stop = anchor.position + outward * stopDistance;
            _from = anchor.position + outward * (stopDistance + approachFromOutside);

            ghost.transform.position = _from;
            ghost.enabled = true;
            SetAlpha(0f);
        }

        private void HandleResolved(int corner, bool discarded)
        {
            if (corner != _corner || ghost == null || !ghost.enabled) return;
            if (discarded) { Hide(); return; }  // 게임오버·엔딩으로 전조가 접힌 경우 — 연출도 접는다

            // 소금이 막아냈다. 귀신은 튕겨 나가고, 그 대가로 소금이 검어진다
            // (오염 자체는 SaltCorners가 이미 처리했다 — 여기서는 그 인과를 눈에 보이게만 한다).
            _repelling = true;
            _repelElapsed = 0f;
            _used = true;
            GameEvents.RaiseSaltAttentionRequested(_corner, flashSec + repelSec);
        }

        private void Update()
        {
            if (ghost == null || !ghost.enabled) return;
            float dt = Time.deltaTime;

            if (!_repelling)
            {
                _elapsed += dt;
                float t = Mathf.Clamp01(_elapsed / _telegraphSec);
                // 뒤로 갈수록 느려진다 — 벽에 닿기 직전의 망설임처럼 보이게
                ghost.transform.position = Vector3.Lerp(_from, _stop, 1f - (1f - t) * (1f - t));
                SetAlpha(ghostMaxAlpha * Mathf.Clamp01(t * 2f));
                return;
            }

            _repelElapsed += dt;
            float r = Mathf.Clamp01(_repelElapsed / repelSec);
            Vector3 outward = (_stop - cornerAnchors[_corner].position).normalized;
            ghost.transform.position = _stop + outward * (repelDistance * r * r); // 가속 후퇴 = "튕겨났다"
            SetAlpha(ghostMaxAlpha * (1f - r));

            // 튕기는 순간 소금선이 하얗게 탄다 — 막았다는 사실을 색으로 못 박는다
            if (_repelElapsed <= flashSec)
            {
                ghost.color = Color.Lerp(ghost.color, flashColor, 0.5f);
            }

            if (r >= 1f) Hide();
        }

        private void Hide()
        {
            _repelling = false;
            _corner = CornerIndex.None;
            if (ghost != null) ghost.enabled = false;
        }

        private void SetAlpha(float a)
        {
            Color c = ghost.color;
            c.a = a;
            ghost.color = c;
        }
    }
}
