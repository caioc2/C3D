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
    public Vector3 pos;
    public Quaternion rot;
    public Material mat;
    public Matrix4x4 obj2World;

    void Awake()
    {
        mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = mesh;
        mesh.name = "Root Of Shame";
        ps = GetComponent<ParticleSystem>();
        pos = transform.position;
        rot = transform.rotation;
        obj2World = transform.localToWorldMatrix;
    }
}
