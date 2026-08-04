using System;
using UnityEngine;

namespace Morae.Game.Core
{
    /// <summary>
    /// 타이틀 화면 = WebGL 오디오 게이트 (architecture §8.2 — 첫 사용자 입력 전 오디오 재생 금지).
    /// 첫 실행에만 표시 (재시작은 SessionContext.SkipPrologue로 GameFlow가 통째 스킵).
    /// 아무 입력(클릭·키) → 숨기고 시작 콜백 — GameFlow.BeginFromTitle이 여기로 옮겨온다 (예정됐던 이동).
    /// </summary>
    public sealed class TitleScreenView : MonoBehaviour
    {
        [SerializeField] private GameObject root;

        private Action _onStart;
        private bool _visible;

        public void Show(Action onStart)
        {
            _onStart = onStart;
            _visible = true;
            if (root != null) root.SetActive(true);
        }

        private void Update()
        {
            if (!_visible) return;
            if (!Input.anyKeyDown && !Input.GetMouseButtonDown(0)) return;

            _visible = false;
            if (root != null) root.SetActive(false);
            Action callback = _onStart;
            _onStart = null;
            Debug.Log("[TITLE] 시작 입력 — 오디오 게이트 통과");
            callback?.Invoke();
        }
    }
}
