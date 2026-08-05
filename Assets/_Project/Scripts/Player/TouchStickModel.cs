using UnityEngine;

namespace Morae.Game.Player
{
    /// <summary>
    /// 가상 스틱의 순수 계산부 (MonoBehaviour 무관 — EditMode 테스트 대상).
    /// 스틱 변위를 키보드와 <b>동일한 값</b>의 방향 벡터로 환산한다:
    /// 8방향 스냅 = WASD 8방향과 1:1 (대각은 정규화 0.7071 — 대각 등속 규칙 유지).
    /// Corners 모드는 기도 조준 전용 — 어느 방향으로 기울여도 가장 가까운 귀퉁이(대각)로 스냅한다
    /// (PrayerInteractable의 "대각 입력만 귀퉁이 매핑" 규칙을 엄지 조작에서 성립시키기 위함).
    /// </summary>
    public static class TouchStickModel
    {
        public enum SnapMode
        {
            EightWay, // 이동·문 밀기
            Corners,  // 기도 조준 (Praying 상태)
        }

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
        public static Vector2 Resolve(Vector2 delta, float radius, float deadZone01, SnapMode mode)
        {
            if (radius <= 0f) return Vector2.zero;
            float dead = radius * Mathf.Clamp01(deadZone01);
            if (delta.sqrMagnitude <= dead * dead) return Vector2.zero;

            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;

            if (mode == SnapMode.Corners)
            {
                // 대각 4방향(45·135·225·315)으로만 — 사분면 판정과 동치
                int corner = Mathf.RoundToInt((angle - 45f) / 90f);
                corner = ((corner % 4) + 4) % 4;
                return Directions[1 + corner * 2];
            }

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
