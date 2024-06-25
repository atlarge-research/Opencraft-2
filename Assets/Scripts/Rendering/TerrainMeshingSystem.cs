using System.Runtime.CompilerServices;
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
        private BufferLookup<TerrainBlocks> _terrainBlocksBufferLookup;
        private BufferLookup<BlockLogicState> _terrainLogicStateLookup;
        private BufferLookup<BlockDirection> _terrainDirectionLookup;
        private BufferLookup<TerrainColMinY> _terrainColumnMinBufferLookup;
        private BufferLookup<TerrainColMaxY> _terrainColumnMaxBufferLookup;
        private NativeArray<VertexAttributeDescriptor> _vertexLayout;

        protected override void OnCreate()
        {
            RequireForUpdate<TerrainSpawner>();
            RequireForUpdate<TerrainArea>();
            _terrainSpawnerQuery = GetEntityQuery(ComponentType.ReadOnly<TerrainSpawner>());
            // Fetch terrain that needs to be remeshed
            NativeList<ComponentType> components = new NativeList<ComponentType>(8, Allocator.Temp)
            {
                ComponentType.ReadOnly<TerrainBlocks>(),
                ComponentType.ReadOnly<BlockLogicState>(),
                ComponentType.ReadOnly<BlockDirection>(),
                ComponentType.ReadOnly<TerrainArea>(),
                ComponentType.ReadOnly<TerrainNeighbors>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<RenderMeshArray>(),
                ComponentType.ReadOnly<Remesh>()
            };
            _terrainAreaQuery = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll(ref components)
                    .Build(EntityManager);
            // Set layout object for creating VBO
            _vertexLayout = new NativeArray<VertexAttributeDescriptor>(1, Allocator.Persistent);
            // Block locations are on a discrete, limited grid within a terrain area
            // Max size of a terrain area is thus 255!
            // 24 coord, TexCoord as 5 bits, normals as 3 bits
            _vertexLayout[0] = new VertexAttributeDescriptor(attribute: VertexAttribute.Position, format: VertexAttributeFormat.SInt32, dimension: 1, stream: 0);
            Debug.Log("MESHING SYSTEM IS ACTIVE");
            components.Dispose();
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
            // Get block types and column heightmaps
            _terrainBlocksBufferLookup = GetBufferLookup<TerrainBlocks>(true);
            _terrainLogicStateLookup = GetBufferLookup<BlockLogicState>(true);
            _terrainDirectionLookup = GetBufferLookup<BlockDirection>(true);
            _terrainColumnMinBufferLookup = GetBufferLookup<TerrainColMinY>(true);
            _terrainColumnMaxBufferLookup = GetBufferLookup<TerrainColMaxY>(true);
            // Construct our unmanaged mesh array that can be passed to the job 
            Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(chunksToUpdate.Length);
            MeshTerrainChunkJob meshJob = new MeshTerrainChunkJob
            {
                vertexLayout = _vertexLayout,
                meshDataArray = meshDataArray,
                areasToUpdate = chunksToUpdate,
                terrainAreas = terrainAreas,
                terrainNeighbors = terrainNeighbors,
                terrainBufferLookup = _terrainBlocksBufferLookup,
                terrainLogicStateLookup = _terrainLogicStateLookup,
                terrainDirectionLookup = _terrainDirectionLookup,
                terrainColumnMinBufferLookup = _terrainColumnMinBufferLookup,
                terrainColumnMaxBufferLookup = _terrainColumnMaxBufferLookup,
                UseDebug = PolkaDOTS.ApplicationConfig.DebugEnabled.Value
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


    // Greedy meshing algorithm adapted from https://vercidium.com/blog/voxel-world-optimisations/
    // One mesh per terrain area, one quad face per run
    // X and Z runs extend along y axis, Y runs extend along X axis.
    [BurstCompile]
    public partial struct MeshTerrainChunkJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<VertexAttributeDescriptor> vertexLayout;
        public Mesh.MeshDataArray meshDataArray;
        [ReadOnly] public NativeArray<Entity> areasToUpdate;
        [ReadOnly] public NativeArray<TerrainArea> terrainAreas;
        [ReadOnly] public NativeArray<TerrainNeighbors> terrainNeighbors;
        [ReadOnly] public BufferLookup<TerrainBlocks> terrainBufferLookup;
        [ReadOnly] public BufferLookup<BlockLogicState> terrainLogicStateLookup;
        [ReadOnly] public BufferLookup<BlockDirection> terrainDirectionLookup;
        [ReadOnly] public BufferLookup<TerrainColMinY> terrainColumnMinBufferLookup;
        [ReadOnly] public BufferLookup<TerrainColMaxY> terrainColumnMaxBufferLookup;
        public bool UseDebug;
        public void Execute(int index)
        {
            Entity entity = areasToUpdate[index];
            TerrainNeighbors terrainNeighbor = terrainNeighbors[index];
            TerrainArea terrainArea = terrainAreas[index];
            // When area is remeshed, outline it in red
            if (UseDebug)
            {
                float3 terrainAreaLocation = terrainArea.location * Env.AREA_SIZE;
                TerrainUtilities.DebugDrawTerrainArea(in terrainAreaLocation, Color.red, 0.5f);
            }


            // Mesh object vertex data
            Mesh.MeshData meshData = meshDataArray[index];
            // The blocks in this chunk
            DynamicBuffer<TerrainBlocks> blocks = terrainBufferLookup[entity];
            // The min and max y of blocks in a given column in the chunk
            DynamicBuffer<byte> colMin = terrainColumnMinBufferLookup[entity].Reinterpret<byte>();
            DynamicBuffer<byte> colMax = terrainColumnMaxBufferLookup[entity].Reinterpret<byte>();


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
            NativeArray<bool> visitedXN = new NativeArray<bool>(Env.AREA_SIZE_POW_3, Allocator.Temp);
            NativeArray<bool> visitedXP = new NativeArray<bool>(Env.AREA_SIZE_POW_3, Allocator.Temp);
            NativeArray<bool> visitedZN = new NativeArray<bool>(Env.AREA_SIZE_POW_3, Allocator.Temp);
            NativeArray<bool> visitedZP = new NativeArray<bool>(Env.AREA_SIZE_POW_3, Allocator.Temp);
            NativeArray<bool> visitedYN = new NativeArray<bool>(Env.AREA_SIZE_POW_3, Allocator.Temp);
            NativeArray<bool> visitedYP = new NativeArray<bool>(Env.AREA_SIZE_POW_3, Allocator.Temp);

            // Setup mesh data arrays
            int currentVertexBufferSize = 6144;
            meshData.SetVertexBufferParams(currentVertexBufferSize, vertexLayout);
            int currentIndexBufferSize = 9216;
            meshData.SetIndexBufferParams(currentIndexBufferSize, IndexFormat.UInt16);
            NativeArray<int> vertexBuffer = meshData.GetVertexData<int>();
            NativeArray<ushort> indices = meshData.GetIndexData<ushort>();

            // Precalculate the map-relative Y position of the chunk in the map
            int chunkY = terrainArea.location.y * Env.AREA_SIZE;
            // Allocate variables on the stack
            // iBPS is i * bps, kBPS2 is k*bps*bps. S means shifted, x1 means x + 1
            int access, heightMapAccess, iBPS, kBPS2, i1, k1, j, j1, jS, jS1, topJ,
                kS, kS1, y, texture, accessIncremented, chunkAccess, length;
            bool minX, maxX, minY, maxY, minZ, maxZ;
            k1 = 1;
            int numFaces = 0;

            // Z axis
            for (int k = 0; k < Env.AREA_SIZE; k++, k1++)
            {
                kBPS2 = k * Env.AREA_SIZE_POW_2;
                i1 = 1;
                heightMapAccess = k * Env.AREA_SIZE;
                // Is the current run on the Z- or Z+ edge of the chunk
                minZ = k == 0;
                maxZ = k == Env.AREA_SIZE_1;

                // X axis
                for (int i = 0; i < Env.AREA_SIZE; i++, i1++)
                {
                    j = colMin[heightMapAccess];
                    topJ = colMax[heightMapAccess];
                    heightMapAccess++;
                    // Calculate this once, rather than multiple times in the inner loop
                    iBPS = i * Env.AREA_SIZE;
                    // Calculate access here and increment it each time in the innermost loop
                    access = kBPS2 + iBPS + j;
                    minX = i == 0;
                    maxX = i == Env.AREA_SIZE_1;
                    // Y axis
                    for (; j < topJ; j++, access++)
                    {
                        if (access >= Env.AREA_SIZE_POW_3)
                            Debug.Log($"Access {access} OOB for {i} {j} {k} with col max height {topJ}");
                        BlockType b = blocks[access].type;

                        if (b == BlockType.Air)
                            continue;

                        // Calculate length of run and make quads accordingly
                        minY = j == 0;
                        maxY = j == Env.AREA_SIZE_1;
                        kS = (k & 255) << 16; // pre bit shift for packing in AppendQuad functions
                        kS1 = (k1 & 255) << 16;
                        y = j + chunkY;
                        texture = BlockData.BlockToTexture[(int)b];
                        accessIncremented = access + 1;
                        j1 = j + 1;
                        jS = (j & 255) << 8;
                        jS1 = (j1 & 255) << 8;
                        // Left (X-)
                        if (!visitedXN[access] && TerrainUtilities.VisibleFaceXN(j, access, minX, kBPS2, ref blocks, ref neighborXN))
                        {
                            visitedXN[access] = true;
                            chunkAccess = accessIncremented;

                            for (length = jS1; length < Env.AREA_SIZE_SHIFTED; length += (1 << 8))
                            {
                                if (blocks[chunkAccess].type != b)
                                    break;

                                visitedXN[chunkAccess++] = true;
                            }

                            AppendQuadX(ref vertexBuffer, ref indices, ref numFaces, i, jS, length, kS, kS1, (int)FaceDirectionShifted.xn, texture);
                        }

                        // Right (X+)
                        if (!visitedXP[access] && TerrainUtilities.VisibleFaceXP(j, access, maxX, kBPS2, ref blocks, ref neighborXP))
                        {
                            visitedXP[access] = true;

                            chunkAccess = accessIncremented;

                            for (length = jS1; length < Env.AREA_SIZE_SHIFTED; length += (1 << 8))
                            {
                                if (blocks[chunkAccess].type != b)
                                    break;

                                visitedXP[chunkAccess++] = true;
                            }

                            AppendQuadX(ref vertexBuffer, ref indices, ref numFaces, i1, jS, length, kS1, kS, (int)FaceDirectionShifted.xp, texture);
                        }
                        // Back (Z-)
                        if (!visitedZN[access] && TerrainUtilities.VisibleFaceZN(j, access, minZ, iBPS, ref blocks, ref neighborZN))
                        {
                            visitedZN[access] = true;

                            chunkAccess = accessIncremented;

                            for (length = jS1; length < Env.AREA_SIZE_SHIFTED; length += (1 << 8))
                            {
                                if (blocks[chunkAccess].type != b)
                                    break;

                                visitedZN[chunkAccess++] = true;
                            }

                            AppendQuadZ(ref vertexBuffer, ref indices, ref numFaces, i, i1, jS, length, kS, (int)FaceDirectionShifted.zn, texture);
                        }

                        // Front (Z+)
                        if (!visitedZP[access] && TerrainUtilities.VisibleFaceZP(j, access, maxZ, iBPS, ref blocks, ref neighborZP))
                        {
                            visitedZP[access] = true;

                            chunkAccess = accessIncremented;

                            for (length = jS1; length < Env.AREA_SIZE_SHIFTED; length += (1 << 8))
                            {
                                if (blocks[chunkAccess].type != b)
                                    break;

                                visitedZP[chunkAccess++] = true;
                            }

                            AppendQuadZ(ref vertexBuffer, ref indices, ref numFaces, i1, i, jS, length, kS1, (int)FaceDirectionShifted.zp, texture);
                        }
                        // Bottom (Y-)
                        if (!visitedYN[access] && TerrainUtilities.VisibleFaceYN(access, minY, iBPS, kBPS2, ref blocks, ref neighborYN))
                        {
                            visitedYN[access] = true;

                            chunkAccess = access + Env.AREA_SIZE;

                            for (length = i1; length < Env.AREA_SIZE; length++)
                            {
                                if (blocks[chunkAccess].type != b)
                                    break;

                                visitedYN[chunkAccess] = true;

                                chunkAccess += Env.AREA_SIZE;
                            }
                            AppendQuadY(ref vertexBuffer, ref indices, ref numFaces, i, length, jS, kS1, kS, (int)FaceDirectionShifted.yn, texture);
                        }

                        // Top (Y+)
                        if (!visitedYP[access] && TerrainUtilities.VisibleFaceYP(access, maxY, iBPS, kBPS2, ref blocks, ref neighborYP))
                        {
                            visitedYP[access] = true;

                            chunkAccess = access + Env.AREA_SIZE;

                            for (length = i1; length < Env.AREA_SIZE; length++)
                            {
                                if (blocks[chunkAccess].type != b)
                                    break;

                                visitedYP[chunkAccess] = true;

                                chunkAccess += Env.AREA_SIZE;
                            }
                            AppendQuadY(ref vertexBuffer, ref indices, ref numFaces, i, length, jS1, kS, kS1, (int)FaceDirectionShifted.yp, texture);
                        }

                    }
                    // Extend if necessary
                    if (numFaces * 4 > currentVertexBufferSize - 2048)
                    {
                        currentVertexBufferSize += 2048;
                        meshData.SetVertexBufferParams(currentVertexBufferSize, vertexLayout);
                        currentIndexBufferSize += 3072;
                        meshData.SetIndexBufferParams(currentIndexBufferSize, IndexFormat.UInt16);
                        Debug.Log($"Terrain area {terrainArea.location} vertex buffer extended to {currentVertexBufferSize}!");
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


        // Specifies what direction a face is pointing, shifted for packing in AppendQuad functions
        private enum FaceDirectionShifted
        {
            yp = 0 << 29,
            yn = 1 << 29,
            xp = 2 << 29,
            xn = 3 << 29,
            zp = 4 << 29,
            zn = 5 << 29,
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendQuadX(ref NativeArray<int> vertexBuffer, ref NativeArray<ushort> indices, ref int numFaces, int x, int jBottom, int jTop, int kLeft, int kRight, int normal, int texture)
        {
            var shared = x |
                         texture |
                         normal;
            int vb = numFaces * 4;
            vertexBuffer[vb] = jBottom | kLeft | shared; // bl
            vertexBuffer[vb + 1] = jBottom | kRight | shared; // br
            vertexBuffer[vb + 2] = jTop | kRight | shared; // tr
            vertexBuffer[vb + 3] = jTop | kLeft | shared; // tl

            int ib = numFaces * 6;
            indices[ib] = indices[ib + 5] = (ushort)vb;
            indices[ib + 1] = (ushort)(vb + 1);
            indices[ib + 2] = indices[ib + 3] = (ushort)(vb + 2);
            indices[ib + 4] = (ushort)(vb + 3);

            numFaces++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendQuadY(ref NativeArray<int> vertexBuffer, ref NativeArray<ushort> indices, ref int numFaces, int xBottom, int xTop, int y, int zLeft, int zRight, int normal, int texture)
        {
            var shared = y | // x is not shifted, y is shifted by 8, z by 16
                         texture | // texture by 24
                         normal;   // normal by 29

            int vb = numFaces * 4;
            vertexBuffer[vb] = xBottom | zLeft | shared; // bl
            vertexBuffer[vb + 1] = xBottom | zRight | shared; // br
            vertexBuffer[vb + 2] = xTop | zRight | shared; // tr
            vertexBuffer[vb + 3] = xTop | zLeft | shared; // tl

            int ib = numFaces * 6;
            indices[ib] = indices[ib + 5] = (ushort)vb;
            indices[ib + 1] = (ushort)(vb + 1);
            indices[ib + 2] = indices[ib + 3] = (ushort)(vb + 2);
            indices[ib + 4] = (ushort)(vb + 3);


            numFaces++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendQuadZ(ref NativeArray<int> vertexBuffer, ref NativeArray<ushort> indices, ref int numFaces, int xBottom, int xTop, int yLeft, int yRight, int z, int normal, int texture)
        {
            var shared = z |
                         texture |
                         normal;


            int vb = numFaces * 4;
            vertexBuffer[vb] = xBottom | yLeft | shared; // bl
            vertexBuffer[vb + 1] = xBottom | yRight | shared; // br
            vertexBuffer[vb + 2] = xTop | yRight | shared; //tr
            vertexBuffer[vb + 3] = xTop | yLeft | shared; //tl

            int ib = numFaces * 6;
            indices[ib] = indices[ib + 5] = (ushort)vb;
            indices[ib + 1] = (ushort)(vb + 1);
            indices[ib + 2] = indices[ib + 3] = (ushort)(vb + 2);
            indices[ib + 4] = (ushort)(vb + 3);

            numFaces++;
        }

    }
}