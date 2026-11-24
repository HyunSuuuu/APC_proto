using APC.Data;
using APC.Debugging;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace APC.EditorTools
{
    /// <summary>
    /// 메뉴 `APC > Test > Nav Benchmark (A* vs NavMesh)` (2026-11-24): TycoonTest 씬에서 Play를 시작하고 `NavBenchmark`를 붙인다.
    /// 씬 파일은 고치지 않는다. Deep Profile이 켜져 있으면 측정 동안 끄고(C# 코드만 크게 느려져 비교가 무의미) Play가 끝나면 다시 켠다.
    /// </summary>
    [InitializeOnLoad]
    static class NavBenchmarkMenu
    {
        const string PendingKey = "APC.NavBench.Pending";
        const string RestoreDeepKey = "APC.NavBench.RestoreDeep";
        const string ConfigPath = "Assets/Data/NavBenchmarkConfig.asset";

        static NavBenchmarkMenu() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        [MenuItem("APC/Test/Nav Benchmark (A* vs NavMesh)")]
        static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[NavBench] Play를 멈춘 뒤 실행하세요");
                return;
            }
            if (SceneManager.GetActiveScene().name != "TycoonTest")
            {
                EditorUtility.DisplayDialog("Nav Benchmark", "TycoonTest 씬을 연 뒤 실행하세요 (APC > Test > Build Tycoon 8x8).", "확인");
                return;
            }
            SessionState.SetBool(PendingKey, true);
            if (ProfilerDriver.deepProfiling)
            {
                SessionState.SetBool(RestoreDeepKey, true);
                ProfilerDriver.deepProfiling = false;
                Debug.Log("[NavBench] 측정 동안 Deep Profile을 끕니다 (Play가 끝나면 다시 켬)");
            }
            EditorApplication.isPlaying = true;
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PendingKey, false))
            {
                SessionState.SetBool(PendingKey, false);
                var config = AssetDatabase.LoadAssetAtPath<NavBenchmarkConfig>(ConfigPath);
                if (config == null)
                {
                    Debug.LogError($"[NavBench] 설정이 없습니다: {ConfigPath}");
                    return;
                }
                new GameObject("NavBenchmark").AddComponent<NavBenchmark>().Init(config);
            }
            else if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(RestoreDeepKey, false))
            {
                SessionState.SetBool(RestoreDeepKey, false);
                ProfilerDriver.deepProfiling = true;
                Debug.Log("[NavBench] Deep Profile을 다시 켰습니다 (측정 전 상태)");
            }
        }
    }
}
