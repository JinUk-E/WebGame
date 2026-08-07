using System;
using System.Collections.Generic;
using System.IO;
using Morae.Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Morae.Game.Tests
{
    /// <summary>
    /// **소리와 흔들림의 박자가 실제로 같은지**를 배포되는 wav로 검사한다.
    ///
    /// <para>
    /// 흔들림 박자표(<see cref="RattlePattern"/>)는 짝이 되는 클립의 온셋을 옮겨 적은 것이라,
    /// 클립을 다시 뽑으면서 리듬을 바꾸면 표와 소리가 조용히 갈라진다 — 화면으로는
    /// "덜컹 —(늦게) 흔들림"으로만 보이고 아무도 원인을 못 짚는다.
    /// v0.6.1의 교훈(밸런스 회귀 방어는 <b>배포되는 데이터</b>를 읽어야 한다)을 소리에 적용한 것이다.
    /// </para>
    ///
    /// <para>
    /// 그래서 여기서는 합성 픽스처를 쓰지 않고 <c>Assets/_Project/Audio/**.wav</c>를 직접 파싱해
    /// 포락선을 재고 표와 대조한다. 동시에 "흔들림이 끝나면 원위치로 정확히 돌아온다"(누적 드리프트 0)와
    /// "삼중 습격의 세 소리가 실제로 겹친다"(자막이 거짓말을 하지 않는다)도 못 박는다.
    /// </para>
    /// </summary>
    public sealed class RattleSyncTests
    {
        private const string AudioDir = "_Project/Audio";
        private const float HopSec = 0.005f;          // 포락선 해상도 (5ms)
        private const float OnsetHigh = 0.45f;        // 이 위로 오르면 타격 시작
        private const float OnsetLow = 0.22f;         // 이 아래로 떨어져야 다음 타격을 셀 준비가 된다
        private const float OnsetToleranceSec = 0.03f;

        /// <summary>전조(소금) 흔들림 주파수 — CornerTelegraphView.shakeHz. 여기와 겹치면 두 연출이 헷갈린다.</summary>
        private const float TelegraphShakeHz = 26f;

        private static string ClipPath(RattleKind kind) => kind switch
        {
            RattleKind.WindowKnock => "SFX_Window/window_knock.wav",
            RattleKind.WindowRattle => "SFX_Window/window_rattle.wav",
            _ => "SFX_Handle/handle_rattle.wav",
        };

        // ---------- ① 박자 = 클립 온셋 ----------

        [Test]
        public void WindowKnock_HitTimes_MatchClipOnsets() => AssertOnsetsMatch(RattleKind.WindowKnock);

        [Test]
        public void DoorHandle_HitTimes_MatchClipOnsets() => AssertOnsetsMatch(RattleKind.DoorHandle);

        private static void AssertOnsetsMatch(RattleKind kind)
        {
            float[] envelope = Envelope(kind, out float _);
            List<float> onsets = Onsets(envelope);

            Assert.AreEqual(RattlePattern.HitCount(kind), onsets.Count,
                $"{kind}: 클립에서 검출된 타격 수({onsets.Count})와 RattlePattern 표({RattlePattern.HitCount(kind)})가 다르다. "
                + $"검출: [{string.Join(", ", onsets.ConvertAll(o => o.ToString("F3")))}] — "
                + "클립을 다시 뽑았다면 Tools/gen_assault_sfx.py 의 표와 RattlePattern을 함께 고칠 것");

            for (int i = 0; i < onsets.Count; i++)
            {
                Assert.AreEqual(RattlePattern.HitTime(kind, i), onsets[i], OnsetToleranceSec,
                    $"{kind}: {i}번째 타격이 소리({onsets[i]:F3}s)와 그림({RattlePattern.HitTime(kind, i):F3}s)에서 어긋난다");
            }
        }

        /// <summary>
        /// 유리창 드르륵은 타격이 분리되지 않는 연속 떨림이라 온셋이 아니라 <b>감쇠 시상수</b>로 맞춘다.
        /// 모델이 소리보다 빨리 죽으면 "소리는 나는데 창은 멈춰 있는" 구간이 생긴다.
        /// </summary>
        [Test]
        public void WindowRattle_SustainDecay_MatchesClipEnvelope()
        {
            float[] envelope = Envelope(RattleKind.WindowRattle, out float _);
            float measured = MeasureDecayTau(envelope);
            Assert.AreEqual(RattlePattern.SustainTau(RattleKind.WindowRattle), measured, 0.10f,
                $"유리창 떨림 감쇠가 클립(측정 τ={measured:F3}s)과 어긋난다");
        }

        [Test]
        public void EveryPattern_EndsBeforeItsClipDoes()
        {
            foreach (RattleKind kind in Enum.GetValues(typeof(RattleKind)))
            {
                Envelope(kind, out float clipLength);
                Assert.LessOrEqual(RattlePattern.DurationSec(kind), clipLength + 0.001f,
                    $"{kind}: 소리가 끝난 뒤에도 계속 흔들린다 (흔들림 {RattlePattern.DurationSec(kind):F2}s > 클립 {clipLength:F2}s)");
                Assert.GreaterOrEqual(RattlePattern.DurationSec(kind),
                    RattlePattern.HitTime(kind, RattlePattern.HitCount(kind) - 1) + 0.15f,
                    $"{kind}: 마지막 타격 직후 흔들림이 끊긴다 — 때린 결과가 안 보인다");
            }
        }

        // ---------- ② 원위치 복구 (누적 드리프트 0) ----------

        [Test]
        public void Offset_IsExactlyZero_OutsideDuration()
        {
            foreach (RattleKind kind in Enum.GetValues(typeof(RattleKind)))
            {
                float d = RattlePattern.DurationSec(kind);
                Assert.AreEqual(Vector2.zero, RattlePattern.Offset(kind, -0.01f), $"{kind}: 시작 전 변위");
                Assert.AreEqual(Vector2.zero, RattlePattern.Offset(kind, d), $"{kind}: 종료 시점 변위");
                Assert.AreEqual(Vector2.zero, RattlePattern.Offset(kind, d + 5f), $"{kind}: 종료 후 변위");
                Assert.IsFalse(RattlePattern.IsActive(kind, d), $"{kind}: 종료 시점에 아직 활성");
            }
        }

        /// <summary>
        /// 뷰가 하는 일을 그대로 흉내 낸다 — 불규칙한 프레임 간격으로 3회 연속 흔들고,
        /// 매번 <c>rest + Offset</c>을 <b>대입</b>한 뒤 마지막에 원위치와 <b>비트 단위로</b> 같은지 본다.
        /// 어디선가 가산(+=)으로 바뀌면 여기가 바로 빨개진다.
        /// </summary>
        [Test]
        public void RepeatedRattles_LeaveNoPositionDrift()
        {
            var rest = new Vector3(2.5f, 2.13f, 0f);
            var rng = new System.Random(20260807);

            foreach (RattleKind kind in Enum.GetValues(typeof(RattleKind)))
            {
                Vector3 pos = rest;
                for (int run = 0; run < 3; run++)
                {
                    float t = 0f;
                    while (true)
                    {
                        t += 0.008f + (float)rng.NextDouble() * 0.05f;  // 8~58ms — 웹 프레임 흔들림 흉내
                        if (!RattlePattern.IsActive(kind, t))
                        {
                            pos = rest;   // 뷰의 Rest()
                            break;
                        }
                        Vector2 o = RattlePattern.Offset(kind, t);
                        pos = rest + new Vector3(o.x, o.y, 0f);
                    }
                }
                Assert.AreEqual(rest.x, pos.x, $"{kind}: x 드리프트");
                Assert.AreEqual(rest.y, pos.y, $"{kind}: y 드리프트");
                Assert.AreEqual(rest.z, pos.z, $"{kind}: z 드리프트");
            }
        }

        [Test]
        public void Envelope_StaysWithinUnitRange()
        {
            foreach (RattleKind kind in Enum.GetValues(typeof(RattleKind)))
            {
                for (float t = -0.2f; t < RattlePattern.DurationSec(kind) + 0.2f; t += 0.004f)
                {
                    float e = RattlePattern.Envelope(kind, t);
                    Assert.GreaterOrEqual(e, 0f, $"{kind} @{t:F3}");
                    Assert.LessOrEqual(e, 1f, $"{kind} @{t:F3}");
                    Vector2 o = RattlePattern.Offset(kind, t);
                    Assert.LessOrEqual(Mathf.Abs(o.x), RattlePattern.Amplitude(kind) + 1e-4f, $"{kind} x @{t:F3}");
                    Assert.LessOrEqual(Mathf.Abs(o.y), RattlePattern.Amplitude(kind) + 1e-4f, $"{kind} y @{t:F3}");
                }
            }
        }

        // ---------- ③ 전조와 혼동되지 않을 것 ----------

        [Test]
        public void Amplitude_FollowsEventStrength()
        {
            Assert.Less(RattlePattern.Amplitude(RattleKind.WindowKnock),
                RattlePattern.Amplitude(RattleKind.WindowRattle), "통통이 유리창보다 세다");
            Assert.Less(RattlePattern.Amplitude(RattleKind.WindowRattle),
                RattlePattern.Amplitude(RattleKind.DoorHandle), "유리창이 손잡이보다 세다");
        }

        [Test]
        public void ShakeFrequency_KeepsDistanceFromTelegraph()
        {
            foreach (RattleKind kind in Enum.GetValues(typeof(RattleKind)))
            {
                Assert.GreaterOrEqual(Mathf.Abs(RattlePattern.ShakeHzX(kind) - TelegraphShakeHz), 3f,
                    $"{kind}: 가로 진동수가 전조 소금 흔들림({TelegraphShakeHz}Hz)과 너무 가깝다 — 분위기가 신호로 오인된다");
            }
        }

        // ---------- ④ 삼중 습격: 자막이 거짓말을 하지 않을 것 ----------

        [Test]
        public void TripleAssault_AllThreeLayersSoundTogether()
        {
            Assert.Greater(TripleAssaultCue.OverlapSec, 0.8f,
                $"세 소리가 함께 나는 구간이 {TripleAssaultCue.OverlapSec:F2}s뿐이다 — "
                + "자막은 '동시에 울린다'고 말한다");
            Assert.LessOrEqual(TripleAssaultCue.OverlapStartSec, 1.0f, "세 번째 소리가 너무 늦게 합류한다");
        }

        [Test]
        public void TripleAssault_ClipDurations_MatchConstants()
        {
            Assert.AreEqual(TripleAssaultCue.PhoneDurationSec, ClipLength("SFX_Phone/phone_ring.wav"), 0.05f,
                "전화벨 클립 길이가 TripleAssaultCue.PhoneDurationSec와 다르다");
            Assert.AreEqual(TripleAssaultCue.KnockDurationSec, ClipLength("SFX_Knock/knock.wav"), 0.05f,
                "노크 클립 길이가 TripleAssaultCue.KnockDurationSec와 다르다");
            Assert.AreEqual(TripleAssaultCue.HandleDurationSec, ClipLength(ClipPath(RattleKind.DoorHandle)), 0.05f,
                "손잡이 클립 길이가 RattlePattern.DoorHandle 총 길이와 다르다");
        }

        [Test]
        public void TripleAssault_KnockTimes_AreStrictlyIncreasing()
        {
            for (int i = 1; i < TripleAssaultCue.KnockCount; i++)
            {
                Assert.Greater(TripleAssaultCue.KnockTime(i), TripleAssaultCue.KnockTime(i - 1),
                    "노크 시각이 뒤로 가거나 겹친다");
            }
            Assert.AreEqual(TripleAssaultCue.KnockCount, TripleAssaultCue.KnockCountUpTo(999f), "카운터가 전부를 세지 않는다");
            Assert.AreEqual(0, TripleAssaultCue.KnockCountUpTo(-1f), "시작 전에 이미 두드렸다");
        }

        // ---------- wav 파싱·포락선 ----------

        private static float ClipLength(string relative)
        {
            ReadWav(relative, out int rate, out float[] samples);
            return samples.Length / (float)rate;
        }

        /// <summary>0~1로 정규화한 5ms 피크 포락선. clipLength는 초 단위 전체 길이.</summary>
        private static float[] Envelope(RattleKind kind, out float clipLength)
        {
            ReadWav(ClipPath(kind), out int rate, out float[] samples);
            clipLength = samples.Length / (float)rate;

            int hop = Mathf.Max(1, Mathf.RoundToInt(rate * HopSec));
            int count = Mathf.Max(1, (samples.Length - hop) / hop);
            var env = new float[count];
            float max = 1e-6f;
            for (int i = 0; i < count; i++)
            {
                float peak = 0f;
                int start = i * hop;
                for (int j = start; j < start + hop && j < samples.Length; j++)
                {
                    float a = Mathf.Abs(samples[j]);
                    if (a > peak) peak = a;
                }
                env[i] = peak;
                if (peak > max) max = peak;
            }
            for (int i = 0; i < count; i++) env[i] /= max;
            return env;
        }

        private static List<float> Onsets(float[] envelope)
        {
            var result = new List<float>();
            bool armed = true;
            for (int i = 0; i < envelope.Length; i++)
            {
                if (armed && envelope[i] > OnsetHigh)
                {
                    result.Add(i * HopSec);
                    armed = false;
                }
                else if (!armed && envelope[i] < OnsetLow)
                {
                    armed = true;
                }
            }
            return result;
        }

        /// <summary>
        /// 상단 포락선이 e⁻¹·e⁻²를 지나는 시각으로 감쇠 시상수를 추정한다.
        /// 알갱이(granular) 성분 때문에 순간값은 요동치므로 ±50ms 슬라이딩 최대값을 먼저 씌운다.
        /// </summary>
        private static float MeasureDecayTau(float[] envelope)
        {
            int w = Mathf.RoundToInt(0.05f / HopSec);
            var upper = new float[envelope.Length];
            for (int i = 0; i < envelope.Length; i++)
            {
                float peak = 0f;
                for (int j = Mathf.Max(0, i - w); j <= Mathf.Min(envelope.Length - 1, i + w); j++)
                    if (envelope[j] > peak) peak = envelope[j];
                upper[i] = peak;
            }

            float t1 = FirstCrossing(upper, Mathf.Exp(-1f));
            float t2 = FirstCrossing(upper, Mathf.Exp(-2f));
            Assert.Greater(t1, 0f, "클립에서 감쇠 구간을 찾지 못했다");
            Assert.Greater(t2, 0f, "클립에서 감쇠 구간을 찾지 못했다");
            return 0.5f * (t1 + t2 * 0.5f);
        }

        private static float FirstCrossing(float[] upper, float level)
        {
            for (int i = 0; i < upper.Length; i++)
                if (upper[i] < level) return i * HopSec;
            return -1f;
        }

        /// <summary>16bit PCM WAV 파서 — 에디터 API 없이 파일만 읽는다(테스트 어셈블리 참조 제약 회피).</summary>
        private static void ReadWav(string relative, out int rate, out float[] samples)
        {
            string path = Path.Combine(Application.dataPath, AudioDir, relative);
            Assert.IsTrue(File.Exists(path), $"클립 없음: {path} — Tools/gen_assault_sfx.py 를 실행했는가?");
            byte[] bytes = File.ReadAllBytes(path);

            rate = 0;
            int channels = 1;
            int bits = 16;
            int dataOffset = -1;
            int dataLength = 0;

            int p = 12; // "RIFF"(4) + size(4) + "WAVE"(4)
            while (p + 8 <= bytes.Length)
            {
                string id = System.Text.Encoding.ASCII.GetString(bytes, p, 4);
                int size = BitConverter.ToInt32(bytes, p + 4);
                int body = p + 8;
                if (id == "fmt ")
                {
                    channels = BitConverter.ToInt16(bytes, body + 2);
                    rate = BitConverter.ToInt32(bytes, body + 4);
                    bits = BitConverter.ToInt16(bytes, body + 14);
                }
                else if (id == "data")
                {
                    dataOffset = body;
                    dataLength = size;
                    break;
                }
                p = body + size + (size % 2); // 청크는 짝수 정렬
            }

            Assert.AreEqual(16, bits, $"{relative}: 16bit PCM만 지원한다");
            Assert.Greater(dataOffset, 0, $"{relative}: data 청크를 못 찾았다");

            int frames = dataLength / 2 / channels;
            samples = new float[frames];
            for (int i = 0; i < frames; i++)
            {
                short v = BitConverter.ToInt16(bytes, dataOffset + i * 2 * channels); // 첫 채널만
                samples[i] = v / 32768f;
            }
        }
    }
}
