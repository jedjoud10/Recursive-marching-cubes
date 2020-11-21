using System;
using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OctreeTest : MonoBehaviour
{
    [Range(0, 7)]
    public int maxHierarchyIndex = 2;
    public TerrainData data;
    public float isoThreshold;
    public float isoObstacleThreshold;
    public bool drawGizmos;
    public int octreeIndexTest;

    void Start()
    {

    }
    private void CreateOctree() 
    {
        int maxSize = Mathf.RoundToInt(Mathf.Pow(2, maxHierarchyIndex)) + 3;

        //Make native containers
        NativeList<Octree> octrees = new NativeList<Octree>(Allocator.TempJob);
        NativeList<Octree> totalOctrees = new NativeList<Octree>(Allocator.TempJob);
        NativeArray<float> densities = new NativeArray<float>(maxSize * maxSize * maxSize, Allocator.TempJob);
        //Mesh native containers
        NativeList<Vector3> vertices = new NativeList<Vector3>(Allocator.TempJob);
        NativeList<int> triangles = new NativeList<int>(Allocator.TempJob);
        NativeList<Color> vertexColors = new NativeList<Color>(Allocator.TempJob);
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        //Create the jobs and execute them
        DensityJob densityJob = new DensityJob
        {
            densities = densities,
            maxSize = maxSize,
            data = data
        };
        densityJob.Run(maxSize * maxSize * maxSize);
        OctreeJob octreeJob = new OctreeJob
        {
            maxHierarchyIndex = maxHierarchyIndex,
            isoThreshold = isoThreshold,
            isoObstacleThreshold = isoObstacleThreshold,
            voxels = densities,
            finalOctrees = octrees,
            totalOctrees = totalOctrees
        };
        octreeJob.Run();
        MarchingCubeJob mcJob = new MarchingCubeJob
        {
            octrees = octrees,
            vertices = vertices,
            vertexColors = vertexColors,
            triangles = triangles,
            densities = densities,
            isoThreshold = isoThreshold,
            gridSize = new int3(maxSize, maxSize, maxSize)
        };
        mcJob.Run();
        //Gizmo draw
        if (drawGizmos)
        {
            foreach (var octree in totalOctrees)
            {
                Gizmos.color = Color.Lerp(Color.black, Color.white, (float)octree.hierarchyIndex / (float)maxHierarchyIndex);
                Gizmos.DrawWireCube(math.float3(new float3(octree.position) + ((float)octree.size / 2f)), new Vector3(octree.size, octree.size, octree.size));
            }
            
            Octree currentOctree = totalOctrees[octreeIndexTest];
            List<int> neighbours = FindNeighbours(totalOctrees, octreeIndexTest);

            foreach (var item in neighbours)
            {
                Octree octreeNeighbour = totalOctrees[item];
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(octreeNeighbour.center, new Vector3(octreeNeighbour.size, octreeNeighbour.size, octreeNeighbour.size));
            }            
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(currentOctree.center, new Vector3(currentOctree.size, currentOctree.size, currentOctree.size));
        }

        Mesh mesh = new Mesh() { vertices = vertices.ToArray(), colors = vertexColors.ToArray(), triangles = triangles.ToArray() };
        mesh.Optimize();
        mesh.RecalculateNormals();
        GetComponent<MeshFilter>().mesh = mesh;
        GetComponent<MeshCollider>().sharedMesh = mesh;

        //Release the native containers from the memory
        vertices.Dispose();
        triangles.Dispose();
        octrees.Dispose();
        densities.Dispose();

        UnityEngine.Debug.Log("Final: " + stopwatch.ElapsedMilliseconds);
        stopwatch.Stop();
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

    //Find the neighbours
    private List<int> FindNeighbours(NativeList<Octree> octrees, int currentOctreeIndex) 
    {
        Octree rootOctree = octrees[currentOctreeIndex];
        Octree currentOctree;
        List<int> subOctreesToSearch = new List<int>();
        List<int> finalList = new List<int>();
        subOctreesToSearch.Add(0);
        //Recursively find the correct children
        for (int i = 0; i < subOctreesToSearch.Count; i++)
        {
            currentOctree = octrees[subOctreesToSearch[i]];
            if (currentOctree.childIndexStart != -1 && currentOctree.index != rootOctree.index)
            {
                //Search through the children
                for (int k = 0; k < 8; k++)
                {
                    Octree childOctree = octrees[currentOctree.childIndexStart + k];
                    //finalList.Add(currentOctree.childIndexStart + k);
                    if (OctreeOctreeIntersectTest(rootOctree, childOctree))
                    {
                        if (childOctree.childIndexStart != -1) subOctreesToSearch.Add(childOctree.index);
                        else finalList.Add(childOctree.index);
                    }
                }
            }
        }
        return finalList;
    }
    //Octree-octree intersect test
    private bool OctreeOctreeIntersectTest(Octree rootOctree, Octree currentOctree) 
    {
        bool3 dir1 = new float3(rootOctree.position) - 0.5f <= currentOctree.position + currentOctree.size;
        bool3 dir2 = new float3(rootOctree.position) + rootOctree.size + 0.5f >= currentOctree.position;
        bool3 final = dir1 & dir2;
        return final.x && final.y && final.z;
    }
    private int FindBiggestNeighbour(NativeList<Octree> octrees, int currentOctreeIndex) 
    {
        /*
        *  0, 0, 0
        *  0, 0, 1
        *  0, 1, 0
        *  0, 1, 1
        *  1, 0, 0
        *  1, 0, 1
        *  1, 1, 0
        *  1, 1, 1
        */
        Octree currentOctree = octrees[currentOctreeIndex];
        int biggestNeighbour = -1;
        //Find neighbour of bigger size
        //Check if we are the northest child, if we are then go up the tree
        while ((currentOctree.childDirection == 1 || currentOctree.childDirection == 3 || currentOctree.childDirection == 5 || currentOctree.childDirection == 7) && currentOctree.hierarchyIndex != 0)
        {
            //Go up the tree 
            currentOctree = octrees[currentOctree.cameFromIndex];
        }
        //Check if we are the root node
        if (currentOctree.hierarchyIndex != 0)
        {
            //Current node is not a parent, so don't think of it as a parent, think of it as a sibling instead
            if (currentOctree.childDirection == 0) biggestNeighbour = currentOctree.index + 1;
            if (currentOctree.childDirection == 2) biggestNeighbour = currentOctree.index + 1;
            if (currentOctree.childDirection == 4) biggestNeighbour = currentOctree.index + 1;
            if (currentOctree.childDirection == 6) biggestNeighbour = currentOctree.index + 1;
        }
        return biggestNeighbour;
    }
    private List<int> FindSmallerNeighbourChildren(NativeList<Octree> octrees, int currentNeighbourOctreeIndex, int currentOctreeIndex) 
    {
        /*
        *  0, 0, 0
        *  0, 0, 1
        *  0, 1, 0
        *  0, 1, 1
        *  1, 0, 0
        *  1, 0, 1
        *  1, 1, 0
        *  1, 1, 1
        */
        List<int> subOctreesToSearch = new List<int>();
        List<int> finalList = new List<int>();
        subOctreesToSearch.Add(currentNeighbourOctreeIndex);
        Octree currentOctree = octrees[currentNeighbourOctreeIndex];
        Octree currentRootOctree = octrees[currentOctreeIndex];
        int octreeDirection = -1;
        //Octree doesn't have any children, so exit out of the method
        if (currentOctree.childIndexStart == -1)
        {
            finalList.Add(currentNeighbourOctreeIndex);
            return finalList;
        }
        //Recursively find the correct children
        for (int i = 0; i < subOctreesToSearch.Count; i++)
        {
            currentOctree = octrees[subOctreesToSearch[i]];
            if (currentOctree.childIndexStart != -1)
            {
                octreeDirection = currentOctree.childDirection;
                //Search through the children
                for (int k = 0; k < 8; k++)
                {
                    Octree childOctree = octrees[currentOctree.childIndexStart + k];
                    int childDirection = childOctree.childDirection;
                    bool correctDirection = childDirection == 0 || childDirection == 2 || childDirection == 4 || childDirection == 6;
                    //finalList.Add(currentOctree.childIndexStart + k);
                    UnityEngine.Debug.Log(childOctree.hierarchyIndex + " : " + currentRootOctree.hierarchyIndex);
                    if (correctDirection && childOctree.hierarchyIndex >= currentRootOctree.hierarchyIndex)
                    {                        
                        if (childOctree.childIndexStart != -1) subOctreesToSearch.Add(childOctree.index);
                        else finalList.Add(childOctree.index);                        
                    }   
                }
            }
        }

        return finalList;
    }

}
[Serializable]
public struct TerrainData 
{
    public float3 offset;
    public float3 scale;
    public float3 noise1Scale;
}
