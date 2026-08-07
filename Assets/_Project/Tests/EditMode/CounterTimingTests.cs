using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Morae.Game.Core;
using Morae.Game.Data;
using NUnit.Framework;
using UnityEngine;

namespace Morae.Game.Tests
{
    /// <summary>
    /// **능동 방어가 성립하는가 — 실제 데이터 에셋 값으로 검사하는 회귀 방어.**
    ///
    /// <para>
    /// 2026-08-07 사고: 전조 3.0s(AttackTable)와 기도 채널 3.0s(BalanceConfig)가 같아서,
    /// <b>이동 시간 0을 가정해도 정확히 동시</b>였다. 즉 명세의 핵심 규칙("전조 3초 안에 기도를 완료하면 상쇄")이
    /// 한 번도 성립한 적이 없었다. 두 수치는 각각 보면 멀쩡하고 <b>관계</b>만 깨져 있어서,
    /// 어느 한쪽 파일을 아무리 들여다봐도 보이지 않는다.
    /// </para>
    ///
    /// 그래서 관계를 테스트로 못 박는다 — 나중에 누가 수치를 만져도 다음 셋 중 하나라도 깨지면 여기서 멈춘다:
    /// <list type="number">
    ///   <item>단일 상쇄 <b>가능</b> — 채널 ≤ 전조 − 최소 이동 여유</item>
    ///   <item>2연속 상쇄 <b>불가</b> — 채널 × 2 &gt; 전조 (트리아지 = "전부는 못 막는다")</item>
    ///   <item>심화 상쇄 <b>가능하되 여유 없음</b> — 채널 × 1.5 ≤ 전조, 이동 여유는 남지 않는다</item>
    /// </list>
    ///
    /// <para>
    /// 값은 <b>런타임이 실제로 보는 값</b>으로 읽는다: ScriptableObject의 C# 필드 초기값 위에
    /// 에셋 YAML에 적힌 키만 덮어쓴다 — 유니티의 역직렬화와 같은 규칙이다
    /// (에셋에 없는 필드는 코드 초기값이 쓰인다. 반대로 에셋에 있으면 코드 초기값은 무시된다).
    /// </para>
    /// </summary>
    public sealed class CounterTimingTests
    {
        private const string BalancePath = "_Project/Data/BalanceConfig.asset";
        private const string AttackPath = "_Project/Data/AttackTable.asset";
        private const string PhasePath = "_Project/Data/PhaseTable.asset";

        private static float _channel;
        private static float _telegraph;
        private static float _deepMultiplier;
        private static float _moveSpeed;
        private static float _trapTelegraph;

        [SetUp]
        public void LoadEffectiveValues()
        {
            // 코드 초기값 = 에셋에 키가 없을 때 런타임이 쓰는 값
            var defaults = ScriptableObject.CreateInstance<BalanceConfig>();
            Dictionary<string, float> balance = Floats(BalancePath);

            _channel = Pick(balance, "prayerChannelSec", defaults.PrayerChannelSec);
            _deepMultiplier = Pick(balance, "prayerDeepenedMultiplier", defaults.PrayerDeepenedMultiplier);
            _moveSpeed = Pick(balance, "moveSpeed", defaults.MoveSpeed);
            _trapTelegraph = Pick(balance, "trapTelegraphSec", defaults.TrapTelegraphSec);
            Object.DestroyImmediate(defaults);

            List<float> telegraphs = ListOf(AttackPath, "telegraphDuration");
            Assert.IsNotEmpty(telegraphs, $"{AttackPath}에 공격 행이 없다 — 데이터 에셋을 다시 만들 것");
            _telegraph = telegraphs[0];
        }

        // ---------- ① 단일 상쇄 가능 ----------

        [Test]
        public void SingleCounter_FitsInsideTelegraph_WithTravelSlack()
        {
            float need = CounterTimingModel.RequiredSlackSec(_moveSpeed);
            float slack = CounterTimingModel.SlackSec(_channel, _telegraph);

            Assert.IsTrue(CounterTimingModel.CanCounter(_channel, _telegraph, need),
                $"전조를 보고 출발해도 늦는다. 채널 {_channel}s / 전조 {_telegraph}s → 여유 {slack:F2}s, " +
                $"필요 {need:F2}s ({CounterTimingModel.ReferenceTravelUnits}u ÷ {_moveSpeed}u/s). " +
                "전조를 늘리거나 채널을 줄일 것 — 단 ②를 깨지 않는 범위에서.");
        }

