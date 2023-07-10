using System;
using Opencraft.Terrain.Authoring;
using Opencraft.Terrain.Blocks;
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
                .WithAll<TerrainBlocks, TerrainArea, TerrainNeighbors, LocalTransform, RenderMeshArray, Remesh>()
                .Build(EntityManager);
            // Set layout object for creating VBO
            _vertexLayout = new NativeArray<VertexAttributeDescriptor>(1, Allocator.Persistent);
            // Block locations are on a discrete, limited grid within a terrain area
            // Max size of a terrain area is thus 255!
            // 24 coord, TexCoord as 5 bits, normals as 3 bits
            _vertexLayout[0] = new VertexAttributeDescriptor(attribute: VertexAttribute.Position, format: VertexAttributeFormat.UInt32, dimension: 1, stream: 0);
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
            NativeArray<TerrainNeighbors> terrainNeighbors =
                _terrainAreaQuery.ToComponentDataArray<TerrainNeighbors>(Allocator.TempJob);
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
                terrainNeighbors= terrainNeighbors,
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
            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, meshes, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);
            chunksToUpdate.Dispose();
            terrainAreas.Dispose();
            terrainNeighbors.Dispose();

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
        public NativeArray<TerrainNeighbors> terrainNeighbors;

        [ReadOnly] public BufferLookup<TerrainBlocks> terrainBufferLookup;

        public void Execute(int index)
        {
            Entity entity = areasToUpdate[index];
            TerrainNeighbors terrainNeighbor = terrainNeighbors[index];
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
            if (terrainNeighbor.neighborXP != Entity.Null)
                neighborXP = terrainBufferLookup[terrainNeighbor.neighborXP];
            DynamicBuffer<TerrainBlocks> neighborXN = default;
            if (terrainNeighbor.neighborXN != Entity.Null)
                neighborXN = terrainBufferLookup[terrainNeighbor.neighborXN];
            DynamicBuffer<TerrainBlocks> neighborYP = default;
            if (terrainNeighbor.neighborYP != Entity.Null)
                neighborYP = terrainBufferLookup[terrainNeighbor.neighborYP];
            DynamicBuffer<TerrainBlocks> neighborYN = default;
            if (terrainNeighbor.neighborYN != Entity.Null)
                neighborYN = terrainBufferLookup[terrainNeighbor.neighborYN];
            DynamicBuffer<TerrainBlocks> neighborZP = default;
            if (terrainNeighbor.neighborZP != Entity.Null)
                neighborZP = terrainBufferLookup[terrainNeighbor.neighborZP];
            DynamicBuffer<TerrainBlocks> neighborZN = default;
            if (terrainNeighbor.neighborZN != Entity.Null)
                neighborZN = terrainBufferLookup[terrainNeighbor.neighborZN];
            
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
            NativeArray<uint> vertexBuffer = meshData.GetVertexData<uint>();
            NativeArray<ushort> indices = meshData.GetIndexData<ushort>();

            int access;
            int numFaces = 0;
            //i, j and k refer to chunk-relative positions (range 0 to blocksPerSide-1)
            // Y axis - start from the bottom and search up
            for (uint j = 0; j < blocksPerSide; j++)
            {
                uint j1 = j + 1;
                // Z axis
                for (uint k = 0; k < blocksPerSide; k++)
                {
                    uint k1 = k + 1;
                    // X axis
                    for (uint i = 0; i < blocksPerSide; i++)
                    {
                        access = (int)(i + j * blocksPerSide + k * blocksPerSideSquared);
                        BlockType b = blocks[access].Value;

                        if (b == BlockType.Air)
                            continue;
                        uint i1 = i + 1;
                        uint length;
                        int chunkAccess = 0;

                        // Left face (XN)
                        if (!visitedXN[access] && TerrainUtilities.VisibleFaceXN((int)(i - 1), (int)j, (int)k, ref blocks, ref neighborXN))
                        {
                            length = 0;
                            // Search upwards to determine run length
                            for (uint q = j; q < blocksPerSide; q++)
                            {
                                // Pre-calculate the array lookup as it is used twice
                                chunkAccess = (int)(i + q * blocksPerSide + k * blocksPerSideSquared);

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
                                AppendQuad(ref vertexBuffer, ref indices, numFaces,
                                    new uint3(i, j, k),
                                    new uint3(i, length + j, k),
                                    new uint3(i, length + j, k1),
                                    new uint3(i, j, k1),
                                    (int)FaceTypeShifted.xn, b);
                                numFaces++;
                            }
                        }

                        // Right face (XP)
                        if (!visitedXP[access] && TerrainUtilities.VisibleFaceXP((int)i1, (int)j, (int)k, ref blocks, ref neighborXP))
                        {
                            length = 0;
                            for (uint q = j; q < blocksPerSide; q++)
                            {
                                chunkAccess = (int)(i + q * blocksPerSide + k * blocksPerSideSquared);

                                if (b != blocks[chunkAccess].Value)
                                    break;

                                visitedXP[chunkAccess] = true;

                                length++;
                            }

                            if (length > 0)
                            {
                                AppendQuad(ref vertexBuffer, ref indices, numFaces,
                                    new uint3(i1, j, k1),
                                    new uint3(i1, length + j, k1),
                                    new uint3(i1, length + j, k),
                                    new uint3(i1, j, k),
                                    (int)FaceTypeShifted.xp, b);
                                numFaces++;
                            }
                        }

                        // Back face (ZN)
                        if (!visitedZN[access] && TerrainUtilities.VisibleFaceZN((int)i, (int)j, (int)k - 1, ref blocks, ref neighborZN))
                        {
                            length = 0;
                            for (uint q = j; q < blocksPerSide; q++)
                            {
                                chunkAccess = (int)(i + q * blocksPerSide + k * blocksPerSideSquared);

                                if (b != blocks[chunkAccess].Value)
                                    break;

                                visitedZN[chunkAccess] = true;

                                length++;
                            }

                            if (length > 0)
                            {
                                AppendQuad(ref vertexBuffer, ref indices, numFaces,
                                    new uint3(i1, j, k),
                                    new uint3(i1, length + j, k),
                                    new uint3(i, length + j, k),
                                    new uint3(i, j, k),
                                    (int)FaceTypeShifted.zn, b);
                                numFaces++;
                            }
                        }

                        // Front face (ZP)
                        if (!visitedZP[access] && TerrainUtilities.VisibleFaceZP((int)i, (int)j, (int)k1, ref blocks, ref neighborZP))
                        {
                            length = 0;
                            for (uint q = j; q < blocksPerSide; q++)
                            {
                                chunkAccess = (int)(i + q * blocksPerSide + k * blocksPerSideSquared);

                                if (b != blocks[chunkAccess].Value)
                                    break;

                                visitedZP[chunkAccess] = true;

                                length++;
                            }

                            if (length > 0)
                            {
                                AppendQuad(ref vertexBuffer, ref indices, numFaces,
                                    new uint3(i, j, k1),
                                    new uint3(i, length + j, k1),
                                    new uint3(i1, length + j, k1),
                                    new uint3(i1, j, k1),
                                    (int)FaceTypeShifted.zp, b);
                                numFaces++;
                            }
                        }

                        // Bottom face (YN)
                        if (!visitedYN[access] && TerrainUtilities.VisibleFaceYN((int)i, (int)j - 1, (int)k, ref blocks, ref neighborYN))
                        {
                            length = 0;
                            // extend in X axis
                            for (uint q = i; q < blocksPerSide; q++)
                            {
                                chunkAccess = (int)(q + j * blocksPerSide + k * blocksPerSideSquared);

                                if (b != blocks[chunkAccess].Value)
                                    break;

                                visitedYN[chunkAccess] = true;

                                length++;
                            }

                            if (length > 0)
                            {
                                AppendQuad(ref vertexBuffer, ref indices, numFaces,
                                    new uint3(i, j, k1),
                                    new uint3(length + i, j, k1),
                                    new uint3(length + i, j, k),
                                    new uint3(i, j, k),
                                    (int)FaceTypeShifted.yn, b);
                                numFaces++;
                            }
                        }

                        // Top face (YP)
                        if (!visitedYP[access] && TerrainUtilities.VisibleFaceYP((int)i, (int)j1, (int)k, ref blocks, ref neighborYP))
                        {
                            length = 0;
                            // extend in X axis
                            for (uint q = i; q < blocksPerSide; q++)
                            {
                                chunkAccess = (int)(q + j * blocksPerSide + k * blocksPerSideSquared);

                                if (b != blocks[chunkAccess].Value)
                                    break;

                                visitedYP[chunkAccess] = true;

                                length++;
                            }

                            if (length > 0)
                            {
                                AppendQuad(ref vertexBuffer, ref indices, numFaces,
                                    new uint3(i, j1, k),
                                    new uint3(length + i, j1, k),
                                    new uint3(length + i, j1, k1),
                                    new uint3(i, j1, k1),
                                    (int)FaceTypeShifted.yp, b);
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
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, numFaces * 6), MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);
        }
        

        private enum FaceTypeShifted : int
        {
            yp = 0 << 29,
            yn = 1 << 29,
            xp = 2 << 29,
            xn = 3 << 29,
            zp = 4 << 29,
            zn = 5 << 29,
        }
    
        private void AppendQuad(ref NativeArray<uint> verts, ref NativeArray<ushort> indices,
            int numFaces, uint3 bl, uint3 tl, uint3 tr, uint3 br, int normal, BlockType blockType)
        {
            int vb = numFaces * 4;
            // Pre bit-shifted texture ID map
            int texture = BlockData.BlockToTexture[(int)blockType];
            // 32 bit vertex layout:
            // byte: x pos
            // byte: y pos
            // byte: z pos
            // 5 bits: texture unit
            // 3 bits: normal
            uint shared = (uint)(texture | normal);
            // Top left vertex
            uint tlv = CombinePosition(tl, shared);

            // Top right vertex
            uint trv = CombinePosition(tr, shared);

            // Bottom left vertex
            uint blv = CombinePosition(bl, shared);

            // Bottom right vertex
            uint brv = CombinePosition(br, shared);
        
            // Store each vertex directly into the buffer
            verts[vb] = blv;
            verts[vb + 1] = brv;
            verts[vb + 2] = trv;
            verts[vb + 3] = tlv;
        
            // Set indices
            int ib = numFaces * 6;
            indices[ib] = (ushort)vb;
            indices[ib + 1] = (ushort)(vb + 1);
            indices[ib + 2] = (ushort)(vb + 2);
            indices[ib + 3] = (ushort)(vb + 2);
            indices[ib + 4] = (ushort)(vb + 3);
            indices[ib + 5] = (ushort)(vb + 0);
        }

        // Combine position data with the shared uint
        private uint CombinePosition(uint3 pos, uint shared)
        {
            return (uint)(shared | 
                          ((uint)pos.x & 255) |
                          ((uint)pos.y & 255) << 8 |
                          ((uint)pos.z & 255) << 16);
        }

    }
}