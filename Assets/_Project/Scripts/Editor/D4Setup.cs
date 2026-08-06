using System.IO;
using Morae.Game.Core;
using Morae.Game.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Morae.EditorTools
{
    /// <summary>
    /// D4 — 심장 UI 배선 + 화면 3종(타이틀/게임오버/엔딩) <b>프리팹 인스턴스</b> 참조 배선.
    /// (씬 재생성 없음, 멱등. 하트 스프라이트는 절차 생성 — 외부 에셋 불필요.)
    /// <para>
    /// ⚠ 2026-08-06 개편: 화면 3종은 <c>Assets/_Project/Prefab/Screens/*.prefab</c>이 <b>단일 진실</b>이다.
    /// 예전에 여기 있던 "코드로 화면을 만든다" 경로(BuildScreenUi/MakeButton/BuildVolumeSlider 등)는
    /// 프리팹 작업물을 덮어쓰기 때문에 전부 제거했다 (복원이 필요하면 git 이력 참조).
    /// 화면 레이아웃·문구·스킨 수정은 <b>에디터에서 프리팹을 편집</b>한다. 이 스크립트는 씬 쪽 참조만 잇는다.
    /// </para>
    /// CLI: -executeMethod Morae.EditorTools.D4Setup.Setup
    /// </summary>
    public static class D4Setup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        private const string HeartSpritePath = "Assets/_Project/Art/UI/heart128.png";

        [MenuItem("Morae/Setup D4 (심장 UI·화면 참조 배선)")]
        public static void Setup()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            SetupHeart();
            WireScreenPrefabs();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[D4-SETUP] 배선·씬 저장 완료");
        }

        // ---------- 심장 UI ----------

        private static void SetupHeart()
        {
            var canvas = GameObject.Find("UI");
            var holderTr = canvas.transform.Find("HeartView");
            GameObject holder = holderTr != null ? holderTr.gameObject : new GameObject("HeartView");
            if (holderTr == null)
            {
                holder.transform.SetParent(canvas.transform, false);
                var rect = holder.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(0f, 18f);
                rect.sizeDelta = new Vector2(72f, 72f);
            }

            var image = holder.GetComponent<Image>();
            if (image == null) image = holder.AddComponent<Image>();
            image.sprite = EnsureHeartSprite();
            image.raycastTarget = false;

            var view = holder.GetComponent<HeartView>();
            if (view == null) view = holder.AddComponent<HeartView>();
            Wire(view, "heart", image);
        }

        private static Sprite EnsureHeartSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(HeartSpritePath);
            if (existing != null) return existing;

            const int size = 128;
            Directory.CreateDirectory(Path.GetDirectoryName(FileUtil.GetPhysicalPath(HeartSpritePath)));
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    // 임플리시트 하트: (x²+y²−1)³ − x²y³ ≤ 0
                    float x = (px / (float)(size - 1) - 0.5f) * 2.6f;
                    float y = (py / (float)(size - 1) - 0.42f) * 2.6f;
                    float f = Mathf.Pow(x * x + y * y - 1f, 3f) - x * x * y * y * y;
                    pixels[py * size + px] = f <= 0f
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(0, 0, 0, 0);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(FileUtil.GetPhysicalPath(HeartSpritePath), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(HeartSpritePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(HeartSpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(HeartSpritePath);
        }

        // ---------- 화면 3종 = 프리팹 인스턴스 참조만 ----------

        /// <summary>
        /// 씬의 <c>Screens/*</c> 프리팹 인스턴스를 찾아 씬 쪽 참조(GameFlow → 타이틀)를 잇고,
        /// 각 View의 프리팹 내부 배선이 살아 있는지 검증 로그를 남긴다. <b>생성·파괴는 하지 않는다.</b>
        /// </summary>
        private static void WireScreenPrefabs()
        {
            var title = FindScreen<TitleScreenView>("Screens/TitleScreen");
            var gameOver = FindScreen<GameOverScreenView>("Screens/GameOverScreen");
            var ending = FindScreen<EndingScreenView>("Screens/EndingScreen");

            if (title != null)
            {
                var flow = Object.FindFirstObjectByType<GameFlowController>();
                if (flow != null) Wire(flow, "titleScreen", title);
            }

            VerifyInternalWiring(title, "root", "startButton", "helpButton", "helpPanel", "helpText",
                "helpPageLabel", "helpPrevButton", "helpNextButton", "helpCloseButton", "skipRow", "skipButton",
                "skipLabel");
            VerifyInternalWiring(gameOver, "root", "group", "titleLabel");
            VerifyInternalWiring(ending, "root", "group", "titleLabel");
        }

        private static T FindScreen<T>(string path) where T : Component
        {
            var go = GameObject.Find(path);
            if (go == null)
            {
                Debug.LogError($"[D4-SETUP] {path} 없음 — 화면 프리팹 인스턴스가 씬에서 사라졌다 "
                               + "(Assets/_Project/Prefab/Screens/*.prefab을 씬 Screens 아래에 다시 배치할 것)");
                return null;
            }

            var view = go.GetComponent<T>();
            if (view == null)
            {
                Debug.LogError($"[D4-SETUP] {path}에 {typeof(T).Name} 없음 — 프리팹 원본 확인 필요");
            }
            return view;
        }

        private static void VerifyInternalWiring(Component view, params string[] fields)
        {
            if (view == null) return;
            var so = new SerializedObject(view);
            foreach (string field in fields)
            {
                var prop = so.FindProperty(field);
                if (prop == null)
                {
                    Debug.LogError($"[D4-SETUP] {view.GetType().Name}.{field} 필드 없음");
                    continue;
                }
                if (prop.objectReferenceValue == null)
                {
                    Debug.LogError($"[D4-SETUP] {view.GetType().Name}.{field} 미배선 — 프리팹을 열어 배선할 것");
                }
            }
        }

        private static void Wire(Component target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();

            Object check = new SerializedObject(target).FindProperty(field).objectReferenceValue;
            Debug.Log($"[D4-SETUP] {target.GetType().Name}.{field} = {(check != null ? check.name : "NULL!")}");
        }
    }
}
