using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;

public class SetupMeshMulti {

    private List<Vector3>[] vertices;
    private List<Vector2>[] uv;
    private List<int>[] triangles;
    int[] count;
    int[] numTri;
    int[] maxTime;

    public SetupMeshMulti()
    {
        
    }

    public void allocVecs(int numTrees)
    {
        vertices = new List<Vector3>[numTrees];
        uv = new List<Vector2>[numTrees];
        triangles = new List<int>[numTrees];
        count = new int[numTrees];
        numTri = new int[numTrees];
        maxTime = new int[numTrees];
        for (int i = 0; i < numTrees; ++i)
        {
            vertices[i] = new List<Vector3>();
            uv[i] = new List<Vector2>();
            triangles[i] = new List<int>();
        }
    }


    public void setCapacity(List<MyTreeNode>[] root,
                            int numCircPoints)
    {
        long ntri = 0;
        for (int i = 0; i < root.Length; ++i)
        {
            maxTime[i] = root[i][root[i].Count - 1].epoch + root[i][root[i].Count - 1].points.Count + 1;
            int numVertices = 0;
            for (int j = 0; j < root[i].Count; ++j)
            {
                numVertices += root[i][j].points.Count;
            }
            vertices[i] = new List<Vector3>();
            uv[i] = new List<Vector2>();
            triangles[i] = new List<int>();

            uv[i].Capacity = numVertices * numCircPoints - root[i].Count * (numCircPoints - 1);
            vertices[i].Capacity = numVertices * numCircPoints - root[i].Count * (numCircPoints - 1);
            triangles[i].Capacity = 3 * 2 * (numVertices * numCircPoints);
            ntri += 2 * (numVertices * numCircPoints);
        }
        Debug.Log("Number of triangles: " + ntri);
    }

    void setupMesh(Mesh mesh, ParticleSystem ps, List<Vector3> v, List<Vector2> uv, List<int> tri)
    {
        mesh.Clear();
        mesh.SetVertices(v);
        mesh.SetTriangles(tri, 0);
        mesh.SetUVs(0, uv);

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        if (ps != null) { 
            var sh = (ps.GetComponent<ParticleSystem>()).shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Mesh;
            sh.mesh = mesh;
        }
    }

    void setMeshParticles(ParticleSystem ps, bool isNight, int count, int maxCount)
    {
        if (ps == null) return;

        var em = ps.emission;
        if (isNight && count > 0)
        {
            //Debug.Log("rate : " + count + "/" + maxCount);
            em.rateOverTime = 2.0f * (float)count / (float)maxCount;
        }
        else
        {
            em.rateOverTime = 0.0f;
        }
    }

    int processMesh(List<MyTreeNode> root, float time, int i, 
                                             float maxGrowth,
                                             float growRate,
                                             float diamLengthScale,
                                             int numCircPoints,
                                             Vector3[] shape,
                                             float[] coords,
                                             float texScale,
                                             float LOD)
    {
        vertices[i].Clear();
        triangles[i].Clear();
        uv[i].Clear();
        int ret = FillMeshData.fillVerticesTriangles(vertices[i], triangles[i], uv[i], root, time, 
                                                    maxGrowth,
                                                    growRate,
                                                    diamLengthScale,
                                                    numCircPoints,
                                                    shape,
                                                    coords,
                                                    texScale,
                                                    LOD);
        numTri[i] = triangles[i].Count / 3;
        return ret;
    }

    public void update(List<MyTreeNode>[] root, root_component[] comp, float t, float last_t, bool isNight,
                       float maxGrowth,
                       float growRate,
                       float diamLengthScale,
                       int numCircPoints,
                       Vector3[] shape,
                       float[] coords,
                       float texScale,
                       float LOD)
    {
        List<int> toProcess = new List<int>();
        for (int i = 0; i < root.Length; ++i)
        {
            if (root == null || root[i] == null) continue;

            count[i] = root[i].Count;
            if (last_t < maxTime[i])
            {
                toProcess.Add(i);
            }
        }

        int[] counter = new int[root.Length];
        for (int i = 0; i < counter.Length; ++i) counter[i] = 0;

        int nt = Math.Max(1, Environment.ProcessorCount/2), maxThreads;
        Thread[] td = new Thread[nt];
        for (int i = 0; i < nt; i++)
        {
            int startIdx = i;
            td[i] = new Thread(() => {
                float curTime = t;
                for (int j = startIdx; j < toProcess.Count; j += nt)
                { 
                    int idx = toProcess[j];
                    count[idx] = processMesh(root[idx], curTime, idx, 
                                             maxGrowth,
                                             growRate,
                                             diamLengthScale,
                                             numCircPoints,
                                             shape,
                                             coords,
                                             texScale,
                                             LOD);
                    counter[idx]++;
                }
            });
            td[i].Start();
        }

        for (int i = 0; i < td.Length; ++i)
            td[i].Join();

        for (int i = 0; i < toProcess.Count; ++i)
        {
            int idx = toProcess[i];
            setupMesh((comp[idx].GetComponent<MeshFilter>()).sharedMesh, comp[idx].GetComponent<ParticleSystem>(), vertices[idx], uv[idx], triangles[idx]);
            setMeshParticles(comp[idx].GetComponent<ParticleSystem>(), isNight, count[idx], root[idx].Count);
        }

        if (toProcess.Count > 0)
        {
            long total = 0;
            for (int i = 0; i < numTri.Length; ++i)
            {
                total += numTri[i];
            }
        }
    }

    public void updateSingle(List<MyTreeNode>[] root, root_component[] comp, float t, float last_t, bool isNight,
                       float maxGrowth,
                       float growRate,
                       float diamLengthScale,
                       int numCircPoints,
                       Vector3[] shape,
                       float[] coords,
                       float texScale,
                       float LOD)
    {
        List<int> toProcess = new List<int>();
        for (int i = 0; i < root.Length; ++i)
        {
            if (root == null || root[i] == null) continue;

            count[i] = root[i].Count;
            if (last_t < maxTime[i])
            {
                toProcess.Add(i);
            }
        }

        int[] counter = new int[root.Length];
        for (int i = 0; i < counter.Length; ++i) counter[i] = 0;


        float curTime = t;
        for (int j = 0; j < toProcess.Count; ++j)
        {
            int idx = toProcess[j];
            count[idx] = processMesh(root[idx], curTime, idx,
                                        maxGrowth,
                                        growRate,
                                        diamLengthScale,
                                        numCircPoints,
                                        shape,
                                        coords,
                                        texScale,
                                        LOD);
            counter[idx]++;
        }

        for (int i = 0; i < toProcess.Count; ++i)
        {
            int idx = toProcess[i];
            setupMesh((comp[idx].GetComponent<MeshFilter>()).sharedMesh, comp[idx].GetComponent<ParticleSystem>(), vertices[idx], uv[idx], triangles[idx]);
            setMeshParticles(comp[idx].GetComponent<ParticleSystem>(), isNight, count[idx], root[idx].Count);
        }

        if (toProcess.Count > 0)
        {
            long total = 0;
            for (int i = 0; i < numTri.Length; ++i)
            {
                total += numTri[i];
            }
        }
    }
}
