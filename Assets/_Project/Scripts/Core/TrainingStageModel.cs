using UnityEngine;

namespace Morae.Game.Core
{
    /// <summary>
    /// 프롤로그 학습 구간의 "무대 연출" 계산 — 순수 함수만 (v0.6.1). EditMode 테스트 대상.
    ///
    /// <para>
    /// <b>왜 있는가</b>: 어두운 방에서 섬광만으로는 "저기가 목적지"가 안 읽힌다. 그래서 학습 구간 동안
    /// ① 목표 소금 앞 바닥에 <b>목적지 서클</b>을 놓고 ② 실내 전역광을 한 단계 더 내려 그 주변만 밝힌다.
    /// </para>
    ///
    /// <para>
    /// <b>왜 모델로 뽑았는가</b>: 이 연출은 <b>반드시 원복돼야 하는</b> 종류다. 본편에 잔여 감광이 남으면
    /// 밸런스(감광 예외 3종·minRoomLight)가 통째로 어긋나는데, 그 어긋남은 화면으로는 "좀 어둡네" 정도로만 보인다.
    /// 그래서 "학습이 꺼지면 배율은 정확히 1"을 함수의 성질로 만들고 테스트로 못 박는다.
    /// </para>
    /// </summary>
    public static class TrainingStageModel
    {
        /// <summary>연출이 내릴 수 있는 하한 — 이보다 더 내리면 촛불 밖이 완전히 죽는다.</summary>
        public const float MinDimScale = 0.2f;

        // ---------- 소금 귀퉁이 자리 (씬 좌표) ----------
        // 에디터 배선과 회귀 검증이 **같은 상수**를 본다. 따로 적으면 한쪽만 옮겨져
        // "서클은 켜졌는데 상호작용이 안 되는" 거짓 신호가 조용히 생긴다.

        /// <summary>소금 4귀퉁이 위치 — Room.prefab의 SaltCorner_0~3과 같은 값 (CornerIndex 순서).</summary>
        public static readonly Vector2[] SaltCorners =
        {
            new Vector2(-4.5f, 1.5f),   // 0 좌상
            new Vector2(4.5f, 0.2f),    // 1 우상
            new Vector2(-4.5f, -3.8f),  // 2 좌하
            new Vector2(4.5f, -3.8f),   // 3 우하
        };

        /// <summary>소금 트리거 크기 — 불상과 같은 2.2 × 2.2u.</summary>
        public static readonly Vector2 SaltTriggerSize = new Vector2(2.2f, 2.2f);

        /// <summary>플레이어 본체 콜라이더 반경 — 씬의 Player CircleCollider2D.</summary>
        public const float PlayerColliderRadius = 0.35f;

        /// <summary>좌측 구역 바닥 상단(Wall_Top 하단, y=1.865). 이보다 위로는 걸어 들어갈 수 없다.</summary>
        public const float LeftRegionTopY = 1.865f;

        /// <summary>
        /// 목적지 서클을 놓는 자리. 귀퉁이 자체가 아니라 <b>거기 설 수 있는 자리</b>다.
        /// <para>
        /// ⚠ 좌상(C0)은 y=1.5인데 플레이어 중심 상한이 <c>1.865 − 0.35 = 1.515</c>라 여유가 0.015u뿐이다.
        /// 서클을 귀퉁이 정중앙에 놓으면 플레이어가 벽에 끼여 정확히 못 선다 — 그래서 아래로 내려 잡는다.
        /// 트리거가 2.2u라 내려도 상호작용 범위 안이다.
        /// </para>
        /// </summary>
        public static Vector2 StandPointFor(int corner)
        {
            if (corner < 0 || corner >= SaltCorners.Length) return Vector2.zero;
            Vector2 p = SaltCorners[corner];
            float maxY = LeftRegionTopY - PlayerColliderRadius;
            if (p.y > maxY) p.y = maxY - 0.35f; // 벽에 붙지 않게 한 몸 더 내린다
            return p;
        }

        /// <summary>
        /// 이 위치에 <b>서면 소금을 뿌릴 수 있는가</b>. 상호작용은 트리거와 플레이어 <i>콜라이더</i>의 겹침으로
        /// 잡히므로 실제 범위는 트리거 박스를 플레이어 반경만큼 넓힌 영역이다 — 박스 자체로 재면 실제보다 좁게 나온다.
        /// </summary>
        public static bool IsWithinSaltRange(Vector2 playerCenter, int corner)
        {
            if (corner < 0 || corner >= SaltCorners.Length) return false;
            Vector2 half = SaltTriggerSize * 0.5f + Vector2.one * PlayerColliderRadius;
            Vector2 d = playerCenter - SaltCorners[corner];
            return Mathf.Abs(d.x) <= half.x && Mathf.Abs(d.y) <= half.y;
        }

        /// <summary>
        /// 실내 전역광에 곱할 배율. 학습이 아니면 <b>정확히 1</b>(부동소수 오차 없음 — 리터럴 반환).
        /// </summary>
        public static float RoomDimScale(bool trainingActive, float trainingDimScale)
            => trainingActive ? Mathf.Clamp(trainingDimScale, MinDimScale, 1f) : 1f;

        /// <summary>
        /// 불상 촛불에 곱할 배율 — 감광 예외②("촛불은 아래로 내려가지 않는다")를 지키려고 <b>위로만</b> 간다.
        /// 학습이 아니면 정확히 1.
        /// </summary>
        public static float CandleScale(bool trainingActive, float boost)
            => trainingActive ? 1f + Mathf.Max(0f, boost) : 1f;

        /// <summary>
        /// 플레이어가 목적지 서클 위에 서 있는가 (제곱 비교 — 매 프레임 호출, 할당 없음).
        /// <paramref name="verticalSquash"/>는 바닥 원반이 탑뷰 원근으로 눌린 비율 —
        /// 판정을 <b>보이는 타원과 같은 모양</b>으로 만든다. 원으로 재면 위아래로 "서클 밖인데 켜지는" 띠가 생긴다.
        /// </summary>
        public static bool IsOnMarker(Vector2 player, Vector2 marker, float radius, float verticalSquash = 1f)
        {
            Vector2 d = player - marker;
            if (verticalSquash > 0f) d.y /= verticalSquash;
            return d.sqrMagnitude <= radius * radius;
        }
    }
}
