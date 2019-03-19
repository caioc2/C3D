using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class root_component : MonoBehaviour {
    
    public int minEpoch = 0;
    public int maxEpoch = 50;
    public Vector3 startPos = new Vector3(0.0f, 0.0f, 0.0f);
    public Vector3 startDir = new Vector3(0.0f, 0.0f, 0.0f);

    /*[HideInInspector]
    public MeshFilter meshFilter = null;
    [HideInInspector]
    public MeshRenderer meshRenderer = null;
    [HideInInspector]
    public ParticleSystem ps = null;*/

    void Start()
    {
        /*meshFilter = gameObject.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
            Debug.Log("MeshFilter Added");
        }

        meshRenderer = gameObject.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            Debug.Log("MeshRenderer Added");
        }*/
        
        Mesh mesh = new Mesh();
        GetComponent<MeshFilter>().sharedMesh = mesh;
        mesh.name = "Root Of Shame";
        //ps = GetComponent<ParticleSystem>();
    }
}
