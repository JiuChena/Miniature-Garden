using System;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using TeoGames.Mesh_Combiner.Scripts.Combine;
using TeoGames.Mesh_Combiner.Scripts.Combine.MeshRendererManager;
using TeoGames.Mesh_Combiner.Scripts.Extension;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

namespace TeoGames.Mesh_Combiner.Scripts.Extractor {
	public enum ShadowConfiguration {
		[UsedImplicitly] Off,
		[UsedImplicitly] On,
		[UsedImplicitly] TwoSided,
		[UsedImplicitly] ShadowsOnly,
		[UsedImplicitly] Original,
	}

	[AddComponentMenu("Mesh Combiner/MC Mesh Extractor")]
	[HelpURL("https://teogames.gitbook.io/dynamic-mesh-combiner/components/mc-extractor")]
	public class MeshExtractor : MonoBehaviour {
		[Tooltip("Will show all meshes by default if TRUE")]
		public bool showByDefault = true;

		[Tooltip("Meshes will be scaled independent from game object scale if TRUE")]
		public bool useRelativeScale;

		[Tooltip("Define which renderers can be used")]
		public TargetRendererType rendererTypes =
			TargetRendererType.MeshRenderer | TargetRendererType.SkinnerMeshRenderer;

		[Tooltip("Will replace all materials with specified one if passed")]
		public Material globalMaterial;

		[Tooltip("Will add mesh combiner to bake all meshes into single one")]
		public bool combineMeshes = true;

		public ShadowConfiguration shadow = ShadowConfiguration.Original;

		public UnityEvent<Mesh[]> onUpdated;

		public int Count => _Meshes.Length;
		private AbstractCombinable[] _Meshes = new AbstractCombinable[] { };
		private BindTransform _Bind;
		public MeshCombiner combiner;

		public virtual void Clean() => transform.RemoveAll();

		public virtual void Show(int id) => Show(_Meshes[id]);

		protected virtual void Show(AbstractCombinable target) {
			if (IsVisible(target)) return;

			if (target.IsStatic) target.gameObject.SetActive(true);
			else target.transform.localScale = Vector3Extensions.One;
		}

		public virtual void ShowAll() => _Meshes.ForEach(Show);

		public virtual void Hide(int id) => Hide(_Meshes[id]);

		protected virtual void Hide(AbstractCombinable target) {
			if (!IsVisible(target)) return;

			if (target.IsStatic) target.gameObject.SetActive(false);
			else target.transform.localScale = Vector3Extensions.Zero;
		}

		[UsedImplicitly]
		public virtual bool IsVisible(int id) => IsVisible(_Meshes[id]);

		protected virtual bool IsVisible(AbstractCombinable target) => target.IsStatic
			? target.gameObject.activeSelf
			: target.transform.localScale == Vector3Extensions.One;

		public virtual void HideAll() => _Meshes.ForEach(Hide);

		protected virtual bool IsValidCombinable(Combinable combinable) => true;

		[UsedImplicitly]
		public virtual Task Build(Transform original) =>
			Build(original, Vector3Extensions.Zero, Vector3Extensions.Zero, 0);

		public virtual async Task Build(Transform original, Vector3 offset, Vector3 rotation, float scale) {
			Clean();

			var instance = new GameObject {
				name = "Extracted Meshes",
				transform = {
					parent = transform,
					localPosition = offset,
					localScale = Vector3Extensions.One,
					localEulerAngles = rotation,
				}
			};

			var isPrefab = original.gameObject.scene.name == null && !combineMeshes;
			var isStaticMode = rendererTypes == TargetRendererType.MeshRenderer || isPrefab;
			if (combineMeshes) {
				instance.SetActive(false);
				combiner = instance.AddComponent<MeshCombiner>();
				combiner.rendererTypes = isStaticMode ? TargetRendererType.MeshRenderer : rendererTypes;
				combiner.onUpdated = new UnityEvent();
				combiner.onUpdated.AddListener(TriggerUpdate);
				combiner.bakeMaterials = !globalMaterial;
				instance.SetActive(true);
			}

			_Bind = instance.AddComponent<BindTransform>();
			_Bind.alignParent = original;

			try {
				var parent = instance.transform;
				_Meshes = original
					.GetComponentsInChildren<Combinable>(false)
					.Select(c => ParseCombinable(parent, isStaticMode, scale, c))
					.NotNull()
					.ToArray();
			} catch (Exception ex) {
				Debug.LogException(ex);
				throw;
			}

			if (combineMeshes) await combiner.UpdateTask;
			else await Task.Yield();
		}

