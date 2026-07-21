using System;
using System.Collections.Generic;
using UnityEngine;

namespace TeoGames.Mesh_Combiner.Scripts.Combine.SceneCombiner {
	[AddComponentMenu("Mesh Combiner/MC Scene Combiner Registry")]
	[HelpURL("https://teogames.gitbook.io/dynamic-mesh-combiner")]
	public class SceneCombinerRegistry : MonoBehaviour {
		[Tooltip("Add combiners that you want to make global for entire scene")]
		public AbstractMeshCombiner[] combiners;

		private static SceneCombinerRegistry INSTANCE;
		private static SceneCombinerState STATE;
		private static readonly AbstractMeshCombiner[] Empty = Array.Empty<AbstractMeshCombiner>();

		public void Awake() => Load();

		private static void Load() {
			INSTANCE = FindObjectOfType<SceneCombinerRegistry>();
			STATE = INSTANCE ? SceneCombinerState.Ready : SceneCombinerState.NotFound;
		}

		public static IEnumerable<AbstractMeshCombiner> Combiners
		{
			get
			{
				switch (STATE) {
					case SceneCombinerState.NotFound: return Empty;
					case SceneCombinerState.NotLoaded:
						Load();
						return Combiners;
					case SceneCombinerState.Ready:
						if (INSTANCE) return INSTANCE.combiners;

						Load();
						return Combiners;

					default: throw new ArgumentOutOfRangeException();
				}
			}
		}
	}
}