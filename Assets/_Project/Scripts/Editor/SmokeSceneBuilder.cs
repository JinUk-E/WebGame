using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Morae.Smoke;

namespace Morae.EditorTools
{
    /// <summary>
    /// D1 WebGL 스모크 씬 프로그래매틱 생성기 (architecture.md §8.6).
    /// 메뉴 Morae/Build Smoke Scene 또는 SmokeBuild가 호출. 여러 번 실행해도 안전(덮어쓰기).
    /// </summary>
    public static class SmokeSceneBuilder
    {
        private const string FontsDir = "Assets/_Project/Art/Fonts";
        private const string FontTtfPath = FontsDir + "/Pretendard-Regular.ttf";
        private const string FontAssetPath = FontsDir + "/Pretendard-Regular SDF.asset";
        private const string ScenePath = "Assets/_Project/Scenes/SmokeTest.unity";
        private const string VolumeProfilePath = "Assets/_Project/Scenes/SmokeTestVolumeProfile.asset";
        private const string WhiteSpritePath = "Assets/_Project/Art/Smoke/white32.png";
        private const string MuffledClipPath = "Assets/_Project/Audio/Smoke/smoke_voice_muffled.wav";
        private const string ClearClipPath = "Assets/_Project/Audio/Smoke/smoke_voice_clear.wav";
        private const string SpriteLitMatPath =
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";

        [MenuItem("Morae/Build Smoke Scene")]
        public static void Build()
        {
            EnsureTmpEssentials();
            TMP_FontAsset fontAsset = EnsureFontAsset();
            Sprite whiteSprite = EnsureWhiteSprite();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildCamera();
            BuildLighting();
            BuildProps(whiteSprite);
            VolumeProfile profile = BuildVolume();
            var audio = BuildAudio();
            var ui = BuildUi(fontAsset);
            BuildEventSystem();
            WireController(audio.muffled, audio.clear, ui.overlay, ui.button);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SMOKE-BUILDER] 씬 저장 완료: {ScenePath} (VolumeProfile: {profile.name})");
        }

        // ---------- 에셋 준비 ----------

        private static void EnsureTmpEssentials()
        {
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0) return;

