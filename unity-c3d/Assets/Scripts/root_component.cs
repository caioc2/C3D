using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class root_component : MonoBehaviour {
    
    public int minEpoch = 0;
    public int maxEpoch = 50;
    public Vector3 startPos = new Vector3(0.0f, 0.0f, 0.0f);
    public Vector3 startDir = new Vector3(0.0f, 0.0f, 0.0f);
    public Mesh mesh;
    public ParticleSystem ps;

    void Awake()
    {
        mesh = new Mesh();
        GetComponent<MeshFilter>().sharedMesh = mesh;
        mesh.name = "Root Of Shame";
        ps = GetComponent<ParticleSystem>();
    }
}
