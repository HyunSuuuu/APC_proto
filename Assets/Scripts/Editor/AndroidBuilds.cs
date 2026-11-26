using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace APC.EditorTools
{
    /// <summary>
    /// Android APK 2개 — Game 씬 · TycoonTest 씬을 따로 빌드해 한 기기에 같이 설치할 수 있게 한다 (2026-11-27).
    /// Tycoon은 패키지 ID 끝에 ".tycoon", 이름 끝에 " Tycoon"을 붙인다. 빌드 뒤 ID · 이름은 원래대로 돌린다.
    /// 결과: Builds/Android/APC_Game.apk · APC_Tycoon.apk, 로그 "[AndroidBuilds]".
    /// </summary>
    public static class AndroidBuilds
    {
        const string OutputFolder = "Builds/Android";

        [MenuItem("APC/Build/Android — Game + Tycoon (APK 2개)")]
        static void BuildBoth() => EditorApplication.delayCall += Run; // 메뉴를 바로 끝내고 다음 에디터 틱에 빌드 (MCP 호출이 빌드 시간 동안 막히지 않게)

        static void Run()
        {
            var target = NamedBuildTarget.Android;
            var originalId = PlayerSettings.GetApplicationIdentifier(target);
            var originalName = PlayerSettings.productName;
            Directory.CreateDirectory(OutputFolder);
            try
            {
                Build("Game", new[] { "Assets/Scenes/Game.unity" }, originalId, originalName);
                Build("Tycoon", new[] { "Assets/Scenes/TycoonTest.unity" }, originalId + ".tycoon", originalName + " Tycoon");
            }
            finally
            {
                PlayerSettings.SetApplicationIdentifier(target, originalId);
                PlayerSettings.productName = originalName;
                AssetDatabase.SaveAssets();
            }
            Debug.Log("[AndroidBuilds] 끝");
        }

        static void Build(string label, string[] scenes, string id, string productName)
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, id);
            PlayerSettings.productName = productName;
            var path = $"{OutputFolder}/APC_{label}.apk";
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            });
            var summary = report.summary;
            var line = $"[AndroidBuilds] {label}: {summary.result} · {path} · {summary.totalSize / (1024f * 1024f):F1}MB · {summary.totalTime.TotalSeconds:F0}초 · 에러 {summary.totalErrors} · id {id}";
            if (summary.result == BuildResult.Succeeded) Debug.Log(line);
            else Debug.LogError(line);
        }
    }
}
