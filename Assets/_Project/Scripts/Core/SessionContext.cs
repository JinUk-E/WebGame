using System;

namespace Morae.Game.Core
{
    /// <summary>
    /// 씬 리로드(재시작)를 살아남는 유일한 static 상태 (architecture §3.2).
    /// 시드·프롤로그 스킵·첫 엔딩 여부만 — 그 외 static 상태 추가 금지 (여기가 봉인 지점).
    /// 2026-08-04 타이틀 개편: 스킵은 자동이 아니라 타이틀 토글(첫 엔딩 후 노출)의 사용자 선택.
    /// </summary>
    public static class SessionContext
    {
        public static int Seed { get; private set; }
        public static bool SkipPrologue { get; private set; }
        /// <summary>첫 엔딩(사망·클리어 무관) 경험 여부 — 타이틀의 프롤로그 스킵 토글 노출 조건.</summary>
        public static bool HasEnded { get; private set; }

        private static bool _initialized;

        /// <summary>최초 진입 시 1회 — 시드를 뽑고 프롤로그는 정상 진행.</summary>
        public static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            Seed = NewSeed();
            SkipPrologue = false;
        }

        /// <summary>첫 게임오버/엔딩 기록 — 스킵 토글이 열리고, 기본값은 켜짐(기존 재시작 동작 유지).</summary>
        public static void MarkEnded()
        {
            if (HasEnded) return;
            HasEnded = true;
            SkipPrologue = true;
        }

        /// <summary>타이틀 토글의 사용자 선택.</summary>
        public static void SetSkipPrologue(bool skip) => SkipPrologue = skip;

        /// <summary>재시작(씬 리로드) 직전 호출 — 새 시드(지터 변주). 스킵 여부는 사용자 토글 보존.</summary>
        public static void PrepareRestart()
        {
            _initialized = true;
            Seed = NewSeed();
        }

        private static int NewSeed() => Environment.TickCount ^ (int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF);
    }
}
