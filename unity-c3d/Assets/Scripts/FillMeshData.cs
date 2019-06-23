using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

public class FillMeshData {
    
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private unsafe static void transformAndFill(Vector3 * v, int len, Vector3 * outv, float * coords, float height, Vector2 * uv, ref int idx,
                                           Vector3 orientation,
                                           Vector3 normal, 
                                           Vector3 position, 
                                           float scale)
    {
        var rot = Quaternion.FromToRotation(orientation, normal);
        for (int i = 0; i < len; ++i, ++idx)
        {
            outv[idx] = (rot * (v[i] * scale)) + position;
            uv[idx] = new Vector2(coords[i], height);
        }
    }
    
    private static Vector3 orientation = new Vector3(0.0f, 0.0f, 1.0f);

    public static unsafe void fillVerticesTriangles(Vector3[] vertices,
                                             int[] triangles,
                                             Vector2[] uv,
                                             List<MyTreeNode> root,
                                             float epoch,
                                             float diamLengthScale,
                                             Vector3[] shape,
                                             float[] coords,
                                             float texScale,
                                             float LOD,
                                             out int VCount,
                                             out int TCount)
    {

        unsafe
        {

            fixed (Vector3* __vertices = &vertices[0])
            {
                Vector3* _vertices = __vertices;
                fixed (Vector2* __uv = &uv[0])
                {
                    Vector2* _uv = __uv;
                    fixed (int* __triangles = &triangles[0])
                    {
                        int* _triangles = __triangles;
                        fixed (Vector3* __shape = &shape[0])
                        {
                            Vector3* _shape = __shape;

                            fixed (float* __coords = &coords[0])
                            {
                                float* _coords = __coords;

                                int ii = 0;
                                int vsi = 0;
                                int tsi = 0;
                                int npts = shape.Length;
                                for (; ii < root.Count; ii++)
                                {
                                    MyTreeNode node = root[ii];

                                    float maxEpochF = maxf(0.0f, epoch - node.epoch);
                                    int maxEpoch = (int)(maxEpochF);
                                    float last = maxEpochF - (float)maxEpoch;
                                    int maxCount = mini(node.points.Count, maxEpoch);

                                    if (node.epoch > epoch)
                                        continue;


                                    MyTreeNode parent = root[node.parentId];
                                    int pId1 = mini(maxi(0, (int)epoch - parent.epoch), parent.points.Count - 1);
                                    int pId2 = mini(pId1 + 1, parent.points.Count - 1);
                                    float parentLen = (1.0f - last) * parent.length[pId1] + last * parent.length[pId2];
                                    float parentMaxDiam = node.parentId == ii ? float.MaxValue : parent.maxDiameter;
                                    node.maxDiameter = minf(parentMaxDiam, (parentLen - node.startLen) * diamLengthScale);


                                    pId1 = maxi(0, maxCount - 1);
                                    pId2 = maxi(0, mini(pId1 + 1, node.points.Count - 1));
                                    float nodeLen = (1.0f - last) * node.length[pId1] + last * node.length[pId2];
                                    float curLen = nodeLen;
                                    float lastLen = 0;

                                    Vector3 curPos = node.startPos;

                                    int startVsi = vsi;

                                    transformAndFill(_shape, npts, _vertices, _coords, 0.0f, _uv, ref vsi, orientation, node.points[0], curPos, node.maxDiameter);

                                    int skipped = 0;
                                    float dlen = 0.0f;

                                    int lastChild = -1;
                                    for (int j = 0; j < maxCount; ++j)
                                    {
                                        float nodeLenJ = node.length[j];
                                        Vector3 pos = node.points[j];
                                        curPos.x += pos.x; curPos.y += pos.y; curPos.z += pos.z;
                                        curLen = nodeLen - nodeLenJ;

                                        dlen = nodeLenJ - lastLen;

                                        bool haveChild = false;
                                        if (node.childrenStartIdx.Count > 0 && j <= node.childrenStartIdx[node.childrenStartIdx.Count - 1])
                                        {
                                            for (int k = lastChild + 1; k < node.childrenStartIdx.Count; k++)
                                            {
                                                if (j == node.childrenStartIdx[k] - 1)
                                                {
                                                    lastChild = k;
                                                    haveChild = true;
                                                }
                                            }
                                        }

                                        if (dlen < LOD && j != maxCount - 1 && !haveChild)
                                        {
                                            skipped += 1;
                                            continue;
                                        }
                                        else
                                        {
                                            dlen = 0.0f;

                                            float maxDiam = minf(node.maxDiameter, diamLengthScale * curLen);
                                            float height = texScale * (nodeLenJ - curLen);
                                            transformAndFill(_shape, npts, _vertices, _coords, height, _uv, ref vsi, orientation, node.points[j], curPos, maxDiam);
                                        }
                                        lastLen = node.length[j];
                                    }

                                    maxCount -= skipped;
                                    for (int j = 0; j < maxCount; ++j, tsi += 6)
                                    {
                                        for (int k = 0; k < npts - 1; ++k, tsi += 6)
                                        {
                                            triangles[tsi + 0] = (int)(startVsi + (k) + (j) * npts);
                                            triangles[tsi + 1] = (int)(startVsi + (k + 1) + (j) * npts);
                                            triangles[tsi + 2] = (int)(startVsi + (k + 1) + (j + 1) * npts);
                                            triangles[tsi + 3] = (int)(startVsi + (k) + (j) * npts);
                                            triangles[tsi + 4] = (int)(startVsi + (k + 1) + (j + 1) * npts);
                                            triangles[tsi + 5] = (int)(startVsi + (k) + (j + 1) * npts);
                                        }
                                        triangles[tsi + 0] = (int)(startVsi + (npts - 1) + (j) * npts);
                                        triangles[tsi + 1] = (int)(startVsi + (j) * npts);
                                        triangles[tsi + 2] = (int)(startVsi + (j + 1) * npts);
                                        triangles[tsi + 3] = (int)(startVsi + (npts - 1) + (j) * npts);
                                        triangles[tsi + 4] = (int)(startVsi + (j + 1) * npts);
                                        triangles[tsi + 5] = (int)(startVsi + (npts - 1) + (j + 1) * npts);
                                    }

                                    if (maxCount < node.points.Count)
                                    {
                                        vertices[vsi] = (curPos + node.points[maxCount] * last);
                                        uv[vsi] = new Vector2(0.5f, nodeLen);
                                        vsi++;
                                        int lastPoint = vsi - 1;
                                        for (int k = 0; k < npts - 1; ++k, tsi += 3)
                                        {
                                            triangles[tsi + 0] = (int)(startVsi + (k + 1) + maxCount * npts);
                                            triangles[tsi + 1] = (int)(startVsi + (k) + maxCount * npts);
                                            triangles[tsi + 2] = (int)(lastPoint);
                                        }
                                        triangles[tsi + 0] = (int)(startVsi + maxCount * npts);
                                        triangles[tsi + 1] = (int)(startVsi + (npts - 1) + maxCount * npts);
                                        triangles[tsi + 2] = (int)(lastPoint);
                                        tsi += 3;
                                    }
                                }
                                VCount = vsi;
                                TCount = tsi;
                            }
                        }
                    }
                }
            }
        }
    }

