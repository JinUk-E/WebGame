using Morae.Game.Core;
using Morae.Game.Data;
using Morae.Game.Gauges;
using Morae.Game.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Morae.EditorTools
{
    /// <summary>
    /// 명세 v0.5 배선 — 흑화 즉시 대가(감광·속삭임) / 어둠 속 실루엣 / 프롤로그 강제 학습.
    /// 저장된 Main.unity에 **추가·참조 배선만** 한다 (씬 재생성 없음, 화면 3종은 프리팹이 단일 진실 — 손대지 않는다).
    /// 멱등: 이미 있으면 참조만 갱신한다.
    ///
    /// 만드는 것: Lighting/BuddhaCandleLight (감광 예외② 등대), Silhouettes 루트(SilhouetteDirector).
    /// 잇는 것: LightingController.config/촛불, Sanity.salt, SoundManager 귀퉁이 4채널, PrologueDirector.config/scheduler.
    /// CLI: -executeMethod Morae.EditorTools.V05Setup.Setup
    /// </summary>
    public static class V05Setup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        private const string WhisperClipPath = "Assets/_Project/Audio/SFX_Corner/whisper_loop.wav";
        private const string SilhouetteSpritePath = "Assets/_Project/Art/Props/prop_silhouette.png";

        [MenuItem("Morae/Setup v0.5 (흑화 대가·실루엣·프롤로그 학습)")]
        public static void Setup()
        {
            EnsureScene();

            var balance = AssetDatabase.LoadAssetAtPath<BalanceConfig>(DataAssetBuilder.BalanceConfigPath);
            if (balance == null)
            {
                Debug.LogError("[V05-SETUP] BalanceConfig 없음 — 먼저 Morae/Build Data Assets + 음성 재배선 실행");
                return;
            }

            SetupCandleLight(balance);
            SetupSanity();
            SetupCornerWhispers(balance);
            SetupSilhouettes(balance);
            SetupPrologueTraining(balance);

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[V05-SETUP] v0.5 배선·씬 저장 완료");
        }

        private static void EnsureScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        // ---------- 감광 예외② — 불상 촛불 (항상 일정 밝기, 암전 시 등대) ----------

        private static void SetupCandleLight(BalanceConfig balance)
        {
            var lightingRoot = GameObject.Find("Lighting");
            var controller = Object.FindFirstObjectByType<LightingController>();
            if (lightingRoot == null || controller == null)
            {
                Debug.LogError("[V05-SETUP] Lighting 루트 또는 LightingController 없음 — 감광 배선 실패");
                return;
            }

            var buddha = GameObject.Find("Room/Buddha");
            Vector3 pos = buddha != null ? buddha.transform.position : new Vector3(-2.5f, 2.2f, 0f);

            Transform existing = lightingRoot.transform.Find("BuddhaCandleLight");
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject("BuddhaCandleLight");
                go.transform.SetParent(lightingRoot.transform);
                var created = go.AddComponent<Light2D>();
                created.lightType = Light2D.LightType.Point;
                created.color = new Color(1f, 0.82f, 0.55f); // 촛불 — 전조(붉은 점멸)와 색이 겹치지 않는 따뜻한 황색
                created.pointLightOuterRadius = 2.8f;
                created.pointLightInnerRadius = 0.4f;
            }
            go.transform.position = pos;
            // 강도는 LightingController.candleIntensity가 Start에서 덮어쓴다 — 여기서 값을 정하지 않는다(매직 넘버 중복 방지).
            var light = go.GetComponent<Light2D>();

            Wire(controller, "config", balance);
            Wire(controller, "buddhaCandleLight", light);
        }

        // ---------- 흑화 상시 이성 드레인 ----------

        private static void SetupSanity()
        {
            var sanity = Object.FindFirstObjectByType<Sanity>();
            var salt = Object.FindFirstObjectByType<SaltCorners>();
            if (sanity == null || salt == null)
            {
                Debug.LogError("[V05-SETUP] Sanity/SaltCorners 없음 — 흑화 드레인 배선 실패");
                return;
            }
            Wire(sanity, "salt", salt);
        }

        // ---------- 귀퉁이 속삭임 4채널 ----------

        private static void SetupCornerWhispers(BalanceConfig balance)
        {
            var sound = Object.FindFirstObjectByType<SoundManager>();
            if (sound == null)
            {
                Debug.LogError("[V05-SETUP] SoundManager 없음 — 먼저 Morae/Setup Sound Manager 실행");
                return;
            }

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(WhisperClipPath);
            if (clip == null) Debug.LogError($"[V05-SETUP] 속삭임 클립 없음 — {WhisperClipPath}");

            Wire(sound, "sfxCornerWhisper", clip);
            Wire(sound, "balance", balance);

            var sources = new AudioSource[CornerIndex.Count];
            for (int i = 0; i < CornerIndex.Count; i++)
            {
                // 없으면 만든다 — 다른 셋업과 동일 정책. 이 오브젝트가 사라지면 속삭임 4채널이 통째로 죽는데
                // LogError만 하고 넘어가면 스크립트로 복구할 방법이 없다.
                var go = GameObject.Find($"Audio/CornerSource_{i}");
                if (go == null)
                {
                    var audioRoot = GameObject.Find("Audio");
                    if (audioRoot == null) audioRoot = new GameObject("Audio");
                    go = new GameObject($"CornerSource_{i}");
                    go.transform.SetParent(audioRoot.transform);
                    Debug.Log($"[V05-SETUP] Audio/CornerSource_{i} 신설");
                }
                var src = go.GetComponent<AudioSource>();
                if (src == null) src = go.AddComponent<AudioSource>();
                sources[i] = src;

                // 소스를 실제 귀퉁이 좌표로 옮긴다 — 정위를 3D 위치로 하기 때문(WebGL에 panStereo 바인딩이 없다).
                // 씬 빌더는 이 소스들을 원점에 만들어 뒀다.
                var corner = GameObject.Find($"Room/SaltCorner_{i}");
                if (corner != null) go.transform.position = corner.transform.position;
                else Debug.LogWarning($"[V05-SETUP] Room/SaltCorner_{i} 없음 — CornerSource_{i} 위치 미설정(정위 안 됨)");
            }
            WireArray(sound, "cornerSources", sources);
        }

        // ---------- 어둠 속 실루엣 ----------

        private static void SetupSilhouettes(BalanceConfig balance)
        {
            var rootGo = GameObject.Find("Silhouettes");
            if (rootGo == null) rootGo = new GameObject("Silhouettes");
            rootGo.transform.position = Vector3.zero;

            var director = rootGo.GetComponent<SilhouetteDirector>();
            if (director == null) director = rootGo.AddComponent<SilhouetteDirector>();

            var player = GameObject.Find("Player");
            var buddha = GameObject.Find("Room/Buddha");
            var corners = new Transform[CornerIndex.Count];
            for (int i = 0; i < CornerIndex.Count; i++)
            {
                var go = GameObject.Find($"Room/SaltCorner_{i}");
                corners[i] = go != null ? go.transform : null;
            }

            // 절차 생성 실루엣 (Tools/art-gen/gen_silhouette.py, 시드 42500) — 무채색·뭉갠 윤곽.
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SilhouetteSpritePath);
            // 조명 미수광 머티리얼 — 감광이 심할수록 실루엣만 남아야 한다
            var unlit = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");

            Wire(director, "config", balance);
            Wire(director, "silhouetteSprite", sprite);
            Wire(director, "unlitMaterial", unlit);
            Wire(director, "player", player != null ? player.transform : null);
            Wire(director, "altar", buddha != null ? buddha.transform : null);
            WireArray(director, "cornerTransforms", corners);
        }

        // ---------- 프롤로그 강제 학습 ----------

        private static void SetupPrologueTraining(BalanceConfig balance)
        {
            var prologue = Object.FindFirstObjectByType<PrologueDirector>();
            var scheduler = Object.FindFirstObjectByType<AttackScheduler>();
            if (prologue == null || scheduler == null)
            {
                Debug.LogError("[V05-SETUP] PrologueDirector/AttackScheduler 없음 — 강제 학습 배선 실패");
                return;
            }
            Wire(prologue, "config", balance);
            Wire(prologue, "scheduler", scheduler);

            // 프롤로그 중 문·TV·이불 차단 게이트 (기도만 허용) — 미배선이면 게이트가 없는 것과 같다
            var interaction = Object.FindFirstObjectByType<Morae.Game.Player.PlayerInteraction>();
            var flow = Object.FindFirstObjectByType<GameFlowController>();
            if (interaction != null && flow != null) Wire(interaction, "flow", flow);
            else Debug.LogError("[V05-SETUP] PlayerInteraction/GameFlowController 없음 — 프롤로그 상호작용 게이트 미배선");
        }

        // ---------- 헬퍼 (Art2Setup 선례 — SerializedObject 배선 + 재확인 로그) ----------

        private static void Wire(Component target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[V05-SETUP] 배선 실패 — {target.GetType().Name}.{field} 프로퍼티 없음");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();

            Object check = new SerializedObject(target).FindProperty(field).objectReferenceValue;
            Debug.Log($"[V05-SETUP] {target.GetType().Name}.{field} = " +
                      $"{(check != null ? check.name : value == null ? "(의도된 null)" : "NULL!")}");
        }

        private static void WireArray(Component target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[V05-SETUP] 배선 실패 — {target.GetType().Name}.{field} 프로퍼티 없음");
                return;
            }
            prop.arraySize = values.Length;
            int nulls = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == null) nulls++;
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[V05-SETUP] {target.GetType().Name}.{field} = {values.Length}개 (NULL {nulls})");
        }
    }
}
