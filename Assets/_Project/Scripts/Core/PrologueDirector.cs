using System;
using Morae.Game.Data;
using Morae.Game.Player;
using UnityEngine;

namespace Morae.Game.Core
{
    /// <summary>
    /// 프롤로그 연출 (명세 §4 — 규칙 학습, 컷 불가 항목. D3).
    /// 할아버지 대면 대사를 한 줄씩 GameEventFired로 발화 — 자막은 SubtitleView, 음성은 SoundManager가
    /// 기존 구독 경로로 소화한다 (전용 UI 없음). 마지막 줄 후 걸쇠 잠금 = 완료 콜백 → GameFlow가 본편 진입.
    /// E 탭 = 남은 프롤로그 스킵 (첫 판에도 허용 — 잼 심사 배려). 재시작 시엔 GameFlow가 통째로 스킵.
    ///
    /// v0.5 §3 — 대사 다음에 **강제 학습 구간**이 붙는다. 규칙을 텍스트가 아니라 1회 실행으로 가르친다:
    ///   할아버지의 경고("소금이 검어지면 그쪽으로 길이 열린다") → 전조 → 불상 앞 방향 기도로 상쇄 → 통과.
    ///   실패해도 사망하지 않고 재시도한다 (AttackScheduler 학습 모드 = 오염·이성 감소 없음).
    ///   학습 중에는 E가 기도 입력이므로 E 스킵을 막는다 — 스킵은 대사 구간에서만.
    /// </summary>
    public sealed class PrologueDirector : MonoBehaviour
    {
        [Serializable]
        private struct PrologueLine
        {
            public string speaker;
            [TextArea] public string text;
            public float duration;

            public PrologueLine(string speaker, string text, float duration)
            {
                this.speaker = speaker;
                this.text = text;
                this.duration = duration;
            }
        }

        [SerializeField] private float linePauseSec = 0.4f;
        [SerializeField]
        private PrologueLine[] lines =
        {
            // 정보량은 그대로 두되 손자에게 하는 말투로 — 규칙 나열이 아니라 당부처럼 들려야 한다.
            new PrologueLine("할아버지", "얘야, 너무 겁먹지 말거라. 오늘 밤만… 오늘 밤만 넘기면 된다.", 4f),
            new PrologueLine("할아버지", "이 방에서 나가지 말고. 네 귀퉁이 소금이 널 지켜줄 게다. 검게 물들거든 불상 앞에 앉아 손 모으고 빌면 돼.", 5.5f),
            new PrologueLine("할아버지", "부적은 딱 한 번, 너 대신 막아준다. 그게 까맣게 타거든… 그땐 정말 조심해야 한다, 알겠지.", 5f),
            new PrologueLine("할아버지", "그리고 이건 꼭 새겨듣거라. 문이 잠긴 뒤로 문밖에서 나는 소리는… 할애비 목소리라도 믿지 마라.", 5.5f),
            new PrologueLine("할아버지", "그것은 목소리를 훔치는 놈이야. 진짜 아침은 소리로 오지 않는다. 창밖이 밝아야 아침이지. 시계도 믿지 말고.", 5.5f),
            new PrologueLine("할아버지", "일곱 시 반이면 할멈이랑 같이 데리러 오마. 그때까지만… 응? 우리 손주 잘할 수 있지.", 5f),
            new PrologueLine("나", "(걸쇠를 걸었다. 이제 이 방이 전부다.)", 3f),
        };

        [Header("강제 학습 (명세 v0.5 §3)")]
        [SerializeField] private BalanceConfig config;
        [SerializeField] private AttackScheduler scheduler;
        [SerializeField] private int trainingCorner = CornerIndex.TopRight; // 불상(좌상단)에서 가장 잘 보이는 대각
        // 인과를 말로 못 박는 경고 — 공격 **전에** 온다. 학습이 끝난 뒤의 설명은 이미 늦다.
        [SerializeField]
        private PrologueLine warningLine = new PrologueLine("할아버지",
            "소금이 검어지면 그쪽으로 길이 열린다. 네 곳이 다 열리면 끝이야.", 5.5f);
        [SerializeField]
        private PrologueLine telegraphLine = new PrologueLine("할아버지",
            "…쉿. 벌써 하나가 들썩인다. 불상 앞에 앉아, 그쪽으로 손을 모아라. 어서!", 4.5f);
        [SerializeField]
        private PrologueLine retryLine = new PrologueLine("할아버지",
            "괜찮다, 아직은 내가 붙잡고 있다. 다시 — 소리 나는 쪽이다.", 3.5f);
        [SerializeField]
        private PrologueLine clearedLine = new PrologueLine("할아버지",
            "그렇지. 그렇게 막는 거다. 이제 혼자서도 할 수 있겠지.", 4f);

