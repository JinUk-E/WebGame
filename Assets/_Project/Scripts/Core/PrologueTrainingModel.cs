using UnityEngine;

namespace Morae.Game.Core
{
    /// <summary>프롤로그 강제 학습이 이번 틱에 요구하는 동작 (명세 v0.5 §3).</summary>
    public enum TrainingCommand { None, FireTelegraph }

    /// <summary>학습 구간의 진행 단계.</summary>
    public enum TrainingStep { NotStarted, Warning, Telegraph, Cleanup, RetryGap, Cleared }

    /// <summary>
    /// 프롤로그 강제 학습 게이트 — 순수 상태 머신.
    ///
    /// 규칙을 텍스트가 아니라 1회 실행으로 가르친다: 경고 대사 → 전조 → <b>오염</b> → 그 자리로 가서 소금 뿌리기
    /// → 성공해야 본편 진입. <b>실패해도 사망하지 않고 재시도</b>한다 (안전 구간) — 그래서 이 모델에는
    /// 실패로 끝나는 종단 상태가 아예 없다. 유일한 출구는 Cleared 또는 Skip(프롤로그 스킵)이다.
    ///
    /// <para>
    /// <b>v0.7: Cleanup 단계 신설.</b> 옛 구조는 전조 판정 = 즉시 성패였다(전조 안에 기도를 완료하면 성공).
    /// 새 동사는 <b>더러워진 뒤에</b> 하는 일이라, 오염이 나고 나서 지울 시간이 따로 있어야 한다.
    /// 그래서 전조 → 오염(OnContaminated) → 정화 대기(Cleanup) → 정화 성공(OnPurified) 순으로 늘어났다.
    /// 정화 대기 시간이 지나면 벌 없이 재시도다.
    /// </para>
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
        /// <summary>오염된 소금을 지우기를 기다리는 중 — 이때만 플레이어의 손이 정답을 낼 수 있다.</summary>
        public bool IsAwaitingCleanup => Step == TrainingStep.Cleanup;

        public void Begin(int targetCorner)
        {
            if (Step != TrainingStep.NotStarted) return;
            TargetCorner = targetCorner;
            Attempts = 0;
            _timer = 0f;
            Step = TrainingStep.Warning; // 경고 대사가 먼저 — 인과("소금이 검어지면 길이 열린다")를 말로 못 박고 시작
        }

        /// <summary>
        /// 초기 상태로 되돌린다 — 프롤로그를 다시 재생하는 경로용.
        /// Begin은 NotStarted가 아니면 무시하므로, Reset 없이 Play가 두 번 불리면 학습이 조용히 건너뛰어진다.
        /// </summary>
        public void Reset()
        {
            Step = TrainingStep.NotStarted;
            TargetCorner = Data.CornerIndex.None;
            Attempts = 0;
            ClearedByMercy = false;
            _timer = 0f;
        }

        /// <summary>
        /// 대사·재시도·정화 대기 시간을 진행시키고, 전조를 띄울 때가 되면 FireTelegraph를 돌려준다.
        /// cleanupSec을 넘기도록 지우지 못하면 벌 없이 재시도로 돌아간다 (maxAttempts에 걸리면 자비 통과).
        /// </summary>
        public TrainingCommand Tick(float deltaTime, float warningSec, float retryGapSec,
            float cleanupSec, int maxAttempts)
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

                case TrainingStep.Cleanup:
                    _timer += deltaTime;
                    if (_timer < cleanupSec) return TrainingCommand.None;
                    FailAttempt(maxAttempts);
                    return TrainingCommand.None;

                default:
                    return TrainingCommand.None;
            }
        }

        /// <summary>
        /// 스스로 지운 게 아니라 시도 횟수로 통과했는가 — 규칙을 못 배운 채 넘어간 것이므로 호출부가 대사를 달리한다.
        /// </summary>
        public bool ClearedByMercy { get; private set; }

        /// <summary>전조가 판정되어 소금이 실제로 더러워졌다 — 이제부터가 배울 구간이다.</summary>
        public void OnContaminated()
        {
            if (Step != TrainingStep.Telegraph) return; // 학습 밖의 판정은 무시 (본편 스케줄과 섞이지 않게)
            _timer = 0f;
            Step = TrainingStep.Cleanup;
        }

        /// <summary>플레이어가 그 귀퉁이를 실제로 지웠다 — 통과.</summary>
        public void OnPurified()
        {
            if (Step != TrainingStep.Cleanup) return;
            _timer = 0f;
            Step = TrainingStep.Cleared;
        }

        /// <summary>
        /// 정화 대기 시간을 넘겼다 — 벌 없이 재시도. maxAttempts에 도달하면 자비 통과다:
        /// <b>소프트락 방지가 학습보다 우선</b>이다. 위치를 못 찾는 플레이어를 영원히 가두면
        /// 그 판은 첫 화면에서 끝난다 (잼 심사 포함).
        /// </summary>
        private void FailAttempt(int maxAttempts)
        {
            _timer = 0f;
            if (maxAttempts > 0 && Attempts >= maxAttempts)
            {
                ClearedByMercy = true;
                Step = TrainingStep.Cleared;
                return;
            }
            Step = TrainingStep.RetryGap;
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

        /// <summary>
        /// 학습용 정화 대기 시간 — 오염이 난 뒤 그 자리까지 <b>걸어가서</b> 홀드를 끝낼 시간이다.
        /// v0.7에서 예산이 바뀌었다: 옛 학습은 "불상 한 곳까지"만 가면 됐지만(6u 가정),
        /// 이제는 방 임의 지점에서 임의 귀퉁이까지라 최악 10.65u = 3.04초다.
        /// 처음 하는 사람은 화면에서 목적지를 찾는 시간까지 필요하므로 여유(travelAllowanceSec)를 넉넉히 얹는다.
        /// </summary>
        public static float CleanupDuration(float saltHoldSec, float travelAllowanceSec)
            => saltHoldSec + Mathf.Max(0f, travelAllowanceSec);
    }
}
