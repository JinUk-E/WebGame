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

        private Action _onComplete;
        private int _index;
        private float _timer;
        private bool _playing;

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

            if (InputReader.InteractDown)
            {
                Debug.Log("[PROLOGUE] 스킵 (E)");
                Finish();
                return;
            }

            _timer += Time.deltaTime;
            if (_timer < lines[_index].duration + linePauseSec) return;

            _timer = 0f;
            _index++;
            if (_index >= lines.Length)
            {
                Finish();
                return;
            }
            FireLine(_index);
        }

        private void FireLine(int index)
        {
            PrologueLine line = lines[index];
            // 프롤로그 전용 EventDef 즉석 생성 — 자막·사운드가 본편과 같은 구독 경로를 탄다
            var def = new EventDef($"prologue-{index}", PhaseId.P1, 0f, GameEventKind.Scripted, AudioChannel.Room,
                0f, false, new[] { new SubtitleLine(line.speaker, line.text, line.duration) });
            GameEvents.RaiseGameEventFired(def);
        }

        private void Finish()
        {
            if (!_playing && _onComplete == null) return;
            _playing = false;
            Action callback = _onComplete;
            _onComplete = null;
            Debug.Log("[PROLOGUE] 완료 — 본편 진입");
            callback?.Invoke();
        }
    }
}
