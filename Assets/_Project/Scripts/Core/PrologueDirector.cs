using System;
using Morae.Game.Data;
using Morae.Game.Player;
using UnityEngine;

namespace Morae.Game.Core
{
    /// <summary>
    /// 프롤로그 연출 (명세 §4 — 규칙 학습, 컷 불가 항목. D3).
    /// 할아버지 대면 대사를 한 줄씩 GameEventFired로 발화 — 대화상자는 DialogueBoxView, 음성은 SoundManager가
    /// 기존 구독 경로로 소화한다.
    ///
    /// <para>
    /// <b>[2026-08-06] 순서: 대사 → 학습 → 걸쇠</b>. 예전에는 걸쇠를 잠근 **뒤에** 학습 대사가 나가서,
    /// "문이 잠긴 뒤 문밖 목소리는 전부 의심하라"는 규칙을 세운 바로 그 사람이 잠긴 문 밖에서 계속 가르치고 있었다.
    /// 이제 마지막 <c>linesAfterTraining</c>줄(걸쇠 잠금)은 학습이 끝난 뒤에 재생한다 —
    /// 할아버지는 아직 방에 있을 때 가르치고, 걸쇠가 튜토리얼의 끝이자 본편의 시작이 된다.
    /// 본편에서 조언이 필요하면 화자는 손자(속마음)여야 한다 — RecoveryHintDirector 참조.
    /// </para>
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
            // 규칙 나열이 아니라 손자와의 대화로 — 정보(소금 넷·부적 1회·문밖 목소리·시계 불신·07:30)는 그대로 유지한다.
            new PrologueLine("할아버지", "…아까 담장 위에서 본 걸 말해봐라. 키가 2미터는 되고, 흰 원피스에 '포포포' 소리를 냈다고 했냐?", 5.0f),
            new PrologueLine("나", "네, 할아버지… 남자가 여장한 건가 싶어서 말씀드린 건데, 왜 그렇게 무섭게 노발대발하세요?", 4.5f),
            new PrologueLine("할아버지", "쯧, 큰일 났구나… 그건 사람이 아니다. 이 동네에 봉인되어 있던 '팔척님'이야.", 5.0f),
            new PrologueLine("나", "팔척님요…? 그게 뭔데요? 할머니는 왜 옆에서 떨고 계세요?", 3.5f),
            new PrologueLine("할아버지", "그놈 마음에 들면 이튿날을 못 넘기고 죽는다! 오늘은 절대 집에 돌아갈 생각 마라.", 5.0f),
            new PrologueLine("할아버지", "이 방 창문은 신문지와 부적으로 막았고, 네 귀퉁이에 소금을 쌓았다. 부적은 딱 한 번 널 대신해 재앙을 막아줄 게다.", 5.5f),
            new PrologueLine("나", "그럼 전 아침까지 이 방에 갇혀 있어야 하는 거예요?", 3.5f),
            new PrologueLine("할아버지", "그래. 문밖에서 내 목소리가 들려도 절대 열지 마라. 그놈은 사람 목소리를 훔쳐 유혹하는 놈이다.", 5.5f),
            new PrologueLine("할아버지", "시계나 소리는 믿지 말고, 창밖이 환하게 밝아오면 그때 일곱 시 반에 내가 직접 데리러 오마.", 5.5f),
            new PrologueLine("나", "(문이 닫히고 걸쇠를 잠갔다. 방 안에는 침묵과 기묘한 정적만이 남아있다.)", 4.0f),
        };

        [Header("강제 학습 (명세 v0.5 §3)")]
        // **학습은 걸쇠를 잠그기 전에 끝난다.** 게임의 핵심 규칙이 "문이 잠긴 뒤 문밖 목소리는 전부 의심하라"인데,
        // 잠근 뒤에도 할아버지가 밖에서 가르치고 있으면 규칙이 세워지자마자 스스로를 부정한다.
        // 그래서 마지막 N줄(기본 1줄 = 걸쇠 잠금)은 학습이 끝난 뒤에 재생한다 — 걸쇠가 튜토리얼의 끝이자 본편의 시작이 된다.
        [SerializeField] private int linesAfterTraining = 1;
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

        [Header("소금 주의 유도 (v0.6)")]
        // "네 귀퉁이에 소금을 쌓았다" 줄에서 소금 넷을 순서대로 반짝인다 — 말과 화면이 같은 순간에 같은 것을
        // 가리켜야 대사가 길어진 게 손해가 아니라 이득이 된다. -1이면 비활성.
        [SerializeField] private int saltCueLineIndex = 5;
        // 학습 구간에서 불상 후광을 대사보다 조금 더 오래 남긴다 (대사를 넣은 뒤 움직이기까지의 간격)
        [SerializeField] private float altarHighlightExtraSec = 2.5f;
        [SerializeField] private float saltCueStepSec = 0.32f;   // 귀퉁이 간 간격
        [SerializeField] private float saltCueHoldSec = 0.55f;   // 한 귀퉁이가 빛나는 시간

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
        private int _saltCueNext = -1;   // 다음에 반짝일 귀퉁이 (-1 = 진행 중 아님)
        private float _saltCueTimer;
        private int _lineOffset;         // 현재 대사 구간이 lines 배열의 어디서 시작하는가
        private bool _tailPhase;         // 학습 뒤 꼬리 대사(걸쇠 잠금) 재생 중

        /// <summary>학습 **전에** 재생할 줄 수. 나머지는 학습이 끝난 뒤에 나간다.</summary>
        private int HeadCount
        {
            get
            {
                int total = lines != null ? lines.Length : 0;
                return Mathf.Max(0, total - Mathf.Clamp(linesAfterTraining, 0, total));
            }
        }

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
            _lineOffset = 0;
            _tailPhase = false;
            _dialogue.Begin(HeadCount);
            _playing = true;   // 대사가 0줄이어도 학습 구간은 돌아야 한다
            if (!_dialogue.IsActive)
            {
                BeginTraining();
                return;
            }
            FireLine(_dialogue.Index);
        }

        private void Update()
        {
            // 인계 유예는 대사가 끝난 뒤에도 흘러야 한다 — _playing 가드보다 앞 (뒤에 두면 게이트가 영구히 잠긴다)
            if (_handoffTimer > 0f) _handoffTimer -= Time.deltaTime;
            TickSaltCue();
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

            // 인계 유예 동안은 진행 입력을 안 읽는다 — 기도로 눌린 E가 그대로 다음 대사를 넘기지 않게
            bool advance = _handoffTimer <= 0f
                           && ((pointerDown && !skipZoneHit) || InputReader.AdvanceKeyDown);
            switch (_dialogue.Step(Time.deltaTime, advance, LineMinShowSec))
            {
                case DialogueCommand.ShowLine:
                    FireLine(_dialogue.Index);
                    break;
                case DialogueCommand.Finish:
                    if (_tailPhase) Finish();   // 걸쇠까지 잠갔다 — 본편으로
                    else BeginTraining();
                    break;
            }
        }

        // ---------- v0.5 §3 강제 학습 ----------

        /// <summary>
        /// 걸쇠를 잠그기 **전** — 할아버지가 아직 방에 있을 때 스크립트된 공격 1회. 여기서만은 실패해도 죽지 않는다.
        /// (학습이 끝나야 마지막 줄 "문이 닫히고 걸쇠를 잠갔다"가 나간다 — <see cref="BeginTailLines"/>)
        /// </summary>
        private void BeginTraining()
        {
            if (scheduler == null || config == null)
            {
                Debug.LogWarning("[PROLOGUE] scheduler/config 미배선 — 강제 학습 생략");
                BeginTailLines();   // 학습을 건너뛰더라도 걸쇠 대사는 남긴다
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
                // 통과 대사를 끝까지 들려준 뒤 걸쇠 대사로 (대사가 잘려 나가면 인과가 안 남는다)
                _clearedTimer -= dt;
                if (_clearedTimer <= 0f) BeginTailLines();
                return;
            }

            // 경고(인과) → 조작 안내 → 전조. 조작 안내는 전조 전에 끝나야 한다 — 전조가 뜬 뒤에 읽으면 이미 늦다.
            if (!_hintFired && _training.Step == TrainingStep.Warning)
            {
                _warningTimer += dt;
                if (_warningTimer >= warningLine.duration + linePauseSec)
                {
                    _hintFired = true;
                    // 대사가 "불상 앞에서 빌어라"고 말하는 그 순간, 화면도 그 불상을 가리킨다 —
                    // 처음 온 플레이어는 불상이 화면 어느 물건인지 모른 채로 문장만 듣게 된다.
                    GameEvents.RaiseAltarAttentionRequested(controlHintLine.duration + altarHighlightExtraSec);
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
            // 전조가 뜼는 순간에도 한 번 더 — "지금 저기로 가라"가 행동 직전에 반복되어야 손이 움직인다
            GameEvents.RaiseAltarAttentionRequested(telegraphLine.duration + altarHighlightExtraSec);
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
        private void FireLine(int index)
        {
            int absolute = _lineOffset + index;
            // id는 **절대 인덱스**로 만든다 — 꼬리 구간에서 0부터 다시 시작하면 앞 줄과 id가 겹친다
            FireDialogue($"prologue-line-{absolute}", lines[absolute]);
            if (absolute == saltCueLineIndex) StartSaltCue();
        }

        /// <summary>
        /// 학습 통과 후 남은 대사(걸쇠 잠금)를 재생한다. 남은 줄이 없으면 바로 본편으로.
        /// 여기서 인계 유예를 다시 거는 이유: 상쇄에 쓴 그 E가 같은 프레임에 대사를 넘겨버리기 때문이다.
        /// </summary>
        private void BeginTailLines()
        {
            _inTraining = false;
            int tail = (lines != null ? lines.Length : 0) - HeadCount;
            if (tail <= 0)
            {
                Finish();
                return;
            }

            _tailPhase = true;
            _lineOffset = HeadCount;
            _handoffTimer = inputHandoffGraceSec;
            _dialogue.Begin(tail);
            if (!_dialogue.IsActive)
            {
                Finish();
                return;
            }
            _playing = true;
            FireLine(_dialogue.Index);
        }

        /// <summary>소금 넷을 0→1→2→3 순서로 반짝인다. 줄을 빨리 넘겨도 남은 반짝임은 그대로 끝난다.</summary>
        private void StartSaltCue()
        {
            _saltCueNext = 0;
            _saltCueTimer = 0f;
        }

        private void TickSaltCue()
        {
            if (_saltCueNext < 0) return;
            _saltCueTimer -= Time.deltaTime;
            if (_saltCueTimer > 0f) return;

            GameEvents.RaiseSaltAttentionRequested(_saltCueNext, saltCueHoldSec);
            _saltCueNext++;
            _saltCueTimer = saltCueStepSec;
            if (_saltCueNext >= CornerIndex.Count) _saltCueNext = -1;
        }

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
