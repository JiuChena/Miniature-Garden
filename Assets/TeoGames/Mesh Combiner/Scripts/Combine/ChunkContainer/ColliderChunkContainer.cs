using UnityEngine;

namespace TeoGames.Mesh_Combiner.Scripts.Combine.ChunkContainer {
	[AddComponentMenu("Mesh Combiner/Chunk/MC Collider Chunk Container")]
	[HelpURL("https://teogames.gitbook.io/dynamic-mesh-combiner")]
	public class ColliderChunkContainer : SingleCombinerChunkContainer {
		[SerializeField]
		[Tooltip(
			"If combinable isn't inside collider, then it will be combined if it's closest one compared to other containers"
		)]
		private bool acceptClosest = true;

		[SerializeField] private UnityEngine.Collider col;

		public float Distance(Vector3 position) {
			return Vector3.Distance(col.ClosestPoint(position), position);
		}

		public override float Compability(Vector3 pos) {
			var dist = Distance(pos);

			return dist < .1f ? 1 : (acceptClosest ? -dist : 0);
		}
	}
}