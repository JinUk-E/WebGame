using System;
using Morae.Game.Data;
using Morae.Game.Player;
using UnityEngine;

namespace Morae.Game.Core
{
    /// <summary>
    /// 프롤로그 연출 (명세 §4 — 규칙 학습, 컷 불가 항목. D3).
    /// 할아버지 대면 대사를 한 줄씩 GameEventFired로 발화 — 대화상자는 DialogueBoxView, 음성은 SoundManager가
    /// 기존 구독 경로로 소화한다. 마지막 줄 후 걸쇠 잠금 = 완료 콜백 → GameFlow가 본편 진입.
    ///
    /// <para>
    /// <b>[2026-08-06] 대사는 시간이 아니라 입력으로 넘어간다</b> — 클릭 / 터치 탭 / E · Space.
    /// 판정은 순수 모델 <see cref="DialogueAdvanceModel"/>이 소유하고(최소 표시 시간 = 연타 방어),
    /// 여기서는 입력 수집과 발화만 한다. 대사 줄 id는 <c>prologue-line-N</c> — 대화상자가 이 접두사를
    /// <b>수동 진행 줄</b>로 보고 자동 넘김을 끈다 (학습 구간 대사 <c>prologue-warn</c> 등은 기존 자동 그대로).
    /// </para>
    /// <para>
    /// <b>입력 소유권</b> — 구간마다 정확히 한 주인만 둔다:
    ///   ① 대사 구간: 진행 입력은 여기가 독점하고, 그동안 월드 상호작용은 잠긴다
    ///      (GameFlowController.PrologueDialogueLock → PlayerInteraction 게이트).
    ///   ② 강제 학습 구간: 진행 입력을 아예 읽지 않는다 — E·터치 버튼은 기도(PlayerInteraction)의 것이다.
    ///   ③ 스킵: 두 구간 모두에서 <b>Esc 또는 화면 우상단 스킵 영역 탭</b>으로만. 진행 입력과 겹치지 않는다.
    ///      (2026-08-06 이전에는 E 탭 = 스킵이었다 — 이제 E는 진행이다.)
    /// </para>
    ///
    /// v0.5 §3 — 대사 다음에 **강제 학습 구간**이 붙는다. 규칙을 텍스트가 아니라 1회 실행으로 가르친다:
    ///   할아버지의 경고("소금이 검어지면 그쪽으로 길이 열린다") → 전조 → 불상 앞 방향 기도로 상쇄 → 통과.
    ///   실패해도 사망하지 않고 재시도한다 (AttackScheduler 학습 모드 = 오염·이성 감소 없음).
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
        // duration은 이제 "표시 시간"이 아니라 **음성·연출 길이 힌트**다 (대화상자는 입력을 기다린다).
        // 학습 구간 대사(warning/telegraph/retry/cleared)에서는 여전히 표시 시간으로 쓰인다.
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
        // 막는 법 — 경고(인과) 다음, **전조 전**에 온다. 전조가 뜬 뒤에 읽히면 이미 손이 늦다.
        // ⚠ 대사에 키 이름을 넣지 말 것 — 할아버지가 게임 시스템을 아는 존재가 되면 몰입이 깨진다.
        //   조작(시스템 채널)은 이 대사와 같은 순간에 뜨는 **키캡 UI**(PrayerHintView)가 맡는다.
        [SerializeField]
        private PrologueLine controlHintLine = new PrologueLine("할아버지",
            "막는 법은 이렇다. 불상 앞에서 빌어라. 소금이 검어진 쪽을 향해서.", 4.5f);
        [SerializeField]
        private PrologueLine telegraphLine = new PrologueLine("할아버지",
            "…쉿. 벌써 하나가 들썩인다. 불상 앞에 앉아, 그쪽으로 손을 모아라. 어서!", 4.5f);
        // 재시도 — 벌 없이 다시 (조작은 키캡 UI가 계속 떠 있으므로 대사는 방향만 짚어준다)
        [SerializeField]
        private PrologueLine retryLine = new PrologueLine("할아버지",
            "괜찮다, 아직은 내가 붙잡고 있다. 다시 — 소리 나는 쪽으로 빌어라.", 3.5f);
        [SerializeField]
        private PrologueLine clearedLine = new PrologueLine("할아버지",
            "그렇지. 그렇게 막는 거다. 이제 혼자서도 할 수 있겠지.", 4f);
        // 시도 상한에 걸려 넘어갈 때 — 규칙을 다시 한 번 문장으로 남긴다 (배우지 못한 채 본편에 들어가므로)
        [SerializeField]
        private PrologueLine mercyLine = new PrologueLine("할아버지",
            "…이번엔 내가 막았다. 다음엔 네가 해야 해. 불상 앞에 앉아, 검어진 쪽으로 손을 모으는 거다.", 5f);

