using Morae.Game.Core;
using Morae.Game.Data;
using NUnit.Framework;
using UnityEngine;

namespace Morae.Game.Tests.EditMode
{
    /// <summary>
    /// 명세 v0.5 — 흑화 귀퉁이당 즉시 대가 / 어둠 속 실루엣 / 프롤로그 강제 학습의 순수 로직 회귀 테스트.
    /// 기본값은 BalanceConfig 필드 초기값과 일치시켜 둔다 (에셋 튜닝이 갈라지면 여기가 먼저 깨져야 한다).
    /// </summary>
    public sealed class SaltStakesTests
    {
        // BalanceConfig 초기값
        private const float LightPenalty = 0.018f;
        private const float MinRoomLight = 0.055f;
        private const float SanityDrain = 0.15f;
        private const float IntervalReduction = 0.05f;
        private const float MinIntervalScale = 0.6f;
        // LightingController 직렬화 초기값
        private const int MaxAttempts = 3;
        private const float GlobalBase = 0.12f;
        private const float DawnBoost = 0.18f;

        // ---------- §1 조도: 합산 후 클램프 ----------

        [Test]
        public void RoomLight_NoBlack_IsBaseUnchanged()
        {
            Assert.AreEqual(GlobalBase, CornerPenaltyModel.RoomLightIntensity(
                GlobalBase, 0f, DawnBoost, 0f, 0, LightPenalty, MinRoomLight), 1e-5f);
        }

        [Test]
        public void RoomLight_PhaseBiasAndPenalty_AreSummedNotAppliedTwice()
        {
            // P6 연출 bias(−0.10)와 흑 2개 감광(−0.036)은 **한 번만** 합산돼야 한다.
            // 따로 클램프하면 이중 감광이 되어 바닥에 처박힌다 (v0.5 금지 사항).
            float expected = GlobalBase + 0.4f * DawnBoost - 0.10f - LightPenalty * 2f;
            float actual = CornerPenaltyModel.RoomLightIntensity(
                GlobalBase, 0.4f, DawnBoost, -0.10f, 2, LightPenalty, MinRoomLight);
            Assert.Greater(expected, MinRoomLight, "이 케이스는 바닥에 걸리지 않아야 검증이 의미가 있다");
            Assert.AreEqual(expected, actual, 1e-5f);
        }

        [Test]
        public void RoomLight_PositiveBias_BrightensEvenWithBlackCorners()
        {
            // P4 소강(+0.10)은 흑화 중에도 밝아진다 — bias가 감광에 삼켜지면 "안심 유도" 연출이 죽는다
            float withBias = CornerPenaltyModel.RoomLightIntensity(GlobalBase, 0f, DawnBoost, 0.10f, 1, LightPenalty, MinRoomLight);
            float withoutBias = CornerPenaltyModel.RoomLightIntensity(GlobalBase, 0f, DawnBoost, 0f, 1, LightPenalty, MinRoomLight);
            Assert.AreEqual(0.10f, withBias - withoutBias, 1e-5f);
        }

        [Test]
        public void RoomLight_AllCornersBlack_ClampsToFloorNotBlackout()
        {
            // 최악(흑 4 + 가장 어두운 페이즈 bias)에서도 바닥 아래로는 내려가지 않는다 — 암전 금지
            float v = CornerPenaltyModel.RoomLightIntensity(GlobalBase, 0f, DawnBoost, -0.13f, 4, LightPenalty, MinRoomLight);
            Assert.AreEqual(MinRoomLight, v, 1e-5f);
            Assert.Greater(v, 0f);
        }

        [Test]
        public void RoomLight_IsMonotonicallyDarkerPerBlackCorner()
        {
            float prev = float.MaxValue;
            for (int n = 0; n <= 4; n++)
            {
                float v = CornerPenaltyModel.RoomLightIntensity(GlobalBase, 0f, DawnBoost, 0f, n, LightPenalty, MinRoomLight);
                Assert.LessOrEqual(v, prev, $"흑 {n}개에서 오히려 밝아졌다");
                prev = v;
            }
        }

