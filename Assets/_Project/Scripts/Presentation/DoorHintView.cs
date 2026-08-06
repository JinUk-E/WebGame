using Morae.Game.Core;
using Morae.Game.Data;
using Morae.Game.Interactions;
using Morae.Game.Player;
using TMPro;
using UnityEngine;

namespace Morae.Game.Presentation
{
    /// <summary>
    /// 문 개방 조작 힌트 (표현 계층 — 구독만, §1.2).
    ///
    /// 귀를 대는 것(E 홀드)까지는 <see cref="InteractPromptView"/>가 안내하지만, 그 상태에서
    /// **문 방향키를 계속 누르면 걸쇠가 열린다**는 두 번째 조작은 어디에도 표시되지 않았다.
    /// 안내가 없는 조작은 없는 조작이나 마찬가지고, 이 게임에서는 그게 곧 엔딩 경로 하나가 잠기는 것이다.
    ///
    /// 표시 구간은 <see cref="PlayerState.ListeningAtDoor"/> 동안만 — 문 앞에 서기만 해도 뜨면
    /// "열어도 된다"는 잘못된 권유가 된다. 귀를 댄 사람에게만, 선택지로 보여준다.
    ///
    /// 방향키 글리프는 <see cref="DoorInteractable.PushDirection"/>에서 끌어온다 — 문을 다른 벽으로 옮기면
    /// 화살표도 따라 바뀐다. 손으로 적어두면 그 순간부터 조용히 거짓말을 한다.
    /// </summary>
    public sealed class DoorHintView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text keyGlyph;
        [SerializeField] private TMP_Text label;
        [SerializeField] private DoorInteractable door;   // 방향 원본 (읽기 전용 참조)

        [SerializeField] private string keyboardLabel = "문 밀기 (계속 누르기)";
        [SerializeField] private string touchLabel = "스틱을 문 쪽으로";
        [SerializeField] private string touchGlyph = "◎";

        private bool _shown;

        private void Awake() => SetShown(false);

        private void OnEnable() => GameEvents.PlayerStateChanged += HandlePlayerState;
        private void OnDisable() => GameEvents.PlayerStateChanged -= HandlePlayerState;

        private void HandlePlayerState(PlayerState state) => SetShown(state == PlayerState.ListeningAtDoor);

        private void SetShown(bool show)
        {
            if (_shown == show && root != null && root.activeSelf == show) return;
            _shown = show;
            if (root != null) root.SetActive(show);
            if (!show) return;

            bool touch = TouchSupport.IsTouchDevice;
            if (label != null) label.text = touch ? touchLabel : keyboardLabel;
            if (keyGlyph != null) keyGlyph.text = touch ? touchGlyph : Arrow();
        }

        /// <summary>문을 미는 방향 → 화살표 글리프. 대각은 없다 (문은 언제나 한 벽에 붙어 있다).</summary>
        private string Arrow()
        {
            Vector2 d = door != null ? door.PushDirection : Vector2.up;
            if (Mathf.Abs(d.x) >= Mathf.Abs(d.y)) return d.x >= 0f ? "→" : "←";
            return d.y >= 0f ? "↑" : "↓";
        }
    }
}
