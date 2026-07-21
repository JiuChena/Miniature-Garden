using System.Collections;
using UnityEngine;

namespace TeoGames.Mesh_Combiner.Demo.Tests.Scripts {
	[AddComponentMenu("Mesh Combiner/Demo/Tests/MC Toggle State")]
	[HelpURL("https://teogames.gitbook.io/dynamic-mesh-combiner")]
	public class ToggleState : MonoBehaviour {
		public MonoBehaviour target;
		public float intervalSec;

		private void OnEnable() {
			StopAllCoroutines();
			StartCoroutine(Toggle());
		}

		private IEnumerator Toggle() {
			while (true) {
				yield return new WaitForSeconds(intervalSec);
				target.enabled = !target.enabled;
			}
		}
	}
}