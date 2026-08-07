using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Morae.Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Morae.Game.Tests
{
    /// <summary>
    /// **씬에 굳어버린 옛 텍스트를 잡는 회귀 방어.**
    ///
    /// <para>
    /// 2026-08-07 사고: 프롤로그 대사 <c>controlHintLine</c>이 화면에
    /// "…불상 앞에 앉아 <b>{0}</b> 검어진 귀퉁이를 겨누는 게다"로 나왔다.
    /// 코드에는 자리표시자가 없는 새 문구가 있었지만, <c>[SerializeField]</c>의 C# 초기값은
    /// <b>컴포넌트가 씬에 처음 추가될 때 한 번만</b> 쓰이므로 씬 YAML의 옛 값이 이긴다.
    /// 게다가 한글은 <c>\uXXXX</c>로 이스케이프돼 직렬화되므로 씬 파일을 grep해도 눈에 띄지 않는다.
    /// </para>
    ///
    /// 그래서 씬 파일을 직접 읽어 디코딩한 뒤 두 가지를 못 박는다:
    ///   ① 어떤 직렬화 문자열에도 <c>{0}</c> 같은 자리표시자가 없을 것
    ///   ② PrologueDirector의 대사가 코드 기본값과 <b>정확히 일치</b>할 것
    ///
    /// 정정 수단은 에디터 메뉴 <c>Morae/Resync Prologue Text</c> (batch: <c>PrologueTextSync.Sync</c>).
    /// </summary>
    public sealed class SceneTextIntegrityTests
    {
        private const string MainScene = "_Project/Scenes/Main.unity";
        private const string PrefabDir = "_Project/Prefab";
        private const string DirectorTag = "Morae.Game.Core.PrologueDirector";
        /// <summary>YAML 문서 경계(<c>--- !u!114 &amp;...</c>) 신호 — 컴포넌트 블록의 끝을 알려준다.</summary>
        private const string DocumentBreak = "---";

        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        // ---------- ① 자리표시자 ----------

        [Test]
        public void SerializedStrings_HaveNoFormatPlaceholders()
        {
            var offenders = new List<string>();
            foreach (string path in SerializedAssets())
            {
                foreach (YamlString s in YamlStrings(path))
                {
                    if (!HasFormatPlaceholder(s.Value)) continue;
                    offenders.Add($"{Path.GetFileName(path)}:{s.Line}  {s.Key} = {s.Value}");
                }
            }

            Assert.IsEmpty(offenders,
                "씬·프리팹에 직렬화된 문자열에 {0} 같은 자리표시자가 남아 있다 — 화면에 그대로 노출된다.\n"
                + "코드 기본값을 고쳐도 씬 값이 이긴다: 메뉴 Morae/Resync Prologue Text 로 되돌릴 것.\n"
                + string.Join("\n", offenders));
        }

        // ---------- ② 프롤로그 대사 ↔ 코드 기본값 ----------

        [Test]
        public void PrologueDialogue_InScene_MatchesCodeDefaults()
        {
            List<string> codeLines = CodeDefaultDialogue();
            List<string> sceneLines = SceneDialogue();

            Assert.IsNotEmpty(sceneLines, "씬에서 PrologueDirector 대사를 찾지 못했다 — 블록 파싱 규칙을 확인할 것");

            var missingInCode = new List<string>();
            var pool = new List<string>(codeLines);
            foreach (string line in sceneLines)
            {
                int at = pool.IndexOf(line);
                if (at < 0) missingInCode.Add(line);
                else pool.RemoveAt(at);
            }

            var message = new StringBuilder();
            if (missingInCode.Count > 0)
            {
                message.AppendLine("씬에만 있는 대사 (코드에 없는 옛 문구 — 씬 직렬화 값이 코드를 덮어쓰고 있다):");
                foreach (string s in missingInCode) message.AppendLine("  씬 : " + s);
            }
            if (pool.Count > 0)
            {
                message.AppendLine("코드에만 있는 대사 (씬에 반영되지 않음):");
                foreach (string s in pool) message.AppendLine("  코드: " + s);
            }
            if (message.Length > 0)
                message.AppendLine("정정: 메뉴 Morae/Resync Prologue Text (batch: Morae.EditorTools.PrologueTextSync.Sync)");

            Assert.IsTrue(missingInCode.Count == 0 && pool.Count == 0, message.ToString());
        }

        /// <summary>새 인스턴스 = C# 필드 초기값만 들어간 "코드 기본값" 스냅샷.</summary>
        private List<string> CodeDefaultDialogue()
        {
            var go = new GameObject("~PrologueDefaults") { hideFlags = HideFlags.HideAndDontSave };
            _spawned.Add(go);
            PrologueDirector director = go.AddComponent<PrologueDirector>();

            Type type = typeof(PrologueDirector);
            Type lineType = type.GetNestedType("PrologueLine", BindingFlags.NonPublic);
            Assert.IsNotNull(lineType, "PrologueLine 중첩 타입을 못 찾았다 — 이름이 바뀌었나?");
            FieldInfo textField = lineType.GetField("text");
            Assert.IsNotNull(textField, "PrologueLine.text 필드를 못 찾았다");

            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var result = new List<string>();

            FieldInfo linesField = type.GetField("lines", Flags);
            Assert.IsNotNull(linesField, "PrologueDirector.lines 필드를 못 찾았다");
            if (linesField.GetValue(director) is Array arr)
                foreach (object item in arr) result.Add((string)textField.GetValue(item));

            foreach (string name in new[]
                     { "warningLine", "controlHintLine", "telegraphLine", "retryLine", "clearedLine", "mercyLine" })
            {
                FieldInfo f = type.GetField(name, Flags);
                Assert.IsNotNull(f, $"PrologueDirector.{name} 필드를 못 찾았다");
                result.Add((string)textField.GetValue(f.GetValue(director)));
            }
            return result;
        }

        /// <summary>Main.unity의 PrologueDirector 블록에서 <c>text:</c> 값을 전부 뽑는다.</summary>
        private static List<string> SceneDialogue()
        {
            var result = new List<string>();
            bool inBlock = false;
            foreach (YamlString s in YamlStrings(AssetPath(MainScene)))
            {
                if (s.Key == DocumentBreak) { inBlock = false; continue; }
                if (s.Key == "m_EditorClassIdentifier")
                {
                    inBlock = s.Value.EndsWith(DirectorTag, StringComparison.Ordinal);
                    continue;
                }
                if (inBlock && s.Key == "text") result.Add(s.Value);
            }
            return result;
        }

        // ---------- YAML 문자열 추출 (Unity는 비ASCII를 \uXXXX로 이스케이프한다) ----------

        private struct YamlString
        {
            public int Line;
            public string Key;
            public string Value;
        }

        private static string AssetPath(string relative)
            => Path.Combine(Application.dataPath, relative).Replace('\\', '/');

        private static IEnumerable<string> SerializedAssets()
        {
            yield return AssetPath(MainScene);
            string prefabRoot = AssetPath(PrefabDir);
            if (!Directory.Exists(prefabRoot)) yield break;
            foreach (string p in Directory.GetFiles(prefabRoot, "*.prefab", SearchOption.AllDirectories))
                yield return p.Replace('\\', '/');
        }

        private static IEnumerable<YamlString> YamlStrings(string path)
        {
            if (!File.Exists(path)) yield break;
            string[] lines = File.ReadAllText(path, Encoding.UTF8).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');
                if (line.StartsWith("--- ", StringComparison.Ordinal))
                {
                    yield return new YamlString { Line = i + 1, Key = DocumentBreak, Value = string.Empty };
                    continue;
                }
                int colon = KeyColon(line);
                if (colon < 0) continue;
                string key = line.Substring(0, colon).TrimStart(' ', '-').Trim();
                if (key.Length == 0) continue;
                string rest = line.Substring(colon + 1).Trim();
                if (rest.Length == 0 || rest[0] == '{' || rest[0] == '[') continue;

                if (rest[0] != '"')
                {
                    yield return new YamlString { Line = i + 1, Key = key, Value = rest };
                    continue;
                }

                // 이중따옴표 스칼라 — 종료 따옴표까지 접는다 (줄바꿈은 공백 1개로 폴딩)
                int start = i;
                string body = rest.Substring(1);
                while (true)
                {
                    int end = ClosingQuote(body);
                    if (end >= 0) { body = body.Substring(0, end); break; }
                    i++;
                    if (i >= lines.Length) break;
                    body = body + " " + lines[i].TrimEnd('\r').Trim();
                }
                yield return new YamlString { Line = start + 1, Key = key, Value = Unescape(body) };
            }
        }

        /// <summary>"key: value" 형태의 키 뒤 콜론 위치. 아니면 -1.</summary>
        private static int KeyColon(string line)
        {
            int i = 0;
            while (i < line.Length && (line[i] == ' ' || line[i] == '-')) i++;
            int keyStart = i;
            while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_')) i++;
            if (i == keyStart || i >= line.Length || line[i] != ':') return -1;
            return i;
        }

        private static int ClosingQuote(string body)
        {
            for (int j = 0; j < body.Length; j++)
            {
                if (body[j] == '\\') { j++; continue; }
                if (body[j] == '"') return j;
            }
            return -1;
        }

        private static string Unescape(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] != '\\' || i + 1 >= s.Length) { sb.Append(s[i]); continue; }
                char n = s[++i];
                switch (n)
                {
                    case 'u':
                        sb.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16));
                        i += 4;
                        break;
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case '0': sb.Append('\0'); break;
                    default: sb.Append(n); break;
                }
            }
            return sb.ToString();
        }

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
