using Morae.Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Morae.Game.Tests.EditMode
{
    /// <summary>
    /// TrapTimeline — 최후의 함정(P6) 시간표 순수 로직 (명세 v0.3).
    /// 기본값(BalanceConfig 초기값과 동일): voiceLead 9 / quiet 5 / telegraph 3 / gap 5 / waves 2.
    /// </summary>
    public sealed class TrapTimelineTests
    {
        private const float VoiceLead = 9f;
        private const float Quiet = 5f;
        private const float Telegraph = 3f;
        private const float Gap = 5f;
        private const int Waves = 2;
        private const float P6Duration = 40f;

        [Test]
        public void WaveStart_FirstWave_AfterVoiceAndQuiet()
        {
            // 가짜 목소리 ②(0~9s) → 5초 완전 무공격 정적 → 첫 4귀퉁이 동시 공격
            Assert.AreEqual(VoiceLead + Quiet,
                TrapTimeline.WaveStartTime(0, VoiceLead, Quiet, Telegraph, Gap), 1e-4f);
        }

        [Test]
        public void WaveStart_SecondWave_GapAfterFirstResolve()
        {
            // 웨이브 0 판정(14+3=17s) → 5초 → 웨이브 1 전조 시작(22s) — "5초 → 재차 4귀퉁이 동시 공격"
            float wave0Resolve = TrapTimeline.WaveStartTime(0, VoiceLead, Quiet, Telegraph, Gap) + Telegraph;
            Assert.AreEqual(wave0Resolve + Gap,
                TrapTimeline.WaveStartTime(1, VoiceLead, Quiet, Telegraph, Gap), 1e-4f);
        }

        [Test]
        public void QuietWindow_HasNoWave()
        {
            // 정적 구간(발화 종료~첫 웨이브)에 어떤 웨이브도 시작하지 않는다 — "소금 전조 절대 금지"
            float firstWave = TrapTimeline.WaveStartTime(0, VoiceLead, Quiet, Telegraph, Gap);
            Assert.Greater(firstWave, VoiceLead, "첫 웨이브는 발화 종료 이후여야 한다");
            Assert.AreEqual(Quiet, firstWave - VoiceLead, 1e-4f, "정적 구간 길이는 trapQuietSec");
        }

        [Test]
        public void FullSequence_FitsInsidePhaseDuration()
        {
            // 마지막 웨이브 판정(22+3=25s)이 P6 duration(40s) 안에서 끝난다 — 정적(P7) 침범 금지
            float total = TrapTimeline.TotalDuration(Waves, VoiceLead, Quiet, Telegraph, Gap);
            Assert.AreEqual(25f, total, 1e-4f);
            Assert.LessOrEqual(total, P6Duration);
        }

        [Test]
        public void WaveTimes_AreStrictlyIncreasing()
        {
            float prev = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                float t = TrapTimeline.WaveStartTime(i, VoiceLead, Quiet, Telegraph, Gap);
                Assert.Greater(t, prev);
                prev = t;
            }
        }

        [Test]
        public void ConfigDefaults_MatchSpecValues()
        {
            // BalanceConfig 필드 초기값 = 명세 v0.3 값 (에셋 재생성 없이도 새 판에서 명세 준수)
            var config = ScriptableObject.CreateInstance<Morae.Game.Data.BalanceConfig>();
            try
            {
                Assert.AreEqual(9f, config.TrapVoiceLeadSec, 1e-4f);
                Assert.AreEqual(5f, config.TrapQuietSec, 1e-4f);
                Assert.AreEqual(3f, config.TrapTelegraphSec, 1e-4f);
                Assert.AreEqual(5f, config.TrapWaveGapSec, 1e-4f);
                Assert.AreEqual(2, config.TrapWaveCount);
                Assert.AreEqual(1.5f, config.PrayerDeepenedMultiplier, 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
    }
}
