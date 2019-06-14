using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;

public class SetupMeshGeomShader
{
    private List<Vector3[]> vertices;
    private List<Vector2[]> uv;
    private List<int[]> triangles;
    private volatile List<int> toProcess;
    private volatile List<int> toSetup;
    int[] VCount;
    int[] TCount;
    int[] maxTime;
    private readonly bool _threaded;

    //thread data
    Thread[] td;
    private List<MyTreeNode>[] _root;
    private float _curTime;
    private float _maxGrowth;
    private float _growRate;
    private float _diamLengthScale;
    private float _texScale;
    private float _LOD;
    public int waitTime = 1;
    private volatile bool isRunning = true;

    public SetupMeshGeomShader(bool threaded)
    {
        _threaded = threaded;
        toProcess = new List<int>();
        toSetup = new List<int>();
        if(threaded) {
            int nt = Math.Max(1, Environment.ProcessorCount);
            td = new Thread[nt];
            for (int i = 0; i < nt; i++)
            {
                td[i] = new Thread(() =>
                {
                    ref float curTime = ref _curTime;
                    ref float maxGrowth = ref _maxGrowth;
                    ref float growRate = ref _growRate;
                    ref float diamLengthScale = ref _diamLengthScale;
                    ref float texScale = ref _texScale;
                    ref float LOD = ref _LOD;
                    ref bool run = ref isRunning;
                    ref int wait = ref waitTime;
                    Debug.Log("Thread started");
                    while (run)
                    {
                        int idx = -1;
                        lock (toProcess)
                        {
                            if (toProcess.Count > 0)
                            {
                                idx = toProcess[0];
                                toProcess.RemoveAt(0);
                            }
                        }
                        if (idx == -1)
                        {
                            Thread.Sleep(wait);
                        }
                        else
                        {
                            FillMeshData.fillVerticesTrianglesGeomShader(vertices[idx], triangles[idx], uv[idx], _root[idx], curTime,
                                                    maxGrowth,
                                                    growRate,
                                                    diamLengthScale,
                                                    texScale,
                                                    LOD,
                                                    out VCount[idx],
                                                    out TCount[idx]);
                            lock (toSetup)
                            {
                                toSetup.Add(idx);
                            }
                        }
                    }
                });
                td[i].Start();
            }
        }
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


    public void setCapacity(List<MyTreeNode>[] root)
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
            vertices.Add(new Vector3[numVertices + 1]);
            uv.Add(new Vector2[numVertices + 1]);
            triangles.Add(new int[3 * numVertices]);

        }
    }

    void setupMesh(Mesh mesh, ParticleSystem ps, Vector3[] v, Vector2[] uv, int[] tri, int VCount, int TCount)
    {
        mesh.Clear();

        mesh.SetVertices(v, 0, VCount);
        mesh.SetTriangles(tri, 0, TCount, 0);
        mesh.SetUVs(0, uv, 0, VCount);

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

    int a = 0, c = 0;
    public bool updateMulti(List<MyTreeNode>[] root, root_component[] comp, float t, float last_t, bool isNight,
                       float maxGrowth,
                       float growRate,
                       float diamLengthScale,
                       float texScale,
                       float LOD,
                       bool forceUpdate)
    {
        c++;
        if (toProcess.Count == 0)
        {
            
            lock(toProcess) { 
                for (int i = 0; i < root.Length; ++i)
                {
                    if (root == null || root[i] == null) continue;

                    if (last_t < maxTime[i] || forceUpdate)
                    {
                        a++;
                        toProcess.Add(i);
                        
                    }
                }
                _root = root;
                _curTime = t;
                _maxGrowth = maxGrowth;
                _growRate = growRate;
                _diamLengthScale = diamLengthScale;
                _texScale = texScale;
                _LOD = LOD;
            }
        }


        if (toSetup.Count > 0)
        {
            
            lock (toSetup)
            {
                for (int i = 0; i < toSetup.Count; ++i)
                {
                    int idx = toSetup[i];
                    setupMesh(comp[idx].mesh, comp[idx].ps, vertices[idx], uv[idx], triangles[idx], VCount[idx], TCount[idx]);
                    setMeshParticles(comp[idx].ps, isNight, VCount[idx], vertices[idx].Length);
                }
                toSetup.Clear();
            }
        }
        if(c == 100)
        {
            Debug.Log(" Rate: " + 1.0 * a / (1.0 * c));
            a = c = 0;
        }
        
        return toProcess.Count > 0;
    }

    public bool updateSingle(List<MyTreeNode>[] root, root_component[] comp, float t, float last_t, bool isNight,
                       float maxGrowth,
                       float growRate,
                       float diamLengthScale,
                       float texScale,
                       float LOD,
                       bool forceUpdate)
    {
        toProcess.Clear();
        for (int i = 0; i < root.Length; ++i)
        {
            if (root == null || root[i] == null) continue;

            if (last_t < maxTime[i] || forceUpdate)
            {
                toProcess.Add(i);
            }
        }

        float curTime = t;
        for (int j = 0; j < toProcess.Count; j++)
        {
            int idx = toProcess[j];
            FillMeshData.fillVerticesTrianglesGeomShader(vertices[idx], triangles[idx], uv[idx], root[idx], curTime,
                                                    maxGrowth,
                                                    growRate,
                                                    diamLengthScale,
                                                    texScale,
                                                    LOD,
                                                    out VCount[idx],
                                                    out TCount[idx]);
        }

        for (int j = 0; j < toProcess.Count; j++)
        {
            int idx = toProcess[j];
            setupMesh(comp[idx].mesh, comp[idx].ps, vertices[idx], uv[idx], triangles[idx], VCount[idx], TCount[idx]);
            setMeshParticles(comp[idx].ps, isNight, VCount[idx], vertices[idx].Length);
        }
        return toProcess.Count > 0;
    }

    public bool update(List<MyTreeNode>[] root, root_component[] comp, float t, float last_t, bool isNight,
                       float maxGrowth,
                       float growRate,
                       float diamLengthScale,
                       float texScale,
                       float LOD,
                       bool forceUpdate)
    {
        if (_threaded)
        {
            return updateMulti(root, comp, t, last_t, isNight,
                       maxGrowth,
                       growRate,
                       diamLengthScale,
                       texScale,
                       LOD,
                       forceUpdate);
        }
        else
        {
            return updateSingle(root, comp, t, last_t, isNight,
                       maxGrowth,
                       growRate,
                       diamLengthScale,
                       texScale,
                       LOD,
                       forceUpdate);
        }
    }

    ~SetupMeshGeomShader()
    {
        isRunning = false;
        for (int i = 0; i < td.Length; ++i) td[i].Join();
    }
}
