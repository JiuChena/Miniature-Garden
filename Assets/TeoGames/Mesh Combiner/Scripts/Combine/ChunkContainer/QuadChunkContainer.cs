using TeoGames.Mesh_Combiner.Scripts.Extension;
using UnityEngine;

namespace TeoGames.Mesh_Combiner.Scripts.Combine.ChunkContainer {
	[AddComponentMenu("Mesh Combiner/Chunk/MC Quad Chunk Container")]
	[HelpURL("https://teogames.gitbook.io/dynamic-mesh-combiner/components/combiner/mc-chunk-combiner#list-of-available-containers")]
	public class QuadChunkContainer : GridChunkContainer {
		public override string GetKey(AbstractCombinable combinable) {
			return (GetPosition(combinable) / size).Round().ToString();
		}
	}
}