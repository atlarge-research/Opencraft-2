using Opencraft.Terrain.Authoring;
using Opencraft.Terrain.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace Opencraft.Rendering
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.Presentation)]
    [UpdateAfter(typeof(TerrainChangeMonitoringSystem))]
    [BurstCompile]
    // Creates a mesh for each terrain area using a basic greedy meshing technique
    public partial class TerrainMeshingSystem : SystemBase
    {
        private EntityQuery _terrainSpawnerQuery;
        private EntityQuery _terrainAreaQuery;
        private BufferLookup<TerrainBlocks> _terrainBufferLookup;
        private NativeArray<VertexAttributeDescriptor> _vertexLayout;

        protected override void OnCreate()
        {
            RequireForUpdate<TerrainSpawner>();
            RequireForUpdate<TerrainArea>();
            _terrainSpawnerQuery = GetEntityQuery(ComponentType.ReadOnly<TerrainSpawner>());
            // Fetch terrain that needs to be remeshed
            _terrainAreaQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TerrainBlocks, TerrainArea, LocalTransform, RenderMeshArray, Remesh>()
                .Build(EntityManager);
            // Set layout object for creating VBO
            _vertexLayout = new NativeArray<VertexAttributeDescriptor>(2, Allocator.Persistent);
            _vertexLayout[0] = new VertexAttributeDescriptor(VertexAttribute.Position);
            _vertexLayout[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, stream: 1);
        }

        protected override void OnDestroy()
        {
            _vertexLayout.Dispose();
        }

        // Cannot be burst compiled, deals with managed Mesh objects
        protected override void OnUpdate()
        {
            // todo - batch meshing calls as new terrain areas tend to arrive staggered which is a worst case
            // todo - as remeshing must be done for all neighbors of new areas
            if (_terrainAreaQuery.IsEmpty)
                return;

            TerrainSpawner terrainSpawner = _terrainSpawnerQuery.GetSingleton<TerrainSpawner>();
            NativeArray<Entity> chunksToUpdate = _terrainAreaQuery.ToEntityArray(Allocator.TempJob);
            NativeArray<TerrainArea> terrainAreas =
                _terrainAreaQuery.ToComponentDataArray<TerrainArea>(Allocator.TempJob);
            _terrainBufferLookup = GetBufferLookup<TerrainBlocks>(true);
            // Construct our unmanaged mesh array that can be passed to the job 
            Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(chunksToUpdate.Length);
            MeshTerrainChunkJob meshJob = new MeshTerrainChunkJob
            {
                vertexLayout = _vertexLayout,
                blocksPerSide = terrainSpawner.blocksPerSide,
                blocksPerSideSquared = terrainSpawner.blocksPerSide * terrainSpawner.blocksPerSide,
                blocksPerSideCubed = terrainSpawner.blocksPerSide * terrainSpawner.blocksPerSide *
                                     terrainSpawner.blocksPerSide,
                meshDataArray = meshDataArray,
                areasToUpdate = chunksToUpdate,
                terrainAreas = terrainAreas,
                terrainBufferLookup = _terrainBufferLookup
            };
            // todo we can potentially have the handling of meshJob output happen on later frames to reduce
            // todo stuttering caused by large remesh jobs
            JobHandle handle = meshJob.Schedule(chunksToUpdate.Length, 1, Dependency);
            handle.Complete();
            
            // Get the existing terrain area mesh objects
            Mesh[] meshes = new Mesh[chunksToUpdate.Length];
            for (int i = 0; i < chunksToUpdate.Length; i++)
            {
                Entity chunkEntity = chunksToUpdate[i];
                var renderMeshArray = EntityManager.GetSharedComponentManaged<RenderMeshArray>(chunkEntity);
                var materialMeshInfo = EntityManager.GetComponentData<MaterialMeshInfo>(chunkEntity);
                meshes[i] = renderMeshArray.GetMesh(materialMeshInfo);
                //Debug.Log($"Found mesh {meshes[i].name}");
            }
            // Update the terrain area meshes
            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, meshes, MeshUpdateFlags.DontValidateIndices);
            chunksToUpdate.Dispose();
            terrainAreas.Dispose();

            // Mark that these areas have been (re)meshed
            EntityManager.SetComponentEnabled<Remesh>(_terrainAreaQuery, false);
        }
    }


    // Greedy meshing algorithm adapted by https://vercidium.com/blog/voxel-world-optimisations/
    // One mesh per terrain area, one quad face per run
    // X and Z runs extend along y axis, Y runs extend along X axis.
    [BurstCompile]
    public partial struct MeshTerrainChunkJob : IJobParallelFor
    {
        public NativeArray<VertexAttributeDescriptor> vertexLayout;
        public int blocksPerSide;
        public int blocksPerSideSquared;
        public int blocksPerSideCubed;
        public Mesh.MeshDataArray meshDataArray;
        public NativeArray<Entity> areasToUpdate;
        public NativeArray<TerrainArea> terrainAreas;

        [ReadOnly] public BufferLookup<TerrainBlocks> terrainBufferLookup;

        public void Execute(int index)
        {
            Entity entity = areasToUpdate[index];
            TerrainArea terrainArea = terrainAreas[index];
            // When area is remeshed, outline it in red
            float3 loc = terrainArea.location * blocksPerSide;
            TerrainUtilities.DebugDrawTerrainArea(ref loc, Color.red, 0.5f);
            
            // Mesh object vertex data
            Mesh.MeshData meshData = meshDataArray[index];
            // The blocks in this chunk
            DynamicBuffer<TerrainBlocks> blocks = terrainBufferLookup[entity];


            // References to neighbor areas
            DynamicBuffer<TerrainBlocks> neighborXP = default;
            if (terrainArea.neighborXP != Entity.Null)
                neighborXP = terrainBufferLookup[terrainArea.neighborXP];
            DynamicBuffer<TerrainBlocks> neighborXN = default;
            if (terrainArea.neighborXN != Entity.Null)
                neighborXN = terrainBufferLookup[terrainArea.neighborXN];
            DynamicBuffer<TerrainBlocks> neighborYP = default;
            if (terrainArea.neighborYP != Entity.Null)
                neighborYP = terrainBufferLookup[terrainArea.neighborYP];
            DynamicBuffer<TerrainBlocks> neighborYN = default;
            if (terrainArea.neighborYN != Entity.Null)
                neighborYN = terrainBufferLookup[terrainArea.neighborYN];
            DynamicBuffer<TerrainBlocks> neighborZP = default;
            if (terrainArea.neighborZP != Entity.Null)
                neighborZP = terrainBufferLookup[terrainArea.neighborZP];
            DynamicBuffer<TerrainBlocks> neighborZN = default;
            if (terrainArea.neighborZN != Entity.Null)
                neighborZN = terrainBufferLookup[terrainArea.neighborZN];
            
            // Bitmasks that mark a terrain block as visited
            NativeArray<bool> visitedXN = new NativeArray<bool>(blocksPerSideCubed, Allocator.Temp);
            NativeArray<bool> visitedXP = new NativeArray<bool>(blocksPerSideCubed, Allocator.Temp);
            NativeArray<bool> visitedZN = new NativeArray<bool>(blocksPerSideCubed, Allocator.Temp);
            NativeArray<bool> visitedZP = new NativeArray<bool>(blocksPerSideCubed, Allocator.Temp);
            NativeArray<bool> visitedYN = new NativeArray<bool>(blocksPerSideCubed, Allocator.Temp);
            NativeArray<bool> visitedYP = new NativeArray<bool>(blocksPerSideCubed, Allocator.Temp);
            
            // Setup mesh data arrays
            int currentVertexBufferSize = 4096;
            meshData.SetVertexBufferParams(currentVertexBufferSize, vertexLayout);
            int currentIndexBufferSize = 6144;
            meshData.SetIndexBufferParams(currentIndexBufferSize, IndexFormat.UInt16);
            NativeArray<Vector3> verts = meshData.GetVertexData<Vector3>();
            NativeArray<Vector3> norms = meshData.GetVertexData<Vector3>(stream: 1);
            NativeArray<ushort> indices = meshData.GetIndexData<ushort>();

            int access;
            int numFaces = 0;
            //i, j and k refer to chunk-relative positions (range 0 to blocksPerSide-1)
            // Y axis - start from the bottom and search up
            for (int j = 0; j < blocksPerSide; j++)
            {
                int j1 = j + 1;
                // Z axis
                for (int k = 0; k < blocksPerSide; k++)
                {
                    int k1 = k + 1;
                    // X axis
                    for (int i = 0; i < blocksPerSide; i++)
                    {
                        access = i + j * blocksPerSide + k * blocksPerSideSquared;
                        int b = blocks[access].Value;

                        if (b == -1)
                            continue;
                        int i1 = i + 1;
                        int length;
                        int chunkAccess = 0;

                        // Left face (XN)
                        if (!visitedXN[access] && TerrainUtilities.VisibleFaceXN(i - 1, j, k, ref blocks, ref neighborXN))
                        {
                            length = 0;
                            // Search upwards to determine run length
                            for (int q = j; q < blocksPerSide; q++)
                            {
                                // Pre-calculate the array lookup as it is used twice
                                chunkAccess = i + q * blocksPerSide + k * blocksPerSideSquared;

                                // If we reach a different block or an empty block, end the run
                                if (b != blocks[chunkAccess].Value)
                                    break;

                                // Store that we have visited this block
                                visitedXN[chunkAccess] = true;

                                length++;
                            }

                            if (length > 0)
                            {
                                // Create a quad and write it directly to the buffer
                                AppendQuad(ref verts, ref norms, ref indices, numFaces,
                                    new int3(i, j, k),
                                    new int3(i, length + j, k),
                                    new int3(i, length + j, k1),
                                    new int3(i, j, k1),
                                    new float3(-1, 0, 0), b);
                                numFaces++;
                            }
                        }

                        // Right face (XP)
                        if (!visitedXP[access] && TerrainUtilities.VisibleFaceXP(i1, j, k, ref blocks, ref neighborXP))
                        {
                            length = 0;
                            for (int q = j; q < blocksPerSide; q++)
                            {
                                chunkAccess = i + q * blocksPerSide + k * blocksPerSideSquared;

                                if (b != blocks[chunkAccess].Value)
                                    break;

                                visitedXP[chunkAccess] = true;

                                length++;
                            }

                            if (length > 0)
                            {
                                AppendQuad(ref verts, ref norms, ref indices, numFaces,
                                    new int3(i1, j, k1),
                                    new int3(i1, length + j, k1),
                                    new int3(i1, length + j, k),
                                    new int3(i1, j, k),
                                    new float3(1, 0, 0), b);
                                numFaces++;
                            }
                        }

                        // Back face (ZN)
                        if (!visitedZN[access] && TerrainUtilities.VisibleFaceZN(i, j, k - 1, ref blocks, ref neighborZN))
                        {
                            length = 0;
                            for (int q = j; q < blocksPerSide; q++)
                            {
                                chunkAccess = i + q * blocksPerSide + k * blocksPerSideSquared;

                                if (b != blocks[chunkAccess].Value)
                                    break;

                                visitedZN[chunkAccess] = true;

                                length++;
                            }

                            if (length > 0)
                            {
                                AppendQuad(ref verts, ref norms, ref indices, numFaces,
                                    new int3(i1, j, k),
                                    new int3(i1, length + j, k),
                                    new int3(i, length + j, k),
                                    new int3(i, j, k),
                                    new float3(0, 0, -1), b);
                                numFaces++;
                            }
                        }

                        // Front face (ZP)
                        if (!visitedZP[access] && TerrainUtilities.VisibleFaceZP(i, j, k1, ref blocks, ref neighborZP))
                        {
                            length = 0;
                            for (int q = j; q < blocksPerSide; q++)
                            {
                                chunkAccess = i + q * blocksPerSide + k * blocksPerSideSquared;

                                if (b != blocks[chunkAccess].Value)
                                    break;

                                visitedZP[chunkAccess] = true;

                                length++;
                            }

                            if (length > 0)
                            {
                                AppendQuad(ref verts, ref norms, ref indices, numFaces,
                                    new int3(i, j, k1),
                                    new int3(i, length + j, k1),
                                    new int3(i1, length + j, k1),
                                    new int3(i1, j, k1),
                                    new float3(0, 0, 1), b);
                                numFaces++;
                            }
                        }

                        // Bottom face (YN)
                        if (!visitedYN[access] && TerrainUtilities.VisibleFaceYN(i, j - 1, k, ref blocks, ref neighborYN))
                        {
                            length = 0;
                            // extend in X axis
                            for (int q = i; q < blocksPerSide; q++)
                            {
                                chunkAccess = q + j * blocksPerSide + k * blocksPerSideSquared;

                                if (b != blocks[chunkAccess].Value)
                                    break;

                                visitedYN[chunkAccess] = true;

                                length++;
                            }

                            if (length > 0)
                            {
                                AppendQuad(ref verts, ref norms, ref indices, numFaces,
                                    new int3(i, j, k1),
                                    new int3(length + i, j, k1),
                                    new int3(length + i, j, k),
                                    new int3(i, j, k),
                                    new float3(0, -1, 0), b);
                                numFaces++;
                            }
                        }

                        // Top face (YP)
                        if (!visitedYP[access] && TerrainUtilities.VisibleFaceYP(i, j1, k, ref blocks, ref neighborYP))
                        {
                            length = 0;
                            // extend in X axis
                            for (int q = i; q < blocksPerSide; q++)
                            {
                                chunkAccess = q + j * blocksPerSide + k * blocksPerSideSquared;

                                if (b != blocks[chunkAccess].Value)
                                    break;

                                visitedYP[chunkAccess] = true;

                                length++;
                            }

                            if (length > 0)
                            {
                                AppendQuad(ref verts, ref norms, ref indices, numFaces,
                                    new int3(i, j1, k),
                                    new int3(length + i, j1, k),
                                    new int3(length + i, j1, k1),
                                    new int3(i, j1, k1),
                                    new float3(0, 1, 0), b);
                                numFaces++;
                            }
                        }
                    }

                    //Extend the mesh arrays if nearly full
                    // todo test this, not sure if it has ever happened yet
                    if (numFaces * 4 > currentVertexBufferSize - 2048)
                    {
                        currentVertexBufferSize += 2048;
                        meshData.SetVertexBufferParams(currentVertexBufferSize, vertexLayout);
                        currentIndexBufferSize += 3072;
                        meshData.SetIndexBufferParams(currentIndexBufferSize, IndexFormat.UInt16);
                        //Debug.Log($"Terrain area {terrainArea.location} vertex buffer extended to {currentVertexBufferSize}!");
                    }

                }
            }

            //Debug.Log($"Terrain area {terrainArea.location} now has {numFaces} faces");
            meshData.SetVertexBufferParams(numFaces * 4, vertexLayout);
            meshData.SetIndexBufferParams(numFaces * 6, IndexFormat.UInt16);
            // Finalize the mesh
            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, numFaces * 6));
        }
        


        /*
         todo- this can be aggressively optimized, right now we are using float vertices instead of int and float3 normals instead
         todo- of 3 bits. The many assignments are already vectorized by burst though
         */
        private void AppendQuad(ref NativeArray<Vector3> verts, ref NativeArray<Vector3> norms,
            ref NativeArray<ushort> indices,
            int numFaces, float3 bl, float3 tl, float3 tr, float3 br, float3 normal, int blockType)
        {
            int vb = numFaces * 4;
            // Winding order is bottom left, bottom right, top right, top left.
            verts[vb] = bl;
            verts[vb + 1] = br;
            verts[vb + 2] = tr;
            verts[vb + 3] = tl;
            norms[vb] = normal;
            norms[vb + 1] = normal;
            norms[vb + 2] = normal;
            norms[vb + 3] = normal;
            int ib = numFaces * 6;
            // We use triangle meshes and ignore tangent and UV for now
            indices[ib] = (ushort)vb;
            indices[ib + 1] = (ushort)(vb + 1);
            indices[ib + 2] = (ushort)(vb + 2);
            indices[ib + 3] = (ushort)(vb + 2);
            indices[ib + 4] = (ushort)(vb + 3);
            indices[ib + 5] = (ushort)(vb + 0);
        }

    }
}