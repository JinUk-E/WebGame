using System.Collections.Generic;
using System.IO;
using Morae.Game.Core;
using Morae.Game.Data;
using Morae.Game.Gauges;
using Morae.Game.Interactions;
using Morae.Game.Player;
using Morae.Game.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Morae.EditorTools
{
    /// <summary>
    /// Main.unity 프로그래매틱 생성 (architecture §3.1 계층 그대로 — 에디터 수작업 배제).
    /// 소품은 도형(스모크 white32 스프라이트 재활용), 시스템 오브젝트 컴포넌트 연결·SO 참조 배선까지.
    /// 여러 번 실행해도 안전 (씬 전체를 새로 만들어 덮어쓴다).
    /// CLI: -executeMethod Morae.EditorTools.MainSceneBuilder.Build
    /// </summary>
    public static class MainSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        private const string VolumeProfilePath = "Assets/_Project/Scenes/MainVolumeProfile.asset";
        private const string WhiteSpritePath = "Assets/_Project/Art/Smoke/white32.png";
        private const string FontAssetPath = "Assets/_Project/Art/Fonts/Pretendard-Regular SDF.asset";
        private const string SpriteLitMatPath =
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";

        // §3.4 — 방 외벽 포함 14×9유닛, 벽 두께 0.4, 내부 바닥 13.2×8.2
        private const float RoomW = 14f;
        private const float RoomH = 9f;
        private const float WallT = 0.4f;

        /// <summary>씬 구성 참조 다발 — 배선 단계 전달용.</summary>
        private sealed class SystemsRefs
        {
            public GameFlowController Flow;
            public PhaseSequencer Sequencer;
            public AttackScheduler Scheduler;
            public SaltCorners Salt;
            public Sanity Sanity;
            public Talisman Talisman;
            public DebugHud Hud;
        }

        private sealed class RoomRefs
        {
            public DoorInteractable Door;
            public TvInteractable Tv;
            public PrayerInteractable Prayer;
            public JarInteractable Jar;
            public Transform ClockRoot;
            public readonly Transform[] SaltCorners = new Transform[CornerIndex.Count];
        }

        [MenuItem("Morae/Build Main Scene")]
        public static void Build()
        {
            DataAssetBuilder.Ensure(); // SO 4종 선행 (기존 튜닝 보존)

            var phaseTable = AssetDatabase.LoadAssetAtPath<PhaseTable>(DataAssetBuilder.PhaseTablePath);
            var attackTable = AssetDatabase.LoadAssetAtPath<AttackTable>(DataAssetBuilder.AttackTablePath);
            var balance = AssetDatabase.LoadAssetAtPath<BalanceConfig>(DataAssetBuilder.BalanceConfigPath);
            Sprite white = EnsureWhiteSprite();
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (fontAsset == null)
            {
                Debug.LogWarning("[MAIN-BUILDER] Pretendard SDF 폰트 에셋 없음 — ClockView 텍스트가 TMP 기본 폰트로 생성됨");
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildCamera();
            SystemsRefs systems = BuildSystems();
            RoomRefs room = BuildRoom(white);
            var player = BuildPlayer(white, balance);
            BuildLighting();
            BuildAudio();
            BuildUi();
            BuildScreensAndDirectors();
            BuildVolume(); // GlobalVolume + SanityFeedback (volume 배선은 내부에서)
            BuildEventSystem();
            var clockView = BuildClockText(room.ClockRoot, fontAsset);

            // ---- 배선 (SerializedObject — 프리팹/씬 직렬화에 안전하게 기록) ----
            Wire(systems.Sequencer, "phaseTable", phaseTable);
            Wire(systems.Flow, "config", balance);
            Wire(systems.Flow, "phaseSequencer", systems.Sequencer);
            Wire(systems.Flow, "attackScheduler", systems.Scheduler);
            Wire(systems.Flow, "sanity", systems.Sanity);
            Wire(systems.Flow, "player", player);
            Wire(player, "config", balance);
            WireInteractableConfigs(balance);
            Wire(clockView, "sequencer", systems.Sequencer);

            // 순서 4 — 소금·공격
            Wire(systems.Scheduler, "attackTable", attackTable);
            Wire(systems.Scheduler, "phaseTable", phaseTable);
            Wire(systems.Scheduler, "config", balance);
            Wire(systems.Scheduler, "sequencer", systems.Sequencer);
            Wire(systems.Scheduler, "salt", systems.Salt);
            Wire(systems.Scheduler, "sanity", systems.Sanity);
            Wire(systems.Scheduler, "player", player);
            Wire(systems.Scheduler, "tv", room.Tv);
            Wire(systems.Salt, "talisman", systems.Talisman);
            WireArray(systems.Salt, "cornerTransforms", room.SaltCorners);
            Wire(room.Prayer, "salt", systems.Salt);
            Wire(room.Prayer, "scheduler", systems.Scheduler);

            // 순서 5 — 이성·부적
            Wire(systems.Sanity, "config", balance);
            Wire(systems.Sanity, "sequencer", systems.Sequencer);
            Wire(systems.Sanity, "player", player);
            Wire(systems.Sanity, "tv", room.Tv);
            Wire(systems.Sanity, "talisman", systems.Talisman);
            Wire(systems.Talisman, "config", balance);
            Wire(systems.Talisman, "salt", systems.Salt);
            Wire(systems.Talisman, "sanity", systems.Sanity);
            Wire(room.Jar, "sanity", systems.Sanity);

            // 순서 6 — 문·게임오버
            Wire(room.Door, "flow", systems.Flow);
            Wire(room.Door, "talisman", systems.Talisman);

            // 디버그 HUD (개발 빌드 한정 — 필드는 UNITY_EDITOR에서 존재)
            Wire(systems.Hud, "flow", systems.Flow);
            Wire(systems.Hud, "sequencer", systems.Sequencer);
            Wire(systems.Hud, "scheduler", systems.Scheduler);
            Wire(systems.Hud, "salt", systems.Salt);
            Wire(systems.Hud, "sanity", systems.Sanity);
            Wire(systems.Hud, "talisman", systems.Talisman);
            Wire(systems.Hud, "player", player);
            Wire(systems.Hud, "door", room.Door);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureInBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"[MAIN-BUILDER] Main.unity 생성 완료: {ScenePath}");
        }

        // ---------- Systems ----------

        private static SystemsRefs BuildSystems()
        {
            var go = new GameObject("Systems");
            var refs = new SystemsRefs();
            refs.Sequencer = go.AddComponent<PhaseSequencer>();
            refs.Flow = go.AddComponent<GameFlowController>();
            refs.Scheduler = go.AddComponent<AttackScheduler>();
            refs.Salt = go.AddComponent<SaltCorners>();
            refs.Sanity = go.AddComponent<Sanity>();
            refs.Talisman = go.AddComponent<Talisman>();
            go.AddComponent<DebugTimeScale>();   // F1 배속 (에디터·개발 빌드 전용)
            go.AddComponent<DebugEventLogger>(); // GameEvents 전량 로그 — D2 완주 판정 근거
            go.AddComponent<DebugCheats>();      // F2 진짜 신호 강제 발화 (EventDirector 전 임시)
            refs.Hud = go.AddComponent<DebugHud>();
            // §4 순서 7↑(Epic 2)에서 추가: EventDirector, SoundRouter
            return refs;
        }

        // ---------- Room (소품 = 도형) ----------

        private static RoomRefs BuildRoom(Sprite white)
        {
            var refs = new RoomRefs();
            var mat = AssetDatabase.LoadAssetAtPath<Material>(SpriteLitMatPath);
            var root = new GameObject("Room");

            // 바닥 + 벽 4면 (벽은 물리 콜라이더 — 플레이어 이동 경계)
            MakeSprite(root.transform, "Floor", white, mat, Vector2.zero, new Vector2(RoomW, RoomH),
                new Color(0.16f, 0.15f, 0.14f), 0);
            MakeWall(root.transform, "Wall_Top", white, mat, new Vector2(0f, (RoomH - WallT) * 0.5f), new Vector2(RoomW, WallT));
            MakeWall(root.transform, "Wall_Bottom", white, mat, new Vector2(0f, -(RoomH - WallT) * 0.5f), new Vector2(RoomW, WallT));
            MakeWall(root.transform, "Wall_Left", white, mat, new Vector2(-(RoomW - WallT) * 0.5f, 0f), new Vector2(WallT, RoomH));
            MakeWall(root.transform, "Wall_Right", white, mat, new Vector2((RoomW - WallT) * 0.5f, 0f), new Vector2(WallT, RoomH));

            // 소품 (§3.4 기준 크기, 위치는 도형 단계 가안) — 트리거 = 상호작용 범위
            var doorGo = MakeProp(root.transform, "Door", white, mat, new Vector2(-6.6f, 0f),
                new Vector2(0.4f, 1.6f), new Color(0.35f, 0.24f, 0.15f), new Vector2(2.2f, 3.0f));
            refs.Door = doorGo.AddComponent<DoorInteractable>(); // pushDirection 기본값 left = 좌측 벽 배치와 일치

            MakeProp(root.transform, "Window", white, mat, new Vector2(0f, 4.3f),
                new Vector2(1.8f, 0.4f), new Color(0.25f, 0.3f, 0.42f), Vector2.zero); // 상호작용 없음 — 시각물

            refs.ClockRoot = MakeProp(root.transform, "Clock", white, mat, new Vector2(2.5f, 4.05f),
                new Vector2(0.8f, 0.8f), new Color(0.55f, 0.5f, 0.4f), Vector2.zero).transform;

            var tvGo = MakeProp(root.transform, "TV", white, mat, new Vector2(4.8f, -2.2f),
                new Vector2(1.2f, 0.8f), new Color(0.2f, 0.22f, 0.25f), new Vector2(2.6f, 2.2f));
            refs.Tv = tvGo.AddComponent<TvInteractable>();

            var buddhaGo = MakeProp(root.transform, "Buddha", white, mat, new Vector2(-2.5f, 2.2f),
                new Vector2(0.8f, 0.8f), new Color(0.7f, 0.6f, 0.35f), new Vector2(2.2f, 2.2f));
            refs.Prayer = buddhaGo.AddComponent<PrayerInteractable>();

            var blanketGo = MakeProp(root.transform, "Blanket", white, mat, new Vector2(1.5f, -3f),
                new Vector2(2f, 1.4f), new Color(0.4f, 0.3f, 0.35f), new Vector2(3.2f, 2.6f));
            blanketGo.AddComponent<BlanketInteractable>();

            var jarGo = MakeProp(root.transform, "Jar", white, mat, new Vector2(-4.8f, -3.2f),
                new Vector2(0.5f, 0.5f), new Color(0.65f, 0.65f, 0.6f), new Vector2(1.8f, 1.8f));
            refs.Jar = jarGo.AddComponent<JarInteractable>();

            // 소금 4귀퉁이 — Interactable 아님(시각물, §3.1). CornerIndex 규약: 0=좌상 1=우상 2=좌하 3=우하
            // Transform은 SaltCorners.cornerTransforms에 배선 (FarthestFromPlayer 해석 기준점)
            Vector2[] corners = { new Vector2(-6f, 3.5f), new Vector2(6f, 3.5f), new Vector2(-6f, -3.5f), new Vector2(6f, -3.5f) };
            for (int i = 0; i < corners.Length; i++)
            {
                refs.SaltCorners[i] = MakeProp(root.transform, $"SaltCorner_{i}", white, mat, corners[i],
                    new Vector2(0.6f, 0.6f), new Color(0.95f, 0.95f, 0.92f), Vector2.zero).transform;
            }
            return refs;
        }

        // ---------- Player ----------

        private static PlayerController BuildPlayer(Sprite white, BalanceConfig balance)
        {
            var go = new GameObject("Player");
            go.transform.position = new Vector3(0f, -1f, 0f);

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = new Vector3(0.7f, 0.9f, 1f); // §3.4 플레이어 0.7×0.9
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = white;
            sr.color = new Color(0.85f, 0.8f, 0.7f);
            sr.sortingOrder = 2;

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.35f; // §3.4

            var controller = go.AddComponent<PlayerController>();
            go.AddComponent<PlayerInteraction>();
            return controller;
        }

        // ---------- Lighting (§3.1 — 값은 골격, 제어는 LightingController가 Epic 2에서) ----------

        private static void BuildLighting()
        {
            var root = new GameObject("Lighting");

            MakeLight(root.transform, "GlobalLight2D", Light2D.LightType.Global, Vector2.zero,
                new Color(0.75f, 0.78f, 0.9f), 0.12f, 0f);
            MakeLight(root.transform, "TVLight", Light2D.LightType.Point, new Vector2(4.8f, -2.2f),
                new Color(0.55f, 0.7f, 1f), 0f, 4f); // 꺼진 상태로 시작 (TVToggled 구독은 Epic 2)
            Vector2[] corners = { new Vector2(-6f, 3.5f), new Vector2(6f, 3.5f), new Vector2(-6f, -3.5f), new Vector2(6f, -3.5f) };
            for (int i = 0; i < corners.Length; i++)
            {
                MakeLight(root.transform, $"CornerLight_{i}", Light2D.LightType.Point, corners[i],
                    new Color(1f, 0.95f, 0.85f), 0.25f, 2f);
            }
            MakeLight(root.transform, "WindowDawnLight", Light2D.LightType.Point, new Vector2(0f, 4.3f),
                new Color(0.5f, 0.6f, 0.9f), 0f, 5f); // 여명 0 — Dawn01 연동은 Epic 2
        }

        // ---------- Audio (§3.1 — 소스 노드만, 클립·SoundRouter는 이후 단계) ----------

        private static void BuildAudio()
        {
            var root = new GameObject("Audio");
            string[] names =
            {
                "CornerSource_0", "CornerSource_1", "CornerSource_2", "CornerSource_3",
                "DoorSourceMuffled", "DoorSourceClear",
                "WindowSource", "RoomSource", "PhoneSource", "HeartbeatSource", "AmbienceSource",
            };
            foreach (string name in names)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root.transform);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false; // §8.2 오디오 게이트 전 재생 금지
                src.spatialBlend = 0f;
                src.volume = 0f;
            }
        }

        // ---------- UI / Screens / Directors (자리만 — Epic 2 담당) ----------

        private static void BuildUi()
        {
            var canvasGo = new GameObject("UI");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f; // §3.4 Match 1(세로)
            canvasGo.AddComponent<GraphicRaycaster>();

            MakeUiPlaceholder(canvasGo.transform, "SubtitleView");
            MakeUiPlaceholder(canvasGo.transform, "InteractPrompt");
        }

        private static void BuildScreensAndDirectors()
        {
            var screens = new GameObject("Screens");
            new GameObject("TitleScreen").transform.SetParent(screens.transform);
            new GameObject("GameOverScreen").transform.SetParent(screens.transform);
            new GameObject("EndingScreen").transform.SetParent(screens.transform);

            var directors = new GameObject("Directors");
            new GameObject("PrologueDirector").transform.SetParent(directors.transform);
            new GameObject("EndingDirector").transform.SetParent(directors.transform);
        }

        // ---------- Clock (월드 TMP + ClockView) ----------

        private static ClockView BuildClockText(Transform clockRoot, TMP_FontAsset fontAsset)
        {
            var textGo = new GameObject("ClockText");
            textGo.transform.SetParent(clockRoot, false);

            var tmp = textGo.AddComponent<TextMeshPro>();
            if (fontAsset != null) tmp.font = fontAsset;
            tmp.text = "01:00";
            tmp.fontSize = 4f; // 월드 TMP: ≈0.4유닛 높이 — 시계 0.8유닛 안
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.9f, 0.85f, 0.75f);
            tmp.rectTransform.sizeDelta = new Vector2(2f, 1f);
            var meshRenderer = textGo.GetComponent<MeshRenderer>();
            if (meshRenderer != null) meshRenderer.sortingOrder = 5;

            var view = clockRoot.gameObject.AddComponent<ClockView>();
            Wire(view, "label", tmp);
            return view;
        }

        // ---------- Camera / Volume / EventSystem ----------

        private static void BuildCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5.4f; // §3.4 — 고정 카메라, 추적 없음
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            go.AddComponent<AudioListener>();
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true; // Vignette
        }

        private static void BuildVolume()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
                var vignette = profile.Add<Vignette>(true);
                vignette.name = "Vignette";
                vignette.intensity.Override(0.35f); // 기본값(calm) — 런타임 제어는 SanityFeedback
                vignette.smoothness.Override(0.4f);
                AssetDatabase.AddObjectToAsset(vignette, profile);
                EditorUtility.SetDirty(profile);
            }

            var go = new GameObject("GlobalVolume");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            // §4 순서 5 — 이성의 유일한 표현 (SanityChanged 구독, volume.profile 런타임 복제본에만 씀)
            var feedback = go.AddComponent<SanityFeedback>();
            Wire(feedback, "volume", volume);
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

        // ---------- 헬퍼 ----------

        private static Sprite EnsureWhiteSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
            if (existing != null) return existing;

            Directory.CreateDirectory(Path.GetDirectoryName(FileUtil.GetPhysicalPath(WhiteSpritePath)));
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

        private static GameObject MakeSprite(Transform parent, string name, Sprite sprite, Material mat,
            Vector2 pos, Vector2 size, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            if (mat != null) sr.sharedMaterial = mat;
            return go;
        }

        private static void MakeWall(Transform parent, string name, Sprite sprite, Material mat, Vector2 pos, Vector2 size)
        {
            var go = MakeSprite(parent, name, sprite, mat, pos, size, new Color(0.32f, 0.29f, 0.26f), 1);
            go.AddComponent<BoxCollider2D>(); // 스프라이트 1×1 × localScale = 벽 크기 그대로
        }

        /// <summary>소품 루트(스케일 1) + 스케일된 Visual 자식 + (옵션) 트리거 콜라이더(상호작용 범위).</summary>
        private static GameObject MakeProp(Transform parent, string name, Sprite sprite, Material mat,
            Vector2 pos, Vector2 visualSize, Color color, Vector2 triggerSize)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.position = pos;

            var visual = MakeSprite(root.transform, "Visual", sprite, mat, pos, visualSize, color, 1);
            visual.transform.localPosition = Vector3.zero;

            if (triggerSize.sqrMagnitude > 0f)
            {
                var trigger = root.AddComponent<BoxCollider2D>();
                trigger.isTrigger = true;
                trigger.size = triggerSize; // 루트 스케일 1 — 사이즈가 곧 월드 크기
            }
            return root;
        }

        private static void MakeLight(Transform parent, string name, Light2D.LightType type, Vector2 pos,
            Color color, float intensity, float outerRadius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = pos;
            var light = go.AddComponent<Light2D>();
            light.lightType = type;
            light.color = color;
            light.intensity = intensity;
            if (type == Light2D.LightType.Point)
            {
                light.pointLightInnerRadius = 0.3f;
                light.pointLightOuterRadius = outerRadius;
            }
        }

        private static void MakeUiPlaceholder(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Wire(Component target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[MAIN-BUILDER] 배선 실패 — {target.GetType().Name}.{fieldName} 프로퍼티 없음");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireArray(Component target, string fieldName, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null || !prop.isArray)
            {
                Debug.LogError($"[MAIN-BUILDER] 배열 배선 실패 — {target.GetType().Name}.{fieldName} 배열 프로퍼티 없음");
                return;
            }
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireInteractableConfigs(BalanceConfig balance)
        {
            // 씬 새로 생성 직후 1회 — 핫패스 아님
            foreach (Interactable interactable in Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None))
            {
                Wire(interactable, "config", balance);
            }
        }

        private static void EnsureInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}