            string[] candidates =
            {
                "Packages/com.unity.ugui/TMP Essential Resources.unitypackage",
                "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage",
            };
            foreach (string candidate in candidates)
            {
                string physical = FileUtil.GetPhysicalPath(candidate);
                if (!string.IsNullOrEmpty(physical) && File.Exists(physical))
                {
                    Debug.Log($"[SMOKE-BUILDER] TMP Essential Resources 임포트: {candidate}");
                    AssetDatabase.ImportPackage(candidate, false);
                    AssetDatabase.Refresh();
                    return;
                }
            }
            Debug.LogWarning("[SMOKE-BUILDER] TMP Essential Resources 패키지를 찾지 못함 — TMP 렌더가 깨질 수 있음");
        }

        private static TMP_FontAsset EnsureFontAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null) return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>(FontTtfPath);
            if (font == null)
            {
                throw new FileNotFoundException($"폰트 없음: {FontTtfPath} — Pretendard-Regular.ttf를 먼저 배치할 것");
            }

            // §7.1: Dynamic + Multi Atlas ON, 아틀라스 1024²
            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: true);
            fontAsset.name = "Pretendard-Regular SDF";
            fontAsset.isMultiAtlasTexturesEnabled = true;

            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
            fontAsset.material.name = fontAsset.name + " Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            fontAsset.atlasTexture.name = fontAsset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SMOKE-BUILDER] TMP Dynamic 폰트 에셋 생성: {FontAssetPath}");
            return fontAsset;
        }

        private static Sprite EnsureWhiteSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
            if (existing != null) return existing;

            Directory.CreateDirectory(Path.GetDirectoryName(WhiteSpritePath));
            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            var pixels = new Color32[32 * 32];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(FileUtil.GetPhysicalPath(WhiteSpritePath), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(WhiteSpritePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(WhiteSpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 32f; // 1스프라이트 = 1유닛
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
        }

        // ---------- 씬 구성 ----------

        private static void BuildCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5.4f; // §3.4
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            go.AddComponent<AudioListener>();
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true; // Vignette 필수
        }

        private static void BuildLighting()
        {
            var root = new GameObject("Lighting");

            var globalGo = new GameObject("GlobalLight2D");
            globalGo.transform.SetParent(root.transform);
            var global = globalGo.AddComponent<Light2D>();
            global.lightType = Light2D.LightType.Global;
            global.intensity = 0.15f; // 어두운 베이스
            global.color = Color.white;

            var pointGo = new GameObject("PointLight2D_Warm");
            pointGo.transform.SetParent(root.transform);
            pointGo.transform.position = new Vector3(2.5f, 1.5f, 0f);
            var point = pointGo.AddComponent<Light2D>();
            point.lightType = Light2D.LightType.Point;
            point.intensity = 1.3f;
            point.color = new Color(1f, 0.72f, 0.45f); // 따뜻한 색
            point.pointLightInnerRadius = 0.5f;
            point.pointLightOuterRadius = 5f;
        }

        private static void BuildProps(Sprite whiteSprite)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(SpriteLitMatPath);
            if (mat == null)
            {
                Debug.LogWarning("[SMOKE-BUILDER] Sprite-Lit-Default.mat 로드 실패 — 기본 머티리얼 사용(조명 미반응 가능)");
            }

            var root = new GameObject("Room");
            CreateSprite(root, "Floor", whiteSprite, mat,
                new Vector3(0f, 0f, 0f), new Vector3(14f, 9f, 1f), new Color(0.45f, 0.42f, 0.40f), 0);
            CreateSprite(root, "Prop_NearLight", whiteSprite, mat,
                new Vector3(2.5f, 1.5f, 0f), Vector3.one, new Color(0.9f, 0.85f, 0.8f), 1);
            CreateSprite(root, "Prop_Mid", whiteSprite, mat,
                new Vector3(0f, -1f, 0f), Vector3.one, new Color(0.8f, 0.8f, 0.85f), 1);
            CreateSprite(root, "Prop_FarDark", whiteSprite, mat,
                new Vector3(-4.5f, -2.5f, 0f), Vector3.one, new Color(0.8f, 0.8f, 0.8f), 1);
        }

        private static void CreateSprite(GameObject parent, string name, Sprite sprite, Material mat,
            Vector3 pos, Vector3 scale, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            go.transform.position = pos;
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            if (mat != null) sr.sharedMaterial = mat;
        }

        private static VolumeProfile BuildVolume()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
                var vignette = profile.Add<Vignette>(true);
                vignette.name = "Vignette";
                vignette.intensity.Override(0.4f); // §8.6
                vignette.smoothness.Override(0.4f);
                AssetDatabase.AddObjectToAsset(vignette, profile);
                EditorUtility.SetDirty(profile);
            }

            var go = new GameObject("GlobalVolume");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            return profile;
        }

        private static (AudioSource muffled, AudioSource clear) BuildAudio()
        {
            var muffledClip = AssetDatabase.LoadAssetAtPath<AudioClip>(MuffledClipPath);
            var clearClip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClearClipPath);
            if (muffledClip == null || clearClip == null)
            {
                throw new FileNotFoundException("스모크 오디오 클립 없음 — gen_smoke_audio.py 실행 후 재시도");
            }

            var root = new GameObject("Audio");
            AudioSource Make(string name, AudioClip clip)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root.transform);
                var src = go.AddComponent<AudioSource>();
                src.clip = clip;
                src.playOnAwake = false; // 클릭 게이트 전 재생 금지 (§8.2)
                src.loop = true;
                src.volume = 0f;
                src.spatialBlend = 0f;
                return src;
            }
            return (Make("DoorSourceMuffled", muffledClip), Make("DoorSourceClear", clearClip));
        }

        private static (GameObject overlay, Button button) BuildUi(TMP_FontAsset fontAsset)
        {
            var canvasGo = new GameObject("UI");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f; // §3.4 Match 1(세로)
            canvasGo.AddComponent<GraphicRaycaster>();

            // 한글 렌더 확인 텍스트 (오버레이 뒤 — 클릭 후 노출)
            CreateTmpText(canvasGo.transform, "KoreanCheckText", fontAsset,
                "한글 렌더 확인 — 밀실 버티기, 포포포, 07:30", 48f,
                new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(1700f, 120f), new Color(0.92f, 0.9f, 0.88f));
            CreateTmpText(canvasGo.transform, "HintText", fontAsset,
                "Space: 뭉갬↔선명 크로스페이드 / 방향키: 입력 로그", 30f,
                new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(1700f, 80f), new Color(0.6f, 0.6f, 0.6f));

            // 클릭 게이트 오버레이 (전체 화면, 최상위)
            var overlay = new GameObject("ClickGateOverlay");
            overlay.transform.SetParent(canvasGo.transform, false);
            var rect = overlay.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = overlay.AddComponent<Image>();
            image.color = Color.black;
            var button = overlay.AddComponent<Button>();
            button.targetGraphic = image;
            CreateTmpText(overlay.transform, "ClickToStartText", fontAsset,
                "클릭하여 시작", 64f,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 160f), Color.white);

            return (overlay, button);
        }

        private static void CreateTmpText(Transform parent, string name, TMP_FontAsset fontAsset,
            string text, float size, Vector2 anchor, Vector2 anchoredPos, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = fontAsset;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            var rect = tmp.rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = sizeDelta;
        }

        private static void BuildEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        private static void WireController(AudioSource muffled, AudioSource clear, GameObject overlay, Button button)
        {
            var systems = new GameObject("Systems");
            var controller = systems.AddComponent<SmokeController>();
            var so = new SerializedObject(controller);
            so.FindProperty("muffledSource").objectReferenceValue = muffled;
            so.FindProperty("clearSource").objectReferenceValue = clear;
            so.FindProperty("clickGateOverlay").objectReferenceValue = overlay;
            so.FindProperty("clickGateButton").objectReferenceValue = button;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