    private static int mini(int a, int b) => ((a > b) ? b : a);

    private static float minf(float a, float b) => ((a > b) ? b : a);

    private static int maxi(int a, int b) => ((a < b) ? b : a);

    private static float maxf(float a, float b) => ((a < b) ? b : a);

    public static void fillVerticesTrianglesGeomShader(NativeArray<float3> vertices,
                                             NativeArray<int> triangles,
                                             NativeArray<float2> uv,
                                             List<MyTreeNode> root,
                                             float epoch,
                                             float diamLengthScale,
                                             float texScale,
                                             float LOD,
                                             out int VCount,
                                             out int TCount)
    {   unsafe
        {

            float3* _vertices = (float3*)NativeArrayUnsafeUtility.GetUnsafePtr(vertices);
            {
                float2* _uv = (float2*)NativeArrayUnsafeUtility.GetUnsafePtr(uv);
                {
                    int* _triangles = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(triangles);
                    {
                        int ii = 0;
                        int vsi = 0;
                        int tsi = 0;
                        for (; ii < root.Count; ii++)
                        {
                            MyTreeNode node = root[ii];

                            float maxEpochF = maxf(0.0f, epoch - node.epoch);
                            int maxEpoch = (int)(maxEpochF);
                            float last = maxEpochF - (float)maxEpoch;
                            int maxCount = mini(node.points.Count, maxEpoch);

                            if (node.epoch > epoch+1)
                                continue;

                            MyTreeNode parent = root[node.parentId];
                            int pId1 = mini(maxi(0, (int)epoch - parent.epoch), parent.points.Count - 1);
                            int pId2 = mini(pId1 + 1, parent.points.Count - 1);
                            float parentLen = (1.0f - last) * parent.length[pId1] + last * parent.length[pId2];
                            float parentMaxDiam = node.parentId == ii ? float.MaxValue : parent.maxDiameter;
                            node.maxDiameter = minf(parentMaxDiam, (parentLen - node.startLen) * diamLengthScale);


                            pId1 = maxi(0, maxCount - 1);
                            pId2 = maxi(0, mini(pId1 + 1, node.points.Count - 1));
                            float nodeLen = (1.0f - last) * node.length[pId1] + last * node.length[pId2];
                            float curLen = nodeLen;
                            float lastLen = 0;

                            Vector3 curPos = node.startPos;

                            int startVsi = vsi;
                            _vertices->x = curPos.x; _vertices->y = curPos.y; _vertices->z = curPos.z;
                            _uv->x = node.maxDiameter; _uv->y = 0.0f;
                            _vertices+=1;
                            _uv += 1;
                            vsi++;

                            int skipped = 0;
                            float dlen = 0.0f;

                            int lastChild = -1;
                            for (int j = 0; j < maxCount; ++j)
                            {
                                float nodeLenJ = node.length[j];
                                Vector3 pos = node.points[j];
                                curPos.x += pos.x; curPos.y += pos.y; curPos.z += pos.z;
                                curLen = nodeLen - nodeLenJ;

                                dlen = nodeLenJ - lastLen;

                                bool haveChild = false;
                                if (node.childrenStartIdx.Count > 0 && j <= node.childrenStartIdx[node.childrenStartIdx.Count - 1])
                                {
                                    for (int k = lastChild + 1; k < node.childrenStartIdx.Count; k++)
                                    {
                                        if (j == node.childrenStartIdx[k]-1)
                                        {
                                            lastChild = k;
                                            haveChild = true;
                                        }
                                    }
                                }

                                if (dlen < LOD && j != maxCount - 1 && !haveChild)
                                {
                                    skipped += 1;
                                    continue;
                                }
                                else
                                {
                                    dlen = 0.0f;

                                    float maxDiam = minf(node.maxDiameter, diamLengthScale * curLen);
                                    float circLen = (2.0f * Mathf.PI * maxDiam);

                                    //store current root point position to processed in the shader: point -> circle
                                    _vertices->x = curPos.x; _vertices->y = curPos.y; _vertices->z = curPos.z;
                                    //store current point diameter and vertical texture position
                                    _uv->x = maxDiam; _uv->y = texScale * nodeLenJ;// / circLen;
                                    _vertices += 1;
                                    _uv += 1;
                                    vsi++;
                                }
                                lastLen = node.length[j];
                            }

                            maxCount -= skipped;
                            //Dummy triangles i, i+1, i+2
                            for (int j = 0; j <= maxCount - 2; ++j, tsi++)
                            {
                                *_triangles = startVsi + j;
                                _triangles += 1;
                            }

                            int lastIdx = maxCount - 1;
                            if (maxCount < node.points.Count)
                            {
                                Vector3 pos = node.points[maxCount];
                                curPos.x += pos.x * last; curPos.y += pos.y * last; curPos.z += pos.z * last;
                                _vertices->x = curPos.x;
                                _vertices->y = curPos.y;
                                _vertices->z = curPos.z;
                                _uv->x = 0.0001f; _uv->y = nodeLen;
                                _vertices += 1;
                                _uv += 1;
                                vsi++;

                                int lastPoint1 = vsi - 1;
                                *_triangles = lastPoint1 - 1;
                                _triangles += 1;
                                tsi++;
                                lastIdx = maxCount;
                            }
                            //Dummy vertex, in case there are not enough vertices to make a triangle
                            //curPos += node.points[lastIdx];
                            _vertices->x = curPos.x; _vertices->y = curPos.y; _vertices->z = curPos.z;
                            _uv->x = 0.0001f; _uv->y = nodeLen;
                            _vertices += 1;
                            _uv += 1;
                            vsi++;
                            //int lastPoint = vsi - 1;

                            //*_triangles = lastPoint - 4;
                            //tsi++;
                        }
                        VCount = vsi;
                        TCount = tsi;
                    }
                }
            }
        }
    }
}
