#define USE_MULTI_THREAD
//#define USE_GEOM_SHADER
using System.Collections.Generic;
using UnityEngine;
using System;

public class root_of_shame : MonoBehaviour {

    [Header("Start position")]
    private Vector3 startPosition  = new Vector3(0.0f, 0.0f, 0.0f);
    public Vector3 startDirection  = new Vector3(0.0f, 1.0f, 0.0f);

    [Header("Step and length")]
    [Range(0.0f, 1.0f)]
    public float meanStep          = 0.3f;
    [Range(0.0f, 0.1f)]
    public float varStep           = 0.05f;
    [Range(0.0f, 5000.0f)]
    public float maxLength         = 1000.0f;

    [Header("Branching angles")]
    [Range(-90.0f, 90.0f)]
    public float meanGrowNoiseDir  = 0.0f;
    [Range(0.0f, 90.0f)]
    public float varGrowNoiseDir   = 10.0f;
    [Range(0.0f, 90.0f)]
    public float meanChildNoiseDir = 40.0f;
    [Range(0.0f, 90.0f)]
    public float varChildNoiseDir  = 10.0f;
    [Space(5)]
    [Header("Branching rate and growth")]
    [Range(0.0f, 1.0f)]
    public float childRate         = 0.8f;
    [Range(0.0f, 1.0f)]
    public float levelLengthRatio  = 0.6f;
    [Range(0.0f, 1.0f)]
    public float LeftRightRate     = 0.5f;
    [Range(0.0f, 0.5f)]
    public float diamLengthScale   = 0.02f;
    //Disabled
    //[Range(0.0f, 0.01f)]
    private float growRate          = 0.001f;
    [Space(5)]
    [Header("Tree nodes settings")]
    //[Range(0, 500)]
    private int maxGrowth           = 100;
    [Range(0, 20)]
    public int maxLevel            = 10;
    [Range(0, 100)]
    public int minNodesBChild      = 5;
    [Range(10, 5000)]
    public int maxNodes            = 500;
    [Range(0, 100000)]
    public int maxIterations       = 2000;

    [Header("Surface discretization (CPU only)")]
    [Range(3, 30)]
    public int numCircPoints       = 4;
    [Header("2D/3D")]
    public bool planar             = true;
    [Range(0.0f, 180.0f)]
    public float nonPAngle         = 10.0f;

    [Header("Animation Settings")]
    [Range(0.0f, 100.0f)]
    public float speed             = 3.0f;
    public int minEpoch            = 0;
    public int maxEpoch            = 50;
    public bool useGlobalEpoch     = false;

    [Header("Texture Settings")]
    [Range(0.01f, 1.0f)]
    public float texScale          = 0.3f;
    [Range(0.0f, 10.0f)]
    public float LOD               = 0.1f;
    [Space(5)]

    [Header("Default Material")]
    public Material defMat;
    [Header("Geometry Shader Material")]
    public Material geomMat;
    [Header("Force update every frame (benchmark)")]
    public  bool forceUpdate = false;
    [Header("#CPU Threads")]
    [Range(1, 16)]
    public int threads = 2;

    private DayNightCycle dnc;

    private Vector3[] circle;
    private float[] xCoords;

    private List<MyTreeNode>[] root;
    private root_component[] comp;
    private Mesh[] mesh;
    private ParticleSystem[] ps;
    
    float t = 0.0f;

#if !USE_GEOM_SHADER
    SetupMeshMulti mt;

    void allocVecs()
    {
        mt.allocVecs(comp.Length);
    }

    void setCapacity()
    {
        mt.setCapacity(root, numCircPoints);
    }

    bool updateMesh(float t, float last_t)
    {
        return mt.update(root, comp, t, last_t, (dnc && dnc.isNightTime()), maxGrowth, growRate, diamLengthScale, numCircPoints, circle, xCoords, texScale, LOD, forceUpdate);
    }
#else
    SetupMeshGeomShader gs;
    void allocVecs()
    {
        gs.allocVecs(comp.Length);
    }

