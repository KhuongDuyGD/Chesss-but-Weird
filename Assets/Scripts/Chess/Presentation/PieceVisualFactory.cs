using System.Collections.Generic;
using UnityEngine;

/// <summary>Builds visual mesh parts without owning board or match state.</summary>
public sealed class PieceVisualFactory : System.IDisposable
{
    private readonly Dictionary<(Mesh, int), List<MeshComponentData>> cache = new Dictionary<(Mesh, int), List<MeshComponentData>>();

    public List<MeshComponentData> SplitMeshIntoSpatialGroups(Mesh sourceMesh, int expectedGroupCount)
    {
        if (!sourceMesh) return new List<MeshComponentData>();
        var key = (sourceMesh, expectedGroupCount);
        if (!cache.TryGetValue(key, out List<MeshComponentData> parts))
        {
            parts = BuildParts(sourceMesh, expectedGroupCount);
            cache.Add(key, parts);
        }
        // Callers sort by world position; never let that reorder the shared cache.
        return new List<MeshComponentData>(parts);
    }

    public void Dispose()
    {
        foreach (var parts in cache.Values)
            foreach (var part in parts)
                if (part.mesh)
                {
                    if (Application.isPlaying) Object.Destroy(part.mesh);
                    else Object.DestroyImmediate(part.mesh);
                }
        cache.Clear();
    }

    private static List<MeshComponentData> BuildParts(Mesh sourceMesh, int expectedGroupCount)
    {
        Vector3[] vertices = sourceMesh.vertices;
        Vector3[] normals = sourceMesh.normals;
        Vector2[] uvs = sourceMesh.uv;
        int subMeshCount = Mathf.Max(1, sourceMesh.subMeshCount);
        bool splitAlongZ = sourceMesh.bounds.size.z > sourceMesh.bounds.size.x;
        List<TriangleData> triangles = new List<TriangleData>();

        for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
        {
            int[] subMeshTriangles = sourceMesh.GetTriangles(subMesh);
            for (int i = 0; i < subMeshTriangles.Length; i += 3)
            {
                int a = subMeshTriangles[i];
                int b = subMeshTriangles[i + 1];
                int c = subMeshTriangles[i + 2];
                float centerAxis = splitAlongZ
                    ? (vertices[a].z + vertices[b].z + vertices[c].z) / 3f
                    : (vertices[a].x + vertices[b].x + vertices[c].x) / 3f;
                triangles.Add(new TriangleData(subMesh, a, b, c, centerAxis));
            }
        }

        List<MeshComponentData> components = new List<MeshComponentData>();
        if (triangles.Count == 0)
            return components;

        int groupCount = Mathf.Clamp(expectedGroupCount, 1, triangles.Count);
        List<TriangleData>[] groups = GroupTrianglesByCenterKMeans(triangles, groupCount);
        for (int i = 0; i < groups.Length; i++)
            if (groups[i].Count > 0)
                components.Add(BuildMeshComponent(sourceMesh, vertices, normals, uvs, subMeshCount, groups[i]));

        return components;
    }

    private static List<TriangleData>[] GroupTrianglesByCenterKMeans(List<TriangleData> triangles, int groupCount)
    {
        List<TriangleData>[] groups = CreateTriangleGroups(groupCount);
        if (groupCount == 1)
        {
            groups[0].AddRange(triangles);
            return groups;
        }

        float minX = triangles[0].centerAxis;
        float maxX = triangles[0].centerAxis;
        for (int i = 1; i < triangles.Count; i++)
        {
            minX = Mathf.Min(minX, triangles[i].centerAxis);
            maxX = Mathf.Max(maxX, triangles[i].centerAxis);
        }

        float[] centers = new float[groupCount];
        float range = Mathf.Max(0.0001f, maxX - minX);
        for (int i = 0; i < centers.Length; i++)
            centers[i] = minX + range * ((i + 0.5f) / groupCount);

        for (int iteration = 0; iteration < 12; iteration++)
        {
            groups = CreateTriangleGroups(groupCount);
            for (int i = 0; i < triangles.Count; i++)
                groups[FindNearestCenter(centers, triangles[i].centerAxis)].Add(triangles[i]);

            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i].Count == 0)
                    continue;

                float sum = 0f;
                for (int j = 0; j < groups[i].Count; j++)
                    sum += groups[i][j].centerAxis;

