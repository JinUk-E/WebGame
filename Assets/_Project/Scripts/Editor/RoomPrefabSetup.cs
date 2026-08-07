using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Morae.EditorTools
{
    /// <summary>
    /// **방(Room)의 단일 진실을 프리팹으로 옮긴다.**
    ///
    /// <para>
    /// 2026-08-07까지의 상태: <c>Assets/_Project/Prefab/Room.prefab</c>은 존재하지만
    /// <c>Main.unity</c>가 그 GUID를 <b>한 번도 참조하지 않았다</b> — 방은 씬에 직접 배치돼 있고
    /// 프리팹은 따로 놀았다(v0.6 이후로 정렬순서·문 회전·창 흔들림 등이 프리팹에 반영되지 않아 뒤처짐).
    /// 그 상태에서 프리팹을 씬에 적용하면 최신 작업물이 옛것으로 덮인다.
    /// </para>
    ///
    /// <para>
    /// 그래서 방향은 <b>씬 → 프리팹</b>이다: 씬의 현재 Room을 프리팹으로 저장하면서 그 자리를 인스턴스로
    /// 연결한다(<see cref="PrefabUtility.SaveAsPrefabAssetAndConnect"/>). 이후로는 프리팹이 원본이 되어
    /// 아트 담당이 프리팹만 고쳐도 게임에 반영된다.
    /// </para>
    ///
    /// <para>
    /// <b>⚠ 프리팹 경계를 넘는 참조는 끊길 때 아무 소리도 내지 않는다.</b> Room 하위 컴포넌트가
    /// 씬 바깥(Systems 등)을 가리키는 참조는 프리팹 에셋에 담을 수 없어 저장 과정에서 사라질 수 있다
    /// (선례: 2026-08-06 화면 3종 프리팹화 때 <c>TouchControlsView.mobileAudioHint</c> 등 2건이 조용히 죽었다).
    /// 그래서 이 도구는 전환 <b>전에</b> 바깥 참조를 전부 <see cref="CaptureOutward"/>로 걷어두고,
    /// 전환 <b>후에</b> 경로 기준으로 되꽂는다(<see cref="RestoreOutward"/>). fileID가 아니라 경로로 되꽂기
    /// 때문에 전환 과정에서 오브젝트가 재생성돼도 살아남는다.
    /// </para>
    ///
    /// CLI:
    ///   -executeMethod Morae.EditorTools.RoomPrefabSetup.Convert       (전환 — 1회성)
    ///   -executeMethod Morae.EditorTools.RoomPrefabSetup.AuditOverrides (인스턴스 오버라이드 보고)
    /// </summary>
    public static class RoomPrefabSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        public const string PrefabPath = "Assets/_Project/Prefab/Room.prefab";
        private const string RoomName = "Room";

        // ---------- 전환 (1회성) ----------

        [MenuItem("Morae/Convert Room To Prefab Instance (씬 → 프리팹)")]
        public static void Convert()
        {
            EnsureScene();

            GameObject room = FindByPath(RoomName);
            if (room == null)
            {
                Debug.LogError("[ROOM-PREFAB] 씬에 Room이 없다 — 중단");
                return;
            }

            if (PrefabUtility.IsPartOfPrefabInstance(room))
            {
                Debug.Log("[ROOM-PREFAB] Room은 이미 프리팹 인스턴스다 — 전환 생략, 오버라이드를 프리팹에 반영한다");
                ApplyRoomToPrefab();
                return;
            }

            int siblingIndex = room.transform.GetSiblingIndex();
            List<OutwardRef> outward = CaptureOutward(room.transform);
            Debug.Log($"[ROOM-PREFAB] 방 밖을 가리키는 참조 {outward.Count}건 확보 — 전환 후 되꽂는다");
            foreach (OutwardRef r in outward) Debug.Log($"[ROOM-PREFAB]   보관 {r}");

            bool ok;
            // ⚠ 반환값은 씬 인스턴스가 아니라 **저장된 프리팹 에셋의 루트**다 (이름이 헷갈린다).
            //   여기에 GetPropertyModifications를 걸면 "Provided GameObject is not a Prefab instance"로 죽는다.
            //   씬 쪽은 원래 참조(room)가 그대로 인스턴스가 되므로 그걸 쓴다.
            GameObject assetRoot = PrefabUtility.SaveAsPrefabAssetAndConnect(
                room, PrefabPath, InteractionMode.AutomatedAction, out ok);
            if (!ok || assetRoot == null)
            {
                Debug.LogError($"[ROOM-PREFAB] 프리팹 저장 실패: {PrefabPath}");
                return;
            }

            GameObject instance = FindByPath(RoomName);
            if (instance == null)
            {
                Debug.LogError("[ROOM-PREFAB] 전환 후 씬에서 Room을 찾지 못했다 — 씬을 저장하지 않고 중단");
                return;
            }
            // 하이어라키 위치 보존 — 렌더 순서는 sortingOrder가 소유하지만, 씬 diff를 줄이는 편이 낫다
            instance.transform.SetSiblingIndex(siblingIndex);

            int restored = RestoreOutward(outward);
            Debug.Log($"[ROOM-PREFAB] 바깥 참조 {restored}/{outward.Count}건 복구");

            SaveAll();
            Debug.Log($"[ROOM-PREFAB] 전환 완료 — {PrefabPath} 가 방의 원본이 되었다 " +
                      $"(인스턴스 GUID {AssetDatabase.AssetPathToGUID(PrefabPath)})");
            ReportOverrides(instance);
        }

        /// <summary>
        /// 전환 전후로 배선 스냅샷을 남기고 전환한다 — batchmode 1회 실행으로 "끊긴 참조 0"을 증명하기 위한 진입점.
        /// CLI: -executeMethod Morae.EditorTools.RoomPrefabSetup.ConvertWithAudit
        /// </summary>
        public static void ConvertWithAudit()
        {
            WiringAudit.Write("Tools/scene-audit/wiring-before.txt");
            Convert();
            WiringAudit.Write("Tools/scene-audit/wiring-after.txt");
        }

        /// <summary>
        /// **셋업 스크립트 전량 재실행 + 배선 대조.** 방이 프리팹 인스턴스가 된 뒤에도 기존 배선 스크립트가
        /// 그대로 도는지, 그리고 실행이 배선을 바꾸지 않는지(멱등)를 한 번에 확인한다.
        /// 전후 스냅샷 diff가 비어야 정상이다.
        /// CLI: -executeMethod Morae.EditorTools.RoomPrefabSetup.VerifySetupScripts
        /// </summary>
        public static void VerifySetupScripts()
        {
            EnsureScene();
            WiringAudit.Write("Tools/scene-audit/wiring-setups-before.txt");

            Art2Setup.Setup();     // 내부에서 D4Setup 재실행
            D3Setup.Setup();
            V05Setup.Setup();
            V061Setup.Setup();
            RattleFxSetup.Setup(); // 내부에서 SoundSetup 재실행
            TouchSetup.Setup();

            WiringAudit.Write("Tools/scene-audit/wiring-setups-after.txt");
            AuditOverrides();
        }

        // ---------- 셋업 스크립트가 부르는 동기화 ----------

        /// <summary>
        /// **씬 인스턴스에 쌓인 오버라이드를 프리팹 에셋으로 밀어 올린다.**
        /// Room 하위를 만지는 셋업 스크립트(Art2Setup·D3Setup·V05Setup·RattleFxSetup)는 끝에서 이걸 부른다 —
        /// 안 부르면 셋업이 인스턴스에만 값을 써서 프리팹과 씬이 매번 다시 갈라진다.
        ///
        /// <para>
        /// 방 밖(Systems 등)을 가리키는 참조는 프리팹 에셋에 담을 수 없다. 그래서 <b>적용 전에 걷어내고
        /// 적용 후에 되꽂아</b> 씬 인스턴스 오버라이드로만 남긴다 — 이 4~6건이 유일하게 허용되는 오버라이드다.
        /// </para>
        /// </summary>
        public static void ApplyRoomToPrefab()
        {
            GameObject room = FindByPath(RoomName);
            if (room == null) return;
            if (!PrefabUtility.IsPartOfPrefabInstance(room))
            {
                Debug.LogWarning("[ROOM-PREFAB] Room이 프리팹 인스턴스가 아니다 — " +
                                 "메뉴 'Morae/Convert Room To Prefab Instance'를 먼저 실행할 것");
                return;
            }

            GameObject instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(room);
            List<OutwardRef> outward = CaptureOutward(room.transform);
            ClearOutward(outward);

            PrefabUtility.ApplyPrefabInstance(instanceRoot, InteractionMode.AutomatedAction);

            int restored = RestoreOutward(outward);
            Debug.Log($"[ROOM-PREFAB] 프리팹 반영 완료 — 바깥 참조 {restored}/{outward.Count}건을 씬 오버라이드로 되꽂음");
            SaveAll();
        }

        // ---------- 감사 ----------

        [MenuItem("Morae/Audit Room Prefab Overrides (씬↔프리팹 갈라짐 점검)")]
        public static void AuditOverrides()
        {
            EnsureScene();
            GameObject room = FindByPath(RoomName);
            if (room == null)
            {
                Debug.LogError("[ROOM-PREFAB] 씬에 Room이 없다");
                return;
            }
            if (!PrefabUtility.IsPartOfPrefabInstance(room))
            {
                Debug.LogWarning("[ROOM-PREFAB] Room이 프리팹 인스턴스가 아니다 (전환 전 상태)");
                return;
            }
            ReportOverrides(PrefabUtility.GetOutermostPrefabInstanceRoot(room));
        }

        private static void ReportOverrides(GameObject instanceRoot)
        {
            var sb = new StringBuilder();
            PropertyModification[] mods = PrefabUtility.GetPropertyModifications(instanceRoot) ??
                                          Array.Empty<PropertyModification>();

            // 프리팹 인스턴스의 **루트 Transform 배치**는 언제나 오버라이드로 기록된다 (유니티 구조상 불가피).
            // 그걸 "갈라짐"으로 세면 매번 7건이 뜨므로, 루트 배치만 정상으로 친다.
            Object rootSource = PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot.transform);

            int allowed = 0, suspicious = 0;
            foreach (PropertyModification m in mods)
            {
                // m_Name·m_RootOrder·루트 Transform 배치는 인스턴스가 갖는 게 정상이다
                if (IsBenign(m) || IsRootPlacement(m, rootSource)) continue;
                bool sceneRef = m.objectReference != null && !EditorUtility.IsPersistent(m.objectReference);
                if (sceneRef)
                {
                    allowed++;
                    sb.AppendLine($"  허용(씬 참조)  {Describe(m)}");
                }
                else
                {
                    suspicious++;
                    sb.AppendLine($"  ⚠갈라짐        {Describe(m)}");
                }
            }

            GameObject[] added = ToGameObjects(PrefabUtility.GetAddedGameObjects(instanceRoot));
            foreach (GameObject g in added)
                sb.AppendLine($"  ⚠추가 오브젝트 {g.name} (프리팹에 없음 — 되돌리면 사라진다)");
            var addedComps = PrefabUtility.GetAddedComponents(instanceRoot);
            foreach (AddedComponent c in addedComps)
                sb.AppendLine($"  ⚠추가 컴포넌트 {c.instanceComponent.GetType().Name} on {c.instanceComponent.name}");

            int bad = suspicious + added.Length + addedComps.Count;
            string head = $"[ROOM-PREFAB] 오버라이드 — 허용(씬 참조) {allowed}건 / 갈라짐 {bad}건";
            if (bad == 0) Debug.Log($"{head}\n{sb}");
            else Debug.LogWarning($"{head}\n{sb}" +
                                  "\n갈라짐이 있으면 'Morae/Audit Room Prefab Overrides' 기준으로 " +
                                  "프리팹에 반영(ApplyRoomToPrefab)하거나 인스턴스에서 되돌릴 것");
        }

        private static GameObject[] ToGameObjects(List<AddedGameObject> list)
        {
            var arr = new GameObject[list.Count];
            for (int i = 0; i < list.Count; i++) arr[i] = list[i].instanceGameObject;
            return arr;
        }

        private static bool IsBenign(PropertyModification m)
        {
            string p = m.propertyPath;
            return p == "m_Name" || p == "m_RootOrder" || p.StartsWith("m_LocalEulerAnglesHint", StringComparison.Ordinal);
        }

        private static bool IsRootPlacement(PropertyModification m, Object rootSource)
        {
            if (rootSource == null || m.target != rootSource) return false;
            string p = m.propertyPath;
            return p.StartsWith("m_LocalPosition", StringComparison.Ordinal)
                || p.StartsWith("m_LocalRotation", StringComparison.Ordinal)
                || p.StartsWith("m_LocalScale", StringComparison.Ordinal);
        }

        private static string Describe(PropertyModification m)
        {
            string target = m.target == null ? "<유실>" : $"{m.target.name}:{m.target.GetType().Name}";
            string val = m.objectReference != null ? m.objectReference.name : m.value;
            return $"{target}.{m.propertyPath} = {val}";
        }

        // ---------- 방 밖을 가리키는 참조 ----------

        /// <summary>Room 하위 컴포넌트가 방 <b>밖</b>의 씬 오브젝트를 가리키는 참조 1건.</summary>
        private sealed class OutwardRef
        {
            public string ObjectPath;    // Room/Buddha
            public string ComponentType; // PrayerInteractable
            public int ComponentIndex;   // 같은 타입이 여러 개일 때
            public string PropertyPath;  // salt
            public Object Value;

            public override string ToString() =>
                $"{ObjectPath}.{ComponentType}[{ComponentIndex}].{PropertyPath} = " +
                $"{(Value != null ? Value.name : "NULL")}";
        }

        /// <summary>에셋 참조는 프리팹이 그대로 담으므로 제외한다 — 씬 오브젝트를 가리키는 것만 걷는다.</summary>
        private static List<OutwardRef> CaptureOutward(Transform root)
        {
            var list = new List<OutwardRef>();
            foreach (Transform t in AllUnder(root))
            {
                var seen = new Dictionary<Type, int>();
                foreach (MonoBehaviour mb in t.GetComponents<MonoBehaviour>())
                {
                    if (mb == null) continue;
                    Type type = mb.GetType();
                    int index = seen.TryGetValue(type, out int n) ? n : 0;
                    seen[type] = index + 1;
                    if (type.Namespace == null || !type.Namespace.StartsWith("Morae", StringComparison.Ordinal)) continue;

                    var so = new SerializedObject(mb);
                    SerializedProperty p = so.GetIterator();
                    while (p.NextVisible(true))
                    {
                        if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
                        if (p.propertyPath == "m_Script") continue;
                        Object v = p.objectReferenceValue;
                        if (v == null) continue;
                        if (EditorUtility.IsPersistent(v)) continue;   // 에셋 — 프리팹이 담는다
                        if (IsUnder(root, v)) continue;                // 방 안 — 프리팹이 담는다
                        list.Add(new OutwardRef
                        {
                            ObjectPath = PathOf(t),
                            ComponentType = type.Name,
                            ComponentIndex = index,
                            PropertyPath = p.propertyPath,
                            Value = v,
                        });
                    }
                }
            }
            return list;
        }

        private static void ClearOutward(List<OutwardRef> refs)
        {
            foreach (OutwardRef r in refs) Assign(r, null);
        }

        private static int RestoreOutward(List<OutwardRef> refs)
        {
            int ok = 0;
            foreach (OutwardRef r in refs)
            {
                if (Assign(r, r.Value)) ok++;
                else Debug.LogError($"[ROOM-PREFAB] 참조 복구 실패 — {r} " +
                                    "(경로나 컴포넌트가 바뀌었나? 손으로 확인할 것)");
            }
            return ok;
        }

        private static bool Assign(OutwardRef r, Object value)
        {
            GameObject go = FindByPath(r.ObjectPath);
            if (go == null) return false;
            int index = 0;
            foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                if (mb.GetType().Name != r.ComponentType) continue;
                if (index++ != r.ComponentIndex) continue;
                var so = new SerializedObject(mb);
                SerializedProperty p = so.FindProperty(r.PropertyPath);
                if (p == null) return false;
                p.objectReferenceValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
                return true;
            }
            return false;
        }

        // ---------- 공통 ----------

        private static IEnumerable<Transform> AllUnder(Transform root)
        {
            yield return root;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child != root) yield return child;
        }

        private static bool IsUnder(Transform root, Object o)
        {
            Transform t = null;
            var c = o as Component;
            if (c != null) t = c.transform;
            else
            {
                var g = o as GameObject;
                if (g != null) t = g.transform;
            }
            if (t == null) return false;
            while (t != null)
            {
                if (t == root) return true;
                t = t.parent;
            }
            return false;
        }

        private static string PathOf(Transform t)
        {
            var sb = new StringBuilder(t.name);
            Transform cur = t.parent;
            while (cur != null)
            {
                sb.Insert(0, cur.name + "/");
                cur = cur.parent;
            }
            return sb.ToString();
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

        private static void SaveAll()
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureScene()
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
    }
}
