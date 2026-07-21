using System.Linq;
using System.Threading.Tasks;
using TeoGames.Mesh_Combiner.Scripts.Combine.Interfaces;
using TeoGames.Mesh_Combiner.Scripts.Extension;

namespace TeoGames.Mesh_Combiner.Scripts.Combine {
	public partial class ChunkMeshCombiner : IAsyncCombiner {
		public Task UpdateTask => updateQueue.UpdateTask
			.ContinueWith(() => IsLodReady ? Lod.Combiner.UpdateTask : null)
			.ContinueWith(() => Task.WhenAll(chunks.Select(c => c is IAsyncCombiner async ? async.UpdateTask : null)));
	}
}