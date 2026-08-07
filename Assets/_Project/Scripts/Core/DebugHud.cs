using Morae.Game.Data;
using Morae.Game.Gauges;
using Morae.Game.Interactions;
using Morae.Game.Player;
using UnityEngine;

namespace Morae.Game.Core
{
    /// <summary>
    /// 개발용 디버그 오버레이 (개발 빌드 한정 — D2 체크포인트: 게이지 상태를 화면 임시 텍스트로 표시).
    /// 게임플레이 상태를 직접 읽는 유일한 화면 요소 — 디버그 전용이라 표현 계층 규칙(구독만)의 예외로 허용.
    /// 릴리스 빌드에서는 빈 컴포넌트 (본 게임 HUD는 다이어제틱 원칙 — 이 오버레이는 출시물 아님).
    /// </summary>
    public sealed class DebugHud : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [SerializeField] private GameFlowController flow;
        [SerializeField] private PhaseSequencer sequencer;
        [SerializeField] private AttackScheduler scheduler;
        [SerializeField] private SaltCorners salt;
        [SerializeField] private Sanity sanity;
        [SerializeField] private Talisman talisman;
        [SerializeField] private PlayerController player;
        [SerializeField] private DoorInteractable door;

        private string _text = "";
        private float _nextRefresh;

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.25f;
            _text = BuildText(); // 디버그 전용 — 할당 허용, 0.25s 스로틀
        }

        private string BuildText()
        {
            if (flow == null || sequencer == null) return "(DebugHud 미배선)";

            string phase = $"{sequencer.CurrentPhase} {sequencer.PhaseElapsed:F1}s"
                           + (sequencer.CurrentPhaseDef != null ? $"/{sequencer.CurrentPhaseDef.Duration:F0}s" : "");
            string attack = scheduler != null
                ? $"공격시계 {scheduler.LocalAttackClock:F1}s | 전조 {scheduler.ActiveTelegraphCount} | 다음 {scheduler.NextAttackId}@{scheduler.NextAttackTime:F1}s"
                : "공격 스케줄러 없음";
            // 흑 개수 n은 감광의 기준값. 더러운 곳 수는 부적이 타는 조건이라 따로 본다.
            string saltText = salt != null
                ? $"[{Cell(0)}{Cell(1)}{Cell(2)}{Cell(3)}] 흑{salt.BlackCornerCount} 더러움{salt.ContaminatedCornerCount}"
                : "-";
            string sanityText = sanity != null ? $"{sanity.Value:F0}/{sanity.Max:F0}" : "-";
            // v0.7 — 부적은 보유/소모가 아니라 남은 시간이다. 엔딩 등급이 여기서 갈리므로 초 단위로 본다.
            string talismanText = talisman != null
                ? $"{talisman.RemainingSec:F1}/{talisman.TotalSec:F0}s{(talisman.IsCritical ? " ⚠임계" : "")}"
                : "-";
            string playerText = player != null ? player.State.ToString() : "-";
            string doorText = door != null ? $"{door.Door} {door.LatchProgress01:P0}" : "-";

            return $"[F1 배속 {Time.timeScale:F0}x] [F2 진짜 신호] [Esc 재시작]\n"
                   + $"Flow {flow.State}{(flow.TrueSignalFired ? " (진짜 신호 발화됨)" : "")} | Phase {phase} | 시계 {ClockDisplayModel.Format(sequencer.DisplayedClockMin)} | 여명 {sequencer.Dawn01:F2}\n"
                   + $"{attack}\n"
                   + $"소금 {saltText} | 이성 {sanityText} | 부적 {talismanText}\n"
                   + $"플레이어 {playerText} | 문 {doorText}";
        }

        /// <summary>귀퉁이 1칸 표기 — 단계 0~2 (v0.7에서 심화 단계가 사라졌다).</summary>
        private string Cell(int corner) => salt == null ? "-" : salt.GetStage(corner).ToString();

        private void OnGUI()
        {
            GUI.color = Color.white;
            GUI.Label(new Rect(10f, 10f, 900f, 140f), _text);
        }
#endif
    }
}
