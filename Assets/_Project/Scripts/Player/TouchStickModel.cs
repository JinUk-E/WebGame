using UnityEngine;

namespace Morae.Game.Player
{
    /// <summary>
    /// 가상 스틱의 순수 계산부 (MonoBehaviour 무관).
    /// 스틱 변위를 키보드와 <b>동일한 값</b>의 방향 벡터로 환산한다:
    /// 8방향 스냅 = WASD 8방향과 1:1 (대각은 정규화 0.7071 — 대각 등속 규칙 유지).
    ///
    /// <para>
    /// <b>v0.7: SnapMode 폐기 — 스틱은 항상 이동이다.</b> 기도 조준이 있던 시절에는 같은 스틱이
    /// 상황에 따라 8방향 이동과 대각 4방향 조준으로 <b>의미가 바뀌었고</b>, 모바일 플레이어는 그 전환을
    /// 배워야 했다. 조작 축을 하나로 줄인다는 목표의 실질 이득 중 큰 몫이 바로 이것이다 —
    /// 이제 스틱이 하는 일은 하나뿐이라 배울 것이 없다.
    /// </para>
    /// </summary>
    public static class TouchStickModel
    {
        private const float Diag = 0.70710678f;

        // 0=E 1=NE 2=N 3=NW 4=W 5=SW 6=S 7=SE (45° 간격, 반시계)
        private static readonly Vector2[] Directions =
        {
            new Vector2(1f, 0f),
            new Vector2(Diag, Diag),
            new Vector2(0f, 1f),
            new Vector2(-Diag, Diag),
            new Vector2(-1f, 0f),
            new Vector2(-Diag, -Diag),
            new Vector2(0f, -1f),
            new Vector2(Diag, -Diag),
        };

        /// <summary>
        /// 스틱 중심 기준 변위 → 스냅된 방향 벡터. 데드존 미만이면 Vector2.zero.
        /// </summary>
        /// <param name="delta">스틱 중심에서의 변위 (캔버스 로컬 단위)</param>
        /// <param name="radius">스틱 반경 (같은 단위, 0 이하면 무입력)</param>
        /// <param name="deadZone01">반경 대비 데드존 비율 (0~1)</param>
        public static Vector2 Resolve(Vector2 delta, float radius, float deadZone01)
        {
            if (radius <= 0f) return Vector2.zero;
            float dead = radius * Mathf.Clamp01(deadZone01);
            if (delta.sqrMagnitude <= dead * dead) return Vector2.zero;

            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;

            int index = Mathf.RoundToInt(angle / 45f);
            index = ((index % 8) + 8) % 8;
            return Directions[index];
        }

        /// <summary>스틱 노브 표시 위치 — 변위를 최대 오프셋으로 클램프 (연출 전용, 입력값과 무관).</summary>
        public static Vector2 ClampKnob(Vector2 delta, float maxOffset)
        {
            if (maxOffset <= 0f) return Vector2.zero;
            float sqr = delta.sqrMagnitude;
            if (sqr <= maxOffset * maxOffset) return delta;
            return delta * (maxOffset / Mathf.Sqrt(sqr));
        }
    }
}