        // ---------- ② 2연속 상쇄 불가 (트리아지 보존) ----------

        [Test]
        public void TwoCounters_InOneTelegraphWindow_AreImpossible()
        {
            int max = CounterTimingModel.MaxCountersPerWindow(_channel, _telegraph);
            Assert.AreEqual(1, max,
                $"한 전조 창에 상쇄 {max}회가 들어간다 (채널 {_channel}s × 2 = {_channel * 2}s vs 전조 {_telegraph}s). " +
                "\"전부는 못 막는다 — 무엇을 버릴지 고른다\"가 게임의 축이다. 이동 시간 0을 가정한 상한도 1이어야 한다.");
        }

        // ---------- ③ 심화 상쇄 — 가능하되 벌은 남는다 ----------

        [Test]
        public void DeepenedCounter_IsPossible_ButLeavesNoTravelSlack()
        {
            float deep = _channel * _deepMultiplier;
            float need = CounterTimingModel.RequiredSlackSec(_moveSpeed);

            Assert.LessOrEqual(deep, _telegraph,
                $"심화 귀퉁이는 아예 막을 수 없다 (심화 채널 {deep:F2}s > 전조 {_telegraph}s). " +
                "심화는 '어렵다'여야지 '불가능'이면 안 된다 — 불상 앞에 미리 서 있는 대응이 존재해야 한다.");

            Assert.Less(_telegraph - deep, need,
                $"심화 상쇄에 이동 여유({_telegraph - deep:F2}s ≥ 필요 {need:F2}s)가 남는다 — 심화의 벌이 사라졌다. " +
                "심화는 '불상 앞에 이미 서 있어야만 가능'이 설계다.");
        }

        // ---------- 함정 웨이브도 같은 전조 감각을 쓴다 ----------

        [Test]
        public void TrapWaveTelegraph_MatchesScheduledTelegraph()
        {
            Assert.AreEqual(_telegraph, _trapTelegraph, 1e-3f,
                $"함정 웨이브 전조({_trapTelegraph}s)가 스케줄 전조({_telegraph}s)와 다르다. " +
                "플레이어가 배운 '전조 길이' 감각이 P6에서만 어긋나 상쇄되던 손이 갑자기 안 먹는다.");
        }

        [Test]
        public void TrapWave_AllowsExactlyOneRescuePerWave()
        {
            Assert.AreEqual(1, CounterTimingModel.MaxCountersPerWindow(_channel, _trapTelegraph),
                "함정 4동시 웨이브는 웨이브당 한 곳만 구제 가능해야 한다 (의도된 트리아지).");
        }

        // ---------- 전조가 길어져도 페이즈를 넘지 않는다 ----------

        [Test]
        public void AttackTable_AllRowsShareOneTelegraphDuration()
        {
            List<float> telegraphs = ListOf(AttackPath, "telegraphDuration");
            foreach (float t in telegraphs)
            {
                Assert.AreEqual(_telegraph, t, 1e-3f,
                    "행마다 전조 길이가 다르면 반응 창이 학습 불가능해진다 — 전 행 동일 유지.");
            }
        }

        [Test]
        public void TrapSequence_StillFitsInsideItsPhase()
        {
            var defaults = ScriptableObject.CreateInstance<BalanceConfig>();
            Dictionary<string, float> balance = Floats(BalancePath);
            float voice = Pick(balance, "trapVoiceLeadSec", defaults.TrapVoiceLeadSec);
            float quiet = Pick(balance, "trapQuietSec", defaults.TrapQuietSec);
            float gap = Pick(balance, "trapWaveGapSec", defaults.TrapWaveGapSec);
            int waves = Mathf.RoundToInt(Pick(balance, "trapWaveCount", defaults.TrapWaveCount));
            Object.DestroyImmediate(defaults);

            float total = TrapTimeline.TotalDuration(waves, voice, quiet, _trapTelegraph, gap);
            float p6 = PhaseDurationOf(PhaseId.P6);

            Assert.LessOrEqual(total, p6,
                $"함정 시퀀스 {total:F1}s가 P6 길이 {p6:F1}s를 넘는다 — 전조를 늘렸으면 여기도 확인할 것 " +
                "(마지막 웨이브 판정이 페이즈 밖으로 밀리면 정적 구간이 먹힌다).");
        }

