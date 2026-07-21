using System.Collections.Generic;
using System.Linq;
using TeoGames.Mesh_Combiner.Scripts.Combine;
using TeoGames.Mesh_Combiner.Scripts.Combine.ChunkContainer;
using UnityEngine;

namespace TeoGames.Mesh_Combiner.Demo._16___Complex_Visibility_Sample.Scripts {
    public class MapController : MonoBehaviour {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [Header("Visibility")] public Transform visibilityCenter;

        [Header("Map Settings")] public Vector2Int mapGridSize = new Vector2Int(10, 10);
        public float cellSize = 25f;
        public float voxelSize = 0.5f;
        public Material baseMaterial;

        [Header("Surfaces")] public List<SurfaceDefinition> surfaces = new List<SurfaceDefinition>();
        public List<SurfaceDefinition> RuntimeSurfaces { get; private set; }

        [Header("Prefabs")] public MapCell mapCellPrefab;

        [Header("Animals")] public List<GameObject> animalPrefabs;
        public int animalCount = 10;

        private List<MapCell> _Cells;

        private void Start() {
            GenerateMap();
        }

        [ContextMenu("Generate Map")]
        private void GenerateMap() {
            var controller = GetComponentInParent<ChunkMeshCombiner>();

            ClearMap();
            _Cells = new List<MapCell>();
            if (controller) {
                controller.chunks = controller.chunks.Take(1).ToArray();
                controller.Init();
            }

            // Create runtime copy of surfaces to avoid modifying original data
            RuntimeSurfaces = new List<SurfaceDefinition>();
            foreach (var s in surfaces) {
                var newS = s;
                if (s.props != null) newS.props = new List<GameObject>(s.props);
                RuntimeSurfaces.Add(newS);
            }

            // Sort surfaces by height to ensure correct lookup
            RuntimeSurfaces.Sort((a, b) => a.heightStart.CompareTo(b.heightStart));

            // Create materials for each surface
            var materials = new Dictionary<string, Material>();
            foreach (var surface in RuntimeSurfaces) {
                if (baseMaterial != null) {
                    var mat = new Material(baseMaterial) {
                        color = surface.baseColor
                    };
                    // Also try setting _BaseColor for URP/HDRP compatibility if standard color doesn't work
                    if (mat.HasProperty(BaseColor)) mat.SetColor(BaseColor, surface.baseColor);
                    materials[surface.name] = mat;
                }

                // Pre-generate dynamic props
                if (surface.props != null) {
                    for (int i = 0; i < surface.props.Count; i++) {
                        var propPrefab = surface.props[i];
                        if (propPrefab != null && propPrefab.GetComponent<IDynamicProp>() != null) {
                            // Instantiate template
                            var template = Instantiate(propPrefab, transform);
                            template.name = $"{propPrefab.name}_Template";
                            template.SetActive(false); // Hide template

                            // Generate
                            template.GetComponent<IDynamicProp>().Generate();

                            // Replace in list
                            surface.props[i] = template;
                        }
                    }
                }
            }

            // Generate Grid
            for (var x = 0; x < mapGridSize.x; x++) {
                for (var y = 0; y < mapGridSize.y; y++) {
                    var position = new Vector3(x * cellSize, 0, y * cellSize) + transform.position;
                    var cell = Instantiate(mapCellPrefab, position, Quaternion.identity, transform);
                    cell.name = $"Cell_{x}_{y}";
                    _Cells.Add(cell);
                    cell.Initialize(this, new Vector2Int(x, y), materials);
                }
            }

            if (controller != null) {
                controller.chunks = controller.chunks.Concat(GetComponentsInChildren<AbstractChunkContainer>())
                    .ToArray();
                controller.Init();
            }

            SpawnAnimals();
        }

        private void Update() {
            var center = visibilityCenter.position;
            foreach (var cell in _Cells) {
                cell.UpdateVisibility(center);
            }
        }

        private void SpawnAnimals() {
            if (animalPrefabs == null || animalPrefabs.Count == 0) return;

            for (var i = 0; i < animalCount; i++) {
                var prefab = animalPrefabs[Random.Range(0, animalPrefabs.Count)];
                if (prefab == null) continue;

                var x = Random.Range(0, mapGridSize.x * cellSize);
                var z = Random.Range(0, mapGridSize.y * cellSize);

                // Spawn high up, AnimalController will snap to ground
                var position = new Vector3(x, 50f, z) + transform.position;
                Instantiate(prefab, position, Quaternion.identity, transform);
            }
        }

        [ContextMenu("Clear Map")]
        private void ClearMap() {
            while (transform.childCount > 0) {
                var child = transform.GetChild(0);
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }

        [ContextMenu("Setup Default Surfaces")]
        private void SetupDefaultSurfaces() {
            surfaces = new List<SurfaceDefinition> {
                new SurfaceDefinition { name = "Sand", baseColor = Color.yellow, heightStart = 0f },
                new SurfaceDefinition { name = "Grass", baseColor = Color.green, heightStart = 2f },
                new SurfaceDefinition { name = "Mountain", baseColor = Color.gray, heightStart = 4f }
            };
        }
    }
}