        [Header("대사 수동 진행 (2026-08-06)")]
        // 스킵 영역 = 화면 우상단 (뷰포트 비율). Art2Setup이 같은 자리에 "건너뛰기" 라벨을 놓는다 —
        // 라벨 위치를 옮기면 이 사각형도 함께 옮길 것 (표시와 판정이 어긋나면 "안 눌리는 버튼"이 된다).
        [SerializeField] private Rect skipZoneViewport = new Rect(0.80f, 0.88f, 0.20f, 0.12f);
        // config 미배선 시의 최소 표시 시간 폴백 (배선돼 있으면 BalanceConfig.PrologueLineMinShowSec)
        [SerializeField] private float fallbackLineMinShowSec = 0.3f;
        // 마지막 줄을 넘긴 **그 입력**이 학습 구간의 기도로 이어지지 않게 하는 인계 유예.
        // 같은 프레임의 Update 순서는 보장되지 않는다 — PlayerInteraction이 뒤에 돌면 아직 눌린 E를 기도로 읽는다.
        [SerializeField] private float inputHandoffGraceSec = 0.2f;

        private readonly PrologueTrainingModel _training = new PrologueTrainingModel();
        private readonly DialogueAdvanceModel _dialogue = new DialogueAdvanceModel();
        private Action _onComplete;
        private bool _playing;
        private bool _inTraining;
        private float _clearedTimer;
        private float _warningTimer; // 경고 대사 → 조작 안내 발화까지 (학습 구간 전용)
        private bool _hintFired;
        private float _handoffTimer;

        /// <summary>
        /// 대사 구간이 입력을 쥐고 있는가 — GameFlowController가 상호작용 게이트에 쓴다.
        /// 학습 구간에서는 false다 (그때 E·터치 버튼은 기도의 것) — 단, 인계 유예 동안은 잠시 더 잡고 있는다.
        /// </summary>
        public bool OwnsInput => (_playing && !_inTraining && _dialogue.IsActive) || _handoffTimer > 0f;

        private float LineMinShowSec => config != null ? config.PrologueLineMinShowSec : fallbackLineMinShowSec;

        /// <summary>GameFlowController.EnterPrologue가 호출. 완료 콜백 = EnterMainLoop.</summary>
        public void Play(Action onComplete)
        {
            _onComplete = onComplete;
            _inTraining = false;
            _clearedTimer = 0f;
            _warningTimer = 0f;
            _hintFired = false;
            _training.Reset(); // Begin은 NotStarted에서만 먹는다 — 리셋 없이 재생하면 학습이 조용히 건너뛰어진다
            _dialogue.Begin(lines != null ? lines.Length : 0);
            _playing = _dialogue.IsActive;
            if (!_playing)
            {
                Finish();
                return;
            }
            FireLine(_dialogue.Index);
        }

