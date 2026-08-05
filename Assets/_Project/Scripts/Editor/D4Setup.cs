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
        // 아트 2단계 — 절차 생성 UI 스킨 (붓 자국 빈 판. 절차생성-스프라이트 노트 참조)
        private const string ButtonNormalPath = "Assets/_Project/Art/UI/ui_button_normal.png";
        private const string ButtonHoverPath = "Assets/_Project/Art/UI/ui_button_hover.png";
        private const string SliderTrackPath = "Assets/_Project/Art/UI/ui_slider_track.png";
        private const string SliderHandlePath = "Assets/_Project/Art/UI/ui_slider_handle.png";

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

            // 타이틀은 레이아웃 개편(버튼·토글·도움말)이 잦다 — 항상 부수고 새로 만든다 (수동 편집 없음 전제)
            var oldRoot = screenGo.transform.Find("Root");
            if (oldRoot != null) Object.DestroyImmediate(oldRoot.gameObject);

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);

            GameObject root = BuildScreenUi(screenGo, 30, new Color(0.02f, 0.02f, 0.03f, 1f),
                "밀실 버티기", "문이 잠긴 뒤, 문밖의 소리는 전부 의심하라", null, out _, out _);
            root.AddComponent<GraphicRaycaster>(); // 버튼 클릭
            // 버그 수정(2026-08-04): BuildScreenUi 공통값 blocksRaycasts=false가 자식 버튼 클릭 전부 차단 —
            // 버튼 있는 타이틀만 해제 (게임오버/엔딩은 버튼 없음 — false 유지)
            root.GetComponent<CanvasGroup>().blocksRaycasts = true;
            root.transform.Find("BG").GetComponent<Image>().raycastTarget = true; // 뒤 클릭 차단
            root.SetActive(false); // GameFlow.Show가 켠다

            (Button startBtn, _) = MakeButton(root.transform, "StartButton", font, "게임 시작",
                new Vector2(0f, -150f), new Vector2(340f, 76f), 36f, skinned: true);

            // 프롤로그 스킵 토글 — 첫 엔딩 후에만 노출 (TitleScreenView.Show가 제어)
            (Button skipBtn, TMP_Text skipLabel) = MakeButton(root.transform, "SkipRow", font, "인트로 스킵: 꺼짐",
                new Vector2(0f, -250f), new Vector2(300f, 54f), 26f, skinned: true);
            skipBtn.gameObject.SetActive(false);

            BuildVolumeSlider(root.transform, font);

            (Button helpBtn, _) = MakeButton(root.transform, "HelpButton", font, "?",
                new Vector2(880f, 460f), new Vector2(64f, 64f), 34f);

            // 도움말 패널 (좌우 페이징)
            var panel = new GameObject("HelpPanel");
            panel.transform.SetParent(root.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(1160f, 760f);
            var panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0.05f, 0.05f, 0.07f, 0.98f);

            TMP_Text helpText = MakeText(panel.transform, "HelpText", font, "", 28f,
                new Vector2(0f, 30f), new Vector2(960f, 560f), new Color(0.88f, 0.85f, 0.78f));
            helpText.alignment = TextAlignmentOptions.TopLeft;
            TMP_Text pageLabel = MakeText(panel.transform, "PageLabel", font, "1 / 4", 24f,
                new Vector2(0f, -330f), new Vector2(200f, 40f), new Color(0.6f, 0.58f, 0.52f));
            (Button prevBtn, _) = MakeButton(panel.transform, "PrevButton", font, "<",
                new Vector2(-500f, -330f), new Vector2(72f, 56f), 30f);
            (Button nextBtn, _) = MakeButton(panel.transform, "NextButton", font, ">",
                new Vector2(500f, -330f), new Vector2(72f, 56f), 30f);
            (Button closeBtn, _) = MakeButton(panel.transform, "CloseButton", font, "X",
                new Vector2(540f, 340f), new Vector2(56f, 56f), 26f);
            panel.SetActive(false);

            Wire(view, "root", root);
            Wire(view, "startButton", startBtn);
            Wire(view, "skipRow", skipBtn.gameObject);
            Wire(view, "skipButton", skipBtn);
            Wire(view, "skipLabel", skipLabel);
            Wire(view, "helpButton", helpBtn);
            Wire(view, "helpPanel", panel);
            Wire(view, "helpText", helpText);
            Wire(view, "helpPageLabel", pageLabel);
            Wire(view, "helpPrevButton", prevBtn);
            Wire(view, "helpNextButton", nextBtn);
            Wire(view, "helpCloseButton", closeBtn);
            Wire(Object.FindFirstObjectByType<GameFlowController>(), "titleScreen", view);
        }

        private static (Button, TMP_Text) MakeButton(Transform parent, string name, TMP_FontAsset font,
            string label, Vector2 anchoredPos, Vector2 size, float fontSize, bool skinned = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            var image = go.AddComponent<Image>();
            var button = go.AddComponent<Button>();

            var normalSprite = skinned ? AssetDatabase.LoadAssetAtPath<Sprite>(ButtonNormalPath) : null;
            var hoverSprite = skinned ? AssetDatabase.LoadAssetAtPath<Sprite>(ButtonHoverPath) : null;
            if (normalSprite != null && hoverSprite != null)
            {
                // 아트 2단계 — 붓 자국 판 스킨 (normal/hover 같은 시드라 상태 전환 시 형태 안 튐)
                image.sprite = normalSprite;
                image.color = Color.white;
                button.transition = Selectable.Transition.SpriteSwap;
                button.spriteState = new SpriteState
                {
                    highlightedSprite = hoverSprite,
                    pressedSprite = hoverSprite,
                    selectedSprite = hoverSprite,
                };
            }
            else
            {
                image.color = new Color(0.16f, 0.15f, 0.17f, 0.95f);
                var colors = button.colors;
                colors.highlightedColor = new Color(0.3f, 0.28f, 0.3f);
                colors.pressedColor = new Color(0.42f, 0.38f, 0.35f);
                button.colors = colors;
            }

            TMP_Text text = MakeText(go.transform, "Label", font, label, fontSize,
                Vector2.zero, size, new Color(0.9f, 0.87f, 0.8f));
            return (button, text);
        }

        /// <summary>타이틀 볼륨 슬라이더 (아트 2단계 — 스킨 트랙/핸들 + VolumeSliderView(AudioListener.volume)).</summary>
        private static void BuildVolumeSlider(Transform parent, TMP_FontAsset font)
        {
            var go = new GameObject("VolumeSlider");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(30f, -340f);
            rect.sizeDelta = new Vector2(400f, 36f);

            var track = new GameObject("Track");
            track.transform.SetParent(go.transform, false);
            var trackRect = track.AddComponent<RectTransform>();
            trackRect.anchorMin = new Vector2(0f, 0.5f);
            trackRect.anchorMax = new Vector2(1f, 0.5f);
            trackRect.offsetMin = new Vector2(0f, -12f);
            trackRect.offsetMax = new Vector2(0f, 12f);
            var trackImage = track.AddComponent<Image>();
            trackImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SliderTrackPath);
            trackImage.raycastTarget = true; // 트랙 클릭 점프 이동

            var handleArea = new GameObject("HandleArea");
            handleArea.transform.SetParent(go.transform, false);
            var areaRect = handleArea.AddComponent<RectTransform>();
            areaRect.anchorMin = Vector2.zero;
            areaRect.anchorMax = Vector2.one;
            areaRect.offsetMin = new Vector2(18f, 0f);
            areaRect.offsetMax = new Vector2(-18f, 0f);

            var handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            var handleRect = handle.AddComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(36f, 36f);
            var handleImage = handle.AddComponent<Image>();
            handleImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SliderHandlePath);

            var slider = go.AddComponent<Slider>();
            slider.targetGraphic = handleImage;
            slider.handleRect = handleRect;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            var view = go.AddComponent<VolumeSliderView>();
            Wire(view, "slider", slider);

            MakeText(parent, "VolumeLabel", font, "소리", 26f,
                new Vector2(-260f, -340f), new Vector2(120f, 40f), new Color(0.62f, 0.6f, 0.54f));
        }

        private static void SetupGameOver()
        {
            var screenGo = GameObject.Find("Screens/GameOverScreen");
            var view = screenGo.GetComponent<GameOverScreenView>();
            if (view == null) view = screenGo.AddComponent<GameOverScreenView>();

            GameObject root = BuildScreenUi(screenGo, 25, new Color(0.03f, 0f, 0.01f, 0.96f),
                "…", null, "E — 타이틀로", out CanvasGroup group, out TMP_Text title);
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
                "…", null, "E — 타이틀로", out CanvasGroup group, out TMP_Text title);
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
                var hint = rootTr.Find("Hint");
                if (hint != null && !string.IsNullOrEmpty(hintText))
                {
                    hint.GetComponent<TMP_Text>().text = hintText; // 문구 개정 반영 (멱등 재실행)
                }
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
            if (!string.IsNullOrEmpty(hintText))
            {
                MakeText(root.transform, "Hint", font, hintText, 30f,
                    new Vector2(0f, -320f), new Vector2(1200f, 60f), new Color(0.62f, 0.6f, 0.54f));
            }

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
