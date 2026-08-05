using System.Runtime.InteropServices;
using UnityEngine;

namespace Morae.Game.Player
{
    /// <summary>
    /// 온스크린 컨트롤을 켤 기기인지 판정 (한 번만 계산해 캐싱).
    /// <para>
    /// WebGL: <c>Assets/Plugins/WebGL/MoraeTouch.jslib</c>의 미디어 질의
    /// <c>(pointer: coarse) and (hover: none)</c> — 폰·태블릿만 true.
    /// <b>터치 지원 데스크톱(윈도우 터치 노트북)은 false</b>라 키보드 경험이 그대로 유지된다
    /// (Input.touchSupported 를 쓰면 여기서 데스크톱이 오염된다 — 쓰지 않는 이유).
    /// </para>
    /// 에디터·기타 플랫폼: 모바일 플랫폼일 때만 true. 에디터 검증은 TouchControlsView.forceEnable 로.
    /// </summary>
    public static class TouchSupport
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int MoraeIsCoarsePointer();
#endif

        private static int _cached = -1;

        public static bool IsTouchDevice
        {
            get
            {
                if (_cached < 0) _cached = Detect() ? 1 : 0;
                return _cached == 1;
            }
        }

        private static bool Detect()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                return MoraeIsCoarsePointer() != 0;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[TOUCH] 기기 판별 실패 — 데스크톱으로 간주: " + e.Message);
                return false;
            }
#else
            return Application.isMobilePlatform;
#endif
        }
    }
}
