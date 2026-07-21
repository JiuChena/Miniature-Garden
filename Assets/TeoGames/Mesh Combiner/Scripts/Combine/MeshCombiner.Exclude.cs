using TeoGames.Mesh_Combiner.Scripts.Combine.Interfaces;
using TeoGames.Mesh_Combiner.Scripts.Profile;
using UnityEngine;

namespace TeoGames.Mesh_Combiner.Scripts.Combine {
	public partial class MeshCombiner {
		private bool _HasRemovals;

		public override void Exclude(AbstractCombinable combinable) {
			_Updates[combinable] = UpdateType.Exclude;
			_HasRemovals = true;

#if DEBUG_BAKING
			Debug.LogError($">>>>> {combinable.name} > Exclude > {name} > {isVisible} > {IsUpdateInProgress} > {_Updates.Count}");
#endif

			if (isVisible) ScheduleUpdate();
			else combinable.UpdateVisibility();
		}

		protected bool RunExclude(AbstractCombinable combinable) {
			if (!_CombinableToMaterial.TryGetValue(combinable, out var matList)) return false;

			for (var i = 0; i < matList.materials.Length; i++) {
				var material = matList.materials[i];
				var meshKey = matList.cid * 100 + i;
				if (!material.Meshes.Remove(meshKey)) {
					Debug.LogError("Unable to remove material");
					continue;
				}

				material.Updated();
			}

			_CombinableToMaterial.Remove(combinable);
			ProfilerModule.Meshes.Value--;

			return true;
		}
	}
}