        [Test]
        public void CountBlack_TreatsDeepBlackAsBlack()
        {
            // stage 3(흑+심화)은 표기 전용 값 — 흑으로 세지 않으면 심화된 귀퉁이가 대가에서 빠져버린다
            Assert.AreEqual(0, CornerPenaltyModel.CountBlack(new[] { 0, 1, 0, 1 }));
            Assert.AreEqual(1, CornerPenaltyModel.CountBlack(new[] { 2, 1, 0, 0 }));
            Assert.AreEqual(2, CornerPenaltyModel.CountBlack(new[] { 3, 1, 2, 0 }));
            Assert.AreEqual(4, CornerPenaltyModel.CountBlack(new[] { 2, 3, 2, 3 }));
            Assert.IsTrue(CornerPenaltyModel.IsBlackStage((int)CornerStage.DeepBlack));
            Assert.IsFalse(CornerPenaltyModel.IsBlackStage((int)CornerStage.Gray));
        }

        // ---------- §1 대가 스케일링 ----------

        [Test]
        public void SanityDrain_ScalesLinearlyWithBlackCount()
        {
            Assert.AreEqual(0f, CornerPenaltyModel.SanityDrainPerSec(0, SanityDrain), 1e-5f);
            Assert.AreEqual(0.15f, CornerPenaltyModel.SanityDrainPerSec(1, SanityDrain), 1e-5f);
            Assert.AreEqual(0.45f, CornerPenaltyModel.SanityDrainPerSec(3, SanityDrain), 1e-5f);
            Assert.AreEqual(0.60f, CornerPenaltyModel.SanityDrainPerSec(4, SanityDrain), 1e-5f);
        }

        [Test]
        public void SanityDrain_SustainedWorstCase_IsSurvivableLongerThanPrayerLoop()
        {
            // 흑 4는 지속 상태가 아니다 (4번째 흑 = 붕괴 판정). 실제 지속 최대는 흑 3.
            // 그 상태 + 후반 페이즈 상시 −0.5/s에서 만수 이성이 버티는 시간이
            // 흑 3곳을 회색으로 되돌리는 기도 시간(3채널 × 3s)보다 충분히 길어야 즉사가 아니다.
            const float phaseDrain = 0.5f;
            float total = phaseDrain + CornerPenaltyModel.SanityDrainPerSec(3, SanityDrain); // 0.95/s
            float survivalSec = 100f / total;                                                // ≈105s
            float recoveryLoopSec = 3f * 3f;                                                 // 기도 3회 = 9s
            Assert.Greater(survivalSec, recoveryLoopSec * 4f, "대가가 즉사 수준이면 학습이 아니라 처형이다");
            Assert.AreEqual(105.26f, survivalSec, 0.1f);
        }

        [Test]
        public void AttackIntervalScale_ShrinksPerBlackCornerAndHasFloor()
        {
            Assert.AreEqual(1.00f, CornerPenaltyModel.AttackIntervalScale(0, IntervalReduction, MinIntervalScale), 1e-5f);
            Assert.AreEqual(0.95f, CornerPenaltyModel.AttackIntervalScale(1, IntervalReduction, MinIntervalScale), 1e-5f);
            Assert.AreEqual(0.80f, CornerPenaltyModel.AttackIntervalScale(4, IntervalReduction, MinIntervalScale), 1e-5f);
            // 계수를 크게 올려도 간격이 0으로 붕괴하지 않는다
            Assert.AreEqual(MinIntervalScale, CornerPenaltyModel.AttackIntervalScale(4, 0.4f, MinIntervalScale), 1e-5f);
        }

