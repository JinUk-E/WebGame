using UnityEngine;

namespace Morae.Game.Core
{
    /// <summary>프롤로그 강제 학습이 이번 틱에 요구하는 동작 (명세 v0.5 §3).</summary>
    public enum TrainingCommand { None, FireTelegraph }

    /// <summary>학습 구간의 진행 단계.</summary>
    public enum TrainingStep { NotStarted, Warning, Telegraph, RetryGap, Cleared }

    /// <summary>
    /// 프롤로그 강제 학습 게이트 (명세 v0.5 §3) — 순수 상태 머신. EditMode 테스트 대상.
    ///
    /// 규칙을 텍스트가 아니라 1회 실행으로 가르친다: 할아버지의 경고 대사 → 전조 → 불상 앞 방향 기도로 상쇄
    /// → 성공해야 본편 진입. **실패해도 사망하지 않고 재시도**한다 (안전 구간) — 그래서 이 모델에는
    /// 실패로 끝나는 종단 상태가 아예 없다. 유일한 출구는 Cleared(상쇄 성공) 또는 Skip(프롤로그 스킵)이다.
    /// </summary>
    public sealed class PrologueTrainingModel
    {
        private float _timer;

        public TrainingStep Step { get; private set; } = TrainingStep.NotStarted;
        /// <summary>학습에 쓰는 귀퉁이 (CornerIndex 규약). 재시도해도 바뀌지 않는다 — 같은 방향을 두 번 배운다.</summary>
        public int TargetCorner { get; private set; } = Data.CornerIndex.None;
        /// <summary>전조를 낸 횟수 — 첫 시도는 1.</summary>
        public int Attempts { get; private set; }

        /// <summary>본편 진입을 막고 있는가. 시작 전(NotStarted)과 통과 후(Cleared)에만 false.</summary>
        public bool BlocksProgress => Step != TrainingStep.NotStarted && Step != TrainingStep.Cleared;
        public bool IsCleared => Step == TrainingStep.Cleared;
        /// <summary>전조가 떠 있는 동안 = 플레이어 입력(기도)을 기다리는 중.</summary>
        public bool IsAwaitingPrayer => Step == TrainingStep.Telegraph;

        public void Begin(int targetCorner)
        {
            if (Step != TrainingStep.NotStarted) return;
            TargetCorner = targetCorner;
            Attempts = 0;
            _timer = 0f;
            Step = TrainingStep.Warning; // 경고 대사가 먼저 — 인과("소금이 검어지면 길이 열린다")를 말로 못 박고 시작
        }

        /// <summary>대사·재시도 대기 시간을 진행시키고, 전조를 띄울 때가 되면 FireTelegraph를 돌려준다.</summary>
        public TrainingCommand Tick(float deltaTime, float warningSec, float retryGapSec)
        {
            switch (Step)
            {
                case TrainingStep.Warning:
                    _timer += deltaTime;
                    if (_timer < warningSec) return TrainingCommand.None;
                    return EnterTelegraph();

                case TrainingStep.RetryGap:
                    _timer += deltaTime;
                    if (_timer < retryGapSec) return TrainingCommand.None;
                    return EnterTelegraph();

                default:
                    return TrainingCommand.None;
            }
        }

        /// <summary>전조 판정 결과 통보. countered=true면 통과, false면 벌 없이 재시도 대기로 돌아간다.</summary>
        public void OnResolved(bool countered)
        {
            if (Step != TrainingStep.Telegraph) return; // 학습 밖의 판정은 무시 (본편 스케줄과 섞이지 않게)
            _timer = 0f;
            Step = countered ? TrainingStep.Cleared : TrainingStep.RetryGap;
        }

        /// <summary>프롤로그 스킵(2회차 이후·E 스킵) — 이 구간도 함께 건너뛴다 (v0.5 §3).</summary>
        public void Skip()
        {
            _timer = 0f;
            Step = TrainingStep.Cleared;
        }

        private TrainingCommand EnterTelegraph()
        {
            _timer = 0f;
            Attempts++;
            Step = TrainingStep.Telegraph;
            return TrainingCommand.FireTelegraph;
        }

        /// <summary>학습용 전조 길이 — 이동 + 채널 시간을 다 담아야 하므로 본편 3초와 별개로 넉넉히 잡는다.</summary>
        public static float TelegraphDuration(float prayerChannelSec, float travelAllowanceSec)
            => Mathf.Max(prayerChannelSec, prayerChannelSec + Mathf.Max(0f, travelAllowanceSec));
    }
}
