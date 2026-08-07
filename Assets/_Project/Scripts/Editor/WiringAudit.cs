using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Morae.EditorTools
{
    /// <summary>
    /// **씬 배선·좌표·정렬순서의 전수 스냅샷.** 구조 변경(프리팹 전환·이동·재배치) 전후로 두 번 찍어
    /// 텍스트 diff만으로 "조용히 끊긴 참조가 없다"를 증명하기 위한 읽기 전용 도구다.
    ///
    /// <para>
    /// 왜 필요한가 — 2026-08-06 화면 3종 프리팹화 때 씬→프리팹 참조 2건
    /// (<c>TouchControlsView.mobileAudioHint</c>, <c>keyboardHints[0]</c>)이 <b>에러 없이</b> null이 되어
    /// 모바일 안내가 죽어 있었다. 프리팹 경계를 넘는 참조는 끊길 때 아무 소리도 내지 않는다.
    /// </para>
    ///
    /// <para>
    /// 출력은 <b>fileID가 아니라 계층 경로</b>로 적는다 — 프리팹 전환은 fileID를 통째로 갈아치우므로
    /// fileID 기반 스냅샷은 전부 "변경"으로 보여 쓸모가 없다. 경로 기반이라 전환 전후가 그대로 대조된다.
    /// </para>
    ///
    /// 기록 항목:
    ///   1) 모든 <c>Morae.*</c> 컴포넌트의 오브젝트 참조 필드 (배열 원소 포함) → 대상 경로 / NULL / 에셋 경로
    ///   2) 모든 게임오브젝트의 <b>월드</b> 좌표·회전·스케일·활성 상태·컴포넌트 목록
    ///   3) SpriteRenderer / LineRenderer / Canvas 의 정렬 레이어·순서
    ///
    /// CLI:
    ///   -executeMethod Morae.EditorTools.WiringAudit.Run -wiringOut &lt;출력경로&gt;
    /// </summary>
    public static class WiringAudit
    {
        private const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        private const string DefaultOut = "Tools/scene-audit/wiring.txt";

        [MenuItem("Morae/Audit Scene Wiring (배선·좌표·정렬 스냅샷)")]
        public static void RunFromMenu()
        {
            string path = Write(DefaultOut);
            Debug.Log($"[WIRE-AUDIT] 스냅샷 기록: {path}");
        }

        /// <summary>batchmode 진입점. <c>-wiringOut &lt;경로&gt;</c>로 출력 경로를 받는다.</summary>
        public static void Run()
        {
            string outPath = ArgValue("-wiringOut") ?? DefaultOut;
            string path = Write(outPath);
            Debug.Log($"[WIRE-AUDIT] 스냅샷 기록: {path}");
        }

        /// <summary>스냅샷을 파일로 쓴다. 씬은 수정하지 않는다.</summary>
        public static string Write(string outPath)
        {
            EnsureScene();

            var sb = new StringBuilder();
            var roots = new List<GameObject>(EditorSceneManager.GetActiveScene().GetRootGameObjects());
            roots.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            var objects = new List<Transform>();
            foreach (GameObject root in roots) Collect(root.transform, objects);

            // 경로 정렬 — 하이어라키 순서가 바뀌어도 diff가 흔들리지 않게
            objects.Sort((a, b) => string.CompareOrdinal(PathOf(a), PathOf(b)));

            sb.AppendLine("# ===== 1. 오브젝트 (경로 | 활성 | 월드 좌표/회전/스케일 | 컴포넌트) =====");
            foreach (Transform t in objects) sb.AppendLine(DescribeObject(t));

            sb.AppendLine();
            sb.AppendLine("# ===== 2. 렌더 정렬 (경로 | 렌더러 | 레이어 | 순서) =====");
            foreach (Transform t in objects)
            {
                foreach (Renderer r in t.GetComponents<Renderer>())
                {
                    sb.AppendLine($"{PathOf(t)} | {r.GetType().Name} | {r.sortingLayerName} | {r.sortingOrder}");
                }
                foreach (Canvas c in t.GetComponents<Canvas>())
                {
                    sb.AppendLine($"{PathOf(t)} | Canvas | {c.sortingLayerName} | {c.sortingOrder}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("# ===== 3. Morae 컴포넌트 오브젝트 참조 (경로.컴포넌트.필드 = 대상) =====");
            int nulls = 0, total = 0;
            var refLines = new List<string>();
            foreach (Transform t in objects)
            {
                foreach (MonoBehaviour mb in t.GetComponents<MonoBehaviour>())
                {
                    if (mb == null) continue; // 스크립트 유실 슬롯
                    Type type = mb.GetType();
                    if (type.Namespace == null || !type.Namespace.StartsWith("Morae", StringComparison.Ordinal)) continue;

                    var so = new SerializedObject(mb);
                    SerializedProperty p = so.GetIterator();
                    while (p.NextVisible(true))
                    {
                        if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
                        if (p.propertyPath == "m_Script") continue;
                        total++;
                        string target = Describe(p);
                        if (target == "NULL") nulls++;
                        refLines.Add($"{PathOf(t)}.{type.Name}.{p.propertyPath} = {target}");
                    }
                }
            }
            refLines.Sort(StringComparer.Ordinal);
            foreach (string line in refLines) sb.AppendLine(line);

            sb.AppendLine();
            sb.AppendLine($"# ===== 요약: 오브젝트 {objects.Count} / 참조 필드 {total} / 그중 NULL {nulls} =====");

            // 상대 경로는 프로젝트 루트 기준으로 푼다 — batchmode의 현재 디렉토리는 보장되지 않는다
            string full = Path.IsPathRooted(outPath)
                ? outPath
                : Path.GetFullPath(Path.Combine(Application.dataPath, "..", outPath));
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, sb.ToString(), new UTF8Encoding(false));
            Debug.Log($"[WIRE-AUDIT] 오브젝트 {objects.Count} / 참조 {total} / NULL {nulls}");
            return full;
        }

        // ---------- 내부 ----------

        private static void Collect(Transform t, List<Transform> into)
        {
            into.Add(t);
            for (int i = 0; i < t.childCount; i++) Collect(t.GetChild(i), into);
        }

        private static string DescribeObject(Transform t)
        {
            var comps = new List<string>();
            foreach (Component c in t.GetComponents<Component>())
            {
                // 유실 스크립트는 null로 온다 — 조용히 넘기면 회귀를 놓친다
                comps.Add(c == null ? "<MISSING SCRIPT>" : c.GetType().Name);
            }
            comps.Sort(StringComparer.Ordinal);

            Vector3 p = t.position;
            Vector3 e = t.eulerAngles;
            Vector3 s = t.lossyScale;
            return string.Format(CultureInfo.InvariantCulture,
                "{0} | active={1} | pos=({2:F4},{3:F4},{4:F4}) rot=({5:F3},{6:F3},{7:F3}) scale=({8:F4},{9:F4},{10:F4}) | {11}",
                PathOf(t), t.gameObject.activeSelf ? 1 : 0,
                p.x, p.y, p.z, e.x, e.y, e.z, s.x, s.y, s.z,
                string.Join(",", comps));
        }

        private static string Describe(SerializedProperty p)
        {
            Object o = p.objectReferenceValue;
            // 끊긴 참조(대상 소멸)와 애초에 빈 참조는 여기서 구분하지 않는다 —
            // 6000.5에서 objectReferenceInstanceIDValue가 에러 수준으로 폐기됐고,
            // 어차피 전후 diff가 "있던 대상이 사라졌다"를 그대로 보여준다.
            if (o == null) return "NULL";
            if (EditorUtility.IsPersistent(o))
            {
                string path = AssetDatabase.GetAssetPath(o);
                return $"asset:{path}#{o.name}:{o.GetType().Name}";
            }
            var comp = o as Component;
            if (comp != null) return $"scene:{PathOf(comp.transform)}:{o.GetType().Name}";
            var go = o as GameObject;
            if (go != null) return $"scene:{PathOf(go.transform)}:GameObject";
            return $"other:{o.name}:{o.GetType().Name}";
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

        private static void EnsureScene()
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        internal static string ArgValue(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], key, StringComparison.Ordinal)) return args[i + 1];
            return null;
        }
    }
}
