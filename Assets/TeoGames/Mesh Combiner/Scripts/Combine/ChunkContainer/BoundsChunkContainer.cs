using TeoGames.Mesh_Combiner.Scripts.Combine.MeshRendererManager;
using TeoGames.Mesh_Combiner.Scripts.Extension;
using UnityEngine;
using UnityEngine.Rendering;

namespace TeoGames.Mesh_Combiner.Scripts.Combine.ChunkContainer {
	[AddComponentMenu("Mesh Combiner/Chunk/MC Bounds Chunk Container")]
	[HelpURL("https://teogames.gitbook.io/dynamic-mesh-combiner")]
	public class BoundsChunkContainer : SingleCombinerChunkContainer {
		[SerializeField]
		[Tooltip("If combinable isn't inside collider, then it will be combined if it's closest one compared to other containers")]
		private bool acceptClosest = true;

		public Bounds bounds;
		public Bounds GlobalBounds { get; private set; }

		public override void Init(
			TargetRendererType rendererTypes,
			int maxBuildTime,
			bool bakeMaterials,
			bool separateBlendShapes,
			bool clearMaterialCache,
			IndexFormat indexFormat
		) {
			base.Init(rendererTypes, maxBuildTime, bakeMaterials, separateBlendShapes, clearMaterialCache, indexFormat);

			GlobalBounds = new Bounds(bounds.center + transform.position, bounds.size);
		}

		public float Distance(Vector3 position) {
			return Vector3.Distance(GlobalBounds.ClosestPoint(position), position);
		}

		public override float Compability(Vector3 pos) {
			if (GlobalBounds.Contains(pos)) return 1f;

			var dist = Distance(pos);

			return dist < .1f ? 1 : (acceptClosest ? -dist : 0);
		}

		public void RecalculateBounds() => bounds = gameObject.GetBounds();

		private void Reset() => RecalculateBounds();
	}
}