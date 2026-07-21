using TeoGames.Mesh_Combiner.Scripts.Combine.ChunkContainer;
using TeoGames.Mesh_Combiner.Scripts.Extension;
using UnityEngine;

namespace TeoGames.Mesh_Combiner.Demo.Scripts {
	[AddComponentMenu("Mesh Combiner/Demo/MC Toggle Visibility Controller")]
	[HelpURL("https://teogames.gitbook.io/dynamic-mesh-combiner")]
	public class ToggleVisibilityController : MonoBehaviour {
		public AbstractChunkContainer[] list;

		public void Randomize() {
			list.ForEach(i => i.IsVisible = Random.value > .5f);
		}
	}
}