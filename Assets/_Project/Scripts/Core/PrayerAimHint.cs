using Morae.Game.Data;
using UnityEngine;

namespace Morae.Game.Core
{
    /// <summary>조작 힌트에서 강조할 방향키.</summary>
    public enum AimKey { Up = 0, Down = 1, Left = 2, Right = 3 }

    /// <summary>
    /// 기도 조준 조작 힌트의 순수 매핑 (2026-08-06) — 귀퉁이 → 눌러야 할 방향키 / 스틱 기울임 방향.
    ///
    /// <para>
    /// 진실은 <see cref="Morae.Game.Interactions.PrayerInteractable"/>의 조준 판정
    /// (<c>aim.y &gt; 0 ? (aim.x &lt; 0 ? TopLeft : TopRight) : (aim.x &lt; 0 ? BottomLeft : BottomRight)</c>)이고,
    /// 이 클래스는 그 규칙을 <b>역방향</b>으로 읽어 UI에 알려준다. 조준 규칙을 고치면 여기와 테스트가 같이 깨져야 한다 —
    /// "화살표는 위를 가리키는데 실제로는 아래로 조준되는" 조용한 어긋남을 막는 장치다.
    /// </para>
    /// 대각 입력만 귀퉁이로 매핑되므로 힌트는 항상 <b>세로 1개 + 가로 1개</b>, 두 키를 함께 강조한다.
    /// </summary>
    public static class PrayerAimHint
    {
        private const float Diag = 0.70710678f;

        /// <summary>이 귀퉁이를 겨누려면 그 키를 눌러야 하는가. 귀퉁이가 없으면(None) 전부 false.</summary>
        public static bool IsKeyLit(int corner, AimKey key)
        {
            if (corner < 0 || corner >= CornerIndex.Count) return false;
            switch (key)
            {
                case AimKey.Up: return corner == CornerIndex.TopLeft || corner == CornerIndex.TopRight;
                case AimKey.Down: return corner == CornerIndex.BottomLeft || corner == CornerIndex.BottomRight;
                case AimKey.Left: return corner == CornerIndex.TopLeft || corner == CornerIndex.BottomLeft;
                case AimKey.Right: return corner == CornerIndex.TopRight || corner == CornerIndex.BottomRight;
                default: return false;
            }
        }

        /// <summary>
        /// 스틱을 기울일 방향 (터치 힌트의 노브 표시용). 대각 정규화 값은 터치 스냅 규약과 같다
        /// (<see cref="Morae.Game.Player.TouchStickModel"/> Corners 모드) — 그림과 실제 입력이 같은 각도를 가리킨다.
        /// </summary>
        public static Vector2 StickDirection(int corner)
        {
            if (corner < 0 || corner >= CornerIndex.Count) return Vector2.zero;
            float x = IsKeyLit(corner, AimKey.Right) ? Diag : -Diag;
            float y = IsKeyLit(corner, AimKey.Up) ? Diag : -Diag;
            return new Vector2(x, y);
        }
    }
}
