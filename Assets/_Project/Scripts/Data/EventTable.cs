using System;
using UnityEngine;

namespace Morae.Game.Data
{
    /// <summary>자막 1줄 (architecture §2.3).</summary>
    [Serializable]
    public sealed class SubtitleLine
    {
        [SerializeField] private string speaker = "???";
        [SerializeField] private string text;
        [SerializeField] private float duration;

        public string Speaker => speaker;
        public string Text => text;
        public float Duration => duration;

        public SubtitleLine() { }

        public SubtitleLine(string speaker, string text, float duration)
        {
            this.speaker = speaker;
            this.text = text;
            this.duration = duration;
        }
    }

    /// <summary>
    /// 신호·자막·연출 이벤트 1행 (architecture §2.3).
    /// 비공격 시간 이벤트 전부 — 가짜 목소리·노크·실루엣·요의·진짜 신호·K씨 개문.
    /// Scripted 훅은 id 스위치 한 곳만 사용: "urge"(요의), "rescue-open"(07:40 K씨 개문).
    /// </summary>
    [Serializable]
    public sealed class EventDef
    {
        [SerializeField] private string id;
        [SerializeField] private PhaseId phaseId;
        [SerializeField] private float offset;               // 페이즈 시작 기준(s), 지터 없음 — 연출 고정.
                                                             // 마지막 페이즈(P7)는 종료하지 않으므로 duration 초과 offset 허용 (rescue-open 60)
        [SerializeField] private GameEventKind kind;
        [SerializeField] private AudioClip audioClip;
        [SerializeField] private AudioChannel channel;
        [SerializeField] private AudioClip audioClipMuffled; // Door 채널만 — 사전 가공 뭉갬 2벌 (§7.2). null = 단독 재생
        [SerializeField] private SubtitleLine[] subtitleLines;
        [SerializeField] private SubtitleLine[] detailLines; // 귀 대기 중에만 표시되는 상세 자막 (TV 켜짐 시 불가)
        [SerializeField] private float sanityDelta;          // 공포 연출 −10 등. 0 허용
        [SerializeField] private bool isTrueSignal;          // 유일하게 1행 true → TrueSignalStarted

        public string Id => id;
        public PhaseId PhaseId => phaseId;
        public float Offset => offset;
        public GameEventKind Kind => kind;
        public AudioClip AudioClip => audioClip;
        public AudioChannel Channel => channel;
        public AudioClip AudioClipMuffled => audioClipMuffled;
        public SubtitleLine[] SubtitleLines => subtitleLines;
        public SubtitleLine[] DetailLines => detailLines;
        public float SanityDelta => sanityDelta;
        public bool IsTrueSignal => isTrueSignal;

        public EventDef() { }

        /// <summary>에디터 빌더·EditMode 테스트용 생성자. 오디오 클립은 수급 후 배선.</summary>
        public EventDef(string id, PhaseId phaseId, float offset, GameEventKind kind, AudioChannel channel,
            float sanityDelta, bool isTrueSignal, SubtitleLine[] subtitleLines, SubtitleLine[] detailLines = null,
            AudioClip audioClip = null, AudioClip audioClipMuffled = null)
        {
            this.id = id;
            this.phaseId = phaseId;
            this.offset = offset;
            this.kind = kind;
            this.channel = channel;
            this.sanityDelta = sanityDelta;
            this.isTrueSignal = isTrueSignal;
            this.subtitleLines = subtitleLines;
            this.detailLines = detailLines;
            this.audioClip = audioClip;
            this.audioClipMuffled = audioClipMuffled;
        }
    }

    /// <summary>이벤트 테이블 (architecture §2.3). 런타임 읽기 전용.</summary>
    [CreateAssetMenu(menuName = "Morae/Event Table", fileName = "EventTable")]
    public sealed class EventTable : ScriptableObject
    {
        [SerializeField] private EventDef[] events;

        public int Count => events != null ? events.Length : 0;
        public EventDef GetEvent(int index) => events[index];

#if UNITY_EDITOR
        /// <summary>에디터 빌더 전용 — 런타임 호출 금지.</summary>
        public void EditorSetEvents(EventDef[] value) => events = value;
#endif
    }
}