        [Test]
        public void AttackClockRate_MultipliesWithTvAcceleration()
        {
            const float tvRate = 1.333333f;
            // TV 꺼짐 + 흑 4 → 1/0.8 = 1.25배
            Assert.AreEqual(1.25f, CornerPenaltyModel.AttackClockRate(4, IntervalReduction, MinIntervalScale, 1f), 1e-4f);
            // TV 켜짐 + 흑 4 → 곱연산 (합연산이 아니다)
            Assert.AreEqual(tvRate * 1.25f,
                CornerPenaltyModel.AttackClockRate(4, IntervalReduction, MinIntervalScale, tvRate), 1e-4f);
            // 흑 0에서는 TV 배속 그대로 — 대가가 없을 때 기존 동작이 변하면 회귀다
            Assert.AreEqual(tvRate, CornerPenaltyModel.AttackClockRate(0, IntervalReduction, MinIntervalScale, tvRate), 1e-5f);
        }

        [Test]
        public void WhisperVolume_RisesWithCornerStageAndClampsTable()
        {
            float[] table = { 0f, 0.14f, 0.42f, 0.6f };
            Assert.AreEqual(0f, CornerPenaltyModel.WhisperVolume(0, table), 1e-5f);
            Assert.AreEqual(0.42f, CornerPenaltyModel.WhisperVolume((int)CornerStage.Black, table), 1e-5f);
            Assert.AreEqual(0.6f, CornerPenaltyModel.WhisperVolume((int)CornerStage.DeepBlack, table), 1e-5f);
            Assert.AreEqual(0.6f, CornerPenaltyModel.WhisperVolume(99, table), 1e-5f);  // 범위 밖은 마지막 값
            Assert.AreEqual(0f, CornerPenaltyModel.WhisperVolume(2, null), 1e-5f);      // 미배선이어도 예외 없이 무음
        }

        [Test]
        public void SmoothFactor_IsFramerateIndependentAndBounded()
        {
            Assert.AreEqual(0f, CornerPenaltyModel.SmoothFactor(0f, 0.3f), 1e-5f);
            Assert.Less(CornerPenaltyModel.SmoothFactor(0.016f, 0.3f), CornerPenaltyModel.SmoothFactor(0.033f, 0.3f));
            Assert.LessOrEqual(CornerPenaltyModel.SmoothFactor(100f, 0.3f), 1f); // dt가 커도 오버슈트 없음
            Assert.AreEqual(1f, CornerPenaltyModel.SmoothFactor(0.016f, 0f), 1e-5f); // 러프 0 = 즉시
        }

        // ---------- §2 실루엣 ----------

        [Test]
        public void Silhouette_NeverAppearsWithoutBlackCorner()
        {
            Assert.AreEqual(0, SilhouetteSpawnModel.MaxConcurrent(0, 1, 3));
            Assert.Less(SilhouetteSpawnModel.SpawnInterval(0, 7f, 0.55f, 2.2f), 0f); // 음수 = 스폰 루프 정지
        }

        [Test]
        public void Silhouette_ScalesWithBlackCornerCount()
        {
            Assert.AreEqual(1, SilhouetteSpawnModel.MaxConcurrent(1, 1, 3));
            Assert.AreEqual(3, SilhouetteSpawnModel.MaxConcurrent(3, 1, 3));
            Assert.AreEqual(3, SilhouetteSpawnModel.MaxConcurrent(4, 1, 3), "동시 상한을 넘으면 가독성이 깨진다");

            float i1 = SilhouetteSpawnModel.SpawnInterval(1, 7f, 0.55f, 2.2f);
            float i3 = SilhouetteSpawnModel.SpawnInterval(3, 7f, 0.55f, 2.2f);
            Assert.AreEqual(7f, i1, 1e-4f);
            Assert.Less(i3, i1);
            Assert.GreaterOrEqual(SilhouetteSpawnModel.SpawnInterval(4, 7f, 5f, 2.2f), 2.2f); // 하한 준수
        }

