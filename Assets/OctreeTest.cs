using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class OctreeTest : MonoBehaviour
{
    public int maxHierarchyIndex = 2;
    private NativeList<Octree> octrees;
    private NativeArray<float> densities;
    public float3 offset;
    void Start()
    {

    }
    private void CreateOctree() 
    {
        octrees = new NativeList<Octree>(Allocator.TempJob);
        int maxSize = Mathf.RoundToInt(Mathf.Pow(2, maxHierarchyIndex));
        densities = new NativeArray<float>(maxSize * maxSize * maxSize, Allocator.TempJob);
        Unity.Mathematics.Random randomizer = new Unity.Mathematics.Random(54843);
        for (int i = 0; i < maxSize * maxSize * maxSize; i++)
        {
            int3 position = UnflattenIndex(i, new int3(maxSize, maxSize, maxSize));
            densities[i] = Density(new float3(position) * 0.2f + offset);
        }
        OctreeJob job = new OctreeJob
        {
            maxHierarchyIndex = maxHierarchyIndex,
            densities = densities,
            finalOctrees = octrees            
        };
        job.Run();
        foreach (var octree in octrees)
        {
            Gizmos.color = Color.Lerp(Color.black, Color.white, (float)octree.hierarchyIndex / (float)maxHierarchyIndex);
            Gizmos.DrawWireCube(math.float3(octree.position + (octree.size / 2)), new Vector3(octree.size, octree.size, octree.size));
        }
        octrees.Dispose();
        densities.Dispose();
    }
    private void OnDrawGizmos()
    {
        CreateOctree();
    }
    private bool PointIsInsideOctree(float3 point, Octree octree)
    {
        return (point.x >= octree.position.x && point.x < octree.position.x + octree.size)
            && (point.y >= octree.position.y && point.y < octree.position.y + octree.size)
            && (point.z >= octree.position.z && point.z < octree.position.z + octree.size);
    }
    private float Density(float3 position)
    {
        return (Mathf.PerlinNoise(position.x * 0.0256f, position.z * 0.0256f) * 30) - (position.y);
    }
    private static int3 UnflattenIndex(int index, int3 gridSize)
    {
        int z = index / (gridSize.x * gridSize.y);
        index -= (z * gridSize.x * gridSize.y);
        int y = index / gridSize.x;
        int x = index % gridSize.x;
        return new int3(x, y, z);
    }
}
[BurstCompile]
public struct OctreeJob : IJob
{
    public int maxHierarchyIndex;
    public NativeArray<float> densities;
    public NativeList<Octree> finalOctrees;
    //Array magic
    private static int FlattenIndex(int3 position, int3 gridSize)
    {
        return (position.z * gridSize.x * gridSize.y) + (position.y * gridSize.x) + position.x;
    }
    public void Execute()
    {
        //Create native containers
        NativeList<Octree> subOctreesToCalculate = new NativeList<Octree>(Allocator.Temp);

        //Setup root octree
        Octree rootOctree = new Octree();
        rootOctree.hierarchyIndex = 0;
        rootOctree.size = Mathf.RoundToInt(Mathf.Pow(2, maxHierarchyIndex));
        //Main iterations
        subOctreesToCalculate.Add(rootOctree);
        int maxSize = Mathf.RoundToInt(Mathf.Pow(2, maxHierarchyIndex));
        for (int i = 0; i < subOctreesToCalculate.Length; i++)
        {
            Octree octree = subOctreesToCalculate[i];
            //Create octree children 
            //Check if iso-threshold value is in range in the current "octree" chunk
            float minValue = float.MaxValue;
            float maxValue = float.MinValue;
            for (int x2 = octree.position.x; x2 < octree.position.x + octree.size; x2++)
            {
                for (int y2 = octree.position.y; y2 < octree.position.y + octree.size; y2++)
                {
                    for (int z2 = octree.position.z; z2 < octree.position.z + octree.size; z2++)
                    {
                        float density = densities[FlattenIndex(new int3(x2, y2, z2), new int3(maxSize, maxSize, maxSize))];
                        if (density < minValue) minValue = density;
                        if (density > maxValue) maxValue = density;
                    }
                }
            }

            if (octree.hierarchyIndex == maxHierarchyIndex || (minValue > 0.4f || maxValue < -0.4f))
            {
                //Octree is at max hierarchy level or iso-threshold value is not inrange
                continue;
            }
            //Create children
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int z = 0; z < 2; z++)
                    {
                        //Setup Octree child
                        Octree octreeChild = new Octree();
                        octreeChild.size = octree.size / 2;
                        octreeChild.position = (octreeChild.size * new int3(x, y, z)) + octree.position;
                        octreeChild.hierarchyIndex = octree.hierarchyIndex + 1;

                        //Add octree child for next iteration
                        subOctreesToCalculate.Add(octreeChild);
                        if(octreeChild.hierarchyIndex == maxHierarchyIndex) finalOctrees.Add(octreeChild);
                    }
                }
            }
        }
        subOctreesToCalculate.Dispose();
    }
}
public struct Octree 
{
    public int hierarchyIndex;
    public int3 position;
    public int size;    
}