        // ---------- 학습 무대 (v0.6.1) ----------

        [Test]
        public void TrainingDim_RestoresExactly_WhenTrainingEnds()
        {
            Assert.AreEqual(1f, TrainingStageModel.RoomDimScale(false, 0.55f),
                "학습이 끝나면 감광 배율은 **정확히** 1이어야 한다 — 잔여 감광은 본편 밸런스를 통째로 어긋나게 한다.");
            Assert.AreEqual(1f, TrainingStageModel.CandleScale(false, 0.7f),
                "촛불 배율도 원복돼야 한다 (감광 예외② — 촛불은 상수 밝기가 기본값이다).");
        }

        [Test]
        public void TrainingDim_DarkensRoom_ButNeverBelowFloor()
        {
            const float baseLight = 0.12f, dawnBoost = 0.18f, penalty = 0.018f, minLight = 0.055f;
            float normal = CornerPenaltyModel.RoomLightIntensity(baseLight, 0f, dawnBoost, 0f, 0, penalty, minLight);
            float dim = CornerPenaltyModel.RoomLightIntensity(baseLight, 0f, dawnBoost, 0f, 0, penalty, minLight,
                TrainingStageModel.RoomDimScale(true, 0.55f));

            Assert.Less(dim, normal, "학습 스포트라이트가 실내를 더 어둡게 만들지 않으면 대비가 생기지 않는다.");
            Assert.GreaterOrEqual(dim, minLight, "감광 배율이 minRoomLight 바닥을 뚫었다 — 배율은 클램프 **전에** 곱해야 한다.");

            // 흑화가 이미 바닥까지 내린 상태에서도 바닥은 지켜진다 (P6 암전 방지 규칙)
            float dimAtFloor = CornerPenaltyModel.RoomLightIntensity(baseLight, 0f, dawnBoost, 0f, 4, penalty, minLight,
                TrainingStageModel.RoomDimScale(true, TrainingStageModel.MinDimScale));
            Assert.GreaterOrEqual(dimAtFloor, minLight);
        }

        [Test]
        public void TrainingDim_MatchesUndimmed_WhenScaleIsOne()
        {
            float plain = CornerPenaltyModel.RoomLightIntensity(0.12f, 0.4f, 0.18f, 0.1f, 2, 0.018f, 0.055f);
            float scaled = CornerPenaltyModel.RoomLightIntensity(0.12f, 0.4f, 0.18f, 0.1f, 2, 0.018f, 0.055f, 1f);
            Assert.AreEqual(plain, scaled, 1e-6f, "배율 1은 기존 계산과 완전히 같아야 한다 (오버로드 도입 회귀).");
        }

        [Test]
        public void StandMarker_LiesInsideThePrayerRange()
        {
            // 서클 위에 섰는데 기도가 안 되면 피드백이 거짓말이 된다 — 원반의 **모든 점**이 기도 범위 안이어야 한다.
            const float arriveRadius = 0.55f;   // DestinationMarkerView.arriveRadius 기본값
            const float squash = 0.78f;         // 같은 컴포넌트의 verticalSquash
            Vector2 c = TrainingStageModel.AltarStandPoint;

            foreach (Vector2 edge in new[]
                     {
                         c + new Vector2(arriveRadius, 0f), c - new Vector2(arriveRadius, 0f),
                         c + new Vector2(0f, arriveRadius * squash), c - new Vector2(0f, arriveRadius * squash),
                     })
            {
                Assert.IsTrue(TrainingStageModel.IsWithinPrayerRange(edge),
                    $"목적지 서클 {c}(반경 {arriveRadius}, 눌림 {squash})의 가장자리 {edge}가 기도 범위 밖이다 — " +
                    "\"서클 위인데 기도가 안 된다\"가 된다.");
            }
        }

