using System;
using System.Collections.Generic;
using TeoGames.Mesh_Combiner.Scripts.BlendShape;
using TeoGames.Mesh_Combiner.Scripts.Combine.CombinedMaterial.MaterialBake;
using TeoGames.Mesh_Combiner.Scripts.Extension;
using TeoGames.Mesh_Combiner.Scripts.Profile;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TeoGames.Mesh_Combiner.Scripts.Combine.CombinedMaterial {
	[Serializable]
	public class BasicMaterial {
		public Material material;
		public ShadowCastingMode shadow;
		public bool isStatic = true;
		public bool hasBlendShapes;

		public readonly Dictionary<int, AdvancedCombineInstance> Meshes = new Dictionary<int, AdvancedCombineInstance>();

		public Mesh Mesh { get; protected set; }
		public Transform[] Bones { get; protected set; } = Array.Empty<Transform>();
		public int LastUpdatedAt { get; set; } = Timer.MS();

		public BlendShapeContainer blendShape = new BlendShapeContainer();

		private IndexFormat _IndexFormat;


		public BasicMaterial(Material mat, ShadowCastingMode shadow, IndexFormat indexFormat) {
			this.shadow = shadow;
			material = mat;
			_IndexFormat = indexFormat;
		}

		public void Updated() {
			LastUpdatedAt = Timer.MS();
		}

		public CombineInstance GetCombineInstance() => new CombineInstance() { mesh = Mesh, subMeshIndex = 0 };

		public virtual void SetMesh(
			int cID,
			MeshParser parser,
			Transform[] bones,
			int subMeshIndex,
			Mesh mesh,
			bool forceMeshReset,
			bool isStaticMesh,
			Matrix4x4 rootMat,
			Transform renTransform,
			BlendShapeConfiguration blendShapeConf
		) {
			if (parser != null) mesh = parser.GetParsedMesh(mesh, subMeshIndex, forceMeshReset);

			Meshes[cID * 100 + subMeshIndex] = new AdvancedCombineInstance {
				Combine = new CombineInstance {
					transform = rootMat * renTransform.localToWorldMatrix, subMeshIndex = subMeshIndex, mesh = mesh
				}
			};

			Updated();
		}

		public virtual void Build() {
			Clear();

			Mesh = new Mesh { indexFormat = _IndexFormat };
#if MC_DEBUG
			var matName = material ? material.name : "null";
			Mesh.name = $"[MAT] {matName}";
#endif

			var cnt = Meshes.Count;
			var meshes = new CombineInstance[cnt];
			var i = 0;
			if (Application.isPlaying) {
				foreach (var pair in Meshes) meshes[i++] = pair.Value.Combine;
			} else {
				foreach (var pair in Meshes) {
					var res = pair.Value.Combine;
					res.realtimeLightmapScaleOffset =
						res.lightmapScaleOffset = MeshExtension.PlaceCubesInCube(cnt, i, .01f);
					meshes[i++] = res;
				}
			}

			Mesh.CombineMeshes(meshes, true, true, !Application.isPlaying);
		}

		public virtual void Clear() {
			if (Mesh) Object.DestroyImmediate(Mesh, true);
			Updated();
		}
	}
}