using System;
using System.Collections.Generic;
using TeoGames.Mesh_Combiner.Scripts.Extension;
using UnityEngine;

namespace TeoGames.Mesh_Combiner.Scripts.Extractor {
	[Serializable]
	public class BindOptions {
		public Transform source;
		public Transform target;
	}

	[AddComponentMenu("Mesh Combiner/Utils/MC Bind Transform")]
	[HelpURL("https://teogames.gitbook.io/dynamic-mesh-combiner")]
	public class BindTransform : MonoBehaviour {
		public List<BindOptions> list = new List<BindOptions>();
		public Transform alignParent;

		private void LateUpdate() => list.ForEach(UpdateTransform);

		public void Sync(bool isStatic, Transform target, Transform source) {
			var obj = new BindOptions() { source = source, target = target };
			if (!isStatic) list.Add(obj);
			UpdateTransform(obj);
		}

		protected void UpdateTransform(BindOptions obj) {
			if (!obj.source) return;

			if (alignParent.gameObject.scene.name == null) {
				obj.target.SyncTransform(obj.source, alignParent);
			} else {
				var pos = alignParent.InverseTransformPoint(obj.source.position);
				var rot = Quaternion.Inverse(Quaternion.Inverse(obj.source.rotation) * alignParent.rotation);

				obj.target.localPosition = Vector3.Scale(pos, alignParent.localScale);
				obj.target.localRotation = rot;
				obj.target.localScale = obj.source.lossyScale;
			}
		}
	}
}