        [Test]
        public void StandMarker_IsActuallyReachable_NotInsideTheWall()
        {
            float top = TrainingStageModel.AltarStandPoint.y + TrainingStageModel.PlayerColliderRadius;
            Assert.LessOrEqual(top, TrainingStageModel.LeftRegionTopY,
                $"목적지 서클이 벽 안쪽이라 플레이어가 중심에 설 수 없다 (몸 상단 {top:F2} > 바닥 상단 " +
                $"{TrainingStageModel.LeftRegionTopY}). 서클은 갈 수 있는 자리여야 한다.");
        }

        [Test]
        public void IsOnMarker_UsesTheSquashedEllipse_NotACircle()
        {
            Vector2 center = TrainingStageModel.AltarStandPoint;
            const float r = 0.55f, squash = 0.78f;

            Assert.IsTrue(TrainingStageModel.IsOnMarker(center, center, r, squash));
            Assert.IsTrue(TrainingStageModel.IsOnMarker(center + new Vector2(0.5f, 0f), center, r, squash),
                "가로로는 반경까지 인정돼야 한다.");
            Assert.IsFalse(TrainingStageModel.IsOnMarker(center + new Vector2(0f, 0.5f), center, r, squash),
                "세로는 눌린 만큼(×0.78) 좁아야 한다 — 원으로 재면 서클 밖인데 켜지는 띠가 생긴다.");
        }

        // ---------- 에셋 파싱 (씬 로드·AssetDatabase 없이 — SceneTextIntegrityTests 선례) ----------

        private static float PhaseDurationOf(PhaseId id)
        {
            string text = File.ReadAllText(Path.Combine(Application.dataPath, PhasePath));
            // PhaseTable 행: "- phaseId: N" … "duration: X" 순서. 해당 phaseId 블록의 첫 duration을 읽는다.
            var rows = Regex.Matches(text, @"-\s+phaseId:\s*(\d+)(.*?)(?=\n\s*-\s+phaseId:|\z)", RegexOptions.Singleline);
            foreach (Match row in rows)
            {
                if (int.Parse(row.Groups[1].Value, CultureInfo.InvariantCulture) != (int)id) continue;
                Match d = Regex.Match(row.Groups[2].Value, @"duration:\s*([-\d.eE+]+)");
                Assert.IsTrue(d.Success, $"PhaseTable {id} 행에 duration이 없다");
                return float.Parse(d.Groups[1].Value, CultureInfo.InvariantCulture);
            }
            Assert.Fail($"PhaseTable에 {id} 행이 없다");
            return 0f;
        }

        /// <summary>에셋 최상위의 "key: 숫자" 전부. 같은 키가 여러 번이면 첫 값 (BalanceConfig은 평면 구조).</summary>
        private static Dictionary<string, float> Floats(string relative)
        {
            var result = new Dictionary<string, float>();
            foreach (string line in File.ReadAllLines(Path.Combine(Application.dataPath, relative)))
            {
                Match m = Regex.Match(line, @"^\s{2}([A-Za-z_][A-Za-z0-9_]*):\s*([-\d.][-\d.eE+]*)\s*$");
                if (!m.Success) continue;
                if (!result.ContainsKey(m.Groups[1].Value))
                {
                    result[m.Groups[1].Value] = float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                }
            }
            return result;
        }

        /// <summary>리스트 원소의 같은 키를 전부 (AttackTable의 행별 telegraphDuration).</summary>
        private static List<float> ListOf(string relative, string key)
        {
            var result = new List<float>();
            foreach (string line in File.ReadAllLines(Path.Combine(Application.dataPath, relative)))
            {
                Match m = Regex.Match(line, $@"^\s*{key}:\s*([-\d.][-\d.eE+]*)\s*$");
                if (m.Success) result.Add(float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
            }
            return result;
        }

        /// <summary>에셋에 키가 있으면 그 값, 없으면 코드 초기값 — 유니티 역직렬화와 같은 규칙.</summary>
        private static float Pick(Dictionary<string, float> asset, string key, float codeDefault)
            => asset.TryGetValue(key, out float v) ? v : codeDefault;
    }
}
