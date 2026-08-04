using Morae.Game.Core;
using Morae.Game.Data;
using UnityEngine;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 전역 사운드 라우터 (표현 계층 — GameEvents 구독만, §1.2. 게임플레이 직접 참조 금지).
    /// 클립 슬롯은 Audio/ 폴더와 1:1 (BGM_01_Main → bgmMain …). 배선은 메뉴 Morae/Setup Sound Manager.
    /// AudioSource 2개(BGM 루프 / SFX 원샷)는 Awake에서 생성 — 씬 수정 최소화.
    /// WebGL은 첫 사용자 입력 전까지 브라우저가 오디오를 막는다 — Unity가 첫 클릭에 자동 재개 (§8.2).
    /// 상황 매핑:
    ///   P1 진입 = bgmMain + 문 걸어잠금 SFX / P4 = bgmNight[0] / P6 = bgmNight[1]
    ///   페이즈 전환 = 시계 SFX(시계 변조 순간) / 공격 전조·공포 이벤트 = fear 스팅어 랜덤
    ///   Door 채널 이벤트·걸쇠 시도 = door_try, 걸쇠 취소 = door_close
    ///   진짜 신호 = BGM 페이드아웃(정적) / 엔딩 = bgmEnding / 게임오버 = BGM 정지 + fear 스팅어
    /// </summary>
    public sealed class SoundManager : MonoBehaviour
    {
        [Header("BGM — Audio/ 폴더명 기준")]
        [SerializeField] private AudioClip bgmMain;      // BGM_01_Main: 본편 전반 (P1~P3)
        [SerializeField] private AudioClip bgmIntro;     // BGM_02_Intro: 타이틀/프롤로그 (TitleScreen 도입 대비)
        [SerializeField] private AudioClip[] bgmNight;   // BGM_03_Night: 후반 고조 (0 = P4~, 1 = P6~)
        [SerializeField] private AudioClip bgmEnding;    // BGM_04_Ending

        [Header("SFX")]
        [SerializeField] private AudioClip sfxClock;     // SFX_Clock: 페이즈 전환(시계 변조)
        [SerializeField] private AudioClip sfxDoorClose; // SFX_Door/door_close: 밤 시작 걸쇠·걸쇠 취소
        [SerializeField] private AudioClip sfxDoorTry;   // SFX_Door/door_try: 문 흔들림·걸쇠 개방 시도
        [SerializeField] private AudioClip[] sfxFear;    // SFX_Fear: 공포 스팅어 (랜덤 재생)

        [Header("볼륨·페이드")]
        [SerializeField] private float bgmVolume = 0.5f;
        [SerializeField] private float sfxVolume = 0.9f;
        [SerializeField] private float bgmFadeSec = 1.5f;

        private AudioSource _bgm;
        private AudioSource _sfx;
        private AudioClip _pending;   // 페이드아웃 완료 후 교체될 클립 (null = 정지)
        private bool _fadingOut;
        private float _lastLatch;

        private void Awake()
        {
            _bgm = gameObject.AddComponent<AudioSource>();
            _bgm.playOnAwake = false; // §8.2 오디오 게이트 전 재생 금지
            _bgm.loop = true;
            _bgm.spatialBlend = 0f;

            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _sfx.spatialBlend = 0f;
        }

        private void OnEnable()
        {
            GameEvents.PhaseChanged += HandlePhaseChanged;
            GameEvents.AttackTelegraphStarted += HandleTelegraph;
            GameEvents.GameEventFired += HandleGameEventFired;
            GameEvents.DoorLatchProgressChanged += HandleDoorLatch;
            GameEvents.TrueSignalStarted += HandleTrueSignal;
            GameEvents.EndingStarted += HandleEnding;
            GameEvents.GameOver += HandleGameOver;
        }

        private void OnDisable()
        {
            GameEvents.PhaseChanged -= HandlePhaseChanged;
            GameEvents.AttackTelegraphStarted -= HandleTelegraph;
            GameEvents.GameEventFired -= HandleGameEventFired;
            GameEvents.DoorLatchProgressChanged -= HandleDoorLatch;
            GameEvents.TrueSignalStarted -= HandleTrueSignal;
            GameEvents.EndingStarted -= HandleEnding;
            GameEvents.GameOver -= HandleGameOver;
        }

        private void Start()
        {
            // GameFlowController.Start가 먼저 돌아 P1이 이미 시작됐다면 bgmMain이 재생 중 — 덮어쓰지 않는다
            if (_bgm.clip == null) FadeTo(bgmIntro);
        }

        private void Update()
        {
            // F1 배속과 무관하게 실시간 페이드 (unscaled)
            float step = bgmFadeSec > 0f ? bgmVolume * Time.unscaledDeltaTime / bgmFadeSec : bgmVolume;
            if (_fadingOut)
            {
                _bgm.volume = Mathf.MoveTowards(_bgm.volume, 0f, step);
                if (_bgm.volume > 0f) return;
                _fadingOut = false;
                if (_pending != null)
                {
                    _bgm.clip = _pending;
                    _pending = null;
                    _bgm.Play();
                }
                else
                {
                    _bgm.Stop();
                }
            }
            else if (_bgm.isPlaying && _bgm.volume < bgmVolume)
            {
                _bgm.volume = Mathf.MoveTowards(_bgm.volume, bgmVolume, step);
            }
        }

        // ---------- BGM ----------

        private void HandlePhaseChanged(PhaseId phase)
        {
            switch (phase)
            {
                case PhaseId.P1:
                    FadeTo(bgmMain);
                    PlayOneShot(sfxDoorClose); // 밤 시작 — 걸쇠 잠금
                    break;
                case PhaseId.P4:
                    FadeTo(Pick(bgmNight, 0));
                    break;
                case PhaseId.P6:
                    FadeTo(Pick(bgmNight, 1));
                    break;
            }
            PlayOneShot(sfxClock); // 페이즈 전환 = 시계 변조 순간
        }

        private void HandleTrueSignal() => FadeOutBgm(); // 진짜 신호 — 음악이 걷히고 정적

        private void HandleEnding(EndingKind kind) => FadeTo(bgmEnding);

        private void HandleGameOver(GameOverReason reason)
        {
            StopBgm();
            PlayRandom(sfxFear);
        }

        // ---------- SFX ----------

        private void HandleTelegraph(int corner, float duration) => PlayRandom(sfxFear);

        private void HandleGameEventFired(EventDef def)
        {
            if (def.Kind == GameEventKind.TrueSignal) return; // 정적 연출 — BGM 페이드는 TrueSignalStarted에서
            if (def.AudioClip != null)
            {
                _sfx.PlayOneShot(def.AudioClip, sfxVolume); // 테이블에 클립이 배선되면 그것이 우선 (§2.3)
                return;
            }
            if (def.Channel == AudioChannel.Door) PlayOneShot(sfxDoorTry);
            else if (def.Kind == GameEventKind.Scare || def.Kind == GameEventKind.FakeVoice) PlayRandom(sfxFear);
        }

        private void HandleDoorLatch(float progress01)
        {
            if (progress01 > 0f && _lastLatch <= 0f) PlayOneShot(sfxDoorTry);            // 걸쇠 개방 시작
            else if (progress01 <= 0f && _lastLatch is > 0f and < 1f) PlayOneShot(sfxDoorClose); // 취소 — 다시 걸림
            _lastLatch = progress01;
        }

        // ---------- 내부 ----------

        private void FadeTo(AudioClip clip)
        {
            if (clip == null) return;
            if (_bgm.clip == clip && (_bgm.isPlaying || _pending == clip)) return;
            if (!_bgm.isPlaying)
            {
                _bgm.clip = clip;
                _bgm.volume = 0f;
                _bgm.Play();
                return;
            }
            _pending = clip;
            _fadingOut = true;
        }

        private void FadeOutBgm()
        {
            _pending = null;
            _fadingOut = true;
        }

        private void StopBgm()
        {
            _pending = null;
            _fadingOut = false;
            _bgm.Stop();
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (clip != null) _sfx.PlayOneShot(clip, sfxVolume);
        }

        private void PlayRandom(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return;
            PlayOneShot(clips[Random.Range(0, clips.Length)]);
        }

        private static AudioClip Pick(AudioClip[] clips, int index)
        {
            if (clips == null || clips.Length == 0) return null;
            return clips[Mathf.Min(index, clips.Length - 1)];
        }
    }
}
