using UnityEditor;
using UnityEngine;

namespace Morae.EditorTools
{
    /// <summary>
    /// 오디오 임포트 압축 일괄 설정 — WebGL 용량 목표 ≤40MB (스펙 공유본 §기술 스택).
    /// BGM: Vorbis q0.35 · CompressedInMemory (스트리밍은 WebGL 제약 회피).
    /// SFX: Vorbis q0.4 · DecompressOnLoad · ForceToMono (짧은 원샷 — 정위는 안 쓴다).
    /// 멱등 — 이미 같은 설정이면 재임포트 생략.
    /// CLI: -executeMethod Morae.EditorTools.AudioImportOptimizer.Apply
    /// </summary>
    public static class AudioImportOptimizer
    {
        private const string AudioDir = "Assets/_Project/Audio";

        [MenuItem("Morae/Optimize Audio Import (용량)")]
        public static void Apply()
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir });
            int changed = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                bool isBgm = path.Contains("/BGM_");

                var settings = importer.defaultSampleSettings;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = isBgm ? 0.35f : 0.4f;
                settings.loadType = isBgm ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;

                bool needsChange = importer.defaultSampleSettings.compressionFormat != settings.compressionFormat
                                   || !Mathf.Approximately(importer.defaultSampleSettings.quality, settings.quality)
                                   || importer.defaultSampleSettings.loadType != settings.loadType
                                   || importer.forceToMono != !isBgm;
                if (!needsChange) continue;

                importer.defaultSampleSettings = settings;
                importer.forceToMono = !isBgm;
                importer.SaveAndReimport();
                changed++;
                Debug.Log($"[AUDIO-OPT] {path} → Vorbis q{settings.quality:F2}, {settings.loadType}{(!isBgm ? ", mono" : "")}");
            }
            Debug.Log($"[AUDIO-OPT] 완료 — {guids.Length}개 중 {changed}개 재임포트");
        }
    }
}
