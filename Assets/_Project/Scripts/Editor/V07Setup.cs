using Morae.Game.Core;
using Morae.Game.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Morae.EditorTools
{
    /// <summary>
    /// v0.7 배선 — <b>여명 체감</b>(바닥 창틀 빛 무늬 · 창호지 색 단계 · 실내 여명 보정 축소).
    /// 저장된 Main.unity에 <b>추가·값 갱신만</b> 한다 (씬 재생성 없음, 화면 3종 프리팹 무수정).
    /// 멱등: 이미 있으면 위치·참조·수치만 다시 맞춘다.
    ///
    /// <para>
    /// 이 스크립트가 하는 일은 셋인데, 셋 다 <b>씬·프리팹에 이미 굳어 있는 값</b>이라
    /// 코드만 고쳐서는 게임에 반영되지 않는 종류다([[씬-직렬화가-코드-기본값을-이긴다]]):
    /// <list type="number">
    ///   <item><b>Room/Window/Visual/Sky(창호지)를 무광으로</b> + 밤 색으로 초기화 →
    ///         방 조도(흑화 감광·페이즈 bias·학습 배율)가 창에 <b>닿을 수 없게</b> 만든다.
    ///         Room 하위를 만졌으므로 끝에서 <see cref="RoomPrefabSetup.ApplyRoomToPrefab"/>을 부른다.</item>
    ///   <item><b>Dawn 루트 + 바닥 무늬 두 겹</b> 생성 — Room 프리팹 <b>밖</b>이다
    ///         (인스턴스 안에 만든 자식은 프리팹을 되돌릴 때 조용히 사라진다. Stage/StandMarker 선례).</item>
    ///   <item><b>globalDawnBoost 0.18 → 0.06</b> — 방이 창과 함께 밝아지면 창이 광원으로 안 보인다.
    ///         씬에 0.18이 직렬화돼 있어 C# 초기값만 고치면 아무 일도 일어나지 않는다.</item>
    /// </list>
    /// </para>
    ///
    /// CLI: -executeMethod Morae.EditorTools.V07Setup.Setup
    /// </summary>
    public static class V07Setup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        private const string GridSpritePath = "Assets/_Project/Art/Props/prop_dawn_patch_grid.png";
        private const string HazeSpritePath = "Assets/_Project/Art/Props/prop_dawn_patch_haze.png";

        private const string DawnRootName = "Dawn";
        private const string HazeName = "FloorPatchHaze";
        private const string GridName = "FloorPatchGrid";
        /// <summary>
        /// <b>창호지의 실체.</b> 배포 창 아트(<c>room_window.png</c> 136×83)는 <b>창 칸이 알파 0</b>인 나무틀이고,
        /// 그 뒤에 깔린 흰 쿼드(<c>Sky</c>, 정렬 3)가 칸을 통해 보이는 <b>종이/하늘</b>이다.
        /// 그래서 색을 얹을 대상은 창틀(<c>Visual</c>, 정렬 4)이 아니라 이 쿼드다 —
        /// 창틀에 얹으면 나무까지 물들고, 살의 실루엣이 사라진다.
        /// </summary>
        private const string WindowSkyPath = "Room/Window/Visual/Sky";

        /// <summary>실내 소품과 같은 층(0=바닥). 바닥보다는 위, 소품·플레이어보다는 아래여야 한다.</summary>
        private const int PatchSortingOrder = 1;

        /// <summary>
        /// 같은 정렬순서 안에서 <b>뒤</b>로 밀기 위한 z. 바닥에 떨어진 빛은 TV·소금 자국을 덮으면 안 된다
        /// (직교 카메라 + TransparencySortMode.Default = 시선축 거리 정렬. Buddha/Halo z=0.05의 선례).
        /// </summary>
        private const float PatchZ = 0.05f;

        /// <summary>명세 v0.7 §3 — 방이 함께 밝아지는 것을 줄여야 창문이 광원으로 도드라진다.</summary>
        private const float GlobalDawnBoostV07 = 0.06f;

        /// <summary>
        /// 배선 스냅샷을 전후로 남기고 셋업한다 — batchmode 1회 실행으로 "끊긴 참조 0"을 증명하기 위한 진입점.
        /// CLI: -executeMethod Morae.EditorTools.V07Setup.SetupWithAudit
        /// </summary>
        public static void SetupWithAudit()
        {
            EnsureScene();
            WiringAudit.Write("Tools/scene-audit/wiring-v07-before.txt");
            Setup();
            WiringAudit.Write("Tools/scene-audit/wiring-v07-after.txt");
            RoomPrefabSetup.AuditOverrides();
        }

        [MenuItem("Morae/Setup v0.7 (여명 체감 — 바닥 빛무늬·창호지 색)")]
        public static void Setup()
        {
            EnsureScene();

            Sprite grid = Require(GridSpritePath);
            Sprite haze = Require(HazeSpritePath);
            if (grid == null || haze == null) return;

            SpriteRenderer windowPaper = SetupWindowPaper();
            SetupGlobalDawnBoost();
            DawnWindowView view = SetupFloorPatch(grid, haze, windowPaper);

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // Room 하위(창 렌더러)를 만졌으므로 프리팹에 밀어 올린다 — 안 부르면 씬과 프리팹이 갈라진다
            RoomPrefabSetup.ApplyRoomToPrefab();

            Debug.Log($"[V07-SETUP] 여명 체감 배선 완료 — 창호지 무광 전환 / {DawnRootName} 바닥 무늬 2겹 / " +
                      $"globalDawnBoost {GlobalDawnBoostV07} " +
                      $"(view={(view != null ? "OK" : "NULL")})");
        }

        // ---------- ② 창호지 ----------

        /// <summary>
        /// 창호지를 <b>무광</b>으로 바꾼다. 이것이 v0.7의 핵심 한 줄이다 —
        /// Sprite-Lit이면 창의 밝기가 실내 전역광(= 흑화 감광 + 페이즈 bias + 학습 배율)에 곱해져
        /// <b>방어를 잘한 플레이어일수록 진실 채널이 흐려지는</b> 역전이 생긴다.
        /// 무광이면 <see cref="DawnStageModel.PaperColor"/>가 그대로 화면에 나간다.
        ///
        /// <para>
        /// 나무틀(<c>Visual</c>)은 <b>Lit 그대로 둔다</b> — 틀은 방의 물건이라 방과 함께 어두워져야 하고,
        /// 그래야 밝아진 창호지 위로 살의 실루엣이 떠서 "창이 광원"이라는 그림이 완성된다.
        /// </para>
        /// </summary>
        private static SpriteRenderer SetupWindowPaper()
        {
            GameObject go = FindByPath(WindowSkyPath);
            if (go == null)
            {
                Debug.LogError($"[V07-SETUP] {WindowSkyPath}를 찾지 못했다 — 창호지 색 단계 미적용");
                return null;
            }
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                Debug.LogError($"[V07-SETUP] {WindowSkyPath}에 SpriteRenderer가 없다");
                return null;
            }
            sr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            Color night = DawnStageModel.PaperColor(0);
            night.a = sr.color.a;
            sr.color = night;   // 에디터에서 열었을 때도 밤 색으로 보이게 (런타임은 Start에서 다시 잡는다)
            EditorUtility.SetDirty(sr);
            Debug.Log($"[V07-SETUP] 창호지({WindowSkyPath}) 무광 전환 + 밤 남색 초기화 — 방 조도가 창에 닿지 않는다");
            return sr;
        }

        // ---------- ③ 실내 여명 보정 ----------

        private static void SetupGlobalDawnBoost()
        {
            GameObject lighting = FindByPath("Lighting");
            var controller = lighting != null ? lighting.GetComponent<LightingController>() : null;
            if (controller == null)
            {
                Debug.LogError("[V07-SETUP] Lighting/LightingController를 찾지 못했다 — globalDawnBoost 미적용");
                return;
            }
            var so = new SerializedObject(controller);
            SerializedProperty p = so.FindProperty("globalDawnBoost");
            if (p == null)
            {
                Debug.LogError("[V07-SETUP] LightingController.globalDawnBoost 프로퍼티 없음");
                return;
            }
            float before = p.floatValue;
            p.floatValue = GlobalDawnBoostV07;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[V07-SETUP] globalDawnBoost {before} → {GlobalDawnBoostV07} " +
                      "(방이 함께 밝아지면 창이 광원으로 안 보인다)");
        }

        // ---------- ① 바닥 창틀 빛 무늬 ----------

        private static DawnWindowView SetupFloorPatch(Sprite grid, Sprite haze, SpriteRenderer windowPaper)
        {
            GameObject root = FindByPath(DawnRootName);
            if (root == null)
            {
                root = new GameObject(DawnRootName);
                root.transform.position = Vector3.zero;
            }

            SpriteRenderer hazeSr = EnsurePatch(root.transform, HazeName, haze);
            SpriteRenderer gridSr = EnsurePatch(root.transform, GridName, grid);

            var view = root.GetComponent<DawnWindowView>();
            if (view == null) view = root.AddComponent<DawnWindowView>();

            GameObject systems = FindByPath("Systems");
            Wire(view, "sequencer", systems != null ? systems.GetComponent<PhaseSequencer>() : null);
            Wire(view, "windowPaper", windowPaper);
            Wire(view, "patchHaze", hazeSr);
            Wire(view, "patchGrid", gridSr);
            return view;
        }

        private static SpriteRenderer EnsurePatch(Transform parent, string name, Sprite sprite)
        {
            Transform t = parent.Find(name);
            GameObject go = t != null ? t.gameObject : new GameObject(name);
            go.transform.SetParent(parent, false);
            // 초기 배치는 0단계(무늬 없음) 기준 — 런타임에 DawnWindowView가 매 프레임 잡는다
            go.transform.localPosition = new Vector3(DawnStageModel.PatchCenterX(0), DawnStageModel.PatchAnchorY, PatchZ);
            go.transform.localScale = Vector3.one;

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = PatchSortingOrder;
            // 무광 — 바닥 빛이 실내 조도를 타면 그 순간 진실 채널이 소금 상태에 오염된다 (v0.5 감광 예외①)
            sr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            Color c = DawnStageModel.PatchTint(0);
            c.a = 0f;
            sr.color = c;
            sr.enabled = false; // 0단계에는 무늬가 없다
            EditorUtility.SetDirty(sr);
            return sr;
        }

        // ---------- 공통 ----------

        private static Sprite Require(string path)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null)
                Debug.LogError($"[V07-SETUP] 스프라이트 없음: {path} — Tools/art-gen/gen_dawn_patch.py 실행 후 다시 시도할 것");
            return s;
        }

        private static void EnsureScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        /// <summary>비활성 오브젝트도 찾는다 — <c>GameObject.Find</c>는 못 찾는다.</summary>
        private static GameObject FindByPath(string path)
        {
            string[] parts = path.Split('/');
            Transform cur = null;
            foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name != parts[0]) continue;
                cur = root.transform;
                break;
            }
            for (int i = 1; i < parts.Length && cur != null; i++) cur = cur.Find(parts[i]);
            return cur == null ? null : cur.gameObject;
        }

        private static void Wire(Component target, string field, Object value)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[V07-SETUP] 배선 실패 — {target.GetType().Name}.{field} 프로퍼티 없음");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();

            Object check = new SerializedObject(target).FindProperty(field).objectReferenceValue;
            Debug.Log($"[V07-SETUP] {target.GetType().Name}.{field} = {(check != null ? check.name : "NULL!")}");
        }
    }
}
