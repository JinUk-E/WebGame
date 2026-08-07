using Morae.Game.Core;
using UnityEngine;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 여명 체감 연출 (명세 v0.7 §1·§2 — 표현 계층).
    ///
    /// <para>
    /// <b>무엇을 고치는가.</b> "창밖이 밝아지는 것이 체감되지 않는다"는 실플레이 제보.
    /// 여명은 3채널 판별의 <b>유일한 진실 채널</b>이라 안 읽히면 코어 규칙이 무너진다.
    /// 그래서 여명을 밝기가 아니라 <b>모양(바닥 창틀 빛 무늬)</b>과 <b>색(창호지 단계 전환)</b>으로 옮겼다.
    /// 수치·색·경계의 원본은 전부 <see cref="DawnStageModel"/>이고 이 컴포넌트는 그 값을 화면에 얹기만 한다.
    /// </para>
    ///
    /// <para>
    /// <b>⚠ 이 컴포넌트에 오염원을 들이지 말 것.</b> 소금 상태(<c>CornerStageChanged</c>·흑화 감광),
    /// 페이즈 <c>RoomLightBias</c>, 학습 스포트라이트 배율, 에디터 조도 오버라이드 —
    /// 어느 것도 여기 들어와서는 안 된다(v0.5 감광 예외①). 읽는 것은 <see cref="PhaseSequencer.Dawn01"/>
    /// 하나뿐이며, 렌더러는 전부 <b>무광</b>(Sprites-Default)이라 2D 라이트의 영향도 받지 않는다.
    /// <c>DawnLegibilityTests</c>가 이 파일의 소스를 훑어 오염원 식별자가 등장하면 실패한다.
    /// </para>
    ///
    /// <para>
    /// Dawn01은 이벤트가 없는 연속값이라 시퀀서를 직접 읽는다 —
    /// <see cref="LightingController"/>·<c>ClockView</c>의 선례(표현 계층의 읽기 전용 참조).
    /// </para>
    /// </summary>
    public sealed class DawnWindowView : MonoBehaviour
    {
        [Tooltip("본편 진행 — Dawn01(진실 채널)만 읽는다")]
        [SerializeField] private PhaseSequencer sequencer;

        [Header("② 창호지 — 색 단계 전환")]
        // 창 아트는 **창 칸이 알파 0인 나무틀**이고, 칸을 통해 보이는 것은 그 뒤에 깔린 쿼드(Room/Window/Visual/Sky)다.
        // 색은 거기에 얹는다 — 나무틀에 얹으면 틀까지 물들고 살의 실루엣이 사라진다.
        [Tooltip("Room/Window/Visual/Sky — 창 칸 뒤의 종이. 반드시 무광(Sprites-Default)")]
        [SerializeField] private SpriteRenderer windowPaper;

        [Header("① 바닥 창틀 빛 무늬")]
        [Tooltip("형태 없는 빛무리 — 초반에 진하고 아침이 될수록 걷힌다")]
        [SerializeField] private SpriteRenderer patchHaze;
        [Tooltip("창살 격자 — 아침이 될수록 또렷해진다")]
        [SerializeField] private SpriteRenderer patchGrid;

        [Tooltip("무늬 스프라이트의 기준 크기(유닛) — 스케일 계산의 분모. 텍스처 200×260px @PPU100")]
        [SerializeField] private Vector2 patchSpriteSize = new Vector2(2.0f, 2.6f);

        // 계단 전환 상태 — 이전 단계에서 새 단계로 StepBlendSec 동안 건너간다.
        private int _stage = -1;
        private int _prevStage;
        private float _stepAt = -999f;

        private void Reset() => patchSpriteSize = new Vector2(2.0f, 2.6f);

        private void Start()
        {
            // 첫 프레임은 목표값으로 스냅한다 (시작하자마자 0.35초짜리 전환이 도는 것을 막는다)
            int stage = DawnStageModel.Stage(CurrentDawn);
            _stage = stage;
            _prevStage = stage;
            _stepAt = -999f;
            Apply(stage, stage, 1f);
        }

        /// <summary>진실 채널 — 여기 말고 다른 입력을 더하지 말 것.</summary>
        private float CurrentDawn => sequencer != null ? sequencer.Dawn01 : 0f;

        private void Update()
        {
            int stage = DawnStageModel.Stage(CurrentDawn);
            if (stage != _stage)
            {
                _prevStage = _stage < 0 ? stage : _stage;
                _stage = stage;
                _stepAt = Time.time;
                Debug.Log($"[DAWN] 여명 {_prevStage}단계 → {stage}단계 (dawn={CurrentDawn:F3})");
            }

            float k = DawnStageModel.StepBlend01(Time.time - _stepAt);
            Apply(_prevStage, _stage, k);
        }

        /// <summary>
        /// 두 단계 사이를 <paramref name="k"/>만큼 건너간 화면 상태를 만든다.
        /// 계단이 목적이므로 k는 0.35초 안에 1이 된다 — 그 뒤로는 정확히 새 단계 값이다.
        /// </summary>
        private void Apply(int from, int to, float k)
        {
            // ② 창호지 색 — 무광이라 이 값이 그대로 화면에 나간다(방 조도 무관)
            if (windowPaper != null)
            {
                Color c = Color.Lerp(DawnStageModel.PaperColor(from), DawnStageModel.PaperColor(to), k);
                c.a = windowPaper.color.a;
                windowPaper.color = c;
            }

            // ① 바닥 무늬 — 길이·폭·위치·틴트가 함께 움직인다
            float length = Mathf.Lerp(DawnStageModel.PatchLength(from), DawnStageModel.PatchLength(to), k);
            float width = Mathf.Lerp(DawnStageModel.PatchWidth(from), DawnStageModel.PatchWidth(to), k);
            float centerX = Mathf.Lerp(DawnStageModel.PatchCenterX(from), DawnStageModel.PatchCenterX(to), k);
            Color tint = Color.Lerp(DawnStageModel.PatchTint(from), DawnStageModel.PatchTint(to), k);
            float haze = Mathf.Lerp(DawnStageModel.HazeAlpha(from), DawnStageModel.HazeAlpha(to), k);
            float grid = Mathf.Lerp(DawnStageModel.GridAlpha(from), DawnStageModel.GridAlpha(to), k);

            Place(patchHaze, centerX, length, width, tint, haze);
            Place(patchGrid, centerX, length, width, tint, grid);
        }

        /// <summary>
        /// 무늬 한 겹의 배치. 스프라이트 피벗이 중심이라 <b>윗변을 창 아래에 고정</b>하려면
        /// 중심을 길이의 절반만큼 내려야 한다 — 그래야 길어질 때 창에서 <b>뻗어 나오는</b> 것으로 보인다
        /// (중심 고정이면 위아래로 동시에 자라 "커지는 얼룩"이 된다).
        /// </summary>
        private void Place(SpriteRenderer sr, float centerX, float length, float width, Color tint, float alpha)
        {
            if (sr == null) return;

            bool visible = alpha > 0.002f && length > 0.002f;
            if (sr.enabled != visible) sr.enabled = visible;   // 안 보일 때는 오버드로우 자체를 없앤다
            if (!visible) return;

            float sx = patchSpriteSize.x > 0.001f ? width / patchSpriteSize.x : 1f;
            float sy = patchSpriteSize.y > 0.001f ? length / patchSpriteSize.y : 1f;
            Transform t = sr.transform;
            t.localScale = new Vector3(sx, sy, 1f);
            Vector3 p = t.localPosition;
            t.localPosition = new Vector3(centerX, DawnStageModel.PatchAnchorY - length * 0.5f, p.z);

            tint.a = alpha;
            sr.color = tint;
        }
    }
}
