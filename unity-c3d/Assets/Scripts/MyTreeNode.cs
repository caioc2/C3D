using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MyTreeNode
{

    public int id;                      //index in the node array
    public int parentId;                //parent index in the node array
    public int parentChildId;           //index in the parent child array
    public int level;                   //hierarchy level
    public int epoch;                   //epoch this branch started
    public float length;                //total length of current drawn branch
    public float maxDiameter;           //max diameter of current drawn branch
    public List<int> childrenId;        //index of children in the node array
    public List<int> childrenStartIdx;  //index of "points" where each child starts
    public float startLen;              //length of where each child starts
    public List<Vector3> points;        //points of the branch
    public Vector3 startPos;

    public MyTreeNode(int _id, int _parentId, int _level, int _epoch, int _parentChildId)
    {
        id = _id;
        parentId = _parentId;
        level = _level;
        epoch = _epoch;
        parentChildId = _parentChildId;
        length = 0.0f;
        startLen = 0.0f;
        maxDiameter = float.MaxValue;
        childrenId = new List<int>();
        childrenStartIdx = new List<int>();
        points = new List<Vector3>();
    }

    public MyTreeNode()
    {
        id = 0;
        parentId = 0;
        level = 0;
        epoch = 0;
        parentChildId = -1;
        length = 0.0f;
        startLen = 0.0f;
        maxDiameter = float.MaxValue;
        childrenId = new List<int>();
        childrenStartIdx = new List<int>();
        points = new List<Vector3>();
    }

    public void addPoint(Vector3 p)
    {
        points.Add(p);
    }
};
