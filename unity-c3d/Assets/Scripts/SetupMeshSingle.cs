using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SetupMeshSingle {

    private List<Vector3> vertices;
    private List<Vector2> uv;
    private List<int> triangles;

    void allocVecsS()
    {
        vertices = new List<Vector3>();
        uv = new List<Vector2>();
        triangles = new List<int>();
    }

    //TODO
    /*void setCapacityS()
    {
        int maxVertices = 0;
        int minCount = 0;
        for (int i = 0; i < comp.Length; ++i)
        {
            root[i] = generateSkeleton(i);
            maxTime[i] = root[i][root[i].Count - 1].epoch + root[i][root[i].Count - 1].points.Count + 1;
            int numVertices = 0;
            for (int j = 0; j < root[i].Count; ++j)
            {
                numVertices += root[i][j].points.Count;
            }

            maxVertices = Math.Max(maxVertices, numVertices);
            minCount = Math.Min(minCount, root[i].Count);
        }

        //uv.Capacity = maxVertices * numCircPoints - minCount * (numCircPoints - 1);
        //vertices.Capacity = maxVertices * numCircPoints - minCount * (numCircPoints - 1);
        //triangles.Capacity = 3 * (2 * maxVertices * numCircPoints);
    }

    int processMeshS(float time, int i)
    {
        vertices.Clear();
        triangles.Clear();
        uv.Clear();
        return fillVerticesTriangles(vertices, triangles, uv, root[i], time);
    }

    void setupMeshS(int i)
    {
        setupMesh(i, vertices, uv, triangles);
    }

    void updateS()
    {
        for (int i = 0; i < comp.Length; ++i)
        {
            if (root == null || root[i] == null) continue;

            int count;
            if (last_t < maxTime[i])
            {
                count = processMeshS(t, i);
                setupMeshS(i);
            }
            else
            {
                count = root[i].Count;
            }
            setParticles(i, count, root[i].Count);

        }
    }*/
}
