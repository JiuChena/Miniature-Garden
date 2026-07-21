using System.Collections.Generic;
using System.Linq;
using TeoGames.Mesh_Combiner.Scripts.Combine.Interfaces;
using TeoGames.Mesh_Combiner.Scripts.Extension;
using UnityEngine;

namespace TeoGames.Mesh_Combiner.Scripts.Combine {
    using UpdatesType = Dictionary<AbstractCombinable, UpdateType>;

    public partial class MeshCombiner : ICombinerVisibilityTogglable {
        [SerializeField, HideInInspector] private bool isVisible = true;

        public bool IsVisible
        {
            get => isVisible;
            set
            {
                if (isVisible == value) return;

                isVisible = value;
                if (Application.isPlaying) {
                    if (IsLodReady) Lod.Combiner.chunk.IsVisible = value;
                    if (value) OnBecomeVisible();
                    else OnBecomeInvisible();
                }
            }
        }

        private void OnBecomeVisible() {
            if (!this) return;

#if DEBUG_BAKING
			Debug.LogError($"----- {name} > OnBecomeVisible > {IsUpdateInProgress} > {_Updates.Count}", this);
#endif

            if (_Updates.Any()) {
                if (IsUpdateInProgress) {
                    ToggleVisibility(_Updates);
                    ToggleRenderers(true);
                    ScheduleUpdate(true);
                } else {
                    ScheduleUpdate(_Updates.CopyAndClear());
                    UpdateTask.ContinueWith(() => {
                        if (isVisible) ToggleRenderers(true);
                    });
                }
            } else {
                _ToggleUpdates.Clear();
                ToggleRenderers(true);
            }
        }

        private void ToggleVisibility(UpdatesType list) {
            list.Keys.ForEach(u => {
                if (!u) return;
#if DEBUG_BAKING
				Debug.LogError($"----- {u.name} > ToggleVisibility > {name} > {IsUpdateInProgress}", this);
#endif
                u.UpdateVisibility();
            });
        }

        private void ToggleRenderers(bool status) {
#if DEBUG_BAKING
            Debug.LogError($"----- {name} > ToggleRenderers", this);
#endif
            
            GetRenderers().ForEach(r => r.gameObject.SetActive(status));
        }

        private void OnBecomeInvisible() {
#if DEBUG_BAKING
			Debug.LogError($"----- {name} > OnBecomeInvisible > {IsUpdateInProgress} > {_Updates.Count}", this);
#endif

            ToggleVisibility(_Updates);
            ToggleRenderers(false);
        }
    }
}