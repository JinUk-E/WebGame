using Morae.Game.Data;
using UnityEngine;

namespace Morae.Game.Core
{
    /// <summary>
    /// 손자의 속마음 안내 — 세 가지를 맡는다 (v0.7).
    /// <list type="number">
    ///   <item><b>오염 안내</b>: 소금이 더러워질 때마다 "다시 뿌려야 한다"를 반복한다.</item>
    ///   <item><b>무효 정화</b>: 깨끗한 귀퉁이에 뿌리기를 마치면 "여긴 이미 깨끗해"로 닫아준다.</item>
    ///   <item><b>이불 유도</b>: 이성이 임계 아래로 떨어지면 주기적으로 "숨으면 나아질 것 같다"를 말한다.</item>
    /// </list>
    ///
    /// <para>
    /// <b>왜 반복하는가.</b> v0.6까지 오염 안내는 <b>단 1회</b>였다. 한 번 듣고 흘려보내면 다시는 안 나오는데,
    /// 정작 그 인과("검어졌다 → 새로 뿌린다")가 이 게임에서 제일 늦게 학습되는 것이다.
    /// 매번 말하되 <see cref="repeatCooldownSec"/>로 도배는 막는다 — 연속 오염 때 같은 문장이 겹치면 소음이 된다.
    /// </para>
    ///
    /// <para>
    /// <b>화자는 반드시 손자(속마음)다.</b> 문이 잠긴 뒤 문밖 목소리는 전부 의심하라는 게 이 게임의 규칙인데,
    /// 할아버지가 본편에서 조언을 하면 규칙이 스스로를 부정한다. 그래서 id에 <c>prologue-</c> 접두사를 쓰지 않아
    /// 대화상자(초상+화자명)가 아니라 자막으로 나간다 — 밖에서 온 말이 아니라 안에서 든 생각이라는 표시다.
    /// </para>
    /// </summary>
    public sealed class RecoveryHintDirector : MonoBehaviour
    {
        [Header("오염 안내 (매 오염마다)")]
        [SerializeField, TextArea]
        private string contaminatedText = "(소금이 검어졌다. 저기에 새 소금을 다시 뿌려야 해.)";
        [SerializeField] private float contaminatedDuration = 3.2f;
        [SerializeField] private float delaySec = 1.2f;              // 오염 연출(소리·색)이 먼저 도착하도록 한 박자 늦춘다
        [SerializeField] private float repeatCooldownSec = 9f;       // 같은 문장이 겹쳐 도배되지 않게

        [Header("무효 정화 (깨끗한 귀퉁이에 뿌리기 완료)")]
        // 소금이 상시 상호작용이 되면서 생긴 경우 — 효과 없이 끝났다는 걸 말로 닫아줘야
        // "뿌렸는데 왜 아무 일도 없지"가 버그가 아니라 내 판단 착오로 읽힌다.
        [SerializeField, TextArea]
        private string alreadyCleanText = "(여긴 이미 깨끗해.)";
        [SerializeField] private float alreadyCleanDuration = 2.5f;

        [Header("이불 유도 (이성 저하 시 주기)")]
        [SerializeField, TextArea]
        private string scaredText = "(무서워… 이불 속에 숨으면 좀 나아질 것 같아.)";
        [SerializeField] private float scaredDuration = 3.5f;
        [SerializeField, Range(0f, 1f)] private float scaredThreshold01 = 0.45f;  // 이성이 이 아래로 내려가면
        [SerializeField] private float scaredIntervalSec = 22f;                   // 이 간격으로 되뇐다

        private bool _training;
        private float _contaminatedTimer = -1f;
        private float _lastContaminatedAt = -999f;
        private float _sanity01 = 1f;
        private float _lastScaredAt = -999f;
        private bool _inBlanket;
        private bool _running;

        private void OnEnable()
        {
            GameEvents.CornerStageChanged += HandleCornerStage;
            GameEvents.SaltPurifyNoop += HandleSaltPurifyNoop;
            GameEvents.TrainingModeChanged += HandleTrainingMode;
            GameEvents.SanityChanged += HandleSanityChanged;
            GameEvents.PlayerStateChanged += HandlePlayerState;
            GameEvents.PhaseChanged += HandlePhaseChanged;
            GameEvents.GameOver += HandleStop;
            GameEvents.EndingStarted += HandleEndingStop;
        }

