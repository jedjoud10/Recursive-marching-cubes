using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class Octree : MonoBehaviour
{
    public int maxHierarchyIndex = 2;
    private List<OctreeCube> octrees = new List<OctreeCube>();
    void Start()
    {
        OctreeCube rootOctree = new OctreeCube();
        rootOctree.maxHierarchyIndex = maxHierarchyIndex;
        rootOctree.hierarchyIndex = 0;
        rootOctree.size = new float3(10, 10, 10);        
        List<OctreeCube> subOctreesToCalculate = new List<OctreeCube>();
        subOctreesToCalculate.Add(rootOctree);
        octrees.Add(rootOctree);
        for (int i = 0; i < subOctreesToCalculate.Count; i++)
        {
            OctreeCube octree = subOctreesToCalculate[i];
            //subOctreesToCalculate.Remove(octree);
            octree.CreateChildren();

            subOctreesToCalculate.AddRange(octree.children);
            octrees.AddRange(octree.children);
        }
    }
    private void OnDrawGizmos()
    {
        foreach (var octree in octrees)
        {
            Gizmos.color = Color.Lerp(Color.black, Color.white, (float)octree.hierarchyIndex / (float)maxHierarchyIndex);
            Gizmos.DrawWireCube(octree.position + (octree.size / 2f), octree.size);
        }
    }
}
public struct OctreeCube 
{
    public int maxHierarchyIndex;
    public int hierarchyIndex;
    public float3 position;
    public float3 size;
    public List<OctreeCube> children;
    public void CreateChildren() 
    {
        if (hierarchyIndex == maxHierarchyIndex) 
        {
            children = new List<OctreeCube>();
            return;
        }
        children = new List<OctreeCube>();
        int i = 0;
        for (int x = 0; x < 2; x++)
        {
            for (int y = 0; y < 2; y++)
            {
                for (int z = 0; z < 2; z++)
                {
                    //Octree child
                    OctreeCube octreeCube = new OctreeCube();

                    octreeCube.size = size / 2f;
                    octreeCube.position = (octreeCube.size * new float3(x, y, z)) + position;
                    octreeCube.hierarchyIndex = hierarchyIndex + 1;
                    octreeCube.maxHierarchyIndex = maxHierarchyIndex;

                    if (UnityEngine.Random.Range(0, 2) == 0) children.Add(octreeCube);
                    i++;
                }
            }
        }
    }
}