using System;
using System.Collections.Generic;
using TeoGames.Mesh_Combiner.Scripts.Extension;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TeoGames.Mesh_Combiner.Scripts.Combine.CombinedMaterial.MaterialBake {
	public class MeshParser {
		private static readonly Vector2[] EmptyUV = Array.Empty<Vector2>();
		private static readonly Color[] EmptyColors = Array.Empty<Color>();
		
		private readonly Vector2 _Scale;
		private readonly Vector2 _Offset;

		protected readonly Dictionary<long, Mesh> BakeCache = new Dictionary<long, Mesh>();

		public MeshParser(float scale, Vector2 offset) {
			_Scale = new Vector2(scale, scale);
			_Offset = offset;
		}

		public MeshParser(Vector2 scale, Vector2 offset) {
			_Scale = scale;
			_Offset = offset;
		}

		protected void FixUV(Mesh mesh, int subMeshIndex) {
			var subMesh = mesh.GetSubMesh(subMeshIndex);
			FixUVChannel(mesh, subMesh, mesh.uv, 0);

#if DMC_REMOVE_UV
			mesh.SetUVs(1, EmptyUV);
			mesh.SetUVs(2, EmptyUV);
			mesh.SetUVs(3, EmptyUV);
			mesh.SetUVs(4, EmptyUV);
#endif
#if DMC_REMOVE_COLORS
			mesh.SetColors(EmptyColors);
#endif
			// FixUVChannel(mesh, mesh.uv2, 1);
			// FixUVChannel(mesh, mesh.uv3, 2);
		}

		protected void FixUVChannel(Mesh mesh, SubMeshDescriptor subMesh, Vector2[] uv, int channel) {
			if (uv.Length == 0) {
				var cnt = mesh.vertices.Length;
				var pos = new Vector2(0.5f, 0.5f) * _Scale + _Offset;
				uv = new Vector2[cnt];
				for (var i = 0; i < cnt; i++) uv[i] = pos;
			} else {
				var start = subMesh.firstVertex;
				var end = start + subMesh.vertexCount;
				for (var i = start; i < end; i++) uv[i] = uv[i] * _Scale + _Offset;
			}

			mesh.SetUVs(channel, uv);
		}

		public Mesh GetParsedMesh(Mesh mesh, int subMeshIndex, bool forceReset) {
			Mesh newMesh;
			if (mesh.IsCacheDisabled()) {
				newMesh = mesh;

				var key = $"[P-{subMeshIndex}]";
				if (!mesh.name.Contains(key)) {
					FixUV(mesh, subMeshIndex);
					mesh.name += key;
				}

				return newMesh;
			}

			long id = mesh.GetHashCode() * 100 + subMeshIndex;
			if (forceReset || !BakeCache.TryGetValue(id, out newMesh)) {
				BakeCache[id] = newMesh = Object.Instantiate(mesh);
#if MC_DEBUG
				newMesh.name = $"[UV-{subMeshIndex}] {mesh.name}";
#endif
				FixUV(newMesh, subMeshIndex);
			}

			return newMesh;
		}
	}
}