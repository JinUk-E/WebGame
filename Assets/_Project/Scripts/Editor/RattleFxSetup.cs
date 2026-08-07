using Morae.Game.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Morae.EditorTools
{
    /// <summary>
    /// 전화벨·문/창문 흔들림 배선 — 저장된 Main.unity에 <b>추가·참조 배선만</b> 한다
    /// (씬 재생성 없음, 화면 3종 프리팹 무수정). 멱등: 다시 돌리면 위치·참조만 갱신한다.
    ///
    /// 하는 일
    ///   ① <c>Audio/PhoneSource</c>를 <b>문 바깥</b>으로 옮기고 3D 파라미터를 잡는다 —
    ///      전화는 이 방에 없다. 소스가 (0,0)에 있으면 방 한가운데서 울려 "다른 방"이 무너진다.
    ///   ② <c>Room/Window</c>에 <see cref="WindowRattleView"/>를 붙이고 흔들 대상(Visual)을 물린다.
    ///   ③ SoundSetup을 재실행해 새 클립 2종(phone_ring / handle_rattle)을 배선한다.
    ///
    /// 문짝 흔들림은 배선이 필요 없다 — DoorView가 이미 씬에 있고 이벤트를 스스로 구독한다.
    ///
    /// CLI: -executeMethod Morae.EditorTools.RattleFxSetup.Setup
    /// </summary>
    public static class RattleFxSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Main.unity";

        /// <summary>
        /// 복도의 전화 자리. 문(Room/Door, x −3.1)의 <b>바깥쪽</b>(상단 벽 너머 y +6.2)이라
        /// 정위가 문 방향과 일치한다 — 소리가 나는 쪽과 위험이 오는 쪽이 같아야 방향 학습이 유지된다.
        /// </summary>
        private static readonly Vector3 PhonePosition = new Vector3(-3.1f, 6.2f, 0f);

        [MenuItem("Morae/Setup Rattle FX (전화벨·문창 흔들림)")]
        public static void Setup()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            SetupPhoneSource();
            SetupWindowRattle();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // 새 클립 배선 (SoundSetup이 씬 저장까지 수행)
            SoundSetup.Setup();
            Debug.Log("[RATTLE-FX] 배선·씬 저장 완료");
        }

        private static void SetupPhoneSource()
        {
            GameObject go = GameObject.Find("Audio/PhoneSource");
            if (go == null)
            {
                var audioRoot = GameObject.Find("Audio");
                go = new GameObject("PhoneSource");
                if (audioRoot != null) go.transform.SetParent(audioRoot.transform);
                Debug.LogWarning("[RATTLE-FX] Audio/PhoneSource 없어 새로 만들었다 — 씬 구조 확인 권장");
            }

            // ⚠ 좌표는 조건 없이 매번 적용한다. "없을 때만" 잡으면 셋업을 다시 돌려도 안 움직인다
            //    (2026-08-06 D4Setup 하트 좌표 사고와 같은 함정).
            go.transform.position = PhonePosition;

            var src = go.GetComponent<AudioSource>();
            if (src == null) src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;   // §8.2 오디오 게이트
            src.loop = false;
            src.volume = 0f;
            src.spatialBlend = 1f;     // panStereo는 WebGL 미지원 — 정위는 좌표로만 만든다

            var mgr = Object.FindFirstObjectByType<SoundManager>();
            if (mgr == null)
            {
                Debug.LogError("[RATTLE-FX] SoundManager 없음 — 전화벨 소스 배선 실패");
                return;
            }
            var so = new SerializedObject(mgr);
            so.FindProperty("phoneSource").objectReferenceValue = src;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[RATTLE-FX] PhoneSource → {PhonePosition} (SoundManager 배선 완료)");
        }

        private static void SetupWindowRattle()
        {
            GameObject window = GameObject.Find("Room/Window");
            if (window == null)
            {
                Debug.LogError("[RATTLE-FX] Room/Window 없음 — 창문 흔들림 배선 실패");
                return;
            }

            Transform visual = window.transform.Find("Visual");
            if (visual == null)
            {
                Debug.LogWarning("[RATTLE-FX] Room/Window/Visual 없음 — 창문 루트를 흔든다(여명 라이트와 어긋날 수 있음)");
                visual = window.transform;
            }

            var view = window.GetComponent<WindowRattleView>();
            if (view == null) view = window.AddComponent<WindowRattleView>();
            var so = new SerializedObject(view);
            so.FindProperty("target").objectReferenceValue = visual;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[RATTLE-FX] WindowRattleView → {visual.name}");
        }
    }
}
