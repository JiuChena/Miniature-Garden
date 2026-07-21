using System;
using TeoGames.Mesh_Combiner.Scripts.Extension;
using UnityEngine;

namespace TeoGames.Mesh_Combiner.Scripts.Combine {
    public partial class Combinable {
        [Tooltip("Will disable renderer when combined")] public bool disableRenderer = true;

        // TODO Need to find a way to do it without affecting performance for the rest 99% of use cases
        // [Tooltip("Will disable collider when combined")]
        // public bool disableCollider;

        [Tooltip("Will remove all materials when combined")] public bool removeMaterials;

        private BakeStatus _IsBaked;

        public override void ShowOriginalMesh() {
            if (!IsCached) return;
#if DEBUG_BAKING
			Debug.LogError($"{name} > Combinable > ShowOriginalMesh");
#endif
            _IsBaked = BakeStatus.NotBaked;
            if (disableRenderer) cache.renderer.Enable();
            // if (disableCollider) cache.collider.Enable();
            if (removeMaterials) cache.renderer.sharedMaterials = cache.materials;
        }

        public override void HideOriginalMesh() {
            if (!IsCached) return;
#if DEBUG_BAKING
			Debug.LogError($"{name} > Combinable > ShowOriginalMesh");
#endif
            _IsBaked = BakeStatus.Baked;
            if (disableRenderer) cache.renderer.Disable();
            // if (disableCollider) cache.collider.Disable();
            if (removeMaterials) cache.renderer.sharedMaterials = Array.Empty<Material>();
        }

        public override void UpdateVisibility() {
            if (!cache.renderer) return;

            var visibility = GetBakeStatus();

#if DEBUG_BAKING
            Debug.LogError($"Update visibility > {name} > {_IsBaked} > {visibility}", this);
#endif

            if (_IsBaked == visibility) return;

            if (visibility == BakeStatus.Baked) {
                HideOriginalMesh();
            } else {
                ShowOriginalMesh();
            }
        }

        protected virtual BakeStatus GetBakeStatus() {
            return LastCombiner && IsActive && LastCombiner.IsBaked(this)
                ? BakeStatus.Baked
                : BakeStatus.NotBaked;
        }

        public enum BakeStatus {
            None,
            Baked,
            NotBaked
        }
    }
}