using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

public class OctreeTest : MonoBehaviour
{
    public int maxHierarchyIndex = 2;
    private List<Octree> octrees = new List<Octree>();
    private float[,,] densities;
    private Octree rootOctree;
    public Transform bigboi;
    void Start()
    {

    }
    private void CreateOctree() 
    {
        rootOctree = new Octree();
        rootOctree.maxHierarchyIndex = maxHierarchyIndex;
        rootOctree.hierarchyIndex = 0;
        rootOctree.size = Mathf.RoundToInt(Mathf.Pow(2, maxHierarchyIndex));
        densities = new float[rootOctree.size, rootOctree.size, rootOctree.size];
        for (int x = 0; x < rootOctree.size; x++)
        {
            for (int y = 0; y < rootOctree.size; y++)
            {
                for (int z = 0; z < rootOctree.size; z++)
                {
                    densities[x, y, z] = Density(math.float3(x, y, z) + math.float3(bigboi.position));
                }
            }
        }
        List<Octree> subOctreesToCalculate = new List<Octree>();
        subOctreesToCalculate.Add(rootOctree);
        octrees.Clear();
        octrees.Add(rootOctree);
        for (int i = 0; i < subOctreesToCalculate.Count; i++)
        {
            Octree octree = subOctreesToCalculate[i];
            //subOctreesToCalculate.Remove(octree);
            octree.CreateChildren(bigboi.position, densities);

            subOctreesToCalculate.AddRange(octree.children);
            octrees.AddRange(octree.children);
        }
    }
    private void Update()
    {
        CreateOctree();
    }
    private void OnDrawGizmos()
    {
        foreach (var octree in octrees)
        {
            Gizmos.color = Color.Lerp(Color.black, Color.white, (float)octree.hierarchyIndex / (float)maxHierarchyIndex);
            Gizmos.DrawWireCube(math.float3(octree.position + (octree.size / 2)), new Vector3(octree.size, octree.size, octree.size));
        }
    }
    private float Density(float3 position)
    {
        return (Mathf.PerlinNoise(position.x * 0.0256f, position.z * 0.0256f) * 30) - (position.y);
    }
}
public struct Octree 
{
    public int maxHierarchyIndex;
    public int hierarchyIndex;
    public int3 position;
    public int size;
    public List<Octree> children;
    public void CreateChildren(float3 point, float[,,] densities) 
    {
        children = new List<Octree>();
        float min = float.MaxValue;
        float max = float.MinValue;
        for (int x2 = position.x; x2 < position.x + size; x2++)
        {
            for (int y2 = position.y; y2 < position.y + size; y2++)
            {
                for (int z2 = position.z; z2 < position.z + size; z2++)
                {
                    if (densities[x2, y2, z2] < min) min = densities[x2, y2, z2];
                    if (densities[x2, y2, z2] > max) max = densities[x2, y2, z2];
                }
            }
        }
        if (hierarchyIndex == maxHierarchyIndex || (min > 1 || max < -1)) return;
        int i = 0;
        for (int x = 0; x < 2; x++)
        {
            for (int y = 0; y < 2; y++)
            {
                for (int z = 0; z < 2; z++)
                {
                    //Octree child
                    Octree octreeChild = new Octree();

                    octreeChild.size = size / 2;
                    octreeChild.position = (octreeChild.size * new int3(x, y, z)) + position;
                    octreeChild.hierarchyIndex = hierarchyIndex + 1;
                    octreeChild.maxHierarchyIndex = maxHierarchyIndex;
                    children.Add(octreeChild);

                    i++;
                }
            }
        }
    }

    private bool PointIsInsideOctree(float3 point, Octree octree) 
    {
        return (point.x >= octree.position.x && point.x < octree.position.x + octree.size)
            && (point.y >= octree.position.y && point.y < octree.position.y + octree.size)
            && (point.z >= octree.position.z && point.z < octree.position.z + octree.size);
    }
}