using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class GenerateRootSkeleton {

    private static Vector3 rotate(Vector3 v, Vector3 normal, float theta)
    {
        return Quaternion.AngleAxis(theta, normal) * v;
    }

    private static void growNode(List<MyTreeNode> root,
                                 int i,
                                 float maxLength,
                                 float levelLengthRatio,
                                 float meanGrowNoiseDir,
                                 float varGrowNoiseDir,
                                 float meanChildNoiseDir,
                                 float varChildNoiseDir,
                                 float nonPAngle,
                                 float meanStep,
                                 float varStep,
                                 float LeftRightRate,
                                 bool planar,
                                 float childRate,
                                 int maxLevel,
                                 int maxNodes,
                                 int minNodesBChild)
    {
        if (root[i].length[root[i].length.Count-1] < (maxLength * Math.Pow(levelLengthRatio, root[i].level)))
        {
            Vector3 dir = root[i].points[root[i].points.Count - 1].normalized;
            float rd1 = meanGrowNoiseDir + varGrowNoiseDir * MyRandom.nrand();
            float rd2 = nonPAngle * MyRandom.rand();
            float rs = meanStep + varStep * MyRandom.nrand();
            Vector3 rp = new Vector3(MyRandom.rand() - 0.5f, 0.0f, MyRandom.rand() - 0.5f);
            Vector3 normal = Vector3.Cross(Vector3.Cross(dir, rp), dir);
            Vector3 planeRotDir = planar ? rotate(dir, new Vector3(1.0f, 0.0f, 0.0f), rd1) : rotate(dir, normal, rd1);
            Vector3 finalDir = planar ? planeRotDir : rotate(planeRotDir, dir, rd2);
            finalDir *= rs * (float)Math.Pow((double)levelLengthRatio, (double)root[i].level);
            root[i].addPoint(finalDir);

            if (MyRandom.rand() < childRate && root[i].level < maxLevel && root.Count < maxNodes && root[i].points.Count > minNodesBChild)
            {
                MyTreeNode node = new MyTreeNode(root.Count,
                                                i, root[i].level + 1,
                                                root[i].epoch + root[i].points.Count,
                                                root[i].childrenStartIdx.Count,
                                                root[i].length[root[i].length.Count - 1]);
                rd1 = meanChildNoiseDir + varChildNoiseDir * MyRandom.nrand();
                rd2 = nonPAngle * MyRandom.rand();
                dir = finalDir.normalized * meanStep;
                rp = new Vector3(MyRandom.rand() - 0.5f, 0.0f, MyRandom.rand() - 0.5f);
                normal = planar ? new Vector3(1.0f, 0.0f, 0.0f) : Vector3.Cross(Vector3.Cross(dir, rp), dir);
                if (MyRandom.rand() > LeftRightRate)
                {
                    planeRotDir = rotate(dir, normal, rd1);
                }
                else
                {
                    planeRotDir = rotate(dir, normal, -rd1);
                }

                finalDir = planar ? planeRotDir : rotate(planeRotDir, dir, rd2);

                node.addPoint(finalDir);
                root[i].childrenStartIdx.Add(root[i].points.Count - 1);
                root[i].childrenId.Add(root.Count);
                node.parentChildId = root[i].childrenStartIdx.Count;
                root.Add(node);
            }
        }
    }

    public static List<MyTreeNode> generateSkeleton(root_component comp,
                                                    bool useGlobalEpoch,
                                                    int minEpoch,
                                                    int maxEpoch,
                                                    int maxIterations,
                                                    float maxLength,
                                                    float levelLengthRatio,
                                                    float meanGrowNoiseDir,
                                                    float varGrowNoiseDir,
                                                    float meanChildNoiseDir,
                                                    float varChildNoiseDir,
                                                    float nonPAngle,
                                                    float meanStep,
                                                    float varStep,
                                                    float LeftRightRate,
                                                    bool planar,
                                                    float childRate,
                                                    int maxLevel,
                                                    int maxNodes,
                                                    int minNodesBChild)
    {
        List<MyTreeNode> rt = new List<MyTreeNode>();
        MyTreeNode node = new MyTreeNode();
        node.startPos = comp.startPos;
        node.addPoint(comp.startDir * meanStep);
        if (useGlobalEpoch)
        {
            node.epoch = (int)(minEpoch + MyRandom.rand() * (float)(maxEpoch - minEpoch));
        }
        else
        {
            node.epoch = (int)(comp.minEpoch + MyRandom.rand() * (float)(comp.maxEpoch - minEpoch));
        }
        rt.Add(node);

        int it = 0;
        while (it < maxIterations)
        {
            int i = 0;
            while (i < rt.Count && it < maxIterations)
            {
                growNode(   rt,
                            i, 
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
                it++; i++;
            }
        }

        return rt;
    }

}
