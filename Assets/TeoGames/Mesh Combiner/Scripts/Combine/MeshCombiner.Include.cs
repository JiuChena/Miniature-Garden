using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using TeoGames.Mesh_Combiner.Scripts.Combine.CombinedMaterial;
using TeoGames.Mesh_Combiner.Scripts.Combine.MaterialStorage;
using TeoGames.Mesh_Combiner.Scripts.Combine.MeshRendererManager;
using TeoGames.Mesh_Combiner.Scripts.Extension;
using TeoGames.Mesh_Combiner.Scripts.Profile;
using UnityEngine;

namespace TeoGames.Mesh_Combiner.Scripts.Combine {
	public partial class MeshCombiner {
		private AbstractMaterialStorage _Materials;
		private Transform[] _StaticBones;
		private Func<AbstractCombinable, Task<bool>> _StaticInclude;
		private Func<AbstractCombinable, Task<bool>> _DynamicInclude;
		private Matrix4x4 _Matrix;

		[SuppressMessage("ReSharper", "RedundantCast")]
		private void DefineIncludes() {
			var supportStatic = (rendererTypes & TargetRendererType.MeshRenderer) != 0;
			var supportDynamic = (rendererTypes & TargetRendererType.SkinnerMeshRenderer) != 0;
			if (!supportDynamic && !supportStatic) {
				throw new Exception("You should pick at least one type of renderer to make combinable work");
			}
			
			_StaticBones = new[] { transform };
			_StaticInclude = supportStatic ? (Func<AbstractCombinable, Task<bool>>)IncludeAsStatic : IncludeAsDynamic;
			_DynamicInclude = supportDynamic ? (Func<AbstractCombinable, Task<bool>>)IncludeAsDynamic : IncludeAsStatic;
		}

		public override void Include(AbstractCombinable combinable) {
			_Updates[combinable] = UpdateType.Include;

#if DEBUG_BAKING
			Debug.LogError($">>>>> {combinable.name} > Include > {name} > {isVisible} > {IsUpdateInProgress} > {_Updates.Count}");
#endif

			if (isVisible) ScheduleUpdate();
			else combinable.HideOriginalMesh();
		}

		protected Task<bool> RunInclude(AbstractCombinable combinable) {
			if (!combinable.IsActive) return Task.FromResult(false);

			combinable.ClearCache();

			return combinable.IsStatic ? _StaticInclude(combinable) : _DynamicInclude(combinable);
		}

		private async Task<bool> IncludeAsStatic(AbstractCombinable combinable) {
			var cache = combinable.GetCache();
			var mats = cache.materials;
			var isMeshUpdated = cache.status == CacheStatus.MeshUpdated;
			var subMeshes = Math.Min(mats.Length, cache.mesh.subMeshCount);
			var shadow = cache.renderer.shadowCastingMode;
			var offset = 1 + ((int)shadow + 1) * 10 + 0;
			var newMaterials = new BasicMaterial[subMeshes];
			var cID = combinable.GetInstanceID();
			var rTransform = cache.transform;

			if (isMeshUpdated) cache.status = CacheStatus.Cached;

			for (var i = 0; i < subMeshes; i++) {
				if (Timer.IsTimeoutRequired) await Timer.Wait();

				var mat = mats[i];
				var mID = mat.GetCombineID(offset);
				var (parser, material) = _Materials.Get(mID, mat, offset, shadow, true, indexFormat);
				newMaterials[i] = material;

				material.SetMesh(
					cID,
					parser,
					null,
					i,
					cache.mesh,
					isMeshUpdated,
					true,
					_Matrix,
					rTransform,
					null
				);
			}

			AddMaterialMap(combinable, cID, newMaterials);

			return true;
		}

		private async Task<bool> IncludeAsDynamic(AbstractCombinable combinable) {
			var cache = combinable.GetCache();
			var mats = cache.materials;
			var isMeshUpdated = cache.status == CacheStatus.MeshUpdated;
			var subMeshes = Math.Min(mats.Length, cache.mesh.subMeshCount);
			var shadow = cache.renderer.shadowCastingMode;
			var blendShape = cache.blendShape;
			var offset = ((int)shadow + 1) * 10 + (separateBlendShapes && blendShape.enabled ? 100 : 0);
			var newMaterials = new BasicMaterial[subMeshes];
			var cID = combinable.GetInstanceID();
			var isStatic = combinable.IsStatic;

			Mesh parsedMesh;
			if (cache.status < CacheStatus.Baked) {
				parsedMesh = cache.mesh = cache.isCorrectionRequired switch {
					MeshCorrection.Stat => cache.mesh.ToStatic(),
					MeshCorrection.Anim => cache.mesh.ToAnimated(),
					_ => cache.mesh
				};
				cache.status = CacheStatus.Baked;
			} else {
				parsedMesh = cache.mesh;
			}

			var rTransform = cache.transform;
			var realBones = isStatic ? _StaticBones : cache.Bones ?? new[] { rTransform };

			for (var i = 0; i < subMeshes; i++) {
				if (Timer.IsTimeoutRequired) await Timer.Wait();

				var mat = mats[i];
				var mID = mat.GetCombineID(offset);
				var (parser, material) = _Materials.Get(mID, mat, offset, shadow, false, indexFormat);
				newMaterials[i] = material;

				material.SetMesh(
					cID,
					parser,
					realBones,
					i,
					parsedMesh,
					isMeshUpdated,
					isStatic,
					_Matrix,
					rTransform,
					blendShape
				);
			}

			AddMaterialMap(combinable, cID, newMaterials);

			return true;
		}

		private void AddMaterialMap(AbstractCombinable combinable, int cID, BasicMaterial[] newMaterials) {
			if (_CombinableToMaterial.TryGetValue(combinable, out var existing)) {
				var id = existing.cid * 100;
				for (var i = 0; i < existing.materials.Length; i++) {
					var material = existing.materials[i];
					if (newMaterials.Contains(material)) continue;
					if (!material.Meshes.Remove(id + i)) continue;

					material.Updated();
				}
			}

			_CombinableToMaterial[combinable] = (cID, newMaterials);
			ProfilerModule.Meshes.Value++;
		}
	}
}