        [Test]
        public void Silhouette_AvoidsPlayerAltarAndTelegraphingCorner()
        {
            var player = new Vector2(0f, -1f);
            var altar = new Vector2(-2.5f, 2.2f);
            var corners = new[] { new Vector2(6f, 3.5f), Vector2.zero, Vector2.zero, Vector2.zero };

            Assert.IsFalse(SilhouetteSpawnModel.IsReadablePosition(new Vector2(0.5f, -1f), player, altar, corners, 1, 2.2f));
            Assert.IsFalse(SilhouetteSpawnModel.IsReadablePosition(new Vector2(-2.5f, 2.5f), player, altar, corners, 1, 2.2f));
            Assert.IsFalse(SilhouetteSpawnModel.IsReadablePosition(new Vector2(5.5f, 3.5f), player, altar, corners, 1, 2.2f));
            Assert.IsTrue(SilhouetteSpawnModel.IsReadablePosition(new Vector2(3f, -3.4f), player, altar, corners, 1, 2.2f));
            // 전조가 끝난 귀퉁이는 회피 대상이 아니다 (count=0)
            Assert.IsTrue(SilhouetteSpawnModel.IsReadablePosition(new Vector2(5.5f, 3.5f), player, altar, corners, 0, 2.2f));
        }

        [Test]
        public void Silhouette_FadesInAndOut_NeverPopsIn()
        {
            Assert.AreEqual(0f, SilhouetteSpawnModel.FadeAlpha01(0f, 0.35f), 1e-5f);
            Assert.AreEqual(1f, SilhouetteSpawnModel.FadeAlpha01(0.5f, 0.35f), 1e-5f);
            Assert.AreEqual(0f, SilhouetteSpawnModel.FadeAlpha01(1f, 0.35f), 1e-5f);
            Assert.AreEqual(0.5f, SilhouetteSpawnModel.FadeAlpha01(0.175f, 0.35f), 1e-4f);
        }

        // ---------- §3 프롤로그 강제 학습 게이트 ----------

        [Test]
        public void Training_BlocksProgressUntilCountered()
        {
            var m = new PrologueTrainingModel();
            Assert.IsFalse(m.BlocksProgress, "시작 전에는 막지 않는다");

            m.Begin(CornerIndex.TopRight);
            Assert.IsTrue(m.BlocksProgress);
            Assert.AreEqual(TrainingStep.Warning, m.Step);

            // 경고 대사 중에는 전조가 뜨지 않는다 (인과를 말로 먼저 못 박는다)
            Assert.AreEqual(TrainingCommand.None, m.Tick(3f, 6f, 3.5f));
            Assert.AreEqual(TrainingCommand.FireTelegraph, m.Tick(3f, 6f, 3.5f));
            Assert.AreEqual(TrainingStep.Telegraph, m.Step);
            Assert.AreEqual(1, m.Attempts);
            Assert.IsTrue(m.IsAwaitingPrayer);

            m.OnResolved(true, MaxAttempts);
            Assert.IsTrue(m.IsCleared);
            Assert.IsFalse(m.BlocksProgress);
        }

        [Test]
        public void Training_FailureRetriesWithoutEndingRun()
        {
            var m = new PrologueTrainingModel();
            m.Begin(CornerIndex.TopRight);
            m.Tick(6f, 6f, 3.5f);

            m.OnResolved(false, MaxAttempts);
            Assert.AreEqual(TrainingStep.RetryGap, m.Step, "실패는 사망이 아니라 재시도다");
            Assert.IsTrue(m.BlocksProgress);
            Assert.IsFalse(m.IsCleared);

            Assert.AreEqual(TrainingCommand.None, m.Tick(2f, 6f, 3.5f));
            Assert.AreEqual(TrainingCommand.FireTelegraph, m.Tick(2f, 6f, 3.5f));
            Assert.AreEqual(2, m.Attempts);
            Assert.AreEqual(CornerIndex.TopRight, m.TargetCorner, "재시도해도 같은 방향을 배운다");

            m.OnResolved(true, MaxAttempts);
            Assert.IsTrue(m.IsCleared);
        }