    void setCapacity()
    {
        gs.setCapacity(root);
    }

    bool updateMesh(float t, float last_t)
    {
        return gs.update(root, comp, t, last_t, (dnc && dnc.isNightTime()), maxGrowth, growRate, diamLengthScale, texScale, LOD, forceUpdate);
    }
#endif
    void Awake()
    {
        comp = (root_component[])transform.GetComponentsInChildren<root_component>();
        root = new List<MyTreeNode>[comp.Length];
#if !USE_GEOM_SHADER
#if USE_MULTI_THREAD
        mt = new SetupMeshMulti(true, comp.Length, threads);
#else
        mt =  new SetupMeshMulti(false, comp.Length);
#endif
        Material mat = defMat;
#else
#if USE_MULTI_THREAD
        gs =  new SetupMeshGeomShader(true, comp.Length, threads);
#else
        gs =  new SetupMeshGeomShader(false, comp.Length);
#endif
        Material mat = geomMat;
#endif
        for (int i = 0; i < comp.Length; ++i)
        {
            (comp[i].GetComponent<MeshRenderer>()).material = comp[i].mat = new Material(mat);
            root[i] = GenerateRootSkeleton.generateSkeleton(comp[i],
                                                            useGlobalEpoch,
                                                            minEpoch,
                                                            maxEpoch,
                                                            maxIterations,
                                                            maxLength,
                                                            levelLengthRatio,
                                                            meanGrowNoiseDir,
                                                            varGrowNoiseDir,
                                                            meanChildNoiseDir,
                                                            varChildNoiseDir,
                                                            nonPAngle,
                                                            meanStep,
                                                            varStep,
                                                            LeftRightRate,
                                                            planar,
                                                            childRate,
                                                            maxLevel,
                                                            maxNodes,
                                                            minNodesBChild);
        }

        allocVecs();
        setCapacity();
        GameObject go = GameObject.Find("DayNightCycle");

        if (go)
            dnc = go.GetComponent<DayNightCycle>();


        circle = new Vector3[numCircPoints];
        xCoords = new float[numCircPoints];
        double step = 2.0 * Math.PI / (double)numCircPoints;
        for (int i = 0; i < numCircPoints; ++i)
        {
            circle[i].x = (float)Math.Cos((double)i * step);
            circle[i].y = (float)Math.Sin((double)i * step);
            circle[i].z = 0.0f;
            xCoords[i] = (float)i / (float)numCircPoints;
        }

    }

    void Reset()
    {
        for (int i = 0; i < comp.Length; ++i)
        {
            root[i] = GenerateRootSkeleton.generateSkeleton(comp[i],
                                                            useGlobalEpoch,
                                                            minEpoch,
                                                            maxEpoch,
                                                            maxIterations,
                                                            maxLength,
                                                            levelLengthRatio,
                                                            meanGrowNoiseDir,
                                                            varGrowNoiseDir,
                                                            meanChildNoiseDir,
                                                            varChildNoiseDir,
                                                            nonPAngle,
                                                            meanStep,
                                                            varStep,
                                                            LeftRightRate,
                                                            planar,
                                                            childRate,
                                                            maxLevel,
                                                            maxNodes,
                                                            minNodesBChild);
        }
        setCapacity();
        t = 0.0f;
    }

    bool toggle = false;
    float last_t;
    void Update()
    {
        float LocalSpeed = speed;
        if (Input.GetKeyUp(KeyCode.Space))
        {
            t = 0.0f;
            if (toggle)
            {
                toggle = !toggle;
            }
            else
            {
                LocalSpeed = 1000000.0f;
                toggle = !toggle;
            }
        }
        if (Input.GetKeyUp(KeyCode.Return))
        {
            Reset();
        }

        last_t = t;
        t += LocalSpeed * Time.deltaTime;

        if (comp == null) return;

        updateMesh(t, last_t);
    }
#if USE_MULTI_THREAD
    private void OnDestroy()
    {
#if USE_GEOM_SHADER
        gs.OnDestroy();
#else
        mt.OnDestroy();
#endif
     }
#endif
}
