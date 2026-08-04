using System.IO;
using Morae.Game.Core;
using Morae.Game.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Morae.EditorTools
{
    /// <summary>
    /// D4 — 심장 UI + 타이틀/게임오버/엔딩 화면을 저장된 Main.unity에 추가·배선 (씬 재생성 없음, 멱등).
    /// 하트 스프라이트는 절차 생성 (임플리시트 하트 곡선 — 외부 에셋 불필요).
    /// CLI: -executeMethod Morae.EditorTools.D4Setup.Setup
    /// </summary>
    public static class D4Setup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        private const string FontAssetPath = "Assets/_Project/Art/Fonts/Pretendard-Regular SDF.asset";
        private const string HeartSpritePath = "Assets/_Project/Art/UI/heart128.png";

        [MenuItem("Morae/Setup D4 (심장 UI·화면 3종)")]
        public static void Setup()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            SetupHeart();
            SetupTitle();
            SetupGameOver();
            SetupEnding();

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

        // ---------- 화면 3종 ----------

        private static void SetupTitle()
        {
            var screenGo = GameObject.Find("Screens/TitleScreen");
            var view = screenGo.GetComponent<TitleScreenView>();
            if (view == null) view = screenGo.AddComponent<TitleScreenView>();

            GameObject root = BuildScreenUi(screenGo, 30, new Color(0.02f, 0.02f, 0.03f, 1f),
                "밀실 버티기", "문이 잠긴 뒤, 문밖의 소리는 전부 의심하라",
                "아무 키나 눌러 밤을 시작한다", out _, out _);
            root.SetActive(false); // GameFlow.Show가 켠다 — 재시작 시엔 안 보임

            Wire(view, "root", root);
            Wire(Object.FindFirstObjectByType<GameFlowController>(), "titleScreen", view);
        }

        private static void SetupGameOver()
        {
            var screenGo = GameObject.Find("Screens/GameOverScreen");
            var view = screenGo.GetComponent<GameOverScreenView>();
            if (view == null) view = screenGo.AddComponent<GameOverScreenView>();

            GameObject root = BuildScreenUi(screenGo, 25, new Color(0.03f, 0f, 0.01f, 0.96f),
                "…", null, "E — 다시 밤이 시작된다", out CanvasGroup group, out TMP_Text title);
            root.SetActive(false);

            Wire(view, "root", root);
            Wire(view, "group", group);
            Wire(view, "titleLabel", title);
        }

        private static void SetupEnding()
        {
            var screenGo = GameObject.Find("Screens/EndingScreen");
            var view = screenGo.GetComponent<EndingScreenView>();
            if (view == null) view = screenGo.AddComponent<EndingScreenView>();

            GameObject root = BuildScreenUi(screenGo, 25, new Color(0.35f, 0.33f, 0.28f, 0.96f),
                "…", null, "E — 처음부터", out CanvasGroup group, out TMP_Text title);
            root.SetActive(false);

            Wire(view, "root", root);
            Wire(view, "group", group);
            Wire(view, "titleLabel", title);
        }

        /// <summary>화면 공통 골격: Canvas(오버레이) + 배경 + 제목/부제/하단 힌트. 이미 있으면 재사용.</summary>
        private static GameObject BuildScreenUi(GameObject screenGo, int sortOrder, Color bgColor,
            string titleText, string subText, string hintText, out CanvasGroup group, out TMP_Text titleLabel)
        {
            var rootTr = screenGo.transform.Find("Root");
            if (rootTr != null)
            {
                group = rootTr.GetComponent<CanvasGroup>();
                titleLabel = rootTr.Find("Title").GetComponent<TMP_Text>();
                return rootTr.gameObject;
            }

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);

            var root = new GameObject("Root");
            root.transform.SetParent(screenGo.transform, false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            group = root.AddComponent<CanvasGroup>();
            group.alpha = 1f; // 페이드는 각 View가 제어 (타이틀은 즉시 표시)
            group.blocksRaycasts = false;

            var bg = new GameObject("BG");
            bg.transform.SetParent(root.transform, false);
            var bgRect = bg.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImage = bg.AddComponent<Image>();
            bgImage.color = bgColor;
            bgImage.raycastTarget = false;

            titleLabel = MakeText(root.transform, "Title", font, titleText, 76f,
                new Vector2(0f, 120f), new Vector2(1600f, 320f), new Color(0.92f, 0.88f, 0.8f));
            if (!string.IsNullOrEmpty(subText))
            {
                MakeText(root.transform, "Sub", font, subText, 34f,
                    new Vector2(0f, -40f), new Vector2(1600f, 80f), new Color(0.7f, 0.66f, 0.58f));
            }
            MakeText(root.transform, "Hint", font, hintText, 30f,
                new Vector2(0f, -320f), new Vector2(1200f, 60f), new Color(0.62f, 0.6f, 0.54f));

            return root;
        }

        private static TMP_Text MakeText(Transform parent, string name, TMP_FontAsset font, string text,
            float size, Vector2 anchoredPos, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = text;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.raycastTarget = false;
            var rect = label.rectTransform;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = sizeDelta;
            return label;
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
