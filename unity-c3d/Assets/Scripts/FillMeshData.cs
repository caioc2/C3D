using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class FillMeshData {

    private static Vector3[] FullTransform(Vector3[] v,
                                           Vector3 orientation,
                                           Vector3 normal, 
                                           Vector3 position, 
                                           float scale)
    {
        Vector3[] ret = new Vector3[v.Length];

        var rot = Quaternion.FromToRotation(orientation.normalized, normal.normalized);
        for (int i = 0; i < v.Length; ++i)
        {
            ret[i] = (rot * (v[i] * scale)) + position;
        }
        return ret;
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
        node.length = 0.0f;
        Vector3 pos = node.startPos;

        for (int j = 0; j <= i; ++j)
        {
            for (int k = lastIdx; k < node.childrenStartIdx[j]; ++k)
            {
                Vector3 p = node.points[k] * (1.0f + Math.Max(0.0f, Math.Min(maxEpochF - k, maxGrowth)) * growRate);
                node.length += p.magnitude;
                pos += p;
            }
            lastIdx = node.childrenStartIdx[j];
            root[node.childrenId[j]].startLen = node.length;
            root[node.childrenId[j]].startPos = pos;
        }

        for (int k = lastIdx; k < maxCount; ++k)
        {
            node.length += node.points[k].magnitude * (1.0f + Math.Max(0.0f, Math.Min(maxEpochF - k, maxGrowth)) * growRate);
        }

        if (maxCount < node.points.Count)
        {
            node.length += node.points[maxCount].magnitude * last;
        }
    }

    
    private static Vector3 orientation = new Vector3(0.0f, 0.0f, 1.0f);

    public static int fillVerticesTriangles(List<Vector3> vertices,
                                             List<int> triangles,
                                             List<Vector2> uv, 
                                             List<MyTreeNode> root,
                                             float epoch, 
                                             float maxGrowth, 
                                             float growRate, 
                                             float diamLengthScale, 
                                             int numCircPoints, 
                                             Vector3[] shape, 
                                             float[] coords,
                                             float texScale,
                                             float LOD)
    {


        int ii = 0;
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
            node.maxDiameter = Math.Min(parent.maxDiameter, (parent.length - node.startLen) * diamLengthScale);

            float curLen = node.length;

            int vsi = vertices.Count;
            int tsi = triangles.Count;

            Vector3 curPos = node.startPos;

            Vector3[] surfPoints = FullTransform(shape, orientation, node.points[0], curPos, node.maxDiameter);
            vertices.AddRange(surfPoints);
            for (int k = 0; k < numCircPoints; ++k)
                uv.Add(new Vector2(coords[k], 0.0f));

            int skipped = 0;
            float dlen = 0.0f;

            int lastChild = -1;
            for (int j = 0; j < maxCount; ++j)
            {
                Vector3 p = node.points[j] * (1.0f + (Math.Min(maxEpochF - j, maxGrowth) * growRate));
                curPos += p;
                curLen -= p.magnitude;

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
                    surfPoints = FullTransform(shape, orientation, node.points[j], curPos, maxDiam);
                    float circLen = (float)(2.0f * Math.PI * maxDiam);
                    vertices.AddRange(surfPoints);
                    for (int k = 0; k < numCircPoints; ++k)
                        uv.Add(new Vector2(coords[k], texScale * (node.length - curLen) / circLen));
                }
            }

            maxCount -= skipped;
            for (int j = 0; j < maxCount; ++j)
            {
                for (int k = 0; k < numCircPoints - 1; ++k)
                {
                    int[] ta = { vsi + (k    ) + (j    ) * numCircPoints,
                                 vsi + (k + 1) + (j    ) * numCircPoints,
                                 vsi + (k + 1) + (j + 1) * numCircPoints};
                    int[] tb = { vsi + (k    ) + (j    ) * numCircPoints,
                                 vsi + (k + 1) + (j + 1) * numCircPoints,
                                 vsi + (k    ) + (j + 1) * numCircPoints };
                    triangles.AddRange(ta);
                    triangles.AddRange(tb);
                }
                int[] tta = { vsi + (numCircPoints - 1) + (j    ) * numCircPoints,
                              vsi +                       (j    ) * numCircPoints,
                              vsi +                       (j + 1) * numCircPoints};
                int[] ttb = { vsi + (numCircPoints - 1) + (j    ) * numCircPoints,
                              vsi +                     + (j + 1) * numCircPoints,
                              vsi + (numCircPoints - 1) + (j + 1) * numCircPoints };
                triangles.AddRange(tta);
                triangles.AddRange(ttb);
            }

            if (maxCount < node.points.Count)
            {
                vertices.Add(curPos + node.points[maxCount] * last);
                uv.Add(new Vector2(0.5f, node.length));
                int lastPoint = vertices.Count - 1;
                for (int k = 0; k < numCircPoints - 1; ++k)
                {
                    int[] ta = { vsi + (k + 1) + maxCount * numCircPoints,
                                 vsi + (k    ) + maxCount * numCircPoints,
                                 lastPoint };
                    triangles.AddRange(ta);
                }
                int[] ttta = { vsi +                       maxCount * numCircPoints,
                               vsi + (numCircPoints - 1) + maxCount * numCircPoints,
                               lastPoint };
                triangles.AddRange(ttta);
            }
        }
        return ii;
    }

    public static int fillVerticesTrianglesGeomShader(Vector3[] vertices,
                                             int[] triangles,
                                             Vector2[] uv,
                                             List<MyTreeNode> root,
                                             float epoch,
                                             float maxGrowth,
                                             float growRate,
                                             float diamLengthScale,
                                             float texScale,
                                             float LOD)
    {
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
            node.maxDiameter = Math.Min(parent.maxDiameter, (parent.length - node.startLen) * diamLengthScale);

            float curLen = node.length;

            Vector3 curPos = node.startPos;

            //Vector3[] surfPoints = FullTransform(shape, orientation, node.points[0], curPos, node.maxDiameter);
            int startVsi = vsi;
            vertices[vsi].x = curPos.x; vertices[vsi].y = curPos.y; vertices[vsi].z = curPos.z;
            uv[vsi].x = node.maxDiameter; uv[vsi].y = 0.0f;
            vsi++;

            int skipped = 0;
            float dlen = 0.0f;

            int lastChild = -1;
            for (int j = 0; j < maxCount; ++j)
            {
                Vector3 p = node.points[j] * (1.0f + (Math.Min(maxEpochF - j, maxGrowth) * growRate));
                curPos += p;
                curLen -= p.magnitude;

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
                    //surfPoints = FullTransform(shape, orientation, node.points[j], curPos, maxDiam);
                    float circLen = (float)(2.0f * Math.PI * maxDiam);

                    //store current root point position to processed in the shader: point -> circle
                    //Debug.Log("Size: " + vertices.Length + " index: " + vsi + " maxCount: " + maxCount + " ii: " + ii + " rootCount: " + root.Count);
                    vertices[vsi].x = curPos.x; vertices[vsi].y = curPos.y; vertices[vsi].z = curPos.z;
                    //store current point diameter and vertical texture position
                    uv[vsi].x = maxDiam; uv[vsi].y = texScale * (node.length - curLen) / circLen;
                    vsi++;
                }
            }

            maxCount -= skipped;
            //Dummy triangles i, i+1, i+2
            for (int j = 0; j <= maxCount-2; ++j)
            {
                triangles[tsi] = startVsi + j;
                triangles[tsi+1] = startVsi + j + 1;
                triangles[tsi+2] = startVsi + j + 2;
                tsi += 3;
            }

            if (maxCount < node.points.Count)
            {
                curPos = curPos + node.points[maxCount] * last;
                vertices[vsi].x = curPos.x; vertices[vsi].y = curPos.y; vertices[vsi].z = curPos.z;
                uv[vsi].x = 0.0001f; uv[vsi].y = node.length;
                vsi++;
                if (maxCount >= 3){
                    int lastPoint1 = vsi- 1;

                    triangles[tsi] = lastPoint1 - 2;
                    triangles[tsi + 1] = lastPoint1 - 1;
                    triangles[tsi + 2] = lastPoint1;
                    tsi += 3;
                }
            }
            //Dummy vertex, in case there are not enough vertices to make a triangle
            curPos *= 1.001f;
            vertices[vsi].x = curPos.x; vertices[vsi].y = curPos.y; vertices[vsi].z = curPos.z;
            uv[vsi].x = 0.0001f; uv[vsi].y = node.length;
            vsi++;
            int lastPoint = vsi - 1;

            triangles[tsi] = lastPoint - 2;
            triangles[tsi + 1] = lastPoint - 1;
            triangles[tsi + 2] = lastPoint;
            tsi += 3;
        }
        return vsi;
    }
}
