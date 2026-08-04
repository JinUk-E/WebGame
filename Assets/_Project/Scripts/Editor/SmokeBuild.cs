using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Morae.EditorTools
{
    /// <summary>
    /// D1 WebGL 스모크 빌드 (architecture.md §8.3·§8.6).
    /// CLI: Unity.exe -batchmode -quit -projectPath ... -buildTarget WebGL
    ///      -executeMethod Morae.EditorTools.SmokeBuild.Build -logFile build.log
    /// 출력: 프로젝트 루트 docs/ (GitHub Pages 서빙 경로) + .nojekyll
    /// </summary>
    public static class SmokeBuild
    {
        private const string ScenePath = "Assets/_Project/Scenes/SmokeTest.unity";
        private const string OutputDir = "docs";

        [MenuItem("Morae/Build WebGL Smoke")]
        public static void Build()
        {
            // 씬·폰트 에셋이 없으면 먼저 생성 (멱등)
            SmokeSceneBuilder.Build();

            // §8.3: GitHub Pages는 Content-Encoding 제어 불가 — Brotli + Fallback ON 필수
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.template = "PROJECT:Morae"; // §8.4 16:9 letterbox + DPR 1
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.defaultWebScreenWidth = 1920;
            PlayerSettings.defaultWebScreenHeight = 1080;
            PlayerSettings.runInBackground = true;

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = OutputDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log($"[SMOKE-BUILD] 결과: {summary.result}, 총 크기: {summary.totalSize / (1024f * 1024f):F2} MB, " +
                      $"에러 {summary.totalErrors} / 경고 {summary.totalWarnings}, 소요 {summary.totalTime.TotalSeconds:F0}s");

            if (summary.result == BuildResult.Succeeded)
            {
                // Pages Jekyll 처리 배제 보험 (§8.3)
                File.WriteAllText(Path.Combine(OutputDir, ".nojekyll"), string.Empty);
                Debug.Log("[SMOKE-BUILD] .nojekyll 생성 완료");
            }
            else if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
