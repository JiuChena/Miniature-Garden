using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using TeoGames.Mesh_Combiner.Scripts.Combine.CombinedMaterial;
using TeoGames.Mesh_Combiner.Scripts.Combine.Interfaces;
using TeoGames.Mesh_Combiner.Scripts.Combine.MaterialStorage;
using TeoGames.Mesh_Combiner.Scripts.Combine.MeshRendererManager;
using TeoGames.Mesh_Combiner.Scripts.Extension;
using TeoGames.Mesh_Combiner.Scripts.Profile;
using TeoGames.Mesh_Combiner.Scripts.Util;
using UnityEngine;

namespace TeoGames.Mesh_Combiner.Scripts.Combine {
	using CombineType = Dictionary<AbstractCombinable, (int cid, BasicMaterial[] materials)>;
	using UpdatesType = Dictionary<AbstractCombinable, UpdateType>;

	public enum UpdateType {
		Include,
		Exclude
	}

	[AddComponentMenu("Mesh Combiner/MC Mesh Combiner")]
	[HelpURL("https://teogames.gitbook.io/dynamic-mesh-combiner/components/combiner/mc-mesh-combiner")]
	public partial class MeshCombiner : AbstractMeshCombiner, IAsyncCombiner {
		private static readonly ThreadsPool Pool = new ThreadsPool();
		private static readonly Timer Timer = new Timer();

		public readonly List<AbstractMeshRenderer> RendererManagers = new List<AbstractMeshRenderer>();

		private readonly CombineType _CombinableToMaterial = new CombineType();
		private readonly UpdatesType _Updates = new UpdatesType();

		public int CombinableCount => _CombinableToMaterial.Count;
		public Task UpdateTask { get; protected set; } = Task.CompletedTask;

		private bool IsUpdateInProgress => UpdateTask.Status < TaskStatus.RanToCompletion;

		private void Awake() {
			if (!name.StartsWith("__SKIP__")) Init();
		}

		private void OnDestroy() {
			RendererManagers.ForEach(r => r.Clear());
			ProfilerModule.Meshes.Value -= CombinableCount;
		}

		[SuppressMessage("ReSharper", "RedundantCast")]
		public override void Init() {
			_Materials = bakeMaterials
				? new BakedMaterialStorage()
				: new BasicMaterialStorage() as AbstractMaterialStorage;

			DefineIncludes();
		}

		public override void Clear() {
			_RendererComponents.ForEach(
				r => {
					if (!r) return;

					r.DeleteMesh();
					DestroyImmediate(r.gameObject);
				}
			);
			_RendererComponents = Array.Empty<Renderer>();

			RendererManagers.ForEach(
				r => {
					if (r.Renderer) DestroyImmediate(r.Renderer.gameObject);
				}
			);
			RendererManagers.Clear();

			if (IsLodReady) Lod.Clear();
		}

		public override bool IsBaked(AbstractCombinable combinable) {
#if DEBUG_BAKING
			Debug.LogWarning($"{name} >> IsBaked > {combinable.name} > {!isVisible} > {_CombinableToMaterial.ContainsKey(combinable)} > {_Updates.ContainsKey(combinable)}", combinable);
#endif
			return !isVisible || _CombinableToMaterial.ContainsKey(combinable);
		}

		public override Renderer[] GetRenderers() => IsLodReady
			? _RendererComponents.Concat(Lod.Combiner.GetRenderers()).ToArray()
			: _RendererComponents;

		protected void ScheduleUpdate(bool force = false) {
			if (!force && IsUpdateInProgress) return;
#if DEBUG_BAKING
			Debug.LogWarning($"{name} >> ScheduleUpdate for {_Updates.Count} when visibility status is {isVisible}");
#endif

			var started = Time.unscaledTime;
			UpdateTask = Pool.Schedule(Validate, RunUpdate);

			return;

			bool Validate() => _HasRemovals || Time.unscaledTime - started > .5f;
		}

		protected void ScheduleUpdate(UpdatesType updates) {
#if DEBUG_BAKING
			Debug.LogWarning($"{name} >> ScheduleUpdate for {updates.Count} when visibility status is {isVisible}");
#endif

			var started = Time.unscaledTime;
			UpdateTask = Pool.Schedule(Validate, Run);

			return;

			bool Validate() => _HasRemovals || Time.unscaledTime - started > .5f;
			Task Run() => RunUpdate(updates);
		}

		private async Task RunUpdate() => await RunUpdate(null);

		private async Task RunUpdate(UpdatesType updates) {
			if (updates == null) _HasRemovals = false;
			if (_Materials == null) Init();

			try {
				await UpdateMeshList(updates ?? _Updates.CopyAndClear());
				if (isVisible) await UpdateMesh();
				if (isVisible) await ParseRenderers();
				else await ToggleRenderers();
			} catch (Exception ex) {
				if (this) {
					Debug.LogException(ex);
					throw;
				}
			} finally {
				Timer.Stop();
				if (isVisible && _Updates.Any() && this) ScheduleUpdate(true);
			}
		}

		protected async Task UpdateMeshList(UpdatesType updates) {
			if (!this) return;

			Timer.Start(maxBuildTime);
			if (Timer.IsTimeoutRequired) await Timer.Wait();
			_Matrix = transform.worldToLocalMatrix;

			var i = 0;
			foreach (var pair in updates) {
				if (i++ % 10 == 0 && Timer.IsTimeoutRequired) await Timer.Wait();
				var c = pair.Key;

				try {
					if (pair.Value == UpdateType.Include) {
						if (await RunInclude(c)) {
#if DEBUG_BAKING
							Debug.LogError($">> {c.name} >> UpdateMeshList >> RunInclude >> {name}");
#endif
							_ToggleUpdates.TryAdd(c, ToggleStatus.Added);
						}
					} else {
						if (RunExclude(c)) {
#if DEBUG_BAKING
							Debug.LogError($">> {c.name} >> UpdateMeshList >> RunExclude >> {name}");
#endif
							_ToggleUpdates.TryAdd(c, ToggleStatus.Removed);
						}
					}
				} catch (Exception e) {
					if (this) Debug.LogException(e, c);
				}
			}

			Timer.Stop();
		}
	}
}