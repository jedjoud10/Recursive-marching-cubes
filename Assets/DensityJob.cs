using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
[BurstCompile]
public struct DensityJob : IJobParallelFor 
{
    [WriteOnly]
    public NativeArray<float> densities;
    public TerrainData data;
    public int maxSize;
    public void Execute(int index) 
    {
        //Unflatten the index so we can use the density function
        int3 position = UnflattenIndex(index, new int3(maxSize, maxSize, maxSize));
        densities[index] = Density(position, data);
    }
    //Get the density at a specific point in space
    private float Density(float3 point, TerrainData data)
    {
        float density = 0;
        point *= data.scale;
        point += data.offset;
        density = point.y;
        density += math.abs(noise.cnoise(point * data.noise1Scale));
        return density;
    }
    //Turn an index into a 3d position
    private static int3 UnflattenIndex(int index, int3 gridSize)
    {
        int z = index / (gridSize.x * gridSize.y);
        index -= (z * gridSize.x * gridSize.y);
        int y = index / gridSize.x;
        int x = index % gridSize.x;
        return new int3(x, y, z);
    }
}