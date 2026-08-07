using System;
using System.Collections.Generic;
using System.Text;
using Morae.Game.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Morae.EditorTools
{
    /// <summary>
    /// **씬에 굳어버린 옛 대사를 코드 기본값으로 되돌린다.**
    ///
    /// <para>
    /// 왜 필요한가 — <c>[SerializeField]</c> 필드의 C# 초기값은 <b>그 컴포넌트가 씬에 처음 추가될 때 한 번만</b>
    /// 쓰인다. 이후 코드의 초기값을 고쳐도 씬 YAML에 이미 직렬화된 값이 이기고, 에디터 인스펙터에도
    /// "코드와 다르다"는 표시가 없다. 실제 사고(2026-08-07): <c>controlHintLine</c>에 옛 문구
    /// "…불상 앞에 앉아 <b>{0}</b> 검어진 귀퉁이를 겨누는 게다"가 남아 자리표시자가 화면에 그대로 노출됐다.
    /// (자리표시자를 쓰던 시절의 값이 씬에 굳었고, 코드에서 자리표시자를 없앤 커밋이 씬에는 닿지 않았다.)
    /// </para>
    ///
    /// <para>
    /// 비ASCII 문자열은 씬 YAML에 <c>\uXXXX</c>로 이스케이프돼 들어가므로 <b>파일을 grep해도 눈에 안 띈다</b> —
    /// 이 함정이 조용한 이유. 디코딩 도구는 <c>Tools/scene-audit/decode_scene_strings.py</c>,
    /// 회귀 방어는 EditMode <c>SceneTextIntegrityTests</c>.
    /// </para>
    ///
    /// CLI:
    ///   -executeMethod Morae.EditorTools.PrologueTextSync.Sync   (정정 + 저장)
    ///   -executeMethod Morae.EditorTools.PrologueTextSync.Audit  (드리프트 보고만 — 씬 무수정)
    /// </summary>
    public static class PrologueTextSync
    {
        private const string ScenePath = "Assets/_Project/Scenes/Main.unity";

        /// <summary>코드가 단일 진실이어야 하는 대사 필드. 여기 없는 필드(튜닝 수치·참조)는 씬 값을 보존한다.</summary>
        private static readonly string[] DialoguePaths =
        {
            "lines",
            "warningLine",
            "controlHintLine",
            "telegraphLine",
            "retryLine",
            "clearedLine",
            "mercyLine",
        };

        // ---------- 정정 ----------

        [MenuItem("Morae/Resync Prologue Text (씬 대사 → 코드 기본값)")]
        public static void Sync()
        {
            EnsureScene();

            var director = UnityEngine.Object.FindFirstObjectByType<PrologueDirector>(FindObjectsInactive.Include);
            if (director == null)
            {
                Debug.LogError("[TEXT-SYNC] 씬에 PrologueDirector가 없다 — 배선을 먼저 확인할 것");
                return;
            }

            PrologueDirector fresh = CreateFreshDefaults();
            try
            {
                var scene = new SerializedObject(director);
                var defaults = new SerializedObject(fresh);

                List<string> before = CollectStrings(scene);
                int changed = 0;
                foreach (string path in DialoguePaths)
                {
                    SerializedProperty src = defaults.FindProperty(path);
                    if (src == null)
                    {
                        Debug.LogWarning($"[TEXT-SYNC] 필드 '{path}' 없음 — 이름이 바뀌었나? 건너뜀");
                        continue;
                    }
                    // 배열·중첩 구조까지 통째로 복사한다 (fileID·다른 필드는 건드리지 않는다)
                    scene.CopyFromSerializedProperty(src);
                }
                scene.ApplyModifiedPropertiesWithoutUndo();

                List<string> after = CollectStrings(new SerializedObject(director));
                changed = ReportDiff(before, after);

                if (changed == 0)
                {
                    Debug.Log("[TEXT-SYNC] 씬 대사가 이미 코드 기본값과 일치 — 변경 없음");
                    return;
                }

                EditorUtility.SetDirty(director);
                UnityEngine.SceneManagement.Scene s = EditorSceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(s);
                EditorSceneManager.SaveScene(s);
                Debug.Log($"[TEXT-SYNC] 대사 {changed}줄 정정·씬 저장 완료");
            }
            finally
            {
                if (fresh != null) UnityEngine.Object.DestroyImmediate(fresh.gameObject);
            }
        }

        // ---------- 전수 감사 (드리프트 보고만) ----------

        /// <summary>
        /// 씬의 모든 <c>Morae.*</c> 컴포넌트에 대해, 직렬화된 <b>문자열</b> 값이 코드 기본값과 다른 곳을 전부 보고한다.
        /// 씬을 수정하지 않는다 — 어떤 드리프트가 의도된 것인지는 사람이 판단해야 하기 때문.
        /// </summary>
        [MenuItem("Morae/Audit Scene Text (코드 기본값과 대조)")]
        public static void Audit()
        {
            EnsureScene();

            var report = new StringBuilder();
            int drift = 0;
            int placeholders = 0;
            var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (MonoBehaviour mb in behaviours)
            {
                if (mb == null) continue;
                Type type = mb.GetType();
                if (type.Namespace == null || !type.Namespace.StartsWith("Morae")) continue;

                MonoBehaviour fresh = TryCreateFresh(type);
                try
                {
                    var scene = new SerializedObject(mb);
                    SerializedObject defaults = fresh != null ? new SerializedObject(fresh) : null;

                    SerializedProperty p = scene.GetIterator();
                    bool enter = true;
                    while (p.NextVisible(enter))
                    {
                        enter = true;
                        if (p.propertyType != SerializedPropertyType.String) continue;
                        string value = p.stringValue;
                        if (string.IsNullOrEmpty(value)) continue;

                        if (HasFormatPlaceholder(value))
                        {
                            placeholders++;
                            report.AppendLine($"  ⚠자리표시자  {type.Name}.{p.propertyPath} = \"{value}\"");
                            continue;
                        }
                        if (defaults == null) continue;
                        SerializedProperty d = defaults.FindProperty(p.propertyPath);
                        if (d == null || d.propertyType != SerializedPropertyType.String) continue;
                        if (string.IsNullOrEmpty(d.stringValue)) continue; // 코드에 기본값 없음 = 씬이 주인
                        if (d.stringValue == value) continue;

                        drift++;
                        report.AppendLine($"  드리프트  {type.Name}.{p.propertyPath}");
                        report.AppendLine($"      씬 : {value}");
                        report.AppendLine($"      코드: {d.stringValue}");
                    }
                }
                finally
                {
                    if (fresh != null) UnityEngine.Object.DestroyImmediate(fresh.gameObject);
                }
            }

            if (drift == 0 && placeholders == 0)
            {
                Debug.Log("[TEXT-AUDIT] 씬 직렬화 문자열 전수 점검 — 자리표시자·드리프트 0건");
                return;
            }
            Debug.LogWarning($"[TEXT-AUDIT] 자리표시자 {placeholders}건 / 코드 기본값 불일치 {drift}건\n{report}");
        }

        // ---------- 내부 ----------

        private static void EnsureScene()
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        /// <summary>C# 필드 초기값만 들어간 새 인스턴스 = "코드 기본값" 스냅샷.</summary>
        private static PrologueDirector CreateFreshDefaults()
        {
            var go = new GameObject("~PrologueDefaults") { hideFlags = HideFlags.HideAndDontSave };
            return go.AddComponent<PrologueDirector>();
        }

        private static MonoBehaviour TryCreateFresh(Type type)
        {
            if (type.IsAbstract) return null;
            GameObject go = null;
            try
            {
                go = new GameObject("~Defaults") { hideFlags = HideFlags.HideAndDontSave };
                return go.AddComponent(type) as MonoBehaviour;
            }
            catch (Exception e)
            {
                // RequireComponent 등으로 못 붙는 타입은 감사 대상에서 조용히 뺀다 (자리표시자 검사는 계속 돈다)
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
                Debug.Log($"[TEXT-AUDIT] {type.Name} 기본값 인스턴스 생성 불가 — 대조 생략 ({e.GetType().Name})");
                return null;
            }
        }

        private static List<string> CollectStrings(SerializedObject so)
        {
            var list = new List<string>();
            SerializedProperty p = so.GetIterator();
            while (p.NextVisible(true))
            {
                if (p.propertyType == SerializedPropertyType.String)
                    list.Add($"{p.propertyPath}\t{p.stringValue}");
            }
            return list;
        }

        private static int ReportDiff(List<string> before, List<string> after)
        {
            var old = new HashSet<string>(before);
            int changed = 0;
            foreach (string line in after)
            {
                if (old.Contains(line)) continue;
                changed++;
                string path = line.Split('\t')[0];
                string wasValue = "(없던 항목)";
                foreach (string b in before)
                {
                    if (b.StartsWith(path + "\t", StringComparison.Ordinal)) { wasValue = b.Substring(path.Length + 1); break; }
                }
                Debug.Log($"[TEXT-SYNC] {path}\n    이전: {wasValue}\n    이후: {line.Substring(path.Length + 1)}");
            }
            return changed;
        }

        /// <summary><c>{0}</c> 같은 String.Format 자리표시자.</summary>
        private static bool HasFormatPlaceholder(string value)
        {
            for (int i = 0; i + 2 < value.Length; i++)
            {
                if (value[i] != '{') continue;
                int j = i + 1;
                while (j < value.Length && value[j] >= '0' && value[j] <= '9') j++;
                if (j > i + 1 && j < value.Length && value[j] == '}') return true;
            }
            return false;
        }
    }
}