                centers[i] = sum / groups[i].Count;
            }
        }

        return groups;
    }

    private static List<TriangleData>[] CreateTriangleGroups(int groupCount)
    {
        List<TriangleData>[] groups = new List<TriangleData>[groupCount];
        for (int i = 0; i < groups.Length; i++)
            groups[i] = new List<TriangleData>();

        return groups;
    }

    private static int FindNearestCenter(float[] centers, float value)
    {
        int nearestIndex = 0;
        float nearestDistance = Mathf.Abs(value - centers[0]);
        for (int i = 1; i < centers.Length; i++)
        {
            float distance = Mathf.Abs(value - centers[i]);
            if (distance >= nearestDistance)
                continue;

            nearestDistance = distance;
            nearestIndex = i;
        }

        return nearestIndex;
    }

    private static MeshComponentData BuildMeshComponent(
        Mesh sourceMesh,
        Vector3[] vertices,
        Vector3[] normals,
        Vector2[] uvs,
        int subMeshCount,
        List<TriangleData> triangles)
    {
        Bounds localBounds = new Bounds(vertices[triangles[0].a], Vector3.zero);
        for (int i = 0; i < triangles.Count; i++)
        {
            localBounds.Encapsulate(vertices[triangles[i].a]);
            localBounds.Encapsulate(vertices[triangles[i].b]);
            localBounds.Encapsulate(vertices[triangles[i].c]);
        }

        Vector3 pivot = new Vector3(localBounds.center.x, localBounds.min.y, localBounds.center.z);
        Dictionary<int, int> oldToNewIndex = new Dictionary<int, int>();
        List<Vector3> newVertices = new List<Vector3>();
        List<Vector3> newNormals = new List<Vector3>();
        List<Vector2> newUvs = new List<Vector2>();
        List<int>[] newTriangles = new List<int>[subMeshCount];
        for (int i = 0; i < newTriangles.Length; i++)
            newTriangles[i] = new List<int>();

        for (int i = 0; i < triangles.Count; i++)
        {
            TriangleData triangle = triangles[i];
            newTriangles[triangle.subMesh].Add(GetOrCreateMeshIndex(triangle.a, vertices, normals, uvs, pivot, oldToNewIndex, newVertices, newNormals, newUvs));
            newTriangles[triangle.subMesh].Add(GetOrCreateMeshIndex(triangle.b, vertices, normals, uvs, pivot, oldToNewIndex, newVertices, newNormals, newUvs));
            newTriangles[triangle.subMesh].Add(GetOrCreateMeshIndex(triangle.c, vertices, normals, uvs, pivot, oldToNewIndex, newVertices, newNormals, newUvs));
        }

        Mesh mesh = new Mesh
        {
            name = $"{sourceMesh.name} Runtime Piece"
        };
        mesh.SetVertices(newVertices);
        mesh.subMeshCount = subMeshCount;
        for (int i = 0; i < newTriangles.Length; i++)
            mesh.SetTriangles(newTriangles[i], i);

        if (newNormals.Count == newVertices.Count)
            mesh.SetNormals(newNormals);
        else
            mesh.RecalculateNormals();

        if (newUvs.Count == newVertices.Count)
            mesh.SetUVs(0, newUvs);

        mesh.RecalculateBounds();
        return new MeshComponentData(mesh, pivot);
    }

    private static int GetOrCreateMeshIndex(
        int oldIndex,
        Vector3[] vertices,
        Vector3[] normals,
        Vector2[] uvs,
        Vector3 pivot,
        Dictionary<int, int> oldToNewIndex,
        List<Vector3> newVertices,
        List<Vector3> newNormals,
        List<Vector2> newUvs)
    {
        if (oldToNewIndex.TryGetValue(oldIndex, out int newIndex))
            return newIndex;

        newIndex = newVertices.Count;
        oldToNewIndex.Add(oldIndex, newIndex);
        newVertices.Add(vertices[oldIndex] - pivot);

        if (normals != null && normals.Length == vertices.Length)
            newNormals.Add(normals[oldIndex]);

        if (uvs != null && uvs.Length == vertices.Length)
            newUvs.Add(uvs[oldIndex]);

        return newIndex;
    }

    public readonly struct MeshComponentData
    {
        public readonly Mesh mesh;
        public readonly Vector3 pivot;

        public MeshComponentData(Mesh mesh, Vector3 pivot)
        {
            this.mesh = mesh;
            this.pivot = pivot;
        }
    }

    private readonly struct TriangleData
    {
        public readonly int subMesh;
        public readonly int a;
        public readonly int b;
        public readonly int c;
        public readonly float centerAxis;

        public TriangleData(int subMesh, int a, int b, int c, float centerAxis)
        {
            this.subMesh = subMesh;
            this.a = a;
            this.b = b;
            this.c = c;
            this.centerAxis = centerAxis;
        }
    }
}
