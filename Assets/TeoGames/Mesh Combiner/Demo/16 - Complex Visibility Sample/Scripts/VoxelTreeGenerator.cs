using System.Collections.Generic;
using TeoGames.Mesh_Combiner.Scripts.Combine;
using UnityEngine;

namespace TeoGames.Mesh_Combiner.Demo._16___Complex_Visibility_Sample.Scripts {
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class VoxelTreeGenerator : MonoBehaviour, IDynamicProp {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [Header("Dimensions")] public float voxelSize = 0.5f;
        public int height = 6;
        public int trunkWidth = 1;
        public int leavesWidth = 3;
        public int leavesHeight = 3;

        [Header("Materials")] public Material baseMaterial;
        public Color woodColor = new Color(0.55f, 0.27f, 0.07f); // SaddleBrown
        public Color leavesColor = new Color(0.13f, 0.55f, 0.13f); // ForestGreen

        [ContextMenu("Generate Tree")]
        public void Generate() {
            var mesh = GenerateMesh();
            var materials = GenerateMaterials();

            // Apply
            GetComponent<MeshFilter>().sharedMesh = mesh;
            GetComponent<MeshRenderer>().sharedMaterials = materials;

            // Collider
            var col = GetComponent<MeshCollider>();
            if (col == null) col = gameObject.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;

            // Add Combinable
            var combinable = GetComponent<Combinable>();
            if (combinable == null) combinable = gameObject.AddComponent<Combinable>();
            combinable.ClearCache(true);
        }

        private Mesh GenerateMesh() {
            var mesh = new Mesh();
            mesh.name = "Voxel Tree";

            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();

            // 0: Wood, 1: Leaves
            var subMeshTriangles = new List<List<int>> { new List<int>(), new List<int>() };

            // Generate Trunk
            for (var y = 0; y < height; y++) {
                for (var x = -trunkWidth / 2; x < (trunkWidth + 1) / 2; x++) {
                    for (var z = -trunkWidth / 2; z < (trunkWidth + 1) / 2; z++) {
                        AddVoxel(x, y, z, 0, vertices, uvs, subMeshTriangles);
                    }
                }
            }

            // Generate Leaves
            var leavesStartY = Mathf.Max(0, height - 2);
            for (var y = leavesStartY; y < leavesStartY + leavesHeight; y++) {
                for (var x = -leavesWidth / 2; x < (leavesWidth + 1) / 2; x++) {
                    for (var z = -leavesWidth / 2; z < (leavesWidth + 1) / 2; z++) {
                        // Skip corners for a rounder look
                        if (Mathf.Abs(x) == leavesWidth / 2 && Mathf.Abs(z) == leavesWidth / 2) continue;

                        // Don't overlap with trunk if it extends this high (simple check)
                        if (y < height && Mathf.Abs(x) <= trunkWidth / 2 && Mathf.Abs(z) <= trunkWidth / 2) continue;

                        AddVoxel(x, y, z, 1, vertices, uvs, subMeshTriangles);
                    }
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(subMeshTriangles[0], 0);
            mesh.SetTriangles(subMeshTriangles[1], 1);

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        private Material[] GenerateMaterials() {
            var materials = new Material[2];

            if (baseMaterial != null) {
                materials[0] = new Material(baseMaterial) { color = woodColor };
                if (materials[0].HasProperty(BaseColor)) materials[0].SetColor(BaseColor, woodColor);

                materials[1] = new Material(baseMaterial) { color = leavesColor };
                if (materials[1].HasProperty(BaseColor)) materials[1].SetColor(BaseColor, leavesColor);
            } else {
                materials[0] = new Material(Shader.Find("Standard")) { color = woodColor };
                materials[1] = new Material(Shader.Find("Standard")) { color = leavesColor };
            }

            return materials;
        }

        private void AddVoxel(int x, int y, int z, int subMeshIndex, List<Vector3> vertices, List<Vector2> uvs,
            List<List<int>> subMeshTriangles) {
            var s = voxelSize;
            var h = s * 0.5f;
            var c = new Vector3(x * s, y * s, z * s);

            // Top
            AddFace(
                c + new Vector3(-h, h, -h), c + new Vector3(-h, h, h),
                c + new Vector3(h, h, h), c + new Vector3(h, h, -h),
                subMeshIndex, vertices, uvs, subMeshTriangles
            );

            // Bottom
            AddFace(
                c + new Vector3(-h, -h, h), c + new Vector3(-h, -h, -h),
                c + new Vector3(h, -h, -h), c + new Vector3(h, -h, h),
                subMeshIndex, vertices, uvs, subMeshTriangles
            );

            // Front (Z+)
            AddFace(
                c + new Vector3(-h, h, h), c + new Vector3(-h, -h, h),
                c + new Vector3(h, -h, h), c + new Vector3(h, h, h),
                subMeshIndex, vertices, uvs, subMeshTriangles
            );

            // Back (Z-)
            AddFace(
                c + new Vector3(h, h, -h), c + new Vector3(h, -h, -h),
                c + new Vector3(-h, -h, -h), c + new Vector3(-h, h, -h),
                subMeshIndex, vertices, uvs, subMeshTriangles
            );

            // Right (X+)
            AddFace(
                c + new Vector3(h, h, h), c + new Vector3(h, -h, h),
                c + new Vector3(h, -h, -h), c + new Vector3(h, h, -h),
                subMeshIndex, vertices, uvs, subMeshTriangles
            );

            // Left (X-)
            AddFace(
                c + new Vector3(-h, h, -h), c + new Vector3(-h, -h, -h),
                c + new Vector3(-h, -h, h), c + new Vector3(-h, h, h),
                subMeshIndex, vertices, uvs, subMeshTriangles
            );
        }

        private void AddFace(Vector3 bl, Vector3 tl, Vector3 tr, Vector3 br, int subMeshIndex, List<Vector3> vertices,
            List<Vector2> uvs, List<List<int>> subMeshTriangles) {
            var vIndex = vertices.Count;
            vertices.Add(bl);
            vertices.Add(tl);
            vertices.Add(tr);
            vertices.Add(br);

            uvs.Add(new Vector2(0, 0));
            uvs.Add(new Vector2(0, 1));
            uvs.Add(new Vector2(1, 1));
            uvs.Add(new Vector2(1, 0));

            // BL, TL, TR
            subMeshTriangles[subMeshIndex].Add(vIndex);
            subMeshTriangles[subMeshIndex].Add(vIndex + 1);
            subMeshTriangles[subMeshIndex].Add(vIndex + 2);

            // BL, TR, BR
            subMeshTriangles[subMeshIndex].Add(vIndex);
            subMeshTriangles[subMeshIndex].Add(vIndex + 2);
            subMeshTriangles[subMeshIndex].Add(vIndex + 3);
        }
    }
}