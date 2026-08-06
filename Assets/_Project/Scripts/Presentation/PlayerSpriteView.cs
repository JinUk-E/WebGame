using Morae.Game.Core;
using Morae.Game.Data;
using Morae.Game.Player;
using UnityEngine;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 플레이어 스프라이트 표시 제어 (표현 계층 — PlayerStateChanged 구독만, §1.2).
    ///
    /// InBlanket 동안 렌더러를 숨긴다 — 몸은 이불 융기(BlanketView의 bulge)가 대신 표현.
    ///
    /// v0.6 — **회전을 버리고 방향별 스프라이트로 바꿨다.** 탑뷰 그림 한 장을 Z회전시키면
    /// 사람이 걷는 게 아니라 물체가 도는 것으로 보인다(위에서 누른 벌레). 위/아래/옆 3세트를 두고
    /// 좌측은 옆 세트를 flipX로 쓴다.
    ///
    /// 걷기 프레임은 **시간이 아니라 이동 거리**로 넘긴다 — 벽에 붙어 밀고 있을 때 제자리걸음이
    /// 재생되면 "움직이는 줄 알았는데 안 나갔다"는 오해가 생긴다. 실제로 간 만큼만 걷는다.
    /// 대각 입력에서는 가로 세트를 우선한다 — 옆모습이 실루엣으로 가장 잘 읽힌다.
    /// </summary>
    public sealed class PlayerSpriteView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private PlayerController player;

        [Header("방향별 프레임 (0 = 정지, 1~ = 걷기)")]
        [SerializeField] private Sprite[] upFrames;      // 화면 위로 = 등
        [SerializeField] private Sprite[] downFrames;    // 화면 아래로 = 정면
        [SerializeField] private Sprite[] sideFrames;    // 오른쪽 기준 — 왼쪽은 flipX

        [SerializeField] private float stepDistance = 0.45f;   // 이 거리마다 프레임 교체
        [SerializeField] private float diagonalBias = 1.15f;   // 대각에서 옆모습을 얼마나 우대할지

        private enum Facing { Down, Up, Side }

        private Facing _facing = Facing.Down;
        private bool _flip;
        private int _walkIndex;
        private float _travelled;
        private Vector3 _lastPos;

        private void Awake() => _lastPos = transform.position;

        private void OnEnable()
        {
            GameEvents.PlayerStateChanged += HandleStateChanged;
            HandleStateChanged(PlayerState.Idle); // 씬 시작 상태 동기화 (재시작 = 씬 리로드 → 항상 Idle)
        }

        private void OnDisable()
        {
            GameEvents.PlayerStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(PlayerState state)
        {
            if (body == null) return;
            bool visible = state != PlayerState.InBlanket;
            if (body.enabled != visible) body.enabled = visible;
        }

        private void Update()
        {
            if (body == null || player == null) return;

            // 회전은 쓰지 않는다 — 과거 배선이나 프리팹에 남아 있어도 여기서 원상복구한다
            if (body.transform.localRotation != Quaternion.identity)
                body.transform.localRotation = Quaternion.identity;

            Vector3 pos = transform.position;
            float moved = (pos - _lastPos).magnitude;
            _lastPos = pos;

            UpdateFacing();

            if (moved > 0.0001f)
            {
                _travelled += moved;
                while (_travelled >= stepDistance)
                {
                    _travelled -= stepDistance;
                    _walkIndex++;
                }
            }
            else
            {
                // 멈추면 정지 프레임으로 — 다음 출발이 항상 같은 발부터 시작하도록 위상도 리셋
                _travelled = 0f;
                _walkIndex = 0;
            }

            Apply(moved > 0.0001f);
        }

        private void UpdateFacing()
        {
            Vector2 f = player.Facing;
            if (f.sqrMagnitude < 0.0001f) return;

            if (Mathf.Abs(f.x) * diagonalBias >= Mathf.Abs(f.y))
            {
                _facing = Facing.Side;
                _flip = f.x < 0f;   // 원본이 오른쪽 기준
            }
            else
            {
                _facing = f.y > 0f ? Facing.Up : Facing.Down;
                _flip = false;
            }
        }

        private void Apply(bool moving)
        {
            Sprite[] set = _facing == Facing.Up ? upFrames
                : _facing == Facing.Side ? sideFrames
                : downFrames;

            if (set == null || set.Length == 0) return;   // 미배선이면 기존 스프라이트를 그대로 둔다

            Sprite next;
            if (!moving || set.Length == 1)
            {
                next = set[0];
            }
            else
            {
                // 0번은 정지 전용 — 걷는 동안에는 1~ 사이만 순환한다
                int walkCount = set.Length - 1;
                next = set[1 + _walkIndex % walkCount];
            }

            if (body.sprite != next) body.sprite = next;
            if (body.flipX != _flip) body.flipX = _flip;
        }
    }
}
