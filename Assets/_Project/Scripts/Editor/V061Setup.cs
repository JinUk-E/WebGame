using Morae.Game.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Morae.EditorTools
{
    /// <summary>
    /// v0.6.1 배선 — 프롤로그 학습 구간의 <b>목적지 서클</b>(불상 앞 바닥 마커).
    /// 저장된 Main.unity에 **추가·참조 배선만** 한다 (씬 재생성 없음, 화면 3종 프리팹 무수정).
    /// 멱등: 이미 있으면 위치·참조만 갱신한다.
    ///
    /// <para>
    /// 마커는 <b>Room 프리팹 밖</b>(씬 루트 <c>Stage</c>)에 만든다 — Room은 프리팹 인스턴스라
    /// 그 안에 자식을 넣으면 프리팹을 되돌리거나 다시 적용할 때 조용히 사라진다.
    /// (v0.5의 <c>Lighting/BuddhaCandleLight</c>가 같은 이유로 씬 루트에 있다.)
    /// </para>
    ///
    /// <para>
    /// 스포트라이트(학습 중 실내 감광 + 촛불 상향)는 배선이 필요 없다 —
    /// LightingController가 <c>TrainingModeChanged</c>를 직접 구독하고 수치는 자기 SerializeField로 갖는다.
    /// </para>
    ///
    /// CLI: -executeMethod Morae.EditorTools.V061Setup.Setup
    /// </summary>
    public static class V061Setup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        private const string MarkerSpritePath = "Assets/_Project/Art/Props/prop_stand_marker.png";
        private const string StageRootName = "Stage";
        private const string MarkerName = "StandMarker";

        /// <summary>
        /// 불상 앞 바닥 자리 — 좌표의 원본은 <see cref="Morae.Game.Core.TrainingStageModel.AltarStandPoint"/>다
        /// (여기에 다시 적으면 회귀 테스트가 보는 값과 씬이 갈라진다).
        /// </summary>
        private static Vector3 MarkerPosition => Morae.Game.Core.TrainingStageModel.AltarStandPoint;

        /// <summary>바닥 소품과 같은 층(0=바닥, 1=바닥 자국·후광, 8=플레이어) — 플레이어가 서면 그 위로 올라선다.</summary>
        private const int MarkerSortingOrder = 1;

        [MenuItem("Morae/Setup v0.6.1 (목적지 서클)")]
        public static void Setup()
        {
            EnsureScene();

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(MarkerSpritePath);
            if (sprite == null)
            {
                Debug.LogError($"[V061-SETUP] 마커 스프라이트 없음: {MarkerSpritePath} " +
                               "— Tools/art-gen/gen_marker.py 실행 후 다시 시도할 것");
                return;
            }

            GameObject stage = GameObject.Find(StageRootName);
            if (stage == null)
            {
                stage = new GameObject(StageRootName);
                stage.transform.position = Vector3.zero;
            }

            Transform markerTr = stage.transform.Find(MarkerName);
            GameObject marker = markerTr != null ? markerTr.gameObject : new GameObject(MarkerName);
            marker.transform.SetParent(stage.transform, false);
            marker.transform.localPosition = MarkerPosition;
            marker.transform.localScale = Vector3.one;

            var sr = marker.GetComponent<SpriteRenderer>();
            if (sr == null) sr = marker.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = MarkerSortingOrder;
            // 무광 — 학습 스포트라이트로 방이 더 어두워질수록 마커는 오히려 또렷해야 한다
            // (v0.6 "소금은 무광이라 감광될수록 또렷"과 같은 처리. Lit이면 안내가 어둠에 같이 먹힌다.)
            sr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            sr.enabled = false; // DestinationMarkerView가 학습 구간에만 켠다

            var view = marker.GetComponent<DestinationMarkerView>();
            if (view == null) view = marker.AddComponent<DestinationMarkerView>();

            var player = GameObject.Find("Player");
            Wire(view, "marker", sr);
            Wire(view, "player", player != null ? player.transform : null);

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[V061-SETUP] 목적지 서클 배선·씬 저장 완료 — {StageRootName}/{MarkerName} @ {MarkerPosition}");
        }

        private static void EnsureScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static void Wire(Component target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[V061-SETUP] 배선 실패 — {target.GetType().Name}.{field} 프로퍼티 없음");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();

            Object check = new SerializedObject(target).FindProperty(field).objectReferenceValue;
            Debug.Log($"[V061-SETUP] {target.GetType().Name}.{field} = " +
                      $"{(check != null ? check.name : "NULL!")}");
        }
    }
}
