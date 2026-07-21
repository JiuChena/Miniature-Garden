using System;
using System.Collections.Generic;
using System.Linq;
using TeoGames.Mesh_Combiner.Scripts.Combine.ChunkContainer;
using TeoGames.Mesh_Combiner.Scripts.Extension;
using UnityEngine;

namespace TeoGames.Mesh_Combiner.Scripts.Combine {
    using InstancesType = Dictionary<AbstractCombinable, UpdateQueueItem>;

    [AddComponentMenu("Mesh Combiner/MC Chunk Combiner")]
    [HelpURL("https://teogames.gitbook.io/dynamic-mesh-combiner/components/combiner/mc-chunk-combiner")]
    public partial class ChunkMeshCombiner : AbstractMeshCombiner {
        public AbstractChunkContainer[] chunks;

        protected readonly InstancesType Instances = new InstancesType();

        public UpdateQueue<AbstractCombinable, UpdateQueueItem> UpdateQueue => updateQueue;

        private void Reset() {
            if (chunks != null && chunks.Any()) return;

            chunks = GetComponents<AbstractChunkContainer>();
            if (chunks.Any()) return;

            chunks = new AbstractChunkContainer[] { gameObject.AddComponent<GridChunkContainer>(), };
        }

        private void Awake() => Init();

        private void OnDestroy() => updateQueue.Stop();

        public override void Init() {
            if (!chunks.Any()) {
                Debug.LogError(
                    "MC Chunk Combiner doesnt have any chunks configured. Please add some or reset component to add default chunk",
                    this
                );
                return;
            }

            chunks.ForEach(InitChunk);
            updateQueue.Start(this);
        }

        private void InitChunk(AbstractChunkContainer chunk) {
            chunk.Init(
                rendererTypes: rendererTypes,
                maxBuildTime: maxBuildTime,
                bakeMaterials: bakeMaterials,
                separateBlendShapes: separateBlendShapes,
                clearMaterialCache: clearMaterialCache,
                indexFormat: indexFormat
            );
            chunk.OnRenderersUpdated += TriggerOnRendered;
        }

        private void TriggerOnRendered(Renderer[] renderers) {
            OnRenderersUpdated?.Invoke(renderers);
        }

        public override void Clear() {
            chunks.ForEach(chunk => chunk.Clear());
            updateQueue.Clear();
            Instances.Clear();

            if (IsLodReady) Lod.Clear();
        }

        public override bool IsBaked(AbstractCombinable combinable) {
            if (Instances.TryGetValue(combinable, out var item)) return item.container.IsBaked(combinable, item.containerKey);

            var cell = FindNewCell(combinable);
            if (!cell) {
#if DEBUG_BAKING
                Debug.LogError($"{name} > No cell found > {combinable.name} > {chunks.Length}", combinable);
#endif
                return false;
            }
            
            return cell.IsBaked(combinable, null);
        }

        public override Renderer[] GetRenderers() => chunks
            .SelectMany(c => c.GetRenderers())
            .Concat(IsLodReady ? Lod.Combiner.GetRenderers() : Array.Empty<Renderer>())
            .ToArray();

        public override void Include(AbstractCombinable combinable) => updateQueue.Schedule(combinable);

        public override void Exclude(AbstractCombinable combinable) {
            if (Instances.TryGetValue(combinable, out var item)) {
                if (item.container) item.container.Exclude(combinable, item.containerKey);
                Instances.Remove(combinable);
            }

            updateQueue.Remove(combinable);
        }

        private AbstractChunkContainer FindNewCell(AbstractCombinable combinable) {
            var bestCompability = float.NegativeInfinity;
            var pos = AbstractChunkContainer.GetPosition(combinable);
            AbstractChunkContainer bestMatch = null;

            if (combinable.IsStatic) {
                var parent = combinable.GetComponentInParent<AbstractChunkContainer>();
                if (parent && chunks.Contains(parent) && parent.Compability(pos) >= 1) {
                    return parent;
                }
            }

            foreach (var chunk in chunks) {
                var compability = chunk.Compability(pos);
                switch (compability) {
                    case 1: return chunk;
                    case 0: continue;
                    default:
                        if (bestCompability > compability) continue;

                        bestCompability = compability;
                        bestMatch = chunk;

                        break;
                }
            }

            return bestMatch;
        }
    }
}