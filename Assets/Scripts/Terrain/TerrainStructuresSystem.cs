using System;
using Opencraft.Terrain.Authoring;
using Opencraft.Terrain.Blocks;
using Opencraft.Terrain.Structures;
using Opencraft.Terrain.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;

namespace Opencraft.Terrain
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TerrainNeighborSystem))]
    [BurstCompile]
    // System that generates new terrain areas based on basic perlin noise
    public partial struct TerrainStructuresSystem : ISystem
    {
        private EntityQuery _structuresToSpawnQuery;
        private ProfilerMarker _markerStructureGen;
        private BufferLookup<TerrainBlocks> _terrainBlocksLookup;
        private BufferLookup<TerrainColMinY> _terrainColMinLookup;
        private BufferLookup<TerrainColMaxY> _terrainColMaxLookup;
        public void OnCreate(ref SystemState state)
        {
            // Wait for scene load/baking to occur before updates. 
            state.RequireForUpdate<TerrainSpawner>();
            state.RequireForUpdate<TerrainGenerationLayer>();
            state.RequireForUpdate<TerrainColumnsToSpawn>();
            state.RequireForUpdate<TerrainNeighbors>();
            _structuresToSpawnQuery = SystemAPI.QueryBuilder().WithAll<TerrainStructuresToSpawn, GenStructures, TerrainNeighbors, TerrainArea>().Build();
            //_terrainGenLayers= SystemAPI.QueryBuilder().WithAll<TerrainGenerationLayer>().Build().ToComponentDataArray<TerrainGenerationLayer>(Allocator.Persistent);
            _markerStructureGen = new ProfilerMarker("StructureGeneration");
            _terrainBlocksLookup = state.GetBufferLookup<TerrainBlocks>(isReadOnly: false);
            _terrainColMinLookup = state.GetBufferLookup<TerrainColMinY>(isReadOnly: false);
            _terrainColMaxLookup = state.GetBufferLookup<TerrainColMaxY>(isReadOnly: false);
            state.Enabled = false;
        }

   

        public void OnUpdate(ref SystemState state)
        {
            if (_structuresToSpawnQuery.IsEmpty)
            {
                return;
            }
            
            _markerStructureGen.Begin();
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
            EntityCommandBuffer.ParallelWriter parallelEcb = ecb.AsParallelWriter();
            new GenerateStructuresJob()
            {
                ecb = parallelEcb
            }.ScheduleParallel();
            
            ecb.Playback(state.EntityManager);
            _markerStructureGen.End();
        }
    }
    
    [BurstCompile]
    [WithAll(typeof(GenStructures))]
    public partial struct GenerateStructuresJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ecb;
        [ReadOnly] public BufferLookup<TerrainBlocks> terrainBufferLookup;
        [ReadOnly] public BufferLookup<TerrainColMinY> terrainColMinLookup;
        [ReadOnly] public BufferLookup<TerrainColMaxY> terrainColMaxLookup;
        
        public void Execute(Entity entity, [EntityIndexInQuery]int index, in TerrainNeighbors terrainNeighbor,
            ref DynamicBuffer<TerrainStructuresToSpawn> structuresToSpawnBuffer,
            ref DynamicBuffer<TerrainBlocks> terrainBlocksBuffer,
            ref DynamicBuffer<TerrainColMinY> colMinBuffer,
            ref DynamicBuffer<TerrainColMaxY> colMaxBuffer)
        {
            if (structuresToSpawnBuffer.IsEmpty)
            {
                return;
            }
            
            DynamicBuffer<BlockType> terrainBlocks = terrainBlocksBuffer.Reinterpret<BlockType>();
            DynamicBuffer<byte> colMax = colMaxBuffer.Reinterpret<byte>();
            DynamicBuffer<byte> colMin = colMinBuffer.Reinterpret<byte>();
            // References to neighbor areas
            /*DynamicBuffer<BlockType> neighborXP = default;
            if (terrainNeighbor.neighborXP != Entity.Null)
                neighborXP = terrainBufferLookup[terrainNeighbor.neighborXP].Reinterpret<BlockType>();
            DynamicBuffer<BlockType> neighborXN = default;
            if (terrainNeighbor.neighborXN != Entity.Null)
                neighborXN = terrainBufferLookup[terrainNeighbor.neighborXN].Reinterpret<BlockType>();*/
            DynamicBuffer<BlockType> neighborYP = default;
            DynamicBuffer<byte> neighborYPColMin = default;
            DynamicBuffer<byte> neighborYPColMax = default;
            if (terrainNeighbor.neighborYP != Entity.Null)
            {
                neighborYP = terrainBufferLookup[terrainNeighbor.neighborYP].Reinterpret<BlockType>();
                neighborYPColMin = terrainColMinLookup[terrainNeighbor.neighborYP].Reinterpret<byte>();
                neighborYPColMax = terrainColMaxLookup[terrainNeighbor.neighborYP].Reinterpret<byte>();
            }

            /*DynamicBuffer<BlockType> neighborYN = default;
            if (terrainNeighbor.neighborYN != Entity.Null)
                neighborYN = terrainBufferLookup[terrainNeighbor.neighborYN].Reinterpret<BlockType>();
            DynamicBuffer<BlockType> neighborZP = default;
            if (terrainNeighbor.neighborZP != Entity.Null)
                neighborZP = terrainBufferLookup[terrainNeighbor.neighborZP].Reinterpret<BlockType>();
            DynamicBuffer<BlockType> neighborZN = default;
            if (terrainNeighbor.neighborZN != Entity.Null)
                neighborZN = terrainBufferLookup[terrainNeighbor.neighborZN].Reinterpret<BlockType>();*/
            


            DynamicBuffer<TerrainStructuresToSpawn> delaySpawning = new DynamicBuffer<TerrainStructuresToSpawn>();
            bool hasChanged = false;
            // Loop through structures
            AreasUpdated areasUpdated = AreasUpdated.NONE;
            foreach (var structure in structuresToSpawnBuffer)
            {
                switch (structure.structureType)
                {
                    case StructureType.Tree:
                        if (!CheckBoundsTree(structure.localPos, structure.extents, terrainNeighbor))
                        {
                            // This structure overlaps with an area that has not been created yet
                            delaySpawning.Add(structure);   
                            break;
                        }

                        areasUpdated = GenerateTreeStructure(structure, ref terrainBlocks, ref colMin, ref colMax,ref neighborYP, ref neighborYPColMin, ref neighborYPColMax
                            /*ref neighborXP, ref neighborXN, ref neighborYP, ref neighborYN, ref neighborZN, ref neighborZP*/);
                        hasChanged = true;
                        
                        break;
                }
            }
            
            structuresToSpawnBuffer.Clear();
            if (!delaySpawning.IsEmpty)
            {
                structuresToSpawnBuffer.CopyFrom(delaySpawning);
            }
            else
            {
                // Disable GenStructures flag on this area
                ecb.SetComponentEnabled<GenStructures>(index, entity, false);
            }
            
            // Set remeshing flag on this and neighbor areas if necessary
            if (hasChanged)
                ecb.SetComponentEnabled<Remesh>(index, entity, false);
            if ((areasUpdated & AreasUpdated.XP) == AreasUpdated.XP)
                ecb.SetComponentEnabled<Remesh>(index, terrainNeighbor.neighborXP, false);
            if ((areasUpdated & AreasUpdated.XN) == AreasUpdated.XN)
                ecb.SetComponentEnabled<Remesh>(index, terrainNeighbor.neighborXN, false);
            if ((areasUpdated & AreasUpdated.YP) == AreasUpdated.YP)
                ecb.SetComponentEnabled<Remesh>(index, terrainNeighbor.neighborYP, false);
            if ((areasUpdated & AreasUpdated.YN) == AreasUpdated.YN)
                ecb.SetComponentEnabled<Remesh>(index, terrainNeighbor.neighborYN, false);
            if ((areasUpdated & AreasUpdated.ZP) == AreasUpdated.ZP)
                ecb.SetComponentEnabled<Remesh>(index, terrainNeighbor.neighborZP, false);
            if ((areasUpdated & AreasUpdated.ZN) == AreasUpdated.ZN)
                ecb.SetComponentEnabled<Remesh>(index, terrainNeighbor.neighborZN, false);
            
        }

        [Flags]
        private enum AreasUpdated
        {
            NONE = 0,
            XP = 1,
            XN = 2,
            YP = 4,
            YN = 8,
            ZP = 16,
            ZN = 32
        }

        private bool CheckBoundsTree(int3 baseLoc, int3 extent, TerrainNeighbors terrainNeighbors )
        {
            if (baseLoc.x - extent.x < 0 && terrainNeighbors.neighborXN == Entity.Null)
            {
                return false;
            }
            if (baseLoc.x + extent.x >= Env.AREA_SIZE && terrainNeighbors.neighborXP == Entity.Null)
            {
                return false;
            }
            if (baseLoc.z - extent.z < 0 && terrainNeighbors.neighborZN == Entity.Null)
            {
                return false;
            }
            if (baseLoc.z + extent.z >= Env.AREA_SIZE && terrainNeighbors.neighborZP == Entity.Null)
            {
                return false;
            }
            if (baseLoc.y + extent.y >= Env.AREA_SIZE && terrainNeighbors.neighborYP == Entity.Null)
            {
                return false;
            }

            return true;
        }
        
        private AreasUpdated GenerateTreeStructure(TerrainStructuresToSpawn structure,
            ref DynamicBuffer<BlockType> terrainBlocks, ref DynamicBuffer<byte> colMin, ref DynamicBuffer<byte> colMax,
            ref DynamicBuffer<BlockType> neighborYP, ref DynamicBuffer<byte> neighborYPColMin, ref DynamicBuffer<byte> neighborYPColMax
            /*ref DynamicBuffer<BlockType> neighborXP,ref DynamicBuffer<BlockType> neighborXN,
            ref DynamicBuffer<BlockType> neighborYP, ref DynamicBuffer<BlockType> neighborYN,
            ref DynamicBuffer<BlockType> neighborZP, ref DynamicBuffer<BlockType> neighborZN*/)
        {
            AreasUpdated aU = AreasUpdated.NONE;
            //structure.localPos;
            int blockAccess = TerrainUtilities.BlockLocationToIndex(ref structure.localPos);
            int colAccess = TerrainUtilities.BlockLocationToColIndex(ref structure.localPos);
            for (int y = 0; y < structure.extents.y; y++)
            {
                int localY = structure.localPos.y + y;
                if (localY  >= Env.AREA_SIZE)
                {
                    aU &= AreasUpdated.YP;
                    int neighborY = localY - Env.AREA_SIZE;
                    neighborYP[neighborY] = BlockType.Tin;
                    if (neighborYPColMin[colAccess] > neighborY)
                        neighborYPColMin[colAccess] = (byte)neighborY;
                    if (neighborYPColMax[colAccess] < neighborY)
                        neighborYPColMax[colAccess] = (byte)neighborY;
                    
                }
                else
                {
                    terrainBlocks[blockAccess + y] = BlockType.Tin;
                    if (colMin[colAccess] > localY)
                        colMin[colAccess] = (byte)localY;
                    if (colMax[colAccess] < localY)
                        colMax[colAccess] = (byte)localY;
                }
            }
            return aU;
        }
    }
    

}