using System;
using System.Linq;
using UnityEngine;

namespace TeoGames.Mesh_Combiner.Scripts.Extension {
	public static class MaterialExtension {
		public static long GetCombineID(this Material material, int offset) {
			try {
				return offset + material.GetInstanceID() * 1000;
			} catch (Exception) {
				return offset;
			}
		}

		public static bool HasTextures(this Material material) {
			return material.GetTexturePropertyNameIDs().Any(id => material.GetTexture(id) != null);
		}
	}
}