namespace TeoGames.Mesh_Combiner.Scripts.Profile {
	public class UpdateQueueTimer : Timer {
		protected override void TrackProfile(long diff) => ProfilerModule.UpdateQueueTime.Value += diff;
	}
}