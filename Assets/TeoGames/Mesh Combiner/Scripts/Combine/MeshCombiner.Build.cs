using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeoGames.Mesh_Combiner.Scripts.Combine.CombinedMaterial;
using TeoGames.Mesh_Combiner.Scripts.Combine.MeshRendererManager;
using TeoGames.Mesh_Combiner.Scripts.Extension;
using TeoGames.Mesh_Combiner.Scripts.Profile;
using UnityEngine;
using UnityEngine.Events;

namespace TeoGames.Mesh_Combiner.Scripts.Combine {
    public partial class MeshCombiner {
        [Tooltip("Will trigger after each bake")] public UnityEvent onUpdated = new UnityEvent();

        private int _LastUpdateTime;
        private Renderer[] _RendererComponents = Array.Empty<Renderer>();

        private readonly Dictionary<AbstractCombinable, ToggleStatus> _ToggleUpdates =
            new Dictionary<AbstractCombinable, ToggleStatus>();

        private static readonly Dictionary<AbstractCombinable, ToggleStatus> ToRemoveUpdates =
            new Dictionary<AbstractCombinable, ToggleStatus>();

        protected async Task ParseRenderers() {
            if (!this) return;

            Timer.Start(maxBuildTime);

            // Enable renderers that should be active
            var renderers = new List<Renderer>();
            foreach (var ren in RendererManagers) {
                if (!ren.ShouldBeActive) continue;

                var res = ren.BuildRenderer();
                if (res) renderers.Add(res);
            }

            var list = renderers.ToArray();
            OnRenderersUpdated?.Invoke(list);
            await ToggleRenderers();
            if (!this) return;

            if (isVisible) {
                // Disable renderers that should be disabled
                foreach (var ren in RendererManagers) ren.BuildRenderer();

                _RendererComponents = list;
                onUpdated?.Invoke();
            }

            Timer.Stop();
        }

        private async Task ToggleRenderers() {
            if (!_ToggleUpdates.Any()) return;

#if DEBUG_BAKING
			Debug.LogError($">> ToggleRenderers > {name} > {isVisible} > {IsUpdateInProgress} > {_Updates.Count}");
#endif

            var i = 0;
            ToRemoveUpdates.Clear();
            foreach (var pair in _ToggleUpdates) {
                if (pair.Value < ToggleStatus.Skip) ToRemoveUpdates[pair.Key] = pair.Value;
            }

            foreach (var pair in ToRemoveUpdates) {
                if (i++ % 10 == 0 && Timer.IsTimeoutRequired) {
                    await Timer.Wait();
                    if (!this) return;
                }
                
                pair.Key.UpdateVisibility();
                _ToggleUpdates.Remove(pair.Key);
            }
        }

        private async Task UpdateMesh() {
            if (!this) return;

            var time = Timer.MS();
            Timer.Start(maxBuildTime);

            try {
                foreach (var ren in RendererManagers) ren.Reset();
            } catch (Exception e) {
                var nullCount = RendererManagers?.Count(r => r == null) ?? 0;
                var ex = new Exception($"Failed to reset renderers: {nullCount}", e);
                Debug.LogError(ex);

                throw ex;
            }

            foreach (var material in _Materials.List) {
                try {
                    var ren = GetRenderer(material);
                    var isChanged = clearMaterialCache || material.LastUpdatedAt >= _LastUpdateTime || !material.Mesh;

                    if (isChanged) {
                        if (Timer.IsTimeoutRequired) {
                            await Timer.Wait();
                            if (!this) return;
                        }

                        material.Build();
                        material.LastUpdatedAt = time;
                    }

                    ren.IsChanged |= isChanged;
                    if (material.Mesh.vertexCount > 0) ren.RegisterMaterial(material);
                } catch (Exception e) {
                    var ex = new Exception("Failed to update material", e);
                    Debug.LogError(ex);

                    throw ex;
                }
            }

            foreach (var ren in RendererManagers) {
                try {
                    if (Timer.IsTimeoutRequired) {
                        await Timer.Wait();
                        if (!this) return;
                    }

                    await ren.BuildMesh(Timer, clearMaterialCache);
                } catch (Exception e) {
                    var ex = new Exception("Failed to build mesh", e);
                    Debug.LogError(ex);

                    throw ex;
                }
            }

            _LastUpdateTime = Timer.MS();
            Timer.Stop();
        }

        protected AbstractMeshRenderer GetRenderer(BasicMaterial mat) {
            var blendShapeEnabled = !separateBlendShapes || mat.hasBlendShapes;
            var isStatic = mat.isStatic;
            var shadow = mat.shadow;

            foreach (var ren in RendererManagers) {
                if (ren.Validate(blendShapeEnabled, isStatic, shadow)) return ren;
            }

            var obj = new GameObject(name) {
                transform = {
                    parent = transform,
                    localPosition = Vector3Extensions.Zero,
                    localScale = Vector3Extensions.One,
                    localEulerAngles = Vector3Extensions.Zero
                }
            };

            var res = isStatic
                ? (AbstractMeshRenderer)new StaticMeshRenderer(shadow, obj)
                : new DynamicMeshRenderer(blendShapeEnabled, shadow, obj);
            RendererManagers.Add(res);

            return res;
        }
    }
}