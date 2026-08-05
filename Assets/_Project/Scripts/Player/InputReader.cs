using UnityEngine;

namespace Morae.Game.Player
{
    /// <summary>
    /// 입력 정적 래퍼 (architecture §3.3) — 내부는 레거시 Input API.
    /// Active Input Handling = Both 전환 완료(_shared.md 2026-08-04) 전제.
    /// 키보드 6키: WASD/방향키 · E · Esc. 입력 API 교체 시 이 파일만 수정.
    /// <para>
    /// [2026-08-05 모바일] 키보드 + <b>온스크린 터치</b> 합성. 게임 로직은 여전히 InputReader만 본다 —
    /// TouchControlsView가 <see cref="SetTouchMove"/>·<see cref="SetTouchInteract"/>로 값을 밀어넣고,
    /// 여기서 키보드 값과 합친다. 데스크톱에서는 터치 값이 항상 기본값이라 기존 경로와 완전히 동일하다.
    /// </para>
    /// </summary>
    public static class InputReader
    {
        private static Vector2 _touchMove;
        private static readonly TouchButtonLatch TouchInteract = new TouchButtonLatch();

        /// <summary>8방향 이동 축 (정규화, 대각선 등속). 키보드 우선 — 키 입력이 없을 때만 터치 스틱 값.</summary>
        public static Vector2 MoveAxis
        {
            get
            {
                float x = 0f;
                float y = 0f;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1f;
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1f;
                var v = new Vector2(x, y);
                if (v.sqrMagnitude <= 0f) return _touchMove;
                return v.sqrMagnitude > 1f ? v.normalized : v;
            }
        }

        public static bool InteractDown => Input.GetKeyDown(KeyCode.E) || TouchInteract.Down(Time.frameCount);
        public static bool InteractHeld => Input.GetKey(KeyCode.E) || TouchInteract.Held;
        public static bool InteractUp => Input.GetKeyUp(KeyCode.E) || TouchInteract.Up(Time.frameCount);
        public static bool EscapeDown => Input.GetKeyDown(KeyCode.Escape);

        // ---------- 터치 주입 (TouchControlsView 전용) ----------

        /// <summary>가상 스틱의 스냅된 방향 벡터 (키보드와 같은 값 규약 — TouchStickModel이 보장).</summary>
        public static void SetTouchMove(Vector2 snappedAxis) => _touchMove = snappedAxis;

        /// <summary>상호작용 버튼 눌림 상태. Down/Up 엣지는 프레임 래치가 만든다.</summary>
        public static void SetTouchInteract(bool pressed) => TouchInteract.Set(pressed);

        /// <summary>씬 리로드·컨트롤 비활성 시 터치 잔존 상태 제거 (정적 상태 누수 방지).</summary>
        public static void ResetTouchState()
        {
            _touchMove = Vector2.zero;
            TouchInteract.Reset();
        }
    }
}
