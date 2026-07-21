using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using TeoGames.Mesh_Combiner.Scripts.Extension;
using TeoGames.Mesh_Combiner.Scripts.Profile;
using TeoGames.Mesh_Combiner.Scripts.Util;
using UnityEngine;
using UnityEngine.Rendering;

namespace TeoGames.Mesh_Combiner.Scripts.Combine.BatchMesh {
	class RendererCache {
		public Mesh mesh;
		public Material[] materials;
		public int subMeshes;
		public readonly List<Matrix4x4> matrices = new();
	}

	public class BatchMeshBaker {
		private static readonly Timer Timer = new BatchMeshTimer();
		private static readonly ThreadsPool Pool = new();

		private readonly Transform _Container;
		private readonly Dictionary<Renderer, RendererCache> _List = new();
		private bool _IsBakerScheduled = false;

		public MeshRenderer Renderer { get; private set; }

		public Combinable Combinable { [UsedImplicitly] get; private set; }
		private MeshFilter Filter { get; set; }

		public BatchMeshBaker(Transform container) {
			_Container = container;
		}

		public async Task Bake() {
			if (_IsBakerScheduled) return;
			_IsBakerScheduled = true;

			await Pool.Schedule(
				ThreadsPool.RunWithDelay(0.05f),
				async () => {
					_IsBakerScheduled = false;
					if (_List.Count == 0) {
						if (!Renderer) CreateView();

						return;
					}

					Timer.Start(2);
					var materials = GetMaterials();
					var subMeshes = new CombineInstance[materials.Count];
					var i = 0;
					foreach (var (mat, list) in materials) {
						if (Timer.IsTimeoutRequired) await Timer.Wait();

						subMeshes[i++] = new CombineInstance {
							mesh = BakeList(list.ToArray(), $"material-{mat.name}", true),
							transform = Matrix4x4.identity,
							subMeshIndex = 0
						};
					}

					if (Timer.IsTimeoutRequired) await Timer.Wait();
					var mesh = BakeList(subMeshes, _Container.name, false);

					UpdateView(mesh, materials.Keys.ToArray());
					Timer.Stop();
				}
			);
		}

		public Task RemoveInArea(UnityEngine.Collider collider) {
			return Pool.Schedule(
				ThreadsPool.RunAlways,
				async () => {
					if (!collider || !collider.gameObject.activeInHierarchy) {
						throw new Exception("Collider must be active in the scene.");
					}

					Timer.Start(2);

					var p = _Container.position;
					foreach (var (_, ren) in _List) {
						var cnt = ren.matrices.Count - 1;

						for (var i = cnt; i >= 0; i--) {
							var pos = ren.matrices[i].GetPosition() + p;
							if (collider.Contains(pos)) {
								ren.matrices.RemoveAt(i);
							}
						}

						if (Timer.IsTimeoutRequired) await Timer.Wait();
					}

					Timer.Stop();
				}
			);
		}

		public Task RemoveInArea(Vector3 center, float radius) {
			return Pool.Schedule(
				ThreadsPool.RunAlways,
				async () => {
					Timer.Start(2);

					var p = _Container.position;
					foreach (var (_, ren) in _List) {
						var cnt = ren.matrices.Count - 1;

						for (var i = cnt; i >= 0; i--) {
							var pos = ren.matrices[i].GetPosition() + p;
							if (pos.FlatDistance(center) <= radius) {
								ren.matrices.RemoveAt(i);
							}
						}

						if (Timer.IsTimeoutRequired) await Timer.Wait();
					}

					Timer.Stop();
				}
			);
		}

		public void Add(Renderer ren, Matrix4x4 matrix) {
			if (!_List.TryGetValue(ren, out var cache)) {
				cache = _List[ren] = new RendererCache {
					mesh = ren is SkinnedMeshRenderer s ? s.sharedMesh : ren.GetComponent<MeshFilter>().sharedMesh,
					materials = ren.sharedMaterials,
				};
				cache.subMeshes = Mathf.Min(cache.materials.Length, cache.mesh.subMeshCount);
			}

			cache.matrices.Add(matrix);
		}

		public void Add(Renderer ren, Vector3 position, Quaternion rotation, Vector3 scale) {
			var mat = Matrix4x4.TRS(position - _Container.position, rotation, scale);

			Add(ren, mat);
		}

		public void Add(Transform obj, Vector3 position, Quaternion rotation, Vector3 scale) {
			if (!obj.TryGetComponent(out Renderer ren)) ren = obj.GetComponentInChildren<Renderer>();

			Add(ren, position, rotation, scale.Multiply(ren.transform.lossyScale));
		}

		private Mesh BakeList(CombineInstance[] list, string name, bool merge) {
			// var isTotal = merge ? "SMALL" : "TOTAL";
			// Debug.LogError($"-- {isTotal} - {list.Length} - {Timer.DiffMs:0.00}ms");
			var mesh = new Mesh {
				indexFormat = IndexFormat.UInt32,
				name = $"[BMB] [NO-CACHE] {name}"
			};

			mesh.CombineMeshes(list, merge, merge, false);
			// Debug.LogError($"------ {mesh.vertexCount} - {Timer.DiffMs:0.000}ms");

			return mesh;
		}

		private void UpdateView(Mesh mesh, Material[] materials) {
			CreateView();
			Renderer.gameObject.SetActive(true);

			if (Combinable.GetCache().mesh) {
				Filter.SetSharedMesh(mesh);
				Renderer.SetSharedMaterials(materials);
			} else {
				Filter.sharedMesh = mesh;
				Renderer.sharedMaterials = materials;
				Combinable.ClearCache();
			}
		}

		public void CreateView() {
			if (Renderer) return;

			var go = new GameObject($"[BMB] {_Container.name}") {
				transform = {
					parent = _Container,
					localPosition = Vector3.zero,
					localRotation = Quaternion.identity,
					localScale = Vector3.one,
				}
			};
			go.SetActive(false);

			Filter = go.AddComponent<MeshFilter>();
			Renderer = go.AddComponent<MeshRenderer>();
			Combinable = go.AddComponent<Combinable>();
		}

		private Dictionary<Material, List<CombineInstance>> GetMaterials() {
			var materials = new Dictionary<Material, List<CombineInstance>>();

			foreach (var (_, ren) in _List) {
				if (ren.matrices.Count == 0) continue;

				for (var i = 0; i < ren.subMeshes; i++) {
					var material = ren.materials[i];
					if (!materials.TryGetValue(material, out var list)) {
						materials[material] = list = new List<CombineInstance>();
					}

					foreach (var mat in ren.matrices) {
						list.Add(new CombineInstance { mesh = ren.mesh, transform = mat, subMeshIndex = i });
					}
				}
			}

			return materials;
		}
	}
}