        private void Update()
        {
            // 인계 유예는 대사가 끝난 뒤에도 흘러야 한다 — _playing 가드보다 앞 (뒤에 두면 게이트가 영구히 잠긴다)
            if (_handoffTimer > 0f) _handoffTimer -= Time.deltaTime;
            if (!_playing) return;

            // 포인터는 프레임당 한 번만 해석한다 — 스킵 영역이면 스킵, 아니면 진행 후보.
            bool pointerDown = InputReader.PointerDown(out Vector2 pointerPos);
            bool skipZoneHit = pointerDown && DialogueAdvanceModel.InViewportZone(
                pointerPos, Screen.width, Screen.height, skipZoneViewport);

            if (skipZoneHit || InputReader.EscapeDown)
            {
                Debug.Log($"[PROLOGUE] 스킵 ({(skipZoneHit ? "스킵 영역 탭" : "Esc")}) — 학습 구간도 함께 스킵");
                _dialogue.Stop();
                _training.Skip();
                Finish();
                return;
            }

            if (_inTraining)
            {
                // 학습 구간은 진행 입력을 읽지 않는다 — E·탭은 기도의 것이다 (입력 소유권 분리)
                TickTraining();
                return;
            }

            bool advance = (pointerDown && !skipZoneHit) || InputReader.AdvanceKeyDown;
            switch (_dialogue.Step(Time.deltaTime, advance, LineMinShowSec))
            {
                case DialogueCommand.ShowLine:
                    FireLine(_dialogue.Index);
                    break;
                case DialogueCommand.Finish:
                    BeginTraining();
                    break;
            }
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
            _handoffTimer = inputHandoffGraceSec; // 마지막 줄을 넘긴 입력이 그대로 기도로 넘어가지 않게
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

            // 경고(인과) → 조작 안내 → 전조. 조작 안내는 전조 전에 끝나야 한다 — 전조가 뜬 뒤에 읽으면 이미 늦다.
            if (!_hintFired && _training.Step == TrainingStep.Warning)
            {
                _warningTimer += dt;
                if (_warningTimer >= warningLine.duration + linePauseSec)
                {
                    _hintFired = true;
                    // 이 id가 키캡 힌트(PrayerHintView)를 띄우는 신호이기도 하다 — 대사(픽션)와 조작(시스템)이
                    // 같은 순간에 다른 채널로 나간다.
                    FireDialogue("prologue-controls", controlHintLine);
                }
            }

            // 경고 대사는 끝까지 들려준 뒤 전조를 낸다 — 대사와 전조가 겹치면 "왜 죽었는지"가 안 남는다
            float warningSec = Mathf.Max(config.PrologueWarningSec,
                warningLine.duration + controlHintLine.duration + linePauseSec * 2f);
            TrainingCommand command = _training.Tick(dt, warningSec, config.PrologueRetryGapSec);
            if (command != TrainingCommand.FireTelegraph) return;

            // 재시도 대사는 실패 직후(재시도 간격 시작)에 이미 나갔다 — 여기서는 첫 전조 안내만.
            if (_training.Attempts <= 1) FireDialogue("prologue-telegraph", telegraphLine);
            scheduler.FireTrainingTelegraph(_training.TargetCorner,
                PrologueTrainingModel.TelegraphDuration(config.PrayerChannelSec, config.PrologueTelegraphTravelSec));
        }

        private void HandleTrainingResolved(int corner, bool countered)
        {
            _training.OnResolved(countered, config.PrologueMaxAttempts);
            if (!_training.IsCleared)
            {
                // 실패 → 재시도 간격 시작. 조작을 다시 상기시키며, 다음 전조까지 읽을 시간을 준다
                // (예전에는 이 대사가 다음 전조와 같은 프레임에 떠서 읽을 틈이 없었다).
                FireDialogue("prologue-retry", retryLine);
                return;
            }

            scheduler.EndTraining();
            PrologueLine line = _training.ClearedByMercy ? mercyLine : clearedLine;
            FireDialogue(_training.ClearedByMercy ? "prologue-mercy" : "prologue-clear", line);
            _clearedTimer = line.duration + linePauseSec;
            Debug.Log($"[PROLOGUE] 강제 학습 통과 — 시도 {_training.Attempts}회" +
                      (_training.ClearedByMercy ? " (시도 상한 자비 통과 — 상쇄 성공 아님)" : ""));
        }

        /// <summary>
        /// 대사 줄 발화. id는 <c>prologue-line-N</c> — "line" 마디가 <b>수동 진행 줄</b> 표식이다
        /// (DialogueBoxView가 이 접두사에서만 자동 넘김을 끄고 ▼를 띄운다). 학습 구간 대사는 이 마디가 없다.
        /// </summary>
        private void FireLine(int index) => FireDialogue($"prologue-line-{index}", lines[index]);

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
            _dialogue.Stop(); // 진행 입력이 본편으로 새지 않게 — 게이트를 닫고 나간다
            if (scheduler != null) scheduler.EndTraining(); // 스킵 경로 — 학습 전조가 떠 있어도 정리하고 나간다
            Action callback = _onComplete;
            _onComplete = null;
            Debug.Log("[PROLOGUE] 완료 — 본편 진입");
            callback?.Invoke();
        }
    }
}
