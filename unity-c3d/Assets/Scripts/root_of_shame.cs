#define USE_MULTI_THREAD
#define USE_GEOM_SHADER
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.Threading;

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
    [Range(0.0f, 0.01f)]
    public float growRate          = 0.001f;
    [Space(5)]
    [Header("Tree nodes settings")]
    [Range(0, 500)]
    public int maxGrowth           = 100;
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

    private DayNightCycle dnc;

    private Vector3[] circle;
    private float[] xCoords;

    private List<MyTreeNode>[] root;
    private root_component[] comp;
    
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
#if USE_MULTI_THREAD
        return mt.update(root, comp, t, last_t, (dnc && dnc.isNightTime()), maxGrowth, growRate, diamLengthScale, numCircPoints, circle, xCoords, texScale, LOD, forceUpdate);
#else
        return mt.updateSingle(root, comp, t, last_t, (dnc && dnc.isNightTime()), maxGrowth, growRate, diamLengthScale, numCircPoints, circle, xCoords, texScale, LOD, forceUpdate);
#endif
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
#if USE_MULTI_THREAD
        return gs.update(root,  comp, t, last_t, (dnc && dnc.isNightTime()), maxGrowth, growRate, diamLengthScale, texScale, LOD, forceUpdate);
#else
        return gs.updateSingle(root, comp, t, last_t, (dnc && dnc.isNightTime()), maxGrowth, growRate, diamLengthScale, texScale, LOD, forceUpdate);
#endif
    }
#endif

    void Awake()
    {

#if !USE_GEOM_SHADER
        mt = new SetupMeshMulti();
        Material mat = defMat;
#else
        gs =  new SetupMeshGeomShader();
        Material mat = geomMat;
#endif
        comp = (root_component[])transform.GetComponentsInChildren<root_component>();
        root = new List<MyTreeNode>[comp.Length];
        for (int i = 0; i < comp.Length; ++i)
        {
            (comp[i].GetComponent<MeshRenderer>()).material = mat;
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
    long frameCount = 0;
    bool once = true;
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

        if(!updateMesh(t, last_t) && once)
        {
            Debug.Log("Avg Frames: " + frameCount / Time.timeSinceLevelLoad);
            once = false;
        } else
        {
            frameCount++;
        }
    }
}
