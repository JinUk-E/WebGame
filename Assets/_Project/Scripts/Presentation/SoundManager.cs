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
    ///   P1 진입 = bgmMain + 문 걸어잠금 SFX / P5 절정 = bgmNight[0] / P7 정적 = bgmNight[1] (v0.3 8페이즈 리매핑)
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
        [SerializeField] private AudioClip sfxHeartbeat; // SFX_Heartbeat: 이성 저하 심박 루프 (§2 심박의 청각 절반)

        [Header("귀퉁이 속삭임 (명세 v0.5 §1 — 어느 쪽이 뚫렸는지 청각으로 상시 인지)")]
        [SerializeField] private AudioClip sfxCornerWhisper;               // SFX_Corner/whisper_loop (절차 생성 루프)
        [SerializeField] private BalanceConfig balance;                    // 단계별 볼륨 테이블 (읽기 전용 데이터)
        [SerializeField] private AudioSource[] cornerSources = new AudioSource[CornerIndex.Count];
        [SerializeField] private float cornerMaxDistance = 500f;           // 3D 정위만 쓰고 거리 감쇠는 사실상 끈다
        [SerializeField] private float cornerVolumeSmoothSec = 0.35f;
        // 리슨 상태별 배수 (아키텍처 v1.2 §7.2 볼륨 테이블 규약) — 이불 속에서는 소리가 멀어져야 한다.
        [SerializeField] private float cornerVolumeInBlanket = 0.4f;
        [SerializeField] private float cornerVolumeAtDoor = 0.7f;

        [Header("전조 두드림 (v0.6)")]
        [SerializeField] private AudioClip sfxKnock;            // SFX_Knock/knock (절차 생성)
        [SerializeField] private float knockVolume = 0.85f;
        // v0.6 방어 판정음 — 성공/실패를 대역과 결로 가른다.
        // 성공: 맑고 위로 열리는 금속성 / 실패: 탁하고 아래로 내려앉는 사람 목소리.
        // 둘 다 귀퉁이 소스에서 울려 — 어느 곳이 지켜졌고 어느 곳이 뚫렸는지까지 소리로 잡힌다.
        [SerializeField] private AudioClip sfxPurify;           // SFX_Purify/purify (절차 생성)
        [SerializeField] private AudioClip sfxPoppo;            // SFX_Poppo/poppo — 팔척님의 웃음
        [SerializeField] private float purifyVolume = 0.9f;
        [SerializeField] private float poppoVolume = 1f;
        // 함정 웨이브처럼 네 귀퉁이가 한꺼번에 뚫려도 웃음은 한 번만 — 겹치면 조롱이 아니라 소음이 된다
        [SerializeField] private float poppoMinIntervalSec = 0.8f;

        [Header("TV 잡음 (v0.6)")]
        // TV를 켰을 때 **그 근처에서만** 들리는 소리. 2D로 깔면 방 전체가 울려 거리감이 죽는다 —
        // TV 옆으로 가야 크게 들리는 게 이 소리의 존재 이유다 (이불 속·문 앞에서는 멀어진다).
        [SerializeField] private AudioClip sfxTvWhisper;        // SFX_TV/tv_whisper_loop (절차 생성)
        [SerializeField] private Transform tvAnchor;            // 소리가 나올 자리 (Room/TV)
        [SerializeField] private float tvWhisperVolume = 0.55f;
        [SerializeField] private float tvFadeSec = 0.6f;
        [SerializeField] private float tvMaxDistance = 14f;     // 귀퉁이와 달리 거리 감쇠를 살렸다

        [Header("볼륨·페이드")]
        [SerializeField] private float bgmVolume = 0.5f;
        [SerializeField] private float sfxVolume = 0.9f;
        [SerializeField] private float bgmFadeSec = 1.5f;

        private AudioSource _bgm;
        private AudioSource _sfx;
        private AudioSource _heart;   // 심박 루프 — 볼륨·피치를 이성으로 변조
        private AudioClip _pending;   // 페이드아웃 완료 후 교체될 클립 (null = 정지)
        private bool _fadingOut;
        private float _lastLatch;
        private float _fear;          // 1 − 이성(0~1)
        private bool _listening;      // 귀 대기 중 — Door 채널 선명/뭉갬 분기
        private bool _inBlanket;      // 이불 속 — 귀퉁이 속삭임도 멀어진다
        private readonly int[] _cornerStages = new int[CornerIndex.Count]; // v0.5 — 귀퉁이 속삭임 볼륨 근거
        private AudioSource[] _cornerOneShots;  // 두드림·판정음 전용 (속삭임 볼륨에 물들지 않게 분리)
        private AudioSource _tv;   // TV 잡음 루프 (tvAnchor 아래에 런타임 생성)
        private bool _tvOn;
        private int _knockCorner = -1;   // v0.6 두드림 진행 중인 귀퉁이 (-1 = 없음)
        private float _knockStart;
        private float _knockDuration;
        private int _knockDone;
        private float _lastPoppo = -99f;
        private bool _training;   // 학습 구간 — 실패해도 할아버지가 막아준다

        private void Awake()
        {
            _bgm = gameObject.AddComponent<AudioSource>();
            _bgm.playOnAwake = false; // §8.2 오디오 게이트 전 재생 금지
            _bgm.loop = true;
            _bgm.spatialBlend = 0f;

            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _sfx.spatialBlend = 0f;

            _heart = gameObject.AddComponent<AudioSource>();
            _heart.playOnAwake = false;
            _heart.loop = true;
            _heart.spatialBlend = 0f;
            _heart.volume = 0f;

            SetupCornerSources();
            SetupTvSource();
        }

        /// <summary>
        /// TV 잡음 소스 — TV 오브젝트 밑에 런타임으로 달아 좌표를 공유한다.
        /// 씬에 미리 넣지 않는 이유: 이 소스는 순전히 사운드의 구현 사항이라
        /// 씬에 드러나면 누군가 실수로 옮기거나 지울 수 있다 (귀퉁이 소스는 정위 검증 때문에 씬에 있다).
        /// </summary>
        private void SetupTvSource()
        {
            if (tvAnchor == null || sfxTvWhisper == null) return;
            var go = new GameObject("TvWhisperSource");
            go.transform.SetParent(tvAnchor, false);
            _tv = go.AddComponent<AudioSource>();
            _tv.clip = sfxTvWhisper;
            _tv.loop = true;
            _tv.playOnAwake = false;   // §8.2 오디오 게이트 — 첫 입력 전 재생 금지
            _tv.volume = 0f;
            _tv.spatialBlend = 1f;
            _tv.rolloffMode = AudioRolloffMode.Linear;
            _tv.dopplerLevel = 0f;
            _tv.spread = 0f;
            _tv.minDistance = 1.5f;
            _tv.maxDistance = tvMaxDistance;
        }

        /// <summary>TV 잡음 페이드 — 켜고 끄는 게 딸깍 끊기면 장난감처럼 들린다.</summary>
        private void UpdateTvWhisper()
        {
            if (_tv == null) return;
            float target = _tvOn ? tvWhisperVolume * sfxVolume : 0f;
            if (_tvOn && !_tv.isPlaying) _tv.Play();
            float step = tvFadeSec > 0f ? Time.unscaledDeltaTime / tvFadeSec : 1f;
            _tv.volume = Mathf.MoveTowards(_tv.volume, target, step);
            if (!_tvOn && _tv.volume <= 0f && _tv.isPlaying) _tv.Stop();
        }

        /// <summary>
        /// 귀퉁이 속삭임 4채널 — 씬의 Audio/CornerSource_* 를 그대로 쓰고, 볼륨 0 루프로 대기시킨다.
        /// 재생은 여기서 시작하지 않는다 (§8.2 오디오 게이트 — 첫 클릭 전 재생 금지). P1 진입에서 켠다.
        ///
        /// ⚠ 정위는 panStereo가 아니라 **3D 위치**로 한다. Unity WebGL의 오디오 바인딩에는
        /// `_JS_Sound_SetPosition`/`SetListenerPosition`은 있어도 **panStereo에 해당하는 바인딩이 없어**
        /// (build.framework.js 심볼 확인) 웹에서는 pan이 통째로 무시된다 — "어느 쪽이 뚫렸는지"가 사라진다.
        /// 대신 소스를 실제 귀퉁이 좌표에 두고(V05Setup) 거리 감쇠만 사실상 껐다 — 방향만 얻고 크기는 볼륨이 정한다.
        /// </summary>
        private void SetupCornerSources()
        {
            if (cornerSources == null) return;
            _cornerOneShots = new AudioSource[cornerSources.Length];
            for (int i = 0; i < cornerSources.Length; i++)
            {
                AudioSource src = cornerSources[i];
                if (src == null) continue;

                // 원샷 전용 짝 — 같은 오브젝트(=같은 좌표)에 붙여 정위는 공유하고 볼륨만 독립시킨다
                AudioSource one = src.gameObject.AddComponent<AudioSource>();
                one.playOnAwake = false;
                one.loop = false;
                one.volume = 1f;
                one.spatialBlend = 1f;
                one.rolloffMode = AudioRolloffMode.Linear;
                one.dopplerLevel = 0f;
                one.spread = 0f;
                one.minDistance = 1f;
                one.maxDistance = cornerMaxDistance;
                _cornerOneShots[i] = one;
                src.clip = sfxCornerWhisper;
                src.loop = true;
                src.playOnAwake = false;
                src.volume = 0f;
                src.spatialBlend = 1f;                       // 3D — 좌표로 정위
                src.rolloffMode = AudioRolloffMode.Linear;
                src.dopplerLevel = 0f;                       // 소스도 리스너도 안 움직인다
                src.spread = 0f;
                src.minDistance = 1f;
                src.maxDistance = cornerMaxDistance;         // 리스너까지 ~12유닛이라 감쇠는 무시할 수준
            }
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
            GameEvents.SanityChanged += HandleSanityChanged;
            GameEvents.PlayerStateChanged += HandlePlayerStateChanged;
            GameEvents.CornerStageChanged += HandleCornerStageChanged;
            GameEvents.AttackResolved += HandleAttackResolved;
            GameEvents.TrainingModeChanged += HandleTrainingMode;
            GameEvents.TVToggled += HandleTvToggled;
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
            GameEvents.SanityChanged -= HandleSanityChanged;
            GameEvents.PlayerStateChanged -= HandlePlayerStateChanged;
            GameEvents.CornerStageChanged -= HandleCornerStageChanged;
            GameEvents.AttackResolved -= HandleAttackResolved;
            GameEvents.TrainingModeChanged -= HandleTrainingMode;
            GameEvents.TVToggled -= HandleTvToggled;
        }

        private void HandleSanityChanged(float s01) => _fear = 1f - s01;

        private void HandleCornerStageChanged(int corner, int stage)
        {
            if (corner < 0 || corner >= _cornerStages.Length) return;
            _cornerStages[corner] = stage;
        }

        /// <summary>학습 구간 진입/이탈 — 여기서는 팔척님의 웃음을 막는 데만 쓴다.</summary>
        private void HandleTrainingMode(bool active) => _training = active;

        /// <summary>TV 전원 — 잡음 루프의 on/off 근거. 화면(TvScreenView)·조명과 같은 이벤트를 본다.</summary>
        private void HandleTvToggled(bool isOn) => _tvOn = isOn;

        private void HandlePlayerStateChanged(PlayerState state)
        {
            _listening = state == PlayerState.ListeningAtDoor || state == PlayerState.OpeningDoor;
            _inBlanket = state == PlayerState.InBlanket;
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

            // 심박 — 이성 30% 아래부터 서서히 올라와 빨라진다 (HeartView 시각 심박의 청각 짝)
            if (_heart.isPlaying)
            {
                float target = _fear <= 0.3f ? 0f : Mathf.Pow((_fear - 0.3f) / 0.7f, 1.4f) * 0.65f;
                _heart.volume = Mathf.MoveTowards(_heart.volume, target, Time.unscaledDeltaTime * 0.4f);
                _heart.pitch = 0.85f + 0.65f * _fear;
            }

            UpdateCornerWhispers();
            UpdateKnocks();
            UpdateTvWhisper();
        }

        /// <summary>
        /// v0.5 §1 — 귀퉁이별 속삭임 볼륨을 그 귀퉁이의 오염 단계로 결정한다 (0.35s 러프).
        /// 흑화된 방향에서만 소리가 커지므로, 화면을 안 봐도 "어느 쪽이 뚫렸는지"가 상시 들린다.
        /// </summary>
        private void UpdateCornerWhispers()
        {
            if (cornerSources == null || balance == null) return;
            float k = CornerPenaltyModel.SmoothFactor(Time.unscaledDeltaTime, cornerVolumeSmoothSec);
            // 리슨 상태 배수 — 이불 속/문 앞에서는 방 안 소리가 멀어진다 (§7.2 볼륨 테이블 규약)
            float listenScale = _inBlanket ? cornerVolumeInBlanket : _listening ? cornerVolumeAtDoor : 1f;

            for (int i = 0; i < cornerSources.Length; i++)
            {
                AudioSource src = cornerSources[i];
                if (src == null || !src.isPlaying) continue;
                float target = balance.GetCornerWhisperVolume(_cornerStages[i]) * sfxVolume * listenScale;
                src.volume = Mathf.Lerp(src.volume, target, k);
            }
        }

        /// <summary>P1 진입에서 호출 — 오디오 게이트 통과 후에야 루프를 돌린다 (볼륨 0으로 시작).</summary>
        private void StartCornerWhispers()
        {
            if (cornerSources == null || sfxCornerWhisper == null) return;
            for (int i = 0; i < cornerSources.Length; i++)
            {
                AudioSource src = cornerSources[i];
                if (src == null || src.isPlaying) continue;
                src.volume = 0f;
                src.Play();
                // 위상 분리는 Play() **후**에 — 클립이 아직 언로드 상태면 Play 전 time 설정이 무시돼
                // 4채널이 위상까지 겹쳐 한 덩어리로 울린다 (막으려던 바로 그 현상).
                src.time = i * 1.1f % Mathf.Max(0.1f, sfxCornerWhisper.length);
            }
        }

        private void StopCornerWhispers()
        {
            if (cornerSources == null) return;
            for (int i = 0; i < cornerSources.Length; i++)
            {
                if (cornerSources[i] != null) cornerSources[i].Stop();
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
                    if (sfxHeartbeat != null && !_heart.isPlaying)
                    {
                        _heart.clip = sfxHeartbeat; // 볼륨 0에서 시작 — 이성이 떨어져야 들린다
                        _heart.Play();
                    }
                    StartCornerWhispers(); // 볼륨 0에서 시작 — 소금이 더러워져야 들린다
                    break;
                case PhaseId.P5: // v0.3: 절정 진입 — 후반 고조 (구 P4 절정에 해당)
                    FadeTo(Pick(bgmNight, 0));
                    break;
                case PhaseId.P7: // v0.3: 정적 진입 (구 P6 정적에 해당)
                    FadeTo(Pick(bgmNight, 1));
                    break;
            }
            PlayOneShot(sfxClock); // 페이즈 전환 = 시계 변조 순간
        }

        private void HandleTrueSignal() => FadeOutBgm(); // 진짜 신호 — 음악이 걷히고 정적

        private void HandleEnding(EndingKind kind)
        {
            _heart.Stop(); // 아침 — 심장이 가라앉는다
            StopCornerWhispers();
            FadeTo(bgmEnding);
        }

        private void HandleGameOver(GameOverReason reason)
        {
            StopBgm();
            _heart.Stop();
            StopCornerWhispers();
            PlayRandom(sfxFear);
        }

        // ---------- SFX ----------

        private void HandleTelegraph(int corner, float duration)
        {
            PlayRandom(sfxFear);
            // v0.6 — 그 귀퉁이 **밖에서** 두드린다. 방향은 CornerSource 정위가 맡으므로
            // 화면을 안 보고 있어도 "어느 벽인지"가 소리만으로 잡힌다.
            _knockCorner = corner;
            _knockDuration = duration;
            _knockStart = Time.time;
            _knockDone = 0;
        }

        /// <summary>
        /// 방어 판정음 (v0.6). 성공은 그 자리가 닫혔다는 신호라 매번 울린다 —
        /// 기도가 먹혔는지를 화면 없이도 알 수 있어야 다음 귀퉁이로 손이 움직인다.
        /// 실패는 간격을 둔다 — 웃음은 드물수록 무섭다.
        /// </summary>
        private void HandleAttackResolved(int corner, bool countered)
        {
            if (countered)
            {
                PlayAtCorner(corner, sfxPurify, purifyVolume, 1f);
                return;
            }

            // 학습 구간의 실패는 "뚫린 것"이 아니다 — 할아버지가 붙잡고 있다고 말하는 장면에
            // 조롱이 섞이면 대사와 소리가 서로를 부정한다.
            if (_training) return;
            if (Time.time - _lastPoppo < poppoMinIntervalSec) return;
            _lastPoppo = Time.time;
            // 피치를 조금씩 달리 — 같은 파일이 반복되면 사람 소리가 기계음으로 들린다
            PlayAtCorner(corner, sfxPoppo, poppoVolume, 0.94f + Random.value * 0.1f);
        }

        /// <summary>
        /// 귀퉁이 위치에서 원샷을 울린다 (미배선이면 2D 폴백).
        ///
        /// ⚠ **속삭임 소스로 재생하면 안 된다.** <see cref="AudioSource.PlayOneShot"/>은 그 소스의 volume을 곱하는데,
        /// 속삭임 볼륨은 깨끗한 귀퉁이에서 0이다 — 즉 결계가 멀쩡할수록 두드림·판정음이 조용해지고,
        /// 백(0단계)에서는 아예 안 들린다. 그래서 같은 자리에 **볼륨 1 고정 원샷 전용 소스**를 따로 둔다.
        /// </summary>
        private void PlayAtCorner(int corner, AudioClip clip, float volume, float pitch)
        {
            if (clip == null) return;
            AudioSource src = _cornerOneShots != null && corner >= 0 && corner < _cornerOneShots.Length
                ? _cornerOneShots[corner]
                : null;
            if (src == null)
            {
                _sfx.PlayOneShot(clip, sfxVolume * volume);
                return;
            }
            src.pitch = pitch;
            src.PlayOneShot(clip, sfxVolume * volume);
        }

        /// <summary>
        /// 전조 두드림 재생. 박자는 <see cref="KnockRhythm"/> — CornerTelegraphView의 흔들림·어둠과
        /// **같은 함수**를 쓴다. 각자 타이머를 굴리면 소리와 그림이 어긋나 두드림이 벽을 때린 결과로 안 읽힌다.
        /// </summary>
        private void UpdateKnocks()
        {
            if (_knockCorner < 0 || sfxKnock == null) return;
            if (cornerSources == null || _knockCorner >= cornerSources.Length) return;

            float elapsed = Time.time - _knockStart;
            int should = KnockRhythm.CountUpTo(elapsed, _knockDuration);
            while (_knockDone < should)
            {
                // 매번 조금씩 다른 피치 — 같은 파일 3연타는 기계음으로 들린다
                float pitch = 0.92f + 0.14f * ((_knockDone * 37 % 11) / 10f);
                PlayAtCorner(_knockCorner, sfxKnock, knockVolume, pitch);
                _knockDone++;
            }
            if (_knockDone >= KnockRhythm.Count && elapsed > _knockDuration) _knockCorner = -1;
        }

        private void HandleGameEventFired(EventDef def)
        {
            // 테이블 클립 우선 (§2.3) — Door 채널은 귀 대기 여부로 뭉갬/선명 선택 (§7.2 간이판)
            AudioClip clip = def.AudioClip;
            if (def.Channel == AudioChannel.Door && def.AudioClipMuffled != null && !_listening)
            {
                clip = def.AudioClipMuffled;
            }
            if (clip != null)
            {
                _sfx.PlayOneShot(clip, sfxVolume);
                return;
            }

            if (def.Kind == GameEventKind.TrueSignal) return; // 클립 없으면 정적 연출만
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
