using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

internal static class MiniatureGardenProjectSyncUtility
{
    private const string PendingSyncRequestAssetPath =
        "Assets/Editor/Business/MiniatureGarden/ProjectMaintenance/.pending-project-sync";

    [InitializeOnLoadMethod]
    private static void RegisterPendingSyncHook()
    {
        EditorApplication.delayCall += TryHandlePendingSyncRequest;
    }

    [MenuItem("MiniatureGarden/Project Maintenance/Refresh And Sync C# Project")]
    private static void RefreshAndSyncCSharpProject()
    {
        RefreshAndSyncCSharpProjectInternal(requestCompilation: true);
    }

    [MenuItem("MiniatureGarden/Project Maintenance/Sync C# Project Only")]
    private static void SyncCSharpProjectOnly()
    {
        TrySyncSolution();
    }

    private static void TryHandlePendingSyncRequest()
    {
        string requestPath = GetPendingSyncRequestFullPath();
        if (!File.Exists(requestPath))
            return;

        try
        {
            FileUtil.DeleteFileOrDirectory(requestPath);
            FileUtil.DeleteFileOrDirectory(requestPath + ".meta");
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"删除一次性工程同步请求标记失败：{exception.Message}");
        }

        RefreshAndSyncCSharpProjectInternal(requestCompilation: false);
    }

    private static void RefreshAndSyncCSharpProjectInternal(bool requestCompilation)
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        if (requestCompilation)
            CompilationPipeline.RequestScriptCompilation();

        TrySyncSolution();
    }

    private static void TrySyncSolution()
    {
        Type syncVsType = Type.GetType("UnityEditor.SyncVS,UnityEditor");
        MethodInfo syncSolutionMethod = syncVsType?.GetMethod("SyncSolution",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        if (syncSolutionMethod == null)
        {
            Debug.LogWarning("未找到 UnityEditor.SyncVS.SyncSolution，已执行资源刷新和脚本重新编译请求，但 C# 工程文件可能仍需由 IDE 集成自行同步。");
            return;
        }

        try
        {
            syncSolutionMethod.Invoke(null, null);
            Debug.Log("已执行 Unity C# 工程文件同步。");
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"执行 Unity C# 工程文件同步失败：{exception.Message}");
        }
    }

    private static string GetPendingSyncRequestFullPath()
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        return Path.Combine(projectRoot, PendingSyncRequestAssetPath.Replace('/', Path.DirectorySeparatorChar));
    }
}
