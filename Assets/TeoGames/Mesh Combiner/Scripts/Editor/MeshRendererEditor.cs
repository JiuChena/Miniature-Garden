using System.Linq;
using TeoGames.Mesh_Combiner.Scripts.Combine;
using TeoGames.Mesh_Combiner.Scripts.Editor.MenuItems;
using TeoGames.Mesh_Combiner.Scripts.Extension;
using UnityEditor;
using UnityEngine;

namespace TeoGames.Mesh_Combiner.Scripts.Editor {
    [CustomEditor(typeof(MeshRenderer)), CanEditMultipleObjects]
    public class MeshRendererEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            // Update the serialized object to get the latest values
            serializedObject.Update();

            // Draw the default Inspector
            DrawDefaultInspector();

            EditorGUILayout.Space(); // Add some spacing

            var renderers = targets
                .Cast<MeshRenderer>()
                .Where(r => !r.GetComponent<AbstractCombinable>())
                .ToArray();

            if (renderers.Any()) {
                if (GUILayout.Button("Combine mesh")) renderers.ForEach(Utils.AddStaticCombiner);
            } else {
                if (GUILayout.Button("Remove mesh combiner"))
                    renderers.ForEach(r => {
                            var comb = r.GetComponent<AbstractCombinable>();
                            if (!comb) return;

                            Utils.RemoveCombiner(comb);
                            EditorUtility.SetDirty(comb);
                        }
                    );
            }
        }
    }
}