using System;
using System.Linq;
using TeoGames.Mesh_Combiner.Scripts.Combine;
using TeoGames.Mesh_Combiner.Scripts.Combine.SceneCombiner;
using TeoGames.Mesh_Combiner.Scripts.Extension;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeoGames.Mesh_Combiner.Scripts.Editor.MenuItems {
    public class MainMenu : AssetModificationProcessor {
#if DMC_REMOVE_UV
        [MenuItem("Tools/Dynamic Mesh Combiner/Do not remove non-zero UV", false, 900)]
        public static void RemoveNonZeroUV() {
            RemoveDefineSymbol("DMC_REMOVE_UV");
        }
#else
        [MenuItem("Tools/Dynamic Mesh Combiner/Remove non-zero UV", false, 900)]
        public static void AddNonZeroUV() {
            AddDefineSymbol("DMC_REMOVE_UV");
        }
#endif

#if DMC_REMOVE_COLORS
        [MenuItem("Tools/Dynamic Mesh Combiner/Do not remove vector colors", false, 900)]
        public static void RemoveCollors() {
            RemoveDefineSymbol("DMC_REMOVE_COLORS");
        }
#else
        [MenuItem("Tools/Dynamic Mesh Combiner/Remove vector colors", false, 900)]
        public static void AddCollors() {
            AddDefineSymbol("DMC_REMOVE_COLORS");
        }
#endif

        [MenuItem("Tools/Dynamic Mesh Combiner/Add Scene Combiner", false, 1000)]
        public static void AddSceneCombiner() {
            var obj = new UnityEngine.GameObject("Scene Combiner Registry");
            obj.transform.SetSiblingIndex(0);

            var registry = obj.AddComponent<SceneCombinerRegistry>();
            registry.combiners = new AbstractMeshCombiner[] { obj.AddComponent<MeshCombiner>() };
        }

        [MenuItem("Tools/Dynamic Mesh Combiner/Prefabs/Clear Cache", false, 1100)]
        public static void FixPrefabsCache() {
            Utils.GetPrefabs("Assets").ForEach(obj => {
                    var isChanged = obj.GetComponentsInChildren<Combinable>(true).Any(Utils.CheckClearCache);

                    if (isChanged) PrefabUtility.SavePrefabAsset(obj);
                }
            );
        }

        [MenuItem("Tools/Dynamic Mesh Combiner/Scenes/Clear Cache", false, 1101)]
        public static void FixScenesCache() {
            var originalScene = SceneManager.GetActiveScene().path;

            foreach (var sceneGuid in AssetDatabase.FindAssets("t:Scene", new string[] { "Assets" })) {
                try {
                    var scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);

                    Debug.Log($"-- {scenePath}");

                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                    var scene = SceneManager.GetActiveScene();
                    var isChanged = false;

                    scene.GetRootGameObjects().ForEach(obj => {
                            isChanged |= obj.GetComponentsInChildren<Combinable>(true).Any(Utils.CheckClearCache);
                        }
                    );

                    if (isChanged) EditorSceneManager.SaveScene(scene);
                    if (scenePath != originalScene) EditorSceneManager.CloseScene(scene, true);
                } catch (Exception e) {
                    Debug.LogException(e);
                }
            }

            if (originalScene != "") EditorSceneManager.OpenScene(originalScene);
        }

        private static BuildTargetGroup GetCurrentBuildTargetGroup() {
            var currentBuildTarget = EditorUserBuildSettings.activeBuildTarget;
            return BuildPipeline.GetBuildTargetGroup(currentBuildTarget);
        }

        private static void AddDefineSymbol(string symbol) {
            var group = GetCurrentBuildTargetGroup();
            var currentDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
            if (!currentDefines.Contains(symbol)) {
                var newDefines = string.IsNullOrEmpty(currentDefines) ? symbol : currentDefines + ";" + symbol;
                PlayerSettings.SetScriptingDefineSymbolsForGroup(group, newDefines);
                Debug.Log($"Added define symbol: {symbol} for {group}");
            } else {
                Debug.Log($"Symbol {symbol} already exists for {group}.");
            }
        }

        private static void RemoveDefineSymbol(string symbol) {
            var group = GetCurrentBuildTargetGroup();
            var currentDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
            if (currentDefines.Contains(symbol)) {
                var newDefines = currentDefines.Replace(symbol, "").Replace(";;", ";").Trim(';');
                PlayerSettings.SetScriptingDefineSymbolsForGroup(group, newDefines);
                Debug.Log($"Removed define symbol: {symbol} for {group}");
            } else {
                Debug.Log($"Symbol {symbol} does not exist for {group}.");
            }
        }
    }
}