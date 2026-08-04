using System.Collections.Generic;
using Morae.Game.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Morae.EditorTools
{
    /// <summary>
    /// 열려 있는 씬에 SoundManager를 추가하고 Audio/ 폴더 클립을 배선 (씬 재생성 없음 — 수동 배선 보존).
    /// 멱등: 이미 있으면 클립 배선만 갱신. 실행 후 각 슬롯의 배선 결과를 콘솔에 출력하니 NULL이 있으면 확인할 것.
    /// </summary>
    public static class SoundSetup
    {
        private const string AudioDir = "Assets/_Project/Audio";

        [MenuItem("Morae/Setup Sound Manager")]
        public static void Setup()
        {
            var mgr = Object.FindFirstObjectByType<SoundManager>();
            if (mgr == null)
            {
                var go = new GameObject("SoundManager");
                var audioRoot = GameObject.Find("Audio");
                if (audioRoot != null) go.transform.SetParent(audioRoot.transform);
                mgr = go.AddComponent<SoundManager>();
            }

            var so = new SerializedObject(mgr);
            SetClip(so, "bgmMain", LoadFirst("BGM_01_Main"));
            SetClip(so, "bgmIntro", LoadFirst("BGM_02_Intro"));
            SetClips(so, "bgmNight", LoadAll("BGM_03_Night"));
            SetClip(so, "bgmEnding", LoadFirst("BGM_04_Ending"));
            SetClip(so, "sfxClock", LoadFirst("SFX_Clock"));
            SetClip(so, "sfxDoorClose", LoadByName("SFX_Door", "door_close"));
            SetClip(so, "sfxDoorTry", LoadByName("SFX_Door", "door_try"));
            SetClips(so, "sfxFear", LoadAll("SFX_Fear"));
            so.ApplyModifiedPropertiesWithoutUndo();

            // 배선 검증 — 저장 후 실제 값 재확인 (SO 배선 유실 사고 재발 감지)
            var verify = new SerializedObject(mgr);
            string[] singles = { "bgmMain", "bgmIntro", "bgmEnding", "sfxClock", "sfxDoorClose", "sfxDoorTry" };
            foreach (string field in singles)
            {
                Object v = verify.FindProperty(field).objectReferenceValue;
                Debug.Log($"[SOUND-SETUP] {field} = {(v != null ? v.name : "NULL!")}");
            }
            foreach (string field in new[] { "bgmNight", "sfxFear" })
            {
                SerializedProperty arr = verify.FindProperty(field);
                Debug.Log($"[SOUND-SETUP] {field} = {arr.arraySize}개");
            }

            EditorSceneManager.MarkSceneDirty(mgr.gameObject.scene);
            EditorSceneManager.SaveScene(mgr.gameObject.scene);
            Debug.Log("[SOUND-SETUP] SoundManager 배선·씬 저장 완료");
        }

        private static AudioClip LoadFirst(string folder)
        {
            List<AudioClip> clips = LoadAllList(folder);
            return clips.Count > 0 ? clips[0] : null;
        }

        private static AudioClip[] LoadAll(string folder) => LoadAllList(folder).ToArray();

        private static AudioClip LoadByName(string folder, string nameContains)
        {
            foreach (AudioClip clip in LoadAllList(folder))
            {
                if (clip.name.Contains(nameContains)) return clip;
            }
            return null;
        }

        private static List<AudioClip> LoadAllList(string folder)
        {
            var result = new List<AudioClip>();
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { $"{AudioDir}/{folder}" });
            var paths = new List<string>(guids.Length);
            foreach (string guid in guids) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(); // 01, 02 … 파일명 순서 보장
            foreach (string path in paths)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null) result.Add(clip);
            }
            if (result.Count == 0) Debug.LogWarning($"[SOUND-SETUP] {folder}: AudioClip 없음");
            return result;
        }

        private static void SetClip(SerializedObject so, string field, AudioClip clip)
            => so.FindProperty(field).objectReferenceValue = clip;

        private static void SetClips(SerializedObject so, string field, AudioClip[] clips)
        {
            SerializedProperty prop = so.FindProperty(field);
            prop.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            }
        }
    }
}
