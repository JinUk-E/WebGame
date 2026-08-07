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
    /// **여명이 진실 채널로 남아 있는가 — 명세 v0.7 회귀 방어.**
    ///
    /// <para>
    /// 고친 사고: "창밖이 밝아지는 것이 체감되지 않는다". 원인 셋 중 가장 위험한 것은 <b>설계 역전</b>이었다 —
    /// 여명 판별이 "방 대비 창이 밝은가"라는 상대적 대비에 기대고 있었고, 방 조도는 v0.5 흑화 감광이 좌우하니
    /// <b>방어를 잘한 플레이어일수록 진실 채널이 안 보였다.</b>
    /// </para>
    ///
    /// 그래서 세 종류를 못 박는다:
    /// <list type="number">
    ///   <item><b>무오염</b> — 소금 상태·페이즈 bias·학습 배율이 여명 표현에 닿을 수 없다 (수치 + 소스 + 씬 3중).</item>
    ///   <item><b>감지 가능</b> — 계단이 실제로 계단이고, 색상이 파랑→주황으로 넘어가며, 무늬가 길어진다.</item>
    ///   <item><b>P6 모호함 보존</b> — 함정 구간 안에 경계가 정확히 하나 있고, 그 구간이 최종 단계에 닿지 않는다.</item>
    /// </list>
    /// </summary>
    public sealed class DawnLegibilityTests
    {
        private const string PhasePath = "_Project/Data/PhaseTable.asset";
        private const string ScenePath = "_Project/Scenes/Main.unity";
        private const string RoomPrefabPath = "_Project/Prefab/Room.prefab";
        private const string ModelSourcePath = "_Project/Scripts/Core/DawnStageModel.cs";
        private const string ViewSourcePath = "_Project/Scripts/Presentation/DawnWindowView.cs";

        /// <summary>URP Sprite-Lit-Default.mat — 이 머티리얼을 쓰면 렌더러가 실내 조도에 곱해진다.</summary>
        private const string SpriteLitGuid = "a97c105638bdf8b4a8650670310a4cd3";

        /// <summary>
        /// 창호지 = 창틀(<c>Visual</c>) 뒤의 흰 쿼드. 창 아트는 창 칸이 알파 0이라
        /// 실제로 색이 보이는 것은 이 오브젝트다.
        /// </summary>
        private const string SkyObjectName = "Sky";

        // ================================================================= ① 무오염

        [Test]
        public void DawnPresentation_IsUntouchedByEveryRoomLightContaminant()
        {
            // 오염원 셋(흑화 개수 · 페이즈 bias · 학습 감광 배율)을 전부 흔들어 본다.
            // 실내 조도는 **반드시 흔들려야** 하고(그래야 이 검증이 의미가 있다),
            // 여명 표현은 **한 비트도** 달라지면 안 된다.
            const float dawn = 0.42f;                        // P6 한복판 — 가장 판별이 중요한 지점
            int stage = DawnStageModel.Stage(dawn);
            Color paper = DawnStageModel.PaperColor(stage);
            float length = DawnStageModel.PatchLength(stage);
            float grid = DawnStageModel.GridAlpha(stage);

            var roomLights = new HashSet<float>();
            foreach (int black in new[] { 0, 1, 2, 3, 4 })
            foreach (float bias in new[] { -0.13f, 0f, 0.10f })
            foreach (float dim in new[] { 1f, 0.55f, TrainingStageModel.MinDimScale })
            {
                roomLights.Add(CornerPenaltyModel.RoomLightIntensity(
                    0.12f, dawn, 0.06f, bias, black, 0.018f, 0.055f, dim));

                // 같은 dawn이면 여명 표현은 언제나 같다 — 인자에 오염원을 넣을 자리 자체가 없다
                Assert.AreEqual(stage, DawnStageModel.Stage(dawn));
                Assert.AreEqual(paper, DawnStageModel.PaperColor(DawnStageModel.Stage(dawn)));
                Assert.AreEqual(length, DawnStageModel.PatchLength(DawnStageModel.Stage(dawn)), 0f);
                Assert.AreEqual(grid, DawnStageModel.GridAlpha(DawnStageModel.Stage(dawn)), 0f);
            }

            Assert.Greater(roomLights.Count, 3,
                "실내 조도가 오염원에 따라 안 변했다 — 이 테스트가 아무것도 검증하지 못하는 상태다.");
        }

        [Test]
        public void DawnSources_MentionNoRoomLightContaminant()
        {
            // 수치 검증만으로는 "나중에 누가 인자를 하나 더 받게 고치는 것"을 못 막는다.
            // 그래서 여명을 소유한 두 파일의 **소스**에 오염원 식별자가 등장하면 실패시킨다.
            string[] banned =
            {
                "SaltCorners", "CornerStageChanged", "BlackCorner", "CornerPenaltyModel",
                "RoomLightBias", "roomLightBias", "editorLightBoost", "editorFreeLight",
                "TrainingModeChanged", "RoomDimScale", "globalLight", "GlobalLight",
            };

            foreach (string rel in new[] { ModelSourcePath, ViewSourcePath })
            {
                string source = StripComments(ReadAsset(rel));
                foreach (string token in banned)
                {
                    Assert.IsFalse(source.Contains(token),
                        $"{rel} 에 오염원 '{token}' 이 들어왔다. 여명은 진실 채널이라 " +
                        "소금 상태·페이즈 bias·학습 배율·에디터 오버라이드 어느 것도 섞이면 안 된다 " +
                        "(v0.5 감광 예외① / v0.7 설계 역전 교정).");
                }
            }
        }

        [Test]
        public void WindowPaper_IsUnlit_SoRoomDimmingCannotReachIt()
        {
            // 창호지가 Sprite-Lit이면 그 밝기에 실내 전역광이 곱해진다 = 흑화가 진실 채널을 어둡게 만든다.
            // (이것이 v0.7 이전의 실제 상태였다. 소금을 무광으로 바꾼 v0.6과 같은 처방.)
            string material = MaterialGuidOfRendererOn(ReadAsset(RoomPrefabPath), SkyObjectName);
            Assert.IsNotNull(material,
                $"Room.prefab에서 '{SkyObjectName}'의 SpriteRenderer를 찾지 못했다 — " +
                "창 구조를 바꿨다면 SkyObjectName과 V07Setup.WindowSkyPath를 함께 갱신할 것.");
            Assert.AreNotEqual(SpriteLitGuid, material,
                "창호지가 Sprite-Lit이다 — 방이 어두워지면 창도 같이 어두워진다. " +
                "무광(Sprites-Default)으로 되돌릴 것: 메뉴 'Morae/Setup v0.7'.");
        }

        [Test]
        public void GlobalDawnBoost_NoLongerOutshinesTheRoomBase()
        {
            // v0.7 §3. 씬에 직렬화된 값을 본다 — C# 초기값만 고치면 게임은 안 바뀐다
            // ([[씬-직렬화가-코드-기본값을-이긴다]]).
            string scene = ReadAsset(ScenePath);
            float boost = Scalar(scene, "globalDawnBoost");
            float baseLight = Scalar(scene, "globalBase");

            Assert.Less(boost, baseLight,
                $"globalDawnBoost({boost})가 방 기본 조도({baseLight}) 이상이다 — " +
                "아침이 창이 아니라 방을 밝힌다. 창이 광원으로 도드라지지 않는다 (v0.7 §3).");
            Assert.LessOrEqual(boost, 0.08f, $"globalDawnBoost {boost} — 명세 v0.7의 '0.06 내외'를 벗어났다.");
            Assert.Greater(boost, 0f, "0이면 창 주변이 아침에도 전혀 안 밝아져 '빛이 든다'가 사라진다.");
        }

        // ================================================================= ② 감지 가능

        [Test]
        public void Thresholds_AreThreeSortedStepsInsideTheRun()
        {
            Assert.AreEqual(DawnStageModel.StageCount - 1, DawnStageModel.Thresholds.Length,
                "단계 수와 경계 수가 어긋났다 (n단계 = n−1 경계).");
            Assert.IsTrue(DawnStageModel.StageCount >= 3 && DawnStageModel.StageCount <= 4,
                "명세 v0.7은 3~4단계다. 더 잘게 쪼개면 다시 연속 보간으로 돌아간다.");

            float prev = 0f;
            foreach (float t in DawnStageModel.Thresholds)
            {
                Assert.Greater(t, prev, "경계가 오름차순이 아니다");
                Assert.Less(t, 1f);
                prev = t;
            }
        }

        [Test]
        public void Stage_NeverGoesBackwards()
        {
            int prev = 0;
            for (int i = 0; i <= 1000; i++)
            {
                int s = DawnStageModel.Stage(i / 1000f);
                Assert.GreaterOrEqual(s, prev, "여명 단계가 되돌아갔다 — 창이 다시 어두워지면 진실 채널이 거짓말을 한다.");
                prev = s;
            }
            Assert.AreEqual(0, DawnStageModel.Stage(0f), "여명 0(P1~P4)은 밤이어야 한다.");
            Assert.AreEqual(DawnStageModel.StageCount - 1, DawnStageModel.Stage(1f), "여명 1은 완연한 아침이어야 한다.");
        }

        [Test]
        public void PaperColor_CrossesFromBlueToWarm()
        {
            // 색상 변화는 밝기 변화보다 훨씬 잘 감지된다 — 그 전제가 실제로 성립하는지 본다.
            for (int s = 0; s < DawnStageModel.StageCount; s++)
            {
                Color c = DawnStageModel.PaperColor(s);
                bool cool = c.b > c.r;
                Assert.AreEqual(s <= 1, cool,
                    $"{s}단계 색 {c}의 색상 방향이 틀렸다 — 0·1단계는 파랑(남색·회청), 2·3단계는 주황이어야 한다.");
            }
        }

        [Test]
        public void PaperColor_AdjacentStagesAreTellableApart()
        {
            for (int s = 1; s < DawnStageModel.StageCount; s++)
            {
                Color a = DawnStageModel.PaperColor(s - 1);
                Color b = DawnStageModel.PaperColor(s);
                float delta = Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Max(Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b)));
                Assert.Greater(delta, 0.12f,
                    $"{s - 1}→{s}단계 색차 {delta:F3}가 너무 작다 — 계단으로 끊은 의미가 사라진다.");
                Assert.Greater(Luma(b), Luma(a), $"{s}단계가 앞 단계보다 어둡다 — 아침이 거꾸로 간다.");
            }
        }

        [Test]
        public void FloorPatch_GrowsAndSharpens()
        {
            Assert.AreEqual(0f, DawnStageModel.PatchLength(0), "밤에는 바닥에 무늬가 없어야 한다.");
            Assert.AreEqual(0f, DawnStageModel.HazeAlpha(0));
            Assert.AreEqual(0f, DawnStageModel.GridAlpha(0));

            for (int s = 1; s < DawnStageModel.StageCount; s++)
            {
                Assert.Greater(DawnStageModel.PatchLength(s), DawnStageModel.PatchLength(s - 1),
                    "무늬가 길어지지 않으면 '형태가 달라졌다'가 안 읽힌다.");
                Assert.GreaterOrEqual(DawnStageModel.PatchWidth(s), DawnStageModel.PatchWidth(s - 1));
                Assert.Greater(DawnStageModel.GridAlpha(s), DawnStageModel.GridAlpha(s - 1),
                    "창살 격자가 또렷해지지 않으면 '선명해진다'가 없다.");
            }
            for (int s = 2; s < DawnStageModel.StageCount; s++)
            {
                Assert.Less(DawnStageModel.HazeAlpha(s), DawnStageModel.HazeAlpha(s - 1),
                    "흐림이 걷히지 않으면 격자만 겹쳐 보여 선명해지는 인상이 죽는다.");
            }
        }

        [Test]
        public void FloorPatch_StaysOnTheRightRegionFloor()
        {
            // 우측 구역 바닥: x 0.22~5.03 / y −4.245~0.675 (v0.6 L자 지오메트리)
            const float leftWall = 0.22f, rightWall = 5.03f, bottomWall = -4.245f, regionTop = 0.675f;
            Assert.LessOrEqual(DawnStageModel.PatchAnchorY, regionTop,
                "무늬 윗변이 벽 위로 올라가면 바닥이 아니라 벽에 붙은 판으로 보인다.");

            for (int s = 0; s < DawnStageModel.StageCount; s++)
            {
                float half = DawnStageModel.PatchWidth(s) * 0.5f;
                float cx = DawnStageModel.PatchCenterX(s);
                Assert.Greater(cx - half, leftWall, $"{s}단계 무늬가 좌측 단차 벽을 넘는다");
                Assert.Less(cx + half, rightWall, $"{s}단계 무늬가 우측 벽을 넘는다");
                Assert.Greater(DawnStageModel.PatchAnchorY - DawnStageModel.PatchLength(s), bottomWall,
                    $"{s}단계 무늬가 아래 벽을 뚫는다");
            }
        }

        [Test]
        public void StepBlend_IsAnEdgeSoftener_NotAnInterpolation()
        {
            Assert.AreEqual(0f, DawnStageModel.StepBlend01(0f), 1e-5f);
            Assert.AreEqual(1f, DawnStageModel.StepBlend01(DawnStageModel.StepBlendSec), 1e-5f);
            Assert.AreEqual(1f, DawnStageModel.StepBlend01(999f), 1e-5f);
            Assert.LessOrEqual(DawnStageModel.StepBlendSec, 1f,
                "전환이 1초를 넘으면 다시 '연속 변화'가 되어 감지 불가로 돌아간다 (이번 개정의 이유).");
        }

        // ================================================================= ③ P6 모호함 보존

        [Test]
        public void ExactlyOneStep_LandsWellInsideTheP6TrapWindow()
        {
            // 경계가 P6 밖에 있으면 P6 전체가 단일 색이 되어 "아직 아침이 아니다"를 **확신**하게 된다 → 함정이 죽는다.
            // 경계가 P6 가장자리에 붙어도 사실상 같은 일이 벌어진다 — 그래서 여유(0.04)를 둔다.
            const float margin = 0.04f;
            var inside = new List<float>();
            foreach (float t in DawnStageModel.Thresholds)
            {
                if (t > DawnStageModel.TrapDawnStart && t < DawnStageModel.TrapDawnEnd) inside.Add(t);
            }

            Assert.AreEqual(1, inside.Count,
                $"P6 구간({DawnStageModel.TrapDawnStart}~{DawnStageModel.TrapDawnEnd}) 안의 경계가 {inside.Count}개다 — " +
                "정확히 하나여야 한다. 0개면 함정 내내 창이 한 색이라 판별이 쉬워지고, " +
                "2개 이상이면 함정 구간이 '변화 쇼'가 되어 아침으로 오인된다.");

            Assert.Greater(inside[0], DawnStageModel.TrapDawnStart + margin);
            Assert.Less(inside[0], DawnStageModel.TrapDawnEnd - margin);
        }

        [Test]
        public void TrapWindow_NeverShowsTheMorningStage()
        {
            int atEnd = DawnStageModel.Stage(DawnStageModel.TrapDawnEnd - 1e-4f);
            Assert.Less(atEnd, DawnStageModel.StageCount - 1,
                "P6 함정 구간에서 이미 마지막(아침) 단계가 보인다 — 창이 '아침이 왔다'고 말하면 " +
                "문밖 목소리가 진짜가 되어 함정이 함정이 아니게 된다.");
        }

        [Test]
        public void TrapWindow_MatchesThePhaseTableItActuallyShips()
        {
            // 상수로 적어둔 P6 구간이 배포 데이터와 어긋나면 위 두 테스트가 엉뚱한 곳을 검사한다.
            List<PhaseRow> rows = PhaseRows();
            PhaseRow p6 = rows.Find(r => r.Id == (int)PhaseId.P6);
            Assert.IsNotNull(p6, "PhaseTable에 P6 행이 없다");
            Assert.AreEqual(DawnStageModel.TrapDawnStart, p6.DawnStart, 1e-4f,
                "DawnStageModel.TrapDawnStart가 PhaseTable의 P6 dawnStart와 다르다 — 함정 보호가 헛돈다.");
            Assert.AreEqual(DawnStageModel.TrapDawnEnd, p6.DawnEnd, 1e-4f,
                "DawnStageModel.TrapDawnEnd가 PhaseTable의 P6 dawnEnd와 다르다.");
        }

        // ================================================================= ④ 실제 진행에서의 사건 수

        [Test]
        public void TheRun_ShowsThreeUnmistakableChanges()
        {
            // 배포되는 PhaseTable을 실제로 걸어보며 단계가 바뀌는 시각을 센다.
            // "변하고 있다"가 학습되려면 사건이 여러 번, 그리고 너무 늦지 않게 시작해야 한다.
            List<StageChange> changes = WalkTheRun(out float total);

            Assert.AreEqual(DawnStageModel.StageCount - 1, changes.Count,
                $"본편 {total:F0}초 동안 여명 단계가 {changes.Count}번 바뀐다 — " +
                "경계 수와 다르면 어떤 경계는 게임 안에서 영영 넘지 않는다는 뜻이다.");

            Assert.Less(changes[0].AtSec, total * 0.70f,
                $"첫 변화가 {changes[0].AtSec:F0}초(전체의 {changes[0].AtSec / total:P0})에야 온다 — " +
                "너무 늦으면 '창이 변한다'는 학습 자체가 생기지 않는다.");

            bool anyInTrap = changes.Exists(c => c.Phase == (int)PhaseId.P6);
            Assert.IsTrue(anyInTrap, "P6(최후의 함정) 도중에 단계가 바뀌지 않는다 — 모호함이 사라진다.");

            for (int i = 1; i < changes.Count; i++)
            {
                Assert.Greater(changes[i].AtSec - changes[i - 1].AtSec, 10f * DawnStageModel.StepBlendSec,
                    "두 변화가 너무 붙어 있어 각각 '사건'으로 안 읽힌다.");
            }
        }

        // ================================================================= 파싱 유틸

        private struct StageChange
        {
            public float AtSec;
            public int Phase;
            public int To;
        }

        private static List<StageChange> WalkTheRun(out float total)
        {
            var changes = new List<StageChange>();
            List<PhaseRow> rows = PhaseRows();
            const float dt = 0.05f;
            float t = 0f;
            int stage = 0;
            total = 0f;
            foreach (PhaseRow r in rows) total += r.Duration;

            foreach (PhaseRow r in rows)
            {
                for (float e = 0f; e < r.Duration; e += dt)
                {
                    float dawn = Mathf.Lerp(r.DawnStart, r.DawnEnd, r.Duration > 0f ? e / r.Duration : 1f);
                    int s = DawnStageModel.Stage(dawn);
                    if (s != stage)
                    {
                        changes.Add(new StageChange { AtSec = t + e, Phase = r.Id, To = s });
                        stage = s;
                    }
                }
                t += r.Duration;
            }
            return changes;
        }

        private sealed class PhaseRow
        {
            public int Id;
            public float Duration;
            public float DawnStart;
            public float DawnEnd;
        }

        private static List<PhaseRow> PhaseRows()
        {
            var rows = new List<PhaseRow>();
            string text = ReadAsset(PhasePath);
            MatchCollection blocks = Regex.Matches(text,
                @"-\s+phaseId:\s*(\d+)(.*?)(?=\n\s*-\s+phaseId:|\z)", RegexOptions.Singleline);
            foreach (Match b in blocks)
            {
                rows.Add(new PhaseRow
                {
                    Id = int.Parse(b.Groups[1].Value, CultureInfo.InvariantCulture),
                    Duration = Scalar(b.Groups[2].Value, "duration"),
                    DawnStart = Scalar(b.Groups[2].Value, "dawnStart"),
                    DawnEnd = Scalar(b.Groups[2].Value, "dawnEnd"),
                });
            }
            Assert.IsNotEmpty(rows, "PhaseTable을 읽지 못했다");
            return rows;
        }

        private static float Scalar(string block, string key)
        {
            Match m = Regex.Match(block, $@"(?m)^\s*{key}:\s*([-\d.][-\d.eE+]*)\s*$");
            Assert.IsTrue(m.Success, $"'{key}' 를 찾지 못했다");
            return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 이름으로 GameObject를 찾아 그 SpriteRenderer(<c>!u!212</c>)의 머티리얼 GUID를 돌려준다.
        /// fileID 상수를 박아두지 않고 <b>이름 → 컴포넌트 목록 → 렌더러</b>로 따라가므로
        /// 프리팹을 다시 저장해 fileID가 갈려도 살아남는다
        /// (배선 감사가 fileID 대신 계층 경로를 키로 쓰는 것과 같은 이유).
        /// </summary>
        private static string MaterialGuidOfRendererOn(string yaml, string objectName)
        {
            var blocks = new Dictionary<string, string>();  // anchor id → 본문
            var classes = new Dictionary<string, string>(); // anchor id → 클래스 번호
            foreach (string chunk in Regex.Split(yaml, @"(?m)^---\s"))
            {
                Match head = Regex.Match(chunk, @"^!u!(\d+)\s+&(\d+)");
                if (!head.Success) continue;
                classes[head.Groups[2].Value] = head.Groups[1].Value;
                blocks[head.Groups[2].Value] = chunk;
            }

            foreach (KeyValuePair<string, string> kv in blocks)
            {
                if (classes[kv.Key] != "1") continue;                                   // GameObject만
                if (!Regex.IsMatch(kv.Value, $@"(?m)^\s*m_Name:\s*{objectName}\s*$")) continue;
                foreach (Match c in Regex.Matches(kv.Value, @"-\s*component:\s*\{fileID:\s*(\d+)\}"))
                {
                    string id = c.Groups[1].Value;
                    if (!classes.TryGetValue(id, out string cls) || cls != "212") continue;
                    Match mat = Regex.Match(blocks[id], @"m_Materials:\s*\n\s*-\s*\{[^}]*guid:\s*([a-f0-9]{32})");
                    return mat.Success ? mat.Groups[1].Value : string.Empty;
                }
            }
            return null;
        }

        /// <summary>주석 안의 단어까지 금지하면 "왜 안 되는지"를 설명할 수 없게 된다 — 주석은 걷어낸다.</summary>
        private static string StripComments(string source)
        {
            source = Regex.Replace(source, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(source, @"//[^\n]*", " ");
        }

        private static string ReadAsset(string relative)
        {
            string path = Path.Combine(Application.dataPath, relative);
            Assert.IsTrue(File.Exists(path), $"파일이 없다: {path}");
            return File.ReadAllText(path);
        }

        private static float Luma(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
    }
}
