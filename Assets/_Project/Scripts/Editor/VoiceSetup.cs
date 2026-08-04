using System.Collections.Generic;
using Morae.Game.Data;
using UnityEditor;
using UnityEngine;

namespace Morae.EditorTools
{
    /// <summary>
    /// EventTable 에셋에 음성·효과 클립 배선 (id 기준 — 명세 §4 진위표의 오디오 절반).
    /// 클립은 AI 생성물: Windows TTS(Heami) + ffmpeg 가공(피치 다운·뭉갬 2벌·리버브) + 절차 합성(노크·진동).
    /// LICENSES.md 기록 대상. 멱등 — 재실행 시 같은 클립 재배선.
    /// CLI: -executeMethod Morae.EditorTools.VoiceSetup.Setup
    /// </summary>
    public static class VoiceSetup
    {
        private const string VoiceDir = "Assets/_Project/Audio/Voice";
        private const string WindowDir = "Assets/_Project/Audio/SFX_Window";

        // id → (선명 클립, 뭉갬 클립 — Door 채널만. null = 단독)
        private static readonly Dictionary<string, (string clear, string muffled)> Map =
            new Dictionary<string, (string, string)>
            {
                { "tv-hint", ($"{VoiceDir}/tv_hint.wav", null) },
                { "window-knock", ($"{WindowDir}/window_knock.wav", null) },
                { "fake-voice-1", ($"{VoiceDir}/fake1_clear.wav", $"{VoiceDir}/fake1_muffled.wav") },
                { "popopo", ($"{VoiceDir}/popopo_clear.wav", $"{VoiceDir}/popopo_muffled.wav") },
                { "window-rattle", ($"{WindowDir}/window_rattle.wav", null) },
                { "urge", ($"{VoiceDir}/urge.wav", null) },
                { "fake-voice-2", ($"{VoiceDir}/fake2_clear.wav", $"{VoiceDir}/fake2_muffled.wav") },
                { "true-signal", ($"{VoiceDir}/true_signal_clear.wav", $"{VoiceDir}/true_signal_muffled.wav") },
                { "rescue-open", ($"{VoiceDir}/rescue_clear.wav", $"{VoiceDir}/rescue_muffled.wav") },
            };

        /// <summary>오디오 파이프라인 일괄 — 임포트 최적화 → SoundManager 씬 배선 → EventTable 클립 배선.</summary>
        [MenuItem("Morae/Setup Audio All (임포트·배선 일괄)")]
        public static void SetupAll()
        {
            // 배치 실행 대비 — 씬 배선(SoundSetup)은 열린 씬을 전제로 한다
            if (UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path != "Assets/_Project/Scenes/Main.unity")
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/Main.unity");
            }
            AudioImportOptimizer.Apply();
            SoundSetup.Setup();
            Setup();
        }

        [MenuItem("Morae/Setup Voice Clips (EventTable)")]
        public static void Setup()
        {
            var table = AssetDatabase.LoadAssetAtPath<EventTable>(DataAssetBuilder.EventTablePath);
            var so = new SerializedObject(table);
            SerializedProperty events = so.FindProperty("events");

            int wired = 0;
            for (int i = 0; i < events.arraySize; i++)
            {
                SerializedProperty el = events.GetArrayElementAtIndex(i);
                string id = el.FindPropertyRelative("id").stringValue;
                if (!Map.TryGetValue(id, out (string clear, string muffled) paths)) continue;

                var clear = AssetDatabase.LoadAssetAtPath<AudioClip>(paths.clear);
                var muffled = paths.muffled != null ? AssetDatabase.LoadAssetAtPath<AudioClip>(paths.muffled) : null;
                el.FindPropertyRelative("audioClip").objectReferenceValue = clear;
                el.FindPropertyRelative("audioClipMuffled").objectReferenceValue = muffled;
                wired++;
                Debug.Log($"[VOICE-SETUP] {id} → {(clear != null ? clear.name : "NULL!")}"
                          + (paths.muffled != null ? $" / 뭉갬 {(muffled != null ? muffled.name : "NULL!")}" : ""));
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            Debug.Log($"[VOICE-SETUP] EventTable 배선 완료 — {wired}/{events.arraySize}행");
        }
    }
}
