#define MULTI
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.Threading;

public class root_of_shame : MonoBehaviour {

    public Vector3 startPosition  = new Vector3(0.0f, 0.0f, 0.0f);
    public Vector3 startDirection  = new Vector3(0.0f, 1.0f, 0.0f);
    public float meanStep          = 0.3f;
    public float varStep           = 0.05f;
    public float maxLength         = 1000.0f;
    public float meanGrowNoiseDir  = 0.0f;
    public float varGrowNoiseDir   = 10.0f;
    public float meanChildNoiseDir = 40.0f;
    public float varChildNoiseDir  = 10.0f;
    public float childRate         = 0.8f;
    public float levelLengthRatio  = 0.6f;
    public float LeftRightRate     = 0.5f;
    public float diamLengthScale   = 0.02f;
    public float growRate          = 0.001f;
    public int maxGrowth           = 100;
    public int maxLevel            = 10;
    public int minNodesBChild      = 5;
    public int maxNodes            = 500;
    public int maxIterations       = 2000;
    public int numCircPoints       = 4;
    public bool planar             = true;
    public float nonPAngle         = 10.0f;
    public float speed             = 3.0f;
    public int minEpoch            = 0;
    public int maxEpoch            = 50;
    public bool useGlobalEpoch     = false;
    public float texScale          = 0.3f;
    public float LOD               = 0.1f;
    
    private DayNightCycle dnc;

    private Vector3[] circle;
    private float[] xCoords;

    private List<MyTreeNode>[] root;
    private root_component[] comp;
    
    float t = 0.0f;

#if MULTI
    SetupMeshMulti mt;

    void allocVecs()
    {
        mt.allocVecs(comp.Length);
    }

    void setCapacity()
    {
        mt.setCapacity(root, numCircPoints);
    }

    void updateMesh(float t, float last_t)
    {
        mt.update(root, comp, t, last_t, (dnc && dnc.isNightTime()), maxGrowth, growRate, diamLengthScale, numCircPoints, circle, xCoords, texScale, LOD);
    }
#else
    SetUpMeshSingle st;
    void allocVecs()
    {
        st.allocVecs();
    }

    void setCapacity()
    {
        st.setCapacity();
    }

    void updateMesh()
    {
        st.update();
    }
#endif
   
    void Awake()
    {
#if MULTI
        mt = new SetupMeshMulti();
#else
        st =  new SetUpMeshSingle();
#endif
        comp = (root_component[])transform.GetComponentsInChildren<root_component>();
        root = new List<MyTreeNode>[comp.Length];
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
}
