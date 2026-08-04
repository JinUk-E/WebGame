using System;

namespace Morae.Game.Core
{
    /// <summary>
    /// 씬 리로드(재시작)를 살아남는 유일한 static 상태 (architecture §3.2).
    /// 시드·프롤로그 스킵만 — 그 외 static 상태 추가 금지 (여기가 봉인 지점).
    /// </summary>
    public static class SessionContext
    {
        public static int Seed { get; private set; }
        public static bool SkipPrologue { get; private set; }

        private static bool _initialized;

        /// <summary>최초 진입 시 1회 — 시드를 뽑고 프롤로그는 정상 진행.</summary>
        public static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            Seed = NewSeed();
            SkipPrologue = false;
        }

        /// <summary>재시작(씬 리로드) 직전 호출 — 새 시드(지터 변주) + 프롤로그 스킵 여부 결정.</summary>
        public static void PrepareRestart(bool skipPrologue)
        {
            _initialized = true;
            Seed = NewSeed();
            SkipPrologue = skipPrologue;
        }

        private static int NewSeed() => Environment.TickCount ^ (int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF);
    }
}
