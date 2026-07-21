using System.Collections.Generic;
using TeoGames.Mesh_Combiner.Scripts.Combine;
using TeoGames.Mesh_Combiner.Scripts.Combine.ChunkContainer;
using UnityEngine;

namespace TeoGames.Mesh_Combiner.Demo._16___Complex_Visibility_Sample.Scripts {
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class MapCell : MonoBehaviour {
        [SerializeField] private AnimationCurve visibilityCurve;
        private MapController _Controller;
        private Vector2Int _GridPosition;
        private Dictionary<string, Material> _Materials;

        public void Initialize(MapController controller, Vector2Int gridPosition,
            Dictionary<string, Material> materials) {
            _Controller = controller;
            _GridPosition = gridPosition;
            _Materials = materials;

            GenerateTerrain();
        }

        public void UpdateVisibility(Vector3 center) {
            var distance = Vector3.Distance(center, transform.position);
            var scale = visibilityCurve.Evaluate(distance);
            var container = GetComponent<AbstractChunkContainer>();
            var renderers = container.GetRenderers();
            var renScale = new Vector3(scale, scale, scale);
            var renPos = Vector3.Lerp(Vector3.down * 10, Vector3.zero, scale);

            container.IsVisible = scale > 0;
            foreach (var ren in renderers) {
                ren.gameObject.SetActive(ren is not SkinnedMeshRenderer || scale >= 0.9f);
                ren.transform.localScale = renScale;
                ren.transform.localPosition = renPos;
            }
        }

        private void GenerateTerrain() {
            var mesh = new Mesh {
                name = "Generated Terrain"
            };

            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();

            // Group triangles by surface type
            var surfaceTriangles = new Dictionary<string, List<int>>();
            foreach (var surface in _Controller.RuntimeSurfaces) {
                surfaceTriangles[surface.name] = new List<int>();
            }

            var voxelSize = _Controller.voxelSize;
            var steps = Mathf.CeilToInt(_Controller.cellSize / voxelSize);

            for (var x = 0; x < steps; x++) {
                for (var z = 0; z < steps; z++) {
                    var xPos = x * voxelSize;
                    var zPos = z * voxelSize;

                    var height = GetHeight(xPos, zPos);
                    var surface = GetSurfaceForHeight(height);

                    if (!surface.HasValue) continue;

                    // Top Face
                    AddFace(
                        new Vector3(xPos, height, zPos),
                        new Vector3(xPos, height, zPos + voxelSize),
                        new Vector3(xPos + voxelSize, height, zPos + voxelSize),
                        new Vector3(xPos + voxelSize, height, zPos),
                        surface.Value.name,
                        vertices, uvs, surfaceTriangles
                    );

                    // Check neighbors for side faces
                    CheckAndAddSideFace(
                        xPos, zPos, height, xPos - voxelSize, zPos, surface.Value.name, vertices, uvs, surfaceTriangles,
                        true); // West
                    CheckAndAddSideFace(
                        xPos, zPos, height, xPos + voxelSize, zPos, surface.Value.name, vertices, uvs, surfaceTriangles,
                        true); // East
                    CheckAndAddSideFace(
                        xPos, zPos, height, xPos, zPos - voxelSize, surface.Value.name, vertices, uvs, surfaceTriangles,
                        false); // South
                    CheckAndAddSideFace(
                        xPos, zPos, height, xPos, zPos + voxelSize, surface.Value.name, vertices, uvs, surfaceTriangles,
                        false); // North
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);

            // Set SubMeshes
            mesh.subMeshCount = _Controller.RuntimeSurfaces.Count;
            var activeMaterials = new List<Material>();
            var subMeshIndex = 0;

            foreach (var surface in _Controller.RuntimeSurfaces) {
                var tris = surfaceTriangles[surface.name];
                mesh.SetTriangles(tris, subMeshIndex++);

                activeMaterials.Add(
                    _Materials.TryGetValue(surface.name, out var mat)
                        ? mat
                        : new Material(Shader.Find("Standard"))
                ); // Fallback
            }

            GetComponent<MeshRenderer>().sharedMaterials = activeMaterials.ToArray();
            mesh.RecalculateNormals();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            GetComponent<MeshCollider>().sharedMesh = mesh;

            // Re-init combinable if it exists or add it
            var combinable = GetComponent<Combinable>();
            if (combinable == null) combinable = gameObject.AddComponent<Combinable>();
            combinable.ClearCache(true);

            SpawnProps();
        }

        private float GetHeight(float localX, float localZ) {
            var globalX = _GridPosition.x * _Controller.cellSize + localX;
            var globalZ = _GridPosition.y * _Controller.cellSize + localZ;

            float rawHeight = 0;

            // Layer 1: Base terrain (Large features)
            rawHeight += Mathf.PerlinNoise(globalX * 0.02f, globalZ * 0.02f) * 10f;

            // Layer 2: Medium details
            rawHeight += Mathf.PerlinNoise(globalX * 0.15f, globalZ * 0.15f) * 3f;

            // Layer 3: Small details (Roughness)
            rawHeight += Mathf.PerlinNoise(globalX * 0.5f, globalZ * 0.5f) * 0.5f;

            return Mathf.Floor(rawHeight / _Controller.voxelSize) * _Controller.voxelSize;
        }

        private void CheckAndAddSideFace(float x, float z, float currentHeight, float nx, float nz, string surfaceName,
            List<Vector3> vertices, List<Vector2> uvs, Dictionary<string, List<int>> surfaceTriangles, bool isXAxis) {
            // Check if neighbor is outside current cell
            // If outside, we still calculate its height using global noise to match seams
            var neighborHeight = GetHeight(nx, nz);

            if (neighborHeight < currentHeight) {
                // Create face from currentHeight down to neighborHeight
                // Vertices depend on direction
                Vector3 bl, tl, tr, br;

                if (isXAxis) {
                    if (nx < x) // West Face
                    {
                        bl = new Vector3(x, neighborHeight, z + _Controller.voxelSize);
                        tl = new Vector3(x, currentHeight, z + _Controller.voxelSize);
                        tr = new Vector3(x, currentHeight, z);
                        br = new Vector3(x, neighborHeight, z);
                        AddFace(bl, tl, tr, br, surfaceName, vertices, uvs, surfaceTriangles);
                    } else // East Face
                    {
                        bl = new Vector3(x + _Controller.voxelSize, neighborHeight, z);
                        tl = new Vector3(x + _Controller.voxelSize, currentHeight, z);
                        tr = new Vector3(x + _Controller.voxelSize, currentHeight, z + _Controller.voxelSize);
                        br = new Vector3(x + _Controller.voxelSize, neighborHeight, z + _Controller.voxelSize);
                        AddFace(bl, tl, tr, br, surfaceName, vertices, uvs, surfaceTriangles);
                    }
                } else {
                    if (nz < z) // South Face
                    {
                        bl = new Vector3(x, neighborHeight, z);
                        tl = new Vector3(x, currentHeight, z);
                        tr = new Vector3(x + _Controller.voxelSize, currentHeight, z);
                        br = new Vector3(x + _Controller.voxelSize, neighborHeight, z);
                        AddFace(bl, tl, tr, br, surfaceName, vertices, uvs, surfaceTriangles);
                    } else // North Face
                    {
                        bl = new Vector3(x + _Controller.voxelSize, neighborHeight, z + _Controller.voxelSize);
                        tl = new Vector3(x + _Controller.voxelSize, currentHeight, z + _Controller.voxelSize);
                        tr = new Vector3(x, currentHeight, z + _Controller.voxelSize);
                        br = new Vector3(x, neighborHeight, z + _Controller.voxelSize);
                        AddFace(bl, tl, tr, br, surfaceName, vertices, uvs, surfaceTriangles);
                    }
                }
            }
        }

        private void AddFace(Vector3 bl, Vector3 tl, Vector3 tr, Vector3 br, string surfaceName,
            List<Vector3> vertices, List<Vector2> uvs, Dictionary<string, List<int>> surfaceTriangles) {
            int startIndex = vertices.Count;
            vertices.Add(bl);
            vertices.Add(tl);
            vertices.Add(tr);
            vertices.Add(br);

            uvs.Add(new Vector2(0, 0));
            uvs.Add(new Vector2(0, 1));
            uvs.Add(new Vector2(1, 1));
            uvs.Add(new Vector2(1, 0));

            surfaceTriangles[surfaceName].Add(startIndex);
            surfaceTriangles[surfaceName].Add(startIndex + 1);
            surfaceTriangles[surfaceName].Add(startIndex + 2);

            surfaceTriangles[surfaceName].Add(startIndex);
            surfaceTriangles[surfaceName].Add(startIndex + 2);
            surfaceTriangles[surfaceName].Add(startIndex + 3);
        }

        private SurfaceDefinition? GetSurfaceForHeight(float height) {
            // Iterate in reverse to find the highest matching surface
            for (var i = _Controller.RuntimeSurfaces.Count - 1; i >= 0; i--) {
                if (height >= _Controller.RuntimeSurfaces[i].heightStart) {
                    return _Controller.RuntimeSurfaces[i];
                }
            }

            return _Controller.RuntimeSurfaces.Count > 0 ? _Controller.RuntimeSurfaces[0] : null;
        }

        private void SpawnProps() {
            // Simple random prop spawning
            var propCount = 15; // Props per cell
            for (var i = 0; i < propCount; i++) {
                // Only spawn on top faces (normals pointing up)
                // Since we don't store normals explicitly in the list, we can just pick random vertices
                // But we should check if it's a "top" vertex. 
                // In our generation, vertices 0,1,2,3 of a face are BL, TL, TR, BR.
                // Top faces are added first in the loop.
                // A simpler way: just pick a random x,z in the cell, get height, and spawn there.

                var x = Random.Range(0f, _Controller.cellSize);
                var z = Random.Range(0f, _Controller.cellSize);

                // Snap to voxel center
                x = Mathf.Floor(x / _Controller.voxelSize) * _Controller.voxelSize + _Controller.voxelSize * 0.5f;
                z = Mathf.Floor(z / _Controller.voxelSize) * _Controller.voxelSize + _Controller.voxelSize * 0.5f;

                var height = GetHeight(
                    x - _Controller.voxelSize * 0.5f, z - _Controller.voxelSize * 0.5f); // GetHeight expects corner

                var surface = GetSurfaceForHeight(height);

                if (surface.HasValue && surface.Value.props != null && surface.Value.props.Count > 0) {
                    var propPrefab = surface.Value.props[Random.Range(0, surface.Value.props.Count)];
                    if (propPrefab != null) {
                        var prop = Instantiate(propPrefab, transform);
                        prop.transform.localPosition = new Vector3(x, height, z);
                        prop.gameObject.SetActive(true);
                    }
                }
            }
        }
    }
}