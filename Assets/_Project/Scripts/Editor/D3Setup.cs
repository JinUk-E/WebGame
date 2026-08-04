using Morae.Game.Core;
using Morae.Game.Data;
using Morae.Game.Gauges;
using Morae.Game.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Morae.EditorTools
{
    /// <summary>
    /// D3 컴포넌트(EventDirector/SubtitleView/SaltCornersView/LightingController)를 저장된 Main.unity에
    /// 추가·배선 (씬 재생성 없음 — 수동 배선 보존, SoundSetup과 동일 방식). 멱등.
    /// 실행 후 콘솔의 [D3-SETUP] 로그에서 NULL이 없는지 확인할 것 (SO 배선 유실 사고 감지).
    /// CLI: -executeMethod Morae.EditorTools.D3Setup.Setup
    /// </summary>
    public static class D3Setup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        private const string FontAssetPath = "Assets/_Project/Art/Fonts/Pretendard-Regular SDF.asset";

        [MenuItem("Morae/Setup D3 (신호·자막·시각·조명)")]
        public static void Setup()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            SetupEventDirector();
            SetupSubtitleView();
            SetupSaltCornersView();
            SetupLightingController();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[D3-SETUP] 배선·씬 저장 완료");
        }

        private static void SetupEventDirector()
        {
            var systems = GameObject.Find("Systems");
            var director = systems.GetComponent<EventDirector>();
            if (director == null) director = systems.AddComponent<EventDirector>();

            Wire(director, "eventTable",
                AssetDatabase.LoadAssetAtPath<EventTable>(DataAssetBuilder.EventTablePath));
            Wire(director, "config",
                AssetDatabase.LoadAssetAtPath<BalanceConfig>(DataAssetBuilder.BalanceConfigPath));
            Wire(director, "sequencer", systems.GetComponent<PhaseSequencer>());
            Wire(director, "sanity", systems.GetComponent<Sanity>());
        }

        private static void SetupSubtitleView()
        {
            var holder = GameObject.Find("UI/SubtitleView");
            var view = holder.GetComponent<SubtitleView>();
            if (view == null) view = holder.AddComponent<SubtitleView>();

            var labelTr = holder.transform.Find("Label");
            TextMeshProUGUI label;
            if (labelTr == null)
            {
                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(holder.transform, false);
                label = labelGo.AddComponent<TextMeshProUGUI>();
                var rect = label.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(0f, 90f);
                rect.sizeDelta = new Vector2(1500f, 140f);
                label.fontSize = 40f;
                label.alignment = TextAlignmentOptions.Bottom;
                label.color = new Color(0.93f, 0.9f, 0.85f);
                label.text = string.Empty;
            }
            else
            {
                label = labelTr.GetComponent<TextMeshProUGUI>();
            }

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (font != null) label.font = font;
            else Debug.LogWarning("[D3-SETUP] Pretendard SDF 로드 실패 — 한글 자막이 깨질 수 있음");

            Wire(view, "label", label);
        }

        private static void SetupSaltCornersView()
        {
            var room = GameObject.Find("Room");
            var view = room.GetComponent<SaltCornersView>();
            if (view == null) view = room.AddComponent<SaltCornersView>();

            var renderers = new Object[CornerIndex.Count];
            for (int i = 0; i < CornerIndex.Count; i++)
            {
                var visual = GameObject.Find($"Room/SaltCorner_{i}/Visual");
                renderers[i] = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
            }
            WireArray(view, "cornerRenderers", renderers);
        }

        private static void SetupLightingController()
        {
            var lighting = GameObject.Find("Lighting");
            var controller = lighting.GetComponent<LightingController>();
            if (controller == null) controller = lighting.AddComponent<LightingController>();

            Wire(controller, "sequencer", Object.FindFirstObjectByType<PhaseSequencer>());
            Wire(controller, "globalLight", FindLight("Lighting/GlobalLight2D"));
            Wire(controller, "tvLight", FindLight("Lighting/TVLight"));
            Wire(controller, "windowDawnLight", FindLight("Lighting/WindowDawnLight"));
            var corners = new Object[CornerIndex.Count];
            for (int i = 0; i < CornerIndex.Count; i++)
            {
                corners[i] = FindLight($"Lighting/CornerLight_{i}");
            }
            WireArray(controller, "cornerLights", corners);
        }

        private static Light2D FindLight(string path)
        {
            var go = GameObject.Find(path);
            return go != null ? go.GetComponent<Light2D>() : null;
        }

        // ---------- 배선 + 즉시 검증 ----------

        private static void Wire(Component target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();

            Object check = new SerializedObject(target).FindProperty(field).objectReferenceValue;
            Debug.Log($"[D3-SETUP] {target.GetType().Name}.{field} = {(check != null ? check.name : "NULL!")}");
        }

        private static void WireArray(Component target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(field);
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            int nulls = 0;
            SerializedProperty verify = new SerializedObject(target).FindProperty(field);
            for (int i = 0; i < verify.arraySize; i++)
            {
                if (verify.GetArrayElementAtIndex(i).objectReferenceValue == null) nulls++;
            }
            Debug.Log($"[D3-SETUP] {target.GetType().Name}.{field} = {verify.arraySize}개 (NULL {nulls})");
        }
    }
}
