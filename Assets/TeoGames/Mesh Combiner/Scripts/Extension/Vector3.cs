using UnityEngine;

namespace TeoGames.Mesh_Combiner.Scripts.Extension {
	public static class Vector3Extensions {
		public static readonly Vector3 Zero = Vector3.zero;
		public static readonly Vector3 One = Vector3.one;

		public static Vector3 RoundTo(this Vector3 v, float length) {
			return new Vector3(
				Mathf.Floor(v.x / length) * length,
				Mathf.Floor(v.y / length) * length,
				Mathf.Floor(v.z / length) * length
			);
		}

		public static Vector3 Round(this Vector3 v) {
			return new Vector3(
				Mathf.Round(v.x),
				Mathf.Round(v.y),
				Mathf.Round(v.z)
			);
		}

		public static Vector2 ToVector2(this Vector3 v) => new(v.x, v.z);

		public static Vector3 ToVector3(this Vector2 v) => new(v.x, 0, v.y);

		public static Vector3 Divide(this Vector3 a, Vector3 b) => new(
			b.x == 0 ? a.x : a.x / b.x,
			b.y == 0 ? a.y : a.y / b.y,
			b.z == 0 ? a.z : a.z / b.z
		);

		public static Vector3 Multiply(this Vector3 a, Vector3 b) => new(a.x * b.x, a.y * b.y, a.z * b.z);

		public static float FlatDistance(this Vector3 pos, Vector3 target) {
			return Vector2.Distance(pos.ToVector2(), target.ToVector2());
		}
		public static Vector3 Flat(this Vector3 v) => new(v.x, 0, v.z);
		public static Vector3 FlatZ(this Vector3 v) => new(v.x, v.y, 0);

		public static Vector3 AlignPosition(this Vector3 v, Transform target, Vector3 mask) {
			var local = target.InverseTransformPoint(v);
			local.Scale(mask);

			return target.TransformPoint(local);
		}

		public static Vector3 Lerp(this Vector3 v, Vector3 b, float max, float t) {
			return Vector3.Distance(v, b) > max ? b : Vector3.Lerp(v, b, t);
		}
	}
}