using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class FillMeshData {

    private unsafe static void FullTransform(Vector3[] v, Vector3* outv, int idx,
                                           Vector3 orientation,
                                           Vector3 normal, 
                                           Vector3 position, 
                                           float scale)
    {
        var rot = Quaternion.FromToRotation(orientation.normalized, normal.normalized);
        for (int i = 0; i < v.Length; ++i)
        {
            outv[idx+i] = (rot * (v[i] * scale)) + position;
        }
    }

    private static void preProcessLength(List<MyTreeNode> root,
                                         int idx, 
                                         float epoch, 
                                         float maxGrowth, 
                                         float growRate)
    {
        MyTreeNode node = root[idx];
        node.maxDiameter = float.MaxValue;
        float maxEpochF = Math.Max(0.0f, epoch - node.epoch);
        int maxEpoch = (int)Math.Floor(maxEpochF);
        float last = maxEpochF - (float)maxEpoch;
        int maxCount = Math.Min(node.points.Count, maxEpoch);

        int i = -1;
        for (int k = node.childrenStartIdx.Count - 1; k >= 0; --k)
        {
            if (node.childrenStartIdx[k] <= maxCount)
            {
                i = k;
                break;
            }
        }

        int lastIdx = 0;
        //node.length = 0.0f;
        Vector3 pos = node.startPos;

        for (int j = 0; j <= i; ++j)
        {
            for (int k = lastIdx; k < node.childrenStartIdx[j]; ++k)
            {
                Vector3 p = node.points[k];// * (1.0f + Math.Max(0.0f, Math.Min(maxEpochF - k, maxGrowth)) * growRate);
                //node.length += p.magnitude;
                pos += p;
            }
            lastIdx = node.childrenStartIdx[j];
            //root[node.childrenId[j]].startLen = node.length;
            root[node.childrenId[j]].startPos = pos;
        }

        /*for (int k = lastIdx; k < maxCount; ++k)
        {
            node.length += node.points[k].magnitude * (1.0f + Math.Max(0.0f, Math.Min(maxEpochF - k, maxGrowth)) * growRate);
        }

        if (maxCount < node.points.Count)
        {
            node.length += node.points[maxCount].magnitude * last;
        }*/
    }

    
    private static Vector3 orientation = new Vector3(0.0f, 0.0f, 1.0f);

    public static void fillVerticesTriangles(Vector3[] vertices,
                                             int[] triangles,
                                             Vector2[] uv, 
                                             List<MyTreeNode> root,
                                             float epoch, 
                                             float maxGrowth, 
                                             float growRate, 
                                             float diamLengthScale,
                                             Vector3[] shape, 
                                             float[] coords,
                                             float texScale,
                                             float LOD,
                                             out int VCount,
                                             out int TCount)
    {
        //coords.length should be == to shape.length
        int numCircPoints = shape.Length;
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

                        int ii = 0;
                        int vsi = 0;
                        int tsi = 0;
                        for (; ii < root.Count; ii++)
                        {
                            int node_vsi = vsi;
                            MyTreeNode node = root[ii];

                            float maxEpochF = Math.Max(0.0f, epoch - node.epoch);
                            int maxEpoch = (int)Math.Floor(maxEpochF);
                            float last = maxEpochF - (float)maxEpoch;
                            int maxCount = Math.Min(node.points.Count, maxEpoch);

                            if (node.epoch > epoch)
                                break;// continue;

                            //assumes root is somewhat ordered, "node =  root[i]" and "parent = root[j]" -> j < i
                            preProcessLength(root, ii, epoch, maxGrowth, growRate);

                            MyTreeNode parent = root[node.parentId];
                            int pId1 = (int)Math.Min(Math.Max(0, epoch - parent.epoch), parent.points.Count-1);
                            int pId2 = (int)Math.Min(pId1+1, parent.points.Count-1);
                            float parentLen = (1.0f - last) * parent.length[pId1] + last * parent.length[pId2];
                            node.maxDiameter = Math.Min(parent.maxDiameter, (parentLen - node.startLen) * diamLengthScale);


                            pId1 = maxCount - 1;
                            pId2 = (int)Math.Min(pId1 + 1, node.points.Count - 1);
                            float nodeLen = (1.0f - last) * node.length[pId1] + last * node.length[pId2];
                            float curLen = nodeLen;

                            Vector3 curPos = node.startPos;

                            FullTransform(shape, _vertices, vsi, orientation, node.points[0], curPos, node.maxDiameter);
                            for (int k = 0; k < numCircPoints; ++k) { 
                                uv[vsi + k].x = coords[k]; uv[vsi + k].y = 0.0f;
                            }
                            vsi += numCircPoints;

                            int skipped = 0;
                            float dlen = 0.0f;

                            int lastChild = -1;
                            for (int j = 0; j < maxCount; ++j)
                            {
                                Vector3 p = node.points[j];// * (1.0f + (Math.Min(maxEpochF - j, maxGrowth) * growRate));
                                curPos += p;
                                curLen = nodeLen - node.length[j];

                                dlen += p.magnitude;

                                bool haveChild = false;
                                if (node.childrenStartIdx.Count > 0 && j <= node.childrenStartIdx[node.childrenStartIdx.Count - 1])
                                {
                                    for (int k = lastChild + 1; k < node.childrenStartIdx.Count; k++)
                                    {
                                        if (j == node.childrenStartIdx[k])
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

                                    float maxDiam = Math.Min(node.maxDiameter, diamLengthScale * curLen);
                                    FullTransform(shape, _vertices, vsi, orientation, node.points[j], curPos, maxDiam);
                                    float circLen = (float)(2.0f * Math.PI * maxDiam);
                                    for (int k = 0; k < numCircPoints; k++)
                                    {
                                        uv[vsi + k].x = coords[k]; uv[vsi + k].y = texScale * (nodeLen - curLen) / circLen;
                                    }
                                    vsi += numCircPoints;
                                }
                            }

                            maxCount -= skipped;
                            for (int j = 0; j < maxCount; ++j)
                            {
                                for (int k = 0; k < numCircPoints - 1; ++k)
                                {
                                    _triangles[tsi + k + 0] = node_vsi + (k) + (j) * numCircPoints;
                                    _triangles[tsi + k + 1] = node_vsi + (k + 1) + (j) * numCircPoints;
                                    _triangles[tsi + k + 2] = node_vsi + (k + 1) + (j + 1) * numCircPoints;
                                    _triangles[tsi + k + 3] = node_vsi + (k) + (j) * numCircPoints;
                                    _triangles[tsi + k + 4] = node_vsi + (k + 1) + (j + 1) * numCircPoints;
                                    _triangles[tsi + k + 5] = node_vsi + (k) + (j + 1) * numCircPoints;
                                    tsi += 6;
                                }

                                _triangles[tsi + 0] = node_vsi + (numCircPoints - 1) + (j) * numCircPoints;
                                _triangles[tsi + 1] = node_vsi + (j) * numCircPoints;
                                _triangles[tsi + 2] = node_vsi + (j + 1) * numCircPoints;
                                _triangles[tsi + 3] = node_vsi + (numCircPoints - 1) + (j) * numCircPoints;
                                _triangles[tsi + 4] = node_vsi + +(j + 1) * numCircPoints;
                                _triangles[tsi + 5] = node_vsi + (numCircPoints - 1) + (j + 1) * numCircPoints;
                                tsi += 6;
                            }

                            if (maxCount < node.points.Count)
                            {
                                _vertices[vsi]=curPos + node.points[maxCount] * last;
                                _uv[vsi].x = 0.5f; _uv[vsi].y = nodeLen;
                                int lastPoint = vsi;
                                vsi++;

                                for (int k = 0; k < numCircPoints - 1; ++k)
                                {
                                    _triangles[tsi + 0] = node_vsi + (k + 1) + maxCount * numCircPoints;
                                    _triangles[tsi + 1] = node_vsi + (k) + maxCount * numCircPoints;
                                    _triangles[tsi + 2] = lastPoint;
                                    tsi += 3;
                                }
                                _triangles[tsi + 0] = node_vsi + maxCount * numCircPoints;
                                _triangles[tsi + 1] = node_vsi + (numCircPoints - 1) + maxCount * numCircPoints;
                                _triangles[tsi + 2] = lastPoint;
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

    public static void fillVerticesTrianglesGeomShader(Vector3[] vertices,
                                             int[] triangles,
                                             Vector2[] uv,
                                             List<MyTreeNode> root,
                                             float epoch,
                                             float maxGrowth,
                                             float growRate,
                                             float diamLengthScale,
                                             float texScale,
                                             float LOD,
                                             out int VCount,
                                             out int TCount)
    {   unsafe
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

                        int ii = 0;
                        int vsi = 0;
                        int tsi = 0;
                        for (; ii < root.Count; ii++)
                        {
                            MyTreeNode node = root[ii];

                            float maxEpochF = Math.Max(0.0f, epoch - node.epoch);
                            int maxEpoch = (int)Math.Floor(maxEpochF);
                            float last = maxEpochF - (float)maxEpoch;
                            int maxCount = Math.Min(node.points.Count, maxEpoch);

                            if (node.epoch > epoch)
                                break;// continue;

                            //assumes root is somewhat ordered, "node =  root[i]" and "parent = root[j]" -> j < i
                            preProcessLength(root, ii, epoch, maxGrowth, growRate);

                            MyTreeNode parent = root[node.parentId];
                            int pId1 = (int)Math.Min(Math.Max(0, epoch - parent.epoch), parent.points.Count - 1);
                            int pId2 = (int)Math.Min(pId1 + 1, parent.points.Count - 1);
                            float parentLen = (1.0f - last) * parent.length[pId1] + last * parent.length[pId2];
                            node.maxDiameter = Math.Min(parent.maxDiameter, (parentLen - node.startLen) * diamLengthScale);


                            pId1 = maxCount - 1;
                            pId2 = (int)Math.Min(pId1 + 1, node.points.Count - 1);
                            float nodeLen = (1.0f - last) * node.length[pId1] + last * node.length[pId2];
                            float curLen = nodeLen;

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
                                Vector3 p = node.points[j] * (1.0f + (Math.Min(maxEpochF - j, maxGrowth) * growRate));
                                curPos += p;
                                curLen = nodeLen - node.length[j];

                                dlen += p.magnitude;

                                bool haveChild = false;
                                if (node.childrenStartIdx.Count > 0 && j <= node.childrenStartIdx[node.childrenStartIdx.Count - 1])
                                {
                                    for (int k = lastChild + 1; k < node.childrenStartIdx.Count; k++)
                                    {
                                        if (j == node.childrenStartIdx[k])
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

                                    float maxDiam = Math.Min(node.maxDiameter, diamLengthScale * curLen);
                                    float circLen = (float)(2.0f * Math.PI * maxDiam);

                                    //store current root point position to processed in the shader: point -> circle
                                    _vertices->x = curPos.x; _vertices->y = curPos.y; _vertices->z = curPos.z;
                                    //store current point diameter and vertical texture position
                                    _uv->x = maxDiam; _uv->y = texScale * (nodeLen - curLen) / circLen;
                                    _vertices += 1;
                                    _uv += 1;
                                    vsi++;
                                }
                            }

                            maxCount -= skipped;
                            //Dummy triangles i, i+1, i+2
                            for (int j = 0; j <= maxCount - 2; ++j)
                            {
                                *_triangles = startVsi + j;
                                _triangles += 1;
                                *_triangles = startVsi + j + 1;
                                _triangles += 1;
                                *_triangles = startVsi + j + 2;
                                _triangles += 1;
                                tsi += 3;
                            }

                            if (maxCount < node.points.Count)
                            {
                                curPos = curPos + node.points[maxCount] * last;
                                _vertices->x = curPos.x; _vertices->y = curPos.y; _vertices->z = curPos.z;
                                _uv->x = 0.0001f; _uv->y = nodeLen;
                                _vertices += 1;
                                _uv += 1;
                                vsi++;
                                if (maxCount >= 3)
                                {
                                    int lastPoint1 = vsi - 1;

                                    *_triangles = lastPoint1 - 2;
                                    _triangles += 1;
                                    *_triangles = lastPoint1 - 1;
                                    _triangles += 1;
                                    *_triangles = lastPoint1;
                                    _triangles += 1;
                                    tsi += 3;
                                }
                            }
                            //Dummy vertex, in case there are not enough vertices to make a triangle
                            curPos *= 1.001f;
                            _vertices->x = curPos.x; _vertices->y = curPos.y; _vertices->z = curPos.z;
                            _uv->x = 0.0001f; _uv->y = nodeLen;
                            _vertices += 1;
                            _uv += 1;
                            vsi++;
                            int lastPoint = vsi - 1;

                            *_triangles = lastPoint - 2;
                            _triangles += 1;
                            *_triangles = lastPoint - 1;
                            _triangles += 1;
                            *_triangles = lastPoint;
                            _triangles += 1;
                            tsi += 3;
                        }
                        VCount = vsi;
                        TCount = tsi;
                    }
                }
            }
        }
    }
}
