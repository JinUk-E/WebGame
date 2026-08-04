using UnityEngine;

namespace Morae.Game.Core
{
    /// <summary>
    /// 개발용 치트 (개발 빌드 한정). F2 = 진짜 신호 강제 발화 —
    /// EventDirector(Epic 2) 전에 개문 엔딩 분기(Perfect/Survived)를 검증하기 위한 임시 수단.
    /// </summary>
    public sealed class DebugCheats : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F2))
            {
                Debug.Log("[CHEAT] 진짜 신호 강제 발화 (F2)");
                GameEvents.RaiseTrueSignalStarted();
            }
        }
#endif
    }
}
