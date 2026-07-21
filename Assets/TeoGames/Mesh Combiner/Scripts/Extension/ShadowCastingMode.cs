using TeoGames.Mesh_Combiner.Scripts.Combine.CombinedMaterial;
using UnityEngine;
using UnityEngine.Rendering;

namespace TeoGames.Mesh_Combiner.Scripts.Extension {
	public static class ShadowCastingModeExtensions {
		public static BasicMaterial GetMaterialInstance(this ShadowCastingMode shadow, bool isStatic, Material mat, IndexFormat format) {
			return isStatic ? new BasicMaterial(mat, shadow, format) : new DynamicMaterial(mat, shadow, format);
		}
	}
}