		private void TriggerUpdate() => onUpdated?.Invoke(
			combiner.GetRenderers().Select(
				c =>
					c.TryGetComponent<SkinnedMeshRenderer>(out var smr)
						? smr.sharedMesh
						: c.GetComponent<MeshFilter>().sharedMesh
			).ToArray()
		);

		private AbstractCombinable ParseCombinable(Transform parent, bool isStatic, float scale, Combinable obj) {
			if (!IsValidCombinable(obj) || !obj.enabled) return null;

			obj.ClearCache();
			var root = new GameObject {
				name = obj.name,
				tag = obj.tag,
				transform = {
					parent = parent,
					localPosition = Vector3Extensions.Zero,
					localScale = Vector3Extensions.One,
					localEulerAngles = Vector3Extensions.Zero,
				}
			};

			var trans = root.transform;
			var cache = obj.GetCache();
			var objTransform = obj.transform;
			var objScale = useRelativeScale ? Vector3Extensions.One : objTransform.lossyScale;
			var isCombStatic = false;

			if (isStatic || !cache.isSkinnedMesh) {
				var filter = root.AddComponent<MeshFilter>();
				filter.sharedMesh = cache.mesh.Scale(objScale, scale);

				var ren = root.AddComponent<MeshRenderer>();
				ren.shadowCastingMode = GetRendererShadowCastingMode(cache);
				ren.sharedMaterials = globalMaterial
					? Enumerable.Repeat(globalMaterial, cache.renderer.sharedMaterials.Length).ToArray()
					: cache.renderer.sharedMaterials;

				if (obj.isStatic) {
					_Bind.Sync(true, trans, objTransform);
					isCombStatic = true;
				} else {
					var bone = new GameObject {
						name = "Align anchor",
						transform = {
							parent = parent,
							localPosition = Vector3Extensions.Zero,
							localScale = Vector3Extensions.One,
							localEulerAngles = Vector3Extensions.Zero,
						}
					}.transform;
					trans.parent = bone;
					
					_Bind.Sync(false, bone, objTransform);
				}
			} else {
				var ren = root.AddComponent<SkinnedMeshRenderer>();
				ren.sharedMesh = cache.mesh.Scale(objScale, scale);
				ren.shadowCastingMode = GetRendererShadowCastingMode(cache);
				ren.sharedMaterials = globalMaterial
					? Enumerable.Repeat(globalMaterial, cache.renderer.sharedMaterials.Length).ToArray()
					: cache.renderer.sharedMaterials;

				ren.bones = (cache.Bones ?? new[] { objTransform })
					.Select(
						b => {
							var bone = new GameObject {
								name = b.name,
								transform = {
									parent = trans,
									localPosition = Vector3Extensions.Zero,
									localScale = Vector3Extensions.One,
									localEulerAngles = Vector3Extensions.Zero,
								}
							}.transform;

							_Bind.Sync(false, bone, b);

							return bone;
						}
					)
					.ToArray();
				ren.rootBone = ren.bones[0];
			}

			root.SetActive(showByDefault);
			var combinable = root.AddComponent<Combinable>();
			combinable.isStatic = isCombStatic;
			combinable.ClearCache();

			return combinable;
		}

		private ShadowCastingMode GetRendererShadowCastingMode(CombinableCache cache) {
			return shadow == ShadowConfiguration.Original
				? cache.renderer.shadowCastingMode
				: (ShadowCastingMode)shadow;
		}
	}
}