using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;
using System;
using System.Threading;

public class SetupMeshGeomShader
{
    private List<Vector3[]> vertices;
    private List<Vector2[]> uv;
    private List<int[]> triangles;

    private List<ComputeBuffer> vv;
    private List<ComputeBuffer> uu;
    private List<ComputeBuffer> tt;

    private volatile ConcurrentQueue<int> toProcess;
    private volatile ConcurrentQueue<int> toSetup;
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
    private volatile bool isRunning = true;
    private ManualResetEvent _mr;
    private CountdownEvent _cd;

    public SetupMeshGeomShader(bool threaded, int numObj, int tCount = 1)
    {
        _threaded = threaded;
        toProcess = new ConcurrentQueue<int>();
        toSetup = new ConcurrentQueue<int>();
        if(threaded) {
            int nt = Math.Max(1, tCount);
            _mr = new ManualResetEvent(false);
            _cd = new CountdownEvent(numObj);

            //Start zeroed
            for (int i = 0; i < numObj; ++i)
                _cd.Signal();

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
                    while (run)
                    {
                        int idx = -1;
                        if (!toProcess.TryDequeue(out idx))
                        {
                            _mr.WaitOne();
                        }
                        else
                        {
                            FillMeshData.fillVerticesTrianglesGeomShader(vertices[idx], triangles[idx], uv[idx], _root[idx], curTime,
                                                    diamLengthScale,
                                                    texScale,
                                                    LOD,
                                                    out VCount[idx],
                                                    out TCount[idx]);
                           
                            toSetup.Enqueue(idx);
                            _cd.Signal();
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

        vv = new List<ComputeBuffer>();
        uu = new List<ComputeBuffer>();
        tt = new List<ComputeBuffer>();
    }

    public void clearVecs()
    {
        vertices.Clear();
        uv.Clear();
        triangles.Clear();

        if(vv != null)
        {
            for(int i = 0; i < vv.Count; i++)
            {
                vv[i].Dispose();
                uu[i].Dispose();
                tt[i].Dispose();
            }
        }
    }


    public void setCapacity(List<MyTreeNode>[] root)
    {

        clearVecs();

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
            triangles.Add(new int[numVertices]);

            vv.Add(new ComputeBuffer(numVertices + 1, 3*sizeof(float)));
            uu.Add(new ComputeBuffer(numVertices + 1, 2*sizeof(float)));
            tt.Add(new ComputeBuffer(numVertices, sizeof(int)));

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

    int a = 0, b = 0;
    public bool updateMulti(List<MyTreeNode>[] root, root_component[] comp, float t, float last_t, bool isNight,
                       float maxGrowth,
                       float growRate,
                       float diamLengthScale,
                       float texScale,
                       float LOD,
                       bool forceUpdate)
    {
        _cd.Wait();
        _cd.Reset();

        _mr.Reset();
        while (toSetup.Count > 0) { 
            int idx;
            if (toSetup.TryDequeue(out idx))
            {
                vv[idx].SetData(vertices[idx], 0, 0, VCount[idx]);
                uu[idx].SetData(uv[idx], 0, 0, VCount[idx]);
                tt[idx].SetData(triangles[idx], 0, 0, TCount[idx]);

                comp[idx].mat.SetBuffer("_vertices", vv[idx]);
                comp[idx].mat.SetBuffer("_uv", uu[idx]);
                comp[idx].mat.SetBuffer("_triangles", tt[idx]);
                comp[idx].mat.SetMatrix("_obj2World", comp[idx].obj2World);

                Graphics.DrawProcedural(comp[idx].mat, new Bounds(comp[idx].pos, new Vector3(10, 10, 10)), MeshTopology.Points, TCount[idx]);
            }
        }

            
        for (int i = 0; i < root.Length; ++i)
        {
            if (root == null || root[i] == null) continue;

            if (last_t < maxTime[i] || forceUpdate)
            {
                toProcess.Enqueue(i);
                        
            }
        }
        _root = root;
        _curTime = t;
        _maxGrowth = maxGrowth;
        _growRate = growRate;
        _diamLengthScale = diamLengthScale;
        _texScale = texScale;
        _LOD = LOD;
        _mr.Set();
        
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
        
        for (int i = 0; i < root.Length; ++i)
        {
            if (root == null || root[i] == null) continue;

            if (last_t < maxTime[i] || forceUpdate)
            {
                toProcess.Enqueue(i);
            }
        }

        float curTime = t;
        while (toProcess.Count > 0) { 
            int idx;
            if (toProcess.TryDequeue(out idx))
            {
                FillMeshData.fillVerticesTrianglesGeomShader(vertices[idx], triangles[idx], uv[idx], root[idx], curTime,
                                                        diamLengthScale,
                                                        texScale,
                                                        LOD,
                                                        out VCount[idx],
                                                        out TCount[idx]);
                toSetup.Enqueue(idx);
            }
        }

        while (toSetup.Count > 0)
        {
            int idx;
            if (toSetup.TryDequeue(out idx))
            {
                vv[idx].SetData(vertices[idx], 0, 0, VCount[idx]);
                uu[idx].SetData(uv[idx], 0, 0, VCount[idx]);
                tt[idx].SetData(triangles[idx], 0, 0, TCount[idx]);

                comp[idx].mat.SetBuffer("_vertices", vv[idx]);
                comp[idx].mat.SetBuffer("_uv", uu[idx]);
                comp[idx].mat.SetBuffer("_triangles", tt[idx]);
                comp[idx].mat.SetMatrix("_obj2World", comp[idx].obj2World);

                Graphics.DrawProcedural(comp[idx].mat, new Bounds(comp[idx].pos, new Vector3(1000000, 1000000, 1000000)), MeshTopology.Points, TCount[idx]);
                setMeshParticles(comp[idx].ps, isNight, VCount[idx], vertices[idx].Length);
            }
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

    public void OnDestroy()
    {
        isRunning = false;
        for (int i = 0; i < td.Length; ++i) td[i].Join();
        clearVecs();
    }
}
