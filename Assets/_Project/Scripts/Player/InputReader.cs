using UnityEngine;

namespace Morae.Game.Player
{
    /// <summary>
    /// 입력 정적 래퍼 (architecture §3.3) — 내부는 레거시 Input API.
    /// Active Input Handling = Both 전환 완료(_shared.md 2026-08-04) 전제.
    /// 키보드 전용 6키: WASD/방향키 · E · Esc. 입력 API 교체 시 이 파일만 수정.
    /// </summary>
    public static class InputReader
    {
        /// <summary>8방향 이동 축 (정규화, 대각선 등속).</summary>
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
                return v.sqrMagnitude > 1f ? v.normalized : v;
            }
        }

        public static bool InteractDown => Input.GetKeyDown(KeyCode.E);
        public static bool InteractHeld => Input.GetKey(KeyCode.E);
        public static bool InteractUp => Input.GetKeyUp(KeyCode.E);
        public static bool EscapeDown => Input.GetKeyDown(KeyCode.Escape);
    }
}
