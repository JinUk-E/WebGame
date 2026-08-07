using Morae.Game.Core;
using Morae.Game.Data;
using Morae.Game.Gauges;
using Morae.Game.Player;
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
            SetupChannelBars();
            SetupPrologue();
            SetupInteractPrompt();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // SaltCornersView·ChannelBarView는 Room 하위에 산다 — 인스턴스에만 쓰면 프리팹과 갈라진다.
            // 방은 프리팹이 단일 진실이므로 여기서 프리팹으로 밀어 올린다 (씬 참조는 오버라이드로 되꽂힌다).
            RoomPrefabSetup.ApplyRoomToPrefab();
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

        private static void SetupChannelBars()
        {
            SetupBar("Room/Buddha", ChannelBarView.Source.Prayer, new Vector3(0f, 0.75f, 0f));
            SetupBar("Room/Door", ChannelBarView.Source.DoorLatch, new Vector3(0.55f, 1.1f, 0f));
            SetupBar("Room/Jar", ChannelBarView.Source.Jar, new Vector3(0f, 0.65f, 0f));
            SetupBar("Room/Blanket", ChannelBarView.Source.BlanketExit, new Vector3(0f, 1.05f, 0f));
        }

        private static void SetupBar(string propPath, ChannelBarView.Source source, Vector3 localPos)
        {
            var prop = GameObject.Find(propPath);

            // v1.4 잔재 마이그레이션 — PrayerView(삭제된 스크립트)·PrayerBar는 ChannelBarView·ChannelBar로 대체됨
            int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(prop);
            if (removed > 0) Debug.Log($"[D3-SETUP] {prop.name}: 누락 스크립트 {removed}개 제거 (구 PrayerView)");
            var legacyBar = prop.transform.Find("PrayerBar");
            if (legacyBar != null) Object.DestroyImmediate(legacyBar.gameObject);

            var view = prop.GetComponent<ChannelBarView>();
            if (view == null) view = prop.AddComponent<ChannelBarView>();

            var barTr = prop.transform.Find("ChannelBar");
            GameObject barRoot;
            Transform fill;
            if (barTr == null)
            {
                var white = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Smoke/white32.png");

                barRoot = new GameObject("ChannelBar");
                barRoot.transform.SetParent(prop.transform, false);
                barRoot.transform.localPosition = localPos;

                var bg = new GameObject("BG");
                bg.transform.SetParent(barRoot.transform, false);
                bg.transform.localScale = new Vector3(1f, 0.14f, 1f);
                var bgSr = bg.AddComponent<SpriteRenderer>();
                bgSr.sprite = white;
                bgSr.color = new Color(0.08f, 0.08f, 0.1f, 0.85f);
                bgSr.sortingOrder = 6;

                var fillGo = new GameObject("Fill");
                fillGo.transform.SetParent(barRoot.transform, false);
                fillGo.transform.localScale = new Vector3(0f, 0.1f, 1f); // x = ChannelBarView가 진행률로 제어
                var fillSr = fillGo.AddComponent<SpriteRenderer>();
                fillSr.sprite = white;
                fillSr.color = new Color(1f, 0.85f, 0.3f); // 기도 조준 하이라이트와 동일 금빛
                fillSr.sortingOrder = 7;

                fill = fillGo.transform;
                barRoot.SetActive(false);
            }
            else
            {
                barRoot = barTr.gameObject;
                fill = barTr.Find("Fill");
            }

            var so = new SerializedObject(view);
            so.FindProperty("source").enumValueIndex = (int)source;
            so.ApplyModifiedPropertiesWithoutUndo();
            Wire(view, "barRoot", barRoot);
            Wire(view, "fill", fill);
        }

        private static void SetupPrologue()
        {
            var directorGo = GameObject.Find("Directors/PrologueDirector");
            var director = directorGo.GetComponent<PrologueDirector>();
            if (director == null) director = directorGo.AddComponent<PrologueDirector>();

            var flow = Object.FindFirstObjectByType<GameFlowController>();
            Wire(flow, "prologueDirector", director);
        }

        private static void SetupInteractPrompt()
        {
            var holder = GameObject.Find("UI/InteractPrompt");
            var view = holder.GetComponent<InteractPromptView>();
            if (view == null) view = holder.AddComponent<InteractPromptView>();

            // ⚠ 라벨은 Art2Setup의 키캡 행(`Row/Keycap` + `Row/Label`)이 소유한다.
            // 예전엔 여기서 `InteractPrompt/Label`(구 단일 라벨)을 없으면 만들었는데, Art2Setup이
            // 그 구 라벨을 지우고 Row로 대체한 뒤로는 **D3를 나중에 돌릴 때마다 구 라벨이 되살아나**
            // InteractPromptView.label이 그쪽을 가리켜 키캡 행이 죽은 채 남았다(실행 순서에 따라 달라지는 버그).
            // 그래서 여기서는 만들지 않고 찾기만 한다.
            // (UnityEngine.Object에 ?? 금지 — 가짜 null 때문에 명시 비교로 쓴다)
            Transform labelTr = holder.transform.Find("Row/Label");
            if (labelTr == null) labelTr = holder.transform.Find("Label");
            TextMeshProUGUI label = labelTr != null ? labelTr.GetComponent<TextMeshProUGUI>() : null;
            if (label == null)
            {
                Debug.LogError("[D3-SETUP] UI/InteractPrompt에 라벨이 없다 — " +
                               "'Morae/Setup Art2 (스프라이트 배선)'을 먼저 실행할 것");
                return;
            }

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (font != null) label.font = font;

            Wire(view, "interaction", Object.FindFirstObjectByType<PlayerInteraction>());
            Wire(view, "label", label);
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
