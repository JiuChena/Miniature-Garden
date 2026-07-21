using UnityEngine;

namespace TeoGames.Mesh_Combiner.Scripts.Extension {
	public static class TransformExtension {
		public static void RemoveAll(this Transform transform) {
			if (Application.isPlaying) {
				for (var i = transform.childCount - 1; i >= 0; i--) {
					Object.Destroy(transform.GetChild(i).gameObject);
				}
			} else {
				while (transform.childCount > 0) {
					Object.DestroyImmediate(transform.GetChild(0).gameObject);
				}
			}
		}
		public static void DisableAll(this Transform transform) {
			foreach (Transform child in transform) {
				child.gameObject.SetActive(false);
			}
		}

		public static void LazyRemoveAll(this Transform transform) {
			while (transform.childCount > 0) {
				transform.GetChild(0).gameObject.LazyDestroy();
			}
		}

		public static void SyncTransform(this Transform transform, Transform target, Transform alignParent) {
			transform.localPosition = target.position - alignParent.position;
			transform.localScale = target.lossyScale;
			transform.localEulerAngles = target.eulerAngles;
		}
	}
}