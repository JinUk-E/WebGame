using System.IO;
using Morae.Game.Presentation;
using Morae.Game.Player;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Morae.EditorTools
{
    /// <summary>
    /// 모바일 온스크린 컨트롤(가상 스틱 + 상호작용 버튼)을 저장된 Main.unity에 배선 (씬 재생성 없음, 멱등).
    /// 링·노브 스프라이트는 절차 생성 — 외부 에셋 불필요 (D4Setup 하트 선례).
    /// <para>
    /// 2026-08-06: 화면 3종은 프리팹(<c>Assets/_Project/Prefab/Screens/</c>)이 단일 진실이다.
    /// "이어폰 권장" 안내(MobileAudioHint)와 게임오버·엔딩 하단 힌트는 <b>프리팹 안에 들어 있고</b>,
    /// 이 셋업은 그것들을 <b>찾아서 참조만</b> 한다 (생성·파괴 없음 — 예전 생성 코드는 프리팹을 덮어써서 제거).
    /// 따라서 D4Setup/Art2Setup 실행 후 이 셋업을 다시 돌려야 했던 규칙도 없어졌다.
    /// </para>
    /// CLI: -executeMethod Morae.EditorTools.TouchSetup.Setup
    /// </summary>
    public static class TouchSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        private const string FontAssetPath = "Assets/_Project/Art/Fonts/Pretendard-Regular SDF.asset";
        private const string RingPath = "Assets/_Project/Art/UI/ui_touch_ring.png";
        private const string KnobPath = "Assets/_Project/Art/UI/ui_touch_knob.png";

        // 좌하단 부적 UI(TalismanStatus: x 36~136, y 36~336)와 겹치지 않는 안쪽 배치
        private static readonly Vector2 StickPos = new Vector2(330f, 250f);
        private static readonly Vector2 ButtonPos = new Vector2(-300f, 250f);
        private const float StickSize = 260f;
        private const float KnobSize = 112f;
        private const float ButtonSize = 240f;

        [MenuItem("Morae/Setup Touch Controls (모바일 온스크린)")]
        public static void Setup()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var canvas = GameObject.Find("UI");
            if (canvas == null)
            {
                Debug.LogError("[TOUCH-SETUP] UI 캔버스를 찾지 못했다 — Main 씬 확인 필요");
                return;
            }

            Sprite ring = EnsureRingSprite();
            Sprite knob = EnsureKnobSprite();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);

            // 뷰 홀더는 controlsRoot 바깥 — 게임오버에서 controlsRoot를 꺼도 Update가 살아 있어야 한다
            GameObject holder = EnsureChild(canvas.transform, "TouchControls", stretch: true);
            var view = holder.GetComponent<TouchControlsView>();
            if (view == null) view = holder.AddComponent<TouchControlsView>();

            GameObject controls = EnsureChild(holder.transform, "Controls", stretch: true);

            // ---- 가상 스틱 (좌하단) ----
            GameObject stick = EnsureChild(controls.transform, "StickBase", stretch: false);
            var stickRect = (RectTransform)stick.transform;
            SetAnchored(stickRect, new Vector2(0f, 0f), StickPos, new Vector2(StickSize, StickSize));
            var stickImage = EnsureImage(stick, ring, new Color(1f, 0.97f, 0.92f, 0.20f));

            GameObject knobGo = EnsureChild(stick.transform, "Knob", stretch: false);
            var knobRect = (RectTransform)knobGo.transform;
            SetAnchored(knobRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(KnobSize, KnobSize));
            EnsureImage(knobGo, knob, new Color(1f, 0.97f, 0.92f, 0.34f));

            // ---- 상호작용 버튼 (우하단) ----
            GameObject button = EnsureChild(controls.transform, "InteractButton", stretch: false);
            var buttonRect = (RectTransform)button.transform;
            SetAnchored(buttonRect, new Vector2(1f, 0f), ButtonPos, new Vector2(ButtonSize, ButtonSize));
            EnsureImage(button, ring, new Color(1f, 0.93f, 0.82f, 0.30f));

            GameObject inner = EnsureChild(button.transform, "Inner", stretch: false);
            var innerRect = (RectTransform)inner.transform;
            SetAnchored(innerRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(ButtonSize * 0.62f, ButtonSize * 0.62f));
            EnsureImage(inner, knob, new Color(0.86f, 0.78f, 0.62f, 0.20f));

            TMP_Text label = EnsureLabel(button.transform, "Label", font);
            var labelRect = label.rectTransform;
            SetAnchored(labelRect, new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(460f, 64f));
            label.fontSize = 30f;
            label.color = new Color(0.88f, 0.84f, 0.76f, 0.95f);
            label.alignment = TextAlignmentOptions.Center;
            label.text = string.Empty;

            // ---- 타이틀 "이어폰 권장" 안내 (터치 기기에서만 켜짐 — TitleScreen 프리팹 소유) ----
            GameObject audioHint = FindTitleAudioHint();

            // ---- 게임오버·엔딩 하단 힌트 (터치 문구로 교체될 대상) ----
            TMP_Text gameOverHint = FindHint("Screens/GameOverScreen");
            TMP_Text endingHint = FindHint("Screens/EndingScreen");

            Wire(view, "interaction", Object.FindFirstObjectByType<PlayerInteraction>());
            Wire(view, "controlsRoot", controls);
            Wire(view, "stickBase", stickRect);
            Wire(view, "stickKnob", knobRect);
            Wire(view, "interactButton", buttonRect);
            Wire(view, "interactButtonRoot", button);
            Wire(view, "interactLabel", label);
            Wire(view, "mobileAudioHint", audioHint);
            WireArray(view, "keyboardHints", new Object[] { gameOverHint, endingHint });

            // 절차 생성 스프라이트가 비었을 때를 대비한 방어 로그
            if (stickImage.sprite == null) Debug.LogWarning("[TOUCH-SETUP] 링 스프라이트 로드 실패");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[TOUCH-SETUP] 온스크린 컨트롤 배선·씬 저장 완료");
        }

        // ---------- 타이틀 안내 (프리팹 소유 — 찾기만 한다) ----------

        /// <summary>
        /// TitleScreen 프리팹 안의 "이어폰 권장" 안내를 찾는다. 기본 비활성이며
        /// <see cref="TouchControlsView"/>가 터치 기기에서만 켠다 — 여기서 만들거나 지우지 않는다.
        /// </summary>
        private static GameObject FindTitleAudioHint()
        {
            var titleRoot = GameObject.Find("Screens/TitleScreen/Root");
            if (titleRoot == null)
            {
                Debug.LogError("[TOUCH-SETUP] Screens/TitleScreen/Root 없음 — 타이틀 프리팹 인스턴스 확인 필요");
                return null;
            }

            var hint = titleRoot.transform.Find("MobileAudioHint");
            if (hint == null)
            {
                Debug.LogError("[TOUCH-SETUP] TitleScreen 프리팹에 MobileAudioHint가 없다 — "
                               + "프리팹을 열어 Root 아래에 안내 텍스트를 추가할 것 (모바일 이어폰 권장 문구)");
                return null;
            }
            return hint.gameObject;
        }

        /// <summary>게임오버·엔딩 프리팹의 하단 힌트("E — 타이틀로") — 터치 기기에서 탭 문구로 교체될 대상.</summary>
        private static TMP_Text FindHint(string screenPath)
        {
            var screen = GameObject.Find(screenPath);
            var root = screen != null ? screen.transform.Find("Root") : null;
            var hint = root != null ? root.Find("Hint") : null;
            var text = hint != null ? hint.GetComponent<TMP_Text>() : null;
            if (text == null)
            {
                Debug.LogError($"[TOUCH-SETUP] {screenPath}/Root/Hint 없음 — 프리팹에 하단 힌트 텍스트를 넣을 것 "
                               + "(모바일에서 '화면을 탭하면 타이틀로'로 교체되는 자리)");
            }
            return text;
        }

        // ---------- 헬퍼 ----------

        private static GameObject EnsureChild(Transform parent, string name, bool stretch)
        {
            var found = parent.Find(name);
            GameObject go;
            if (found != null)
            {
                go = found.gameObject;
            }
            else
            {
                go = new GameObject(name);
                go.transform.SetParent(parent, false);
            }

            var rect = go.GetComponent<RectTransform>();
            if (rect == null) rect = go.AddComponent<RectTransform>();
            if (stretch)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            go.SetActive(true);
            return go;
        }

        private static void SetAnchored(RectTransform rect, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static Image EnsureImage(GameObject go, Sprite sprite, Color color)
        {
            var image = go.GetComponent<Image>();
            if (image == null) image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false; // 직접 히트 테스트 — EventSystem 레이캐스트와 경합 금지
            return image;
        }

        private static TMP_Text EnsureLabel(Transform parent, string name, TMP_FontAsset font)
        {
            var found = parent.Find(name);
            GameObject go;
            if (found != null)
            {
                go = found.gameObject;
            }
            else
            {
                go = new GameObject(name);
                go.transform.SetParent(parent, false);
            }
            var text = go.GetComponent<TextMeshProUGUI>();
            if (text == null) text = go.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.raycastTarget = false;
            return text;
        }

        // ---------- 절차 생성 스프라이트 ----------

        private static Sprite EnsureRingSprite() => EnsureCircleSprite(RingPath, hollow: true);

        private static Sprite EnsureKnobSprite() => EnsureCircleSprite(KnobPath, hollow: false);

        private static Sprite EnsureCircleSprite(string path, bool hollow)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            const int size = 192;
            const float outer = 0.94f;
            const float inner = 0.80f; // 링 두께
            Directory.CreateDirectory(Path.GetDirectoryName(FileUtil.GetPhysicalPath(path)));

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    float x = (px + 0.5f) / size * 2f - 1f;
                    float y = (py + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(x * x + y * y);

                    float a;
                    if (hollow)
                    {
                        // 안팎 경계를 부드럽게 — 저해상도 화면에서 계단 방지
                        float outerEdge = Mathf.InverseLerp(outer, outer - 0.05f, r);
                        float innerEdge = Mathf.InverseLerp(inner - 0.05f, inner, r);
                        a = Mathf.Clamp01(Mathf.Min(outerEdge, innerEdge));
                    }
                    else
                    {
                        a = Mathf.Clamp01(Mathf.InverseLerp(outer, outer - 0.08f, r));
                    }

                    pixels[py * size + px] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(FileUtil.GetPhysicalPath(path), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void Wire(Component target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[TOUCH-SETUP] 필드 없음: {target.GetType().Name}.{field}");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[TOUCH-SETUP] {target.GetType().Name}.{field} = {(value != null ? value.name : "NULL!")}");
        }

        private static void WireArray(Component target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[TOUCH-SETUP] 배열 필드 없음: {target.GetType().Name}.{field}");
                return;
            }
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[TOUCH-SETUP] {target.GetType().Name}.{field}[{values.Length}] 배선");
        }
    }
}
