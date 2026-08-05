using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Morae.EditorTools
{
    /// <summary>
    /// WebGL 빌드 (architecture.md §8.3·§8.6). 출력: 프로젝트 루트 docs/ (GitHub Pages 서빙 경로) + .nojekyll
    ///
    /// ⚠ 배포용은 반드시 <b>BuildMain</b>(본편 Main.unity)이다. Build/BuildSmoke는 D1 검증용 스모크 씬 —
    /// 잘못 호출하면 "한글 렌더 확인" 테스트 화면이 그대로 배포된다(2026-08-05 실제 사고).
    /// CLI: Unity.exe -batchmode -quit -projectPath ... -buildTarget WebGL
    ///      -executeMethod Morae.EditorTools.SmokeBuild.<b>BuildMain</b> -logFile build.log
    /// </summary>
    public static class SmokeBuild
    {
        private const string ScenePath = "Assets/_Project/Scenes/SmokeTest.unity";
        private const string OutputDir = "docs";

        /// <summary>D1 검증용 스모크 씬 빌드 — 배포용 아님. 배포는 BuildMain.</summary>
        [MenuItem("Morae/Build WebGL Smoke (검증용 — 배포 아님)")]
        public static void BuildSmoke()
        {
            // 씬·폰트 에셋이 없으면 먼저 생성 (멱등)
            SmokeSceneBuilder.Build();
            BuildScene(ScenePath);
        }

        /// <summary>구 이름 호환 — 스모크 빌드. 새 코드는 BuildSmoke/BuildMain을 명시적으로 쓸 것.</summary>
        public static void Build() => BuildSmoke();

        /// <summary>배포용 본편 빌드 — Pages에 올라가는 것은 이것이다.</summary>
        [MenuItem("Morae/Build WebGL Main (배포용)")]
        public static void BuildMain()
        {
            // 주의: 씬 재생성 없이 저장된 Main.unity 그대로 빌드 —
            // MainSceneBuilder 재생성은 SO 에셋 배선을 유실시키는 문제가 있어(수동 배선 보존) 호출하지 않는다.
            BuildScene("Assets/_Project/Scenes/Main.unity");
        }

        private static void BuildScene(string scenePath)
        {
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
                scenes = new[] { scenePath },
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
