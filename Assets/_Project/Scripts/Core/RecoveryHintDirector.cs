using Morae.Game.Data;
using UnityEngine;

namespace Morae.Game.Core
{
    /// <summary>
    /// 회복(사후 정화) 1회 힌트 (v0.6).
    ///
    /// **왜 튜토리얼이 아니라 여기인가**: 학습 구간은 오염이 일어나지 않도록 설계돼 있어(AttackScheduler 학습 모드)
    /// "검어진 걸 지운다"를 가르칠 대상이 화면에 없다. 그래서 본편에서 **실제로 처음 더러워지는 순간**에 한 번만 말한다.
    /// 그때는 플레이어도 이미 "이거 어떡하지"를 느끼고 있어서, 같은 문장이 훨씬 적은 비용으로 박힌다.
    ///
    /// **화자는 반드시 손자(속마음)다.** 문이 잠긴 뒤 문밖 목소리는 전부 의심하라는 게 이 게임의 규칙인데,
    /// 할아버지가 본편에서 조언을 하면 규칙이 스스로를 부정한다. 그래서 id에 <c>prologue-</c> 접두사를 쓰지 않아
    /// 대화상자(초상+화자명)가 아니라 자막으로 나간다 — 밖에서 온 말이 아니라 안에서 든 생각이라는 표시다.
    /// </summary>
    public sealed class RecoveryHintDirector : MonoBehaviour
    {
        [SerializeField, TextArea]
        private string text = "(…검다. 나중에라도 지울 수 있댔지. 숨 돌릴 때 불상 앞에 앉자.)";
        [SerializeField] private float duration = 4.5f;
        [SerializeField] private float delaySec = 1.2f;   // 오염 연출(소리·색)이 먼저 도착하도록 한 박자 늦춘다

        private bool _fired;
        private bool _training;
        private float _timer = -1f;

        private void OnEnable()
        {
            GameEvents.CornerStageChanged += HandleCornerStage;
            GameEvents.TrainingModeChanged += HandleTrainingMode;
        }

        private void OnDisable()
        {
            GameEvents.CornerStageChanged -= HandleCornerStage;
            GameEvents.TrainingModeChanged -= HandleTrainingMode;
        }

        private void HandleTrainingMode(bool active) => _training = active;

        private void HandleCornerStage(int corner, int stage)
        {
            // 학습 구간에서는 오염이 없지만, 혹시 발행되더라도 여기서 막는다 (할아버지가 아직 옆에 있는 장면)
            if (_fired || _training || stage < (int)CornerStage.Gray) return;
            _fired = true;
            _timer = delaySec;
        }

        private void Update()
        {
            if (_timer < 0f) return;
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = -1f;

            // 화자 빈 문자열 = 속마음. 자막이 "화자: 내용"으로 찍지 않고 괄호 문장만 남긴다
            var def = new EventDef("hint-recovery", PhaseId.P1, 0f, GameEventKind.Scripted, AudioChannel.Room,
                0f, false, new[] { new SubtitleLine(string.Empty, text, duration) });
            GameEvents.RaiseGameEventFired(def);
            Debug.Log("[HINT] 첫 오염 — 사후 정화 안내 1회");
        }
    }
}
