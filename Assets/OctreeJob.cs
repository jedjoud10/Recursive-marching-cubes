using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public struct OctreeJob : IJob
{
    public float isoThreshold;
    public float isoObstacleThreshold;
    public int maxHierarchyIndex;
    [ReadOnly]
    public NativeArray<float> densities;
    public NativeList<Octree> finalOctrees;
    public NativeList<Octree> totalOctrees;
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
        totalOctrees.Add(rootOctree);
        int maxSize = Mathf.RoundToInt(Mathf.Pow(2, maxHierarchyIndex)) + 3;
        int3 gridSize = new int3(maxSize, maxSize, maxSize);
        for (int i = 0; i < subOctreesToCalculate.Length; i++)
        {
            Octree octree = subOctreesToCalculate[i];
            //Create octree children 


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

                        //Check if iso-threshold value is in range in the current "octree" chunk
                        float minValue = float.MaxValue;
                        float maxValue = float.MinValue;
                        for (int x2 = octreeChild.position.x; x2 < octreeChild.position.x + octreeChild.size + 1; x2++)
                        {
                            for (int y2 = octreeChild.position.y; y2 < octreeChild.position.y + octreeChild.size + 1; y2++)
                            {
                                for (int z2 = octreeChild.position.z; z2 < octreeChild.position.z + octreeChild.size + 1; z2++)
                                {
                                    float density = densities[FlattenIndex(new int3(x2, y2, z2), gridSize)];
                                    if (density < minValue) minValue = density;
                                    if (density > maxValue) maxValue = density;
                                }
                            }
                        }
                        octreeChild.isObstacle = minValue < isoObstacleThreshold && maxValue < isoObstacleThreshold;
                        totalOctrees.Add(octreeChild);
                        //Octree child is at the maximum hierarchy index
                        if (octreeChild.hierarchyIndex == maxHierarchyIndex)
                        {
                            //Only add the leaf if it is in range
                            if (minValue < isoThreshold && maxValue > isoThreshold)
                            {
                                finalOctrees.Add(octreeChild);
                                continue;
                            }
                        }

                        //Check if the octree child is in range
                        if (minValue < isoThreshold && maxValue > isoThreshold)
                        {
                            //Add octreeChild for next iteration since it is in range
                            subOctreesToCalculate.Add(octreeChild);
                        }
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
    public bool isObstacle;
    public int3 position;
    public int size;
}