using UnityEngine;

namespace Morae.Game.Core
{
    /// <summary>
    /// 어둠 속 실루엣의 출현 규칙 (명세 v0.5 §2) — 순수 계산부. EditMode 테스트 대상.
    /// 실루엣은 **분위기 전용**(피해·상호작용 없음)이며, 흑화 개수에 비례해 잦아지고 늘어나므로
    /// 그 자체가 "내가 얼마나 무너졌나"를 알려주는 다이어제틱 게이지가 된다. n=0이면 절대 나오지 않는다.
    /// </summary>
    public static class SilhouetteSpawnModel
    {
        /// <summary>흑 n개일 때 동시 출현 상한 (n=0 → 0). perCorner=1, cap=3이면 1/2/3/3.</summary>
        public static int MaxConcurrent(int blackCorners, int perCorner, int cap)
        {
            if (blackCorners <= 0) return 0;
            return Mathf.Clamp(blackCorners * Mathf.Max(1, perCorner), 1, Mathf.Max(1, cap));
        }

        /// <summary>
        /// 다음 출현까지의 간격(초). n이 커질수록 짧아진다 — base/(1 + gain·(n−1)), minInterval로 클램프.
        /// n=0은 출현 자체가 없으므로 무한대를 뜻하는 -1을 돌려준다 (호출부가 스폰 루프를 멈춘다).
        /// </summary>
        public static float SpawnInterval(int blackCorners, float baseInterval, float gainPerCorner, float minInterval)
        {
            if (blackCorners <= 0) return -1f;
            float divisor = 1f + Mathf.Max(0f, gainPerCorner) * (blackCorners - 1);
            return Mathf.Max(minInterval, baseInterval / divisor);
        }

        /// <summary>
        /// 가독성 보호 (v0.5 §2): 플레이어·불상·전조 중인 귀퉁이 위로 겹치면 안 된다.
        /// 셋 중 어느 것과도 clearance보다 멀면 true.
        /// </summary>
        public static bool IsReadablePosition(Vector2 candidate, Vector2 player, Vector2 altar,
            Vector2[] telegraphingCorners, int telegraphCount, float clearance)
        {
            float sqrClear = clearance * clearance;
            if ((candidate - player).sqrMagnitude < sqrClear) return false;
            if ((candidate - altar).sqrMagnitude < sqrClear) return false;
            if (telegraphingCorners != null)
            {
                int count = Mathf.Min(telegraphCount, telegraphingCorners.Length);
                for (int i = 0; i < count; i++)
                {
                    if ((candidate - telegraphingCorners[i]).sqrMagnitude < sqrClear) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 알파 곡선 0~1 — 등장·퇴장이 전부 페이드다 (팝인이 있으면 "반응해야 하나?"로 읽힌다).
        /// t는 수명 진행도 0~1, fadePortion은 앞뒤 각각의 페이드 비율(0~0.5).
        /// </summary>
        public static float FadeAlpha01(float t01, float fadePortion)
        {
            float t = Mathf.Clamp01(t01);
            float f = Mathf.Clamp(fadePortion, 0.01f, 0.5f);
            if (t < f) return t / f;
            if (t > 1f - f) return (1f - t) / f;
            return 1f;
        }
    }
}
