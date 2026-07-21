using UnityEngine;

namespace TeoGames.Mesh_Combiner.Scripts.Extension {
	public static class ColliderExtension {
		public static bool Contains(this Collider collider, Vector3 point) {
			return Vector3.Distance(collider.ClosestPoint(point), point) < .001f;
		}

		public static Vector3 GetRandomPoint(this Collider collider) {
			var size = collider.bounds.size.Flat() / 2;
			var tries = 10;

			while (tries-- > 0) {
				var rnd = collider.transform.position + size * Random.Range(-1f, 1f);
				if (!collider.Contains(rnd)) continue;

				return rnd;
			}

			return collider.transform.position;
		}
	}
}