        private readonly PrologueTrainingModel _training = new PrologueTrainingModel();
        private Action _onComplete;
        private int _index;
        private float _timer;
        private bool _playing;
        private bool _inTraining;
        private float _clearedTimer;

        /// <summary>GameFlowController.EnterPrologue가 호출. 완료 콜백 = EnterMainLoop.</summary>
        public void Play(Action onComplete)
        {
            _onComplete = onComplete;
            _index = 0;
            _timer = 0f;
            _playing = lines != null && lines.Length > 0;
            if (!_playing)
            {
                Finish();
                return;
            }
            FireLine(_index);
        }

        private void Update()
        {
            if (!_playing) return;

            if (_inTraining)
            {
                TickTraining();
                return;
            }

            if (InputReader.InteractDown)
            {
                Debug.Log("[PROLOGUE] 스킵 (E) — 학습 구간도 함께 스킵");
                _training.Skip();
                Finish();
                return;
            }

            _timer += Time.deltaTime;
            if (_timer < lines[_index].duration + linePauseSec) return;

            _timer = 0f;
            _index++;
            if (_index >= lines.Length)
            {
                BeginTraining();
                return;
            }
            FireLine(_index);
        }

        // ---------- v0.5 §3 강제 학습 ----------

        /// <summary>봉인(대사·걸쇠) 직후 — 스크립트된 공격 1회. 여기서만은 실패해도 죽지 않는다.</summary>
        private void BeginTraining()
        {
            if (scheduler == null || config == null)
            {
                Debug.LogWarning("[PROLOGUE] scheduler/config 미배선 — 강제 학습 생략");
                Finish();
                return;
            }

            _inTraining = true;
            _training.Begin(trainingCorner);
            scheduler.BeginTraining(HandleTrainingResolved);
            FireDialogue("prologue-warn", warningLine);
            Debug.Log($"[PROLOGUE] 강제 학습 시작 — 목표 귀퉁이 {trainingCorner}");
        }

        private void TickTraining()
        {
            float dt = Time.deltaTime;

            if (_training.IsCleared)
            {
                // 통과 대사를 끝까지 들려준 뒤 본편으로 (대사가 잘려 나가면 인과가 안 남는다)
                _clearedTimer -= dt;
                if (_clearedTimer <= 0f) Finish();
                return;
            }

            // 경고 대사는 끝까지 들려준 뒤 전조를 낸다 — 대사와 전조가 겹치면 "왜 죽었는지"가 안 남는다
            float warningSec = Mathf.Max(config.PrologueWarningSec, warningLine.duration + linePauseSec);
            TrainingCommand command = _training.Tick(dt, warningSec, config.PrologueRetryGapSec);
            if (command != TrainingCommand.FireTelegraph) return;

            FireDialogue(_training.Attempts > 1 ? "prologue-retry" : "prologue-telegraph",
                _training.Attempts > 1 ? retryLine : telegraphLine);
            scheduler.FireTrainingTelegraph(_training.TargetCorner,
                PrologueTrainingModel.TelegraphDuration(config.PrayerChannelSec, config.PrologueTelegraphTravelSec));
        }

        private void HandleTrainingResolved(int corner, bool countered)
        {
            _training.OnResolved(countered);
            if (!_training.IsCleared) return;

            scheduler.EndTraining();
            FireDialogue("prologue-clear", clearedLine);
            _clearedTimer = clearedLine.duration + linePauseSec;
            Debug.Log($"[PROLOGUE] 강제 학습 통과 — 시도 {_training.Attempts}회");
        }

        private void FireLine(int index) => FireDialogue($"prologue-{index}", lines[index]);

        private void FireDialogue(string id, PrologueLine line)
        {
            // 프롤로그 전용 EventDef 즉석 생성 — 자막·사운드가 본편과 같은 구독 경로를 탄다.
            // id의 "prologue-" prefix가 대화상자 담당 규약 (_shared.md).
            var def = new EventDef(id, PhaseId.P1, 0f, GameEventKind.Scripted, AudioChannel.Room,
                0f, false, new[] { new SubtitleLine(line.speaker, line.text, line.duration) });
            GameEvents.RaiseGameEventFired(def);
        }

        private void Finish()
        {
            if (!_playing && _onComplete == null) return;
            _playing = false;
            _inTraining = false;
            if (scheduler != null) scheduler.EndTraining(); // 스킵 경로 — 학습 전조가 떠 있어도 정리하고 나간다
            Action callback = _onComplete;
            _onComplete = null;
            Debug.Log("[PROLOGUE] 완료 — 본편 진입");
            callback?.Invoke();
        }
    }
}
