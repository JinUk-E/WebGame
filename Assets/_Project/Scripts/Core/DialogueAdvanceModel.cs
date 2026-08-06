using UnityEngine;

namespace Morae.Game.Core
{
    /// <summary>수동 진행 대사가 이번 틱에 요구하는 동작.</summary>
    public enum DialogueCommand
    {
        /// <summary>변화 없음 — 계속 현재 줄을 보여준다.</summary>
        None,
        /// <summary>다음 줄로 넘어갔다 — 호출자가 <see cref="DialogueAdvanceModel.Index"/> 줄을 발화한다.</summary>
        ShowLine,
        /// <summary>마지막 줄까지 넘겼다 — 대사 구간 종료.</summary>
        Finish,
    }

    /// <summary>
    /// 프롤로그 대사 수동 진행 게이트 (2026-08-06) — 순수 상태 머신. EditMode 테스트 대상.
    ///
    /// <para>
    /// 대사는 <b>시간이 아니라 입력</b>으로 넘어간다. 읽는 속도는 사람마다 다르고, 시간 기반 진행은
    /// 빠른 사람에겐 기다림이고 느린 사람에겐 유실이다. 이 모델은 "언제 넘길 수 있는가"만 판정하고,
    /// 무엇을 표시할지·입력을 어디서 읽는지는 호출자(PrologueDirector)가 정한다.
    /// </para>
    /// <para>
    /// <b>최소 표시 시간</b>이 유일한 시간 조건이다 — 연타(또는 눌린 채 들어온 손가락)로 대사 여러 줄이
    /// 한 프레임에 통째로 날아가는 것을 막는다. 줄마다 새로 재는 값이라 N줄이면 최소 N×minShowSec은 보장된다.
    /// </para>
    /// </summary>
    public sealed class DialogueAdvanceModel
    {
        /// <summary>현재 표시 중인 줄 번호. 시작 전·종료 후에는 -1.</summary>
        public int Index { get; private set; } = -1;
        /// <summary>현재 줄을 표시한 뒤 흐른 시간.</summary>
        public float Elapsed { get; private set; }
        /// <summary>대사 구간이 진행 중인가 (입력을 소유하는 구간인가).</summary>
        public bool IsActive { get; private set; }
        public int LineCount { get; private set; }

        /// <summary>0번 줄이 이미 표시된 상태로 시작한다 (호출자가 Begin 직후 0번을 발화한다).</summary>
        public void Begin(int lineCount)
        {
            LineCount = lineCount;
            Elapsed = 0f;
            if (lineCount <= 0)
            {
                Index = -1;
                IsActive = false;
                return;
            }
            Index = 0;
            IsActive = true;
        }

        /// <summary>스킵·중단 — 이후 어떤 입력도 이 모델을 통과하지 못한다.</summary>
        public void Stop()
        {
            IsActive = false;
            Index = -1;
            Elapsed = 0f;
        }

        /// <summary>최소 표시 시간을 채워 지금 넘길 수 있는가 (▼ 표시·디버그용).</summary>
        public bool CanAdvance(float minShowSec) => IsActive && Elapsed >= minShowSec;

        /// <summary>
        /// 시간을 진행시키고 진행 입력을 판정한다.
        /// 비활성 구간에서는 입력이 무조건 무시된다 — 학습 구간·본편으로 진행 입력이 새지 않게 하는 1차 방어선.
        /// </summary>
        public DialogueCommand Step(float deltaTime, bool advanceRequested, float minShowSec)
        {
            if (!IsActive) return DialogueCommand.None;

            Elapsed += deltaTime;
            if (!advanceRequested) return DialogueCommand.None;
            if (Elapsed < minShowSec) return DialogueCommand.None; // 연타 방어 — 아직 읽을 시간도 없었다

            Elapsed = 0f;
            Index++;
            if (Index < LineCount) return DialogueCommand.ShowLine;

            Index = -1;
            IsActive = false;
            return DialogueCommand.Finish;
        }

        /// <summary>
        /// 스크린 좌표가 뷰포트 비율로 지정한 영역(스킵 버튼 자리) 안인가 — 순수 판정.
        /// 해상도·레터박스와 무관하도록 비율로 다룬다. UI 라벨 위치와 이 사각형은 호출자가 맞춰 둔다.
        /// </summary>
        public static bool InViewportZone(Vector2 screenPos, float screenWidth, float screenHeight, Rect viewportZone)
        {
            if (screenWidth <= 0f || screenHeight <= 0f) return false;
            return viewportZone.Contains(new Vector2(screenPos.x / screenWidth, screenPos.y / screenHeight));
        }
    }
}
