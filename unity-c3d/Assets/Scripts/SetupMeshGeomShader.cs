using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;

public class SetupMeshGeomShader {

    private List<Vector3[]> vertices;
    private List<Vector2[]> uv;
    private List<int[]> triangles;
    int[] VCount;
    int[] TCount;
    int[] maxTime;
    public SetupMeshGeomShader()
    {

    }

    public void allocVecs(int numTrees)
    {
        vertices = new List<Vector3[]>();
        uv = new List<Vector2[]>();
        triangles = new List<int[]>();
        VCount = new int[numTrees];
        TCount = new int[numTrees];
        maxTime = new int[numTrees];
    }


    public void setCapacity( List<MyTreeNode>[] root)
    {
        vertices.Clear();
        uv.Clear();
        triangles.Clear();
        for (int i = 0; i < root.Length; ++i)
        {
            maxTime[i] = root[i][root[i].Count - 1].epoch + root[i][root[i].Count - 1].points.Count + 1;
            int numVertices = 0;
            for (int j = 0; j < root[i].Count; ++j)
            {
                numVertices += root[i][j].points.Count > 3 ? root[i][j].points.Count + 2 : 4;
            }
            vertices.Add(new Vector3[numVertices+1]);
            uv.Add(new Vector2[numVertices+1]);
            triangles.Add(new int[3*numVertices]);
            
        }
    }

    void setupMesh(Mesh mesh, ParticleSystem ps,  Vector3[] v,  Vector2[] uv,  int[] tri, int VCount, int TCount)
    {
        mesh.Clear();

        mesh.SetVertices(v, 0, VCount);
        //mesh.vertices=v;
        mesh.SetTriangles(tri, 0, TCount, 0);
        //mesh.triangles=tri;
        mesh.SetUVs(0, uv, 0, VCount);
        //mesh.uv=uv;
        
        mesh.RecalculateBounds();

        if (ps != null)
        {
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
            em.rateOverTime = 2.0f * (float)count / (float)maxCount;
        }
        else
        {
            em.rateOverTime = 0.0f;
        }
    }

    void processMesh(List<MyTreeNode> root, float time, int i,
                                             float maxGrowth,
                                             float growRate,
                                             float diamLengthScale,
                                             float texScale,
                                             float LOD,
                                             out int VCount,
                                             out int TCount)
    {
        FillMeshData.fillVerticesTrianglesGeomShader( vertices[i],  triangles[i],  uv[i],  root, time,
                                                    maxGrowth,
                                                    growRate,
                                                    diamLengthScale,
                                                    texScale,
                                                    LOD,
                                                    out VCount,
                                                    out TCount);
    }

    public bool update(List<MyTreeNode>[] root,  root_component[] comp, float t, float last_t, bool isNight,
                       float maxGrowth,
                       float growRate,
                       float diamLengthScale,
                       float texScale,
                       float LOD,
                       bool forceUpdate)
    {
        List<int> toProcess = new List<int>();
        for (int i = 0; i < root.Length; ++i)
        {
            if (root == null || root[i] == null) continue;
            
            if (last_t < maxTime[i] || forceUpdate)
            {
                toProcess.Add(i);
            }
        }

        int nt = Math.Max(1, Environment.ProcessorCount);
        Thread[] td = new Thread[nt];
        for (int i = 0; i < nt; i++)
        {
            int startIdx = i;
            td[i] = new Thread(() => {
                float curTime = t;
                for (int j = startIdx; j < toProcess.Count; j += nt)
                {
                    int idx = toProcess[j];
                    processMesh(root[idx], curTime, idx,
                                             maxGrowth,
                                             growRate,
                                             diamLengthScale,
                                             texScale,
                                             LOD,
                                             out VCount[j],
                                             out TCount[j]);
                }
            });
            td[i].Start();
        }

        for (int i = 0; i < td.Length; ++i)
            td[i].Join();

        for (int i = 0; i < toProcess.Count; ++i)
        {
            int idx = toProcess[i];
            setupMesh((comp[idx].GetComponent<MeshFilter>()).sharedMesh, comp[idx].GetComponent<ParticleSystem>(),  vertices[idx],  uv[idx],  triangles[idx], VCount[idx], TCount[idx]);
            setMeshParticles(comp[idx].GetComponent<ParticleSystem>(), isNight, VCount[idx], root[idx].Count);
        }

        return toProcess.Count > 0;
    }

    public bool updateSingle( List<MyTreeNode>[] root,  root_component[] comp, float t, float last_t, bool isNight,
                       float maxGrowth,
                       float growRate,
                       float diamLengthScale,
                       float texScale,
                       float LOD,
                       bool forceUpdate)
    {
        List<int> toProcess = new List<int>();
        for (int i = 0; i < root.Length; ++i)
        {
            if (root == null || root[i] == null) continue;
            
            if (last_t < maxTime[i] || forceUpdate)
            {
                toProcess.Add(i);
            }
        }

        float curTime = t;
        for (int j = 0; j < toProcess.Count; j ++)
        {
            int idx = toProcess[j];
            processMesh(root[idx], curTime, idx,
                                        maxGrowth,
                                        growRate,
                                        diamLengthScale,
                                        texScale,
                                        LOD,
                                        out VCount[j],
                                        out TCount[j]);
        }

        for (int i = 0; i < toProcess.Count; ++i)
        {
            int idx = toProcess[i];
            setupMesh((comp[idx].GetComponent<MeshFilter>()).sharedMesh, comp[idx].GetComponent<ParticleSystem>(),  vertices[idx],  uv[idx],  triangles[idx], VCount[idx], TCount[idx]);
            setMeshParticles(comp[idx].GetComponent<ParticleSystem>(), isNight, VCount[idx], vertices[idx].Length);
        }

        return toProcess.Count > 0;
    }
}
