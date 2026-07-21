using TeoGames.Mesh_Combiner.Scripts.Profile;

namespace TeoGames.Mesh_Combiner.Scripts.Combine.BatchMesh {
	public class BatchMeshTimer : Timer {
		protected override void TrackProfile(long diff) => ProfilerModule.BatchMeshBakerTime.Value += diff;
	}
}