        private void OnDisable()
        {
            GameEvents.CornerStageChanged -= HandleCornerStage;
            GameEvents.SaltPurifyNoop -= HandleSaltPurifyNoop;
            GameEvents.TrainingModeChanged -= HandleTrainingMode;
            GameEvents.SanityChanged -= HandleSanityChanged;
            GameEvents.PlayerStateChanged -= HandlePlayerState;
            GameEvents.PhaseChanged -= HandlePhaseChanged;
            GameEvents.GameOver -= HandleStop;
            GameEvents.EndingStarted -= HandleEndingStop;
        }

        private void HandleTrainingMode(bool active) => _training = active;
        private void HandleSanityChanged(float s01) => _sanity01 = s01;
        private void HandlePlayerState(PlayerState state) => _inBlanket = state == PlayerState.InBlanket;
        private void HandlePhaseChanged(PhaseId phase) => _running = true;
        private void HandleStop(GameOverReason reason) => _running = false;
        private void HandleEndingStop(EndingKind kind) => _running = false;

        private void HandleCornerStage(int corner, int stage)
        {
            // 정화(단계 하락)에는 말하지 않는다 — 말할 이유가 생기는 건 더러워질 때뿐이다.
            // 학습 구간은 할아버지가 직접 가르치는 장면이라 속마음이 끼어들면 화자가 겹친다.
            if (_training || stage < (int)CornerStage.Gray) return;
            if (Time.time - _lastContaminatedAt < repeatCooldownSec) return;
            _lastContaminatedAt = Time.time;
            _contaminatedTimer = delaySec;
        }

        /// <summary>
        /// 깨끗한 귀퉁이에 뿌리기 완료 — 즉시 말한다. 오염 안내와 달리 지연이 없다:
        /// 이건 세계의 사건이 아니라 **방금 한 내 행동에 대한 반응**이라 한 박자 늦으면 인과가 끊긴다.
        /// 쿨다운도 없다 — 홀드 1.5초가 이미 자연 도배 방지다. 학습 구간 억제는 오염 안내와 같은 이유.
        /// </summary>
        private void HandleSaltPurifyNoop(int corner)
        {
            if (_training) return;
            Say("hint-already-clean", alreadyCleanText, alreadyCleanDuration);
        }

        private void Update()
        {
            TickContaminated();
            TickScared();
        }

        private void TickContaminated()
        {
            if (_contaminatedTimer < 0f) return;
            _contaminatedTimer -= Time.deltaTime;
            if (_contaminatedTimer > 0f) return;
            _contaminatedTimer = -1f;
            Say("hint-recovery", contaminatedText, contaminatedDuration);
        }

        /// <summary>
        /// 이성이 낮은 동안 주기적으로 이불을 떠올린다.
        /// <b>이불 안에서는 말하지 않는다</b> — 이미 숨어 있는데 숨으라고 하면 안내가 아니라 소음이다.
        /// </summary>
        private void TickScared()
        {
            if (!_running || _training || _inBlanket) return;
            if (_sanity01 > scaredThreshold01) return;
            if (Time.time - _lastScaredAt < scaredIntervalSec) return;
            _lastScaredAt = Time.time;
            Say("hint-scared", scaredText, scaredDuration);
        }

        /// <summary>화자 빈 문자열 = 속마음. 자막이 "화자: 내용"으로 찍지 않고 괄호 문장만 남긴다.</summary>
        private void Say(string id, string text, float duration)
        {
            if (string.IsNullOrEmpty(text)) return;
            var def = new EventDef(id, PhaseId.P1, 0f, GameEventKind.Scripted, AudioChannel.Room,
                0f, false, new[] { new SubtitleLine(string.Empty, text, duration) });
            GameEvents.RaiseGameEventFired(def);
            Debug.Log($"[HINT] {id}");
        }
    }
}
