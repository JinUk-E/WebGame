using UnityEngine;

namespace Morae.Game.Core
{
    /// <summary>
    /// 개발용 시간 배속 토글 — 본편 420초를 빠르게 검증 (§4 순서 3 권고).
    /// F1으로 1→4→8 순환. 에디터·개발 빌드에서만 동작 (릴리스 빌드에서는 컴파일 제외).
    /// Time.timeScale을 쓰므로 시퀀서·이동·홀드 등 시간 소비 로직 전부가 일관되게 배속된다.
    /// </summary>
    public sealed class DebugTimeScale : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly float[] Steps = { 1f, 4f, 8f };
        private int _index;

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.F1)) return;
            _index = (_index + 1) % Steps.Length;
            Time.timeScale = Steps[_index];
            Debug.Log($"[DEBUG] Time.timeScale = {Time.timeScale:F0}x");
        }

        private void OnDisable()
        {
            // 씬 리로드·비활성화 시 원복 — 배속이 다음 런에 새지 않게
            Time.timeScale = 1f;
            _index = 0;
        }
#endif
    }
}