        [Test]
        public void Training_SkipClearsGate()
        {
            var m = new PrologueTrainingModel();
            m.Begin(CornerIndex.BottomLeft);
            m.Skip(); // 프롤로그 스킵 시 이 구간도 함께 스킵 (v0.5 §3)
            Assert.IsTrue(m.IsCleared);
            Assert.IsFalse(m.BlocksProgress);
            Assert.AreEqual(TrainingCommand.None, m.Tick(100f, 6f, 3.5f), "통과 후에는 더 이상 전조를 내지 않는다");
        }

        [Test]
        public void Training_IgnoresResolveOutsideTelegraph()
        {
            var m = new PrologueTrainingModel();
            m.Begin(CornerIndex.TopLeft);
            m.OnResolved(true, MaxAttempts); // 경고 대사 중 날아온 본편 판정 — 학습을 건너뛰게 하면 안 된다
            Assert.AreEqual(TrainingStep.Warning, m.Step);
            Assert.IsFalse(m.IsCleared);
        }

        [Test]
        public void Training_MercyPassAfterMaxAttempts_NoSoftLock()
        {
            // 조준·위치를 못 찾는 플레이어를 영원히 가두면 그 판은 첫 화면에서 끝난다.
            // 시도 상한에 도달하면 상쇄 없이도 통과시키되, 배우지 못했다는 사실은 플래그로 남긴다.
            var m = new PrologueTrainingModel();
            m.Begin(CornerIndex.TopRight);
            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                m.Tick(99f, 6f, 3.5f);
                Assert.AreEqual(attempt, m.Attempts);
                m.OnResolved(false, MaxAttempts);
                if (attempt < MaxAttempts) Assert.IsFalse(m.IsCleared, $"{attempt}회차에서 조기 통과");
            }
            Assert.IsTrue(m.IsCleared);
            Assert.IsTrue(m.ClearedByMercy, "자비 통과는 상쇄 성공과 구분돼야 대사가 갈린다");
            Assert.IsFalse(m.BlocksProgress);
        }

        [Test]
        public void Training_MercyDoesNotFireOnSuccess()
        {
            var m = new PrologueTrainingModel();
            m.Begin(CornerIndex.TopRight);
            m.Tick(99f, 6f, 3.5f);
            m.OnResolved(true, MaxAttempts);
            Assert.IsTrue(m.IsCleared);
            Assert.IsFalse(m.ClearedByMercy);
        }

        [Test]
        public void Training_ResetAllowsReplay()
        {
            // Begin은 NotStarted에서만 먹는다 — Reset 없이 Play가 두 번 불리면 학습이 조용히 건너뛰어진다
            var m = new PrologueTrainingModel();
            m.Begin(CornerIndex.TopLeft);
            m.Skip();
            m.Begin(CornerIndex.BottomRight);
            Assert.IsTrue(m.IsCleared, "Reset 없이는 Begin이 무시된다");

            m.Reset();
            m.Begin(CornerIndex.BottomRight);
            Assert.AreEqual(TrainingStep.Warning, m.Step);
            Assert.AreEqual(CornerIndex.BottomRight, m.TargetCorner);
            Assert.AreEqual(0, m.Attempts);
            Assert.IsFalse(m.ClearedByMercy);
        }

        [Test]
        public void Training_TelegraphIsLongEnoughToWalkAndChannel()
        {
            // 전조 3초짜리 본편 규칙을 그대로 쓰면 이동만 하다 끝난다 — 학습 전조는 채널+이동 여유를 담아야 한다
            float d = PrologueTrainingModel.TelegraphDuration(3f, 11f);
            Assert.AreEqual(14f, d, 1e-5f);
            Assert.Greater(d, 3f);
        }
    }
}
