namespace Morae.Game.Core
{
    /// <summary>
    /// 최후의 함정(P6) 시퀀스 시간표 — 순수 C# (명세 v0.3, EditMode 테스트 대상).
    /// P6 진입 기준 실시간(PhaseElapsed) 타임라인. TV 가속(로컬 공격 시계)과 무관 — 연출 시퀀스는 실시간 고정.
    ///
    ///   0 ─ 가짜 목소리 ② 발화 (EventTable "fake-voice-2", offset 0)
    ///   voiceLead ─ 발화 종료 → 완전 무공격 정적 시작 (소금 전조 금지 — 문·대사에만 집중하는 고민 구간)
    ///   voiceLead + quiet ─ 웨이브 0 전조 시작 (4귀퉁이 동시)
    ///   + telegraph ─ 웨이브 0 판정
    ///   + gap ─ 웨이브 1 전조 시작 … (waveCount회 반복)
    /// </summary>
    public static class TrapTimeline
    {
        /// <summary>waveIndex(0부터)번째 웨이브의 전조 시작 시각 (P6 PhaseElapsed 기준, 초).</summary>
        public static float WaveStartTime(int waveIndex, float voiceLeadSec, float quietSec,
            float telegraphSec, float waveGapSec)
            => voiceLeadSec + quietSec + waveIndex * (telegraphSec + waveGapSec);

        /// <summary>마지막 웨이브 판정까지 걸리는 총 시간 — P6 duration 안에 들어가는지 검증용.</summary>
        public static float TotalDuration(int waveCount, float voiceLeadSec, float quietSec,
            float telegraphSec, float waveGapSec)
            => waveCount <= 0
                ? voiceLeadSec + quietSec
                : WaveStartTime(waveCount - 1, voiceLeadSec, quietSec, telegraphSec, waveGapSec) + telegraphSec;
    }
}
